using System.Collections.Generic;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    internal enum ItemMark { None, Favorite, Trash }

    internal enum MarkResult { Changed, Unchanged, NotReady, ReadOnly, InvalidItem, RefusedIsTrash, RefusedIsFavorite }

    // The marking rules, in one place, for the local player.
    //
    // Granularity is hybrid. An item that cannot stack (m_maxStackSize <= 1: weapons, armor, tools) is
    // marked on the item itself, so favoriting one sword leaves every other sword alone. A stackable item
    // is marked by type (m_shared.m_name), the way Quick Stack Store Sort does it, because vanilla merges
    // stacks without comparing custom data and a per-stack mark would be lost or spread on every merge.
    //
    // A query honours both sources regardless of the item's current stack size: a non-stackable can still
    // be type-marked (imported from Quick Stack Store Sort), and a mod may change stack sizes later.
    internal static class Marks {
        internal const int FlagItemFavorite = 1;
        internal const int FlagSlotFavorite = 2;
        internal const int FlagTrash = 4;

        // m_shared is null on the bare ItemData of vanilla's pre-1.0 world conversion.
        internal static bool IsValid(ItemDrop.ItemData item) => item != null && item.m_shared != null && item.m_customData != null;

        internal static bool IsPerInstance(ItemDrop.ItemData item) => item.m_shared.m_maxStackSize <= 1;

        // -----------------------------------------------------------------------------------------
        // Queries
        // -----------------------------------------------------------------------------------------

        // This character's mark on this specific item, ignoring type marks. Stale-generation marks and
        // other players' marks read as None.
        internal static ItemMark GetInstanceMark(ItemDrop.ItemData item) {
            if (IsValid(item) == false || MarkStore.TryGet(out _) == false) { return ItemMark.None; }
            return InstanceMarkUnchecked(item);
        }

        private static ItemMark InstanceMarkUnchecked(ItemDrop.ItemData item) {
            if (item.m_customData.TryGetValue(MarkStore.ItemKey, out string value) == false) { return ItemMark.None; }
            if (value == MarkStore.FavoriteValue) { return ItemMark.Favorite; }
            if (value == MarkStore.TrashValue) { return ItemMark.Trash; }
            return ItemMark.None;
        }

        // The item's own mark from either source, before slot marks. Favorite beats trash, so a favorite
        // can never be flagged away by a type-level trash flag.
        internal static ItemMark GetItemMark(ItemDrop.ItemData item) {
            if (IsValid(item) == false || MarkStore.TryGet(out MarkData data) == false) { return ItemMark.None; }
            ItemMark instance = InstanceMarkUnchecked(item);
            string name = item.m_shared.m_name;
            if (instance == ItemMark.Favorite || data.FavoriteTypes.Contains(name)) { return ItemMark.Favorite; }
            if (instance == ItemMark.Trash || data.TrashTypes.Contains(name)) { return ItemMark.Trash; }
            return ItemMark.None;
        }

        internal static bool IsItemFavorited(ItemDrop.ItemData item) => GetItemMark(item) == ItemMark.Favorite;

        internal static bool IsItemTrashFlagged(ItemDrop.ItemData item) => GetItemMark(item) == ItemMark.Trash;

        internal static bool IsSlotFavorited(Vector2i pos) {
            return MarkStore.TryGet(out MarkData data) && data.Slots.Contains(pos);
        }

        // Slot marks only exist in the player's own grid, but every item has an m_gridPos -- including
        // items in chests and in the provider inventories mods like EpicLoot merge into their lists. The
        // containment check comes last, since it scans the inventory.
        internal static bool IsItemInFavoritedSlot(ItemDrop.ItemData item) {
            if (item == null || MarkStore.TryGet(out MarkData data) == false || data.Slots.Count == 0) { return false; }
            if (data.Slots.Contains(item.m_gridPos) == false) { return false; }
            Inventory inventory = Player.m_localPlayer.GetInventory();
            return inventory != null && inventory.ContainsItem(item);
        }

        // The "never move it automatically, never destroy it" predicate offered to other mods.
        internal static bool IsProtected(ItemDrop.ItemData item) => IsItemFavorited(item) || IsItemInFavoritedSlot(item);

        internal static int GetMarks(ItemDrop.ItemData item) {
            int flags = 0;
            ItemMark mark = GetItemMark(item);
            if (mark == ItemMark.Favorite) { flags |= FlagItemFavorite; }
            if (mark == ItemMark.Trash) { flags |= FlagTrash; }
            if (IsItemInFavoritedSlot(item)) { flags |= FlagSlotFavorite; }
            return flags;
        }

        // -----------------------------------------------------------------------------------------
        // Changes
        //
        // owningInventory is the inventory the item sits in, when the caller knows it. A changed instance
        // mark is only saved if that inventory reports a change -- a chest otherwise never rewrites its
        // contents to the world.
        // -----------------------------------------------------------------------------------------

        internal static MarkResult SetItemFavorited(ItemDrop.ItemData item, bool favorited, Inventory owningInventory = null) {
            if (IsValid(item) == false) { return MarkResult.InvalidItem; }
            if (MarkStore.TryGet(out MarkData data) == false) { return MarkResult.NotReady; }
            if (MarkStore.ReadOnly) { return MarkResult.ReadOnly; }

            string name = item.m_shared.m_name;
            if (favorited) {
                ItemMark current = GetItemMark(item);
                if (current == ItemMark.Favorite) { return MarkResult.Unchanged; }
                if (current == ItemMark.Trash) { return MarkResult.RefusedIsTrash; }
                if (IsPerInstance(item)) {
                    Stamp(item, MarkStore.FavoriteValue, owningInventory);
                } else {
                    data.FavoriteTypes.Add(name);
                }
            } else {
                // Clears both sources: an item favorited by type (an import, or a stack size another mod
                // changed) would otherwise stay favorited however often it is toggled.
                bool changed = data.FavoriteTypes.Remove(name);
                if (InstanceMarkUnchecked(item) == ItemMark.Favorite) {
                    Unstamp(item, owningInventory);
                    changed = true;
                }
                if (changed == false) { return MarkResult.Unchanged; }
            }
            MarkStore.Commit();
            MarkEvents.Raise(item, MarkEvents.Favorite, favorited);
            return MarkResult.Changed;
        }

        internal static MarkResult SetItemTrashFlagged(ItemDrop.ItemData item, bool trash, Inventory owningInventory = null) {
            if (IsValid(item) == false) { return MarkResult.InvalidItem; }
            if (MarkStore.TryGet(out MarkData data) == false) { return MarkResult.NotReady; }
            if (MarkStore.ReadOnly) { return MarkResult.ReadOnly; }

            string name = item.m_shared.m_name;
            if (trash) {
                ItemMark current = GetItemMark(item);
                if (current == ItemMark.Trash) { return MarkResult.Unchanged; }
                if (current == ItemMark.Favorite) { return MarkResult.RefusedIsFavorite; }
                if (IsPerInstance(item)) {
                    Stamp(item, MarkStore.TrashValue, owningInventory);
                } else {
                    data.TrashTypes.Add(name);
                }
            } else {
                bool changed = data.TrashTypes.Remove(name);
                if (InstanceMarkUnchecked(item) == ItemMark.Trash) {
                    Unstamp(item, owningInventory);
                    changed = true;
                }
                if (changed == false) { return MarkResult.Unchanged; }
            }
            MarkStore.Commit();
            MarkEvents.Raise(item, MarkEvents.Trash, trash);
            return MarkResult.Changed;
        }

        internal static MarkResult ToggleItemFavorited(ItemDrop.ItemData item, Inventory owningInventory = null) {
            return SetItemFavorited(item, IsItemFavorited(item) == false, owningInventory);
        }

        internal static MarkResult ToggleItemTrashFlagged(ItemDrop.ItemData item, Inventory owningInventory = null) {
            return SetItemTrashFlagged(item, IsItemTrashFlagged(item) == false, owningInventory);
        }

        internal static MarkResult SetSlotFavorited(Vector2i pos, bool favorited) {
            if (pos.x < 0 || pos.y < 0) { return MarkResult.InvalidItem; }
            if (MarkStore.TryGet(out MarkData data) == false) { return MarkResult.NotReady; }
            if (MarkStore.ReadOnly) { return MarkResult.ReadOnly; }

            bool changed = favorited ? data.Slots.Add(pos) : data.Slots.Remove(pos);
            if (changed == false) { return MarkResult.Unchanged; }
            MarkStore.Commit();
            return MarkResult.Changed;
        }

        internal static MarkResult ToggleSlotFavorited(Vector2i pos) => SetSlotFavorited(pos, IsSlotFavorited(pos) == false);

        // Re-applies an instance mark that vanilla dropped, e.g. on a crafting upgrade, which replaces the
        // item. Does nothing when the mark is already there (EpicLoot copies custom data on upgrade).
        internal static void RestoreInstanceMark(ItemDrop.ItemData item, ItemMark mark, Inventory owningInventory) {
            if (IsValid(item) == false || mark == ItemMark.None || MarkStore.TryGet(out _) == false || MarkStore.ReadOnly) { return; }
            if (InstanceMarkUnchecked(item) == mark) { return; }
            Stamp(item, mark == ItemMark.Favorite ? MarkStore.FavoriteValue : MarkStore.TrashValue, owningInventory);
            MarkStore.Commit();
        }

        // -----------------------------------------------------------------------------------------
        // Clearing
        //
        // Bumping a generation cancels every instance mark of that kind wherever the item is now -- in a
        // chest, a tombstone, on the ground -- without having to find it. Marks on the items being carried
        // are also removed outright, so they do not linger as dead keys.
        // -----------------------------------------------------------------------------------------

        internal static bool ClearFavorites() {
            if (MarkStore.TryGet(out MarkData data) == false || MarkStore.ReadOnly) { return false; }
            RemoveCarriedInstanceMarks(ItemMark.Favorite);
            data.FavoriteTypes.Clear();
            data.FavoriteGeneration++;
            return MarkStore.Commit();
        }

        internal static bool ClearTrash() {
            if (MarkStore.TryGet(out MarkData data) == false || MarkStore.ReadOnly) { return false; }
            RemoveCarriedInstanceMarks(ItemMark.Trash);
            data.TrashTypes.Clear();
            data.TrashGeneration++;
            return MarkStore.Commit();
        }

        internal static bool ClearSlots() {
            if (MarkStore.TryGet(out MarkData data) == false || MarkStore.ReadOnly) { return false; }
            data.Slots.Clear();
            return MarkStore.Commit();
        }

        private static void RemoveCarriedInstanceMarks(ItemMark mark) {
            Inventory inventory = Player.m_localPlayer.GetInventory();
            bool removed = false;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems()) {
                if (IsValid(item) && InstanceMarkUnchecked(item) == mark) {
                    item.m_customData.Remove(MarkStore.ItemKey);
                    removed = true;
                }
            }
            if (removed) { inventory.Changed(); }
        }

        // -----------------------------------------------------------------------------------------
        // Enumeration
        // -----------------------------------------------------------------------------------------

        internal static List<ItemDrop.ItemData> GetItems(Inventory inventory, System.Func<ItemDrop.ItemData, bool> predicate) {
            List<ItemDrop.ItemData> result = new List<ItemDrop.ItemData>();
            if (inventory == null && Player.m_localPlayer != null) { inventory = Player.m_localPlayer.GetInventory(); }
            if (inventory == null) { return result; }
            foreach (ItemDrop.ItemData item in inventory.GetAllItems()) {
                if (predicate(item)) { result.Add(item); }
            }
            return result;
        }

        // -----------------------------------------------------------------------------------------

        private static void Stamp(ItemDrop.ItemData item, string value, Inventory owningInventory) {
            item.m_customData[MarkStore.ItemKey] = value;
            SaveOwningInventory(item, owningInventory);
        }

        private static void Unstamp(ItemDrop.ItemData item, Inventory owningInventory) {
            item.m_customData.Remove(MarkStore.ItemKey);
            SaveOwningInventory(item, owningInventory);
        }

        internal static void SaveOwningInventory(ItemDrop.ItemData item, Inventory owningInventory) {
            Inventory inventory = owningInventory ?? FindOwningInventory(item);
            if (inventory != null) { inventory.Changed(); }
        }

        // The two inventories a player can touch from the inventory screen. An item anywhere else (a
        // provider inventory another mod lists) is marked in memory and saved whenever its own inventory
        // next changes.
        private static Inventory FindOwningInventory(ItemDrop.ItemData item) {
            Inventory playerInventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
            if (playerInventory != null && playerInventory.ContainsItem(item)) { return playerInventory; }
            Container container = InventoryGui.instance != null ? InventoryGui.instance.m_currentContainer : null;
            Inventory containerInventory = container != null ? container.GetInventory() : null;
            if (containerInventory != null && containerInventory.ContainsItem(item)) { return containerInventory; }
            return null;
        }
    }
}
