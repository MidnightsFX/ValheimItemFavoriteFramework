using System;
using System.Globalization;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    internal enum RollChoice : byte { None = 0, Pass = 1, Greed = 2, Need = 3 }

    // A drop's lock as the server announces it.
    internal enum LockPhase : byte { Free = 0, Rolling = 1, Reserved = 2 }

    internal enum RollOutcome : byte { Won = 1, AllPassed = 2, Cancelled = 3 }

    // What a pickup gate needs to know about one drop for one player.
    internal enum LockState { Free, Rolling, ReservedForOther, ReservedForMe }

    internal static class RollIds {
        // "<zdo user>:<zdo id>:<nonce hex>": unique per drop and per roll on it.
        internal static string Format(ZDOID drop, long nonce) {
            return drop.UserID.ToString(CultureInfo.InvariantCulture) + ":" + drop.ID.ToString(CultureInfo.InvariantCulture) + ":" + nonce.ToString("X", CultureInfo.InvariantCulture);
        }

        internal static bool TryParse(string rollId, out ZDOID drop, out long nonce) {
            drop = ZDOID.None;
            nonce = 0;
            if (string.IsNullOrEmpty(rollId)) { return false; }
            string[] parts = rollId.Split(':');
            if (parts.Length != 3) { return false; }
            if (long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long user) == false) { return false; }
            if (uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint id) == false) { return false; }
            if (long.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out nonce) == false) { return false; }
            drop = new ZDOID(user, id);
            return true;
        }

        internal static string ChoiceName(RollChoice choice) {
            switch (choice) {
                case RollChoice.Need: return "need";
                case RollChoice.Greed: return "greed";
                case RollChoice.Pass: return "pass";
                default: return "none";
            }
        }

        internal static string OutcomeName(RollOutcome outcome) {
            switch (outcome) {
                case RollOutcome.Won: return "won";
                case RollOutcome.AllPassed: return "allPassed";
                default: return "cancelled";
            }
        }

        internal static string PhaseName(LockPhase phase) {
            switch (phase) {
                case LockPhase.Rolling: return "rolling";
                case LockPhase.Reserved: return "reserved";
                default: return "free";
            }
        }
    }

    // Two keys on the drop's own ZDO, written by whoever starts the roll -- who must own the drop, having
    // just spawned it. They travel in the same snapshot as the item itself, so no client can see the item
    // before it sees that the item is under a roll. The live state is the server's; see RollLocks.
    internal static class RollStamp {
        internal static readonly int NonceKey = "IFF.roll.n".GetStableHashCode();
        internal static readonly int TimeKey = "IFF.roll.t".GetStableHashCode();

        private static readonly Random random = new Random();

        internal static long ReadNonce(ZDO zdo) => zdo != null ? zdo.GetLong(NonceKey) : 0L;

        internal static long ReadTime(ZDO zdo) => zdo != null ? zdo.GetLong(TimeKey) : 0L;

        internal static long NewNonce() {
            byte[] bytes = new byte[8];
            long nonce = 0;
            while (nonce == 0) {
                random.NextBytes(bytes);
                // Positive, so it prints the same in every language's hex.
                nonce = BitConverter.ToInt64(bytes, 0) & long.MaxValue;
            }
            return nonce;
        }

        internal static void Write(ZDO zdo, long nonce) {
            zdo.Set(NonceKey, nonce);
            zdo.Set(TimeKey, ZNet.instance.GetTime().Ticks);
        }

        // Seconds since the stamp, in network time. Network time jumps forward when players sleep, which only
        // ever shortens the brief grace period this is used for.
        internal static double AgeSeconds(ZDO zdo) {
            long stamped = ReadTime(zdo);
            if (stamped <= 0 || ZNet.instance == null) { return double.MaxValue; }
            return (ZNet.instance.GetTime().Ticks - stamped) / (double)TimeSpan.TicksPerSecond;
        }
    }
}
