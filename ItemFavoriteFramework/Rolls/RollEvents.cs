using System;
using System.Collections.Generic;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Roll events for other mods (API 3), as JSON. Every event has "event" and "side": "server" events are
    // raised on the server (dedicated or listen host) for every roll; "client" events on the clients a roll
    // concerns -- the requester and its members. A listen host sees both sides.
    internal static class RollEvents {
        private static readonly List<Action<string>> listeners = new List<Action<string>>();

        internal static void Add(Action<string> listener) {
            if (listener != null && listeners.Contains(listener) == false) { listeners.Add(listener); }
        }

        internal static void Remove(Action<string> listener) => listeners.Remove(listener);

        private static void Raise(JsonBuilder json) {
            if (listeners.Count == 0) { return; }
            string text = json.EndObject().ToString();
            foreach (Action<string> listener in listeners.ToArray()) {
                try {
                    listener(text);
                } catch (Exception e) {
                    Logger.LogError($"A roll listener ({listener.Method.DeclaringType?.FullName}.{listener.Method.Name}) threw: {e}");
                }
            }
        }

        private static JsonBuilder Begin(string name, string side, string rollId) {
            return new JsonBuilder().BeginObject().Str("event", name).Str("side", side).Str("rollId", rollId ?? "");
        }

        internal static void RaiseAccepted(string rollId, string side) {
            if (listeners.Count == 0) { return; }
            Raise(Begin("accepted", side, rollId));
        }

        internal static void RaiseRefused(string rollId, string reason, string side) {
            if (listeners.Count == 0) { return; }
            Raise(Begin("refused", side, rollId).Str("reason", reason ?? ""));
        }

        internal static void RaisePrompt(ClientRoll roll) {
            if (listeners.Count == 0) { return; }
            JsonBuilder json = Begin("prompt", "client", roll.RollId)
                .Str("prefab", roll.Prefab)
                .Str("name", roll.Name)
                .Str("label", roll.Label)
                .Str("groupId", roll.GroupId)
                .Num("seconds", roll.SecondsLeft)
                .Bool("wishlisted", roll.Wishlisted)
                .BeginArray("members");
            foreach (long pid in roll.Members) { json.Str(null, pid.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            Raise(json.EndArray());
        }

        internal static void RaiseVote(string rollId, long voter, int voted, int of, string side) {
            if (listeners.Count == 0) { return; }
            Raise(Begin("vote", side, rollId).Id("player", voter).Num("voted", voted).Num("of", of));
        }

        internal static void RaiseResult(string rollId, string side, RollOutcome outcome, long winner, string winnerName, float reserveSeconds,
            string prefab, string label, string groupId, IList<RollTally> tallies, Func<long, string> nameOf, IList<long> notEligible) {
            if (listeners.Count == 0) { return; }
            JsonBuilder json = Begin("result", side, rollId)
                .Str("outcome", RollIds.OutcomeName(outcome))
                .Id("winner", winner)
                .Str("winnerName", winnerName ?? "")
                .Num("reserveSeconds", reserveSeconds)
                .Str("prefab", prefab ?? "")
                .Str("label", label ?? "")
                .Str("groupId", groupId ?? "")
                .BeginArray("rolls");
            foreach (RollTally tally in tallies) {
                json.BeginObject()
                    .Id("player", tally.Pid)
                    .Str("name", nameOf(tally.Pid))
                    .Str("choice", RollIds.ChoiceName(tally.Choice))
                    .Num("roll", tally.Roll)
                    .Num("rerolls", tally.Rerolls)
                    .EndObject();
            }
            json.EndArray().BeginArray("notEligible");
            if (notEligible != null) {
                foreach (long pid in notEligible) { json.Str(null, pid.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            }
            Raise(json.EndArray());
        }

        internal static void RaiseLock(RollLocks.Entry entry, float secondsLeft, string side) {
            if (listeners.Count == 0) { return; }
            Raise(Begin("lock", side, RollIds.Format(entry.Drop, entry.Nonce))
                .Str("phase", RollIds.PhaseName(entry.Phase))
                .Id("winner", entry.WinnerPid)
                .Num("secondsLeft", secondsLeft));
        }

        internal static void RaiseLock(ZDOID drop, long nonce, LockPhase phase, long winner, float secondsLeft, string side) {
            if (listeners.Count == 0) { return; }
            Raise(Begin("lock", side, RollIds.Format(drop, nonce))
                .Str("phase", RollIds.PhaseName(phase))
                .Id("winner", winner)
                .Num("secondsLeft", secondsLeft));
        }
    }

    // How items are named in roll windows, notices and chat. Mods that rename items (EpicLoot's magic item
    // names) can supply the name; the first non-empty answer wins.
    internal static class ItemNames {
        private static readonly List<Func<ItemDrop.ItemData, string>> providers = new List<Func<ItemDrop.ItemData, string>>();

        internal static void Add(Func<ItemDrop.ItemData, string> provider) {
            if (provider != null && providers.Contains(provider) == false) { providers.Add(provider); }
        }

        internal static void Remove(Func<ItemDrop.ItemData, string> provider) => providers.Remove(provider);

        internal static string Of(ItemDrop.ItemData item) {
            if (item == null || item.m_shared == null) { return "?"; }
            foreach (Func<ItemDrop.ItemData, string> provider in providers.ToArray()) {
                try {
                    string name = provider(item);
                    if (string.IsNullOrEmpty(name) == false) { return name; }
                } catch (Exception e) {
                    Logger.LogDebug($"An item name provider threw: {e.Message}");
                }
            }
            string plain = Localization.instance != null ? Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name;
            if (item.m_quality > 1) { plain += $" ({item.m_quality})"; }
            if (item.m_stack > 1) { plain += $" x{item.m_stack}"; }
            return plain;
        }
    }
}
