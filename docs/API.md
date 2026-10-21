# Item Favorite Framework integration API

Item Favorite Framework (IFF) owns three kinds of player marks and exposes them to other mods:

| Mark | Set with (defaults) | Applies to |
|---|---|---|
| **Favorite item** | Alt + left click | a specific item (weapons, armor, tools — anything that cannot stack), or a whole item type (stackables) |
| **Trash flag** | Alt + middle click, or Left Alt + Left Ctrl + left click | same granularity as favorites |
| **Favorite slot** | Alt + right click | a cell of the player's inventory grid |

Use it so your mod never moves or destroys what the player wants kept: skip protected items when
storing, stacking, sorting, sacrificing or salvaging, and show the player that you did.

It also keeps each player's **wishlist** of wanted items, and runs MMO-style **need/greed/pass loot
rolls** that other mods ask for (API 3).

IFF's plugin GUID is `MidnightsFX.ItemFavoriteFramework`. Favorites, custom marks and the wishlist
itself work client-only. Shared wishlists and loot rolls need IFF on the server as well; a server
without it simply never offers them.

## Three ways in

| | What it is | When to use it |
|---|---|---|
| **`ItemFavoriteFrameworkAPI.ItemFavorites`** | A small shim ([`ItemFavoriteFrameworkAPI/`](../ItemFavoriteFrameworkAPI/)) that binds every endpoint to a typed delegate once. Bundle the DLL with ILRepack, or copy its single source file into your project. | Almost always. Safe to call when IFF is not installed. |
| **`ItemFavoriteFramework.API`** | The public static class inside `ItemFavoriteFramework.dll`. | You hard-depend on IFF and reference its DLL directly. |
| **Reflection** | `Type.GetType("ItemFavoriteFramework.API, ItemFavoriteFramework")` | You already have your own glue. Every signature can be bound with `Delegate.CreateDelegate`. |

Declare the dependency so IFF loads first:

```csharp
[BepInDependency("MidnightsFX.ItemFavoriteFramework", BepInDependency.DependencyFlags.SoftDependency)]
```

## The signature rule

Only primitives, `string`, vanilla/Unity types (`ItemDrop.ItemData`, `ItemDrop`, `Inventory`,
`Character`, `Vector2i`, `Vector3`, `Sprite`, `Color`), lists of those, and `System.Func`/`Action` of
those cross the boundary. No IFF type appears in any signature, so a consumer never needs IFF's
assembly to compile. Structured data travels as JSON strings, and an endpoint that hands back more
than one value uses `ref` parameters (not `out`), so reflection callers can read them. Existing
signatures never change; additions bump `ApiVersion`.

## Versioning

```csharp
int    GetApiVersion()      // 3 (2 added custom marks, mark listeners and click handlers; 3 wishlists and loot rolls)
string GetPluginVersion()   // "0.0.1"
string GetPluginId()        // "MidnightsFX.ItemFavoriteFramework" (facade only)
bool   HasEndpoint(string)  // feature probe
bool   IsReady()            // the local character's marks are loaded
```

## Queries

All answer for the **local player** and must be called on the main thread. Before a character is
loaded they return `false`, `0` or an empty list.

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

`IsProtected` is the one check most mods need.

### How marks are keyed

- **Items that cannot stack** (`m_shared.m_maxStackSize <= 1`) are marked individually. The mark is
  stored in the item's `m_customData` and travels with it through chests, drops and tombstones. It
  records which character set it, so another player holding the item sees it unmarked.
- **Stackable items** are marked by type (`m_shared.m_name`), the way Quick Stack Store Sort does
  it: favoriting one stack of resin favorites all resin. Vanilla merges stacks without comparing
  custom data, so a per-stack mark could not survive.
- Queries honour both sources whatever the current stack size. Favorites imported from Quick Stack
  Store Sort are type marks, even for weapons.
- **Slot marks** only exist in the player's own inventory grid. An item in a chest is never "in a
  favorited slot", whatever its `m_gridPos`.

## Changes

