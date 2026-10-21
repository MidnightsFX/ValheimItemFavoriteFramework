using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Keeps a drop under a roll, or reserved for someone else, out of this player's hands.
    //
    // Several gates, because vanilla has several ways in and one of them is not enough: picking up by hand
    // asks for ownership before it ever reaches Humanoid.Pickup, and auto-pickup drags the item toward the
    // player first. None of them vetoes through ItemDrop.CanPickup: a false there sends the caller into a
    // RequestOwn retry loop, the ownership ping-pong AutoPickupTranspiler describes.
    //
    // Pickup is decided by each player's own game in Valheim, so these stop honest players with this mod;
    // see docs/API.md for what they cannot stop.
    internal static class PickupGatePatches {
        private static float lastMessageAt = -999f;

        // Hand pickup (E), and everything else that goes through ItemDrop.Pickup: refuse before it asks for
        // ownership.
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Pickup))]
        internal static class Pickup {
            private static bool Prefix(ItemDrop __instance, Humanoid character) {
                if (character == null || character != Player.m_localPlayer) { return true; }
                LockState state = RollLocks.Resolve(__instance, RollLocks.LocalPid);
                if (state != LockState.Rolling && state != LockState.ReservedForOther) { return true; }
                Tell(__instance, state);
                return false;
            }
        }

        // No ownership requests for a locked drop from this machine, whatever asked: it is how a non-winner
        // would otherwise pull the item away from its owner.
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.RequestOwn))]
        internal static class RequestOwn {
            private static bool Prefix(ItemDrop __instance) {
                if (Player.m_localPlayer == null) { return true; }
                return RollLocks.IsLockedForLocalPlayer(__instance) == false;
            }
        }

        // The last gate before an item enters the local player's inventory, for mods that call it directly.
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup))]
        internal static class HumanoidPickup {
            private static bool Prefix(Humanoid __instance, GameObject go, ref bool __result) {
                if (__instance != Player.m_localPlayer || go == null) { return true; }
                ItemDrop drop = go.GetComponent<ItemDrop>();
                if (drop == null || RollLocks.IsLockedForLocalPlayer(drop) == false) { return true; }
                __result = false;
                return false;
            }
        }

        // The owner's side: when this machine owns a locked drop, it hands ownership only to the winner. Covers
        // players without the mod asking for it, as long as the current owner runs it.
        [HarmonyPatch(typeof(ItemDrop), "RPC_RequestOwn")]
        internal static class OwnerRefuses {
            private static bool Prefix(ItemDrop __instance, long uid) {
                if (OwnerRefusalEnabled() == false) { return true; }
                ZNetView nview = __instance.m_nview;
                if (nview == null || nview.IsValid() == false || nview.IsOwner() == false) { return true; }
                ZDO zdo = nview.GetZDO();
                if (RollStamp.ReadNonce(zdo) == 0) { return true; }
                long pid = PidOfPeer(uid);
                LockState state = RollLocks.Resolve(zdo, pid);
                if (state == LockState.Free || state == LockState.ReservedForMe) { return true; }
                Logger.LogDebug($"Refused ownership of locked drop {zdo.m_uid} to peer {uid} (player {pid}).");
                return false;
            }

            private static bool OwnerRefusalEnabled() {
                if (ZNet.instance == null) { return false; }
                // The server's own setting on the server; elsewhere only once the server confirmed it runs rolls.
                return ZNet.instance.IsServer() ? LootConfig.OwnerRefusesLockedOwnership.Value
                    : ClientSession.RollsEnabled && LootConfig.OwnerRefusesLockedOwnership.Value;
            }

            private static long PidOfPeer(long uid) {
                if (ZNet.instance.IsServer()) {
                    ServerPeer peer = ServerSession.FindByUid(uid);
                    if (peer != null) { return peer.Pid; }
                }
                long pid = ClientSession.PidOfUid(uid);
                if (pid != 0) { return pid; }
                foreach (Player player in Player.GetAllPlayers()) {
                    if (player.GetOwner() == uid) { return player.GetPlayerID(); }
                }
                return 0;
            }
        }

        // Vanilla merges same-type stacks lying close together; a locked stack must not swallow free items or
        // be swallowed by them.
        [HarmonyPatch(typeof(ItemDrop), "AutoStackItems")]
        internal static class AutoStack {
            private static int itemMask;

            private static bool Prefix(ItemDrop __instance) {
                if (IsLocked(__instance)) { return false; }
                if (itemMask == 0) { itemMask = LayerMask.GetMask("item"); }
                foreach (Collider collider in Physics.OverlapSphere(__instance.transform.position, 4f, itemMask)) {
                    ItemDrop other = collider.attachedRigidbody != null ? collider.attachedRigidbody.GetComponent<ItemDrop>() : null;
                    if (other != null && other != __instance && IsLocked(other)) { return false; }
                }
                return true;
            }

            // Rolling or reserved for anyone. A drop whose roll is over stacks normally again.
            private static bool IsLocked(ItemDrop drop) => RollLocks.Resolve(drop, 0) != LockState.Free;
        }

        private static void Tell(ItemDrop drop, LockState state) {
            Player player = Player.m_localPlayer;
            if (player == null || Time.time - lastMessageAt < 1f) { return; }
            lastMessageAt = Time.time;
            if (state == LockState.Rolling) {
                player.Message(MessageHud.MessageType.Center, RollClient.Localize("$iff_lock_rolling"));
                return;
            }
            ZDOID id = drop.m_nview.GetZDO().m_uid;
            string winner = RollLocks.TryGetEntry(id, out RollLocks.Entry entry) ? entry.WinnerName : "";
            player.Message(MessageHud.MessageType.Center, RollClient.Localize("$iff_lock_reserved", winner, Clock(RollLocks.SecondsLeft(id))));
        }

        internal static string Clock(float seconds) {
            TimeSpan span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : span.ToString(@"m\:ss", CultureInfo.InvariantCulture);
        }
    }
}
