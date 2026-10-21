using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    internal enum ServerRollPhase { Validating, Rolling, Reserved, Free }

    internal sealed class ServerRoll {
        internal string RollId;
        internal ZDOID Drop;
        internal long Nonce;
        // Null when the server itself asked (a mod running on the server, or the listen host's own player).
        internal ServerPeer Requester;
        internal int RequestNo;
        internal readonly List<long> Members = new List<long>();
        internal readonly List<long> NotEligible = new List<long>();
        internal readonly Dictionary<long, RollChoice> Votes = new Dictionary<long, RollChoice>();
        internal string Label = "";
        internal string GroupId = "";
        internal string Source = "";
        internal float RollSeconds;
        internal float ReserveSeconds;
        internal ServerRollPhase Phase;
        // Server realtime: end of validation, of voting, of the reservation, or when a free entry is dropped.
        internal float Deadline;
        internal int PrefabHash;
        internal string Prefab = "";
        internal byte[] ItemBytes;
        internal Vector3 Position;
        internal RollOutcome Outcome;
        internal long WinnerPid;
        internal string WinnerName = "";
        internal List<RollTally> Tallies = new List<RollTally>();
    }

    // The server's half of a roll: it checks the request, tells the members, counts the votes, rolls the
    // numbers, and announces who may pick the drop up and until when. It never writes the drop's ZDO.
    //
    // A request is checked against the drop's ZDO as the server holds it. The request travels over the
    // connection, which is faster than the ZDO stream, so a request waits (Validating) up to a few seconds
    // for the stamped drop to arrive.
    internal static class RollServer {
        private const float ValidateSeconds = 5f;
        private const int MaxItemBytes = 32 * 1024;
        private const int MaxOptionsLength = 1024;
        private const int MaxTextLength = 64;
        private const int MaxActivePerRequester = 30;
        private const float RequesterSpawnWindow = 30f;

        private const string Wait = "";

        private static readonly Dictionary<ZDOID, ServerRoll> rolls = new Dictionary<ZDOID, ServerRoll>();
        private static readonly System.Random random = new System.Random();
        private static ZDOMan hookedZdoMan;

        internal static IEnumerable<ServerRoll> All => rolls.Values;

        private static float Now => Time.realtimeSinceStartup;

        // ---------------------------------------------------------------------------------------------
        // Starting
        // ---------------------------------------------------------------------------------------------

        internal static void OnStart(ServerPeer from, ZPackage pkg) {
            int requestNo = pkg.ReadInt();
            ZDOID drop = pkg.ReadZDOID();
            long nonce = pkg.ReadLong();
            int count = pkg.ReadInt();
            if (count < 0 || count > 256) {
                Ack(from, requestNo, drop, nonce, false, "badRequest");
                return;
            }
            List<long> members = new List<long>();
            for (int i = 0; i < count; i++) {
                long pid = pkg.ReadLong();
                if (pid != 0 && members.Contains(pid) == false) { members.Add(pid); }
            }
            string options = pkg.ReadString();
            Begin(from, requestNo, drop, nonce, members, options, out _);
        }

        // Returns null when the roll was taken on (it may still be validating), otherwise the refusal reason,
        // which has also been sent back to a remote requester.
        internal static string Begin(ServerPeer from, int requestNo, ZDOID drop, long nonce, List<long> memberIds, string optionsJson, out ServerRoll roll) {
            roll = null;
            string error = Check(from, drop, nonce, memberIds, optionsJson, out IDictionary<string, object> options);
            if (error != null) {
                // A refused second roll on a drop that is already rolling must not free the first one.
                Refuse(from, requestNo, drop, nonce, error, broadcastFree: error != "alreadyRolling");
                return error;
            }

            roll = new ServerRoll {
                RollId = RollIds.Format(drop, nonce),
                Drop = drop,
                Nonce = nonce,
                Requester = from,
                RequestNo = requestNo,
                RollSeconds = Mathf.Clamp(Json.GetFloat(options, "rollSeconds", LootConfig.RollSeconds.Value), 5f, LootConfig.RollSeconds.Value),
                ReserveSeconds = Mathf.Clamp(Json.GetFloat(options, "reserveSeconds", LootConfig.ReserveMinutes.Value * 60f), 0f, LootConfig.ReserveMinutes.Value * 60f),
                Label = CleanText(Json.GetString(options, "label")),
                GroupId = CleanText(Json.GetString(options, "groupId")),
                Source = CleanText(Json.GetString(options, "source")),
                Phase = ServerRollPhase.Validating,
                Deadline = Now + ValidateSeconds,
            };
            // In the caller's order; players past MaxRollMembers are listed as not eligible rather than refusing
            // the whole roll (a crowded fight still gets one).
            int limit = Mathf.Max(1, LootConfig.MaxRollMembers.Value);
            foreach (long pid in memberIds) {
                ServerPeer member = ServerSession.FindByPid(pid);
                if (member != null && member.HasIff && roll.Members.Count < limit) {
                    roll.Members.Add(pid);
                } else {
                    roll.NotEligible.Add(pid);
                }
            }
            if (roll.Members.Count == 0) {
                Refuse(from, requestNo, drop, nonce, "noEligibleMembers", broadcastFree: true);
                roll = null;
                return "noEligibleMembers";
            }
            rolls[drop] = roll;
            // The server's own requests (and any whose drop already arrived) start at once.
            Advance(roll);
            return null;
        }

        private static string Check(ServerPeer from, ZDOID drop, long nonce, List<long> memberIds, string optionsJson, out IDictionary<string, object> options) {
            options = null;
            // The drop first: a request for a drop that is already rolling is refused as such, whatever else
            // is wrong with it, and that refusal never touches the running roll.
            if (drop.IsNone() || nonce == 0) { return "badRequest"; }
            if (rolls.TryGetValue(drop, out ServerRoll existing) && existing.Phase != ServerRollPhase.Free) { return "alreadyRolling"; }
            if (LootConfig.EnableLootRolls.Value == false) { return "disabled"; }
            if (from != null) {
                if (from.HasIff == false || from.Verified == false) { return "notVerified"; }
                if (TakeToken(from) == false) { return "rateLimited"; }
                if (rolls.Values.Count(r => r.Requester == from && r.Phase != ServerRollPhase.Free) >= MaxActivePerRequester) { return "tooManyActive"; }
            }
            if (rolls.Values.Count(r => r.Phase != ServerRollPhase.Free) >= LootConfig.MaxActiveRolls.Value) { return "serverBusy"; }
            if (memberIds == null || memberIds.Count == 0) { return "noMembers"; }
            optionsJson = optionsJson ?? "";
            if (optionsJson.Length > MaxOptionsLength) { return "badOptions"; }
            if (optionsJson.Trim().Length > 0) {
                options = Json.ParseObject(optionsJson);
                if (options == null) { return "badOptions"; }
            }
            return null;
        }

        private static bool TakeToken(ServerPeer peer) {
            float perMinute = Mathf.Max(1, LootConfig.RollStartsPerMinutePerPlayer.Value);
            float burst = Mathf.Max(5f, perMinute / 2f);
            float now = Now;
            if (peer.Tokens < 0f) {
                peer.Tokens = burst;
            } else {
                peer.Tokens = Mathf.Min(burst, peer.Tokens + (now - peer.TokensAt) * perMinute / 60f);
            }
            peer.TokensAt = now;
            if (peer.Tokens < 1f) { return false; }
            peer.Tokens -= 1f;
            return true;
        }

        private static string CleanText(string text) {
            if (string.IsNullOrEmpty(text)) { return ""; }
            text = text.Replace("<", "").Replace(">", "").Replace("\n", " ");
            return text.Length > MaxTextLength ? text.Substring(0, MaxTextLength) : text;
        }

        // Wait while the drop has not arrived (or arrived without this roll's stamp); otherwise a reason.
        private static string Validate(ServerRoll roll) {
            ZDO zdo = ZDOMan.instance?.GetZDO(roll.Drop);
            if (zdo == null) { return Wait; }
            if (RollStamp.ReadNonce(zdo) != roll.Nonce) { return Wait; }

            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
            if (prefab == null || prefab.GetComponent<ItemDrop>() == null) { return "notAnItem"; }
            if (zdo.GetBool(ZDOVars.s_piece)) { return "notAnItem"; }

            if (roll.Requester != null) {
                long uid = roll.Requester.Uid;
                bool created = roll.Drop.UserID == uid;
                bool ownsFresh = zdo.GetOwner() == uid && SpawnAgeSeconds(zdo) < RequesterSpawnWindow;
                if (created == false && ownsFresh == false) { return "notYourDrop"; }
                float maxDistance = LootConfig.MaxRequesterDistance.Value;
                if (maxDistance > 0f && ServerSession.TryGetPosition(roll.Requester, out Vector3 at)
                    && Vector3.Distance(at, zdo.GetPosition()) > maxDistance) {
                    return "tooFar";
                }
            }

            byte[] bytes = zdo.GetByteArray(ZDOVars.s_itemData);
            if (bytes == null || bytes.Length <= 2) { return Wait; }
            if (bytes.Length > MaxItemBytes) { return "itemTooLarge"; }

            roll.PrefabHash = zdo.GetPrefab();
            roll.Prefab = prefab.name;
            roll.ItemBytes = bytes;
            roll.Position = zdo.GetPosition();
            return null;
        }

        private static double SpawnAgeSeconds(ZDO zdo) {
            long spawned = zdo.GetLong(ZDOVars.s_spawnTime);
            if (spawned <= 0 || ZNet.instance == null) { return double.MaxValue; }
            return (ZNet.instance.GetTime().Ticks - spawned) / (double)TimeSpan.TicksPerSecond;
        }

        private static void Advance(ServerRoll roll) {
            if (roll.Phase != ServerRollPhase.Validating) { return; }
            string result = Validate(roll);
            if (result == null) {
                StartRolling(roll);
            } else if (result != Wait) {
                Fail(roll, result);
            } else if (Now >= roll.Deadline) {
                Fail(roll, ZDOMan.instance?.GetZDO(roll.Drop) == null ? "dropNotFound" : "stampMismatch");
            }
        }

        private static void StartRolling(ServerRoll roll) {
            roll.Phase = ServerRollPhase.Rolling;
            roll.Deadline = Now + roll.RollSeconds;
            Ack(roll.Requester, roll.RequestNo, roll.Drop, roll.Nonce, true, "");
            RollEvents.RaiseAccepted(roll.RollId, "server");
            BroadcastLock(roll);

            long requesterPid = roll.Requester?.Pid ?? 0L;
            ZPackage prompt = IffNet.NewPackage();
            prompt.Write(roll.Drop);
            prompt.Write(roll.Nonce);
            prompt.Write(roll.PrefabHash);
            prompt.Write(roll.ItemBytes);
            prompt.Write(roll.Position);
            prompt.Write(roll.RollSeconds);
            prompt.Write(roll.Label);
            prompt.Write(roll.GroupId);
            prompt.Write(roll.Source);
            prompt.Write(requesterPid);
            prompt.Write(roll.Members.Count);
            foreach (long pid in roll.Members) { prompt.Write(pid); }
            IffNet.ToPeers(MemberPeers(roll), IffNet.RollPrompt, prompt);
        }

        // A roll that never got going.
        private static void Fail(ServerRoll roll, string reason) {
            Logger.LogDebug($"Roll {roll.RollId} refused: {reason}.");
            rolls.Remove(roll.Drop);
            Refuse(roll.Requester, roll.RequestNo, roll.Drop, roll.Nonce, reason, broadcastFree: true);
        }

        private static void Refuse(ServerPeer from, int requestNo, ZDOID drop, long nonce, string reason, bool broadcastFree) {
            Ack(from, requestNo, drop, nonce, false, reason);
            RollEvents.RaiseRefused(RollIds.Format(drop, nonce), reason, "server");
            if (broadcastFree && drop.IsNone() == false && nonce != 0) {
                // Kept briefly so the drop's stamp reads as free for anyone who joins in the meantime.
                ServerRoll free = new ServerRoll {
                    RollId = RollIds.Format(drop, nonce), Drop = drop, Nonce = nonce,
                    Phase = ServerRollPhase.Free, Deadline = Now + ServerSession.StartGraceSeconds + 1f,
                };
                // Never over a running roll of another nonce: clients keep one lock entry per drop, so even the
                // broadcast would replace that roll's entry and read the drop as free.
                if (rolls.TryGetValue(drop, out ServerRoll existing) == false || existing.Phase == ServerRollPhase.Free || existing.Nonce == nonce) {
                    rolls[drop] = free;
                    BroadcastLock(free);
                }
            }
        }

        private static void Ack(ServerPeer to, int requestNo, ZDOID drop, long nonce, bool accepted, string reason) {
            if (to == null) { return; }
            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(requestNo);
            pkg.Write(drop);
            pkg.Write(nonce);
            pkg.Write(accepted);
            pkg.Write(reason ?? "");
            IffNet.ToPeer(to, IffNet.RollAck, pkg);
        }

        // ---------------------------------------------------------------------------------------------
        // Voting and results
        // ---------------------------------------------------------------------------------------------

        internal static void OnVote(ServerPeer from, ZPackage pkg) {
            ZDOID drop = pkg.ReadZDOID();
            long nonce = pkg.ReadLong();
            byte choice = pkg.ReadByte();
            if (from.Verified == false) { return; }
            if (rolls.TryGetValue(drop, out ServerRoll roll) == false || roll.Nonce != nonce || roll.Phase != ServerRollPhase.Rolling) { return; }
            if (roll.Members.Contains(from.Pid) == false || roll.Votes.ContainsKey(from.Pid)) { return; }
            if (choice < (byte)RollChoice.Pass || choice > (byte)RollChoice.Need) { return; }
            RecordVote(roll, from.Pid, (RollChoice)choice);
        }

        private static void RecordVote(ServerRoll roll, long pid, RollChoice choice) {
            roll.Votes[pid] = choice;
            ZPackage progress = IffNet.NewPackage();
            progress.Write(roll.Drop);
            progress.Write(roll.Nonce);
            progress.Write(pid);
            progress.Write(roll.Votes.Count);
            progress.Write(roll.Members.Count);
            IffNet.ToPeers(MemberPeers(roll), IffNet.RollProgress, progress);
            RollEvents.RaiseVote(roll.RollId, pid, roll.Votes.Count, roll.Members.Count, "server");
            if (roll.Votes.Count >= roll.Members.Count) { Finish(roll); }
        }

        private static void Finish(ServerRoll roll) {
            roll.Outcome = RollResolver.Resolve(roll.Members, roll.Votes, (min, max) => random.Next(min, max), out long winner, out List<RollTally> tallies);
            roll.Tallies = tallies;
            roll.WinnerPid = winner;
            roll.WinnerName = winner != 0 ? NameOf(winner) : "";
            if (roll.Outcome == RollOutcome.Won && roll.ReserveSeconds > 0f) {
                roll.Phase = ServerRollPhase.Reserved;
                roll.Deadline = Now + roll.ReserveSeconds;
            } else {
                roll.Phase = ServerRollPhase.Free;
                roll.Deadline = Now + ServerSession.StartGraceSeconds + 1f;
            }
            SendResult(roll);
            BroadcastLock(roll);
        }

        private static void SendResult(ServerRoll roll) {
            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(roll.Drop);
            pkg.Write(roll.Nonce);
            pkg.Write((byte)roll.Outcome);
            pkg.Write(roll.WinnerPid);
            pkg.Write(roll.WinnerName ?? "");
            pkg.Write(roll.Phase == ServerRollPhase.Reserved ? roll.ReserveSeconds : 0f);
            pkg.Write(roll.Prefab ?? "");
            pkg.Write(roll.Label ?? "");
            pkg.Write(roll.GroupId ?? "");
            pkg.Write(roll.Tallies.Count);
            foreach (RollTally tally in roll.Tallies) {
                pkg.Write(tally.Pid);
                pkg.Write(NameOf(tally.Pid));
                pkg.Write((byte)tally.Choice);
                pkg.Write(tally.Roll);
                pkg.Write((byte)Mathf.Min(255, tally.Rerolls));
            }
            pkg.Write(roll.NotEligible.Count);
            foreach (long pid in roll.NotEligible) { pkg.Write(pid); }

            List<ServerPeer> recipients = MemberPeers(roll);
            if (roll.Requester != null && roll.Requester.HasIff && recipients.Contains(roll.Requester) == false) { recipients.Add(roll.Requester); }
            IffNet.ToPeers(recipients, IffNet.RollResult, pkg);

            RollEvents.RaiseResult(roll.RollId, "server", roll.Outcome, roll.WinnerPid, roll.WinnerName,
                roll.Phase == ServerRollPhase.Reserved ? roll.ReserveSeconds : 0f, roll.Prefab, roll.Label, roll.GroupId,
                roll.Tallies, NameOf, roll.NotEligible);
        }

        private static string NameOf(long pid) {
            ServerPeer peer = ServerSession.FindByPid(pid);
            return peer != null && string.IsNullOrEmpty(peer.Name) == false ? peer.Name : pid.ToString();
        }

        private static List<ServerPeer> MemberPeers(ServerRoll roll) {
            List<ServerPeer> peers = new List<ServerPeer>();
            foreach (long pid in roll.Members) {
                ServerPeer peer = ServerSession.FindByPid(pid);
                if (peer != null && peer.HasIff) { peers.Add(peer); }
            }
            return peers;
        }

        // ---------------------------------------------------------------------------------------------
        // Ticking, leaving, vanishing drops
        // ---------------------------------------------------------------------------------------------

        internal static void Tick(float now) {
            HookZdoMan();
            if (rolls.Count == 0) { return; }
            foreach (ServerRoll roll in rolls.Values.ToList()) {
                switch (roll.Phase) {
                    case ServerRollPhase.Validating:
                        Advance(roll);
                        break;
                    case ServerRollPhase.Rolling:
                        if (now >= roll.Deadline || MemberPeers(roll).Count == 0) { Finish(roll); }
                        break;
                    case ServerRollPhase.Reserved:
                        if (now >= roll.Deadline) {
                            roll.Phase = ServerRollPhase.Free;
                            roll.Deadline = now + ServerSession.StartGraceSeconds + 1f;
                            BroadcastLock(roll);
                        }
                        break;
                    case ServerRollPhase.Free:
                        if (now >= roll.Deadline) { rolls.Remove(roll.Drop); }
                        break;
                }
            }
        }

        // A member who leaves mid-roll passes. Reservations are kept by player id, so a winner who reconnects
        // still has theirs.
        internal static void OnPeerLeft(ServerPeer peer) {
            if (peer.Verified == false) { return; }
            foreach (ServerRoll roll in rolls.Values.ToList()) {
                if (roll.Phase == ServerRollPhase.Rolling && roll.Members.Contains(peer.Pid) && roll.Votes.ContainsKey(peer.Pid) == false) {
                    RecordVote(roll, peer.Pid, RollChoice.Pass);
                }
                if (roll.Requester == peer) { roll.Requester = null; }
            }
        }

        private static void HookZdoMan() {
            ZDOMan current = ZDOMan.instance;
            if (current == null || ReferenceEquals(current, hookedZdoMan)) { return; }
            hookedZdoMan = current;
            current.m_onZDODestroyed -= OnZdoDestroyed;
            current.m_onZDODestroyed += OnZdoDestroyed;
        }

        // The drop was picked up (or otherwise destroyed). Only the server sees every destroy.
        private static void OnZdoDestroyed(ZDO zdo) {
            if (zdo == null || rolls.TryGetValue(zdo.m_uid, out ServerRoll roll) == false) { return; }
            long takerUid = zdo.GetOwner();
            ServerPeer taker = ServerSession.FindByUid(takerUid);
            switch (roll.Phase) {
                case ServerRollPhase.Validating:
                    // Nobody was prompted yet; only the requester is waiting for an answer.
                    Ack(roll.Requester, roll.RequestNo, roll.Drop, roll.Nonce, false, "dropGone");
                    RollEvents.RaiseRefused(roll.RollId, "dropGone", "server");
                    break;
                case ServerRollPhase.Rolling:
                    roll.Outcome = RollOutcome.Cancelled;
                    roll.Tallies = roll.Members.Select(pid => new RollTally {
                        Pid = pid, Choice = roll.Votes.TryGetValue(pid, out RollChoice c) ? c : RollChoice.None,
                    }).ToList();
                    roll.Phase = ServerRollPhase.Free;
                    if (roll.ItemBytes != null) { SendResult(roll); }
                    Announce(roll, taker, "rolling");
                    break;
                case ServerRollPhase.Reserved:
                    if (taker == null || taker.Pid != roll.WinnerPid) { Announce(roll, taker, "reserved"); }
                    break;
            }
            rolls.Remove(zdo.m_uid);
        }

        private static void Announce(ServerRoll roll, ServerPeer taker, string phase) {
            string who = taker != null ? taker.Name : "someone";
            Logger.LogWarning($"{who} took {roll.Prefab} while it was {phase} (roll {roll.RollId}, winner {roll.WinnerName}).");
            if (LootConfig.AnnounceLockedPickups.Value == false || taker != null && taker.Pid == roll.WinnerPid) { return; }
            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(roll.Prefab ?? "");
            pkg.Write(roll.Label ?? "");
            pkg.Write(who);
            pkg.Write(roll.WinnerName ?? "");
            IffNet.ToPeers(MemberPeers(roll), IffNet.RollNotice, pkg);
        }

        // ---------------------------------------------------------------------------------------------
        // Lock table
        // ---------------------------------------------------------------------------------------------

        internal static bool TryGetLock(ZDOID drop, long nonce, out LockPhase phase, out long winner) {
            phase = LockPhase.Free;
            winner = 0;
            if (rolls.TryGetValue(drop, out ServerRoll roll) == false || roll.Nonce != nonce) { return false; }
            phase = PhaseOf(roll);
            winner = roll.WinnerPid;
            return true;
        }

        private static LockPhase PhaseOf(ServerRoll roll) {
            switch (roll.Phase) {
                case ServerRollPhase.Validating:
                case ServerRollPhase.Rolling:
                    return LockPhase.Rolling;
                case ServerRollPhase.Reserved:
                    return Now < roll.Deadline ? LockPhase.Reserved : LockPhase.Free;
                default:
                    return LockPhase.Free;
            }
        }

        private static float SecondsLeft(ServerRoll roll) {
            switch (roll.Phase) {
                case ServerRollPhase.Validating: return Mathf.Max(0f, roll.Deadline - Now) + roll.RollSeconds;
                case ServerRollPhase.Rolling:
                case ServerRollPhase.Reserved: return Mathf.Max(0f, roll.Deadline - Now);
                default: return 0f;
            }
        }

        private static void WriteLock(ZPackage pkg, ServerRoll roll) {
            pkg.Write(roll.Drop);
            pkg.Write(roll.Nonce);
            pkg.Write((byte)PhaseOf(roll));
            pkg.Write(roll.WinnerPid);
            pkg.Write(roll.WinnerName ?? "");
            pkg.Write(SecondsLeft(roll));
            pkg.Write(roll.Label ?? "");
        }

        private static void BroadcastLock(ServerRoll roll) {
            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(false);
            pkg.Write(1);
            WriteLock(pkg, roll);
            IffNet.ToPeers(ServerSession.IffPeers.ToList(), IffNet.Locks, pkg);
            RollEvents.RaiseLock(roll.Drop, roll.Nonce, PhaseOf(roll), roll.WinnerPid, SecondsLeft(roll), "server");
        }

        internal static void SendSnapshotTo(ServerPeer peer) {
            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(true);
            pkg.Write(rolls.Count);
            foreach (ServerRoll roll in rolls.Values) { WriteLock(pkg, roll); }
            IffNet.ToPeer(peer, IffNet.Locks, pkg);
        }

        internal static void Reset() {
            rolls.Clear();
            hookedZdoMan = null;
        }
    }
}
