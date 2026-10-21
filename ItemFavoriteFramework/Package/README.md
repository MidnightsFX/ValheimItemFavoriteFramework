# Item Favorite Framework

Mark the items you want to keep, and the ones you don't, once. Every mod that supports the framework
then respects those marks. Keep a wishlist of the gear you are after, and share loot fairly with
need/greed/pass rolls.

## Features

- **Favorite items.** Storing, stacking and sorting leave them in your inventory, and destructive
  features never offer them.
- **Favorite inventory slots.** Whatever you keep in that cell stays put.
- **Flag items as trash.** Mods that clean up after you know what can go. The framework itself never
  destroys anything.
- **Vanilla "Place stacks" works with it out of the box.** The container button and holding Use on a
  chest leave favorites, and anything in a favorited slot, in your inventory.
- **Optionally, trash on the ground is not auto-picked up.** You can still pick it up by hand.
- **Quick Stack Store Sort favorites are copied over** the first time each character loads.
- **Wishlist.** Mark the items you want. They get a heart wherever they appear, and you're told when one
  drops near you or sits in a chest you open. With Epic Loot you can ask for a rarity, a unique item, a
  set or particular effects.
- **Shared wishlists.** On a server running the mod, everyone sees who wants what.
- **Need / greed / pass loot rolls.** When a mod hands a drop to the framework (Epic Loot can, for its
  magic items), the players it names roll for it. The winner gets it; nobody else can grab it first.
- **Favorites and the wishlist are client-only**, saved with your character. Shared wishlists and loot
  rolls need the mod on the server too; without it they simply stay off.

### Controls

While the inventory is open, hold **Alt**:

| State | Hotkey | Visual |
|---|---|---|
|Toggle Favorite | Alt + left click an item | ![favorite](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_favorite.png?raw=true) |
|Toggle Slot Favorite | Alt + right click a slot | ![favorite slot](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_slot.png?raw=true) ![favorite item in a favorite slot](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_slot_favorite.png?raw=true) |
|Toggle Trash | Alt + middle click an item | ![trash](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_trash.png?raw=true) |
|Toggle Trash (Alternative) | Left Alt + Left Ctrl + left click an item | ![trash](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/border_trash.png?raw=true) |
|Add to/remove from Wishlist | Left Alt + Left Shift + left click an item |  |

Items can be marked inside an open chest too. Slots can only be favorited in your own inventory.

When a roll comes up, vote in the roll window (click it while the inventory, map or chat is open), with
**F6** (Need), **F7** (Greed) and **F8** (Pass), or type `/need`, `/greed` or `/pass` in chat. The
wishlist editor is in the **Mod Config** menu on the main and pause menus.

Marked cells get a woven border: gold for a favorite item, blue for a favorite slot, and dark red for
trash, whose icon is also greyed out. A favorite slot holding a marked item weaves both colours, the
slot's blue outside and the item's colour inside. Tooltips say why.

**Weapons, armor and tools are marked individually.** Favoriting your best sword leaves every other
sword alone, and the mark stays on that sword wherever it goes: into chests, onto the ground, into
your tombstone, through upgrades. **Stackable items are marked by type**, so favoriting one stack of
resin favorites all your resin.

Favorite and trash are exclusive: unflag one before setting the other.

### Configuration

In `BepInEx/config/MidnightsFX.ItemFavoriteFramework.cfg`. Sections 1 to 6 are per-client:

- **1 - Favoriting:** the favoriting keys, the trash key, middle-click trash, marking inside
  containers, and tooltip lines.
- **2 - Colors:** border colours, and how strongly trash icons are greyed out.
- **3 - Protection:** Place stacks protection (on by default) and skipping trash on auto-pickup (off
  by default).
- **4 - Compatibility:** the Quick Stack Store Sort import.
- **5 - Wishlist:** the wishlist key, heart colours, tooltip lines, drop and chest notices, and whether
  others may see your wishlist.
- **6 - Loot rolls:** the vote keys, the roll window, preselecting Need for wishlisted items, and picking
  up what you won automatically.

Section **7 - Server** is set by the server and synced to everyone: whether loot rolls are allowed (off
by default), whether wishlists are shared, how long rolls and reservations last, and limits.