```csharp
bool SetItemFavorited(ItemDrop.ItemData item, bool favorited)
bool SetItemTrashFlagged(ItemDrop.ItemData item, bool trashFlagged)
bool SetSlotFavorited(int x, int y, bool favorited)
```

These return `true` when the item or slot is in the requested state afterwards, including when it
already was. They return `false` when the change is refused:

- no character is loaded;
- favorite and trash are exclusive, so clear one before setting the other;
- the character's saved marks could not be read.

Un-favoriting an item clears both its own mark and its type mark.

## Listeners

```csharp
void AddChangedListener(Action listener)
void RemoveChangedListener(Action listener)
```

A listener fires after any mark changes and after a character's marks load. It carries no payload,
so re-query what you display. A listener that throws is logged with its declaring type and does not
stop the others.

## UI helpers

```csharp
Sprite GetFavoriteIcon()      // white star, dark outline: tint with GetFavoriteColor()
Sprite GetTrashIcon()         // white X, dark outline: tint with GetTrashColor()
Color  GetFavoriteColor()     // the player's configured border colours
Color  GetFavoritedSlotColor()
Color  GetTrashColor()
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

- Use a namespaced id without spaces (`"mymod.loot.need"`). Registering it again updates its colour,
  icon and tooltip; its group never changes. `icon` may be null (border only), `tooltipToken` empty.
- A custom mark is stored **on the item instance**, in `m_customData["IFF.custom.<id>"]`, stackables
  included. It travels with the item and reads the same for every player, and it needs no loaded
  character. What it means is up to you: clear it when you are done with the item.
- Marks registered with the same non-empty `group` are exclusive: setting one clears the others.
- A mark set on an item in the player's inventory or the open container saves that inventory; on an
  item anywhere else it is saved whenever that inventory next saves.

## Mark listeners and item clicks (API 2)

```csharp
void AddMarkListener(Action<ItemDrop.ItemData, string, bool> listener)   // item, mark id, now set
void RemoveMarkListener(Action<ItemDrop.ItemData, string, bool> listener)
void AddItemClickHandler(Func<Inventory, ItemDrop.ItemData, int, bool> handler)
void RemoveItemClickHandler(Func<Inventory, ItemDrop.ItemData, int, bool> handler)
```

- A mark listener hears every item mark change: `"favorite"`, `"trash"` (`MarkIdFavorite`,
  `MarkIdTrash`) or a custom id. Slot marks belong to no item and are not reported.
  `AddChangedListener` still fires as well.
- A click handler is called on mouse down on any inventory cell with the grid's inventory, the item in
  the cell (null when it is empty) and the button (0 left, 1 right). Return `true` to take the click:
  vanilla's pick-up, split or use and the favoriting clicks then ignore it. Handlers are asked before
  IFF's own clicks, on every grid, so only take clicks on your own inventories.

**Let players choose Need / Greed / Pass on loot in your window.**

```csharp
ItemFavorites.RegisterMarkType("mymod.loot.need", "mymod.loot", needColor, needIcon, "$mymod_need");
ItemFavorites.RegisterMarkType("mymod.loot.greed", "mymod.loot", greedColor, greedIcon, "$mymod_greed");
ItemFavorites.AddItemClickHandler((inventory, item, button) => {
    if (inventory != myLootInventory || item == null) { return false; }
    ItemFavorites.SetCustomMark(item, button == ItemFavorites.ButtonLeft ? "mymod.loot.need" : "mymod.loot.greed", true);
    return true;
});
```

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

`GetWishlistJson()` returns:

```json
[{"id":3,"prefab":"SwordIron","name":"Iron sword","autoNeed":false,
  "criteria":[{"provider":"epicloot","json":"{\"v\":1,\"rarity\":\"2\"}","desc":"Epic+"}]}]
```

The wishlist is saved in the character file under its own key (`IFF.wishlist`), apart from the
favorites, and follows the character between worlds.

### Conditions: wishlist providers

```csharp
bool RegisterWishlistProvider(string providerId,
    Func<string, string> getSchemaJson,                    // prefab name -> fields to offer, or ""
    Func<ItemDrop.ItemData, string, bool> matches,         // (item, criteria json) -> does it match
    Func<string, string> describe,                         // criteria json -> "Epic or better"
    Func<ItemDrop.ItemData, string> suggest)               // item -> criteria for this item, or "" (may be null)
