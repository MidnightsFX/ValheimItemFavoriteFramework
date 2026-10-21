using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Wishlist and loot-roll commands. The vote commands are not cheats, so they also work from chat as
    // /need, /greed and /pass.
    internal static partial class TerminalManager {
        private static readonly List<string> VoteTargets = new List<string> { "all" };

        private static void RegisterLootCommands() {
            _ = new ModCommand($"{CommandPrefix}-wishlist",
                "Opens the wishlist editor.",
                args => WishlistPanel.Open(), "wishlist");

            _ = new ModCommand($"{CommandPrefix}-wishlist-list",
                "Lists your wishlist.",
                WishlistList, "wishlist");

            _ = new ModCommand($"{CommandPrefix}-wishlist-add",
                "Format: [item prefab] [optional: conditions json] Adds an item to your wishlist, e.g. iff-wishlist-add SwordIron.",
                WishlistAdd, "wishlist", ItemPrefabOptions);

            _ = new ModCommand($"{CommandPrefix}-wishlist-remove",
                "Format: [item prefab or entry number] Removes entries from your wishlist.",
                WishlistRemove, "wishlist", WishlistPrefabOptions);

            _ = new ModCommand($"{CommandPrefix}-wishlist-clear",
                "Empties your wishlist.",
                args => {
                    args.Output.Info(Wishlists.Clear() ? "Your wishlist is empty." : "The wishlist could not be changed.");
                }, "wishlist");

            _ = new ModCommand($"{CommandPrefix}-wishlist-show",
                "Format: [player name] Shows the wishlist another player shares on this server.",
                WishlistShow, "wishlist", input => input.Length <= 2 ? WishlistRoster.All.Select(s => s.Name).ToList() : new List<string>());

            // A tiny provider, so the condition editor and matching can be tried without EpicLoot.
            _ = new ModCommand($"{CommandPrefix}-wishlist-testprovider",
                "Registers a test wishlist condition: minimum item quality.",
                RegisterTestProvider, "wishlist", hideFromHelp: true);

            _ = new ModCommand($"{CommandPrefix}-net-status",
                "Shows whether the server runs this mod, its loot settings, and who it lists.",
                NetStatus, "loot");

            _ = new ModCommand($"{CommandPrefix}-roll-status",
                "Lists the loot rolls you are in, the locks this game knows, and on a server every roll.",
                RollStatus, "loot");

            _ = new ModCommand($"{CommandPrefix}-roll-test",
                "Format: [optional: item prefab] [optional: count] [optional: radius] [optional: quality] Spawns items in front of you and starts a roll on each among the players nearby. Admins only on a server.",
                RollTest, "loot", ItemPrefabOptions);

            _ = new ModCommand($"{CommandPrefix}-roll-debug",
                "Format: [forcetie] The next roll's first round ties.",
                RollDebug, "loot", serverAuthoritative: true, requiresAdmin: true, hideFromHelp: true);

            RegisterVote("need", RollChoice.Need);
            RegisterVote("greed", RollChoice.Greed);
            RegisterVote("pass", RollChoice.Pass);
        }

        // iff-need, plus a short "need" when no other mod has that name.
        private static void RegisterVote(string word, RollChoice choice) {
            string description = $"Format: [optional: roll number, or all] Votes {word} on your oldest open roll.";
            CommandAction action = args => Vote(args, choice);
            _ = new ModCommand($"{CommandPrefix}-{word}", description, action, "loot", input => input.Length <= 2 ? VoteTargets : new List<string>());
            if (Terminal.commands.ContainsKey(word) == false) {
                _ = new ModCommand(word, description, action, "loot", hideFromHelp: true, canonical: $"{CommandPrefix}-{word}");
            }
        }

        private static void Vote(ModCommandArgs args, RollChoice choice) {
            IReadOnlyList<ClientRoll> open = RollClient.Open;
            if (open.Count == 0) {
                args.Output.Warning("You have no open rolls.");
                return;
            }
            string target = args.Args.GetString(0);
            if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase)) {
                args.Output.Info($"Voted {RollIds.ChoiceName(choice)} on {RollClient.VoteAll(choice)} rolls.");
                return;
            }
            ClientRoll roll;
            if (target.Length > 0) {
                int index = args.Args.GetInt(0) - 1;
                if (index < 0 || index >= open.Count) {
                    args.Output.Error($"There is no roll number {target}; you have {open.Count}.");
                    return;
                }
                roll = open[index];
            } else {
                roll = RollClient.Oldest();
            }
            if (roll == null || RollClient.Vote(roll, choice) == false) {
                args.Output.Warning("You already voted on that roll.");
                return;
            }
            args.Output.Info($"Voted {RollIds.ChoiceName(choice)} on {roll.Name}.");
        }

        // ---------------------------------------------------------------------------------------------

        private static List<string> ItemPrefabOptions(string[] input) {
            if (input.Length > 2 || ObjectDB.instance == null) { return new List<string>(); }
            return ObjectDB.instance.m_items.Where(i => i != null && i.GetComponent<ItemDrop>() != null).Select(i => i.name).OrderBy(n => n).ToList();
        }

        private static List<string> WishlistPrefabOptions(string[] input) {
            if (input.Length > 2 || WishlistStore.TryGet(out WishlistData data) == false) { return new List<string>(); }
            return data.Entries.Select(e => e.Prefab).Distinct().ToList();
        }

        private static void WishlistList(ModCommandArgs args) {
            if (WishlistStore.TryGet(out WishlistData data) == false) {
                args.Output.Warning("No character is loaded.");
                return;
            }
            if (data.Entries.Count == 0) {
                args.Output.Info("Your wishlist is empty.", log: false);
            }
            for (int i = 0; i < data.Entries.Count; i++) {
                WishlistEntry entry = data.Entries[i];
                args.Output.Info($"{i + 1}. {Wishlists.Describe(entry)} [{entry.Prefab}]{(entry.AutoNeed ? " (auto need)" : "")}", log: false);
            }
            args.Output.Detail($"Sharing: {(ClientSession.WishlistsEnabled ? (LootConfig.ShareWishlist.Value ? "on" : "off (ShareWishlist)") : "the server does not share wishlists")}", log: false);
            if (WishlistStore.ReadOnly) { args.Output.Error(WishlistStore.Problem); }
        }

        private static void WishlistAdd(ModCommandArgs args) {
            string prefab = args.Args.GetString(0);
            if (prefab.Length == 0) {
                args.Output.Error("Name an item prefab, e.g. iff-wishlist-add SwordIron.");
                return;
            }
            if (ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(prefab) == null) {
                args.Output.Warning($"No item called {prefab} is loaded; it was added anyway and will match once it exists.");
            }
            string criteriaJson = args.Args.GetStringFrom(1);
            if (Wishlists.TryParseCriteria(criteriaJson, out List<WishlistCriterion> criteria) == false) {
                args.Output.Error("The conditions must be a JSON object such as {\"provider.id\":{\"key\":\"value\"}}.");
                return;
            }
            WishResult result = Wishlists.Add(prefab, criteria, autoNeed: false);
            args.Output.Info(result == WishResult.Added ? $"Added {Wishlists.DisplayName(prefab)}." : $"Not added: {result}.");
        }

        private static void WishlistRemove(ModCommandArgs args) {
            string target = args.Args.GetString(0);
            if (WishlistStore.TryGet(out WishlistData data) == false) {
                args.Output.Warning("No character is loaded.");
                return;
            }
            if (int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= data.Entries.Count) {
                WishlistEntry entry = data.Entries[number - 1];
                args.Output.Info(Wishlists.Remove(entry.Id) ? $"Removed {Wishlists.Describe(entry)}." : "The wishlist could not be changed.");
                return;
            }
            int removed = Wishlists.RemoveByPrefab(target);
            args.Output.Info(removed > 0 ? $"Removed {removed} entries for {target}." : $"Nothing on your wishlist is {target}.");
        }

        private static void WishlistShow(ModCommandArgs args) {
            string name = args.Args.GetStringFrom(0);
            WishlistRoster.Shared shared = WishlistRoster.All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (shared == null) {
                args.Output.Warning(ClientSession.WishlistsEnabled ? $"{name} does not share a wishlist here." : "This server does not share wishlists.");
                return;
            }
            args.Output.Info($"{shared.Name}'s wishlist ({shared.Data.Entries.Count}):", log: false);
            foreach (WishlistEntry entry in shared.Data.Entries) { args.Output.Detail(Wishlists.Describe(entry), log: false); }
        }

        private static void RegisterTestProvider(ModCommandArgs args) {
            const string id = "iff.test.quality";
            WishlistProviders.Register(id,
                prefab => "{\"title\":\"Test\",\"fields\":[{\"key\":\"min\",\"label\":\"Minimum quality\",\"type\":\"enum\",\"options\":[[\"2\",\"2\"],[\"3\",\"3\"],[\"4\",\"4\"]]}]}",
                (item, json) => {
                    IDictionary<string, object> criteria = Json.ParseObject(json);
                    int min = (int)Json.GetFloat(criteria, "min", 1f);
                    return item.m_quality >= min;
                },
                json => "quality " + Json.GetString(Json.ParseObject(json), "min", "?") + "+",
                item => item.m_quality > 1 ? "{\"v\":1,\"min\":\"" + item.m_quality.ToString(CultureInfo.InvariantCulture) + "\"}" : "");
            args.Output.Info($"Registered the test wishlist condition '{id}' (minimum quality) for this session.");
        }

        private static void NetStatus(ModCommandArgs args) {
            args.Output.Info($"Loot channel: {ClientSession.Status}", log: false);
            if (ClientSession.Welcomed) {
                args.Output.Detail($"Loot rolls       : {(ClientSession.RollsEnabled ? "on" : "off")} (roll {ClientSession.RollSeconds}s, reserve {ClientSession.ReserveSeconds:0}s, up to {ClientSession.MaxRollMembers} players)", log: false);
                args.Output.Detail($"Wishlist sharing : {(ClientSession.WishlistsEnabled ? "on" : "off")} (up to {ClientSession.MaxWishlistEntries} entries), yours {(LootConfig.ShareWishlist.Value ? "shared" : "private")}", log: false);
                args.Output.Detail($"Your player id   : {ClientSession.VerifiedPid}{(ClientSession.VerifiedPid == 0 ? " (not verified yet)" : "")}", log: false);
                foreach (ClientSession.DirEntry entry in ClientSession.Directory) {
                    args.Output.Detail($"  {entry.Name} ({entry.Pid}) {(entry.HasIff ? "runs the mod" : "no mod")}{(entry.Shares ? ", shares a wishlist" : "")}", log: false);
                }
                args.Output.Detail($"Shared wishlists : {WishlistRoster.Count}", log: false);
            }
            if (ZNet.instance != null && ZNet.instance.IsServer()) {
                args.Output.Info($"Server: {ServerSession.All.Count()} players, {ServerSession.IffPeers.Count()} with the mod, {WishlistServer.Count} shared wishlists, {RollServer.All.Count()} rolls and locks.", log: false);
            }
        }

        private static void RollStatus(ModCommandArgs args) {
            IReadOnlyList<ClientRoll> open = RollClient.Open;
            args.Output.Info($"Your open rolls: {open.Count}", log: false);
            for (int i = 0; i < open.Count; i++) {
                ClientRoll roll = open[i];
                args.Output.Detail($"  {i + 1}. {roll.Name} {roll.Label} - {Mathf.CeilToInt(roll.SecondsLeft)}s, {roll.Voted}/{roll.Of} chose, you: {RollIds.ChoiceName(roll.MyVote)}", log: false);
            }
            args.Output.Info($"Locks this game knows: {RollLocks.All.Count()}", log: false);
            foreach (RollLocks.Entry entry in RollLocks.All) {
                args.Output.Detail($"  {RollIds.Format(entry.Drop, entry.Nonce)} {RollIds.PhaseName(entry.Phase)}{(entry.WinnerPid != 0 ? " for " + entry.WinnerName : "")}", log: false);
            }
            if (ZNet.instance != null && ZNet.instance.IsServer()) {
                args.Output.Info($"Server rolls: {RollServer.All.Count()}", log: false);
                foreach (ServerRoll roll in RollServer.All) {
                    args.Output.Detail($"  {roll.RollId} {roll.Prefab} {roll.Phase} members {roll.Members.Count} votes {roll.Votes.Count}{(roll.WinnerPid != 0 ? " won by " + roll.WinnerName : "")}", log: false);
                }
            }
        }

        private static void RollTest(ModCommandArgs args) {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null) {
                args.Output.Error("Load into a world first.");
                return;
            }
            if (ZNet.instance.IsServer() == false && SynchronizationManager.Instance.PlayerIsAdmin == false) {
                args.Output.Error("Only server admins can spawn test rolls.");
                return;
            }
            string prefabName = args.Args.GetString(0, "SwordIron");
            int count = Mathf.Clamp(args.Args.GetInt(1, 1), 1, 10);
            float radius = Mathf.Clamp(args.Args.GetFloat(2, 30f), 1f, 200f);
            int quality = Mathf.Clamp(args.Args.GetInt(3, 1), 1, 10);
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            ItemDrop template = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (template == null) {
                args.Output.Error($"No item called {prefabName}.");
                return;
            }

            string members = API.GetPlayerIdsNearJson(player.transform.position, radius);
            string group = count > 1 ? "iff-test-" + Time.frameCount.ToString(CultureInfo.InvariantCulture) : "";
            string options = new JsonBuilder().BeginObject().Str("label", "Test roll").Str("source", "iff-roll-test").Str("groupId", group).EndObject().ToString();
            for (int i = 0; i < count; i++) {
                ItemDrop.ItemData item = template.m_itemData.Clone();
                item.m_dropPrefab = prefab;
                item.m_quality = Mathf.Min(quality, Mathf.Max(1, item.m_shared.m_maxQuality));
                item.m_durability = item.GetMaxDurability();
                Vector3 at = player.transform.position + player.transform.forward * 2f + Vector3.up + UnityEngine.Random.insideUnitSphere * 0.5f;
                string rollId = "";
                string error = "";
                if (API.StartRollForItem(item, at, members, options, ref rollId, ref error)) {
                    args.Output.Info($"Started roll {rollId} on {Wishlists.DisplayName(prefabName)} among {members}.");
                } else {
                    args.Output.Error($"The roll did not start: {error}.");
                }
            }
        }

        private static void RollDebug(ModCommandArgs args) {
            if (args.Has("forcetie")) {
                RollResolver.ForceTieOnce = true;
                args.Output.Info("The next roll's first round ties.");
                return;
            }
            args.Output.Error("Usage: iff-roll-debug forcetie");
        }
    }
}