Console commands: `iff-status`, `iff-list`, `iff-clear <favorites|trash|slots|all>`,
`iff-import-qss [force]`, `iff-wishlist` (and `-list`, `-add`, `-remove`, `-clear`, `-show`),
`iff-net-status`, `iff-roll-status`, `iff-roll-test` (admins), `iff-need`, `iff-greed`, `iff-pass`,
`iff-help`.

### Compatibility

- **Epic Loot:** the enchanting table leaves favorites out of the screens that use items up, such as
  Sacrifice, and shows a star on them. Wishlist entries can ask for a rarity, unique item, set or
  effects, and the server can have Epic Loot's magic drops rolled for among the players nearby.
- **Quick Stack Store Sort** also favorites on Alt + click, and both mods act on the same click.
  After the first import the two sets of favorites are separate, so unbind one mod's favoriting keys.
- **AzuAutoStore** stores items on middle click, so middle-click trash is off while it is installed.
  Use Left Alt + Left Ctrl + left click instead.

## For mod authors: the API

The framework holds the marks and your mod asks about them. Use it so your mod never moves or
destroys what the player wants kept: skip protected items when storing, stacking, sorting,
sacrificing or salvaging.

### Setup

1. Add the shim, `ItemFavoriteFrameworkAPI.ItemFavorites`, in one of two ways:
   - **Copy** [`ItemFavorites.cs`](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/ItemFavoriteFrameworkAPI/src/ItemFavorites.cs)
     into your project. It is one file with no dependencies beyond the game.
   - **Bundle** `ItemFavoriteFrameworkAPI.dll` into your plugin with ILRepack.
2. Declare a soft dependency, so the framework loads before your plugin:

   ```csharp
   [BepInDependency("MidnightsFX.ItemFavoriteFramework", BepInDependency.DependencyFlags.SoftDependency)]
   ```

3. Call it:

   ```csharp
   using ItemFavoriteFrameworkAPI;

   foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems().ToList()) {
       if (ItemFavorites.IsProtected(item)) { continue; }
       // ... store, stack or sacrifice it
   }
   ```

Every shim method is safe when the framework is not installed. Queries return `false`, `0` or an
empty list, and changes return `false`. `ItemFavorites.IsLoaded()` tells you which case you are in.

The shim binds each endpoint to a typed delegate on its first call, so later calls cost no
reflection. It binds only once, so without the soft dependency your plugin may load first, find
nothing, and stay a no-op for the whole session.

Other ways in, if the shim doesn't suit you:

- **Hard dependency:** reference `ItemFavoriteFramework.dll` and call the static class
  `ItemFavoriteFramework.API` directly.
- **Your own reflection:** `Type.GetType("ItemFavoriteFramework.API, ItemFavoriteFramework")`. Every
  method can be bound with `Delegate.CreateDelegate`.

### How marks work

- Everything answers for the **local player** and must be called on the **main thread**. Before a
  character is loaded, queries return `false`, `0` or an empty list, and `IsReady()` is `false`.
- **Items that cannot stack** (`m_shared.m_maxStackSize <= 1`) are marked individually. The mark
  lives in the item's `m_customData`, travels with it, and belongs to the character who set it, so
  another player holding the item sees it unmarked.
- **Stackable items** are marked by type (`m_shared.m_name`). Queries check both kinds of mark,
  whatever the item's stack size.
- **Slot marks** only exist in the player's own inventory grid. An item in a chest is never "in a
  favorited slot", whatever its `m_gridPos`.

### Endpoints

**Queries.** `IsProtected` is the one check most mods need.

| Call | Answers |
|---|---|
| `bool IsProtected(item)` | favorited, or in a favorited slot |
| `bool IsItemFavorited(item)` | this item, or its type, is favorited |
| `bool IsItemTrashFlagged(item)` | flagged as trash (never true for a favorite) |
| `bool IsItemInFavoritedSlot(item)` | in the player's inventory, in a favorited cell |
| `bool IsSlotFavorited(int x, int y)` | a favorited cell of the player's grid |
| `int GetMarks(item)` | bit flags: `MarkItemFavorite` (1), `MarkSlotFavorite` (2), `MarkTrash` (4) |
| `List<ItemDrop.ItemData> GetProtectedItems(inventory)` | also `GetFavoritedItems` and `GetTrashFlaggedItems`; `null` means the player's inventory |
| `List<Vector2i> GetFavoritedSlots()` | sorted by row, then column |

