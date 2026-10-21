using System.Collections.Generic;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // This client's side of the handshake: whether the server runs this mod, which features it turned on
    // and with what limits, and the directory of connected players.
    //
    // Hello goes out once per connection, as soon as the local character has an id. A server without the
    // mod never answers, and everything here stays off.
    internal static class ClientSession {
        internal sealed class DirEntry {
            internal long Uid;
            internal long Pid;
            internal string Name;
            internal bool HasIff;
            internal bool Shares;
        }

        private static readonly Dictionary<long, DirEntry> byPid = new Dictionary<long, DirEntry>();
        private static readonly Dictionary<long, DirEntry> byUid = new Dictionary<long, DirEntry>();

        private static bool helloSent;
        private static float helloSentAt;

        internal static bool Welcomed { get; private set; }
        internal static string ServerVersion { get; private set; } = "";
        internal static byte Features { get; private set; }
        internal static int RollSeconds { get; private set; } = 30;
        internal static float ReserveSeconds { get; private set; } = 300f;
        internal static int MaxRollMembers { get; private set; } = 10;
        internal static float MaxRequesterDistance { get; private set; } = 100f;
        internal static float StartGraceSeconds { get; private set; } = ServerSession.StartGraceSeconds;
        internal static int MaxWishlistEntries { get; private set; } = 100;
        // This client's player id as the server verified it; 0 until then.
        internal static long VerifiedPid { get; private set; }

        internal static bool RollsEnabled => Welcomed && (Features & ServerSession.FeatureRolls) != 0;
        internal static bool WishlistsEnabled => Welcomed && (Features & ServerSession.FeatureWishlists) != 0;

        internal static IEnumerable<DirEntry> Directory => byPid.Values;

        internal static string Status {
            get {
                if (ZNet.instance == null) { return "not in a world"; }
                if (Welcomed) { return $"server runs {ItemFavoriteFramework.PluginName} {ServerVersion}"; }
                if (helloSent == false) { return "waiting for the character to load"; }
                return Time.realtimeSinceStartup - helloSentAt > 10f ? "the server does not run this mod (no answer)" : "waiting for the server";
            }
        }

        internal static void Tick(float now) {
            if (helloSent || ZNet.instance == null) { return; }
            Player local = Player.m_localPlayer;
            if (local == null || local.GetPlayerID() == 0) { return; }
            if (ZNet.instance.IsServer() == false && ZNet.instance.GetServerRPC() == null) { return; }

            ZPackage pkg = IffNet.NewPackage();
            pkg.Write(ItemFavoriteFramework.PluginVersion);
            pkg.Write(local.GetPlayerID());
            pkg.Write(LootConfig.ShareWishlist.Value);
            helloSent = true;
            helloSentAt = now;
            IffNet.ToServer(IffNet.Hello, pkg);
        }

        internal static void OnWelcome(ZPackage pkg) {
            bool wasSharing = WishlistsEnabled;
            ServerVersion = pkg.ReadString();
            Features = pkg.ReadByte();
            RollSeconds = pkg.ReadInt();
            ReserveSeconds = pkg.ReadSingle();
            MaxRollMembers = pkg.ReadInt();
            MaxRequesterDistance = pkg.ReadSingle();
            StartGraceSeconds = pkg.ReadSingle();
            MaxWishlistEntries = pkg.ReadInt();
            VerifiedPid = pkg.ReadLong();
            bool first = Welcomed == false;
            Welcomed = true;
            if (first) { Logger.LogInfo($"The server runs {ItemFavoriteFramework.PluginName} {ServerVersion}: loot rolls {(RollsEnabled ? "on" : "off")}, wishlist sharing {(WishlistsEnabled ? "on" : "off")}."); }
            if (WishlistsEnabled == false) { WishlistRoster.Reset(); }
            // Later Welcomes (a changed server setting, this player's id verified) need no re-upload, unless
            // the server just turned sharing back on and so has no lists.
            if (first || (wasSharing == false && WishlistsEnabled)) { WishlistSync.ForceResend(); }
        }

        internal static void OnDirectory(ZPackage pkg) {
            byPid.Clear();
            byUid.Clear();
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++) {
                DirEntry entry = new DirEntry {
                    Uid = pkg.ReadLong(),
                    Pid = pkg.ReadLong(),
                    Name = pkg.ReadString(),
                };
                byte flags = pkg.ReadByte();
                entry.HasIff = (flags & 1) != 0;
                entry.Shares = (flags & 2) != 0;
                byPid[entry.Pid] = entry;
                byUid[entry.Uid] = entry;
            }
        }

        internal static void OnWishlistOf(ZPackage pkg) {
            long pid = pkg.ReadLong();
            string name = pkg.ReadString();
            int revision = pkg.ReadInt();
            byte[] bytes = pkg.ReadByteArray();
            WishlistRoster.Apply(pid, name, revision, bytes);
        }

        internal static string NameOf(long pid) {
            if (byPid.TryGetValue(pid, out DirEntry entry) && string.IsNullOrEmpty(entry.Name) == false) { return entry.Name; }
            if (Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == pid) { return Player.m_localPlayer.GetPlayerName(); }
            return pid.ToString();
        }

        internal static long PidOfUid(long uid) => byUid.TryGetValue(uid, out DirEntry entry) ? entry.Pid : 0;

        internal static long PidOfName(string name) {
            foreach (DirEntry entry in byPid.Values) {
                if (string.Equals(entry.Name, name, System.StringComparison.OrdinalIgnoreCase)) { return entry.Pid; }
            }
            return 0;
        }

        internal static bool TryGetEntry(long pid, out DirEntry entry) => byPid.TryGetValue(pid, out entry);

        internal static void Reset() {
            byPid.Clear();
            byUid.Clear();
            helloSent = false;
            Welcomed = false;
            ServerVersion = "";
            Features = 0;
            VerifiedPid = 0;
        }
    }
}
