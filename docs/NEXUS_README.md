Item Favorite Framework

Mark the items you want to keep, and the ones you don't, once. Every mod that supports the framework then respects those marks. Keep a wishlist of the gear you are after, and share loot fairly with need/greed/pass rolls.

[size=5][b]Features[/b][/size]

[list]
[*][b]Favorite items.[/b] Storing, stacking and sorting leave them in your inventory, and destructive features never offer them.[/*]
[*][b]Favorite inventory slots.[/b] Whatever you keep in that cell stays put.[/*]
[*][b]Flag items as trash.[/b] Mods that clean up after you know what can go. The framework itself never destroys anything.[/*]
[*][b]Vanilla "Place stacks" works with it out of the box.[/b] The container button and holding Use on a chest leave favorites, and anything in a favorited slot, in your inventory.[/*]
[*][b]Optionally, trash on the ground is not auto-picked up.[/b] You can still pick it up by hand.[/*]
[*][b]Quick Stack Store Sort favorites are copied over[/b] the first time each character loads.[/*]
[*][b]Wishlist.[/b] Mark the items you want. They get a heart wherever they appear, and you're told when one drops near you or sits in a chest you open. With Epic Loot you can ask for a rarity, a unique item, a set or particular effects.[/*]
[*][b]Shared wishlists.[/b] On a server running the mod, everyone sees who wants what.[/*]
[*][b]Need / greed / pass loot rolls.[/b] When a mod hands a drop to the framework (Epic Loot can, for its magic items), the players it names roll for it. The winner gets it; nobody else can grab it first.[/*]
[*][b]Favorites and the wishlist are client-only[/b], saved with your character. Shared wishlists and loot rolls need the mod on the server too; without it they simply stay off.[/*]
[/list]

[size=4][b]Controls[/b][/size]

While the inventory is open, hold [b]Alt[/b]:

[list]
[*][b]Toggle Favorite[/b] - Alt + left click an item
[img]https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_favorite.png?raw=true[/img][/*]
[*][b]Toggle Slot Favorite[/b] - Alt + right click a slot
[img]https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_slot.png?raw=true[/img] [img]https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_slot_favorite.png?raw=true[/img][/*]
[*][b]Toggle Trash[/b] - Alt + middle click an item
[img]https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_trash.png?raw=true[/img][/*]
[*][b]Toggle Trash (Alternative)[/b] - Left Alt + Left Ctrl + left click an item
[img]https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_trash.png?raw=true[/img][/*]
[*][b]Add to/remove from Wishlist[/b] - Left Alt + Left Shift + left click an item
[img]https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/wishlist_heart.png?raw=true[/img][/*]
[/list]

Items can be marked inside an open chest too. Slots can only be favorited in your own inventory.

When a roll comes up, vote in the roll window (click it while the inventory, map or chat is open), with [b]F6[/b] (Need), [b]F7[/b] (Greed) and [b]F8[/b] (Pass), or type [font=Courier New]/need[/font], [font=Courier New]/greed[/font] or [font=Courier New]/pass[/font] in chat. The wishlist editor is in the [b]Mod Config[/b] menu on the main and pause menus.

Marked cells get a woven border: gold for a favorite item, blue for a favorite slot, and dark red for trash, whose icon is also greyed out. A favorite slot holding a marked item weaves both colours, the slot's blue outside and the item's colour inside. Tooltips say why.

[b]Weapons, armor and tools are marked individually.[/b] Favoriting your best sword leaves every other sword alone, and the mark stays on that sword wherever it goes: into chests, onto the ground, into your tombstone, through upgrades. [b]Stackable items are marked by type[/b], so favoriting one stack of resin favorites all your resin.

Favorite and trash are exclusive: unflag one before setting the other.

[size=4][b]Configuration[/b][/size]

In [font=Courier New]BepInEx/config/MidnightsFX.ItemFavoriteFramework.cfg[/font]. Sections 1 to 6 are per-client:

[list]
[*][b]1 - Favoriting:[/b] the favoriting keys, the trash key, middle-click trash, marking inside containers, and tooltip lines.[/*]
[*][b]2 - Colors:[/b] border colours, and how strongly trash icons are greyed out.[/*]
[*][b]3 - Protection:[/b] Place stacks protection (on by default) and skipping trash on auto-pickup (off by default).[/*]
[*][b]4 - Compatibility:[/b] the Quick Stack Store Sort import.[/*]
[*][b]5 - Wishlist:[/b] the wishlist key, heart colours, tooltip lines, drop and chest notices, and whether others may see your wishlist.[/*]
[*][b]6 - Loot rolls:[/b] the vote keys, the roll window, preselecting Need for wishlisted items, and picking up what you won automatically.[/*]
[/list]

Section [b]7 - Server[/b] is set by the server and synced to everyone: whether loot rolls are allowed (off by default), whether wishlists are shared, how long rolls and reservations last, and limits.

Console commands: [font=Courier New]iff-status[/font], [font=Courier New]iff-list[/font], [font=Courier New]iff-clear <favorites|trash|slots|all>[/font], [font=Courier New]iff-import-qss [force][/font], [font=Courier New]iff-wishlist[/font] (and [font=Courier New]-list[/font], [font=Courier New]-add[/font], [font=Courier New]-remove[/font], [font=Courier New]-clear[/font], [font=Courier New]-show[/font]), [font=Courier New]iff-net-status[/font], [font=Courier New]iff-roll-status[/font], [font=Courier New]iff-roll-test[/font] (admins), [font=Courier New]iff-need[/font], [font=Courier New]iff-greed[/font], [font=Courier New]iff-pass[/font], [font=Courier New]iff-help[/font].

[size=4][b]Compatibility[/b][/size]

[list]
[*][b]Epic Loot:[/b] the enchanting table leaves favorites out of the screens that use items up, such as Sacrifice, and shows a star on them. Wishlist entries can ask for a rarity, unique item, set or effects, and the server can have Epic Loot's magic drops rolled for among the players nearby.[/*]
[*][b]Quick Stack Store Sort[/b] also favorites on Alt + click, and both mods act on the same click. After the first import the two sets of favorites are separate, so unbind one mod's favoriting keys.[/*]
[*][b]AzuAutoStore[/b] stores items on middle click, so middle-click trash is off while it is installed. Use Left Alt + Left Ctrl + left click instead.[/*]
[/list]

[line]

[size=5][b]For mod authors: the API[/b][/size]

The same guide is on GitHub, where the code is easier to read: [url=https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/API.md]docs/API.md[/url].

Item Favorite Framework (IFF) owns three kinds of player marks and exposes them to other mods:

[list]
[*][b]Favorite item[/b] - Set with (defaults): Alt + left click. Applies to: a specific item (weapons, armor, tools — anything that cannot stack), or a whole item type (stackables).[/*]
[*][b]Trash flag[/b] - Set with (defaults): Alt + middle click, or Left Alt + Left Ctrl + left click. Applies to: same granularity as favorites.[/*]
[*][b]Favorite slot[/b] - Set with (defaults): Alt + right click. Applies to: a cell of the player's inventory grid.[/*]
[/list]

Use it so your mod never moves or destroys what the player wants kept: skip protected items when storing, stacking, sorting, sacrificing or salvaging, and show the player that you did.

Mods can also draw marks of their own on items (API 2), read and fill each player's [b]wishlist[/b] of wanted items, and ask IFF to run MMO-style [b]need/greed/pass loot rolls[/b] among players they name (API 3).

IFF's plugin GUID is [font=Courier New]MidnightsFX.ItemFavoriteFramework[/font]. Favorites, custom marks and the wishlist itself work client-only. Shared wishlists and loot rolls need IFF on the server as well; a server without it simply never offers them.

[size=4][b]Getting started[/b][/size]

[size=3][b]Three ways in[/b][/size]

[list]
[*][b][font=Courier New]ItemFavoriteFrameworkAPI.ItemFavorites[/font][/b] - What it is: A small shim ([url=https://github.com/MidnightsFX/ValheimItemFavoriteFramework/tree/master/ItemFavoriteFrameworkAPI][font=Courier New]ItemFavoriteFrameworkAPI/[/font][/url]) that binds every endpoint to a typed delegate once. Bundle the DLL with ILRepack, or copy its single source file into your project. When to use it: Almost always. Safe to call when IFF is not installed.[/*]
[*][b][font=Courier New]ItemFavoriteFramework.API[/font][/b] - What it is: The public static class inside [font=Courier New]ItemFavoriteFramework.dll[/font]. When to use it: You hard-depend on IFF and reference its DLL directly.[/*]
[*][b]Reflection[/b] - What it is: [font=Courier New]Type.GetType("ItemFavoriteFramework.API, ItemFavoriteFramework")[/font]. When to use it: You already have your own glue. Every signature can be bound with [font=Courier New]Delegate.CreateDelegate[/font].[/*]
[/list]

The rest of this page uses the shim. [i]Calling IFF without the shim[/i] covers the other two.

[size=3][b]Set up the shim[/b][/size]

[list=1]
[*]Add the shim to your plugin, in one of two ways:
[list]
[*][b]Copy[/b] [url=https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/ItemFavoriteFrameworkAPI/src/ItemFavorites.cs][font=Courier New]ItemFavorites.cs[/font][/url] into your project. It is one file with no dependencies beyond the game.[/*]
[*][b]Bundle[/b] [font=Courier New]ItemFavoriteFrameworkAPI.dll[/font] into your plugin with ILRepack. How to build it is in the [url=https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/ItemFavoriteFrameworkAPI/README.md]shim's README[/url].[/*]
[/list][/*]
[*]Declare a soft dependency, so IFF loads before your plugin.[/*]
[*]Call it.[/*]
[/list]

[code]using System.Linq;
using BepInEx;
using ItemFavoriteFrameworkAPI;

[BepInPlugin("you.MyStorageMod", "My Storage Mod", "1.0.0")]
[BepInDependency(ItemFavorites.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class MyStorageMod : BaseUnityPlugin {
    private void Awake() {
        Logger.LogInfo(ItemFavorites.IsLoaded()
            ? $"Item Favorite Framework {ItemFavorites.GetPluginVersion()} found, API {ItemFavorites.GetApiVersion()}."
            : "Item Favorite Framework is not installed; nothing is protected.");
    }

    internal static void StoreAll(Inventory from, Inventory to) {
        foreach (ItemDrop.ItemData item in from.GetAllItems().ToList()) {
            if (ItemFavorites.IsProtected(item)) { continue; }   // favorited, or in a favorited slot
            // ... move it
        }
    }
}[/code]

The shim looks IFF up once, on its first call, and never again. Without the soft dependency your plugin may load first, find nothing, and stay a no-op for the whole session.

[size=3][b]When IFF is missing or older[/b][/size]

Every shim method is safe when IFF is not installed, and when the installed IFF is older and lacks that endpoint. It then returns a default and does nothing:

[list]
[*][b]queries[/b] - [font=Courier New]false[/font], [font=Courier New]0[/font], an empty list, or [font=Courier New]"[]"[/font][/*]
[*][b]changes and registrations[/b] - [font=Courier New]false[/font][/*]
[*][b][font=Courier New]GetFavoriteIcon[/font], [font=Courier New]GetTrashIcon[/font], [font=Courier New]GetWishlistIcon[/font][/b] - [font=Courier New]null[/font][/*]
[*][b]colours[/b] - IFF's default colours[/*]
[*][b][font=Courier New]GetServerFeaturesJson[/font][/b] - [font=Courier New]{"iff":false}[/font][/*]
[*][b][font=Courier New]GetRollStateJson[/font][/b] - [font=Courier New]{"phase":"free"}[/font][/*]
[*][b][font=Courier New]CanPlayerPickUp[/font][/b] - [font=Courier New]true[/font][/*]
[*][b][font=Courier New]StartRoll[/font], [font=Courier New]StartRollForItem[/font][/b] - [font=Courier New]false[/font], with [font=Courier New]error[/font] set to [font=Courier New]notInstalled[/font][/*]
[/list]

[font=Courier New]IsLoaded()[/font] tells you whether IFF is there at all. To use something newer than the oldest IFF you support, check for it:

[code]bool hasCustomMarks = ItemFavorites.HasEndpoint("RegisterMarkType");   // API 2 or later
bool hasWishlists = ItemFavorites.GetApiVersion() >= 3;[/code]

[size=4][b]Rules every endpoint follows[/b][/size]

[list]
[*][b]Main thread only.[/b] Call from Unity callbacks or Harmony patches, never from a worker thread.[/*]
[*][b]The local player.[/b] Favorites, trash flags, slots and the wishlist answer for the local character. Before one is loaded (main menu, character select) queries return [font=Courier New]false[/font], [font=Courier New]0[/font] or an empty list, changes are refused, and [font=Courier New]IsReady()[/font] is [font=Courier New]false[/font]. The changed listener fires once a character's marks load, so a UI can wait for it instead of polling.[/*]
[*][b]Register once, early.[/b] Listeners, click handlers, mark types and providers can be added in [font=Courier New]Awake[/font]. They last for the whole session, across logouts and world changes.[/*]
[*][b]Delegates are kept by equality.[/b] Adding the same delegate twice adds it once. To remove one, pass the same method group, or keep the lambda in a field:[/*]
[/list]

[code]private static readonly Action onMarksChanged = () => myPanel.Refresh();

ItemFavorites.AddChangedListener(onMarksChanged);
ItemFavorites.RemoveChangedListener(onMarksChanged);[/code]

[list]
[*][b]Your bugs stay yours.[/b] A listener, handler or provider that throws is logged with its declaring type and does not stop the others.[/*]
[/list]

[size=3][b]The signature rule[/b][/size]

Only primitives, [font=Courier New]string[/font], vanilla/Unity types ([font=Courier New]ItemDrop.ItemData[/font], [font=Courier New]ItemDrop[/font], [font=Courier New]Inventory[/font], [font=Courier New]Character[/font], [font=Courier New]Vector2i[/font], [font=Courier New]Vector3[/font], [font=Courier New]Sprite[/font], [font=Courier New]Color[/font]), lists of those, and [font=Courier New]System.Func[/font]/[font=Courier New]Action[/font] of those cross the boundary. No IFF type appears in any signature, so a consumer never needs IFF's assembly to compile. Structured data travels as JSON strings, and an endpoint that hands back more than one value uses [font=Courier New]ref[/font] parameters (not [font=Courier New]out[/font]), so reflection callers can read them. Existing signatures never change; additions bump [font=Courier New]ApiVersion[/font].

[size=3][b]Reading the JSON[/b][/size]

Any JSON parser works. Valheim ships Unity's [font=Courier New]JsonUtility[/font] (and Newtonsoft.Json), so you need nothing extra. [font=Courier New]JsonUtility[/font] fills [font=Courier New][Serializable][/font] classes with public fields and ignores keys you do not declare, but it cannot read a top-level array, so wrap one first. The examples on this page use these helpers:

[code]using System;
using UnityEngine;

internal static class IffJson {
    [Serializable]
    private class Wrapper<T> { public T[] items; }

    internal static T[] ReadArray<T>(string json) {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>("{\"items\":" + json + "}");
        return wrapper?.items ?? new T[0];
    }
}

[Serializable]
internal class PlayerRef {
    public string player;   // a player id: long.Parse(player)
    public string name;
    public bool self;
}[/code]

Player ids are always strings in IFF's JSON, because a 64-bit id loses precision in a parser that reads numbers as doubles. Id lists you give IFF may use strings or numbers.

[size=4][b]Versioning[/b][/size]

[code]int          GetApiVersion()      // 3; the shim returns 0 when IFF is missing
string       GetPluginVersion()   // "0.0.1"; the shim returns "" when IFF is missing
bool         HasEndpoint(string)  // whether the installed IFF has an endpoint of this name
bool         IsReady()            // the local character's marks are loaded
bool         IsLoaded()           // shim only: IFF is installed and its API was found
string       GetPluginId()        // ItemFavoriteFramework.API only; the shim has the constant PluginGuid
List<string> GetEndpointNames()   // ItemFavoriteFramework.API only: every endpoint name, sorted[/code]

[list]
[*][b]API 1[/b] - favorites, trash flags, favorite slots, the changed listener, icons and colours[/*]
[*][b]API 2[/b] - custom marks, mark listeners, item click handlers[/*]
[*][b]API 3[/b] - wishlists and their providers, item name providers, shared wishlists, loot rolls[/*]
[/list]

[size=4][b]Favorites, trash and slots[/b][/size]

[size=3][b]How marks are keyed[/b][/size]

[list]
[*][b]Items that cannot stack[/b] ([font=Courier New]m_shared.m_maxStackSize <= 1[/font]) are marked individually. The mark is stored in the item's [font=Courier New]m_customData[/font] and travels with it through chests, drops and tombstones. It records which character set it, so another player holding the item sees it unmarked.[/*]
[*][b]Stackable items[/b] are marked by type ([font=Courier New]m_shared.m_name[/font]), the way Quick Stack Store Sort does it: favoriting one stack of resin favorites all resin. Vanilla merges stacks without comparing custom data, so a per-stack mark could not survive.[/*]
[*]Queries honour both sources whatever the current stack size. Favorites imported from Quick Stack Store Sort are type marks, even for weapons.[/*]
[*]Favorite and trash never apply together. Favorite wins, so a type's trash flag never takes away an item favorited on its own.[/*]
[*][b]Slot marks[/b] only exist in the player's own inventory grid. An item in a chest is never "in a favorited slot", whatever its [font=Courier New]m_gridPos[/font].[/*]
[/list]

[size=3][b]Queries[/b][/size]

[code]bool IsItemFavorited(ItemDrop.ItemData item)       // this item, or its type, is favorited
bool IsItemTrashFlagged(ItemDrop.ItemData item)    // never true for a favorited item
bool IsSlotFavorited(int x, int y)                 // a favorited cell of the player's grid
bool IsItemInFavoritedSlot(ItemDrop.ItemData item) // in the player's inventory, in a favorited cell
bool IsProtected(ItemDrop.ItemData item)           // favorited OR in a favorited slot
int  GetMarks(ItemDrop.ItemData item)              // MarkItemFavorite(1) | MarkSlotFavorite(2) | MarkTrash(4)

List<ItemDrop.ItemData> GetFavoritedItems(Inventory inventory)     // null = the player's inventory
List<ItemDrop.ItemData> GetTrashFlaggedItems(Inventory inventory)
List<ItemDrop.ItemData> GetProtectedItems(Inventory inventory)
List<Vector2i>          GetFavoritedSlots()                        // sorted by row, then column[/code]

[font=Courier New]IsProtected[/font] is the one check most mods need. Grid cells are [font=Courier New](x, y)[/font] with [font=Courier New]x[/font] the column and [font=Courier New]y[/font] the row; [font=Courier New](0, 0)[/font] is the first hotbar slot.

The list queries work on any inventory, a chest's included: per-item marks travel with the item and type marks apply wherever it is. Only slot marks are limited to the player's own inventory, so [font=Courier New]GetProtectedItems(chest.GetInventory())[/font] lists the chest's favorites.

[font=Courier New]GetMarks[/font] answers everything in one call:

[code]int marks = ItemFavorites.GetMarks(item);
bool favorite = (marks & ItemFavorites.MarkItemFavorite) != 0;
bool inFavoritedSlot = (marks & ItemFavorites.MarkSlotFavorite) != 0;
bool trash = (marks & ItemFavorites.MarkTrash) != 0;[/code]

[size=3][b]Changes[/b][/size]

[code]bool SetItemFavorited(ItemDrop.ItemData item, bool favorited)
bool SetItemTrashFlagged(ItemDrop.ItemData item, bool trashFlagged)
bool SetSlotFavorited(int x, int y, bool favorited)[/code]

These return [font=Courier New]true[/font] when the item or slot is in the requested state afterwards, including when it already was. They return [font=Courier New]false[/font] when the change is refused:

[list]
[*]no character is loaded;[/*]
[*]the item is null or has no shared data, or the cell has a negative coordinate;[/*]
[*]favorite and trash are exclusive, so clear one before setting the other;[/*]
[*]the character's saved marks could not be read. IFF then leaves them untouched for a newer version to recover, and refuses every change.[/*]
[/list]

The same granularity as the player's clicks applies: favoriting a stackable favorites its whole type. Un-favoriting an item clears both its own mark and its type mark.

A per-item mark set on an item in the player's inventory or the open container saves that inventory at once. On an item anywhere else it is saved whenever that inventory next saves.

[b]Favorite a reward you hand out, so a store-all never sweeps it into a chest.[/b]

[code]Player player = Player.m_localPlayer;
ItemDrop.ItemData reward = player.GetInventory().AddItem("SwordIron", 1, 2, 0, player.GetPlayerID(), player.GetPlayerName(), false);
if (reward != null && ItemFavorites.SetItemFavorited(reward, true) == false) {
    Logger.LogDebug("The reward could not be favorited (no character loaded, or it is flagged as trash).");
}[/code]

[size=3][b]Listeners[/b][/size]

[code]void AddChangedListener(Action listener)
void RemoveChangedListener(Action listener)[/code]

A listener fires after any mark changes and after a character's marks load. It carries no payload, so re-query what you display.

[code]private void Awake() => ItemFavorites.AddChangedListener(OnMarksChanged);

private void OnDestroy() => ItemFavorites.RemoveChangedListener(OnMarksChanged);

private void OnMarksChanged() {
    if (myPanel.IsVisible()) { myPanel.Refresh(); }
}[/code]

To know which item changed, use a [i]mark listener[/i] instead.

[size=3][b]UI helpers[/b][/size]

[code]Sprite GetFavoriteIcon()      // white star, dark outline: tint with GetFavoriteColor()
Sprite GetTrashIcon()         // white X, dark outline: tint with GetTrashColor()
Color  GetFavoriteColor()     // the player's configured border colours
Color  GetFavoritedSlotColor()
Color  GetTrashColor()[/code]

The icons are built on first use and shared, so do not destroy them. Read the colours each time you draw: the player can change them in game.

[b]Show a star on a favorited list entry.[/b]

[code]star.sprite = ItemFavorites.GetFavoriteIcon();
star.color = ItemFavorites.GetFavoriteColor();
star.gameObject.SetActive(ItemFavorites.IsItemFavorited(item));   // always set it: rows are usually pooled[/code]

Item tooltips already get a "★ Favorited" / "Flagged as trash" line from IFF, via a postfix on the static [font=Courier New]ItemDrop.ItemData.GetTooltip[/font]. Do not add your own.

[size=4][b]Custom marks (API 2)[/b][/size]

Your own item marks, drawn and listed like the built-in ones: a border in your colour (outranking favorite and trash), your icon in the cell's top-left corner, and your tooltip line.

[code]bool         RegisterMarkType(string id, string group, Color color, Sprite icon, string tooltipToken)
bool         IsMarkTypeRegistered(string id)
bool         HasCustomMark(ItemDrop.ItemData item, string id)
List<string> GetCustomMarks(ItemDrop.ItemData item)          // in registration order
bool         SetCustomMark(ItemDrop.ItemData item, string id, bool marked)[/code]

[list]
[*]Use a namespaced id without spaces, tabs or line breaks ([font=Courier New]"mymod.loot.need"[/font]). [font=Courier New]RegisterMarkType[/font] returns [font=Courier New]false[/font] for an empty id or one with whitespace.[/*]
[*]Registering an id again updates its colour, icon and tooltip; its group never changes. [font=Courier New]icon[/font] may be null (border only). [font=Courier New]tooltipToken[/font] is a localization token ([font=Courier New]"$mymod_need"[/font]) or plain text, shown in the mark's colour, or empty for no line.[/*]
[*]A custom mark is stored [b]on the item instance[/b], in [font=Courier New]m_customData["IFF.custom.<id>"][/font], stackables included. It travels with the item, reads the same for every player, and needs no loaded character. What it means is up to you: clear it when you are done with the item.[/*]
[*]On a stackable, the mark belongs to that one stack. When vanilla merges two stacks it keeps one stack's custom data and drops the other's, so the merged stack carries the mark only if the surviving stack did.[/*]
[*]Marks registered with the same non-empty [font=Courier New]group[/font] are exclusive: setting one clears the others.[/*]
[*]An item carrying several custom marks is drawn with the one registered first. [font=Courier New]GetCustomMarks[/font] lists them all.[/*]
[*][font=Courier New]SetCustomMark[/font] returns [font=Courier New]true[/font] when the item is in the requested state afterwards, and [font=Courier New]false[/font] for an id nobody registered or an invalid item. It saves the item's inventory the same way favorites do.[/*]
[/list]

[b]Mark items your shop will sell.[/b]

[code]// Once, in Awake or when your sprites are loaded.
ItemFavorites.RegisterMarkType("mymod.shop.sell", "", new Color(1f, 0.6f, 0.1f), sellIcon, "$mymod_marked_for_sale");

// When the player picks an item in your shop window.
ItemFavorites.SetCustomMark(item, "mymod.shop.sell", true);

// When the sale goes through or is cancelled. The mark would otherwise stay on the item.
ItemFavorites.SetCustomMark(item, "mymod.shop.sell", false);[/code]

[size=4][b]Mark listeners and item clicks (API 2)[/b][/size]

[code]void AddMarkListener(Action<ItemDrop.ItemData, string, bool> listener)   // item, mark id, now set
void RemoveMarkListener(Action<ItemDrop.ItemData, string, bool> listener)
void AddItemClickHandler(Func<Inventory, ItemDrop.ItemData, int, bool> handler)
void RemoveItemClickHandler(Func<Inventory, ItemDrop.ItemData, int, bool> handler)[/code]

A [b]mark listener[/b] hears every item mark change, after it happens: [font=Courier New]"favorite"[/font], [font=Courier New]"trash"[/font] ([font=Courier New]ItemFavorites.MarkIdFavorite[/font], [font=Courier New]MarkIdTrash[/font]) or a custom id.

[list]
[*]A type mark on a stackable is reported once, for the item it was set through, although every item of that type changed with it.[/*]
[*]Setting a grouped custom mark first reports the marks it cleared ([font=Courier New]false[/font]), then the new one.[/*]
[*]Slot marks belong to no item and are not reported, and nor is clearing everything at once with [font=Courier New]iff-clear[/font]. [font=Courier New]AddChangedListener[/font] still fires for all of them.[/*]
[/list]

An [b]item click handler[/b] is called on mouse down on any inventory cell, with the grid's inventory, the item in the cell (null when it is empty) and the button ([font=Courier New]ItemFavorites.ButtonLeft[/font] 0, [font=Courier New]ButtonRight[/font] 1).

[list]
[*]Return [font=Courier New]true[/font] to take the click: vanilla's pick-up, split or use and the favoriting clicks then ignore it.[/*]
[*]Handlers are asked in the order they were added, before IFF's own clicks, and the first to return [font=Courier New]true[/font] wins. One that throws counts as not taking the click.[/*]
[*]They are asked on every grid (the player's inventory, chests, other mods' windows), so only take clicks on your own inventories.[/*]
[*]They are not asked while the player is dragging an item: the click that drops it belongs to vanilla.[/*]
[/list]

[b]Let players choose Need / Greed / Pass on loot in your window.[/b]

[code]ItemFavorites.RegisterMarkType("mymod.loot.need", "mymod.loot", needColor, needIcon, "$mymod_need");
ItemFavorites.RegisterMarkType("mymod.loot.greed", "mymod.loot", greedColor, greedIcon, "$mymod_greed");
ItemFavorites.AddItemClickHandler((inventory, item, button) => {
    if (inventory != myLootInventory || item == null) { return false; }
    // One group, so marking Need clears Greed and the other way round.
    ItemFavorites.SetCustomMark(item, button == ItemFavorites.ButtonLeft ? "mymod.loot.need" : "mymod.loot.greed", true);
    return true;
});

// When the window closes, take the marks off again.
foreach (ItemDrop.ItemData item in myLootInventory.GetAllItems()) {
    ItemFavorites.SetCustomMark(item, "mymod.loot.need", false);
    ItemFavorites.SetCustomMark(item, "mymod.loot.greed", false);
}[/code]

[b]Queue what the player flags as trash.[/b]

[code]ItemFavorites.AddMarkListener((item, markId, isSet) => {
    if (markId != ItemFavorites.MarkIdTrash) { return; }
    if (isSet) { myAutoSeller.Queue(item); } else { myAutoSeller.Unqueue(item); }
});[/code]

A trash flag on a stackable covers its whole type, so check [font=Courier New]IsItemTrashFlagged[/font] again when you act.

[size=4][b]Wishlists (API 3)[/b][/size]

A player's wishlist: items they want, each an item prefab name plus optional conditions that other mods supply (Epic Loot adds rarity, unique item, set and effects). Items on it get a heart in every inventory grid, a tooltip line and a line on the ground, and the player is told when one drops nearby or is in a container they open. Players add items with Alt + Shift + left click, in the wishlist editor (Mod Config menu, or [font=Courier New]iff-wishlist[/font]), or through these endpoints.

[code]bool   IsItemWishlisted(ItemDrop.ItemData item)            // matches an entry of the local list
string GetWishlistJson()
bool   AddWishlistEntry(string prefabName, string criteriaJson)  // criteriaJson: "" or {"<providerId>": {...}}
bool   AddWishlistEntryFromItem(ItemDrop.ItemData item)    // with the conditions providers suggest for it
int    RemoveWishlistEntries(string prefabName)            // how many were removed
void   AddWishlistChangedListener(Action listener)         // after any change, and after a character loads
void   RemoveWishlistChangedListener(Action listener)
void   OpenWishlistEditor()
Sprite GetWishlistIcon()                                    // white heart: tint with GetWishlistColor()
Color  GetWishlistColor()[/code]

[list]
[*]An entry names an item by its prefab ([font=Courier New]"SwordIron"[/font], the name of [font=Courier New]item.m_dropPrefab[/font]) and holds up to four conditions. It matches an item when the prefab names are equal and every condition's provider says it matches.[/*]
[*][font=Courier New]AddWishlistEntry[/font] returns [font=Courier New]true[/font] when the entry is on the list afterwards, including when the same entry already was. It returns [font=Courier New]false[/font] when no character is loaded, the saved wishlist could not be read, the prefab name is empty or longer than 64 characters, [font=Courier New]criteriaJson[/font] is not a JSON object, there are more than four conditions or one is longer than 1024 characters, or the list already has 500 entries.[/*]
[*]In [font=Courier New]criteriaJson[/font], each key is a provider id and each value that provider's criteria, as an object (or as a string holding the JSON). A condition whose provider is not installed is kept; it simply never matches until that mod is installed.[/*]
[*]Entries added through the API never vote Need on their own. Only the player can turn that on, in the editor.[/*]
[*][font=Courier New]RemoveWishlistEntries[/font] removes every entry for that prefab, whatever its conditions.[/*]
[/list]

[font=Courier New]GetWishlistJson()[/font] returns:

[code][{"id":3,"prefab":"SwordIron","name":"Iron sword","autoNeed":false,
  "criteria":[{"provider":"epicloot","json":"{\"v\":1,\"rarity\":\"2\"}","desc":"Epic+"}]}][/code]

[font=Courier New]name[/font] is the localized item name and [font=Courier New]desc[/font] the summary the provider wrote for the condition. The wishlist is saved in the character file under its own key ([font=Courier New]IFF.wishlist[/font]), apart from the favorites, and follows the character between worlds.

[b]Add a wishlist button to your item window.[/b]

[code]wishButton.onClick.AddListener(() => {
    if (ItemFavorites.IsItemWishlisted(selected)) {
        ItemFavorites.RemoveWishlistEntries(selected.m_dropPrefab.name);
    } else {
        ItemFavorites.AddWishlistEntryFromItem(selected);   // an Epic sword asks for Epic or better
    }
});
ItemFavorites.AddWishlistChangedListener(() => heart.gameObject.SetActive(ItemFavorites.IsItemWishlisted(selected)));[/code]

[b]Ask for a Black Metal sword of Epic rarity or better, with Epic Loot installed.[/b]

[code]ItemFavorites.AddWishlistEntry("SwordBlackmetal", "{\"epicloot\":{\"v\":1,\"rarity\":\"2\"}}");[/code]

[size=3][b]Conditions: wishlist providers[/b][/size]

[code]bool RegisterWishlistProvider(string providerId,
    Func<string, string> getSchemaJson,                    // prefab name -> fields to offer, or ""
    Func<ItemDrop.ItemData, string, bool> matches,         // (item, criteria json) -> does it match
    Func<string, string> describe,                         // criteria json -> "Epic or better"
    Func<ItemDrop.ItemData, string> suggest)               // item -> criteria for this item, or "" (may be null)
bool UnregisterWishlistProvider(string providerId)[/code]

[list]
[*][font=Courier New]providerId[/font] must be non-empty and at most 64 characters, and [font=Courier New]matches[/font] must not be null. Registering an id again replaces it.[/*]
[*][font=Courier New]getSchemaJson[/font] is asked for each item the player edits an entry for. Return [font=Courier New]""[/font] to offer nothing for that item.[/*]
[*][font=Courier New]describe[/font] turns criteria into a short summary (up to 128 characters), stored with the entry and shown wherever it appears, even after your provider is gone.[/*]
[*][font=Courier New]suggest[/font] describes one specific item, for Alt + Shift + click and [font=Courier New]AddWishlistEntryFromItem[/font]. Return [font=Courier New]""[/font] when there is nothing to say about it.[/*]
[*]An entry matches an item when the prefab names are equal and every condition's provider returns true. A condition whose provider is not installed never matches, but its stored [font=Courier New]desc[/font] still shows.[/*]
[/list]

The schema describes the fields IFF draws in its editor:

[code]{"title":"Epic Loot","fields":[
  {"key":"rarity","label":"Minimum rarity","type":"enum","options":[["0","Magic"],["2","Epic"]]},
  {"key":"effects","label":"Must have effects","type":"multi","options":[["ModifyDamage","+X% Damage"]]}]}[/code]

Field types: [font=Courier New]enum[/font] (a cycle button, with "Any" first), [font=Courier New]pick[/font] (a searchable list), [font=Courier New]multi[/font] (a list of picks), [font=Courier New]bool[/font], [font=Courier New]int[/font] and [font=Courier New]text[/font] (the default). Options are [font=Courier New][value, label][/font] pairs, or plain strings when the value is its own label; the value is what is stored. Labels are shown as given, so localize them yourself.

The criteria [font=Courier New]matches[/font] receives are a flat object of the keys that were set, plus [font=Courier New]"v":1[/font]. Every value is a string: a [font=Courier New]bool[/font] field is [font=Courier New]"true"[/font] (and absent when off), an [font=Courier New]int[/font] field its digits, and [font=Courier New]multi[/font] a list of strings. Keys left unset are absent.

[b][font=Courier New]matches[/font] must depend only on its two arguments.[/b] IFF also runs it on other players' entries, on this machine, to show who wants an item: never read the local player, the time or anything random. A provider that throws five times on the local player's own entries is switched off until the game restarts. Throws on other players' entries are logged and not counted, and count as no match.

[b]A "minimum quality" condition for upgradeable items.[/b]

[code][Serializable]
private class QualityCriteria {
    public string min;   // criteria values arrive as strings
}

private void Awake() {
    ItemFavorites.RegisterWishlistProvider("mymod.quality",
        prefab => IsUpgradeable(prefab)
            ? "{\"title\":\"My Mod\",\"fields\":[{\"key\":\"min\",\"label\":\"Minimum quality\",\"type\":\"enum\"," +
              "\"options\":[[\"2\",\"2\"],[\"3\",\"3\"],[\"4\",\"4\"]]}]}"
            : "",
        (item, criteria) => item.m_quality >= MinQuality(criteria),
        criteria => "quality " + MinQuality(criteria) + "+",
        item => item.m_quality > 1 ? "{\"v\":1,\"min\":\"" + item.m_quality + "\"}" : "");
}

private static int MinQuality(string criteria) {
    if (string.IsNullOrEmpty(criteria)) { return 1; }
    QualityCriteria parsed = JsonUtility.FromJson<QualityCriteria>(criteria);
    return parsed != null && int.TryParse(parsed.min, out int min) ? min : 1;
}

private static bool IsUpgradeable(string prefab) {
    GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
    ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
    return drop != null && drop.m_itemData.m_shared.m_maxQuality > 1;
}[/code]

[size=3][b]Item names[/b][/size]

[code]void AddItemNameProvider(Func<ItemDrop.ItemData, string> provider)     // null or "" to pass
void RemoveItemNameProvider(Func<ItemDrop.ItemData, string> provider)[/code]

How items are named in roll windows and loot messages. Providers are asked in the order they were added and the first non-empty answer wins. Without one, the item's localized name is used, with its quality and stack size when above 1. Epic Loot supplies its magic item names.

[code]ItemFavorites.AddItemNameProvider(item =>
    item.m_customData.TryGetValue("mymod.title", out string title) ? title : null);[/code]

[size=4][b]Shared wishlists (API 3)[/b][/size]

When the server runs IFF with [font=Courier New]EnableWishlistSharing[/font] on (the default), every player's wishlist is relayed to everyone else on the server, unless that player turned [font=Courier New]ShareWishlist[/font] off. The server relays at most [font=Courier New]MaxWishlistEntries[/font] entries per player (100 by default). Items another player wants get a blue heart, a "Wanted by" line, and show who wants them in roll windows.

[code]string GetServerFeaturesJson()              // what the server offers
string GetConnectedPlayersJson()            // [{"player":"123","name":"Alice","iff":true,"sharesWishlist":true}]
string GetPlayerWishlistJson(long playerId) // that player's shared list, in GetWishlistJson's shape
string GetWantedByJson(ItemDrop.ItemData item)  // [{"player":"123","name":"Alice","self":false}][/code]

[font=Courier New]GetServerFeaturesJson()[/font] returns:

[code]{"iff":true,"protocol":1,"version":"0.0.1","rolls":true,"wishlists":true,
 "rollSeconds":30,"reserveSeconds":300,"maxMembers":10}[/code]

On the server it reads its own settings; on a client, what the server sent. [font=Courier New]"iff"[/font] stays false until the server has answered this client's hello, and a server without IFF never answers.

[list]
[*][font=Courier New]GetConnectedPlayersJson[/font] lists everyone the server lists, with whether they run IFF and share a wishlist.[/*]
[*][font=Courier New]GetPlayerWishlistJson[/font] returns [font=Courier New]"[]"[/font] for a player who shares nothing. The local player's own id returns the local list.[/*]
[*][font=Courier New]GetWantedByJson[/font] is what this machine knows: everyone whose shared list matches the item, plus the local player ([font=Courier New]"self":true[/font]) when it is on their own list.[/*]
[/list]

[b]Show who wants an item in your loot window.[/b] ([font=Courier New]IffJson[/font] and [font=Courier New]PlayerRef[/font] are from [i]Reading the JSON[/i].)

[code]PlayerRef[] wanted = IffJson.ReadArray<PlayerRef>(ItemFavorites.GetWantedByJson(item));
wantedBy.text = wanted.Length == 0 ? "" : "Wanted by " + string.Join(", ", wanted.Select(p => p.self ? "you" : p.name));[/code]

[size=4][b]Loot rolls (API 3)[/b][/size]

MMO-style need/greed/pass for one drop on the ground, run by the server. [b]IFF never decides what is rolled or who is eligible: your mod asks for each roll and names the players.[/b] Requires IFF on the server with [font=Courier New]EnableLootRolls[/font] on (off by default).

[code]const int RollPass = 1, RollGreed = 2, RollNeed = 3;

bool   IsRollingAvailable()
bool   StartRoll(ItemDrop drop, string playerIdsJson, string optionsJson, ref string rollId, ref string error)
bool   StartRollForItem(ItemDrop.ItemData item, Vector3 position, string playerIdsJson, string optionsJson,
                        ref string rollId, ref string error)
string GetRollStateJson(ItemDrop drop)     // {"rollId","phase":"free|rolling|reserved","winner","winnerName","secondsLeft","lockedForMe"}
bool   IsDropLocked(ItemDrop drop)         // rolling or reserved, for anyone
bool   CanPlayerPickUp(ItemDrop drop, long playerId)
bool   SubmitRollVote(string rollId, int choice)       // for the local player
void   AddRollListener(Action<string> listener)
void   RemoveRollListener(Action<string> listener)
string GetPlayerIdsNearJson(Vector3 point, float radius)
string GetDamagerPlayerIdsJson(Character character)[/code]

The shim's [font=Courier New]StartRoll[/font] and [font=Courier New]StartRollForItem[/font] take [font=Courier New]out[/font] parameters instead of [font=Courier New]ref[/font], and [font=Courier New]ItemFavorites.PlayerIdsToJson(IEnumerable<long>)[/font] builds the id list.

[list]
[*][font=Courier New]IsRollingAvailable()[/font] is true on the server when [font=Courier New]EnableLootRolls[/font] is on, and on a client once the server has said it runs rolls.[/*]
[*][font=Courier New]rollId[/font] names the roll in events and votes. Treat it as an opaque string.[/*]
[*][font=Courier New]GetRollStateJson[/font] returns [font=Courier New]{"phase":"free"}[/font] for a drop with no roll. [font=Courier New]lockedForMe[/font] is true while the drop is rolling, or reserved for someone else.[/*]
[*][font=Courier New]CanPlayerPickUp[/font] is true when the drop is free or reserved for that player.[/*]
[*][font=Courier New]SubmitRollVote[/font] returns [font=Courier New]false[/font] when the local player has no such open roll or already voted.[/*]
[/list]

[size=3][b]How a roll runs[/b][/size]

[list=1]
[*]Your mod spawns the drop and calls [font=Courier New]StartRoll[/font] [b]on the machine that spawned it, in the same frame[/b] (or calls [font=Courier New]StartRollForItem[/font], which spawns and starts in one step). That machine owns the drop; it may be a client or the server itself. IFF saves the item and stamps the drop, so no player can see it before seeing that it is being rolled for. Finish changing the item (enchanting, naming) before you call: later changes do not reach the roll window.[/*]
[*]The server checks the request and tells each eligible player. Players without IFF, players not connected, and anyone past the server's [font=Courier New]MaxRollMembers[/font] (in the order you listed them) are left out and listed in the result as [font=Courier New]notEligible[/font].[/*]
[*]Each player picks Need, Greed or Pass in the roll window, with the vote keys (F6, F7, F8), or with [font=Courier New]/need[/font], [font=Courier New]/greed[/font], [font=Courier New]/pass[/font]. Players who do not answer in time, or leave, pass. The roll ends early once everyone has voted. Wishlisted items preselect Need, and a wishlist entry can vote Need by itself if the player allows it.[/*]
[*]Everyone who chose Need or Greed rolls 1-100. Need beats Greed; the best choice's highest roll wins, and ties roll again among the tied players (after ten tied rounds one of them is picked at random).[/*]
[*]The drop is reserved for the winner, who picks it up automatically when it is loaded near them. A winner who reconnects keeps the reservation. After it, anyone may take the drop. If everyone passed, or the reservation is 0 seconds, it is free at once.[/*]
[/list]

On a client, [font=Courier New]true[/font] means the request went out; the server's answer arrives as an [font=Courier New]accepted[/font] or [font=Courier New]refused[/font] event. On the server, [font=Courier New]true[/font] means the roll was taken on, though the drop itself is still checked and a failure then is a [font=Courier New]refused[/font] event too. A refused drop stays on the ground, free for anyone, so hand it out another way in your [font=Courier New]refused[/font] handler if you need to.

[size=3][b]Options[/b][/size]

[code]{"label":"Eikthyr","source":"mymod","groupId":"raid-42","rollSeconds":30,"reserveSeconds":300}[/code]

All optional; pass [font=Courier New]""[/font] for none. The whole string may be at most 1024 characters.

[list]
[*][b][font=Courier New]label[/font][/b] - shown in the roll window and messages, such as the boss or chest it came from[/*]
[*][b][font=Courier New]source[/font][/b] - your mod's name, for logs[/*]
[*][b][font=Courier New]groupId[/font][/b] - drops with the same [font=Courier New]groupId[/font] are shown together, and the roll window offers Greed all and Pass all, so a boss paying out several items does not bury players in prompts[/*]
[*][b][font=Courier New]rollSeconds[/font][/b] - how long players have to vote: at least 5, and at most the server's [font=Courier New]RollSeconds[/font] (30 by default)[/*]
[*][b][font=Courier New]reserveSeconds[/font][/b] - how long the winner has to pick it up: from 0 up to the server's [font=Courier New]ReserveMinutes[/font] (5 minutes by default)[/*]
[/list]

[font=Courier New]label[/font], [font=Courier New]source[/font] and [font=Courier New]groupId[/font] lose any [font=Courier New]<[/font] and [font=Courier New]>[/font], line breaks become spaces, and they are cut to 64 characters.

[size=3][b]Errors[/b][/size]

[font=Courier New]StartRoll[/font] and [font=Courier New]StartRollForItem[/font] return [font=Courier New]false[/font] at once, with [font=Courier New]error[/font] set, for:

[list]
[*][b][font=Courier New]notInstalled[/font][/b] - the shim found no IFF[/*]
[*][b][font=Courier New]notInWorld[/font][/b] - no world is loaded[/*]
[*][b][font=Courier New]disabled[/font][/b] - the server has rolls off[/*]
[*][b][font=Courier New]serverHasNoIff[/font][/b] - a client has not heard from an IFF server (none, or not yet answered)[/*]
[*][b][font=Courier New]invalidDrop[/font][/b] - the drop is null or not networked[/*]
[*][b][font=Courier New]notOwner[/font][/b] - this machine does not own the drop[/*]
[*][b][font=Courier New]noMembers[/font][/b] - the player list is empty or holds no valid id[/*]
[*][b][font=Courier New]alreadyRolling[/font][/b] - the drop is already rolling or reserved[/*]
[*][b][font=Courier New]invalidItem[/font][/b] - [font=Courier New]StartRollForItem[/font] only: the item is null or has no [font=Courier New]m_dropPrefab[/font][/*]
[/list]

The server checks every request again and can refuse it. A client always hears a refusal as a [font=Courier New]refused[/font] event with a [font=Courier New]reason[/font]. When the server starts a roll itself, [font=Courier New]noEligibleMembers[/font], [font=Courier New]serverBusy[/font] and [font=Courier New]badOptions[/font] come back at once in [font=Courier New]error[/font], and the checks of the drop arrive as a [font=Courier New]refused[/font] event:

[list]
[*][b][font=Courier New]noEligibleMembers[/font][/b] - none of the players is connected with IFF[/*]
[*][b][font=Courier New]serverBusy[/font][/b] - the server has [font=Courier New]MaxActiveRolls[/font] rolls open[/*]
[*][b][font=Courier New]badOptions[/font][/b] - the options are not a JSON object, or too long[/*]
[*][b][font=Courier New]rateLimited[/font][/b] - the client started more than [font=Courier New]RollStartsPerMinutePerPlayer[/font] rolls[/*]
[*][b][font=Courier New]tooManyActive[/font][/b] - the client has 30 rolls open[/*]
[*][b][font=Courier New]notVerified[/font][/b] - the server has not verified the client yet[/*]
[*][b][font=Courier New]notYourDrop[/font][/b] - the client did not create the drop, and does not own one spawned in the last 30 seconds[/*]
[*][b][font=Courier New]tooFar[/font][/b] - the client is more than [font=Courier New]MaxRequesterDistance[/font] from the drop[/*]
[*][b][font=Courier New]notAnItem[/font][/b] - the object is not an item drop[/*]
[*][b][font=Courier New]itemTooLarge[/font][/b] - the item's saved data is over 32 KB[/*]
[*][b][font=Courier New]dropNotFound[/font], [font=Courier New]stampMismatch[/font][/b] - the stamped drop did not reach the server within five seconds[/*]
[*][b][font=Courier New]dropGone[/font][/b] - the drop was picked up or destroyed before the roll started[/*]
[/list]

When [font=Courier New]StartRollForItem[/font] returns false it removes the drop it spawned, so the item can safely be handed out another way instead.

[size=3][b]Events[/b][/size]

[font=Courier New]AddRollListener[/font] hears JSON events, each with [font=Courier New]"event"[/font], [font=Courier New]"side"[/font] and [font=Courier New]"rollId"[/font]. [font=Courier New]"server"[/font] events are raised on the server (dedicated or listen host) for every roll; [font=Courier New]"client"[/font] events on the clients the roll concerns. A listen host hears both, so check [font=Courier New]side[/font].

[list]
[*][b][font=Courier New]accepted[/font], [font=Courier New]refused[/font][/b] - Fields: [font=Courier New]reason[/font] (refused only). Raised on: the requesting machine and the server.[/*]
[*][b][font=Courier New]prompt[/font][/b] - Fields: [font=Courier New]prefab[/font], [font=Courier New]name[/font], [font=Courier New]label[/font], [font=Courier New]groupId[/font], [font=Courier New]seconds[/font], [font=Courier New]members[/font], [font=Courier New]wishlisted[/font]. Raised on: each member.[/*]
[*][b][font=Courier New]vote[/font][/b] - Fields: [font=Courier New]player[/font], [font=Courier New]voted[/font], [font=Courier New]of[/font] (choices stay hidden until the result). Raised on: members and the server.[/*]
[*][b][font=Courier New]result[/font][/b] - Fields: [font=Courier New]outcome[/font] ([font=Courier New]won[/font], [font=Courier New]allPassed[/font], [font=Courier New]cancelled[/font]), [font=Courier New]winner[/font], [font=Courier New]winnerName[/font], [font=Courier New]reserveSeconds[/font], [font=Courier New]prefab[/font], [font=Courier New]label[/font], [font=Courier New]groupId[/font], [font=Courier New]rolls[/font] ([font=Courier New]player[/font], [font=Courier New]name[/font], [font=Courier New]choice[/font], [font=Courier New]roll[/font], [font=Courier New]rerolls[/font]), [font=Courier New]notEligible[/font]. Raised on: members, the requester and the server.[/*]
[*][b][font=Courier New]lock[/font][/b] - Fields: [font=Courier New]phase[/font], [font=Courier New]winner[/font], [font=Courier New]secondsLeft[/font]. Raised on: everyone with IFF, and the server.[/*]
[/list]

[list]
[*][font=Courier New]prompt[/font]: [font=Courier New]name[/font] is the item's display name, [font=Courier New]seconds[/font] the time left to vote, [font=Courier New]members[/font] the players in the roll, and [font=Courier New]wishlisted[/font] whether the item is on the local player's wishlist.[/*]
[*][font=Courier New]vote[/font]: [font=Courier New]player[/font] just voted; [font=Courier New]voted[/font] of [font=Courier New]of[/font] members have voted so far.[/*]
[*][font=Courier New]result[/font]: each of [font=Courier New]rolls[/font] has [font=Courier New]choice[/font] [font=Courier New]need[/font], [font=Courier New]greed[/font] or [font=Courier New]pass[/font] ([font=Courier New]none[/font] for a member who had not voted when a roll was cancelled), [font=Courier New]roll[/font] 1-100 (0 for a pass) and how many tie [font=Courier New]rerolls[/font] it took. [font=Courier New]winner[/font] is [font=Courier New]"0"[/font] when nobody won, and [font=Courier New]reserveSeconds[/font] is 0 when the drop is free at once.[/*]
[*][font=Courier New]lock[/font]: the drop's [font=Courier New]phase[/font] changed to [font=Courier New]free[/font], [font=Courier New]rolling[/font] or [font=Courier New]reserved[/font].[/*]
[/list]

A result looks like this:

[code]{"event":"result","side":"server","rollId":"...","outcome":"won","winner":"123","winnerName":"Alice",
 "reserveSeconds":300,"prefab":"SwordIron","label":"Eikthyr","groupId":"raid-42",
 "rolls":[{"player":"123","name":"Alice","choice":"need","roll":87,"rerolls":0},
          {"player":"456","name":"Bob","choice":"greed","roll":93,"rerolls":0}],
 "notEligible":[]}[/code]

A roll whose drop disappears (picked up, despawned) ends as [font=Courier New]cancelled[/font].

[b]Log results and refusals on the server.[/b]

[code][Serializable]
private class RollTally {
    public string player, name, choice;
    public int roll, rerolls;
}

[Serializable]
private class RollEvent {
    public string @event, side, rollId, reason, outcome, winner, winnerName, prefab, name, label, groupId;
    public float seconds, reserveSeconds;
    public RollTally[] rolls;
}

ItemFavorites.AddRollListener(json => {
    RollEvent e = JsonUtility.FromJson<RollEvent>(json);
    if (e.side != "server") { return; }
    if (e.@event == "result" && e.outcome == "won") {
        Logger.LogInfo($"{e.winnerName} won {e.prefab} ({e.label}).");
    } else if (e.@event == "refused") {
        Logger.LogWarning($"Roll {e.rollId} refused: {e.reason}.");
    }
});[/code]

[b]Vote from your own window.[/b] IFF's roll window still shows unless the player turned [font=Courier New]ShowRollWindow[/font] off.

[code]ItemFavorites.AddRollListener(json => {
    RollEvent e = JsonUtility.FromJson<RollEvent>(json);
    if (e.side == "client" && e.@event == "prompt") { myVoteWindow.Show(e.rollId, e.name, e.seconds); }
});

// When the player clicks Need:
ItemFavorites.SubmitRollVote(rollId, ItemFavorites.RollNeed);[/code]

[size=3][b]Choosing members[/b][/size]

[code]// Everyone within 50 m of a point, nearest first.
string nearby = ItemFavorites.GetPlayerIdsNearJson(boss.transform.position, 50f);

// Everyone who hit a creature.
string fighters = ItemFavorites.GetDamagerPlayerIdsJson(boss);

// Any list of your own: party members, a raid roster, clan members.
string party = ItemFavorites.PlayerIdsToJson(myParty.Select(member => member.PlayerId));[/code]

[font=Courier New]GetPlayerIdsNearJson(point, radius)[/font] lists the players near a point: on the server from every player's position, on a client from the players loaded nearby. Nearest come first, so when the server caps a roll at [font=Courier New]MaxRollMembers[/font] it keeps the closest. [font=Courier New]GetDamagerPlayerIdsJson(character)[/font] lists the players who hit a creature, from the flags vanilla keeps on it; call it on the creature's owner before it is destroyed, for instance in a [font=Courier New]Character.OnDeath[/font] prefix.

[size=3][b]What rolls can and cannot stop[/b][/size]

Whether a player can pick an item up is decided by that player's own game in Valheim, so rolls keep honest players with IFF from grabbing loot that is not theirs: hand pickup, auto-pickup (no pull toward the player either), ownership requests and stack merging all respect the lock. Votes and results are server-side and tied to each player's own connection, so nobody can vote or share a wishlist as someone else.

Rolls cannot stop a modified client, or a player without IFF once the drop's ownership passes to them. An owner running IFF refuses to hand a locked drop to anyone but the winner ([font=Courier New]OwnerRefusesLockedOwnership[/font]), and when someone else takes it anyway the roll's players are told ([font=Courier New]AnnounceLockedPickups[/font]). Mods that move items without a player, such as vacuums and auto-storage, should leave a drop alone while [font=Courier New]IsDropLocked(drop)[/font] is true.

[size=4][b]Calling IFF without the shim[/b][/size]

[b]Hard dependency.[/b] Reference [font=Courier New]ItemFavoriteFramework.dll[/font], declare [font=Courier New][BepInDependency("MidnightsFX.ItemFavoriteFramework")][/font], and call the static class directly. The two roll starts take [font=Courier New]ref[/font] parameters:

[code]if (ItemFavoriteFramework.API.IsProtected(item)) { continue; }

string rollId = "", error = "";
if (ItemFavoriteFramework.API.StartRoll(drop, members, "", ref rollId, ref error) == false) {
    Logger.LogDebug($"No roll: {error}");
}[/code]

[b]Your own reflection.[/b] Bind each method once, after IFF has loaded, to a delegate of exactly its signature. A method that is missing stays unbound, so your mod keeps working against an older IFF:

[code]private delegate bool StartRollFn(ItemDrop drop, string playerIdsJson, string optionsJson, ref string rollId, ref string error);

private static Func<ItemDrop.ItemData, bool> isProtected = item => false;
private static StartRollFn startRoll;

internal static void Bind() {
    Type api = Type.GetType("ItemFavoriteFramework.API, ItemFavoriteFramework");
    if (api == null) { return; }
    isProtected = Get<Func<ItemDrop.ItemData, bool>>(api, "IsProtected") ?? isProtected;
    startRoll = Get<StartRollFn>(api, "StartRoll");
}

private static T Get<T>(Type api, string name) where T : class {
    MethodInfo invoke = typeof(T).GetMethod("Invoke");
    Type[] parameters = invoke.GetParameters().Select(p => p.ParameterType).ToArray();
    MethodInfo method = api.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
    if (method == null || method.ReturnType != invoke.ReturnType) { return null; }
    return Delegate.CreateDelegate(typeof(T), method, throwOnBindFailure: false) as T;
}[/code]

The shim's [url=https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/ItemFavoriteFrameworkAPI/src/ItemFavorites.cs]source[/url] binds every endpoint this way.

[size=4][b]Recipes[/b][/size]

[b]Store all / quick stack / deposit into chests.[/b] Skip [font=Courier New]IsProtected(item)[/font]. For equipped and hotbar items, keep whatever rule you already have; IFF does not replace it.

[code]foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems().ToList()) {
    if (ItemFavorites.IsProtected(item)) { continue; }
    // ... move it
}[/code]

[b]Sort the player's inventory.[/b] Leave [font=Courier New]GetFavoritedSlots()[/font] cells and the items in them where they are, and sort everything else around them.

[code]HashSet<Vector2i> pinned = new HashSet<Vector2i>(ItemFavorites.GetFavoritedSlots());
List<ItemDrop.ItemData> movable = inventory.GetAllItems().Where(item => pinned.Contains(item.m_gridPos) == false).ToList();
// ... sort movable into the cells that are not pinned[/code]

[b]Sacrifice, salvage, disenchant or any other destructive list.[/b] Leave [font=Courier New]IsProtected(item)[/font] items out of the list, and check again when the action runs: the player may favorite an item while your UI is open.

[code]List<ItemDrop.ItemData> offered = player.GetInventory().GetAllItems().Where(item => ItemFavorites.IsProtected(item) == false).ToList();

// Later, when the player confirms:
foreach (ItemDrop.ItemData item in picked) {
    if (ItemFavorites.IsProtected(item)) { continue; }
    Sacrifice(item);
}[/code]

[b]Show a star on a favorited list entry.[/b] See [i]UI helpers[/i].

[b]Act on trash.[/b] [font=Courier New]GetTrashFlaggedItems(null)[/font] lists what the player marked. IFF itself never destroys anything.

[code]foreach (ItemDrop.ItemData item in ItemFavorites.GetTrashFlaggedItems(null)) { Sell(item); }[/code]

[b]Roll for creature loot.[/b] Spawn the drops, finish the items, and start the rolls in the same frame, on the same machine:

[code]string members = ItemFavorites.GetPlayerIdsNearJson(dropPoint, 50f);
string options = "{\"label\":\"Eikthyr\",\"source\":\"mymod\",\"groupId\":\"" + killId + "\"}";
foreach (ItemDrop.ItemData loot in rewards) {
    ItemDrop drop = ItemDrop.DropItem(loot, loot.m_stack, dropPoint, Quaternion.identity);
    if (ItemFavorites.StartRoll(drop, members, options, out string rollId, out string error) == false) {
        Logger.LogDebug($"No roll: {error}");   // the drop stays on the ground, free for anyone
    }
}[/code]

[b]Roll for a reward the server hands out.[/b] On the server, [font=Courier New]StartRollForItem[/font] spawns the reward at a position and rolls it among the players you name, even where no player has the area loaded:

[code]GameObject prefab = ObjectDB.instance.GetItemPrefab("SwordBlackmetal");
ItemDrop.ItemData reward = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
reward.m_dropPrefab = prefab;   // StartRollForItem spawns from this
reward.m_quality = 3;

string members = ItemFavorites.PlayerIdsToJson(raid.PlayerIds);
string options = "{\"label\":\"Raid reward\",\"source\":\"mymod\",\"reserveSeconds\":120}";
if (ItemFavorites.StartRollForItem(reward, shrine.transform.position + Vector3.up, members, options,
        out string rollId, out string error) == false) {
    GiveToRaidLeader(reward);   // nothing was left on the ground
}[/code]

Listen for the [font=Courier New]result[/font] event ([font=Courier New]"side":"server"[/font]) to announce or log it.

[b]Vacuum or auto-store items.[/b] Skip a drop while it is under a roll or reserved:

[code]foreach (ItemDrop drop in nearbyDrops) {
    if (ItemFavorites.IsDropLocked(drop)) { continue; }
    Pull(drop);
}[/code]

A mod that picks things up for one player can ask [font=Courier New]CanPlayerPickUp(drop, player.GetPlayerID())[/font] instead, so a winner can still collect their reserved item.

[size=4][b]What IFF already does itself[/b][/size]

[list]
[*]The vanilla container [b]Place stacks[/b] button and hold-to-stack leave protected items in the player's inventory (config [font=Courier New]ProtectFavoritesFromStackAll[/font], default on).[/*]
[*]Auto-pickup can skip trash-flagged items (config [font=Courier New]PreventAutoPickupOfTrash[/font], default off).[/*]
[*]A crafting upgrade keeps the item's per-item mark.[/*]
[*]Every item tooltip gets the favorite, trash, wishlist, "wanted by" and custom mark lines.[/*]
[/list]

[size=4][b]Testing your integration[/b][/size]

These console commands operate on the local character:

[code]iff-help                     every IFF command
iff-status                   loaded state and mark counts
iff-list                     every favorited/trash type, favorited slot, and marked item carried
iff-clear <favorites|trash|slots|all>
iff-import-qss [force]       copy this character's Quick Stack Store Sort favorites
iff-wishlist                 open the wishlist editor
iff-wishlist-list, -add <prefab> [conditions json], -remove <prefab|number>, -clear, -show <player>
iff-net-status               whether the server runs IFF, its loot settings, and who it lists
iff-roll-test [prefab] [count] [radius] [quality]   spawn items and roll them among nearby players (admin on a server)
iff-roll-status              your open rolls and the locks this game knows (every roll, on a server)
iff-need, iff-greed, iff-pass [number|all]   vote; also /need, /greed, /pass from chat[/code]

[font=Courier New]iff-clear favorites[/font] and [font=Courier New]iff-clear trash[/font] cancel per-item marks everywhere, in chests and tombstones too, without visiting them.

Two hidden commands help test the rarer paths. [font=Courier New]iff-wishlist-testprovider[/font] registers a small "minimum quality" condition for the session, so the condition editor and matching can be tried without another mod. [font=Courier New]iff-roll-debug forcetie[/font] (admins) makes the next roll's first round a tie.

[line]

If you enjoy my mods donations are appreciated. My mods will never be paid and donations are not required.

[url=https://ko-fi.com/midnightsfx][img]https://raster.shields.io/badge/Ko--fi-Support%20me-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white[/img][/url]
