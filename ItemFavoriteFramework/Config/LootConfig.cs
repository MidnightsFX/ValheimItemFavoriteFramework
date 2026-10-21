using BepInEx.Configuration;
using Jotunn.Utils;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Settings for the wishlist and loot rolls. Bound from Awake right after ValConfig, into the same file.
    //
    // Sections 5 and 6 are per-client. Section 7 is admin-only, so Jotunn syncs the server's values to
    // clients; the roll and sharing code still reads the server's numbers from its Welcome message rather
    // than from these, so a client never acts on its own copy before the sync lands.
    internal static class LootConfig {
        // 5 - Wishlist
        internal static ConfigEntry<KeyboardShortcut> WishlistModifierKeybind;
        internal static ConfigEntry<KeyboardShortcut> WishlistEditorKey;
        internal static ConfigEntry<Color> WishlistColor;
        internal static ConfigEntry<Color> WantedByOthersColor;
        internal static ConfigEntry<bool> DisplayWishlistTooltip;
        internal static ConfigEntry<bool> DisplayWantedBy;
        internal static ConfigEntry<bool> NotifyWishlistDrops;
        internal static ConfigEntry<bool> NotifyWishlistInContainers;
        internal static ConfigEntry<float> WishlistNotifyRange;
        internal static ConfigEntry<bool> ShareWishlist;

        // 6 - Loot rolls
        internal static ConfigEntry<bool> PreselectNeedForWishlisted;
        internal static ConfigEntry<bool> AutoVoteNeedForWishlisted;
        internal static ConfigEntry<bool> AutoLootWonItems;
        internal static ConfigEntry<float> AutoLootRange;
        internal static ConfigEntry<KeyboardShortcut> NeedKey;
        internal static ConfigEntry<KeyboardShortcut> GreedKey;
        internal static ConfigEntry<KeyboardShortcut> PassKey;
        internal static ConfigEntry<bool> ShowRollWindow;
        internal static ConfigEntry<bool> RollResultsInChat;

        // 7 - Server
        internal static ConfigEntry<bool> EnableLootRolls;
        internal static ConfigEntry<bool> EnableWishlistSharing;
        internal static ConfigEntry<int> RollSeconds;
        internal static ConfigEntry<float> ReserveMinutes;
        internal static ConfigEntry<int> MaxRollMembers;
        internal static ConfigEntry<float> MaxRequesterDistance;
        internal static ConfigEntry<int> MaxActiveRolls;
        internal static ConfigEntry<int> RollStartsPerMinutePerPlayer;
        internal static ConfigEntry<int> MaxWishlistEntries;
        internal static ConfigEntry<bool> OwnerRefusesLockedOwnership;
        internal static ConfigEntry<bool> AnnounceLockedPickups;

        internal static void Bind(ConfigFile cfg) {
            const string wishlist = "5 - Wishlist";
            WishlistModifierKeybind = Client(cfg, wishlist, "WishlistModifierKeybind", new KeyboardShortcut(KeyCode.LeftShift),
                "Hold this together with FavoritingModifierKeybind1 and left click an item to add it to your wishlist, or remove it.");
            WishlistEditorKey = Client(cfg, wishlist, "WishlistEditorKey", new KeyboardShortcut(KeyCode.None),
                "Opens the wishlist editor. Unbound by default; the editor is also in the Mod Config menu and the iff-wishlist command.");
            WishlistColor = Client(cfg, wishlist, "WishlistColor", new Color(0.95f, 0.36f, 0.55f),
                "Colour of the heart on items you wishlisted.");
            WantedByOthersColor = Client(cfg, wishlist, "WantedByOthersColor", new Color(0.45f, 0.75f, 1f),
                "Colour of the heart on items another player wishlisted.");
            DisplayWishlistTooltip = Client(cfg, wishlist, "DisplayWishlistTooltip", true,
                "Add a line to the tooltip of items on your wishlist.");
            DisplayWantedBy = Client(cfg, wishlist, "DisplayWantedBy", true,
                "Show which other players wishlisted an item, on its tooltip, on the ground and in roll windows.");
            NotifyWishlistDrops = Client(cfg, wishlist, "NotifyWishlistDrops", true,
                "Show a message when an item on your wishlist drops near you.");
            NotifyWishlistInContainers = Client(cfg, wishlist, "NotifyWishlistInContainers", true,
                "Show a message when a container you open holds an item on your wishlist.");
            WishlistNotifyRange = Client(cfg, wishlist, "WishlistNotifyRange", 40f,
                "How far away, in metres, a wishlisted drop can be and still be announced.", new AcceptableValueRange<float>(5f, 200f));
            ShareWishlist = Client(cfg, wishlist, "ShareWishlist", true,
                "Let other players on the server see your wishlist. Only works when the server runs this mod.");

            const string rolls = "6 - Loot rolls";
            PreselectNeedForWishlisted = Client(cfg, rolls, "PreselectNeedForWishlisted", true,
                "In the roll window, Need is preselected for items on your wishlist. Preselecting only highlights it; you still choose.");
            AutoVoteNeedForWishlisted = Client(cfg, rolls, "AutoVoteNeedForWishlisted", false,
                "Vote Need straight away on items your wishlist entry marks for automatic Need.");
            AutoLootWonItems = Client(cfg, rolls, "AutoLootWonItems", true,
                "Pick up items you won as soon as they are loaded near you.");
            AutoLootRange = Client(cfg, rolls, "AutoLootRange", 40f,
                "How far away, in metres, a won item can be and still be picked up automatically.", new AcceptableValueRange<float>(2f, 120f));
            NeedKey = Client(cfg, rolls, "NeedKey", new KeyboardShortcut(KeyCode.F6),
                "Votes Need on the oldest open roll.");
            GreedKey = Client(cfg, rolls, "GreedKey", new KeyboardShortcut(KeyCode.F7),
                "Votes Greed on the oldest open roll.");
            PassKey = Client(cfg, rolls, "PassKey", new KeyboardShortcut(KeyCode.F8),
                "Passes on the oldest open roll.");
            ShowRollWindow = Client(cfg, rolls, "ShowRollWindow", true,
                "Show open rolls in a window. When off, rolls are announced in chat and voted on with the keys or /need, /greed and /pass.");
            RollResultsInChat = Client(cfg, rolls, "RollResultsInChat", true,
                "Write each roll's result to the chat window.");

            const string server = "7 - Server";
            EnableLootRolls = ValConfig.BindServerConfig(server, "EnableLootRolls", false,
                "Allow mods to start need/greed/pass loot rolls on this server. Rolls only ever start when another mod asks for one.");
            EnableWishlistSharing = ValConfig.BindServerConfig(server, "EnableWishlistSharing", true,
                "Relay players' wishlists to everyone on the server.");
            RollSeconds = ValConfig.BindServerConfig(server, "RollSeconds", 30,
                "Longest a roll may stay open. A mod may ask for a shorter roll, never a longer one.", false, 10, 120);
            ReserveMinutes = ValConfig.BindServerConfig(server, "ReserveMinutes", 5f,
                "Longest a won item stays reserved for its winner before anyone may take it. 0 frees it at once.", false, 0f, 60f);
            MaxRollMembers = ValConfig.BindServerConfig(server, "MaxRollMembers", 10,
                "Most players one roll may include. Players a mod names past this many are left out of that roll.", false, 1, 64);
            MaxRequesterDistance = ValConfig.BindServerConfig(server, "MaxRequesterDistance", 100f,
                "How far, in metres, a client may be from a drop it asks to roll. 0 turns the check off.", false, 0f, 1000f);
            MaxActiveRolls = ValConfig.BindServerConfig(server, "MaxActiveRolls", 100,
                "Most rolls and reservations that may be open on the server at once.", false, 1, 500);
            RollStartsPerMinutePerPlayer = ValConfig.BindServerConfig(server, "RollStartsPerMinutePerPlayer", 30,
                "How many rolls one client may start per minute. Bursts of half this many are allowed.", false, 1, 600);
            MaxWishlistEntries = ValConfig.BindServerConfig(server, "MaxWishlistEntries", 100,
                "Most wishlist entries per player the server relays. 0 relays none.", false, 0, 500);
            OwnerRefusesLockedOwnership = ValConfig.BindServerConfig(server, "OwnerRefusesLockedOwnership", true,
                "A client that owns an item under a roll refuses to hand it to anyone but the winner.");
            AnnounceLockedPickups = ValConfig.BindServerConfig(server, "AnnounceLockedPickups", true,
                "Tell a roll's players when someone other than the winner took the item.");
        }

        private static ConfigEntry<T> Client<T>(ConfigFile cfg, string category, string key, T value, string description, AcceptableValueBase range = null) {
            return cfg.Bind(category, key, value, new ConfigDescription(description, range, new ConfigurationManagerAttributes { }));
        }
    }
}
