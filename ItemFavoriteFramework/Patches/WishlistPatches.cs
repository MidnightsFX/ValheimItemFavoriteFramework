using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // The heart in an inventory cell's top-right corner: the player's own colour for an item on their
    // wishlist, another colour for one only other players want. Its own child and component, beside the
    // favorite border (SlotMarker), so the two never fight over one image.
    internal class WishMarker : MonoBehaviour {
        private Image heart;
        private Color lastColor;
        private bool shown;

        internal static WishMarker For(InventoryElement element) {
            WishMarker marker = element.GetComponent<WishMarker>();
            if (marker == null) {
                marker = element.gameObject.AddComponent<WishMarker>();
                marker.Build(element);
            }
            return marker;
        }

        private void Build(InventoryElement element) {
            GameObject go = new GameObject("IFF_WishIcon", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(element.transform, worldPositionStays: false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-2f, -2f);
            rect.sizeDelta = new Vector2(16f, 16f);
            go.transform.SetAsLastSibling();
            heart = go.GetComponent<Image>();
            heart.sprite = SpriteFactory.Heart;
            heart.preserveAspect = true;
            heart.raycastTarget = false;
            heart.enabled = false;
        }

        // null: no heart.
        internal void Show(Color? color) {
            if (heart == null) { return; }
            if (color.HasValue == false) {
                if (shown) {
                    heart.enabled = false;
                    shown = false;
                }
                return;
            }
            if (shown == false) {
                heart.enabled = true;
                shown = true;
            }
            if (color.Value != lastColor) {
                heart.color = color.Value;
                lastColor = color.Value;
            }
        }
    }

    internal static class WishlistPatches {
        private static readonly List<WishMarker> markers = new List<WishMarker>();
        private static readonly List<Color?> wanted = new List<Color?>();
        private static bool reportedFailure;

        // After vanilla lays out a grid each frame (the player's, and an open container's).
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        internal static class GridHearts {
            private static void Postfix(InventoryGrid __instance) {
                try {
                    Draw(__instance);
                } catch (Exception e) {
                    if (reportedFailure == false) {
                        reportedFailure = true;
                        Logger.LogError($"Drawing wishlist hearts failed; further errors are suppressed: {e}");
                    }
                }
            }
        }

        private static void Draw(InventoryGrid grid) {
            Inventory inventory = grid.m_inventory;
            List<InventoryElement> elements = grid.m_elements;
            if (inventory == null || elements == null) { return; }

            wanted.Clear();
            for (int i = 0; i < elements.Count; i++) { wanted.Add(null); }
            int width = inventory.GetWidth();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems()) {
                int index = item.m_gridPos.y * width + item.m_gridPos.x;
                if (item.m_gridPos.x < 0 || item.m_gridPos.y < 0 || index >= wanted.Count) { continue; }
                if (Wishlists.IsWishlisted(item)) {
                    wanted[index] = LootConfig.WishlistColor.Value;
                } else if (LootConfig.DisplayWantedBy.Value && Wishlists.WantedBy(item).Count > 0) {
                    wanted[index] = LootConfig.WantedByOthersColor.Value;
                }
            }
            for (int i = 0; i < elements.Count; i++) {
                // Never create a marker just to hide it: most cells of most grids never show one.
                WishMarker marker = wanted[i].HasValue ? WishMarker.For(elements[i]) : elements[i].GetComponent<WishMarker>();
                if (marker != null) { marker.Show(wanted[i]); }
            }
        }

        // Every item tooltip goes through this static overload; see TooltipPatch. Recipe previews get the
        // lines too, so a wishlisted item shows as such at the workbench.
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
            new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
        [HarmonyAfter(ModCompat.EpicLootGuid)]
        internal static class Tooltip {
            private static readonly StringBuilder builder = new StringBuilder();

            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ItemDrop.ItemData item, bool appending, ref string __result) {
                if (appending) { return; }
                string lines = Lines(item, includeLocal: LootConfig.DisplayWishlistTooltip.Value);
                if (lines.Length == 0) { return; }
                builder.Clear();
                builder.Append(__result).Append('\n').Append(lines);
                __result = builder.ToString();
            }
        }

        // The wishlist lines for an item, coloured, each on its own line; "" when there are none.
        internal static string Lines(ItemDrop.ItemData item, bool includeLocal) {
            StringBuilder sb = new StringBuilder();
            if (includeLocal && Wishlists.IsWishlisted(item)) {
                Line(sb, LootConfig.WishlistColor.Value, RollClient.Localize("$iff_tooltip_wishlisted"));
            }
            if (LootConfig.DisplayWantedBy.Value) {
                List<string> names = Wishlists.WantedByNames(item);
                if (names.Count > 0) { Line(sb, LootConfig.WantedByOthersColor.Value, RollClient.Localize("$iff_tooltip_wanted_by", string.Join(", ", names))); }
            }
            return sb.ToString();
        }

        private static void Line(StringBuilder sb, Color color, string text) {
            if (sb.Length > 0) { sb.Append('\n'); }
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Color.Lerp(color, Color.white, 0.2f))).Append('>').Append(text).Append("</color>");
        }

        // Ground items: roll state and wishlist lines under the name. A postfix runs even though EpicLoot's
        // prefix skips the original for magic items, so this works for both.
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverText))]
        [HarmonyAfter(ModCompat.EpicLootGuid)]
        internal static class HoverText {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ItemDrop __instance, ref string __result) {
                if (__instance == null || Player.m_localPlayer == null) { return; }
                try {
                    string extra = LockLine(__instance);
                    string wish = Lines(__instance.m_itemData, includeLocal: true);
                    if (wish.Length > 0) { extra = extra.Length > 0 ? extra + "\n" + wish : wish; }
                    if (extra.Length > 0) { __result = __result + "\n" + extra; }
                } catch (Exception e) {
                    Logger.LogDebug($"Hover text for a drop failed: {e.Message}");
                }
            }

            private static string LockLine(ItemDrop drop) {
                LockState state = RollLocks.Resolve(drop, RollLocks.LocalPid);
                if (state == LockState.Free) { return ""; }
                if (state == LockState.Rolling) { return "<color=#FFC34D>" + RollClient.Localize("$iff_hover_rolling") + "</color>"; }
                ZDOID id = drop.m_nview.GetZDO().m_uid;
                string clock = PickupGatePatches.Clock(RollLocks.SecondsLeft(id));
                if (state == LockState.ReservedForMe) { return "<color=#7CFC7C>" + RollClient.Localize("$iff_hover_reserved_you", clock) + "</color>"; }
                string winner = RollLocks.TryGetEntry(id, out RollLocks.Entry entry) ? entry.WinnerName : "";
                return "<color=#FF8C69>" + RollClient.Localize("$iff_hover_reserved", winner, clock) + "</color>";
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        internal static class ContainerOpen {
            private static void Postfix(Container container) {
                if (container == null) { return; }
                try {
                    WishlistNotifier.OnContainerOpened(container);
                } catch (Exception e) {
                    Logger.LogDebug($"Wishlist check of an opened container failed: {e.Message}");
                }
            }
        }

        // Same two moments MarkStore reloads at: any reload of the local character, and the spawn.
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        internal static class PlayerLoad {
            private static void Postfix(Player __instance) {
                if (__instance == Player.m_localPlayer) { WishlistStore.Invalidate(); }
            }
        }

        [HarmonyPatch(typeof(Game), "SpawnPlayer")]
        internal static class GameSpawnPlayer {
            [HarmonyAfter("org.bepinex.plugins.servercharacters")]
            private static void Postfix() {
                WishlistStore.Invalidate();
                if (WishlistStore.TryGet(out _)) { WishlistStore.NotifyChanged(); }
            }
        }

        // Alt + Shift + left click (by default) adds the clicked item to the wishlist, or takes it off. Taken
        // through the item-click hook, ahead of the favoriting clicks and vanilla's split.
        internal static bool HandleClick(Inventory inventory, ItemDrop.ItemData item, int button) {
            if (button != ItemClickHandlers.Left) { return false; }
            if (FavoritingInput.IsHeld(ValConfig.FavoritingModifierKeybind1.Value) == false && FavoritingInput.IsHeld(ValConfig.FavoritingModifierKeybind2.Value) == false) { return false; }
            if (FavoritingInput.IsHeld(LootConfig.WishlistModifierKeybind.Value) == false) { return false; }
            Player player = Player.m_localPlayer;
            if (player == null || player.IsTeleporting()) { return false; }
            if (item == null) { return true; }

            WishResult result = Wishlists.Toggle(item);
            switch (result) {
                case WishResult.Added:
                    player.Message(MessageHud.MessageType.TopLeft, RollClient.Localize("$iff_msg_wish_added", ItemNames.Of(item)), 0, item.GetIcon());
                    break;
                case WishResult.Removed:
                    player.Message(MessageHud.MessageType.TopLeft, RollClient.Localize("$iff_msg_wish_removed", ItemNames.Of(item)), 0, item.GetIcon());
                    break;
                case WishResult.ReadOnly:
                    player.Message(MessageHud.MessageType.Center, "$iff_msg_wish_read_only");
                    break;
                case WishResult.Full:
                    player.Message(MessageHud.MessageType.Center, "$iff_msg_wish_full");
                    break;
            }
            return true;
        }
    }
}
