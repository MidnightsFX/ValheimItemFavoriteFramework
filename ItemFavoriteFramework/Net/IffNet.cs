using System;
using System.Collections.Generic;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // The wishlist and loot-roll channel between each client and the server.
    //
    // Every message rides the peer connection itself (ZNetPeer.m_rpc), like TerminalNetwork, never ZRoutedRpc:
    // a routed RPC's sender id is written by the sending client, so anyone could vote or upload a wishlist as
    // someone else. A ZRpc handler is bound to the socket the call arrived on, so the server knows who sent it,
    // and a client registers its handlers only on its link to the server, so only the server can reach them.
    //
    // Every package starts with the protocol byte. On a listen host (and in single player) the host's own
    // player has no connection to itself, so its messages are handed straight to the other side's handler.
    internal static class IffNet {
        internal const byte Protocol = 1;
        // Well under Steam's 512 KB reliable-message limit: a raw ZRpc call is one unsliced socket message.
        internal const int MaxPayload = 200_000;

        private const string Prefix = ItemFavoriteFramework.PluginGUID + ".";
        internal const string Hello = Prefix + "Hello";
        internal const string Welcome = Prefix + "Welcome";
        internal const string Directory = Prefix + "Directory";
        internal const string WishlistPut = Prefix + "WishlistPut";
        internal const string WishlistOf = Prefix + "WishlistOf";
        internal const string RollStart = Prefix + "RollStart";
        internal const string RollAck = Prefix + "RollAck";
        internal const string RollPrompt = Prefix + "RollPrompt";
        internal const string RollVote = Prefix + "RollVote";
        internal const string RollProgress = Prefix + "RollProgress";
        internal const string RollResult = Prefix + "RollResult";
        internal const string Locks = Prefix + "Locks";
        internal const string RollNotice = Prefix + "RollNotice";

        private static readonly Dictionary<string, Action<ServerPeer, ZPackage>> serverHandlers = new Dictionary<string, Action<ServerPeer, ZPackage>> {
            { Hello, ServerSession.OnHello },
            { WishlistPut, WishlistServer.OnPut },
            { RollStart, RollServer.OnStart },
            { RollVote, RollServer.OnVote },
        };

        private static readonly Dictionary<string, Action<ZPackage>> clientHandlers = new Dictionary<string, Action<ZPackage>> {
            { Welcome, ClientSession.OnWelcome },
            { Directory, ClientSession.OnDirectory },
            { WishlistOf, ClientSession.OnWishlistOf },
            { RollAck, RollClient.OnAck },
            { RollPrompt, RollClient.OnPrompt },
            { RollProgress, RollClient.OnProgress },
            { RollResult, RollClient.OnResult },
            { Locks, RollLocks.OnLocks },
            { RollNotice, RollClient.OnNotice },
        };

        private static readonly HashSet<string> reportedFailures = new HashSet<string>();

        // ZNet.OnNewConnection postfix: on the server once per client, on a client once for its server link.
        internal static void RegisterRpcs(ZNet znet, ZNetPeer peer) {
            if (znet.IsServer()) {
                foreach (KeyValuePair<string, Action<ServerPeer, ZPackage>> pair in serverHandlers) {
                    string name = pair.Key;
                    peer.m_rpc.Register<ZPackage>(name, (rpc, pkg) => DispatchServer(name, ServerSession.FromRpc(rpc), pkg));
                }
            } else {
                foreach (KeyValuePair<string, Action<ZPackage>> pair in clientHandlers) {
                    string name = pair.Key;
                    peer.m_rpc.Register<ZPackage>(name, (rpc, pkg) => DispatchClient(name, pkg));
                }
            }
        }

        internal static ZPackage NewPackage() {
            ZPackage pkg = new ZPackage();
            pkg.Write(Protocol);
            return pkg;
        }

        // From this machine's player to the server, wherever the server is.
        internal static void ToServer(string name, ZPackage pkg) {
            ZNet znet = ZNet.instance;
            if (znet == null || TooBig(name, pkg)) { return; }
            if (znet.IsServer()) {
                DispatchServer(name, ServerSession.LocalPeer(), new ZPackage(pkg.GetArray()));
                return;
            }
            ZRpc rpc = znet.GetServerRPC();
            if (rpc != null) { rpc.Invoke(name, pkg); }
        }

        internal static void ToPeer(ServerPeer peer, string name, ZPackage pkg) {
            if (peer == null || TooBig(name, pkg)) { return; }
            if (peer.IsLocal) {
                DispatchClient(name, new ZPackage(pkg.GetArray()));
                return;
            }
            ZRpc rpc = peer.Peer?.m_rpc;
            if (rpc != null) { rpc.Invoke(name, pkg); }
        }

        internal static void ToPeers(IEnumerable<ServerPeer> peers, string name, ZPackage pkg) {
            foreach (ServerPeer peer in peers) { ToPeer(peer, name, pkg); }
        }

        private static bool TooBig(string name, ZPackage pkg) {
            if (pkg.Size() <= MaxPayload) { return false; }
            Logger.LogError($"Not sending {name}: {pkg.Size()} bytes is over the {MaxPayload}-byte limit for one message.");
            return true;
        }

        // Every exception stops here. One escaping a ZRpc handler makes vanilla report an incompatible
        // version and drop the rest of that batch; on a client it can end the connection.
        private static void DispatchServer(string name, ServerPeer from, ZPackage pkg) {
            if (from == null) { return; }
            try {
                byte protocol = pkg.ReadByte();
                if (protocol != Protocol) {
                    ReportOnce(name + ".protocol", $"{from.Name} ({from.Uid}) speaks loot protocol {protocol}, this server {Protocol}; " +
                        "its wishlist and roll messages are ignored until both run the same version of the mod.");
                    return;
                }
                if (serverHandlers.TryGetValue(name, out Action<ServerPeer, ZPackage> handler)) { handler(from, pkg); }
            } catch (Exception e) {
                ReportOnce(name, $"Handling {name} from {from.Name} ({from.Uid}) failed: {e}");
            }
        }

        private static void DispatchClient(string name, ZPackage pkg) {
            try {
                byte protocol = pkg.ReadByte();
                if (protocol != Protocol) {
                    ReportOnce(name + ".protocol", $"The server speaks loot protocol {protocol}, this client {Protocol}. " +
                        "Wishlist sharing and loot rolls stay off until both run the same version of the mod.");
                    return;
                }
                if (clientHandlers.TryGetValue(name, out Action<ZPackage> handler)) { handler(pkg); }
            } catch (Exception e) {
                ReportOnce(name, $"Handling {name} from the server failed: {e}");
            }
        }

        private static void ReportOnce(string key, string message) {
            if (reportedFailures.Add(key)) {
                Logger.LogError(message);
            } else {
                Logger.LogDebug(message);
            }
        }

        internal static void Reset() => reportedFailures.Clear();
    }
}
