using System.Collections.Generic;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // The wishlists other players on this server share, as the server relayed them. Client side; lives
    // until the connection ends. The server already cleaned every list, but it is parsed again here so a
    // bad relay cannot hurt this client either.
    internal static class WishlistRoster {
        internal sealed class Shared {
            internal long Pid;
            internal string Name;
            internal int Revision;
            internal WishlistData Data;
        }

        private static readonly Dictionary<long, Shared> byPid = new Dictionary<long, Shared>();

        internal static IEnumerable<Shared> All => byPid.Values;

        internal static int Count => byPid.Count;

        internal static bool TryGet(long pid, out Shared shared) => byPid.TryGetValue(pid, out shared);

        // Empty bytes: that player stopped sharing or left.
        internal static void Apply(long pid, string name, int revision, byte[] bytes) {
            if (pid == 0) { return; }
            if (bytes == null || bytes.Length == 0) {
                if (byPid.Remove(pid)) { Wishlists.Touch(); }
                return;
            }
            if (WishlistData.TryParse(bytes, WishlistData.MaxEntries, out WishlistData data, out string error) == false) {
                Logger.LogWarning($"Ignored {name}'s shared wishlist: {error}.");
                return;
            }
            byPid[pid] = new Shared { Pid = pid, Name = string.IsNullOrEmpty(name) ? pid.ToString() : name, Revision = revision, Data = data };
            Wishlists.Touch();
        }

        internal static string NameOf(long pid) {
            if (byPid.TryGetValue(pid, out Shared shared)) { return shared.Name; }
            return ClientSession.NameOf(pid);
        }

        internal static void Reset() {
            if (byPid.Count == 0) { return; }
            byPid.Clear();
            Wishlists.Touch();
        }
    }
}
