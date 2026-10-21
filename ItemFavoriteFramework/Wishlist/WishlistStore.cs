using System;
using System.Collections.Generic;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Owns the local character's WishlistData, the way MarkStore owns its MarkData: found in
    // Player.m_customData, cached, written back on every change. Vanilla saves m_customData inside
    // Player.Save, so there is no save hook.
    internal static class WishlistStore {
        internal const string DataKey = "IFF.wishlist";
        internal const string BackupKey = "IFF.wishlist.bak";

        private static Player owner;
        private static WishlistData data;
        private static readonly List<Action> listeners = new List<Action>();

        internal static long PlayerId { get; private set; }
        internal static bool ReadOnly { get; private set; }
        internal static string Problem { get; private set; }

        // Bumped on every load and change; the match cache and the sync compare against it.
        internal static int LocalRevision { get; private set; }

        internal static bool TryGet(out WishlistData result) {
            Player local = Player.m_localPlayer;
            if (local == null) {
                result = null;
                return false;
            }
            if (ReferenceEquals(local, owner) == false || data == null) { Load(local); }
            result = data;
            return data != null;
        }

        internal static void Invalidate() {
            // Only a list that was loaded can leave stale answers behind. Not touching otherwise matters:
            // TryGet retries the load every call while the player id is still 0.
            bool hadData = data != null;
            owner = null;
            data = null;
            PlayerId = 0;
            ReadOnly = false;
            Problem = null;
            if (hadData) {
                LocalRevision++;
                Wishlists.Touch();
            }
        }

        private static void Load(Player player) {
            Invalidate();
            long playerId = player.GetPlayerID();
            if (playerId == 0) { return; }

            WishlistData parsed = null;
            if (player.m_customData.TryGetValue(DataKey, out string raw) && string.IsNullOrEmpty(raw) == false) {
                if (WishlistData.TryParseBase64(raw, out parsed, out string error) == false) {
                    player.m_customData[BackupKey] = raw;
                    Problem = $"This character's saved wishlist is {error}. A copy was kept as '{BackupKey}' in the character file, " +
                        "and the wishlist cannot be changed until the matching version of the mod is installed.";
                    Logger.LogError(Problem);
                    ReadOnly = true;
                    parsed = null;
                }
            }

            owner = player;
            data = parsed ?? new WishlistData();
            PlayerId = playerId;
            LocalRevision++;
            Wishlists.Touch();
        }

        internal static bool Commit() {
            if (owner == null || data == null || ReadOnly) { return false; }
            data.Revision++;
            owner.m_customData[DataKey] = data.ToBase64();
            LocalRevision++;
            Wishlists.Touch();
            NotifyChanged();
            return true;
        }

        internal static void AddListener(Action listener) {
            if (listener != null && listeners.Contains(listener) == false) { listeners.Add(listener); }
        }

        internal static void RemoveListener(Action listener) => listeners.Remove(listener);

        internal static void NotifyChanged() {
            foreach (Action listener in listeners.ToArray()) {
                try {
                    listener();
                } catch (Exception e) {
                    Logger.LogError($"A wishlist-changed listener ({listener.Method.DeclaringType?.FullName}.{listener.Method.Name}) threw: {e}");
                }
            }
        }
    }
}
