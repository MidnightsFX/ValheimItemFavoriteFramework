using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // A roll this client was asked to vote on.
    internal sealed class ClientRoll {
        internal string RollId;
        internal ZDOID Drop;
        internal long Nonce;
        // Rebuilt from the bytes the server read off the drop's ZDO, so it works without the drop being loaded.
        internal ItemDrop.ItemData Item;
        internal string Prefab = "";
        internal string Name = "";
        internal string Label = "";
        internal string GroupId = "";
        internal string Source = "";
        internal long RequesterPid;
        internal List<long> Members = new List<long>();
        internal Vector3 Position;
        internal float StartedAt;
        internal float EndsAt;
        internal RollChoice MyVote;
        internal RollChoice Preselected;
        internal bool Wishlisted;
        internal bool AutoNeed;
        internal List<string> WantedBy = new List<string>();
        internal int Voted;
        internal int Of;

        internal float SecondsLeft => Mathf.Max(0f, EndsAt - Time.realtimeSinceStartup);
    }

    // This client's side of rolls: asking for one (as a mod's caller), being asked to vote, voting, hearing
    // results, and picking up what it won.
    internal static class RollClient {
        private static readonly List<ClientRoll> open = new List<ClientRoll>();
        private static readonly Dictionary<ZDOID, float> won = new Dictionary<ZDOID, float>();
        private static int nextRequestNo = 1;
        private static float nextAutoLootAt;
        private static float lastFullMessageAt = -999f;

        internal static IReadOnlyList<ClientRoll> Open => open;

        // Bumped whenever the open list changes, for the roll window.
        internal static int Revision { get; private set; }

        // ---------------------------------------------------------------------------------------------
        // Starting (the API's caller side)
        // ---------------------------------------------------------------------------------------------

        // Stamps the drop and asks whoever runs rolls. The drop must be this machine's (it just spawned it).
        // On the server (dedicated or listen host) the roll starts at once; on a client the server's answer
        // comes back as an accepted or refused event.
        internal static bool StartRoll(ItemDrop drop, List<long> members, string optionsJson, out string rollId, out string error) {
            rollId = "";
            error = "";
            ZNet znet = ZNet.instance;
            if (znet == null) { error = "notInWorld"; return false; }
            bool isServer = znet.IsServer();
            if (isServer ? LootConfig.EnableLootRolls.Value == false : ClientSession.RollsEnabled == false) {
                error = isServer || ClientSession.Welcomed ? "disabled" : "serverHasNoIff";
                return false;
            }
            if (drop == null || drop.m_nview == null || drop.m_nview.IsValid() == false) { error = "invalidDrop"; return false; }
            if (drop.m_nview.IsOwner() == false) { error = "notOwner"; return false; }
            if (members == null || members.Count == 0) { error = "noMembers"; return false; }
            ZDO zdo = drop.m_nview.GetZDO();
            if (RollStamp.ReadNonce(zdo) != 0 && RollLocks.Resolve(zdo, 0) != LockState.Free) { error = "alreadyRolling"; return false; }

            // Vanilla only writes the item to the ZDO in Start, the frame after spawning, and mods (EpicLoot)
            // change m_itemData in between. Saving now means the stamped snapshot carries the final item.
            drop.Save();
            long nonce = RollStamp.NewNonce();
            RollStamp.Write(zdo, nonce);
            rollId = RollIds.Format(zdo.m_uid, nonce);

            if (isServer) {
                string refused = RollServer.Begin(null, 0, zdo.m_uid, nonce, members, optionsJson, out _);
                if (refused != null) {
                    error = refused;
                    return false;
                }
                return true;
            }

            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(nextRequestNo++);
            pkg.Write(zdo.m_uid);
            pkg.Write(nonce);
            pkg.Write(members.Count);
            foreach (long pid in members) { pkg.Write(pid); }
            pkg.Write(optionsJson ?? "");
            IffNet.ToServer(IffNet.RollStart, pkg);
            return true;
        }

        internal static void OnAck(ZPackage pkg) {
            pkg.ReadInt();
            ZDOID drop = pkg.ReadZDOID();
            long nonce = pkg.ReadLong();
            bool accepted = pkg.ReadBool();
            string reason = pkg.ReadString();
            string rollId = RollIds.Format(drop, nonce);
            if (accepted) {
                RollEvents.RaiseAccepted(rollId, "client");
            } else {
                Logger.LogDebug($"The server refused roll {rollId}: {reason}.");
                RollEvents.RaiseRefused(rollId, reason, "client");
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Being asked
        // ---------------------------------------------------------------------------------------------

        internal static void OnPrompt(ZPackage pkg) {
            ClientRoll roll = new ClientRoll {
                Drop = pkg.ReadZDOID(),
                Nonce = pkg.ReadLong(),
            };
            int prefabHash = pkg.ReadInt();
            byte[] bytes = pkg.ReadByteArray();
            roll.Position = pkg.ReadVector3();
            float seconds = pkg.ReadSingle();
            roll.Label = pkg.ReadString();
            roll.GroupId = pkg.ReadString();
            roll.Source = pkg.ReadString();
            roll.RequesterPid = pkg.ReadLong();
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++) { roll.Members.Add(pkg.ReadLong()); }

            roll.RollId = RollIds.Format(roll.Drop, roll.Nonce);
            roll.StartedAt = Time.realtimeSinceStartup;
            roll.EndsAt = roll.StartedAt + seconds;
            roll.Of = roll.Members.Count;
            roll.Item = Rebuild(prefabHash, bytes, out string prefab);
            roll.Prefab = prefab;
            roll.Name = roll.Item != null ? ItemNames.Of(roll.Item) : prefab;

            if (roll.Item != null) {
                WishlistEntry entry = Wishlists.LocalMatch(roll.Item);
                roll.Wishlisted = entry != null;
                roll.AutoNeed = entry != null && entry.AutoNeed;
                roll.WantedBy = Wishlists.WantedByNames(roll.Item);
            }
            if (roll.Wishlisted && LootConfig.PreselectNeedForWishlisted.Value) { roll.Preselected = RollChoice.Need; }

            open.RemoveAll(r => r.Drop == roll.Drop);
            open.Add(roll);
            Revision++;
            RollEvents.RaisePrompt(roll);

            if (roll.AutoNeed && LootConfig.AutoVoteNeedForWishlisted.Value) {
                Vote(roll, RollChoice.Need);
                return;
            }
            Announce(roll, seconds);
        }

        private static void Announce(ClientRoll roll, float seconds) {
            string label = roll.Label.Length > 0 ? $" ({roll.Label})" : "";
            Player local = Player.m_localPlayer;
            if (local != null) {
                local.Message(MessageHud.MessageType.TopLeft, Localize("$iff_roll_prompt_short", roll.Name), 0, roll.Item?.GetIcon());
            }
            if (LootConfig.ShowRollWindow.Value == false || RollWindow.IsUsable == false) {
                string wish = roll.Wishlisted ? " " + Localize("$iff_roll_on_wishlist") : "";
                ChatLine(Localize("$iff_roll_prompt_chat", roll.Name + label + wish, Mathf.RoundToInt(seconds).ToString(CultureInfo.InvariantCulture)));
            }
        }

        // The item from the drop's ZDO bytes, rebuilt the way ItemDrop.LoadFromZDO does it.
        internal static ItemDrop.ItemData Rebuild(int prefabHash, byte[] bytes, out string prefabName) {
            prefabName = "";
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabHash) : null;
            ItemDrop template = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (template == null) { return null; }
            prefabName = prefab.name;
            try {
                ItemDrop.ItemData item = template.m_itemData.Clone();
                item.m_dropPrefab = prefab;
                ZPackage pkg = new ZPackage(bytes);
                global::Version.Item version = (global::Version.Item)pkg.ReadByte();
                ItemDrop.ItemData.Load(pkg, item, version);
                return item;
            } catch (Exception e) {
                Logger.LogWarning($"Could not read the item of a roll ({prefab.name}): {e.Message}");
                return null;
            }
        }

        internal static void OnProgress(ZPackage pkg) {
            ZDOID drop = pkg.ReadZDOID();
            long nonce = pkg.ReadLong();
            long voter = pkg.ReadLong();
            int voted = pkg.ReadInt();
            int of = pkg.ReadInt();
            ClientRoll roll = open.FirstOrDefault(r => r.Drop == drop && r.Nonce == nonce);
            if (roll == null) { return; }
            roll.Voted = voted;
            roll.Of = of;
            Revision++;
            RollEvents.RaiseVote(roll.RollId, voter, voted, of, "client");
        }

        // ---------------------------------------------------------------------------------------------
        // Voting
        // ---------------------------------------------------------------------------------------------

        internal static bool Vote(ClientRoll roll, RollChoice choice) {
            if (roll == null || roll.MyVote != RollChoice.None || choice == RollChoice.None) { return false; }
            roll.MyVote = choice;
            Revision++;
            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(roll.Drop);
            pkg.Write(roll.Nonce);
            pkg.Write((byte)choice);
            IffNet.ToServer(IffNet.RollVote, pkg);
            return true;
        }

        internal static bool Vote(string rollId, RollChoice choice) {
            return Vote(open.FirstOrDefault(r => r.RollId == rollId), choice);
        }

        // The oldest roll this player has not voted on yet.
        internal static ClientRoll Oldest() => open.FirstOrDefault(r => r.MyVote == RollChoice.None);

        internal static int VoteAll(RollChoice choice, string groupId = null) {
            int count = 0;
            foreach (ClientRoll roll in open.ToList()) {
                if (groupId != null && roll.GroupId != groupId) { continue; }
                if (Vote(roll, choice)) { count++; }
            }
            return count;
        }

        // Per frame: the vote keys act on the oldest open roll, never while the player is typing.
        internal static void PollKeys() {
            if (open.Count == 0 || IsTyping()) { return; }
            RollChoice choice = RollChoice.None;
            if (Pressed(LootConfig.NeedKey.Value)) { choice = RollChoice.Need; }
            else if (Pressed(LootConfig.GreedKey.Value)) { choice = RollChoice.Greed; }
            else if (Pressed(LootConfig.PassKey.Value)) { choice = RollChoice.Pass; }
            if (choice != RollChoice.None) { Vote(Oldest(), choice); }
        }

        internal static bool IsTyping() {
            return (Chat.instance != null && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible()
                || Menu.IsVisible() || Minimap.InTextInput();
        }

        private static bool Pressed(BepInEx.Configuration.KeyboardShortcut shortcut) {
            if (shortcut.MainKey == KeyCode.None || ZInput.GetKeyDown(shortcut.MainKey, logWarning: false) == false) { return false; }
            foreach (KeyCode modifier in shortcut.Modifiers) {
                if (ZInput.GetKey(modifier, logWarning: false) == false) { return false; }
            }
            return true;
        }

        // ---------------------------------------------------------------------------------------------
        // Results
        // ---------------------------------------------------------------------------------------------

        internal static void OnResult(ZPackage pkg) {
            ZDOID drop = pkg.ReadZDOID();
            long nonce = pkg.ReadLong();
            RollOutcome outcome = (RollOutcome)pkg.ReadByte();
            long winner = pkg.ReadLong();
            string winnerName = pkg.ReadString();
            float reserveSeconds = pkg.ReadSingle();
            string prefab = pkg.ReadString();
            string label = pkg.ReadString();
            string groupId = pkg.ReadString();
            int count = pkg.ReadInt();
            List<RollTally> tallies = new List<RollTally>();
            Dictionary<long, string> names = new Dictionary<long, string>();
            for (int i = 0; i < count; i++) {
                RollTally tally = new RollTally { Pid = pkg.ReadLong() };
                names[tally.Pid] = pkg.ReadString();
                tally.Choice = (RollChoice)pkg.ReadByte();
                tally.Roll = pkg.ReadInt();
                tally.Rerolls = pkg.ReadByte();
                tallies.Add(tally);
            }
            int notEligibleCount = pkg.ReadInt();
            List<long> notEligible = new List<long>();
            for (int i = 0; i < notEligibleCount; i++) { notEligible.Add(pkg.ReadLong()); }

            string rollId = RollIds.Format(drop, nonce);
            ClientRoll roll = open.FirstOrDefault(r => r.Drop == drop && r.Nonce == nonce);
            if (roll != null) {
                open.Remove(roll);
                Revision++;
            }
            string itemName = roll != null ? roll.Name : Wishlists.DisplayName(prefab);
            RollEvents.RaiseResult(rollId, "client", outcome, winner, winnerName, reserveSeconds, prefab, label, groupId, tallies,
                pid => names.TryGetValue(pid, out string n) ? n : ClientSession.NameOf(pid), notEligible);

            long me = RollLocks.LocalPid;
            bool member = tallies.Any(t => t.Pid == me);
            if (member == false) { return; }

            ReportResult(outcome, winner, winnerName, itemName, tallies, names, me);
            if (outcome == RollOutcome.Won && winner == me && reserveSeconds > 0f) {
                won[drop] = Time.realtimeSinceStartup + reserveSeconds;
            }
        }

        private static void ReportResult(RollOutcome outcome, long winner, string winnerName, string itemName,
            List<RollTally> tallies, Dictionary<long, string> names, long me) {
            Player local = Player.m_localPlayer;
            string line;
            switch (outcome) {
                case RollOutcome.Won:
                    RollTally best = tallies.FirstOrDefault(t => t.Pid == winner);
                    string how = best != null ? $"{Localize("$iff_roll_choice_" + RollIds.ChoiceName(best.Choice))} {best.Roll}" : "";
                    line = Localize("$iff_roll_result_won", winnerName, itemName, how);
                    if (local != null) {
                        local.Message(winner == me ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft,
                            winner == me ? Localize("$iff_roll_you_won", itemName) : line);
                    }
                    break;
                case RollOutcome.AllPassed:
                    line = Localize("$iff_roll_result_all_passed", itemName);
                    break;
                default:
                    line = Localize("$iff_roll_result_cancelled", itemName);
                    break;
            }
            if (LootConfig.RollResultsInChat.Value) {
                List<string> parts = new List<string>();
                foreach (RollTally tally in tallies.OrderByDescending(t => t.Choice).ThenByDescending(t => t.Roll)) {
                    string name = names.TryGetValue(tally.Pid, out string n) ? n : ClientSession.NameOf(tally.Pid);
                    string choice = Localize("$iff_roll_choice_" + RollIds.ChoiceName(tally.Choice));
                    parts.Add(tally.Roll > 0 ? $"{name} {choice} {tally.Roll}" : $"{name} {choice}");
                }
                ChatLine(parts.Count > 0 ? $"{line} [{string.Join(", ", parts)}]" : line);
            }
        }

        internal static void OnNotice(ZPackage pkg) {
            string prefab = pkg.ReadString();
            pkg.ReadString();
            string taker = pkg.ReadString();
            string winner = pkg.ReadString();
            string item = Wishlists.DisplayName(prefab);
            ChatLine(string.IsNullOrEmpty(winner)
                ? Localize("$iff_roll_taken_while_rolling", taker, item)
                : Localize("$iff_roll_taken_reserved", taker, item, winner));
        }

        // ---------------------------------------------------------------------------------------------
        // Ticking: stale prompts, auto-loot
        // ---------------------------------------------------------------------------------------------

        internal static void Tick(float now) {
            // The server always answers; a prompt long past its end lost its result to a dropped connection.
            if (open.RemoveAll(r => now > r.EndsAt + 30f) > 0) { Revision++; }
            if (won.Count == 0 || now < nextAutoLootAt) { return; }
            nextAutoLootAt = now + 0.5f;
            foreach (ZDOID drop in won.Keys.ToList()) {
                if (now > won[drop]) {
                    won.Remove(drop);
                    continue;
                }
                if (LootConfig.AutoLootWonItems.Value) { TryAutoLoot(drop, now); }
            }
        }

        private static void TryAutoLoot(ZDOID dropId, float now) {
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.IsTeleporting() || ZNetScene.instance == null) { return; }
            GameObject go = ZNetScene.instance.FindInstance(dropId);
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_nview == null || drop.m_nview.IsValid() == false) { return; }
            if (Vector3.Distance(player.transform.position, drop.transform.position) > LootConfig.AutoLootRange.Value) { return; }
            if (RollLocks.Resolve(drop, RollLocks.LocalPid) != LockState.ReservedForMe) { return; }
            if (player.GetInventory().CanAddItem(drop.m_itemData) == false) {
                if (now - lastFullMessageAt > 10f) {
                    lastFullMessageAt = now;
                    player.Message(MessageHud.MessageType.Center, Localize("$iff_roll_inventory_full", ItemNames.Of(drop.m_itemData)));
                }
                return;
            }
            drop.Pickup(player);
        }

        internal static bool IsWon(ZDOID drop) => won.ContainsKey(drop);

        // ---------------------------------------------------------------------------------------------

        internal static void ChatLine(string text) {
            Chat chat = Chat.instance;
            if (chat == null) { return; }
            chat.AddString("<color=#FFC34D>" + Localize("$iff_roll_chat_prefix") + "</color> " + text);
            // Shown the way a new chat message is: the window opens briefly, then fades.
            chat.m_hideTimer = 0f;
        }

        internal static string Localize(string token, params string[] words) {
            if (Localization.instance == null) { return token; }
            return words.Length == 0 ? Localization.instance.Localize(token) : Localization.instance.Localize(token, words);
        }

        internal static void Reset() {
            open.Clear();
            won.Clear();
            Revision++;
        }
    }
}
