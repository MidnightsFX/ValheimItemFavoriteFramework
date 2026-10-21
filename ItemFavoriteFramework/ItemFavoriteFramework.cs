using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using ItemFavoriteFramework.Common;

namespace ItemFavoriteFramework
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    // Jotunn only. This is a framework other mods depend on, so it must never declare a dependency --
    // not even a soft one -- on a mod that may depend on it: BepInEx refuses to start on the cycle.
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Favorites and wishlists live in the character file and on items, so neither side needs the mod for
    // those. Loot rolls and shared wishlists only work when the server runs it too; clients find out from
    // the server's answer to their Hello (Net/), so a mixed server never refuses anyone.
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    internal class ItemFavoriteFramework : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.ItemFavoriteFramework";
        public const string PluginName = "ItemFavoriteFramework";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal ValConfig cfg;
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        // Read by the template's Common/Jotunn* item and piece helpers. This mod ships no bundle, so it
        // stays null; load one here if a helper ever needs it.
        public static AssetBundle EmbeddedResourceBundle = null;

        public void Awake() {
            Log = this.Logger;
            cfg = new ValConfig(Config);
            LootConfig.Bind(Config);

            // All startup hooks should go after the config & Logger have been wired up

            LocalizationLoader.AddLocalizations();

            // Console commands. Registered here rather than from a Terminal/Console hook so the command
            // table also exists on a dedicated server, which never builds either. See Common/Terminal.
            TerminalManager.Init();

            // Polls the main config file for hand edits. This mod registers no yaml files, so the yaml
            // manager that would normally start the watcher is not initialized.
            ConfigFileWatcher.Initialize();

            // The template's helpers patch through their own Harmony ids, so this only picks up this mod's
            // own [HarmonyPatch] classes (Patches/ and Net/).
            new Harmony(PluginGUID).PatchAll(typeof(ItemFavoriteFramework).Assembly);

            // Alt + Shift + click adds an item to the wishlist, through the same hook other mods use.
            ItemClickHandlers.Add(WishlistPatches.HandleClick);
            // The wishlist editor, in the shared "Mod Config" menu on the main and pause menus.
            ConfigUILauncher.Register("Wishlist", WishlistPanel.Open);

            // Configs are not written until after they are all wired up, they exist in memory before this.
            // Flushing all of the configs at once is a significant speedup in mod load time
            ValConfig.SaveOnSet(true);
        }
    }
}
