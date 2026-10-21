# Item Favorite Framework API

Item Favorite Framework (IFF) owns three kinds of player marks and exposes them to other mods:

| Mark | Set with (defaults) | Applies to |
|---|---|---|
| **Favorite item** | Alt + left click | a specific item (weapons, armor, tools — anything that cannot stack), or a whole item type (stackables) |
| **Trash flag** | Alt + middle click, or Left Alt + Left Ctrl + left click | same granularity as favorites |
| **Favorite slot** | Alt + right click | a cell of the player's inventory grid |

Use it so your mod never moves or destroys what the player wants kept: skip protected items when
storing, stacking, sorting, sacrificing or salvaging, and show the player that you did.

Mods can also draw marks of their own on items (API 2), read and fill each player's **wishlist** of
wanted items, and ask IFF to run MMO-style **need/greed/pass loot rolls** among players they name
(API 3).

IFF's plugin GUID is `MidnightsFX.ItemFavoriteFramework`. Favorites, custom marks and the wishlist
itself work client-only. Shared wishlists and loot rolls need IFF on the server as well; a server
without it simply never offers them.

## Contents

- [Getting started](#getting-started)
- [Rules every endpoint follows](#rules-every-endpoint-follows)
- [Versioning](#versioning)
- [Favorites, trash and slots](#favorites-trash-and-slots)
- [Custom marks (API 2)](#custom-marks-api-2)
- [Mark listeners and item clicks (API 2)](#mark-listeners-and-item-clicks-api-2)
- [Wishlists (API 3)](#wishlists-api-3)
- [Shared wishlists (API 3)](#shared-wishlists-api-3)
- [Loot rolls (API 3)](#loot-rolls-api-3)
- [Calling IFF without the shim](#calling-iff-without-the-shim)
- [Recipes](#recipes)
- [What IFF already does itself](#what-iff-already-does-itself)
- [Testing your integration](#testing-your-integration)

## Getting started

### Three ways in

| | What it is | When to use it |
|---|---|---|
| **`ItemFavoriteFrameworkAPI.ItemFavorites`** | A small shim ([`ItemFavoriteFrameworkAPI/`](../ItemFavoriteFrameworkAPI/)) that binds every endpoint to a typed delegate once. Bundle the DLL with ILRepack, or copy its single source file into your project. | Almost always. Safe to call when IFF is not installed. |
| **`ItemFavoriteFramework.API`** | The public static class inside `ItemFavoriteFramework.dll`. | You hard-depend on IFF and reference its DLL directly. |
| **Reflection** | `Type.GetType("ItemFavoriteFramework.API, ItemFavoriteFramework")` | You already have your own glue. Every signature can be bound with `Delegate.CreateDelegate`. |

The rest of this page uses the shim. [Calling IFF without the shim](#calling-iff-without-the-shim)
covers the other two.

### Set up the shim

1. Add the shim to your plugin, in one of two ways:
   - **Copy** [`ItemFavorites.cs`](../ItemFavoriteFrameworkAPI/src/ItemFavorites.cs) into your
     project. It is one file with no dependencies beyond the game.
   - **Bundle** `ItemFavoriteFrameworkAPI.dll` into your plugin with ILRepack. How to build it is in
     the [shim's README](../ItemFavoriteFrameworkAPI/README.md).
2. Declare a soft dependency, so IFF loads before your plugin.
3. Call it.

```csharp
using System.Linq;
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
}
```

The shim looks IFF up once, on its first call, and never again. Without the soft dependency your
plugin may load first, find nothing, and stay a no-op for the whole session.

### When IFF is missing or older

Every shim method is safe when IFF is not installed, and when the installed IFF is older and lacks
that endpoint. It then returns a default and does nothing:

| Kind of method | Returns |
|---|---|
| queries | `false`, `0`, an empty list, or `"[]"` |
| changes and registrations | `false` |
| `GetFavoriteIcon`, `GetTrashIcon`, `GetWishlistIcon` | `null` |
| colours | IFF's default colours |
| `GetServerFeaturesJson` | `{"iff":false}` |
| `GetRollStateJson` | `{"phase":"free"}` |
| `CanPlayerPickUp` | `true` |
| `StartRoll`, `StartRollForItem` | `false`, with `error` set to `notInstalled` |

`IsLoaded()` tells you whether IFF is there at all. To use something newer than the oldest IFF you
support, check for it:

```csharp
bool hasCustomMarks = ItemFavorites.HasEndpoint("RegisterMarkType");   // API 2 or later
bool hasWishlists = ItemFavorites.GetApiVersion() >= 3;
```

## Rules every endpoint follows

- **Main thread only.** Call from Unity callbacks or Harmony patches, never from a worker thread.
- **The local player.** Favorites, trash flags, slots and the wishlist answer for the local
  character. Before one is loaded (main menu, character select) queries return `false`, `0` or an
  empty list, changes are refused, and `IsReady()` is `false`. The changed listener fires once a
  character's marks load, so a UI can wait for it instead of polling.
- **Register once, early.** Listeners, click handlers, mark types and providers can be added in
  `Awake`. They last for the whole session, across logouts and world changes.
- **Delegates are kept by equality.** Adding the same delegate twice adds it once. To remove one,
  pass the same method group, or keep the lambda in a field:

  ```csharp
  private static readonly Action onMarksChanged = () => myPanel.Refresh();

  ItemFavorites.AddChangedListener(onMarksChanged);
  ItemFavorites.RemoveChangedListener(onMarksChanged);
  ```

- **Your bugs stay yours.** A listener, handler or provider that throws is logged with its declaring
  type and does not stop the others.

### The signature rule

Only primitives, `string`, vanilla/Unity types (`ItemDrop.ItemData`, `ItemDrop`, `Inventory`,
`Character`, `Vector2i`, `Vector3`, `Sprite`, `Color`), lists of those, and `System.Func`/`Action` of
those cross the boundary. No IFF type appears in any signature, so a consumer never needs IFF's
assembly to compile. Structured data travels as JSON strings, and an endpoint that hands back more
than one value uses `ref` parameters (not `out`), so reflection callers can read them. Existing
signatures never change; additions bump `ApiVersion`.

### Reading the JSON

Any JSON parser works. Valheim ships Unity's `JsonUtility` (and Newtonsoft.Json), so you need nothing
extra. `JsonUtility` fills `[Serializable]` classes with public fields and ignores keys you do not
declare, but it cannot read a top-level array, so wrap one first. The examples on this page use these
helpers:

```csharp
using System;
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
}
```

Player ids are always strings in IFF's JSON, because a 64-bit id loses precision in a parser that
reads numbers as doubles. Id lists you give IFF may use strings or numbers.

## Versioning

```csharp
int          GetApiVersion()      // 3; the shim returns 0 when IFF is missing
string       GetPluginVersion()   // "0.0.1"; the shim returns "" when IFF is missing
bool         HasEndpoint(string)  // whether the installed IFF has an endpoint of this name
bool         IsReady()            // the local character's marks are loaded
bool         IsLoaded()           // shim only: IFF is installed and its API was found
string       GetPluginId()        // ItemFavoriteFramework.API only; the shim has the constant PluginGuid
List<string> GetEndpointNames()   // ItemFavoriteFramework.API only: every endpoint name, sorted
```

| API | Added |
|---|---|
| 1 | favorites, trash flags, favorite slots, the changed listener, icons and colours |
| 2 | custom marks, mark listeners, item click handlers |
| 3 | wishlists and their providers, item name providers, shared wishlists, loot rolls |

## Favorites, trash and slots

### How marks are keyed

- **Items that cannot stack** (`m_shared.m_maxStackSize <= 1`) are marked individually. The mark is
  stored in the item's `m_customData` and travels with it through chests, drops and tombstones. It
  records which character set it, so another player holding the item sees it unmarked.
- **Stackable items** are marked by type (`m_shared.m_name`), the way Quick Stack Store Sort does
  it: favoriting one stack of resin favorites all resin. Vanilla merges stacks without comparing
  custom data, so a per-stack mark could not survive.
- Queries honour both sources whatever the current stack size. Favorites imported from Quick Stack
  Store Sort are type marks, even for weapons.
- Favorite and trash never apply together. Favorite wins, so a type's trash flag never takes away
  an item favorited on its own.
- **Slot marks** only exist in the player's own inventory grid. An item in a chest is never "in a
  favorited slot", whatever its `m_gridPos`.

### Queries

```csharp
bool IsItemFavorited(ItemDrop.ItemData item)       // this item, or its type, is favorited
bool IsItemTrashFlagged(ItemDrop.ItemData item)    // never true for a favorited item
bool IsSlotFavorited(int x, int y)                 // a favorited cell of the player's grid
bool IsItemInFavoritedSlot(ItemDrop.ItemData item) // in the player's inventory, in a favorited cell
bool IsProtected(ItemDrop.ItemData item)           // favorited OR in a favorited slot
int  GetMarks(ItemDrop.ItemData item)              // MarkItemFavorite(1) | MarkSlotFavorite(2) | MarkTrash(4)

List<ItemDrop.ItemData> GetFavoritedItems(Inventory inventory)     // null = the player's inventory
List<ItemDrop.ItemData> GetTrashFlaggedItems(Inventory inventory)
List<ItemDrop.ItemData> GetProtectedItems(Inventory inventory)
List<Vector2i>          GetFavoritedSlots()                        // sorted by row, then column
```

`IsProtected` is the one check most mods need. Grid cells are `(x, y)` with `x` the column and `y`
the row; `(0, 0)` is the first hotbar slot.

The list queries work on any inventory, a chest's included: per-item marks travel with the item and
type marks apply wherever it is. Only slot marks are limited to the player's own inventory, so
`GetProtectedItems(chest.GetInventory())` lists the chest's favorites.

`GetMarks` answers everything in one call:

```csharp
int marks = ItemFavorites.GetMarks(item);
bool favorite = (marks & ItemFavorites.MarkItemFavorite) != 0;
bool inFavoritedSlot = (marks & ItemFavorites.MarkSlotFavorite) != 0;
bool trash = (marks & ItemFavorites.MarkTrash) != 0;
```

### Changes

```csharp
bool SetItemFavorited(ItemDrop.ItemData item, bool favorited)
bool SetItemTrashFlagged(ItemDrop.ItemData item, bool trashFlagged)
bool SetSlotFavorited(int x, int y, bool favorited)
```

These return `true` when the item or slot is in the requested state afterwards, including when it
already was. They return `false` when the change is refused:

- no character is loaded;
- the item is null or has no shared data, or the cell has a negative coordinate;
- favorite and trash are exclusive, so clear one before setting the other;
- the character's saved marks could not be read. IFF then leaves them untouched for a newer version
  to recover, and refuses every change.

The same granularity as the player's clicks applies: favoriting a stackable favorites its whole
type. Un-favoriting an item clears both its own mark and its type mark.

A per-item mark set on an item in the player's inventory or the open container saves that inventory
at once. On an item anywhere else it is saved whenever that inventory next saves.

**Favorite a reward you hand out, so a store-all never sweeps it into a chest.**

```csharp
Player player = Player.m_localPlayer;
ItemDrop.ItemData reward = player.GetInventory().AddItem("SwordIron", 1, 2, 0, player.GetPlayerID(), player.GetPlayerName(), false);
if (reward != null && ItemFavorites.SetItemFavorited(reward, true) == false) {
    Logger.LogDebug("The reward could not be favorited (no character loaded, or it is flagged as trash).");
}
```

### Listeners

```csharp
void AddChangedListener(Action listener)
void RemoveChangedListener(Action listener)
```

A listener fires after any mark changes and after a character's marks load. It carries no payload,
so re-query what you display.

```csharp
private void Awake() => ItemFavorites.AddChangedListener(OnMarksChanged);

private void OnDestroy() => ItemFavorites.RemoveChangedListener(OnMarksChanged);

private void OnMarksChanged() {
    if (myPanel.IsVisible()) { myPanel.Refresh(); }
}
```

To know which item changed, use a [mark listener](#mark-listeners-and-item-clicks-api-2) instead.

### UI helpers

```csharp
Sprite GetFavoriteIcon()      // white star, dark outline: tint with GetFavoriteColor()
Sprite GetTrashIcon()         // white X, dark outline: tint with GetTrashColor()
Color  GetFavoriteColor()     // the player's configured border colours
Color  GetFavoritedSlotColor()
Color  GetTrashColor()
```

The icons are built on first use and shared, so do not destroy them. Read the colours each time you
draw: the player can change them in game.

**Show a star on a favorited list entry.**

```csharp
star.sprite = ItemFavorites.GetFavoriteIcon();
star.color = ItemFavorites.GetFavoriteColor();
star.gameObject.SetActive(ItemFavorites.IsItemFavorited(item));   // always set it: rows are usually pooled
```

Item tooltips already get a "★ Favorited" / "Flagged as trash" line from IFF, via a postfix on the
static `ItemDrop.ItemData.GetTooltip`. Do not add your own.

## Custom marks (API 2)

Your own item marks, drawn and listed like the built-in ones: a border in your colour (outranking
favorite and trash), your icon in the cell's top-left corner, and your tooltip line.

```csharp
bool         RegisterMarkType(string id, string group, Color color, Sprite icon, string tooltipToken)
bool         IsMarkTypeRegistered(string id)
bool         HasCustomMark(ItemDrop.ItemData item, string id)
List<string> GetCustomMarks(ItemDrop.ItemData item)          // in registration order
bool         SetCustomMark(ItemDrop.ItemData item, string id, bool marked)
```

- Use a namespaced id without spaces, tabs or line breaks (`"mymod.loot.need"`). `RegisterMarkType`
  returns `false` for an empty id or one with whitespace.
- Registering an id again updates its colour, icon and tooltip; its group never changes. `icon` may
  be null (border only). `tooltipToken` is a localization token (`"$mymod_need"`) or plain text,
  shown in the mark's colour, or empty for no line.
- A custom mark is stored **on the item instance**, in `m_customData["IFF.custom.<id>"]`, stackables
  included. It travels with the item, reads the same for every player, and needs no loaded
  character. What it means is up to you: clear it when you are done with the item.
- On a stackable, the mark belongs to that one stack. When vanilla merges two stacks it keeps one
  stack's custom data and drops the other's, so the merged stack carries the mark only if the
  surviving stack did.
- Marks registered with the same non-empty `group` are exclusive: setting one clears the others.
- An item carrying several custom marks is drawn with the one registered first. `GetCustomMarks`
  lists them all.
- `SetCustomMark` returns `true` when the item is in the requested state afterwards, and `false` for
  an id nobody registered or an invalid item. It saves the item's inventory the same way favorites do.

**Mark items your shop will sell.**

```csharp
// Once, in Awake or when your sprites are loaded.
ItemFavorites.RegisterMarkType("mymod.shop.sell", "", new Color(1f, 0.6f, 0.1f), sellIcon, "$mymod_marked_for_sale");

// When the player picks an item in your shop window.
ItemFavorites.SetCustomMark(item, "mymod.shop.sell", true);

// When the sale goes through or is cancelled. The mark would otherwise stay on the item.
ItemFavorites.SetCustomMark(item, "mymod.shop.sell", false);
```

## Mark listeners and item clicks (API 2)

```csharp
void AddMarkListener(Action<ItemDrop.ItemData, string, bool> listener)   // item, mark id, now set
void RemoveMarkListener(Action<ItemDrop.ItemData, string, bool> listener)
void AddItemClickHandler(Func<Inventory, ItemDrop.ItemData, int, bool> handler)
void RemoveItemClickHandler(Func<Inventory, ItemDrop.ItemData, int, bool> handler)
```

A **mark listener** hears every item mark change, after it happens: `"favorite"`, `"trash"`
(`ItemFavorites.MarkIdFavorite`, `MarkIdTrash`) or a custom id.

- A type mark on a stackable is reported once, for the item it was set through, although every item
  of that type changed with it.
- Setting a grouped custom mark first reports the marks it cleared (`false`), then the new one.
- Slot marks belong to no item and are not reported, and nor is clearing everything at once with
  `iff-clear`. `AddChangedListener` still fires for all of them.

An **item click handler** is called on mouse down on any inventory cell, with the grid's inventory,
the item in the cell (null when it is empty) and the button (`ItemFavorites.ButtonLeft` 0,
`ButtonRight` 1).

- Return `true` to take the click: vanilla's pick-up, split or use and the favoriting clicks then
  ignore it.
- Handlers are asked in the order they were added, before IFF's own clicks, and the first to return
  `true` wins. One that throws counts as not taking the click.
- They are asked on every grid (the player's inventory, chests, other mods' windows), so only take
  clicks on your own inventories.
- They are not asked while the player is dragging an item: the click that drops it belongs to vanilla.

**Let players choose Need / Greed / Pass on loot in your window.**

```csharp
ItemFavorites.RegisterMarkType("mymod.loot.need", "mymod.loot", needColor, needIcon, "$mymod_need");
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
}
```

**Queue what the player flags as trash.**

```csharp
ItemFavorites.AddMarkListener((item, markId, isSet) => {
    if (markId != ItemFavorites.MarkIdTrash) { return; }
    if (isSet) { myAutoSeller.Queue(item); } else { myAutoSeller.Unqueue(item); }
});
```

A trash flag on a stackable covers its whole type, so check `IsItemTrashFlagged` again when you act.

## Wishlists (API 3)

A player's wishlist: items they want, each an item prefab name plus optional conditions that other
mods supply (Epic Loot adds rarity, unique item, set and effects). Items on it get a heart in every
inventory grid, a tooltip line and a line on the ground, and the player is told when one drops nearby
or is in a container they open. Players add items with Alt + Shift + left click, in the wishlist
editor (Mod Config menu, or `iff-wishlist`), or through these endpoints.

```csharp
bool   IsItemWishlisted(ItemDrop.ItemData item)            // matches an entry of the local list
string GetWishlistJson()
bool   AddWishlistEntry(string prefabName, string criteriaJson)  // criteriaJson: "" or {"<providerId>": {...}}
bool   AddWishlistEntryFromItem(ItemDrop.ItemData item)    // with the conditions providers suggest for it
int    RemoveWishlistEntries(string prefabName)            // how many were removed
void   AddWishlistChangedListener(Action listener)         // after any change, and after a character loads
void   RemoveWishlistChangedListener(Action listener)
void   OpenWishlistEditor()
Sprite GetWishlistIcon()                                    // white heart: tint with GetWishlistColor()
Color  GetWishlistColor()
```

- An entry names an item by its prefab (`"SwordIron"`, the name of `item.m_dropPrefab`) and holds up
  to four conditions. It matches an item when the prefab names are equal and every condition's
  provider says it matches.
- `AddWishlistEntry` returns `true` when the entry is on the list afterwards, including when the same
  entry already was. It returns `false` when no character is loaded, the saved wishlist could not be
  read, the prefab name is empty or longer than 64 characters, `criteriaJson` is not a JSON object,
  there are more than four conditions or one is longer than 1024 characters, or the list already has
  500 entries.
- In `criteriaJson`, each key is a provider id and each value that provider's criteria, as an object
  (or as a string holding the JSON). A condition whose provider is not installed is kept; it simply
  never matches until that mod is installed.
- Entries added through the API never vote Need on their own. Only the player can turn that on, in
  the editor.
- `RemoveWishlistEntries` removes every entry for that prefab, whatever its conditions.

`GetWishlistJson()` returns:

```json
[{"id":3,"prefab":"SwordIron","name":"Iron sword","autoNeed":false,
  "criteria":[{"provider":"epicloot","json":"{\"v\":1,\"rarity\":\"2\"}","desc":"Epic+"}]}]
```

`name` is the localized item name and `desc` the summary the provider wrote for the condition. The
wishlist is saved in the character file under its own key (`IFF.wishlist`), apart from the favorites,
and follows the character between worlds.

**Add a wishlist button to your item window.**

```csharp
wishButton.onClick.AddListener(() => {
    if (ItemFavorites.IsItemWishlisted(selected)) {
        ItemFavorites.RemoveWishlistEntries(selected.m_dropPrefab.name);
    } else {
        ItemFavorites.AddWishlistEntryFromItem(selected);   // an Epic sword asks for Epic or better
    }
});
ItemFavorites.AddWishlistChangedListener(() => heart.gameObject.SetActive(ItemFavorites.IsItemWishlisted(selected)));
```

**Ask for a Black Metal sword of Epic rarity or better, with Epic Loot installed.**

```csharp
ItemFavorites.AddWishlistEntry("SwordBlackmetal", "{\"epicloot\":{\"v\":1,\"rarity\":\"2\"}}");
```

### Conditions: wishlist providers

```csharp
bool RegisterWishlistProvider(string providerId,
    Func<string, string> getSchemaJson,                    // prefab name -> fields to offer, or ""
    Func<ItemDrop.ItemData, string, bool> matches,         // (item, criteria json) -> does it match
    Func<string, string> describe,                         // criteria json -> "Epic or better"
    Func<ItemDrop.ItemData, string> suggest)               // item -> criteria for this item, or "" (may be null)
bool UnregisterWishlistProvider(string providerId)
```

- `providerId` must be non-empty and at most 64 characters, and `matches` must not be null.
  Registering an id again replaces it.
- `getSchemaJson` is asked for each item the player edits an entry for. Return `""` to offer nothing
  for that item.
- `describe` turns criteria into a short summary (up to 128 characters), stored with the entry and
  shown wherever it appears, even after your provider is gone.
- `suggest` describes one specific item, for Alt + Shift + click and `AddWishlistEntryFromItem`.
  Return `""` when there is nothing to say about it.
- An entry matches an item when the prefab names are equal and every condition's provider returns
  true. A condition whose provider is not installed never matches, but its stored `desc` still shows.

The schema describes the fields IFF draws in its editor:

```json
{"title":"Epic Loot","fields":[
  {"key":"rarity","label":"Minimum rarity","type":"enum","options":[["0","Magic"],["2","Epic"]]},
  {"key":"effects","label":"Must have effects","type":"multi","options":[["ModifyDamage","+X% Damage"]]}]}
```

Field types: `enum` (a cycle button, with "Any" first), `pick` (a searchable list), `multi` (a list of
picks), `bool`, `int` and `text` (the default). Options are `[value, label]` pairs, or plain strings
when the value is its own label; the value is what is stored. Labels are shown as given, so localize
them yourself.

The criteria `matches` receives are a flat object of the keys that were set, plus `"v":1`. Every value
is a string: a `bool` field is `"true"` (and absent when off), an `int` field its digits, and `multi`
a list of strings. Keys left unset are absent.

**`matches` must depend only on its two arguments.** IFF also runs it on other players' entries, on
this machine, to show who wants an item: never read the local player, the time or anything random.
A provider that throws five times on the local player's own entries is switched off until the game
restarts. Throws on other players' entries are logged and not counted, and count as no match.

**A "minimum quality" condition for upgradeable items.**

```csharp
[Serializable]
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
}
```

### Item names

```csharp
void AddItemNameProvider(Func<ItemDrop.ItemData, string> provider)     // null or "" to pass
void RemoveItemNameProvider(Func<ItemDrop.ItemData, string> provider)
```

How items are named in roll windows and loot messages. Providers are asked in the order they were
added and the first non-empty answer wins. Without one, the item's localized name is used, with its
quality and stack size when above 1. Epic Loot supplies its magic item names.

```csharp
ItemFavorites.AddItemNameProvider(item =>
    item.m_customData.TryGetValue("mymod.title", out string title) ? title : null);
```

## Shared wishlists (API 3)

When the server runs IFF with `EnableWishlistSharing` on (the default), every player's wishlist is
relayed to everyone else on the server, unless that player turned `ShareWishlist` off. The server
relays at most `MaxWishlistEntries` entries per player (100 by default). Items another player wants
get a blue heart, a "Wanted by" line, and show who wants them in roll windows.

```csharp
string GetServerFeaturesJson()              // what the server offers
string GetConnectedPlayersJson()            // [{"player":"123","name":"Alice","iff":true,"sharesWishlist":true}]
string GetPlayerWishlistJson(long playerId) // that player's shared list, in GetWishlistJson's shape
string GetWantedByJson(ItemDrop.ItemData item)  // [{"player":"123","name":"Alice","self":false}]
```

`GetServerFeaturesJson()` returns:

```json
{"iff":true,"protocol":1,"version":"0.0.1","rolls":true,"wishlists":true,
 "rollSeconds":30,"reserveSeconds":300,"maxMembers":10}
```

On the server it reads its own settings; on a client, what the server sent. `"iff"` stays false
until the server has answered this client's hello, and a server without IFF never answers.

- `GetConnectedPlayersJson` lists everyone the server lists, with whether they run IFF and share a
  wishlist.
- `GetPlayerWishlistJson` returns `"[]"` for a player who shares nothing. The local player's own id
  returns the local list.
- `GetWantedByJson` is what this machine knows: everyone whose shared list matches the item, plus the
  local player (`"self":true`) when it is on their own list.

**Show who wants an item in your loot window.** (`IffJson` and `PlayerRef` are from
[Reading the JSON](#reading-the-json).)

```csharp
PlayerRef[] wanted = IffJson.ReadArray<PlayerRef>(ItemFavorites.GetWantedByJson(item));
wantedBy.text = wanted.Length == 0 ? "" : "Wanted by " + string.Join(", ", wanted.Select(p => p.self ? "you" : p.name));
```

## Loot rolls (API 3)

MMO-style need/greed/pass for one drop on the ground, run by the server. **IFF never decides what is
rolled or who is eligible: your mod asks for each roll and names the players.** Requires IFF on the
server with `EnableLootRolls` on (off by default).

```csharp
const int RollPass = 1, RollGreed = 2, RollNeed = 3;

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
string GetDamagerPlayerIdsJson(Character character)
```

The shim's `StartRoll` and `StartRollForItem` take `out` parameters instead of `ref`, and
`ItemFavorites.PlayerIdsToJson(IEnumerable<long>)` builds the id list.

- `IsRollingAvailable()` is true on the server when `EnableLootRolls` is on, and on a client once the
  server has said it runs rolls.
- `rollId` names the roll in events and votes. Treat it as an opaque string.
- `GetRollStateJson` returns `{"phase":"free"}` for a drop with no roll. `lockedForMe` is true while
  the drop is rolling, or reserved for someone else.
- `CanPlayerPickUp` is true when the drop is free or reserved for that player.
- `SubmitRollVote` returns `false` when the local player has no such open roll or already voted.

### How a roll runs

1. Your mod spawns the drop and calls `StartRoll` **on the machine that spawned it, in the same
   frame** (or calls `StartRollForItem`, which spawns and starts in one step). That machine owns the
   drop; it may be a client or the server itself. IFF saves the item and stamps the drop, so no
   player can see it before seeing that it is being rolled for. Finish changing the item (enchanting,
   naming) before you call: later changes do not reach the roll window.
2. The server checks the request and tells each eligible player. Players without IFF, players not
   connected, and anyone past the server's `MaxRollMembers` (in the order you listed them) are left out
   and listed in the result as `notEligible`.
3. Each player picks Need, Greed or Pass in the roll window, with the vote keys (F6, F7, F8), or with
   `/need`, `/greed`, `/pass`. Players who do not answer in time, or leave, pass. The roll ends early
   once everyone has voted. Wishlisted items preselect Need, and a wishlist entry can vote Need by
   itself if the player allows it.
4. Everyone who chose Need or Greed rolls 1-100. Need beats Greed; the best choice's highest roll
   wins, and ties roll again among the tied players (after ten tied rounds one of them is picked at
   random).
5. The drop is reserved for the winner, who picks it up automatically when it is loaded near them.
   A winner who reconnects keeps the reservation. After it, anyone may take the drop. If everyone
   passed, or the reservation is 0 seconds, it is free at once.

On a client, `true` means the request went out; the server's answer arrives as an `accepted` or
`refused` event. On the server, `true` means the roll was taken on, though the drop itself is still
checked and a failure then is a `refused` event too. A refused drop stays on the ground, free for
anyone, so hand it out another way in your `refused` handler if you need to.

### Options

```json
{"label":"Eikthyr","source":"mymod","groupId":"raid-42","rollSeconds":30,"reserveSeconds":300}
```

All optional; pass `""` for none. The whole string may be at most 1024 characters.

| Key | Meaning |
|---|---|
| `label` | shown in the roll window and messages, such as the boss or chest it came from |
| `source` | your mod's name, for logs |
| `groupId` | drops with the same `groupId` are shown together, and the roll window offers Greed all and Pass all, so a boss paying out several items does not bury players in prompts |
| `rollSeconds` | how long players have to vote: at least 5, and at most the server's `RollSeconds` (30 by default) |
| `reserveSeconds` | how long the winner has to pick it up: from 0 up to the server's `ReserveMinutes` (5 minutes by default) |

`label`, `source` and `groupId` lose any `<` and `>`, line breaks become spaces, and they are cut to
64 characters.

### Errors

`StartRoll` and `StartRollForItem` return `false` at once, with `error` set, for:

| `error` | Meaning |
|---|---|
| `notInstalled` | the shim found no IFF |
| `notInWorld` | no world is loaded |
| `disabled` | the server has rolls off |
| `serverHasNoIff` | a client has not heard from an IFF server (none, or not yet answered) |
| `invalidDrop` | the drop is null or not networked |
| `notOwner` | this machine does not own the drop |
| `noMembers` | the player list is empty or holds no valid id |
| `alreadyRolling` | the drop is already rolling or reserved |
| `invalidItem` | `StartRollForItem` only: the item is null or has no `m_dropPrefab` |

The server checks every request again and can refuse it. A client always hears a refusal as a
`refused` event with a `reason`. When the server starts a roll itself, `noEligibleMembers`,
`serverBusy` and `badOptions` come back at once in `error`, and the checks of the drop arrive as a
`refused` event:

| `reason` | Meaning |
|---|---|
| `noEligibleMembers` | none of the players is connected with IFF |
| `serverBusy` | the server has `MaxActiveRolls` rolls open |
| `badOptions` | the options are not a JSON object, or too long |
| `rateLimited` | the client started more than `RollStartsPerMinutePerPlayer` rolls |
| `tooManyActive` | the client has 30 rolls open |
| `notVerified` | the server has not verified the client yet |
| `notYourDrop` | the client did not create the drop, and does not own one spawned in the last 30 seconds |
| `tooFar` | the client is more than `MaxRequesterDistance` from the drop |
| `notAnItem` | the object is not an item drop |
| `itemTooLarge` | the item's saved data is over 32 KB |
| `dropNotFound`, `stampMismatch` | the stamped drop did not reach the server within five seconds |
| `dropGone` | the drop was picked up or destroyed before the roll started |

When `StartRollForItem` returns false it removes the drop it spawned, so the item can safely be
handed out another way instead.

### Events

`AddRollListener` hears JSON events, each with `"event"`, `"side"` and `"rollId"`. `"server"` events
are raised on the server (dedicated or listen host) for every roll; `"client"` events on the clients
the roll concerns. A listen host hears both, so check `side`.

| event | fields | raised on |
|---|---|---|
| `accepted`, `refused` | `reason` (refused only) | the requesting machine and the server |
| `prompt` | `prefab`, `name`, `label`, `groupId`, `seconds`, `members`, `wishlisted` | each member |
| `vote` | `player`, `voted`, `of` (choices stay hidden until the result) | members and the server |
| `result` | `outcome` (`won`, `allPassed`, `cancelled`), `winner`, `winnerName`, `reserveSeconds`, `prefab`, `label`, `groupId`, `rolls` (`player`, `name`, `choice`, `roll`, `rerolls`), `notEligible` | members, the requester and the server |
| `lock` | `phase`, `winner`, `secondsLeft` | everyone with IFF, and the server |

- `prompt`: `name` is the item's display name, `seconds` the time left to vote, `members` the
  players in the roll, and `wishlisted` whether the item is on the local player's wishlist.
- `vote`: `player` just voted; `voted` of `of` members have voted so far.
- `result`: each of `rolls` has `choice` `need`, `greed` or `pass` (`none` for a member who had not
  voted when a roll was cancelled), `roll` 1-100 (0 for a pass) and how many tie `rerolls` it took.
  `winner` is `"0"` when nobody won, and `reserveSeconds` is 0 when the drop is free at once.
- `lock`: the drop's `phase` changed to `free`, `rolling` or `reserved`.

A result looks like this:

```json
{"event":"result","side":"server","rollId":"...","outcome":"won","winner":"123","winnerName":"Alice",
 "reserveSeconds":300,"prefab":"SwordIron","label":"Eikthyr","groupId":"raid-42",
 "rolls":[{"player":"123","name":"Alice","choice":"need","roll":87,"rerolls":0},
          {"player":"456","name":"Bob","choice":"greed","roll":93,"rerolls":0}],
 "notEligible":[]}
```

A roll whose drop disappears (picked up, despawned) ends as `cancelled`.

**Log results and refusals on the server.**

```csharp
[Serializable]
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
});
```

**Vote from your own window.** IFF's roll window still shows unless the player turned
`ShowRollWindow` off.

```csharp
ItemFavorites.AddRollListener(json => {
    RollEvent e = JsonUtility.FromJson<RollEvent>(json);
    if (e.side == "client" && e.@event == "prompt") { myVoteWindow.Show(e.rollId, e.name, e.seconds); }
});

// When the player clicks Need:
ItemFavorites.SubmitRollVote(rollId, ItemFavorites.RollNeed);
```

### Choosing members

```csharp
// Everyone within 50 m of a point, nearest first.
string nearby = ItemFavorites.GetPlayerIdsNearJson(boss.transform.position, 50f);

// Everyone who hit a creature.
string fighters = ItemFavorites.GetDamagerPlayerIdsJson(boss);

// Any list of your own: party members, a raid roster, clan members.
string party = ItemFavorites.PlayerIdsToJson(myParty.Select(member => member.PlayerId));
```

`GetPlayerIdsNearJson(point, radius)` lists the players near a point: on the server from every
player's position, on a client from the players loaded nearby. Nearest come first, so when the server
caps a roll at `MaxRollMembers` it keeps the closest. `GetDamagerPlayerIdsJson(character)` lists the
players who hit a creature, from the flags vanilla keeps on it; call it on the creature's owner before
it is destroyed, for instance in a `Character.OnDeath` prefix.

### What rolls can and cannot stop

Whether a player can pick an item up is decided by that player's own game in Valheim, so rolls keep
honest players with IFF from grabbing loot that is not theirs: hand pickup, auto-pickup (no pull toward
the player either), ownership requests and stack merging all respect the lock. Votes and results are
server-side and tied to each player's own connection, so nobody can vote or share a wishlist as someone
else.

Rolls cannot stop a modified client, or a player without IFF once the drop's ownership passes to them.
An owner running IFF refuses to hand a locked drop to anyone but the winner (`OwnerRefusesLockedOwnership`),
and when someone else takes it anyway the roll's players are told (`AnnounceLockedPickups`). Mods that
move items without a player, such as vacuums and auto-storage, should leave a drop alone while
`IsDropLocked(drop)` is true.

## Calling IFF without the shim

**Hard dependency.** Reference `ItemFavoriteFramework.dll`, declare
`[BepInDependency("MidnightsFX.ItemFavoriteFramework")]`, and call the static class directly. The two
roll starts take `ref` parameters:

```csharp
if (ItemFavoriteFramework.API.IsProtected(item)) { continue; }

string rollId = "", error = "";
if (ItemFavoriteFramework.API.StartRoll(drop, members, "", ref rollId, ref error) == false) {
    Logger.LogDebug($"No roll: {error}");
}
```

**Your own reflection.** Bind each method once, after IFF has loaded, to a delegate of exactly its
signature. A method that is missing stays unbound, so your mod keeps working against an older IFF:

```csharp
private delegate bool StartRollFn(ItemDrop drop, string playerIdsJson, string optionsJson, ref string rollId, ref string error);

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
}
```

The shim's [source](../ItemFavoriteFrameworkAPI/src/ItemFavorites.cs) binds every endpoint this way.

## Recipes

**Store all / quick stack / deposit into chests.** Skip `IsProtected(item)`. For equipped and hotbar
items, keep whatever rule you already have; IFF does not replace it.

```csharp
foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems().ToList()) {
    if (ItemFavorites.IsProtected(item)) { continue; }
    // ... move it
}
```

**Sort the player's inventory.** Leave `GetFavoritedSlots()` cells and the items in them where they
are, and sort everything else around them.

```csharp
HashSet<Vector2i> pinned = new HashSet<Vector2i>(ItemFavorites.GetFavoritedSlots());
List<ItemDrop.ItemData> movable = inventory.GetAllItems().Where(item => pinned.Contains(item.m_gridPos) == false).ToList();
// ... sort movable into the cells that are not pinned
```

**Sacrifice, salvage, disenchant or any other destructive list.** Leave `IsProtected(item)` items
out of the list, and check again when the action runs: the player may favorite an item while your
UI is open.

```csharp
List<ItemDrop.ItemData> offered = player.GetInventory().GetAllItems().Where(item => ItemFavorites.IsProtected(item) == false).ToList();

// Later, when the player confirms:
foreach (ItemDrop.ItemData item in picked) {
    if (ItemFavorites.IsProtected(item)) { continue; }
    Sacrifice(item);
}
```

**Show a star on a favorited list entry.** See [UI helpers](#ui-helpers).

**Act on trash.** `GetTrashFlaggedItems(null)` lists what the player marked. IFF itself never
destroys anything.

```csharp
foreach (ItemDrop.ItemData item in ItemFavorites.GetTrashFlaggedItems(null)) { Sell(item); }
```

**Roll for creature loot.** Spawn the drops, finish the items, and start the rolls in the same frame,
on the same machine:

```csharp
string members = ItemFavorites.GetPlayerIdsNearJson(dropPoint, 50f);
string options = "{\"label\":\"Eikthyr\",\"source\":\"mymod\",\"groupId\":\"" + killId + "\"}";
foreach (ItemDrop.ItemData loot in rewards) {
    ItemDrop drop = ItemDrop.DropItem(loot, loot.m_stack, dropPoint, Quaternion.identity);
    if (ItemFavorites.StartRoll(drop, members, options, out string rollId, out string error) == false) {
        Logger.LogDebug($"No roll: {error}");   // the drop stays on the ground, free for anyone
    }
}
```

**Roll for a reward the server hands out.** On the server, `StartRollForItem` spawns the reward at a
position and rolls it among the players you name, even where no player has the area loaded:

```csharp
GameObject prefab = ObjectDB.instance.GetItemPrefab("SwordBlackmetal");
ItemDrop.ItemData reward = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
reward.m_dropPrefab = prefab;   // StartRollForItem spawns from this
reward.m_quality = 3;

string members = ItemFavorites.PlayerIdsToJson(raid.PlayerIds);
string options = "{\"label\":\"Raid reward\",\"source\":\"mymod\",\"reserveSeconds\":120}";
if (ItemFavorites.StartRollForItem(reward, shrine.transform.position + Vector3.up, members, options,
        out string rollId, out string error) == false) {
    GiveToRaidLeader(reward);   // nothing was left on the ground
}
```

Listen for the `result` event (`"side":"server"`) to announce or log it.

**Vacuum or auto-store items.** Skip a drop while it is under a roll or reserved:

```csharp
foreach (ItemDrop drop in nearbyDrops) {
    if (ItemFavorites.IsDropLocked(drop)) { continue; }
    Pull(drop);
}
```

A mod that picks things up for one player can ask `CanPlayerPickUp(drop, player.GetPlayerID())`
instead, so a winner can still collect their reserved item.

## What IFF already does itself

- The vanilla container **Place stacks** button and hold-to-stack leave protected items in the
  player's inventory (config `ProtectFavoritesFromStackAll`, default on).
- Auto-pickup can skip trash-flagged items (config `PreventAutoPickupOfTrash`, default off).
- A crafting upgrade keeps the item's per-item mark.
- Every item tooltip gets the favorite, trash, wishlist, "wanted by" and custom mark lines.

## Testing your integration

These console commands operate on the local character:

```
iff-help                     every IFF command
iff-status                   loaded state and mark counts
iff-list                     every favorited/trash type, favorited slot, and marked item carried
iff-clear <favorites|trash|slots|all>
iff-import-qss [force]       copy this character's Quick Stack Store Sort favorites
iff-wishlist                 open the wishlist editor
iff-wishlist-list, -add <prefab> [conditions json], -remove <prefab|number>, -clear, -show <player>
iff-net-status               whether the server runs IFF, its loot settings, and who it lists
iff-roll-test [prefab] [count] [radius] [quality]   spawn items and roll them among nearby players (admin on a server)
iff-roll-status              your open rolls and the locks this game knows (every roll, on a server)
iff-need, iff-greed, iff-pass [number|all]   vote; also /need, /greed, /pass from chat
```

`iff-clear favorites` and `iff-clear trash` cancel per-item marks everywhere, in chests and tombstones
too, without visiting them.

Two hidden commands help test the rarer paths. `iff-wishlist-testprovider` registers a small
"minimum quality" condition for the session, so the condition editor and matching can be tried
without another mod. `iff-roll-debug forcetie` (admins) makes the next roll's first round a tie.
