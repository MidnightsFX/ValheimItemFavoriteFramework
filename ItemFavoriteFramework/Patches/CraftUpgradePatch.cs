using HarmonyLib;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Upgrading an item at a workbench or the forge removes it and adds a brand-new ItemData in the same
    // grid position, without its custom data -- so without this an upgraded favorite silently stops
    // being one. Type marks are unaffected; only instance marks need carrying over.
    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    internal static class CraftUpgradePatch {

        internal struct Upgrade {
            internal ItemDrop.ItemData Original;
            internal string Name;
            internal Vector2i Position;
            internal ItemMark Mark;
        }

        private static void Prefix(InventoryGui __instance, out Upgrade __state) {
            __state = default;
            ItemDrop.ItemData original = __instance.m_craftUpgradeItem;
            if (original == null) { return; }
            ItemMark mark = Marks.GetInstanceMark(original);
            if (mark == ItemMark.None) { return; }
            __state = new Upgrade { Original = original, Name = original.m_shared.m_name, Position = original.m_gridPos, Mark = mark };
        }

        private static void Postfix(Player player, Upgrade __state) {
            if (__state.Original == null || player == null) { return; }
            Inventory inventory = player.GetInventory();

            // Still there: DoCrafting returned early (missing materials, no room) and nothing changed.
            if (inventory.ContainsItem(__state.Original)) { return; }

            // A forge break refunds ingredients to the first free cell, which may be the one just freed;
            // only an item of the upgraded kind is the upgrade.
            ItemDrop.ItemData upgraded = inventory.GetItemAt(__state.Position.x, __state.Position.y);
            if (upgraded == null || upgraded == __state.Original || upgraded.m_shared.m_name != __state.Name) { return; }

            Marks.RestoreInstanceMark(upgraded, __state.Mark, inventory);
        }
    }
}
