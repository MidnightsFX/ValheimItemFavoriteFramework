using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // None of these are cheats: they only read or change the local character's own marks.
    internal static partial class TerminalManager {
        // Called from Init().
        private static void RegisterCommands() {
            RegisterHelpCommand();
            RegisterFavoriteCommands();
            RegisterLootCommands();
        }

        private static readonly List<string> ClearTargets = new List<string> { "favorites", "trash", "slots", "all" };

        private static void RegisterFavoriteCommands() {
            _ = new ModCommand($"{CommandPrefix}-status",
                "Shows whether this character's favorites are loaded, and how many of each mark it has.",
                Status, "favorites");

            _ = new ModCommand($"{CommandPrefix}-list",
                "Lists favorited and trash-flagged item types, favorited slots, and the marked items you carry.",
                List, "favorites");

            _ = new ModCommand($"{CommandPrefix}-import-qss",
                "Format: [optional: force] Copies this character's Quick Stack Store Sort favorites. Without force it only runs if it has not run for this character yet.",
                ImportQss, "favorites", input => input.Length <= 2 ? new List<string> { "force" } : new List<string>());

            _ = new ModCommand($"{CommandPrefix}-clear",
                "Format: [favorites/trash/slots/all] Removes this character's marks of that kind, including marks on items stored anywhere else.",
                Clear, "favorites", input => input.Length <= 2 ? ClearTargets : new List<string>());
        }

        private static void Status(ModCommandArgs args) {
            if (MarkStore.TryGet(out MarkData data) == false) {
                args.Output.Warning("No character is loaded.");
                return;
            }
            Inventory inventory = Player.m_localPlayer.GetInventory();
            args.Output.Info($"{ItemFavoriteFramework.PluginName} {ItemFavoriteFramework.PluginVersion}, API {API.ApiVersion}");
            args.Output.Detail($"Player id        : {MarkStore.PlayerId}");
            args.Output.Detail($"Favorite types   : {data.FavoriteTypes.Count}");
            args.Output.Detail($"Trash types      : {data.TrashTypes.Count}");
            args.Output.Detail($"Favorited slots  : {data.Slots.Count}");
            args.Output.Detail($"Carried, marked  : {inventory.GetAllItems().Count(i => Marks.GetInstanceMark(i) != ItemMark.None)} items with a per-item mark");
            args.Output.Detail($"Generations      : favorites {data.FavoriteGeneration}, trash {data.TrashGeneration}");
            args.Output.Detail($"QSS imported     : {(data.QssImported ? "yes" : "no")}{(ModCompat.QuickStackStoreLoaded ? " (Quick Stack Store Sort is installed)" : "")}");
            args.Output.Detail($"Middle click     : {(ModCompat.MiddleClickTrashActive ? "flags trash" : ModCompat.AzuAutoStoreLoaded ? "off (AzuAutoStore is installed)" : "off")}");
            args.Output.Detail($"Wishlist entries : {(WishlistStore.TryGet(out WishlistData wishlist) ? wishlist.Entries.Count : 0)}");
            args.Output.Detail($"Loot channel     : {ClientSession.Status}");
            if (MarkStore.ReadOnly) { args.Output.Error(MarkStore.Problem); }
            if (WishlistStore.ReadOnly) { args.Output.Error(WishlistStore.Problem); }
        }

        private static void List(ModCommandArgs args) {
            if (MarkStore.TryGet(out MarkData data) == false) {
                args.Output.Warning("No character is loaded.");
                return;
            }
            args.Output.Info("Favorite types: " + Names(data.FavoriteTypes), log: false);
            args.Output.Info("Trash types: " + Names(data.TrashTypes), log: false);
            args.Output.Info("Favorited slots: " + (data.Slots.Count == 0 ? "none" :
                string.Join(" ", data.Slots.OrderBy(s => s.y).ThenBy(s => s.x).Select(s => $"({s.x},{s.y})"))), log: false);

            List<string> carried = Player.m_localPlayer.GetInventory().GetAllItems()
                .Where(i => Marks.GetInstanceMark(i) != ItemMark.None)
                .OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x)
                .Select(i => $"{Localization.instance.Localize(i.m_shared.m_name)} ({i.m_gridPos.x},{i.m_gridPos.y}) {Marks.GetInstanceMark(i)}")
                .ToList();
            args.Output.Info("Carried items with a per-item mark: " + (carried.Count == 0 ? "none" : string.Join(", ", carried)), log: false);
        }

        private static string Names(IEnumerable<string> names) {
            List<string> localized = names.Select(n => $"{Localization.instance.Localize(n)} [{n}]").OrderBy(n => n).ToList();
            return localized.Count == 0 ? "none" : string.Join(", ", localized);
        }

        private static void ImportQss(ModCommandArgs args) {
            QssImport.Result result = QssImport.Run(force: args.Has("force"));
            if (result.Imported) {
                args.Output.Info($"Imported: {result}.");
            } else if (result.Failed) {
                args.Output.Error($"Not imported: {result}.");
            } else {
                args.Output.Warning($"Not imported: {result}.");
            }
        }

        private static void Clear(ModCommandArgs args) {
            string target = args.Args.GetString(0, "").ToLowerInvariant();
            if (ClearTargets.Contains(target) == false) {
                args.Output.Error($"Usage: {CommandPrefix}-clear [{string.Join("/", ClearTargets)}]");
                return;
            }
            if (MarkStore.TryGet(out _) == false) {
                args.Output.Warning("No character is loaded.");
                return;
            }
            if (MarkStore.ReadOnly) {
                args.Output.Error(MarkStore.Problem);
                return;
            }

            bool all = target == "all";
            if (all || target == "favorites") { Marks.ClearFavorites(); }
            if (all || target == "trash") { Marks.ClearTrash(); }
            if (all || target == "slots") { Marks.ClearSlots(); }
            args.Output.Info($"Cleared {target}.");
        }
    }
}
