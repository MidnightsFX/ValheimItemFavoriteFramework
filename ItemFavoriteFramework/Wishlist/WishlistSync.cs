using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Client side: uploads this character's wishlist when the server shares wishlists. Sent after the
    // Welcome, after any change (a couple of seconds after the last one, so editing does not stream), when
    // the character changes, and when the player turns sharing off -- which sends an empty list.
    internal static class WishlistSync {
        private const float Debounce = 2f;

        private static int sentRevision = -1;
        private static bool sentShare;
        private static long sentPid;
        private static float dueAt = -1f;

        internal static void ForceResend() {
            sentRevision = -1;
            dueAt = -1f;
        }

        internal static void Tick(float now) {
            if (ClientSession.WishlistsEnabled == false) { return; }
            if (WishlistStore.TryGet(out WishlistData data) == false) { return; }

            bool share = LootConfig.ShareWishlist.Value;
            long pid = WishlistStore.PlayerId;
            bool current = sentRevision == WishlistStore.LocalRevision && sentShare == share && sentPid == pid;
            if (current) {
                dueAt = -1f;
                return;
            }
            if (dueAt < 0f) {
                // The first upload of a connection goes at once; edits wait for the player to stop.
                dueAt = sentRevision < 0 ? now : now + Debounce;
                return;
            }
            if (now < dueAt) { return; }

            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(share);
            pkg.Write(data.Revision);
            pkg.Write(share ? data.ToBytes() : new byte[0]);
            IffNet.ToServer(IffNet.WishlistPut, pkg);
            sentRevision = WishlistStore.LocalRevision;
            sentShare = share;
            sentPid = pid;
            dueAt = -1f;
        }

        internal static void Reset() {
            sentRevision = -1;
            sentPid = 0;
            dueAt = -1f;
        }
    }

    // Server side: the wishlists players share, kept in memory only. Each upload is attributed to the
    // connection it arrived on, parsed with the server's limits, and relayed in its cleaned form, one
    // message per player -- one message with everyone's lists could pass the unsliced-message limit.
    internal static class WishlistServer {
        private const float MinInterval = 2f;

        private sealed class Stored {
            internal string Name;
            internal int Revision;
            internal byte[] Bytes;
        }

        private static readonly Dictionary<long, Stored> roster = new Dictionary<long, Stored>();

        internal static void OnPut(ServerPeer from, ZPackage pkg) {
            from.PendingShare = pkg.ReadBool();
            from.PendingRevision = pkg.ReadInt();
            from.PendingWishlist = pkg.ReadByteArray();
            from.HasPendingWishlist = true;
        }

        internal static void Tick(float now) {
            if (LootConfig.EnableWishlistSharing.Value == false) {
                if (roster.Count > 0) {
                    foreach (long pid in roster.Keys.ToList()) { RemoveAndAnnounce(pid, null); }
                }
                foreach (ServerPeer peer in ServerSession.All) { peer.HasPendingWishlist = false; }
                return;
            }
            foreach (ServerPeer peer in ServerSession.All.ToList()) {
                // Held until the peer's identity is known: a list is only ever stored under the id read from
                // the sender's own character.
                if (peer.HasPendingWishlist == false || peer.Verified == false) { continue; }
                if (now - peer.LastWishlistApplied < MinInterval) { continue; }
                peer.LastWishlistApplied = now;
                peer.HasPendingWishlist = false;
                Apply(peer, peer.PendingShare, peer.PendingRevision, peer.PendingWishlist);
            }
        }

        private static void Apply(ServerPeer peer, bool share, int revision, byte[] bytes) {
            if (peer.SharesWishlist != share) {
                peer.SharesWishlist = share;
                ServerSession.MarkDirectoryDirty();
            }
            int limit = LootConfig.MaxWishlistEntries.Value;
            if (share == false || bytes == null || bytes.Length == 0 || limit <= 0) {
                RemoveAndAnnounce(peer.Pid, peer);
                return;
            }
            if (WishlistData.TryParse(bytes, limit, out WishlistData data, out string error) == false) {
                Logger.LogWarning($"Rejected {peer.Name}'s wishlist ({peer.Uid}): {error}.");
                return;
            }
            Stored stored = new Stored { Name = peer.Name, Revision = revision, Bytes = data.ToBytes() };
            roster[peer.Pid] = stored;
            ZPackage relay = Build(peer.Pid, stored);
            IffNet.ToPeers(ServerSession.IffPeers.Where(p => p != peer).ToList(), IffNet.WishlistOf, relay);
        }

        private static void RemoveAndAnnounce(long pid, ServerPeer except) {
            if (roster.Remove(pid) == false) { return; }
            ZPackage relay = Build(pid, new Stored { Name = "", Revision = 0, Bytes = new byte[0] });
            IffNet.ToPeers(ServerSession.IffPeers.Where(p => p != except).ToList(), IffNet.WishlistOf, relay);
        }

        private static ZPackage Build(long pid, Stored stored) {
            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(pid);
            pkg.Write(stored.Name ?? "");
            pkg.Write(stored.Revision);
            pkg.Write(stored.Bytes ?? new byte[0]);
            return pkg;
        }

        internal static void SendAllTo(ServerPeer peer) {
            foreach (KeyValuePair<long, Stored> pair in roster) {
                if (pair.Key == peer.Pid) { continue; }
                IffNet.ToPeer(peer, IffNet.WishlistOf, Build(pair.Key, pair.Value));
            }
        }

        internal static void OnPeerLeft(ServerPeer peer) {
            if (peer.Verified) { RemoveAndAnnounce(peer.Pid, peer); }
        }

        internal static int Count => roster.Count;

        internal static void Reset() => roster.Clear();
    }
}
