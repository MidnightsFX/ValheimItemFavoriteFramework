using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // Everything this mod remembers per character. Saved in Player.m_customData (see MarkStore), so it
    // lives in the character's .fch and follows the character between machines and servers.
    //
    // Per-instance marks are not in here -- they sit on the items themselves. The two generations are
    // what those marks are checked against: bumping one invalidates every instance mark of that kind,
    // including ones on items in chests this character will never open again.
    internal sealed class MarkData {
        internal const int CurrentVersion = 1;

        internal int FavoriteGeneration;
        internal int TrashGeneration;
        internal bool QssImported;
        internal readonly HashSet<Vector2i> Slots = new HashSet<Vector2i>();
        // Keyed by ItemData.m_shared.m_name, the same key Quick Stack Store Sort uses.
        internal readonly HashSet<string> FavoriteTypes = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> TrashTypes = new HashSet<string>(StringComparer.Ordinal);

        internal string ToBase64() {
            ZPackage pkg = new ZPackage();
            pkg.Write(CurrentVersion);
            pkg.Write(FavoriteGeneration);
            pkg.Write(TrashGeneration);
            pkg.Write(QssImported);

            // Sorted so an unchanged set always serializes to the same string.
            List<Vector2i> slots = Slots.OrderBy(s => s.y).ThenBy(s => s.x).ToList();
            pkg.Write(slots.Count);
            foreach (Vector2i slot in slots) { pkg.Write(slot); }

            WriteNames(pkg, FavoriteTypes);
            WriteNames(pkg, TrashTypes);
            return pkg.GetBase64();
        }

        // False when the data cannot be used as-is: unreadable, or written by a newer version of this mod
        // that may hold fields this one would silently drop on the next save.
        internal static bool TryParse(string base64, out MarkData data, out string error) {
            data = null;
            error = null;
            try {
                ZPackage pkg = new ZPackage(base64);
                int version = pkg.ReadInt();
                if (version > CurrentVersion) {
                    error = $"saved by a newer version of {ItemFavoriteFramework.PluginName} (data version {version}, this version reads {CurrentVersion})";
                    return false;
                }
                if (version < 1) {
                    error = $"unknown data version {version}";
                    return false;
                }

                MarkData parsed = new MarkData();
                parsed.FavoriteGeneration = pkg.ReadInt();
                parsed.TrashGeneration = pkg.ReadInt();
                parsed.QssImported = pkg.ReadBool();

                int slotCount = pkg.ReadInt();
                for (int i = 0; i < slotCount; i++) { parsed.Slots.Add(pkg.ReadVector2i()); }

                ReadNames(pkg, parsed.FavoriteTypes);
                ReadNames(pkg, parsed.TrashTypes);
                data = parsed;
                return true;
            } catch (Exception e) {
                error = $"unreadable ({e.GetType().Name}: {e.Message})";
                return false;
            }
        }

        private static void WriteNames(ZPackage pkg, HashSet<string> names) {
            List<string> sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
            pkg.Write(sorted.Count);
            foreach (string name in sorted) { pkg.Write(name); }
        }

        private static void ReadNames(ZPackage pkg, HashSet<string> into) {
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++) {
                string name = pkg.ReadString();
                if (string.IsNullOrEmpty(name) == false) { into.Add(name); }
            }
        }
    }
}
