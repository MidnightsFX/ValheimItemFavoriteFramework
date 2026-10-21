using HarmonyLib;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Favoriting clicks, taken on mouse *down* like Quick Stack Store Sort, before vanilla starts a drag
    // or a split. Returning false skips vanilla; other mods' prefixes still run (HarmonyX runs them all).
    // Other mods' click handlers (API 2) are asked first.
    //
    // The parameter names must match vanilla's, which differ between the two methods.
    [HarmonyPatch(typeof(InventoryGrid))]
    internal static class GridInputPatches {

        [HarmonyPatch("OnLeftDown")]
        [HarmonyPrefix]
        private static bool OnLeftDown(InventoryGrid __instance, UIInputHandler clickHandler) {
            Vector2i pos = __instance.GetButtonPos(clickHandler.gameObject);
            if (pos.x < 0) { return true; }
            if (ItemClickHandlers.Handle(__instance, pos, ItemClickHandlers.Left)) { return false; }
            if (FavoritingInput.CanHandleClick() == false) { return true; }
            return FavoritingInput.HandleItemClick(__instance, pos, trash: FavoritingInput.IsTrashChordHeld()) == false;
        }

        [HarmonyPatch("OnRightDown")]
        [HarmonyPrefix]
        private static bool OnRightDown(InventoryGrid __instance, UIInputHandler element) {
            Vector2i pos = __instance.GetButtonPos(element.gameObject);
            if (pos.x < 0) { return true; }
            if (ItemClickHandlers.Handle(__instance, pos, ItemClickHandlers.Right)) { return false; }
            if (FavoritingInput.CanHandleClick() == false) { return true; }
            return FavoritingInput.HandleSlotClick(__instance, pos) == false;
        }
    }
}
