using BepInEx.Bootstrap;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Other inventory mods whose input or data overlaps this one. Only their presence is read; nothing
    // here reflects into them.
    //
    // There is deliberately no BepInDependency on any of them (see the plugin class), so a mod may load
    // after this one and is not in Chainloader.PluginInfos during Awake yet. Presence is therefore
    // looked up on first use -- in game, long after every plugin has loaded.
    internal static class ModCompat {
        internal const string QuickStackStoreGuid = "goldenrevolver.quick_stack_store";
        internal const string AzuAutoStoreGuid = "Azumatt.AzuAutoStore";
        // Only used as a Harmony id for patch ordering, which needs no load-order dependency.
        internal const string EpicLootGuid = "randyknapp.mods.epicloot";

        private static bool? quickStackStoreLoaded;
        private static bool? azuAutoStoreLoaded;
        private static bool warned;

        internal static bool QuickStackStoreLoaded {
            get {
                if (quickStackStoreLoaded == null) { quickStackStoreLoaded = Chainloader.PluginInfos.ContainsKey(QuickStackStoreGuid); }
                return quickStackStoreLoaded.Value;
            }
        }

        internal static bool AzuAutoStoreLoaded {
            get {
                if (azuAutoStoreLoaded == null) { azuAutoStoreLoaded = Chainloader.PluginInfos.ContainsKey(AzuAutoStoreGuid); }
                return azuAutoStoreLoaded.Value;
            }
        }

        // AzuAutoStore stores the clicked item on a bare middle click and does not check for other
        // modifiers, so a middle-click trash flag would also ship the item to a chest.
        internal static bool MiddleClickTrashActive => ValConfig.MiddleClickTrash.Value && AzuAutoStoreLoaded == false;

        // Once per session, from the first character spawn, so the warning lands where a player reading
        // the log after a confusing click would look for it.
        internal static void WarnOnce() {
            if (warned) { return; }
            warned = true;

            // BepInEx's HarmonyX runs every prefix even after one returns false, so both mods act on the
            // same click; neither can suppress the other.
            if (QuickStackStoreLoaded) {
                Logger.LogWarning("Quick Stack Store Sort is installed. It also favorites on Alt + click, and both mods act on the same click. " +
                    "Unbind one of the two favoriting keys; this mod's are FavoritingModifierKeybind1/2 in its config.");
            }
            if (AzuAutoStoreLoaded) {
                Logger.LogWarning("AzuAutoStore is installed. It keeps its own favorites, which it may read from Quick Stack Store Sort, not from this mod, " +
                    "and it stores items on middle click, so this mod's middle-click trash flag is disabled.");
            }
        }
    }
}
