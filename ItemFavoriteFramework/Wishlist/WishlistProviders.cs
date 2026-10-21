using System;
using System.Collections.Generic;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // A mod that adds conditions to wishlist entries beyond "this item" -- EpicLoot's rarity, legendary,
    // set and effects, for instance.
    //
    // Matching must be a pure function of (item, criteria json): IFF evaluates other players' entries on
    // this machine to show who wants an item, so a provider must not read the local player, the time or
    // anything random. Every call is wrapped; a provider that keeps throwing is switched off for the session.
    internal sealed class WishlistProvider {
        internal string Id;
        // (prefab name) -> schema json, or "" when the provider has nothing to offer for that item.
        internal Func<string, string> GetSchema;
        internal Func<ItemDrop.ItemData, string, bool> Matches;
        // (criteria json) -> a short summary, e.g. "Epic or better".
        internal Func<string, string> Describe;
        // (item) -> criteria json describing this specific item, or "" for none. Optional.
        internal Func<ItemDrop.ItemData, string> Suggest;
        internal int Failures;
        internal bool Disabled;
    }

    internal static class WishlistProviders {
        private const int MaxFailures = 5;

        private static readonly Dictionary<string, WishlistProvider> providers = new Dictionary<string, WishlistProvider>(StringComparer.Ordinal);
        private static readonly List<WishlistProvider> ordered = new List<WishlistProvider>();

        // Bumped whenever the set of providers changes, so cached matches are recomputed.
        internal static int Revision { get; private set; }

        internal static IEnumerable<WishlistProvider> All => ordered;

        internal static bool Register(string id, Func<string, string> getSchema, Func<ItemDrop.ItemData, string, bool> matches,
            Func<string, string> describe, Func<ItemDrop.ItemData, string> suggest) {
            if (string.IsNullOrEmpty(id) || id.Length > WishlistData.MaxProviderLength || matches == null) { return false; }
            Unregister(id);
            WishlistProvider provider = new WishlistProvider {
                Id = id, GetSchema = getSchema, Matches = matches, Describe = describe, Suggest = suggest,
            };
            providers[id] = provider;
            ordered.Add(provider);
            Revision++;
            Wishlists.Touch();
            return true;
        }

        internal static bool Unregister(string id) {
            if (id == null || providers.TryGetValue(id, out WishlistProvider existing) == false) { return false; }
            providers.Remove(id);
            ordered.Remove(existing);
            Revision++;
            Wishlists.Touch();
            return true;
        }

        internal static bool TryGet(string id, out WishlistProvider provider) {
            provider = null;
            return id != null && providers.TryGetValue(id, out provider) && provider.Disabled == false;
        }

        internal static bool IsRegistered(string id) => TryGet(id, out _);

        // False for an unknown or disabled provider: a condition nobody here can check never matches.
        // countFailures is false for other players' entries: criteria another machine wrote are not this
        // provider's fault, and one bad shared entry must not switch the provider off for everyone.
        internal static bool Match(string providerId, ItemDrop.ItemData item, string json, bool countFailures = true) {
            if (TryGet(providerId, out WishlistProvider provider) == false) { return false; }
            try {
                return provider.Matches(item, json);
            } catch (Exception e) {
                if (countFailures) {
                    Fail(provider, "matches", e);
                } else {
                    Logger.LogDebug($"Wishlist provider '{provider.Id}' threw on a shared entry: {e.Message}");
                }
                return false;
            }
        }

        internal static string Schema(WishlistProvider provider, string prefab) {
            if (provider.Disabled || provider.GetSchema == null) { return ""; }
            try {
                return provider.GetSchema(prefab) ?? "";
            } catch (Exception e) {
                Fail(provider, "schema", e);
                return "";
            }
        }

        internal static string Describe(string providerId, string json) {
            if (TryGet(providerId, out WishlistProvider provider) == false || provider.Describe == null) { return ""; }
            try {
                return provider.Describe(json) ?? "";
            } catch (Exception e) {
                Fail(provider, "describe", e);
                return "";
            }
        }

        internal static string Suggest(WishlistProvider provider, ItemDrop.ItemData item) {
            if (provider.Disabled || provider.Suggest == null) { return ""; }
            try {
                return provider.Suggest(item) ?? "";
            } catch (Exception e) {
                Fail(provider, "suggest", e);
                return "";
            }
        }

        private static void Fail(WishlistProvider provider, string what, Exception e) {
            provider.Failures++;
            if (provider.Failures == 1) {
                Logger.LogError($"Wishlist provider '{provider.Id}' threw in {what}: {e}");
            }
            if (provider.Failures >= MaxFailures && provider.Disabled == false) {
                provider.Disabled = true;
                Revision++;
                Wishlists.Touch();
                Logger.LogError($"Wishlist provider '{provider.Id}' failed {MaxFailures} times and is switched off until the game restarts.");
            }
        }
    }
}
