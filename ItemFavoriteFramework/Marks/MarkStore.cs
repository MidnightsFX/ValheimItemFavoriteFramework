using System;
using System.Collections.Generic;
using System.Globalization;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Owns the local character's MarkData: finds it in Player.m_customData, caches it, and writes it back
    // on every change. Vanilla saves m_customData inside Player.Save, so there is no save hook here.
    //
    // Everything is resolved against Player.m_localPlayer, never a player passed in: the character-select
    // preview and other players' Player objects must never be read as ours.
    internal static class MarkStore {
        internal const string DataKey = "IFF.data";
        internal const string BackupKey = "IFF.data.bak";
        // Suffixed with the owning player id, so two players can mark the same item independently and one
        // player's marks mean nothing to anyone else who ends up holding the item.
        internal const string ItemKeyPrefix = "IFF.mark.";

        private static Player owner;
        private static MarkData data;
        private static readonly List<Action> listeners = new List<Action>();

        internal static long PlayerId { get; private set; }

        // The m_customData key and the two values a current instance mark can hold. Precomputed so the
        // per-frame border pass compares strings instead of building them.
        internal static string ItemKey { get; private set; }
        internal static string FavoriteValue { get; private set; }
        internal static string TrashValue { get; private set; }

        // Set when the saved data could not be read. Marks then show as empty and nothing is written, so
        // the original stays in the character file for a newer version (or a person) to recover.
        internal static bool ReadOnly { get; private set; }
        internal static string Problem { get; private set; }

        internal static bool TryGet(out MarkData result) {
            Player local = Player.m_localPlayer;
            if (local == null) {
                result = null;
                return false;
            }
            if (ReferenceEquals(local, owner) == false || data == null) { Load(local); }
            result = data;
            return data != null;
        }

        // The next TryGet re-reads m_customData. Called whenever the local Player is (re)loaded.
        internal static void Invalidate() {
            owner = null;
            data = null;
            PlayerId = 0;
            ItemKey = null;
            FavoriteValue = null;
            TrashValue = null;
            ReadOnly = false;
            Problem = null;
        }

        private static void Load(Player player) {
            Invalidate();
            // Zero until PlayerProfile.LoadPlayerData has run; nothing can be keyed before that.
            long playerId = player.GetPlayerID();
            if (playerId == 0) { return; }

            MarkData parsed = null;
            if (player.m_customData.TryGetValue(DataKey, out string raw) && string.IsNullOrEmpty(raw) == false) {
                if (MarkData.TryParse(raw, out parsed, out string error) == false) {
                    player.m_customData[BackupKey] = raw;
                    Problem = $"This character's saved favorites are {error}. A copy was kept as '{BackupKey}' in the character file, " +
                        "and favorites cannot be changed until the matching version of the mod is installed.";
                    Logger.LogError(Problem);
                    ReadOnly = true;
                    parsed = null;
                }
            }

            owner = player;
            data = parsed ?? new MarkData();
            PlayerId = playerId;
            RefreshKeys();
        }

        private static void RefreshKeys() {
            ItemKey = ItemKeyPrefix + PlayerId.ToString(CultureInfo.InvariantCulture);
            FavoriteValue = "F:" + data.FavoriteGeneration.ToString(CultureInfo.InvariantCulture);
            TrashValue = "T:" + data.TrashGeneration.ToString(CultureInfo.InvariantCulture);
        }

        // Writes the per-character data through to m_customData and tells listeners something changed.
        // Also the notification path for instance-mark changes, which touch no per-character data.
        internal static bool Commit() {
            if (owner == null || data == null || ReadOnly) { return false; }
            owner.m_customData[DataKey] = data.ToBase64();
            RefreshKeys();
            NotifyChanged();
            return true;
        }

        internal static void AddListener(Action listener) {
            if (listener != null && listeners.Contains(listener) == false) { listeners.Add(listener); }
        }

        internal static void RemoveListener(Action listener) {
            listeners.Remove(listener);
        }

        internal static void NotifyChanged() {
            // A copy, so a listener may remove itself while being called.
            foreach (Action listener in listeners.ToArray()) {
                try {
                    listener();
                } catch (Exception e) {
                    // Another mod's bug: log it with the culprit and keep notifying the rest.
                    Logger.LogError($"A favorites-changed listener ({listener.Method.DeclaringType?.FullName}.{listener.Method.Name}) threw: {e}");
                }
            }
        }
    }
}
