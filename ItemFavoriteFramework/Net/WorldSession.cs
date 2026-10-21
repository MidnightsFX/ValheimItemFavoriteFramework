using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Everything that belongs to one world connection, cleared whenever a world starts or ends -- including
    // the suspend path (ShutdownWithoutSave), which skips Shutdown.
    internal static class WorldSession {
        internal static void Reset() {
            ServerSession.Reset();
            ClientSession.Reset();
            WishlistServer.Reset();
            WishlistSync.Reset();
            WishlistRoster.Reset();
            WishlistNotifier.Reset();
            RollServer.Reset();
            RollClient.Reset();
            RollLocks.Reset();
            IffNet.Reset();
        }
    }

    // One ZNet.Update postfix drives everything that runs on a timer. It runs on a dedicated server, a listen
    // host and clients, only while a world is up, and after ZDOMan has taken in the frame's ZDOs, so a roll
    // request's drop is visible as soon as it arrives. Each part has its own catch: one broken part must not
    // stop the rest, and a per-frame error is logged once.
    internal static class WorldTick {
        private static float nextServer;
        private static float nextServerSlow;
        private static float nextClient;
        private static float nextNotifier;
        private static readonly HashSet<string> reported = new HashSet<string>();

        internal static void Run() {
            ZNet znet = ZNet.instance;
            if (znet == null) { return; }
            float now = Time.realtimeSinceStartup;

            if (znet.IsServer()) {
                if (now >= nextServer) {
                    nextServer = now + 0.1f;
                    Guard("rolls (server)", () => RollServer.Tick(now));
                }
                if (now >= nextServerSlow) {
                    nextServerSlow = now + 0.5f;
                    Guard("sessions (server)", () => ServerSession.Tick(now));
                    Guard("wishlists (server)", () => WishlistServer.Tick(now));
                }
            }

            if (Player.m_localPlayer == null) { return; }
            Guard("roll keys", RollClient.PollKeys);
            Guard("wishlist key", WishlistPanel.PollKey);
            Guard("roll window", RollWindow.Update);
            if (now >= nextClient) {
                nextClient = now + 0.25f;
                Guard("session (client)", () => ClientSession.Tick(now));
                Guard("wishlist sync", () => WishlistSync.Tick(now));
                Guard("roll locks", () => RollLocks.Tick(now));
                Guard("rolls (client)", () => RollClient.Tick(now));
            }
            if (now >= nextNotifier) {
                nextNotifier = now + 1f;
                Guard("wishlist notices", () => WishlistNotifier.Tick(now));
            }
        }

        private static void Guard(string what, Action action) {
            try {
                action();
            } catch (Exception e) {
                if (reported.Add(what)) {
                    Logger.LogError($"The {what} update failed; further errors from it are only logged at debug level: {e}");
                } else {
                    Logger.LogDebug($"The {what} update failed again: {e.Message}");
                }
            }
        }
    }

    internal static class NetPatches {
        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        internal static class OnNewConnection {
            private static void Postfix(ZNet __instance, ZNetPeer peer) {
                try {
                    IffNet.RegisterRpcs(__instance, peer);
                } catch (Exception e) {
                    Logger.LogError($"Could not register the wishlist and loot-roll messages for a connection: {e}");
                }
            }
        }

        // Every disconnect, whatever its cause, ends here.
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        internal static class Disconnect {
            private static void Prefix(ZNet __instance, ZNetPeer peer) {
                if (__instance.IsServer() == false) { return; }
                try {
                    ServerSession.OnDisconnect(peer);
                } catch (Exception e) {
                    Logger.LogError($"Cleaning up after a disconnect failed: {e}");
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
        internal static class Shutdown {
            private static void Prefix() => WorldSession.Reset();
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.ShutdownWithoutSave))]
        internal static class ShutdownWithoutSave {
            private static void Prefix() => WorldSession.Reset();
        }

        [HarmonyPatch(typeof(ZNet), "Awake")]
        internal static class Awake {
            private static void Postfix() => WorldSession.Reset();
        }

        [HarmonyPatch(typeof(ZNet), "Update")]
        internal static class Update {
            private static void Postfix() => WorldTick.Run();
        }
    }
}
