using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // One-way copy of a character's Quick Stack Store Sort favorites. QSS keeps them in
    // BepInEx/config/QuickStackStore_player_<playerId>.dat as three BinaryFormatter objects back to back:
    // List<Tuple<int,int>> favorited slots, List<string> favorited item names, List<string> trash-flagged
    // item names (names are ItemData.m_shared.m_name, the key this mod's type marks use).
    //
    // Everything is imported as type marks, which is exactly what QSS meant by them: a favorited
    // "Bronze sword" stays favorited for every bronze sword. Nothing is written back to QSS.
    internal static class QssImport {

        internal struct Result {
            internal bool Imported;
            internal bool Failed;
            internal string Reason;
            internal int Slots;
            internal int Favorites;
            internal int Trash;

            public override string ToString() {
                return Imported ? $"{Slots} new slots, {Favorites} new favorite item types, {Trash} new trash item types" : Reason;
            }
        }

        internal static string FilePath(long playerId) {
            return Path.Combine(Paths.ConfigPath, $"QuickStackStore_player_{playerId}.dat");
        }

        // Without force, runs once per character: the flag is kept in the character's own mark data, so it
        // survives reinstalls and follows the character. Re-running only ever adds marks.
        internal static Result Run(bool force) {
            if (MarkStore.TryGet(out MarkData data) == false) { return Skip("no character is loaded"); }
            if (MarkStore.ReadOnly) { return Skip("this character's favorites are read-only", failed: true); }
            if (data.QssImported && force == false) { return Skip("already imported for this character"); }

            // No file is not "done": the character may not have used QSS yet, so a later spawn tries again.
            string path = FilePath(MarkStore.PlayerId);
            if (File.Exists(path) == false) { return Skip($"no Quick Stack Store Sort data at {path}"); }

            List<Tuple<int, int>> slots;
            List<string> favorites;
            List<string> trash;
            try {
                // Read-only and sharing: QSS itself may have the file open, and must never see it created
                // or truncated by this mod.
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    BinaryFormatter formatter = new BinaryFormatter { Binder = new ExpectedTypesBinder() };
                    slots = Next<List<Tuple<int, int>>>(formatter, stream);
                    favorites = Next<List<string>>(formatter, stream);
                    trash = Next<List<string>>(formatter, stream);
                }
            } catch (Exception e) {
                return Skip($"could not read {path}: {e.Message}", failed: true);
            }

            Result result = new Result { Imported = true };
            if (slots != null) {
                foreach (Tuple<int, int> slot in slots) {
                    if (slot != null && slot.Item1 >= 0 && slot.Item2 >= 0 && data.Slots.Add(new Vector2i(slot.Item1, slot.Item2))) {
                        result.Slots++;
                    }
                }
            }
            if (favorites != null) {
                foreach (string name in favorites) {
                    if (string.IsNullOrEmpty(name)) { continue; }
                    data.TrashTypes.Remove(name);
                    if (data.FavoriteTypes.Add(name)) { result.Favorites++; }
                }
            }
            if (trash != null) {
                foreach (string name in trash) {
                    if (string.IsNullOrEmpty(name) || data.FavoriteTypes.Contains(name)) { continue; }
                    if (data.TrashTypes.Add(name)) { result.Trash++; }
                }
            }

            data.QssImported = true;
            MarkStore.Commit();
            return result;
        }

        private static Result Skip(string reason, bool failed = false) {
            return new Result { Imported = false, Failed = failed, Reason = reason };
        }

        // AzuAutoStore's copy of this code rewrites QSS's file with only the first two lists when QSS is
        // installed, so a missing third object is normal, not an error.
        private static T Next<T>(BinaryFormatter formatter, Stream stream) where T : class {
            if (stream.Position >= stream.Length) { return null; }
            return formatter.Deserialize(stream) as T;
        }

        // BinaryFormatter will instantiate any type a file names. This file is only ever meant to hold the
        // three lists above, so anything else is refused rather than constructed.
        private sealed class ExpectedTypesBinder : SerializationBinder {
            private static readonly HashSet<Type> Expected = new HashSet<Type> {
                typeof(List<Tuple<int, int>>),
                typeof(Tuple<int, int>),
                typeof(Tuple<int, int>[]),
                typeof(List<string>),
                typeof(string),
                typeof(string[]),
            };

            public override Type BindToType(string assemblyName, string typeName) {
                Type type = Type.GetType($"{typeName}, {assemblyName}", throwOnError: false);
                if (type != null && Expected.Contains(type)) { return type; }
                throw new SerializationException($"unexpected type '{typeName}' in the file");
            }
        }
    }
}
