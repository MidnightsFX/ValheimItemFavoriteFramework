using HarmonyLib;
using System.Text;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Every item tooltip -- inventory grids, EpicLoot's enchanting lists and Temper rows, the trader --
    // is built by this static overload (the instance GetTooltip() just forwards to it), so one postfix
    // covers them all.
    //
    // After EpicLoot, which replaces magic items' tooltips in a prefix and recolours the result in a
    // Priority.Last postfix; running after that keeps this line's colours intact.
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    [HarmonyAfter(ModCompat.EpicLootGuid)]
    internal static class TooltipPatch {
        private static readonly StringBuilder builder = new StringBuilder();

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ItemDrop.ItemData item, bool crafting, bool appending, ref string __result) {
            // crafting: the recipe preview describes an item that does not exist yet. appending: the
            // secondary tooltip vanilla attaches to another one, which already got the line.
            if (crafting || appending || ValConfig.DisplayTooltipHint.Value == false) { return; }

            int flags = Marks.GetMarks(item);
            CustomMarkType firstCustom = CustomMarks.First(item);
            if (flags == 0 && firstCustom == null) { return; }

            builder.Clear();
            builder.Append(__result);
            builder.Append('\n');
            if ((flags & Marks.FlagItemFavorite) != 0) {
                AppendLine(ValConfig.BorderColorFavoritedItem.Value, "$iff_tooltip_favorited");
            }
            if ((flags & Marks.FlagSlotFavorite) != 0) {
                AppendLine(ValConfig.BorderColorFavoritedSlot.Value, "$iff_tooltip_favorited_slot");
            }
            if ((flags & Marks.FlagTrash) != 0) {
                // The trash border colour is a deep red, unreadable as text on the dark tooltip.
                AppendLine(Color.Lerp(ValConfig.BorderColorTrashFlaggedItem.Value, Color.white, 0.4f), "$iff_tooltip_trash");
            }
            if (firstCustom != null) {
                foreach (CustomMarkType custom in CustomMarks.All(item)) {
                    if (custom.TooltipToken.Length > 0) { AppendLine(Color.Lerp(custom.Color, Color.white, 0.3f), custom.TooltipToken); }
                }
            }
            __result = builder.ToString();
        }

        // Localized here rather than left as a token: not every mod that shows GetTooltip() localizes it.
        private static void AppendLine(Color color, string token) {
            builder.Append("\n<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>');
            builder.Append(Localization.instance != null ? Localization.instance.Localize(token) : token);
            builder.Append("</color>");
        }
    }
}