**Changes.**

```csharp
bool SetItemFavorited(ItemDrop.ItemData item, bool favorited)
bool SetItemTrashFlagged(ItemDrop.ItemData item, bool trashFlagged)
bool SetSlotFavorited(int x, int y, bool favorited)
```

These return `true` when the item or slot ends up in the requested state, including when it already
was. They return `false` when no character is loaded, when the item is favorited and you ask to
trash it (or the other way round), or when the character's saved marks could not be read.
Unfavoriting an item clears both its own mark and its type mark.

**Listeners.** `AddChangedListener(Action)` and `RemoveChangedListener(Action)`. A listener runs after
any mark changes and after a character's marks load. It carries no payload, so re-query whatever you
display. A listener that throws is logged and does not stop the others.

**UI.** `GetFavoriteIcon()` and `GetTrashIcon()` return a white `Sprite` with a dark outline; tint it
with `GetFavoriteColor()`, `GetFavoritedSlotColor()` or `GetTrashColor()`, which return the player's
configured border colours.

**Custom marks (API 2).** `RegisterMarkType`, `SetCustomMark`, `HasCustomMark`, `GetCustomMarks`,
mark listeners and item click handlers: your own marks, drawn like the built-in ones.

**Wishlists (API 3).** `IsItemWishlisted`, `GetWishlistJson`, `AddWishlistEntry`,
`RemoveWishlistEntries`, and `RegisterWishlistProvider` to add your own conditions to wishlist entries.
On a server running the framework, `GetWantedByJson(item)` tells you who wants an item.

**Loot rolls (API 3).** `StartRoll(drop, playerIdsJson, optionsJson, out rollId, out error)` hands a drop
you just spawned to a server-run need/greed/pass roll among the players you name, and
`AddRollListener` reports the votes and the winner. `IsDropLocked(drop)` tells vacuum and auto-store
mods to leave a drop alone.

**Versioning.** `GetApiVersion()` (currently 3), `GetPluginVersion()`, `HasEndpoint(string name)` and
`IsReady()`. The shim adds `IsLoaded()`.

Only primitives, `string`, vanilla and Unity types, and `Func`/`Action` of those appear in a
signature (structured data travels as JSON), so you never need the framework's assembly to compile. Existing signatures never change;
new endpoints bump `GetApiVersion()`, and you can probe for them with `HasEndpoint`.

### Recipes

**Store all, quick stack, deposit.** Skip `IsProtected(item)`. Keep whatever rule you already have for
equipped and hotbar items; the framework doesn't replace it.

**Sort the player's inventory.** Leave the `GetFavoritedSlots()` cells, and the items in them, where
they are, and sort everything else around them.

**Sacrifice, salvage, disenchant, or any list that destroys items.** Leave `IsProtected(item)` items
out of the list, and check again when the action runs: the player may favorite an item while your UI
is open.

**Show a star on a favorited row.**

```csharp
star.sprite = ItemFavorites.GetFavoriteIcon();
star.color = ItemFavorites.GetFavoriteColor();
star.gameObject.SetActive(ItemFavorites.IsItemFavorited(item));   // always set it: rows are usually pooled
```

**Act on trash.** `GetTrashFlaggedItems(null)` lists what the player flagged. What happens to it is
up to your mod.

The framework already handles vanilla Place stacks, keeps per-item marks through crafting upgrades,
and adds the favorite and trash lines to every item tooltip, so don't add your own.

### Testing

`iff-status` shows whether marks are loaded and how many there are, and `iff-list` lists every
marked type, slot and carried item. `iff-clear` and `iff-import-qss` reset or re-import a test
character's marks.

The full reference, including the reflection details, is
[docs/API.md](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/API.md).

---

If you enjoy my mods donations are appreciated. My mods will never be paid and donations are not required.

<a href="https://ko-fi.com/midnightsfx"><img height="36" src="https://storage.ko-fi.com/cdn/kofi2.png?v=3" alt="Support me on Ko-fi"></a>
