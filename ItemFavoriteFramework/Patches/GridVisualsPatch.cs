using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Runs after vanilla has laid out a grid, once per frame per visible grid (the player's and, while
    // one is open, the container's). Vanilla has rebuilt m_elements by now if the grid changed size.
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class GridVisualsPatch {
        // Reused every frame: one marker per element, index-aligned with m_elements.
        private static readonly List<SlotMarker> markers = new List<SlotMarker>();
        private static bool reportedFailure;

        private static void Postfix(InventoryGrid __instance) {
            try {
                HandleMiddleClick(__instance);
                DrawMarks(__instance);
            } catch (Exception e) {
                // Once: a per-frame patch that throws would otherwise flood the log.
                if (reportedFailure == false) {
                    reportedFailure = true;
                    Logger.LogError($"Drawing favorite borders failed; further errors are suppressed: {e}");
                }
            }
        }

        // Vanilla never wires the cells' middle-click handlers, so the hovered cell is found here instead.
        private static void HandleMiddleClick(InventoryGrid grid) {
            if (ModCompat.MiddleClickTrashActive == false) { return; }
            if (ZInput.GetKeyDown(KeyCode.Mouse2, logWarning: false) == false) { return; }
            if (FavoritingInput.CanHandleClick() == false) { return; }
            InventoryElement hovered = grid.GetHoveredElement();
            if (hovered == null) { return; }
            FavoritingInput.HandleItemClick(grid, hovered.Position, trash: true);
        }

        private static void DrawMarks(InventoryGrid grid) {
            Inventory inventory = grid.m_inventory;
            List<InventoryElement> elements = grid.m_elements;
            if (inventory == null || elements == null) { return; }

            bool ready = MarkStore.TryGet(out MarkData data);
            // Slot marks belong to the player's grid only. Item marks show everywhere: an instance mark
            // travels with the item, so a favorited sword stays gold inside a chest.
            bool showSlots = ready && data.Slots.Count > 0 && FavoritingInput.IsPlayerInventory(inventory);

            markers.Clear();
            for (int i = 0; i < elements.Count; i++) {
                SlotMarker marker = SlotMarker.For(elements[i]);
                marker.Reset(showSlots && data.Slots.Contains(elements[i].Position));
                markers.Add(marker);
            }

            // Custom marks live on the item and need no loaded character.
            if (ready || CustomMarks.Any) {
                // Same index vanilla's GetElement uses.
                int width = inventory.GetWidth();
                foreach (ItemDrop.ItemData item in inventory.GetAllItems()) {
                    int index = item.m_gridPos.y * width + item.m_gridPos.x;
                    if (item.m_gridPos.x < 0 || item.m_gridPos.y < 0 || index >= markers.Count) { continue; }
                    if (ready) { markers[index].ItemMark = Marks.GetItemMark(item); }
                    markers[index].CustomMark = CustomMarks.First(item);
                }
            }

            foreach (SlotMarker marker in markers) { marker.Apply(); }
            markers.Clear();
        }
    }
}
