using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130
    internal class ValConfig {
        public static ConfigFile cfg;
        
        // Add Client sided config entries under here
        public static ConfigEntry<bool> EnableDebugMode;
        public static ConfigEntry<bool> EnableTerminalColors;

        public static ConfigEntry<KeyboardShortcut> FavoritingModifierKeybind1;
        public static ConfigEntry<KeyboardShortcut> FavoritingModifierKeybind2;
        public static ConfigEntry<KeyboardShortcut> TrashModifierKeybind;
        public static ConfigEntry<bool> MiddleClickTrash;
        public static ConfigEntry<bool> AllowMarkingInContainers;
        public static ConfigEntry<bool> DisplayTooltipHint;

        public static ConfigEntry<Color> BorderColorFavoritedItem;
        public static ConfigEntry<Color> BorderColorFavoritedSlot;
        public static ConfigEntry<Color> BorderColorTrashFlaggedItem;
        public static ConfigEntry<Color> IconOverlayTrashFlaggedItem;

        public static ConfigEntry<bool> ProtectFavoritesFromStackAll;
        public static ConfigEntry<bool> PreventAutoPickupOfTrash;

        public static ConfigEntry<bool> ImportQuickStackStoreFavorites;

        // Add Server synced config entries under here
        public static ConfigEntry<int> InMemoryModificationsPerTick;
        public static ConfigEntry<float> ConfigApplyDelay;
        public static ConfigEntry<float> ConfigPollIntervalSeconds;

        // Derived from the plugin rather than written out, because scripts/RenameSolution.ps1 only
        // rewrites the .sln, .cs, .csproj and AssemblyInfo.cs -- it never touches Common/. A hardcoded
        // literal here means every mod copied from this template ships with its config folder still
        // called "ItemFavoriteFramework". PluginName is rewritten, so deriving it fixes that for free.
        public static readonly string cfgFolder = ItemFavoriteFramework.PluginName;

        public ValConfig(ConfigFile cf) {
            // ensure all the config values are created
            cfg = cf;
            cfg.SaveOnConfigSet = true;
            CreateConfigValues(cf);
            Logger.SetDebugLogging(EnableDebugMode.Value);

            // Watch #1. Registering before ConfigFileWatcher.Initialize() only touches a dictionary, so
            // ordering against YamlConfigManager.Init() does not matter.
            ConfigFileWatcher.Register(cfg.ConfigFilePath, OnMainConfigFileChanged);
        }

        // Every setting this mod reads is per-client, so every machine reloads its own file. Only the
        // template's unused "Config" section is server-synced, and a local reload of that is harmless.
        //
        // Not routed through ConfigChangeDebouncer -- Reload raises SettingChanged per entry, and those
        // are already debounced individually by whichever handler is attached to them.
        private static void OnMainConfigFileChanged(string _) {
            Logger.LogInfo("Configuration file has been changed, reloading settings.");
            cfg.Reload();
        }

        public static void SaveOnSet(bool enabled) {
            cfg.SaveOnConfigSet = enabled;
            cfg.Save();
        }

        private void CreateConfigValues(ConfigFile Config) {
            // Debugmode
            EnableDebugMode = Config.Bind("Client config", "EnableDebugMode", false,
                new ConfigDescription("Enables Debug logging.",
                null,
                new ConfigurationManagerAttributes { IsAdvanced = true }));
            EnableDebugMode.SettingChanged += Logger.EnableDebugLogging;
            Logger.CheckEnableDebugLogging();

            // Read by TerminalOutput. Only affects what the console shows; the BepInEx log is always
            // written as plain text.
            EnableTerminalColors = Config.Bind("Client config", "EnableTerminalColors", true,
                new ConfigDescription("Colours this mod's console command output by severity.",
                null,
                new ConfigurationManagerAttributes { }));

            // Defaults follow Quick Stack Store Sort, so its users keep their muscle memory.
            const string input = "1 - Favoriting";
            FavoritingModifierKeybind1 = BindClient(input, "FavoritingModifierKeybind1", new KeyboardShortcut(KeyCode.LeftAlt),
                "Hold this while clicking in the inventory: left click favorites an item, right click favorites a slot. Also the key the trash chord needs.");
            FavoritingModifierKeybind2 = BindClient(input, "FavoritingModifierKeybind2", new KeyboardShortcut(KeyCode.RightAlt),
                "Alternative to FavoritingModifierKeybind1 for favoriting items and slots. Does not count for the trash chord, because AltGr reports as Left Control + Right Alt.");
            TrashModifierKeybind = BindClient(input, "TrashModifierKeybind", new KeyboardShortcut(KeyCode.LeftControl),
                "Hold this together with FavoritingModifierKeybind1 and left click an item to flag it as trash.");
            MiddleClickTrash = BindClient(input, "MiddleClickTrash", true,
                "While a favoriting key is held, middle click flags an item as trash. Ignored while AzuAutoStore is installed, which stores the item on middle click.");
            AllowMarkingInContainers = BindClient(input, "AllowMarkingInContainers", true,
                "Allow favoriting and trash flagging items inside an open container, not just in your own inventory.");
            DisplayTooltipHint = BindClient(input, "DisplayTooltipHint", true,
                "Add a line to the tooltip of favorited and trash-flagged items.");

            const string colors = "2 - Colors";
            // A cell's border is two woven cords. A favorited slot holding a marked item shows both colours:
            // the slot's on the outer cord, the item's on the inner one.
            BorderColorFavoritedItem = BindClient(colors, "BorderColorFavoritedItem", new Color(1f, 0.8482759f, 0f),
                "Woven border of a slot holding a favorited item. In a favorited slot it colours the inner cord.");
            BorderColorFavoritedSlot = BindClient(colors, "BorderColorFavoritedSlot", new Color(0f, 0.5f, 1f),
                "Woven border of a favorited slot. When the slot also holds a marked item it colours the outer cord.");
            BorderColorTrashFlaggedItem = BindClient(colors, "BorderColorTrashFlaggedItem", new Color(0.5f, 0f, 0f),
                "Woven border of a slot holding a trash-flagged item. In a favorited slot it colours the inner cord.");
            IconOverlayTrashFlaggedItem = BindClient(colors, "IconOverlayTrashFlaggedItem", new Color(0.4f, 0.4f, 0.4f, 0.45f),
                "Laid over a trash-flagged item's icon to grey it out. Its alpha sets how strongly; 0 turns it off.");

            const string protection = "3 - Protection";
            ProtectFavoritesFromStackAll = BindClient(protection, "ProtectFavoritesFromStackAll", true,
                "The container 'Place stacks' button and hold-to-stack leave favorited items, and anything in a favorited slot, in your inventory.");
            PreventAutoPickupOfTrash = BindClient(protection, "PreventAutoPickupOfTrash", false,
                "Trash-flagged items on the ground are not picked up automatically. You can still pick them up by hand.");

            const string compat = "4 - Compatibility";
            ImportQuickStackStoreFavorites = BindClient(compat, "ImportQuickStackStoreFavorites", true,
                "The first time a character loads, copy its Quick Stack Store Sort favorites, favorited slots and trash flags. Runs once per character; the iff-import-qss command runs it again.");

            // Instantiate server synced config entries here
            InMemoryModificationsPerTick = BindServerConfig("Config", "Updates Per Tick", 20, "Number of updates per tick that are applied when modifying items or pieces.", true, 1, 150);
            ConfigApplyDelay = BindServerConfig("Config", "Config Apply Delay", 1f, "Delay in seconds before a changed config entry is applied in-game. Coalesces a burst of rapid edits (typing, file reloads, server sync) into a single apply. Set to 0 to apply instantly.", true, 0f, 10f);

            // Read by Common/Config/ConfigFileWatcher every frame, so it is bound rather than hardcoded:
            // an admin editing yaml wants a short interval, a busy server wants a long one.
            ConfigPollIntervalSeconds = BindServerConfig("Config", "Config Poll Interval", 30f, "Seconds between checks for edits to this mod's yaml config files and its BepInEx config file. Lower reacts faster to a hand edit, higher does less disk work.", true, 1f, 300f);
        }

        // A per-machine setting: never synced, never admin-only.
        private static ConfigEntry<T> BindClient<T>(string category, string key, T value, string description) {
            return cfg.Bind(category, key, value,
                new ConfigDescription(description, null, new ConfigurationManagerAttributes { }));
        }

        // Every overload below marks the entry IsAdminOnly, which is what makes Jotunn's
        // SynchronizationManager push the server's value to every client. Use cfg.Bind directly for
        // anything that is genuinely per-machine (UI, logging, client-side toggles).
        //
        // The numeric overloads come in two shapes: pass a min and max for the common bounded case, or
        // pass an AcceptableValueBase (null for unbounded) when a range is wrong for the setting.

        /// <summary>
        /// Binds a server-synced float array within a shared range.
        /// </summary>
        /// <param name="category">Config file section.</param>
        /// <param name="key">Entry name within the section.</param>
        /// <param name="value">Default value.</param>
        /// <param name="description">Shown in the config file and the Configuration Manager.</param>
        /// <param name="advanced">Hides the entry behind the Advanced toggle.</param>
        /// <param name="valMin">Lowest accepted value.</param>
        /// <param name="valMax">Highest accepted value.</param>
        public static ConfigEntry<float[]> BindServerConfig(string category, string key, float[] value, string description, bool advanced = false, float valMin = 0, float valMax = 150) {
            return cfg.Bind(category, key, value,
                new ConfigDescription(description,
                new AcceptableValueRange<float>(valMin, valMax),
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Binds a server-synced bool.
        /// </summary>
        /// <param name="category">Config file section.</param>
        /// <param name="key">Entry name within the section.</param>
        /// <param name="value">Default value.</param>
        /// <param name="description">Shown in the config file and the Configuration Manager.</param>
        /// <param name="acceptableValues">Optional constraint. Normally null for a bool.</param>
        /// <param name="advanced">Hides the entry behind the Advanced toggle.</param>
        public static ConfigEntry<bool> BindServerConfig(string category, string key, bool value, string description, AcceptableValueBase acceptableValues = null, bool advanced = false) {
            return cfg.Bind(category, key, value,
                new ConfigDescription(description,
                    acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Binds a server-synced int constrained to a range.
        /// </summary>
        /// <param name="category">Config file section.</param>
        /// <param name="key">Entry name within the section.</param>
        /// <param name="value">Default value.</param>
        /// <param name="description">Shown in the config file and the Configuration Manager.</param>
        /// <param name="advanced">Hides the entry behind the Advanced toggle.</param>
        /// <param name="valMin">Lowest accepted value.</param>
        /// <param name="valMax">Highest accepted value.</param>
        public static ConfigEntry<int> BindServerConfig(string category, string key, int value, string description, bool advanced = false, int valMin = 0, int valMax = 150) {
            return cfg.Bind(category, key, value,
                new ConfigDescription(description,
                new AcceptableValueRange<int>(valMin, valMax),
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Binds a server-synced int with an explicit constraint. Pass null for an unbounded value.
        /// </summary>
        /// <param name="category">Config file section.</param>
        /// <param name="key">Entry name within the section.</param>
        /// <param name="value">Default value.</param>
        /// <param name="description">Shown in the config file and the Configuration Manager.</param>
        /// <param name="acceptableValues">Constraint to apply, or null for no constraint.</param>
        /// <param name="advanced">Hides the entry behind the Advanced toggle.</param>
        public static ConfigEntry<int> BindServerConfig(string category, string key, int value, string description, AcceptableValueBase acceptableValues, bool advanced = false) {
            return cfg.Bind(category, key, value,
                new ConfigDescription(description,
                acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Binds a server-synced float constrained to a range.
        /// </summary>
        /// <param name="category">Config file section.</param>
        /// <param name="key">Entry name within the section.</param>
        /// <param name="value">Default value.</param>
        /// <param name="description">Shown in the config file and the Configuration Manager.</param>
        /// <param name="advanced">Hides the entry behind the Advanced toggle.</param>
        /// <param name="valMin">Lowest accepted value.</param>
        /// <param name="valMax">Highest accepted value.</param>
        public static ConfigEntry<float> BindServerConfig(string category, string key, float value, string description, bool advanced = false, float valMin = 0, float valMax = 150) {
            return cfg.Bind(category, key, value,
                new ConfigDescription(description,
                new AcceptableValueRange<float>(valMin, valMax),
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Binds a server-synced float with an explicit constraint. Pass null for an unbounded value.
        /// </summary>
        /// <param name="category">Config file section.</param>
        /// <param name="key">Entry name within the section.</param>
        /// <param name="value">Default value.</param>
        /// <param name="description">Shown in the config file and the Configuration Manager.</param>
        /// <param name="acceptableValues">Constraint to apply, or null for no constraint.</param>
        /// <param name="advanced">Hides the entry behind the Advanced toggle.</param>
        public static ConfigEntry<float> BindServerConfig(string category, string key, float value, string description, AcceptableValueBase acceptableValues, bool advanced = false) {
            return cfg.Bind(category, key, value,
                new ConfigDescription(description,
                acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }

        /// <summary>
        /// Binds a server-synced string, optionally restricted to a fixed list of values.
        /// </summary>
        /// <param name="category">Config file section.</param>
        /// <param name="key">Entry name within the section.</param>
        /// <param name="value">Default value.</param>
        /// <param name="description">Shown in the config file and the Configuration Manager.</param>
        /// <param name="acceptableValues">Allowed values, or null to accept anything.</param>
        /// <param name="advanced">Hides the entry behind the Advanced toggle.</param>
        public static ConfigEntry<string> BindServerConfig(string category, string key, string value, string description, AcceptableValueList<string> acceptableValues = null, bool advanced = false) {
            return cfg.Bind(category, key, value,
                new ConfigDescription(
                    description,
                    acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
                );
        }
    }
}
