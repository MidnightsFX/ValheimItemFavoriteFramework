using BepInEx.Configuration;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // What the player's mouse and keys mean on an inventory grid, and what they are told back. The
    // Harmony patches only find the grid position; everything they decide goes through here.
    internal static class FavoritingInput {

        // Through ZInput rather than UnityEngine.Input: Valheim runs on the new Input System, where the
        // legacy Input class reports nothing.
        internal static bool IsHeld(KeyboardShortcut shortcut) {
            if (shortcut.MainKey == KeyCode.None || ZInput.GetKey(shortcut.MainKey, logWarning: false) == false) { return false; }
            foreach (KeyCode modifier in shortcut.Modifiers) {
                if (ZInput.GetKey(modifier, logWarning: false) == false) { return false; }
            }
            return true;
        }

        internal static bool IsFavoritingModeActive() {
            return IsHeld(ValConfig.FavoritingModifierKeybind1.Value) || IsHeld(ValConfig.FavoritingModifierKeybind2.Value);
        }

        // Only the first favoriting key counts, so AltGr (Left Control + Right Alt) held on the second one
        // favorites rather than trashes.
        internal static bool IsTrashChordHeld() {
            return IsHeld(ValConfig.FavoritingModifierKeybind1.Value) && IsHeld(ValConfig.TrashModifierKeybind.Value);
        }

        // Shared guards for every favoriting click: a click that starts or ends a drag, or lands while the
        // player is teleporting, belongs to vanilla.
        internal static bool CanHandleClick() {
            if (IsFavoritingModeActive() == false) { return false; }
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || gui.m_dragGo != null) { return false; }
            Player player = Player.m_localPlayer;
            return player != null && player.IsTeleporting() == false;
        }

        internal static bool IsPlayerInventory(Inventory inventory) {
            return inventory != null && Player.m_localPlayer != null && inventory == Player.m_localPlayer.GetInventory();
        }

        // Left click (or the trash chord, or middle click) on an item. Returns whether the click was
        // consumed. A click on an empty cell is consumed too, so a favoriting click never falls through to
        // an unexpected vanilla action.
        internal static bool HandleItemClick(InventoryGrid grid, Vector2i pos, bool trash) {
            Inventory inventory = grid.GetInventory();
            if (inventory == null) { return false; }
            if (IsPlayerInventory(inventory) == false && ValConfig.AllowMarkingInContainers.Value == false) { return false; }

            ItemDrop.ItemData item = inventory.GetItemAt(pos.x, pos.y);
            if (item == null) { return true; }

            MarkResult result = trash
                ? Marks.ToggleItemTrashFlagged(item, inventory)
                : Marks.ToggleItemFavorited(item, inventory);
            Report(result);
            return true;
        }

        // Right click on a cell. Slot favorites only exist in the player's own grid; elsewhere the click is
        // still consumed so Alt + right click never uses an item by accident.
        internal static bool HandleSlotClick(InventoryGrid grid, Vector2i pos) {
            if (IsPlayerInventory(grid.GetInventory()) == false) { return true; }
            Report(Marks.ToggleSlotFavorited(pos));
            return true;
        }

        private static void Report(MarkResult result) {
            switch (result) {
                case MarkResult.RefusedIsTrash:
                    Message("$iff_msg_cant_favorite_trash");
                    break;
                case MarkResult.RefusedIsFavorite:
                    Message("$iff_msg_cant_trash_favorite");
                    break;
                case MarkResult.ReadOnly:
                    Message("$iff_msg_read_only");
                    break;
            }
        }

        private static void Message(string token) {
            if (Player.m_localPlayer != null) { Player.m_localPlayer.Message(MessageHud.MessageType.Center, token); }
        }
    }
}
