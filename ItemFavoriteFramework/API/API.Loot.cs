using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemFavoriteFramework {

    // Wishlists and loot rolls (API 3). Same signature rule as API.cs; structured data travels as JSON, and
    // player ids inside JSON are strings (a 64-bit id does not survive a parser that reads numbers as doubles).
    // Id lists may be given as strings or numbers.
    public static partial class API {

        // -----------------------------------------------------------------------------------------
        // Wishlist (local player)
        //
        // An entry is an item prefab name ("SwordIron") plus optional conditions, each owned by a provider
        // mod. It matches an item of that prefab when every condition's provider says so.
        // -----------------------------------------------------------------------------------------

        public static bool IsItemWishlisted(ItemDrop.ItemData item) => Wishlists.IsWishlisted(item);

        // [{"id":3,"prefab":"SwordIron","name":"Iron sword","autoNeed":false,
        //   "criteria":[{"provider":"epicloot","json":"{...}","desc":"Epic or better"}]}]
        public static string GetWishlistJson() {
            WishlistStore.TryGet(out WishlistData data);
            return Wishlists.ToJson(data);
        }

        // criteriaJson: "" for any item of that prefab, or {"<providerId>": {<that provider's criteria>}, ...}.
        // True when the entry is on the list afterwards (including when it already was).
        public static bool AddWishlistEntry(string prefabName, string criteriaJson) {
            if (Wishlists.TryParseCriteria(criteriaJson, out List<WishlistCriterion> criteria) == false) { return false; }
            WishResult result = Wishlists.Add(prefabName, criteria, autoNeed: false);
            return result == WishResult.Added || result == WishResult.Unchanged;
        }

        // The item as an entry, with whatever conditions the providers suggest for it.
        public static bool AddWishlistEntryFromItem(ItemDrop.ItemData item) {
            WishResult result = Wishlists.AddFromItem(item);
            return result == WishResult.Added || result == WishResult.Unchanged;
        }

        // Removes every entry for that prefab; returns how many.
        public static int RemoveWishlistEntries(string prefabName) => Wishlists.RemoveByPrefab(prefabName);

        public static void AddWishlistChangedListener(Action listener) => WishlistStore.AddListener(listener);

        public static void RemoveWishlistChangedListener(Action listener) => WishlistStore.RemoveListener(listener);

        public static void OpenWishlistEditor() => WishlistPanel.Open();

        // A mod's own wishlist conditions.
        //   getSchemaJson(prefabName): the fields to offer for that item, or "" for none. Shape:
        //     {"title":"Epic Loot","fields":[{"key":"rarity","label":"Minimum rarity","type":"enum",
        //       "options":[["2","Epic"],["3","Legendary"]]}]}
        //     types: enum, pick, multi (a list), bool, int, text. Option lists are [value, label] pairs.
        //   matches(item, criteriaJson): the criteria are a flat object of the field keys (string, or a list of
        //     strings for multi) plus "v":1. MUST depend on its two arguments only -- it is also used on other
        //     players' entries, to show who wants an item.
        //   describe(criteriaJson): a short summary, e.g. "Epic or better". Stored with the entry.
        //   suggest(item): criteria describing this specific item, or "" (may be null).
        // Registering an id again replaces it.
        public static bool RegisterWishlistProvider(string providerId, Func<string, string> getSchemaJson,
            Func<ItemDrop.ItemData, string, bool> matches, Func<string, string> describe, Func<ItemDrop.ItemData, string> suggest) =>
            WishlistProviders.Register(providerId, getSchemaJson, matches, describe, suggest);

        public static bool UnregisterWishlistProvider(string providerId) => WishlistProviders.Unregister(providerId);

        // White with a dark outline; tint it with GetWishlistColor.
        public static Sprite GetWishlistIcon() => SpriteFactory.Heart;

        public static Color GetWishlistColor() => LootConfig.WishlistColor.Value;

        // How items are named in roll windows and loot messages, e.g. a magic item's full name. The first
        // provider to return a non-empty string wins; otherwise the localized item name.
        public static void AddItemNameProvider(Func<ItemDrop.ItemData, string> provider) => ItemNames.Add(provider);

        public static void RemoveItemNameProvider(Func<ItemDrop.ItemData, string> provider) => ItemNames.Remove(provider);

        // -----------------------------------------------------------------------------------------
        // Shared wishlists (needs the mod on the server)
        // -----------------------------------------------------------------------------------------

        // {"iff":true,"protocol":1,"version":"0.1.0","rolls":true,"wishlists":true,"rollSeconds":30,
        //  "reserveSeconds":300,"maxMembers":10} -- "iff" is false until the server has answered.
        public static string GetServerFeaturesJson() {
            bool isServer = ZNet.instance != null && ZNet.instance.IsServer();
            bool rolls = isServer ? LootConfig.EnableLootRolls.Value : ClientSession.RollsEnabled;
            bool wishlists = isServer ? LootConfig.EnableWishlistSharing.Value : ClientSession.WishlistsEnabled;
            return new JsonBuilder().BeginObject()
                .Bool("iff", isServer || ClientSession.Welcomed)
                .Num("protocol", IffNet.Protocol)
                .Str("version", isServer ? ItemFavoriteFramework.PluginVersion : ClientSession.ServerVersion)
                .Bool("rolls", rolls)
                .Bool("wishlists", wishlists)
                .Num("rollSeconds", isServer ? LootConfig.RollSeconds.Value : ClientSession.RollSeconds)
                .Num("reserveSeconds", isServer ? LootConfig.ReserveMinutes.Value * 60f : ClientSession.ReserveSeconds)
                .Num("maxMembers", isServer ? LootConfig.MaxRollMembers.Value : ClientSession.MaxRollMembers)
                .EndObject().ToString();
        }

        // [{"player":"123","name":"Alice","iff":true,"sharesWishlist":true}] -- everyone the server lists.
        public static string GetConnectedPlayersJson() {
            JsonBuilder json = new JsonBuilder().BeginArray();
            if (ZNet.instance != null && ZNet.instance.IsServer()) {
                foreach (ServerPeer peer in ServerSession.All) {
                    if (peer.Verified == false) { continue; }
                    json.BeginObject().Id("player", peer.Pid).Str("name", peer.Name).Bool("iff", peer.HasIff).Bool("sharesWishlist", peer.SharesWishlist).EndObject();
                }
            } else {
                foreach (ClientSession.DirEntry entry in ClientSession.Directory) {
                    json.BeginObject().Id("player", entry.Pid).Str("name", entry.Name).Bool("iff", entry.HasIff).Bool("sharesWishlist", entry.Shares).EndObject();
                }
            }
            return json.EndArray().ToString();
        }

        // Another player's shared wishlist, in GetWishlistJson's shape; "[]" when they share none. The local
        // player's id gives the local list.
        public static string GetPlayerWishlistJson(long playerId) {
            if (playerId != 0 && playerId == WishlistStore.PlayerId) { return GetWishlistJson(); }
            return Wishlists.ToJson(WishlistRoster.TryGet(playerId, out WishlistRoster.Shared shared) ? shared.Data : null);
        }

        // Who wants this item, as far as this machine knows: [{"player":"123","name":"Alice","self":false}].
        // Includes the local player when the item is on their own list.
        public static string GetWantedByJson(ItemDrop.ItemData item) {
            JsonBuilder json = new JsonBuilder().BeginArray();
            if (Wishlists.IsWishlisted(item)) {
                json.BeginObject().Id("player", WishlistStore.PlayerId).Str("name", Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "").Bool("self", true).EndObject();
            }
            foreach (long pid in Wishlists.WantedBy(item)) {
                json.BeginObject().Id("player", pid).Str("name", WishlistRoster.NameOf(pid)).Bool("self", false).EndObject();
            }
            return json.EndArray().ToString();
        }

        // -----------------------------------------------------------------------------------------
        // Loot rolls (needs the mod on the server, and EnableLootRolls on there)
        //
        // A roll is always asked for by a mod, for one drop on the ground, among the players it names. The
        // server collects Need / Greed / Pass, rolls 1-100 for everyone who chose Need or Greed, and the best
        // choice's highest roll wins. The drop is locked while the roll runs, then reserved for the winner for a
        // while, then free; if everyone passes it is free at once.
        // -----------------------------------------------------------------------------------------

        public const int RollPass = (int)RollChoice.Pass;
        public const int RollGreed = (int)RollChoice.Greed;
        public const int RollNeed = (int)RollChoice.Need;

        // True when a roll asked for on this machine can start: on the server when EnableLootRolls is on, on a
        // client once the server has said it runs rolls.
        public static bool IsRollingAvailable() {
            if (ZNet.instance == null) { return false; }
            return ZNet.instance.IsServer() ? LootConfig.EnableLootRolls.Value : ClientSession.RollsEnabled;
        }

        // Starts a roll on a drop this machine owns -- call it right after spawning the drop, on the machine
        // that spawned it (a client, or the server itself). playerIdsJson: ["123","456"]. optionsJson (or ""):
        //   {"label":"Eikthyr","source":"mymod","groupId":"raid-42","rollSeconds":30,"reserveSeconds":300}
        //   durations are capped by the server's settings; drops with the same groupId are shown together.
        // True when the request went out (on the server: when the roll started); rollId identifies it in
        // events. False with error set otherwise: notInWorld, disabled, serverHasNoIff, invalidDrop, notOwner,
        // noMembers, alreadyRolling, noEligibleMembers, ... A client's request can still be refused by the
        // server afterwards: listen for the "refused" event.
        public static bool StartRoll(ItemDrop drop, string playerIdsJson, string optionsJson, ref string rollId, ref string error) {
            bool started = RollClient.StartRoll(drop, Json.ParseIdList(playerIdsJson), optionsJson, out string id, out string why);
            rollId = id;
            error = why;
            return started;
        }

        // Spawns the item as a drop at position and starts a roll on it, in one step. When it returns false
        // the drop is removed again, so a caller can safely hand the item out some other way instead.
        public static bool StartRollForItem(ItemDrop.ItemData item, Vector3 position, string playerIdsJson, string optionsJson, ref string rollId, ref string error) {
            if (item == null || item.m_dropPrefab == null) {
                rollId = "";
                error = "invalidItem";
                return false;
            }
            if (IsRollingAvailable() == false) {
                rollId = "";
                error = ZNet.instance == null ? "notInWorld" : "disabled";
                return false;
            }
            ItemDrop drop = ItemDrop.DropItem(item, item.m_stack, position, Quaternion.identity);
            if (StartRoll(drop, playerIdsJson, optionsJson, ref rollId, ref error)) { return true; }
            // Refused before anything went out: take the drop back rather than leave a stray copy behind.
            if (drop != null && ZNetScene.instance != null) { ZNetScene.instance.Destroy(drop.gameObject); }
            return false;
        }

        // {"rollId":"...","phase":"free|rolling|reserved","winner":"123","winnerName":"Alice","secondsLeft":42,
        //  "lockedForMe":true}, or {"phase":"free"} for a drop with no roll.
        public static string GetRollStateJson(ItemDrop drop) {
            JsonBuilder json = new JsonBuilder().BeginObject();
            ZDO zdo = drop != null && drop.m_nview != null && drop.m_nview.IsValid() ? drop.m_nview.GetZDO() : null;
            long nonce = RollStamp.ReadNonce(zdo);
            if (nonce == 0) { return json.Str("phase", "free").EndObject().ToString(); }
            LockState state = RollLocks.Resolve(zdo, RollLocks.LocalPid);
            string phase = state == LockState.Rolling ? "rolling" : state == LockState.Free ? "free" : "reserved";
            json.Str("rollId", RollIds.Format(zdo.m_uid, nonce)).Str("phase", phase);
            if (RollLocks.TryGetEntry(zdo.m_uid, out RollLocks.Entry entry) && entry.Nonce == nonce) {
                json.Id("winner", entry.WinnerPid).Str("winnerName", entry.WinnerName).Num("secondsLeft", RollLocks.SecondsLeft(zdo.m_uid));
            }
            return json.Bool("lockedForMe", state == LockState.Rolling || state == LockState.ReservedForOther).EndObject().ToString();
        }

        // Under a roll or reserved for anyone. For mods that move items around without a player (vacuums,
        // auto-storage): leave such a drop alone.
        public static bool IsDropLocked(ItemDrop drop) {
            LockState state = RollLocks.Resolve(drop, 0);
            return state != LockState.Free;
        }

        // Whether that player may take the drop now: free, or reserved for them.
        public static bool CanPlayerPickUp(ItemDrop drop, long playerId) => RollLocks.IsLockedFor(drop, playerId) == false;

        // Votes for the local player: RollNeed, RollGreed or RollPass. False when there is no such open roll
        // or the player already voted.
        public static bool SubmitRollVote(string rollId, int choice) {
            if (choice < RollPass || choice > RollNeed) { return false; }
            return RollClient.Vote(rollId, (RollChoice)choice);
        }

        // Roll events as JSON, each with "event" and "side" ("server" on the server, "client" on clients):
        //   accepted / refused {"rollId","reason"}            -- the requester's machine and the server
        //   prompt {"rollId","prefab","name","label","groupId","seconds","members":[...],"wishlisted"} -- members
        //   vote {"rollId","player","voted","of"}             -- members and the server
        //   result {"rollId","outcome":"won|allPassed|cancelled","winner","winnerName","reserveSeconds",
        //           "prefab","label","groupId","rolls":[{"player","name","choice","roll","rerolls"}],
        //           "notEligible":[...]}                       -- members, the requester and the server
        //   lock {"rollId","phase","winner","secondsLeft"}    -- everyone with the mod
        public static void AddRollListener(Action<string> listener) => RollEvents.Add(listener);

        public static void RemoveRollListener(Action<string> listener) => RollEvents.Remove(listener);

        // Helpers for building a member list.
        // Players within radius of point: on a server from every player's position, on a client from the
        // players loaded nearby. ["123","456"].
        public static string GetPlayerIdsNearJson(Vector3 point, float radius) => RollHelpers.ToJson(RollHelpers.PlayersNear(point, radius));

        // Players who hit the character, from vanilla's per-attacker flags. Call it on the character's owner
        // before the character is destroyed (e.g. a prefix on Character.OnDeath). ["123","456"].
        public static string GetDamagerPlayerIdsJson(Character character) => RollHelpers.ToJson(RollHelpers.Damagers(character));
    }
}
