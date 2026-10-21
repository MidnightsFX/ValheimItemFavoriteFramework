using System;
using System.Collections.Generic;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Clicks other mods take on inventory cells (API 2), asked before the framework's own favoriting clicks
    // and before vanilla. A handler gets the grid's inventory, the item in the clicked cell (null for an empty
    // cell) and the button (0 left, 1 right), and returns true to take the click: vanilla's pick-up, split or
    // use and the favoriting clicks then do nothing. A mod normally only takes clicks on its own inventories.
    internal static class ItemClickHandlers {
        internal const int Left = 0;
        internal const int Right = 1;

        private static readonly List<Func<Inventory, ItemDrop.ItemData, int, bool>> handlers = new List<Func<Inventory, ItemDrop.ItemData, int, bool>>();

        internal static void Add(Func<Inventory, ItemDrop.ItemData, int, bool> handler) {
            if (handler != null && handlers.Contains(handler) == false) { handlers.Add(handler); }
        }

        internal static void Remove(Func<Inventory, ItemDrop.ItemData, int, bool> handler) => handlers.Remove(handler);

        internal static bool Handle(InventoryGrid grid, Vector2i pos, int button) {
            if (handlers.Count == 0) { return false; }
            // A click that ends a drag belongs to vanilla.
            if (InventoryGui.instance != null && InventoryGui.instance.m_dragGo != null) { return false; }
            Inventory inventory = grid.GetInventory();
            if (inventory == null) { return false; }
            ItemDrop.ItemData item = inventory.GetItemAt(pos.x, pos.y);
            foreach (Func<Inventory, ItemDrop.ItemData, int, bool> handler in handlers.ToArray()) {
                try {
                    if (handler(inventory, item, button)) { return true; }
                } catch (Exception e) {
                    Logger.LogError($"An item click handler ({handler.Method.DeclaringType?.FullName}.{handler.Method.Name}) threw: {e}");
                }
            }
            return false;
        }
    }
}
