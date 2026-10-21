using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // A mark type another mod registered (API 2): what it looks like, and which marks it excludes.
    internal sealed class CustomMarkType {
        internal string Id;
        internal string Group;
        internal Color Color;
        internal Sprite Icon;
        internal string TooltipToken;
        internal string Key;
    }

    // Mark types other mods own, such as "need" and "greed" on items a group is rolling for.
    //
    // Unlike favorites, a custom mark is stored on the item instance itself, in
    // m_customData["IFF.custom.<id>"], for stackable items too: it means the same to whoever holds the item,
    // it travels with it, and the mod that registered it decides what it means and when to clear it. Marks
    // registered with the same group are exclusive: setting one clears the others of its group.
    //
    // Drawn like the built-in marks: a border in the mark's colour (it outranks favorite and trash, since a
    // custom mark is about this item right now), its icon in the cell's corner, and its tooltip line.
    internal static class CustomMarks {
        internal const string KeyPrefix = "IFF.custom.";

        private static readonly List<CustomMarkType> types = new List<CustomMarkType>();
        private static readonly Dictionary<string, CustomMarkType> byId = new Dictionary<string, CustomMarkType>(StringComparer.Ordinal);

        internal static bool Any => types.Count > 0;

        // Re-registering an id updates its look (a mod reloading its sprites); the group never changes.
        internal static bool Register(string id, string group, Color color, Sprite icon, string tooltipToken) {
            if (string.IsNullOrEmpty(id) || id.IndexOfAny(new[] { ' ', '\n', '\t' }) >= 0) { return false; }
            if (byId.TryGetValue(id, out CustomMarkType existing)) {
                existing.Color = color;
                existing.Icon = icon;
                existing.TooltipToken = tooltipToken ?? "";
                return true;
            }
            CustomMarkType type = new CustomMarkType {
                Id = id,
                Group = group ?? "",
                Color = color,
                Icon = icon,
                TooltipToken = tooltipToken ?? "",
                Key = KeyPrefix + id,
            };
            types.Add(type);
            byId[id] = type;
            return true;
        }

        internal static bool IsRegistered(string id) => id != null && byId.ContainsKey(id);

        internal static bool Has(ItemDrop.ItemData item, string id) {
            return item?.m_customData != null && id != null && byId.TryGetValue(id, out CustomMarkType type) && item.m_customData.ContainsKey(type.Key);
        }

        // The first registered type the item carries, for drawing; null when it carries none.
        internal static CustomMarkType First(ItemDrop.ItemData item) {
            if (item?.m_customData == null || types.Count == 0 || item.m_customData.Count == 0) { return null; }
            foreach (CustomMarkType type in types) {
                if (item.m_customData.ContainsKey(type.Key)) { return type; }
            }
            return null;
        }

        internal static IEnumerable<CustomMarkType> All(ItemDrop.ItemData item) {
            if (item?.m_customData == null || item.m_customData.Count == 0) { yield break; }
            foreach (CustomMarkType type in types) {
                if (item.m_customData.ContainsKey(type.Key)) { yield return type; }
            }
        }

        internal static List<string> Get(ItemDrop.ItemData item) {
            List<string> ids = new List<string>();
            foreach (CustomMarkType type in All(item)) { ids.Add(type.Id); }
            return ids;
        }

        // Needs no loaded character: the mark lives on the item, not in the character's data.
        internal static bool Set(ItemDrop.ItemData item, string id, bool marked) {
            if (Marks.IsValid(item) == false || id == null || byId.TryGetValue(id, out CustomMarkType type) == false) { return false; }
            bool has = item.m_customData.ContainsKey(type.Key);
            if (has == marked) { return true; }

            List<CustomMarkType> cleared = new List<CustomMarkType>();
            if (marked) {
                if (type.Group.Length > 0) {
                    foreach (CustomMarkType other in types) {
                        if (other != type && other.Group == type.Group && item.m_customData.Remove(other.Key)) { cleared.Add(other); }
                    }
                }
                item.m_customData[type.Key] = "1";
            } else {
                item.m_customData.Remove(type.Key);
            }
            Marks.SaveOwningInventory(item, null);

            foreach (CustomMarkType other in cleared) { MarkEvents.Raise(item, other.Id, false); }
            MarkEvents.Raise(item, type.Id, marked);
            MarkStore.NotifyChanged();
            return true;
        }
    }

    // Listeners that hear which mark changed on which item (API 2). Ids: "favorite", "trash", or a custom
    // mark's id. Slot marks belong to no item and are not reported here.
    internal static class MarkEvents {
        internal const string Favorite = "favorite";
        internal const string Trash = "trash";

        private static readonly List<Action<ItemDrop.ItemData, string, bool>> listeners = new List<Action<ItemDrop.ItemData, string, bool>>();

        internal static void Add(Action<ItemDrop.ItemData, string, bool> listener) {
            if (listener != null && listeners.Contains(listener) == false) { listeners.Add(listener); }
        }

        internal static void Remove(Action<ItemDrop.ItemData, string, bool> listener) => listeners.Remove(listener);

        internal static void Raise(ItemDrop.ItemData item, string id, bool marked) {
            foreach (Action<ItemDrop.ItemData, string, bool> listener in listeners.ToArray()) {
                try {
                    listener(item, id, marked);
                } catch (Exception e) {
                    Logger.LogError($"A mark listener ({listener.Method.DeclaringType?.FullName}.{listener.Method.Name}) threw: {e}");
                }
            }
        }
    }
}
