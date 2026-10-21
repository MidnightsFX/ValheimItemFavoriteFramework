using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Auto-pickup skips drops a loot roll has locked for this player, and (opt-in) trash-flagged items.
    // Player.AutoPickup's first filter is
    //
    //     if (component == null || !component.m_autoPickup || ...) continue;
    //
    // and the m_autoPickup read is routed through Filter. Hooking there, and not at CanPickup, matters:
    // a false CanPickup makes the player RequestOwn() the item, which would pull ownership of every trash
    // drop nearby back and forth with other players. Same injection point as ComfyMods' PutMeDown, and
    // chains with it. Picking the item up by hand is left to PickupGatePatches.
    [HarmonyPatch(typeof(Player), "AutoPickup")]
    internal static class AutoPickupTranspiler {

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            FieldInfo autoPickup = AccessTools.Field(typeof(ItemDrop), nameof(ItemDrop.m_autoPickup));
            MethodInfo filter = AccessTools.Method(typeof(AutoPickupTranspiler), nameof(Filter));

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (int i = 1; i < codes.Count; i++) {
                if (codes[i].LoadsField(autoPickup) == false || codes[i - 1].IsLdloc() == false) { continue; }

                // [bool m_autoPickup] -> load the same ItemDrop local again -> Filter(bool, ItemDrop). The
                // copy leaves the original's labels where they are.
                codes.Insert(i + 1, new CodeInstruction(codes[i - 1].opcode, codes[i - 1].operand));
                codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, filter));
                Logger.LogDebug("Patched Player.AutoPickup to honour loot-roll locks and PreventAutoPickupOfTrash.");
                return codes;
            }

            Logger.LogError("Player.AutoPickup no longer reads ItemDrop.m_autoPickup; PreventAutoPickupOfTrash has no effect, and auto-pickup ignores loot-roll locks.");
            return codes;
        }

        private static bool Filter(bool autoPickup, ItemDrop drop) {
            if (autoPickup == false || drop == null) { return autoPickup; }
            // Under a loot roll, or reserved for someone else: neither picked up nor pulled toward this player.
            if (RollLocks.IsLockedForLocalPlayer(drop)) { return false; }
            if (ValConfig.PreventAutoPickupOfTrash.Value == false) { return true; }
            // The drop's data, custom data included, was loaded from its ZDO in ItemDrop.Awake.
            return Marks.IsItemTrashFlagged(drop.m_itemData) == false;
        }
    }
}
