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
|Add to/remove from Wishlist | Left Alt + Left Shift + left click an item | ![wishlist](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/images/wishlist_heart.png?raw=true) |

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

## For mod authors

Other mods can check and set these marks, add marks of their own, read and fill wishlists, and start
loot rolls through the framework's public API. Setup, every endpoint and worked examples are in the
[API guide](https://github.com/MidnightsFX/ValheimItemFavoriteFramework/blob/master/docs/API.md).

---

If you enjoy my mods donations are appreciated. My mods will never be paid and donations are not required.

<a href="https://ko-fi.com/midnightsfx"><img height="36" src="https://storage.ko-fi.com/cdn/kofi2.png?v=3" alt="Support me on Ko-fi"></a>
