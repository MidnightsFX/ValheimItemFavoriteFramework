using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Which drops are under a roll or reserved, and for whom: the one question every pickup gate asks.
    //
    // The answer combines the stamp on the drop's ZDO with the lock table the server pushes:
    //   - no stamp: free;
    //   - a table entry for this drop with the stamp's nonce: that entry decides;
    //   - a stamp the table does not know: locked only while the stamp is younger than a short grace period.
    // The last rule covers the moment between a drop appearing and the server's first word on it, and also
    // frees stale stamps for good: a refused request, a server restart, a world saved mid-roll, a server
    // without the mod. On the server itself the server's own table is read instead.
    internal static class RollLocks {
        internal sealed class Entry {
            internal ZDOID Drop;
            internal long Nonce;
            internal LockPhase Phase;
            internal long WinnerPid;
            internal string WinnerName = "";
            internal string Label = "";
            // Local realtime.
            internal float ExpiresAt;
        }

        private static readonly Dictionary<ZDOID, Entry> entries = new Dictionary<ZDOID, Entry>();

        internal static IEnumerable<Entry> All => entries.Values;

        internal static long LocalPid => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;

        internal static void OnLocks(ZPackage pkg) {
            float now = Time.realtimeSinceStartup;
            bool full = pkg.ReadBool();
            if (full) { entries.Clear(); }
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++) {
                Entry entry = new Entry {
                    Drop = pkg.ReadZDOID(),
                    Nonce = pkg.ReadLong(),
                    Phase = (LockPhase)pkg.ReadByte(),
                    WinnerPid = pkg.ReadLong(),
                    WinnerName = pkg.ReadString(),
                };
                float secondsLeft = pkg.ReadSingle();
                if (float.IsNaN(secondsLeft) || float.IsInfinity(secondsLeft)) { secondsLeft = 0f; }
                entry.Label = pkg.ReadString();
                if (entry.Phase == LockPhase.Free && entries.TryGetValue(entry.Drop, out Entry live)
                    && live.Nonce != entry.Nonce && live.Phase != LockPhase.Free && now <= live.ExpiresAt) {
                    continue;
                }
                // A free entry is kept as long as an unknown stamp would count as locked, so it overrides it. A
                // rolling one outlives its deadline by a margin, so a result that arrives a little late never
                // finds the drop briefly free.
                switch (entry.Phase) {
                    case LockPhase.Free: entry.ExpiresAt = now + ClientSession.StartGraceSeconds + 1f; break;
                    case LockPhase.Rolling: entry.ExpiresAt = now + secondsLeft + 15f; break;
                    default: entry.ExpiresAt = now + secondsLeft; break;
                }
                entries[entry.Drop] = entry;
                RollEvents.RaiseLock(entry, secondsLeft, "client");
            }
        }

        internal static LockState Resolve(ItemDrop drop, long pid) {
            if (drop == null) { return LockState.Free; }
            ZNetView nview = drop.m_nview;
            if (nview == null || nview.IsValid() == false) { return LockState.Free; }
            return Resolve(nview.GetZDO(), pid);
        }

        internal static LockState Resolve(ZDO zdo, long pid) {
            long nonce = RollStamp.ReadNonce(zdo);
            if (nonce == 0) { return LockState.Free; }

            LockPhase phase;
            long winner;
            if (ZNet.instance != null && ZNet.instance.IsServer()) {
                if (RollServer.TryGetLock(zdo.m_uid, nonce, out phase, out winner) == false) { return Unknown(zdo, ServerSession.StartGraceSeconds); }
            } else {
                if (entries.TryGetValue(zdo.m_uid, out Entry entry) == false || entry.Nonce != nonce) { return Unknown(zdo, ClientSession.StartGraceSeconds); }
                if (entry.Phase != LockPhase.Free && Time.realtimeSinceStartup > entry.ExpiresAt) { return LockState.Free; }
                phase = entry.Phase;
                winner = entry.WinnerPid;
            }
            switch (phase) {
                case LockPhase.Rolling: return LockState.Rolling;
                case LockPhase.Reserved: return winner != 0 && winner == pid ? LockState.ReservedForMe : LockState.ReservedForOther;
                default: return LockState.Free;
            }
        }

        private static LockState Unknown(ZDO zdo, float grace) {
            return RollStamp.AgeSeconds(zdo) < grace ? LockState.Rolling : LockState.Free;
        }

        internal static bool IsLockedFor(ItemDrop drop, long pid) {
            LockState state = Resolve(drop, pid);
            return state == LockState.Rolling || state == LockState.ReservedForOther;
        }

        internal static bool IsLockedForLocalPlayer(ItemDrop drop) => IsLockedFor(drop, LocalPid);

        internal static bool TryGetEntry(ZDOID drop, out Entry entry) => entries.TryGetValue(drop, out entry);

        // Seconds left on a reservation, for messages; 0 when unknown.
        internal static float SecondsLeft(ZDOID drop) {
            if (entries.TryGetValue(drop, out Entry entry) == false) { return 0f; }
            return Mathf.Max(0f, entry.ExpiresAt - Time.realtimeSinceStartup);
        }

        internal static void Tick(float now) {
            if (entries.Count == 0) { return; }
            foreach (ZDOID stale in entries.Values.Where(e => now > e.ExpiresAt + 5f).Select(e => e.Drop).ToList()) {
                entries.Remove(stale);
            }
        }

        internal static void Reset() => entries.Clear();
    }
}
