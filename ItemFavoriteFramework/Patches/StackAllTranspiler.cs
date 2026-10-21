using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Vanilla's "Place stacks": the container button, hold-to-stack (Container.RPC_StackResponse) and
    // mods such as ValheimPlus that call it all end in Inventory.StackAll, whose per-item test is
    //
    //     ContainsItemByName(name) && !Player.m_localPlayer.IsItemEquiped(item) && AddItem(item)
    //
    // The IsItemEquiped call is swapped for one that also answers "protected", with fromInventory passed
    // along so only the player's own inventory is filtered. Coexists with EAQS, which strips its slot
    // items out of the inventory in a prefix instead.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
    internal static class StackAllTranspiler {

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            MethodInfo isItemEquiped = AccessTools.Method(typeof(Humanoid), nameof(Humanoid.IsItemEquiped));
            MethodInfo replacement = AccessTools.Method(typeof(StackAllTranspiler), nameof(IsEquipedOrProtected));

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].Calls(isItemEquiped) == false) { continue; }

                // Stack before: [humanoid, item]. Push fromInventory (arg 1; arg 0 is this) and call the
                // static helper in place, keeping any labels on the original instruction.
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = replacement;
                codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                Logger.LogDebug("Patched Inventory.StackAll to leave protected items behind.");
                return codes;
            }

            // A game update changed the method. Stacking keeps working; it just stops respecting favorites.
            Logger.LogError("Inventory.StackAll no longer calls IsItemEquiped; 'Place stacks' will not protect favorites.");
            return codes;
        }

        private static bool IsEquipedOrProtected(Humanoid humanoid, ItemDrop.ItemData item, Inventory fromInventory) {
            if (humanoid.IsItemEquiped(item)) { return true; }
            if (ValConfig.ProtectFavoritesFromStackAll.Value == false) { return false; }
            return FavoritingInput.IsPlayerInventory(fromInventory) && Marks.IsProtected(item);
        }
    }
}
