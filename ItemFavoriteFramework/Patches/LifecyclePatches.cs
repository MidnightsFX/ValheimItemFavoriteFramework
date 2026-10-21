using HarmonyLib;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    internal static class LifecyclePatches {

        // Any (re)load of the local character: the first load on spawn, and mods like ServerCharacters
        // that load the character again afterwards. The character-select preview loads a Player too, but
        // it is never the local player.
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        internal static class PlayerLoad {
            private static void Postfix(Player __instance) {
                if (__instance == Player.m_localPlayer) { MarkStore.Invalidate(); }
            }
        }

        // First-load work goes here, not in Player.Load: a brand-new character is never loaded, only
        // given its starting items. After ServerCharacters, which swaps in the server's copy of the
        // character during spawn.
        [HarmonyPatch(typeof(Game), "SpawnPlayer")]
        internal static class GameSpawnPlayer {
            [HarmonyAfter("org.bepinex.plugins.servercharacters")]
            private static void Postfix() {
                MarkStore.Invalidate();
                if (MarkStore.TryGet(out _) == false) { return; }

                ModCompat.WarnOnce();
                if (ValConfig.ImportQuickStackStoreFavorites.Value) {
                    QssImport.Result result = QssImport.Run(force: false);
                    if (result.Imported) { Logger.LogInfo($"Imported Quick Stack Store Sort favorites: {result}."); }
                    if (result.Failed) { Logger.LogWarning($"Could not import Quick Stack Store Sort favorites: {result}."); }
                }
                MarkStore.NotifyChanged();
            }
        }
    }
}
