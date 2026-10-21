using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    internal enum WishResult { Added, Removed, Changed, Unchanged, NotReady, ReadOnly, InvalidItem, Full }

    // The wishlist rules, for the local player's list and for the lists other players share.
    //
    // An entry matches an item when the item's prefab name is the entry's, and every condition on the entry
    // is met according to the provider that owns it (a condition whose provider is not installed here never
    // matches). Lookups run every frame for every visible cell, so results are cached per item and only
    // recomputed when the item, any list, or the set of providers changes.
    internal static class Wishlists {
        private sealed class Cached {
            internal int Revision;
            internal int Fingerprint;
            internal WishlistEntry Local;
            internal List<long> WantedBy;
        }

        private static readonly ConditionalWeakTable<ItemDrop.ItemData, Cached> cache = new ConditionalWeakTable<ItemDrop.ItemData, Cached>();
        private static readonly List<long> noOne = new List<long>();

        private static int revision = 1;
        private static int prefabsRevision;
        private static readonly HashSet<string> wantedPrefabs = new HashSet<string>(StringComparer.Ordinal);

        // Any list, provider or character changed: every cached answer is stale.
        internal static void Touch() => revision++;

        internal static int Revision => revision;

        internal static string PrefabName(ItemDrop.ItemData item) {
            if (item == null) { return null; }
            if (item.m_dropPrefab != null) { return item.m_dropPrefab.name; }
            if (item.m_shared != null && ObjectDB.instance != null && ObjectDB.instance.TryGetItemPrefab(item.m_shared, out GameObject prefab)) {
                return prefab.name;
            }
            return null;
        }

        internal static string DisplayName(string prefab) {
            if (string.IsNullOrEmpty(prefab)) { return ""; }
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null) { return prefab; }
            return Localization.instance != null ? Localization.instance.Localize(drop.m_itemData.m_shared.m_name) : drop.m_itemData.m_shared.m_name;
        }

        // ownEntry: false for another player's shared entry, whose provider failures are not counted.
        internal static bool EntryMatches(WishlistEntry entry, ItemDrop.ItemData item, string prefab, bool ownEntry = true) {
            if (entry == null || string.Equals(entry.Prefab, prefab, StringComparison.Ordinal) == false) { return false; }
            foreach (WishlistCriterion criterion in entry.Criteria) {
                if (WishlistProviders.Match(criterion.Provider, item, criterion.Json, ownEntry) == false) { return false; }
            }
            return true;
        }

        // -----------------------------------------------------------------------------------------
        // Queries (cached)
        // -----------------------------------------------------------------------------------------

        internal static bool IsWishlisted(ItemDrop.ItemData item) => LocalMatch(item) != null;

        internal static WishlistEntry LocalMatch(ItemDrop.ItemData item) {
            Cached cached = Lookup(item);
            return cached?.Local;
        }

        // Ids of the other players whose shared wishlist wants this item.
        internal static List<long> WantedBy(ItemDrop.ItemData item) {
            Cached cached = Lookup(item);
            return cached?.WantedBy ?? noOne;
        }

        internal static List<string> WantedByNames(ItemDrop.ItemData item) {
            List<string> names = new List<string>();
            foreach (long pid in WantedBy(item)) { names.Add(WishlistRoster.NameOf(pid)); }
            return names;
        }

        private static Cached Lookup(ItemDrop.ItemData item) {
            if (Marks.IsValid(item) == false) { return null; }
            RefreshPrefabs();
            string prefab = PrefabName(item);
            if (prefab == null || wantedPrefabs.Contains(prefab) == false) { return null; }

            int fingerprint = Fingerprint(item);
            if (cache.TryGetValue(item, out Cached cached) && cached.Revision == revision && cached.Fingerprint == fingerprint) {
                return cached;
            }
            if (cached == null) {
                cached = new Cached();
                cache.Add(item, cached);
            }
            cached.Revision = revision;
            cached.Fingerprint = fingerprint;
            cached.Local = null;
            cached.WantedBy = null;

            if (WishlistStore.TryGet(out WishlistData data)) {
                foreach (WishlistEntry entry in data.Entries) {
                    if (EntryMatches(entry, item, prefab)) {
                        cached.Local = entry;
                        break;
                    }
                }
            }
            foreach (WishlistRoster.Shared shared in WishlistRoster.All) {
                if (shared.Pid == WishlistStore.PlayerId) { continue; }
                foreach (WishlistEntry entry in shared.Data.Entries) {
                    if (EntryMatches(entry, item, prefab, ownEntry: false)) {
                        if (cached.WantedBy == null) { cached.WantedBy = new List<long>(); }
                        cached.WantedBy.Add(shared.Pid);
                        break;
                    }
                }
            }
            return cached;
        }

        // Every prefab any list mentions. Most items are not on any list, and this one set lookup is all
        // they ever cost.
        private static void RefreshPrefabs() {
            WishlistStore.TryGet(out WishlistData data);
            if (prefabsRevision == revision) { return; }
            prefabsRevision = revision;
            wantedPrefabs.Clear();
            if (data != null) {
                foreach (WishlistEntry entry in data.Entries) { wantedPrefabs.Add(entry.Prefab); }
            }
            foreach (WishlistRoster.Shared shared in WishlistRoster.All) {
                foreach (WishlistEntry entry in shared.Data.Entries) { wantedPrefabs.Add(entry.Prefab); }
            }
        }

        // Changes when anything a provider may look at changes. EpicLoot replaces its data string when an
        // item is enchanted, and a ground item that reloads from its ZDO gets a new custom-data dictionary,
        // so reference hashes of the values catch both without reading them.
        private static int Fingerprint(ItemDrop.ItemData item) {
            unchecked {
                int h = RuntimeHelpers.GetHashCode(item.m_shared);
                h = h * 31 + item.m_quality;
                h = h * 31 + item.m_variant;
                h = h * 31 + RuntimeHelpers.GetHashCode(item.m_customData);
                h = h * 31 + item.m_customData.Count;
                foreach (KeyValuePair<string, string> pair in item.m_customData) {
                    h ^= RuntimeHelpers.GetHashCode(pair.Value) * 397;
                }
                return h;
            }
        }

        // -----------------------------------------------------------------------------------------
        // Changes to the local list
        // -----------------------------------------------------------------------------------------

        internal static WishResult Toggle(ItemDrop.ItemData item) {
            if (Marks.IsValid(item) == false) { return WishResult.InvalidItem; }
            if (WishlistStore.TryGet(out WishlistData data) == false) { return WishResult.NotReady; }
            if (WishlistStore.ReadOnly) { return WishResult.ReadOnly; }

            string prefab = PrefabName(item);
            if (prefab == null) { return WishResult.InvalidItem; }
            int removed = data.Entries.RemoveAll(e => EntryMatches(e, item, prefab));
            if (removed > 0) {
                WishlistStore.Commit();
                return WishResult.Removed;
            }
            return AddFromItem(item);
        }

        // The item as an entry: its prefab, plus whatever each provider suggests for this specific item
        // (for EpicLoot, its rarity, or its legendary).
        internal static WishResult AddFromItem(ItemDrop.ItemData item) {
            if (Marks.IsValid(item) == false) { return WishResult.InvalidItem; }
            string prefab = PrefabName(item);
            if (prefab == null) { return WishResult.InvalidItem; }
            List<WishlistCriterion> criteria = new List<WishlistCriterion>();
            foreach (WishlistProvider provider in WishlistProviders.All) {
                if (criteria.Count >= WishlistData.MaxCriteria) { break; }
                string json = WishlistProviders.Suggest(provider, item);
                if (string.IsNullOrEmpty(json)) { continue; }
                criteria.Add(MakeCriterion(provider.Id, json));
            }
            return Add(prefab, criteria, autoNeed: false);
        }

        internal static WishlistCriterion MakeCriterion(string providerId, string json) {
            string desc = WishlistProviders.Describe(providerId, json);
            if (string.IsNullOrEmpty(desc)) { desc = json.Length > 60 ? json.Substring(0, 60) + "..." : json; }
            if (desc.Length > WishlistData.MaxDescLength) { desc = desc.Substring(0, WishlistData.MaxDescLength); }
            return new WishlistCriterion { Provider = providerId, Json = json, Desc = desc };
        }

        internal static WishResult Add(string prefab, List<WishlistCriterion> criteria, bool autoNeed) {
            if (string.IsNullOrEmpty(prefab) || prefab.Length > WishlistData.MaxPrefabLength) { return WishResult.InvalidItem; }
            if (WishlistStore.TryGet(out WishlistData data) == false) { return WishResult.NotReady; }
            if (WishlistStore.ReadOnly) { return WishResult.ReadOnly; }
            criteria = criteria ?? new List<WishlistCriterion>();
            if (criteria.Count > WishlistData.MaxCriteria) { return WishResult.InvalidItem; }
            foreach (WishlistCriterion criterion in criteria) {
                if (string.IsNullOrEmpty(criterion.Provider) || criterion.Provider.Length > WishlistData.MaxProviderLength
                    || (criterion.Json ?? "").Length > WishlistData.MaxCriterionJson) {
                    return WishResult.InvalidItem;
                }
            }
            foreach (WishlistEntry existing in data.Entries) {
                if (SameEntry(existing, prefab, criteria)) { return WishResult.Unchanged; }
            }
            if (data.Entries.Count >= WishlistData.MaxEntries) { return WishResult.Full; }

            WishlistEntry entry = new WishlistEntry { Id = data.NextId++, Prefab = prefab, AutoNeed = autoNeed };
            entry.Criteria.AddRange(criteria);
            data.Entries.Add(entry);
            WishlistStore.Commit();
            return WishResult.Added;
        }

        private static bool SameEntry(WishlistEntry entry, string prefab, List<WishlistCriterion> criteria) {
            if (entry.Prefab != prefab || entry.Criteria.Count != criteria.Count) { return false; }
            for (int i = 0; i < criteria.Count; i++) {
                if (entry.Criteria[i].Provider != criteria[i].Provider || entry.Criteria[i].Json != criteria[i].Json) { return false; }
            }
            return true;
        }

        // Replaces an entry in place, keeping its id (the editor's save).
        internal static WishResult Update(WishlistEntry edited) {
            if (WishlistStore.TryGet(out WishlistData data) == false) { return WishResult.NotReady; }
            if (WishlistStore.ReadOnly) { return WishResult.ReadOnly; }
            int index = data.Entries.FindIndex(e => e.Id == edited.Id);
            if (index < 0) { return WishResult.InvalidItem; }
            data.Entries[index] = edited;
            WishlistStore.Commit();
            return WishResult.Changed;
        }

        internal static bool Remove(int id) {
            if (WishlistStore.TryGet(out WishlistData data) == false || WishlistStore.ReadOnly) { return false; }
            if (data.Entries.RemoveAll(e => e.Id == id) == 0) { return false; }
            WishlistStore.Commit();
            return true;
        }

        internal static int RemoveByPrefab(string prefab) {
            if (WishlistStore.TryGet(out WishlistData data) == false || WishlistStore.ReadOnly) { return 0; }
            int removed = data.Entries.RemoveAll(e => string.Equals(e.Prefab, prefab, StringComparison.Ordinal));
            if (removed > 0) { WishlistStore.Commit(); }
            return removed;
        }

        internal static bool Clear() {
            if (WishlistStore.TryGet(out WishlistData data) == false || WishlistStore.ReadOnly) { return false; }
            if (data.Entries.Count == 0) { return true; }
            data.Entries.Clear();
            return WishlistStore.Commit();
        }

        // -----------------------------------------------------------------------------------------
        // JSON (API)
        // -----------------------------------------------------------------------------------------

        internal static string ToJson(WishlistData data) {
            JsonBuilder json = new JsonBuilder().BeginArray();
            if (data != null) {
                foreach (WishlistEntry entry in data.Entries) { AppendEntry(json, entry); }
            }
            return json.EndArray().ToString();
        }

        internal static void AppendEntry(JsonBuilder json, WishlistEntry entry) {
            json.BeginObject()
                .Num("id", entry.Id)
                .Str("prefab", entry.Prefab)
                .Str("name", DisplayName(entry.Prefab))
                .Bool("autoNeed", entry.AutoNeed)
                .BeginArray("criteria");
            foreach (WishlistCriterion criterion in entry.Criteria) {
                json.BeginObject().Str("provider", criterion.Provider).Str("json", criterion.Json).Str("desc", criterion.Desc).EndObject();
            }
            json.EndArray().EndObject();
        }

        // {"providerId": {criteria}, ...} -> one condition per provider. Unknown providers are kept: the
        // entry then simply never matches until that mod is installed.
        internal static bool TryParseCriteria(string criteriaJson, out List<WishlistCriterion> criteria) {
            criteria = new List<WishlistCriterion>();
            if (string.IsNullOrEmpty(criteriaJson)) { return true; }
            IDictionary<string, object> obj = Json.ParseObject(criteriaJson);
            if (obj == null) { return false; }
            foreach (KeyValuePair<string, object> pair in obj) {
                string json = pair.Value is string s ? s : SimpleJson.SimpleJson.SerializeObject(pair.Value);
                criteria.Add(MakeCriterion(pair.Key, json));
            }
            return criteria.Count <= WishlistData.MaxCriteria;
        }

        internal static string Describe(WishlistEntry entry) {
            string name = DisplayName(entry.Prefab);
            if (entry.Criteria.Count == 0) { return name; }
            List<string> parts = new List<string>();
            foreach (WishlistCriterion criterion in entry.Criteria) { parts.Add(criterion.Desc); }
            return $"{name} ({string.Join(", ", parts)})";
        }
    }
}