bool UnregisterWishlistProvider(string providerId)
```

An entry matches an item when the prefab names are equal and every condition's provider returns true.
A condition whose provider is not installed never matches, but its stored `desc` still shows.

The schema describes the fields IFF draws in its editor:

```json
{"title":"Epic Loot","fields":[
  {"key":"rarity","label":"Minimum rarity","type":"enum","options":[["0","Magic"],["2","Epic"]]},
  {"key":"effects","label":"Must have effects","type":"multi","options":[["ModifyDamage","+X% Damage"]]}]}
```

Field types: `enum` (a cycle button, with "Any" first), `pick` (a searchable list), `multi` (a list of
picks), `bool`, `int` and `text`. Options are `[value, label]` pairs; the value is what is stored. The
criteria `matches` receives are a flat object of the keys that were set (a string, or a list of strings
for `multi`) plus `"v":1`.

**`matches` must depend only on its two arguments.** IFF also runs it on other players' entries, on
this machine, to show who wants an item: never read the local player, the time or anything random. A
provider that throws five times is switched off for the session.

### Item names

```csharp
void AddItemNameProvider(Func<ItemDrop.ItemData, string> provider)     // null or "" to pass
void RemoveItemNameProvider(Func<ItemDrop.ItemData, string> provider)
```

How items are named in roll windows and loot messages. Epic Loot supplies its magic item names.

## Shared wishlists (API 3)

When the server runs IFF with `EnableWishlistSharing` on (the default), every player's wishlist is
relayed to everyone else on the server, unless that player turned `ShareWishlist` off. Items another
player wants get a blue heart, a "Wanted by" line, and show who wants them in roll windows.

```csharp
string GetServerFeaturesJson()              // {"iff":true,"rolls":true,"wishlists":true,"rollSeconds":30,...}
string GetConnectedPlayersJson()            // [{"player":"123","name":"Alice","iff":true,"sharesWishlist":true}]
string GetPlayerWishlistJson(long playerId) // that player's shared list, in GetWishlistJson's shape
string GetWantedByJson(ItemDrop.ItemData item)  // [{"player":"123","name":"Alice","self":false}]
```

`"iff"` stays false until the server has answered this client's hello; a server without IFF never
answers. Player ids are always strings in JSON, because a 64-bit id loses precision in a parser that
reads numbers as doubles. Id lists given to IFF may use strings or numbers.

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

### How a roll runs

1. Your mod spawns the drop and calls `StartRoll` **on the machine that spawned it, in the same
   frame** (or calls `StartRollForItem`, which spawns, saves and starts in one step). That machine owns
   the drop; it may be a client or the server itself. IFF saves the item and stamps the drop, so no
   player can see it before seeing that it is being rolled for.
2. The server checks the request and tells each eligible player. Players without IFF, players not
   connected, and anyone past the server's `MaxRollMembers` (in the order you listed them) are left out
   and listed in the result as `notEligible`.
3. Each player picks Need, Greed or Pass in the roll window, with the vote keys (F6, F7, F8), or with
   `/need`, `/greed`, `/pass`. Players who do not answer in time, or leave, pass. Wishlisted items
   preselect Need, and a wishlist entry can vote Need by itself if the player allows it.
4. Everyone who chose Need or Greed rolls 1-100. Need beats Greed; the best choice's highest roll
   wins, and ties roll again among the tied players.
5. The drop is reserved for the winner, who picks it up automatically when it is loaded near them.
   After the reservation anyone may take it. If everyone passed it is free at once.

### Options

```json
{"label":"Eikthyr","source":"mymod","groupId":"raid-42","rollSeconds":30,"reserveSeconds":300}
```

All optional. `rollSeconds` and `reserveSeconds` are capped by the server's `RollSeconds` and
`ReserveMinutes`. Drops started with the same `groupId` are shown together, and the roll window
offers Greed all and Pass all, so a boss paying out several items does not bury players in prompts.

### Errors

`StartRoll` returns false with `error` set to one of: `notInWorld`, `disabled` (the server has rolls
off), `serverHasNoIff`, `invalidDrop`, `notOwner`, `noMembers`, `alreadyRolling`, `invalidItem`, and on
the server also `noEligibleMembers`, `serverBusy`, `badOptions`. When
`StartRollForItem` returns false it removes the drop it spawned, so the item can safely be handed out
another way instead. A client's request
can still be refused by the server afterwards (`rateLimited`, `tooFar`, `notYourDrop`,
`noEligibleMembers`, ...): listen for the `refused` event.

### Events

`AddRollListener` hears JSON events, each with `"event"` and `"side"`. `"server"` events are raised on
the server (dedicated or listen host) for every roll; `"client"` events on the clients the roll concerns.

| event | fields | raised on |
|---|---|---|
| `accepted`, `refused` | `rollId`, `reason` | the requesting machine and the server |
| `prompt` | `rollId`, `prefab`, `name`, `label`, `groupId`, `seconds`, `members`, `wishlisted` | each member |
| `vote` | `rollId`, `player`, `voted`, `of` (choices stay hidden until the result) | members and the server |
| `result` | `rollId`, `outcome` (`won`, `allPassed`, `cancelled`), `winner`, `winnerName`, `reserveSeconds`, `prefab`, `label`, `groupId`, `rolls` (`player`, `name`, `choice`, `roll`, `rerolls`), `notEligible` | members, the requester and the server |
| `lock` | `rollId`, `phase`, `winner`, `secondsLeft` | everyone with IFF, and the server |

A roll whose drop disappears (picked up, despawned) ends as `cancelled`.

### Choosing members

`GetPlayerIdsNearJson(point, radius)` lists the players near a point: on the server from every
player's position, on a client from the players loaded nearby. `GetDamagerPlayerIdsJson(character)`
lists the players who hit a creature, from the flags vanilla keeps on it; call it on the creature's
owner before it is destroyed, for instance in a `Character.OnDeath` prefix. Any other list works too:
party members, raid rosters, clan members.

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

**Sacrifice, salvage, disenchant or any other destructive list.** Leave `IsProtected(item)` items
out of the list, and check again when the action runs: the player may favorite an item while your
UI is open.

**Show a star on a favorited list entry.**

```csharp
star.sprite = ItemFavorites.GetFavoriteIcon();
star.color = ItemFavorites.GetFavoriteColor();
star.gameObject.SetActive(ItemFavorites.IsItemFavorited(item));   // always set it: rows are usually pooled
```

**Act on trash.** `GetTrashFlaggedItems(null)` lists what the player marked. IFF itself never
destroys anything.

**Roll for creature loot.** Start the rolls right after spawning the drops, on the same machine:

```csharp
string members = ItemFavorites.GetPlayerIdsNearJson(dropPoint, 50f);
string options = "{\"label\":\"Eikthyr\",\"source\":\"mymod\",\"groupId\":\"" + killId + "\"}";
foreach (GameObject go in spawned) {
    if (ItemFavorites.StartRoll(go.GetComponent<ItemDrop>(), members, options, out string rollId, out string error) == false) {
        Logger.LogDebug($"No roll: {error}");
    }
}
```

**Roll for a reward the server hands out.** On the server, `StartRollForItem(item, position, members,
options, ...)` spawns the reward at a position and rolls it among the players you name, even where no
player has the area loaded. Listen for the `result` event (`"side":"server"`) to announce or log it.

**Vacuum or auto-store items.** Skip a drop while `IsDropLocked(drop)` is true.

## What IFF already does itself

- The vanilla container **Place stacks** button and hold-to-stack leave protected items in the
  player's inventory (config `ProtectFavoritesFromStackAll`, default on).
- Auto-pickup can skip trash-flagged items (config `PreventAutoPickupOfTrash`, default off).
- A crafting upgrade keeps the item's per-item mark.

## Testing your integration

These console commands operate on the local character:

```
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

`iff-wishlist-testprovider` registers a small "minimum quality" condition for the session, so the
condition editor and matching can be tried without another mod.
