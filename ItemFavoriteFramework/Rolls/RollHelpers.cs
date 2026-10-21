using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Ways for a caller to build a roll's member list. IFF never decides who is eligible; these only help.
    internal static class RollHelpers {

        // Players within radius of a point. On a server (which has no Player objects for remote players)
        // from each connected player's character position; on a client from the players loaded around it.
        // Nearest first, so a roll that only takes MaxRollMembers players keeps the closest.
        internal static List<long> PlayersNear(Vector3 point, float radius) {
            Dictionary<long, float> distances = new Dictionary<long, float>();
            if (ZNet.instance == null) { return new List<long>(); }
            if (ZNet.instance.IsServer()) {
                foreach (ServerPeer peer in ServerSession.All) {
                    if (peer.Verified && ServerSession.TryGetPosition(peer, out Vector3 at)) { Keep(distances, peer.Pid, Vector3.Distance(at, point), radius); }
                }
            }
            List<Player> players = new List<Player>();
            Player.GetPlayersInRange(point, radius, players);
            foreach (Player player in players) { Keep(distances, player.GetPlayerID(), Vector3.Distance(player.transform.position, point), radius); }
            return distances.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToList();
        }

        private static void Keep(Dictionary<long, float> distances, long pid, float distance, float radius) {
            if (pid == 0 || distance > radius) { return; }
            if (distances.TryGetValue(pid, out float known) == false || distance < known) { distances[pid] = distance; }
        }

        // Players who hit the character, from the flags vanilla keeps on its ZDO -- one per attacking player,
        // keyed by the player's name. Only the character's owner has them for sure, and only until the
        // character is destroyed at the end of Character.OnDeath, so call it from the owner, before that.
        internal static List<long> Damagers(Character character) {
            List<long> ids = new List<long>();
            ZNetView nview = character != null ? character.m_nview : null;
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo == null || ZNet.instance == null) { return ids; }

            string prefix = ZDOVars.s_attackers.ToString();
            foreach (KeyValuePair<string, long> candidate in Candidates()) {
                if (zdo.GetBool(prefix + candidate.Key)) { Add(ids, candidate.Value); }
            }
            return ids;
        }

        // Every player name this machine can map to an id.
        private static Dictionary<string, long> Candidates() {
            Dictionary<string, long> byName = new Dictionary<string, long>();
            foreach (ClientSession.DirEntry entry in ClientSession.Directory) {
                if (string.IsNullOrEmpty(entry.Name) == false) { byName[entry.Name] = entry.Pid; }
            }
            if (ZNet.instance.IsServer()) {
                foreach (ServerPeer peer in ServerSession.All) {
                    if (peer.Verified && string.IsNullOrEmpty(peer.Name) == false) { byName[peer.Name] = peer.Pid; }
                }
            }
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList()) {
                if (string.IsNullOrEmpty(info.m_name) || byName.ContainsKey(info.m_name)) { continue; }
                ZDO character = ZDOMan.instance?.GetZDO(info.m_characterID);
                long pid = character != null ? character.GetLong(ZDOVars.s_playerID) : 0L;
                if (pid != 0) { byName[info.m_name] = pid; }
            }
            foreach (Player player in Player.GetAllPlayers()) {
                string name = player.GetPlayerName();
                if (string.IsNullOrEmpty(name) == false && byName.ContainsKey(name) == false && player.GetPlayerID() != 0) {
                    byName[name] = player.GetPlayerID();
                }
            }
            return byName;
        }

        private static void Add(List<long> ids, long pid) {
            if (pid != 0 && ids.Contains(pid) == false) { ids.Add(pid); }
        }

        internal static string ToJson(List<long> ids) {
            JsonBuilder json = new JsonBuilder().BeginArray();
            foreach (long id in ids) { json.Str(null, id.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            return json.EndArray().ToString();
        }
    }
}
