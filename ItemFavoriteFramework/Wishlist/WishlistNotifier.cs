using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // "An item on your wishlist dropped nearby" and "...is in this chest": once per drop, and once per
    // container and item, for the session.
    //
    // A drop counts when it is fresh loot -- never picked up by anyone (m_pickedUp is set the moment an item
    // enters a player's inventory, so re-dropped items are skipped) and spawned within the last minute.
    internal static class WishlistNotifier {
        private const double FreshSeconds = 60.0;
        private const int MaxRemembered = 5000;

        private static readonly HashSet<ZDOID> seenDrops = new HashSet<ZDOID>();
        private static readonly HashSet<string> seenContainerItems = new HashSet<string>();

        internal static void Tick(float now) {
            if (LootConfig.NotifyWishlistDrops.Value == false) { return; }
            if (WishlistStore.TryGet(out WishlistData data) == false || data.Entries.Count == 0) { return; }
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null) { return; }
            if (seenDrops.Count > MaxRemembered) { seenDrops.Clear(); }

            float range = LootConfig.WishlistNotifyRange.Value;
            Vector3 at = player.transform.position;
            foreach (ItemDrop drop in ItemDrop.s_instances) {
                if (drop == null || drop.m_nview == null || drop.m_nview.IsValid() == false) { continue; }
                // EpicLoot fills in a magic item after spawning; give it a moment.
                if (Time.time - drop.m_spawnTime < 0.5f) { continue; }
                ZDO zdo = drop.m_nview.GetZDO();
                if (seenDrops.Contains(zdo.m_uid)) { continue; }
                if (drop.m_itemData.m_pickedUp || SpawnAge(zdo) > FreshSeconds) {
                    seenDrops.Add(zdo.m_uid);
                    continue;
                }
                if (Vector3.Distance(at, drop.transform.position) > range) { continue; }
                // A roll this player is in announces the item itself.
                if (RollClient.Open.Any(r => r.Drop == zdo.m_uid)) {
                    seenDrops.Add(zdo.m_uid);
                    continue;
                }
                if (Wishlists.IsWishlisted(drop.m_itemData) == false) { continue; }
                seenDrops.Add(zdo.m_uid);
                player.Message(MessageHud.MessageType.TopLeft, RollClient.Localize("$iff_wish_drop_nearby", ItemNames.Of(drop.m_itemData)), 0, drop.m_itemData.GetIcon());
            }
        }

        private static double SpawnAge(ZDO zdo) {
            long spawned = zdo.GetLong(ZDOVars.s_spawnTime);
            if (spawned <= 0) { return 0; }
            return (ZNet.instance.GetTime().Ticks - spawned) / (double)System.TimeSpan.TicksPerSecond;
        }

        internal static void OnContainerOpened(Container container) {
            if (LootConfig.NotifyWishlistInContainers.Value == false || Player.m_localPlayer == null) { return; }
            if (WishlistStore.TryGet(out WishlistData data) == false || data.Entries.Count == 0) { return; }
            Inventory inventory = container.GetInventory();
            if (inventory == null) { return; }
            string where = container.m_nview != null && container.m_nview.IsValid() ? container.m_nview.GetZDO().m_uid.ToString() : container.GetInstanceID().ToString();

            List<string> found = new List<string>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems()) {
                if (Wishlists.IsWishlisted(item) == false) { continue; }
                string key = where + "|" + Wishlists.PrefabName(item) + "|" + item.m_quality;
                if (seenContainerItems.Add(key)) { found.Add(ItemNames.Of(item)); }
            }
            if (found.Count == 0) { return; }
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, RollClient.Localize("$iff_wish_in_container", string.Join(", ", found.Distinct())));
        }

        internal static void Reset() {
            seenDrops.Clear();
            seenContainerItems.Clear();
        }
    }
}
