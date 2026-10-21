using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ItemFavoriteFramework {

    // The public integration surface for other mods. Three ways in:
    //   - a hard reference to ItemFavoriteFramework.dll plus [BepInDependency], calling this directly;
    //   - the ItemFavoriteFrameworkAPI shim (bundled or copied), which binds these methods once and
    //     no-ops when this mod is not installed;
    //   - plain reflection on Type.GetType("ItemFavoriteFramework.API, ItemFavoriteFramework").
    //
    // The signature rule: only primitives, string, vanilla/Unity types and System.Func/Action of those
    // cross this boundary -- no type of this mod appears in any signature -- so every method can be bound
    // with Delegate.CreateDelegate by a mod that never references this assembly. Existing signatures never
    // change; additions bump ApiVersion.
    //
    // Everything answers for the local player and must be called on the main thread. Before a character
    // is loaded, queries return false or an empty list and changes are refused.
    //
    // The wishlist and loot-roll endpoints (API 3) are in API.Loot.cs.
    public static partial class API {
        // 2: custom mark types, per-item mark listeners and item click handlers.
        // 3: wishlists (with condition providers and sharing) and server-run need/greed/pass loot rolls.
        public const int ApiVersion = 3;

        // Bits of GetMarks.
        public const int MarkItemFavorite = Marks.FlagItemFavorite;
        public const int MarkSlotFavorite = Marks.FlagSlotFavorite;
        public const int MarkTrash = Marks.FlagTrash;

        // -----------------------------------------------------------------------------------------
        // Versioning / diagnostics
        // -----------------------------------------------------------------------------------------

        public static int GetApiVersion() => ApiVersion;

        public static string GetPluginVersion() => ItemFavoriteFramework.PluginVersion;

        public static string GetPluginId() => ItemFavoriteFramework.PluginGUID;

        public static bool HasEndpoint(string name) => GetEndpointNames().Contains(name);

        public static List<string> GetEndpointNames() {
            return typeof(API).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        }

        // True once the local character's marks are loaded.
        public static bool IsReady() => MarkStore.TryGet(out _);

        // -----------------------------------------------------------------------------------------
        // Queries
        // -----------------------------------------------------------------------------------------

        // Favorited, either this specific item (weapons, armor, tools) or its whole type (stackables).
        public static bool IsItemFavorited(ItemDrop.ItemData item) => Marks.IsItemFavorited(item);

        // Flagged as trash. Never true for a favorited item.
        public static bool IsItemTrashFlagged(ItemDrop.ItemData item) => Marks.IsItemTrashFlagged(item);

        // A favorited cell of the local player's inventory grid.
        public static bool IsSlotFavorited(int x, int y) => Marks.IsSlotFavorited(new Vector2i(x, y));

        // The item is in the local player's inventory, in a favorited cell.
        public static bool IsItemInFavoritedSlot(ItemDrop.ItemData item) => Marks.IsItemInFavoritedSlot(item);

        // Favorited, or sitting in a favorited slot. The check for anything that moves items out of the
        // player's inventory automatically (store all, quick stack, sort) or destroys them (sacrifice,
        // salvage, disenchant).
        public static bool IsProtected(ItemDrop.ItemData item) => Marks.IsProtected(item);

        // MarkItemFavorite | MarkSlotFavorite | MarkTrash, or 0.
        public static int GetMarks(ItemDrop.ItemData item) => Marks.GetMarks(item);

        // Pass null for the local player's inventory. Items of any inventory can carry marks, since
        // per-item marks travel with the item; slot marks only count in the player's inventory.
        public static List<ItemDrop.ItemData> GetFavoritedItems(Inventory inventory) => Marks.GetItems(inventory, Marks.IsItemFavorited);

        public static List<ItemDrop.ItemData> GetTrashFlaggedItems(Inventory inventory) => Marks.GetItems(inventory, Marks.IsItemTrashFlagged);

        public static List<ItemDrop.ItemData> GetProtectedItems(Inventory inventory) => Marks.GetItems(inventory, Marks.IsProtected);

        // Favorited cells of the local player's inventory grid, sorted by row then column.
        public static List<Vector2i> GetFavoritedSlots() {
            if (MarkStore.TryGet(out MarkData data) == false) { return new List<Vector2i>(); }
            return data.Slots.OrderBy(s => s.y).ThenBy(s => s.x).ToList();
        }

        // -----------------------------------------------------------------------------------------
        // Changes
        //
        // True when the item or slot is in the requested state afterwards, including when it already was.
        // False when refused: no character loaded, favorite and trash are exclusive (unflag one before
        // setting the other), or the character's saved marks could not be read.
        // -----------------------------------------------------------------------------------------

        public static bool SetItemFavorited(ItemDrop.ItemData item, bool favorited) => Succeeded(Marks.SetItemFavorited(item, favorited));

        public static bool SetItemTrashFlagged(ItemDrop.ItemData item, bool trashFlagged) => Succeeded(Marks.SetItemTrashFlagged(item, trashFlagged));

        public static bool SetSlotFavorited(int x, int y, bool favorited) => Succeeded(Marks.SetSlotFavorited(new Vector2i(x, y), favorited));

        private static bool Succeeded(MarkResult result) => result == MarkResult.Changed || result == MarkResult.Unchanged;

        // -----------------------------------------------------------------------------------------
        // Listeners
        //
        // Called after any mark changes and after a character's marks load. Carries no payload: re-query
        // what you display. A listener that throws is logged and does not stop the others.
        // -----------------------------------------------------------------------------------------

        public static void AddChangedListener(Action listener) => MarkStore.AddListener(listener);

        public static void RemoveChangedListener(Action listener) => MarkStore.RemoveListener(listener);

        // -----------------------------------------------------------------------------------------
        // UI
        // -----------------------------------------------------------------------------------------

        // White with a dark outline; tint it with GetFavoriteColor / GetTrashColor.
        public static Sprite GetFavoriteIcon() => SpriteFactory.Star;

        public static Sprite GetTrashIcon() => SpriteFactory.Trash;

        // The player's configured border colours.
        public static Color GetFavoriteColor() => ValConfig.BorderColorFavoritedItem.Value;

        public static Color GetFavoritedSlotColor() => ValConfig.BorderColorFavoritedSlot.Value;

        public static Color GetTrashColor() => ValConfig.BorderColorTrashFlaggedItem.Value;

        // -----------------------------------------------------------------------------------------
        // Custom marks (API 2)
        //
        // A mod's own item marks, drawn and listed like the built-in ones. A custom mark is stored on the
        // item instance (m_customData "IFF.custom.<id>", stackables included), so it travels with the item
        // and reads the same for whoever holds it; the registering mod decides what it means and clears it
        // when done. Marks registered with the same non-empty group are exclusive: setting one clears the
        // others of its group. They need no loaded character.
        // -----------------------------------------------------------------------------------------

        // The listener ids of the built-in item marks (slot marks belong to no item and are not reported).
        public const string MarkIdFavorite = MarkEvents.Favorite;
        public const string MarkIdTrash = MarkEvents.Trash;

        // id: unique and namespaced ("clans.loot.need"), no spaces. icon: drawn in the cell's top-left
        // corner, or null for a border only. tooltipToken: a line added to the item's tooltip, or empty.
        // Registering an id again updates its look; its group never changes.
        public static bool RegisterMarkType(string id, string group, Color color, Sprite icon, string tooltipToken) =>
            CustomMarks.Register(id, group, color, icon, tooltipToken);

        public static bool IsMarkTypeRegistered(string id) => CustomMarks.IsRegistered(id);

        public static bool HasCustomMark(ItemDrop.ItemData item, string id) => CustomMarks.Has(item, id);

        // The registered ids this item carries, in registration order.
        public static List<string> GetCustomMarks(ItemDrop.ItemData item) => CustomMarks.Get(item);

        // True when the item is in the requested state afterwards. False for an unknown id or an invalid item.
        public static bool SetCustomMark(ItemDrop.ItemData item, string id, bool marked) => CustomMarks.Set(item, id, marked);

        // Called with the item, the mark id (MarkIdFavorite, MarkIdTrash or a custom id) and whether it is
        // now set, after any item mark changes. The no-payload AddChangedListener still fires too.
        public static void AddMarkListener(Action<ItemDrop.ItemData, string, bool> listener) => MarkEvents.Add(listener);

        public static void RemoveMarkListener(Action<ItemDrop.ItemData, string, bool> listener) => MarkEvents.Remove(listener);

        // -----------------------------------------------------------------------------------------
        // Item clicks (API 2)
        //
        // handler(inventory, item, button): the clicked grid's inventory, the item in the clicked cell (null
        // when it is empty) and the button (0 left, 1 right), on mouse down. Return true to take the click:
        // vanilla's pick-up, split or use and the favoriting clicks then do nothing. Asked before the
        // framework's own clicks, on every grid; normally take clicks only on your own inventories.
        // -----------------------------------------------------------------------------------------

        public static void AddItemClickHandler(Func<Inventory, ItemDrop.ItemData, int, bool> handler) => ItemClickHandlers.Add(handler);

        public static void RemoveItemClickHandler(Func<Inventory, ItemDrop.ItemData, int, bool> handler) => ItemClickHandlers.Remove(handler);
    }
}
