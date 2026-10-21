using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // One connected player as the server sees it.
    internal sealed class ServerPeer {
        internal long Uid;
        internal ZNetPeer Peer;
        // The host's own player on a listen server: no connection, messages are handed over in-process.
        internal bool IsLocal;
        // Sent Hello, so it runs this mod and receives the loot messages.
        internal bool HasIff;
        internal long ClaimedPid;
        // The player id read from this peer's own character, never from anything the client sent. 0 until
        // the character exists; kept through deaths, when the character id is briefly none.
        internal long Pid;
        internal string Name = "";
        internal bool SharesWishlist;
        // Owed a Welcome that carries its now-verified player id.
        internal bool NeedsWelcome;

        // The latest wishlist upload, applied at most every few seconds.
        internal bool HasPendingWishlist;
        internal bool PendingShare;
        internal int PendingRevision;
        internal byte[] PendingWishlist;
        internal float LastWishlistApplied = -999f;

        // Roll-start rate limit (token bucket).
        internal float Tokens = -1f;
        internal float TokensAt;

        internal bool Verified => Pid != 0;
    }

    // The server's view of who is connected: identity, whether they run this mod, the player directory every
    // client receives (so names and ids can be mapped anywhere), and the Hello/Welcome handshake.
    internal static class ServerSession {
        internal const byte FeatureRolls = 1;
        internal const byte FeatureWishlists = 2;
        // How long a client treats a stamped drop as locked when it has heard nothing about it.
        internal const float StartGraceSeconds = 10f;

        private static readonly Dictionary<long, ServerPeer> peers = new Dictionary<long, ServerPeer>();
        private static bool directoryDirty;
        private static bool welcomeDirty;
        private static bool settingsHooked;

        internal static IEnumerable<ServerPeer> All => peers.Values;

        internal static IEnumerable<ServerPeer> IffPeers => peers.Values.Where(p => p.HasIff);

        internal static bool Active => ZNet.instance != null && ZNet.instance.IsServer();

        internal static ServerPeer FromRpc(ZRpc rpc) {
            ZNet znet = ZNet.instance;
            ZNetPeer peer = znet != null ? znet.GetPeer(rpc) : null;
            // Not ready: the handshake has not finished, so this is not a player in the world yet.
            if (peer == null || peer.IsReady() == false) { return null; }
            if (peers.TryGetValue(peer.m_uid, out ServerPeer known) == false) {
                known = new ServerPeer { Uid = peer.m_uid, Peer = peer, Name = peer.m_playerName ?? "" };
                peers[peer.m_uid] = known;
                directoryDirty = true;
            }
            known.Peer = peer;
            return known;
        }

        internal static ServerPeer LocalPeer() {
            long uid = ZDOMan.GetSessionID();
            if (peers.TryGetValue(uid, out ServerPeer local) == false) {
                local = new ServerPeer { Uid = uid, IsLocal = true };
                peers[uid] = local;
                directoryDirty = true;
            }
            return local;
        }

        internal static ServerPeer FindByPid(long pid) {
            if (pid == 0) { return null; }
            foreach (ServerPeer peer in peers.Values) {
                if (peer.Pid == pid) { return peer; }
            }
            return null;
        }

        internal static ServerPeer FindByUid(long uid) => peers.TryGetValue(uid, out ServerPeer peer) ? peer : null;

        // Where the player is, as far as the server knows: its character's position, or the reference point
        // the client last reported when the character is not there (dead, loading).
        internal static bool TryGetPosition(ServerPeer peer, out Vector3 position) {
            position = Vector3.zero;
            if (peer == null) { return false; }
            if (peer.IsLocal) {
                if (Player.m_localPlayer == null) { return false; }
                position = Player.m_localPlayer.transform.position;
                return true;
            }
            if (peer.Peer == null) { return false; }
            ZDO character = ZDOMan.instance?.GetZDO(peer.Peer.m_characterID);
            position = character != null ? character.GetPosition() : peer.Peer.m_refPos;
            return true;
        }

        // ---------------------------------------------------------------------------------------------

        internal static void OnHello(ServerPeer from, ZPackage pkg) {
            string version = pkg.ReadString();
            long claimedPid = pkg.ReadLong();
            bool shares = pkg.ReadBool();

            from.HasIff = true;
            from.ClaimedPid = claimedPid;
            from.SharesWishlist = shares;
            Verify(from);
            directoryDirty = true;
            Logger.LogDebug($"{from.Name} ({from.Uid}) runs {ItemFavoriteFramework.PluginName} {version}.");

            SendWelcome(from);
            IffNet.ToPeer(from, IffNet.Directory, BuildDirectory());
            WishlistServer.SendAllTo(from);
            RollServer.SendSnapshotTo(from);
        }

        private static void SendWelcome(ServerPeer to) {
            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(ItemFavoriteFramework.PluginVersion);
            byte features = 0;
            if (LootConfig.EnableLootRolls.Value) { features |= FeatureRolls; }
            if (LootConfig.EnableWishlistSharing.Value) { features |= FeatureWishlists; }
            pkg.Write(features);
            pkg.Write(LootConfig.RollSeconds.Value);
            pkg.Write(LootConfig.ReserveMinutes.Value * 60f);
            pkg.Write(LootConfig.MaxRollMembers.Value);
            pkg.Write(LootConfig.MaxRequesterDistance.Value);
            pkg.Write(StartGraceSeconds);
            pkg.Write(LootConfig.MaxWishlistEntries.Value);
            pkg.Write(to.Pid);
            IffNet.ToPeer(to, IffNet.Welcome, pkg);
        }

        // Reads the id from the peer's own character. A client that claims a different id is left unverified
        // (and so can neither vote nor share), and one id can only belong to one connection.
        private static void Verify(ServerPeer peer) {
            long pid;
            string name;
            if (peer.IsLocal) {
                Player local = Player.m_localPlayer;
                if (local == null) { return; }
                pid = local.GetPlayerID();
                name = local.GetPlayerName();
            } else {
                if (peer.Peer == null) { return; }
                ZDO character = ZDOMan.instance?.GetZDO(peer.Peer.m_characterID);
                if (character == null) { return; }
                pid = character.GetLong(ZDOVars.s_playerID);
                name = character.GetString(ZDOVars.s_playerName, peer.Peer.m_playerName);
            }
            if (pid == 0) { return; }
            if (peer.Pid == pid) {
                if (string.IsNullOrEmpty(name) == false && name != peer.Name) {
                    peer.Name = name;
                    directoryDirty = true;
                }
                return;
            }
            if (peer.HasIff && peer.ClaimedPid != 0 && peer.ClaimedPid != pid) {
                Logger.LogWarning($"{name} ({peer.Uid}) claimed player id {peer.ClaimedPid} but its character is {pid}; ignoring its loot messages.");
                return;
            }
            foreach (ServerPeer other in peers.Values) {
                if (other != peer && other.Pid == pid) {
                    Logger.LogWarning($"{name} ({peer.Uid}) has player id {pid}, already in use by {other.Name} ({other.Uid}); ignoring its loot messages.");
                    return;
                }
            }
            peer.Pid = pid;
            if (string.IsNullOrEmpty(name) == false) { peer.Name = name; }
            directoryDirty = true;
            // A verified Hello deserves a Welcome that carries the id; only this peer needs it.
            if (peer.HasIff) { peer.NeedsWelcome = true; }
        }

        // Every half second on the server.
        internal static void Tick(float now) {
            HookSettings();
            ZNet znet = ZNet.instance;
            HashSet<long> connected = new HashSet<long>();
            foreach (ZNetPeer peer in znet.GetPeers()) {
                if (peer == null || peer.IsReady() == false) { continue; }
                connected.Add(peer.m_uid);
                // Players without the mod are listed too, so their names can be mapped to ids.
                if (peers.TryGetValue(peer.m_uid, out ServerPeer known) == false) {
                    known = new ServerPeer { Uid = peer.m_uid, Peer = peer, Name = peer.m_playerName ?? "" };
                    peers[peer.m_uid] = known;
                    directoryDirty = true;
                }
                known.Peer = peer;
            }
            foreach (ServerPeer stale in peers.Values.Where(p => p.IsLocal == false && connected.Contains(p.Uid) == false).ToList()) {
                Remove(stale);
            }
            foreach (ServerPeer peer in peers.Values.ToList()) { Verify(peer); }

            foreach (ServerPeer peer in IffPeers.ToList()) {
                if (welcomeDirty || peer.NeedsWelcome) {
                    peer.NeedsWelcome = false;
                    SendWelcome(peer);
                }
            }
            welcomeDirty = false;
            if (directoryDirty) {
                directoryDirty = false;
                IffNet.ToPeers(IffPeers.ToList(), IffNet.Directory, BuildDirectory());
            }
        }

        internal static void OnDisconnect(ZNetPeer peer) {
            if (peer != null && peers.TryGetValue(peer.m_uid, out ServerPeer known)) { Remove(known); }
        }

        private static void Remove(ServerPeer peer) {
            peers.Remove(peer.Uid);
            directoryDirty = true;
            RollServer.OnPeerLeft(peer);
            WishlistServer.OnPeerLeft(peer);
        }

        internal static void MarkDirectoryDirty() => directoryDirty = true;

        private static ZPackage BuildDirectory() {
            ZPackage pkg = IffNet.NewPackage();
            List<ServerPeer> listed = peers.Values.Where(p => p.Verified).ToList();
            pkg.Write(listed.Count);
            foreach (ServerPeer peer in listed) {
                pkg.Write(peer.Uid);
                pkg.Write(peer.Pid);
                pkg.Write(peer.Name ?? "");
                byte flags = 0;
                if (peer.HasIff) { flags |= 1; }
                if (peer.SharesWishlist) { flags |= 2; }
                pkg.Write(flags);
            }
            return pkg;
        }

        // A changed server setting reaches clients through a fresh Welcome.
        private static void HookSettings() {
            if (settingsHooked) { return; }
            settingsHooked = true;
            System.EventHandler changed = (_, __) => welcomeDirty = true;
            LootConfig.EnableLootRolls.SettingChanged += changed;
            LootConfig.EnableWishlistSharing.SettingChanged += changed;
            LootConfig.RollSeconds.SettingChanged += changed;
            LootConfig.ReserveMinutes.SettingChanged += changed;
            LootConfig.MaxRollMembers.SettingChanged += changed;
            LootConfig.MaxRequesterDistance.SettingChanged += changed;
            LootConfig.MaxWishlistEntries.SettingChanged += changed;
        }

        internal static void Reset() {
            peers.Clear();
            directoryDirty = false;
            welcomeDirty = false;
        }
    }
}
