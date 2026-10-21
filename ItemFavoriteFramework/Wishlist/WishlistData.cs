using System;
using System.Collections.Generic;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // One condition a provider (another mod, e.g. EpicLoot) checks on top of the base item. Json is the
    // provider's own criteria, Desc the human-readable summary its describe function wrote when the entry
    // was made -- kept so other players can read the entry even without that provider or that language.
    internal sealed class WishlistCriterion {
        internal string Provider = "";
        internal string Json = "";
        internal string Desc = "";

        internal WishlistCriterion Clone() => new WishlistCriterion { Provider = Provider, Json = Json, Desc = Desc };
    }

    internal sealed class WishlistEntry {
        internal int Id;
        // The item's prefab name (m_dropPrefab.name): stable, unlocalized, the same on every machine.
        internal string Prefab = "";
        // Vote Need automatically when a roll offers a matching item (if the player enabled that).
        internal bool AutoNeed;
        internal readonly List<WishlistCriterion> Criteria = new List<WishlistCriterion>();

        internal WishlistEntry Clone() {
            WishlistEntry copy = new WishlistEntry { Id = Id, Prefab = Prefab, AutoNeed = AutoNeed };
            foreach (WishlistCriterion criterion in Criteria) { copy.Criteria.Add(criterion.Clone()); }
            return copy;
        }
    }

    // A character's wishlist. Saved in Player.m_customData under its own key with its own version, apart
    // from the favorites (MarkData), so a change here never makes an older build refuse the favorites.
    //
    // The same bytes are what a client uploads to the server; the server parses them with the limits below
    // and re-serializes the cleaned copy before relaying it, so nothing malformed reaches other players.
    internal sealed class WishlistData {
        internal const int CurrentVersion = 1;

        internal const int MaxEntries = 500;
        internal const int MaxCriteria = 4;
        internal const int MaxPrefabLength = 64;
        internal const int MaxProviderLength = 64;
        internal const int MaxCriterionJson = 1024;
        internal const int MaxDescLength = 128;
        internal const int MaxBytes = 16 * 1024;

        private const byte FlagAutoNeed = 1;

        internal int Revision;
        internal int NextId = 1;
        internal readonly List<WishlistEntry> Entries = new List<WishlistEntry>();

        internal byte[] ToBytes() {
            ZPackage pkg = new ZPackage();
            Write(pkg);
            return pkg.GetArray();
        }

        internal string ToBase64() {
            ZPackage pkg = new ZPackage();
            Write(pkg);
            return pkg.GetBase64();
        }

        private void Write(ZPackage pkg) {
            pkg.Write(CurrentVersion);
            pkg.Write(Revision);
            pkg.Write(NextId);
            pkg.Write(Entries.Count);
            foreach (WishlistEntry entry in Entries) {
                pkg.Write(entry.Id);
                pkg.Write(entry.Prefab ?? "");
                pkg.Write(entry.AutoNeed ? FlagAutoNeed : (byte)0);
                pkg.Write((byte)entry.Criteria.Count);
                foreach (WishlistCriterion criterion in entry.Criteria) {
                    pkg.Write(criterion.Provider ?? "");
                    pkg.Write(criterion.Json ?? "");
                    pkg.Write(criterion.Desc ?? "");
                }
            }
        }

        internal static bool TryParseBase64(string base64, out WishlistData data, out string error) {
            data = null;
            try {
                return TryParse(new ZPackage(base64), int.MaxValue, out data, out error);
            } catch (Exception e) {
                error = $"unreadable ({e.GetType().Name}: {e.Message})";
                return false;
            }
        }

        // maxEntries: the server's relay limit; entries past it are dropped rather than refusing the list.
        // Oversized strings fail the parse: they never come from an honest client.
        internal static bool TryParse(byte[] bytes, int maxEntries, out WishlistData data, out string error) {
            data = null;
            if (bytes == null) {
                error = "empty";
                return false;
            }
            if (bytes.Length > MaxBytes) {
                error = $"{bytes.Length} bytes, more than {MaxBytes}";
                return false;
            }
            return TryParse(new ZPackage(bytes), maxEntries, out data, out error);
        }

        private static bool TryParse(ZPackage pkg, int maxEntries, out WishlistData data, out string error) {
            data = null;
            error = null;
            try {
                int version = pkg.ReadInt();
                if (version > CurrentVersion) {
                    error = $"saved by a newer version of {ItemFavoriteFramework.PluginName} (wishlist version {version}, this version reads {CurrentVersion})";
                    return false;
                }
                if (version < 1) {
                    error = $"unknown wishlist version {version}";
                    return false;
                }

                WishlistData parsed = new WishlistData {
                    Revision = pkg.ReadInt(),
                    NextId = pkg.ReadInt(),
                };
                int count = pkg.ReadInt();
                if (count < 0 || count > MaxEntries) {
                    error = $"{count} entries";
                    return false;
                }
                for (int i = 0; i < count; i++) {
                    WishlistEntry entry = new WishlistEntry {
                        Id = pkg.ReadInt(),
                        Prefab = pkg.ReadString(),
                    };
                    entry.AutoNeed = (pkg.ReadByte() & FlagAutoNeed) != 0;
                    int criteria = pkg.ReadByte();
                    if (criteria > MaxCriteria) {
                        error = $"an entry with {criteria} conditions";
                        return false;
                    }
                    for (int c = 0; c < criteria; c++) {
                        WishlistCriterion criterion = new WishlistCriterion {
                            Provider = pkg.ReadString(),
                            Json = pkg.ReadString(),
                            Desc = pkg.ReadString(),
                        };
                        if (criterion.Provider.Length > MaxProviderLength || criterion.Json.Length > MaxCriterionJson || criterion.Desc.Length > MaxDescLength) {
                            error = "an oversized condition";
                            return false;
                        }
                        entry.Criteria.Add(criterion);
                    }
                    if (string.IsNullOrEmpty(entry.Prefab) || entry.Prefab.Length > MaxPrefabLength) {
                        error = "an entry with no item, or an oversized item name";
                        return false;
                    }
                    if (parsed.Entries.Count < maxEntries) { parsed.Entries.Add(entry); }
                }
                if (parsed.NextId <= 0) { parsed.NextId = 1; }
                foreach (WishlistEntry entry in parsed.Entries) {
                    if (entry.Id >= parsed.NextId) { parsed.NextId = entry.Id + 1; }
                }
                data = parsed;
                return true;
            } catch (Exception e) {
                error = $"unreadable ({e.GetType().Name}: {e.Message})";
                return false;
            }
        }
    }
}
