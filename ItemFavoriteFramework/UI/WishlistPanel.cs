using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // The wishlist editor: the player's own list (add, edit conditions, automatic Need, remove) and, on a
    // server that shares them, the lists of the other players. Built with the shared ConfigUI kit; it is a
    // menu, so it holds input like the config editors do.
    //
    // Conditions come from the registered providers (EpicLoot's rarity, legendary, ...), each describing its
    // fields as schema JSON:
    //   {"title":"Epic Loot","fields":[{"key":"rarity","label":"Minimum rarity","type":"enum",
    //     "options":[["0","Magic"],["1","Rare"]]}]}
    // Field types: enum (cycle), pick (long list with a picker), multi (a list of picks), bool, int, text.
    internal static class WishlistPanel {
        private const float Width = 720f;
        private const float Height = 660f;
        private const float BodyX = 24f;
        private const float BodyY = 104f;
        private const float BodyW = Width - 48f;
        private const float BodyH = 470f;

        private enum View { Mine, Others, Edit }

        private static GameObject panel;
        private static GameObject bodyRoot;
        private static Text status;
        private static View view = View.Mine;

        // The entry being edited (a copy), and its condition values per provider.
        private static WishlistEntry editing;
        private static bool editingIsNew;
        private static Dictionary<string, Dictionary<string, object>> editValues;
        // Providers whose fields the editor showed for this item; only their conditions are rebuilt on save.
        private static readonly HashSet<string> renderedProviders = new HashSet<string>(StringComparer.Ordinal);

        internal static bool IsOpen => panel != null;

        internal static void Toggle() {
            if (IsOpen) {
                Close();
            } else {
                Open();
            }
        }

        internal static void PollKey() {
            KeyCode key = LootConfig.WishlistEditorKey.Value.MainKey;
            if (key == KeyCode.None) { return; }
            // Typing the key into one of the editor's own fields must not close it.
            if (IsOpen ? TextFieldFocused() : RollClient.IsTyping()) { return; }
            if (ZInput.GetKeyDown(key, logWarning: false) == false) { return; }
            foreach (KeyCode modifier in LootConfig.WishlistEditorKey.Value.Modifiers) {
                if (ZInput.GetKey(modifier, logWarning: false) == false) { return; }
            }
            Toggle();
        }

        private static bool TextFieldFocused() {
            UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            InputField field = selected != null ? selected.GetComponent<InputField>() : null;
            return field != null && field.isFocused;
        }

        internal static void Open() {
            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null) { return; }
            Close();
            view = View.Mine;
            editing = null;
            panel = ConfigUI.CreatePanel(ConfigUI.L("$iff_wishlist_title", "Wishlist"), Width, Height, out Transform root);
            ConfigUI.AddCloseX(root, Width, Close);

            if (Player.m_localPlayer == null || WishlistStore.TryGet(out _) == false) {
                ConfigUI.AddText(root, BodyX, BodyY, BodyW, 60f, ConfigUI.L("$iff_wishlist_no_character", "Load a character to edit its wishlist."),
                    17, TextAnchor.MiddleCenter);
                return;
            }

            ConfigUI.AddButton(root, BodyX, 58f, 200f, ConfigUI.L("$iff_wishlist_tab_mine", "My wishlist"), () => Show(View.Mine), 34f);
            ConfigUI.AddButton(root, BodyX + 210f, 58f, 200f, ConfigUI.L("$iff_wishlist_tab_others", "Other players"), () => Show(View.Others), 34f);
            status = ConfigUI.AddText(root, BodyX, BodyY + BodyH + 14f, BodyW - 280f, 50f, "", 14, TextAnchor.UpperLeft);
            Render();
        }

        internal static void Close() {
            if (panel != null) { UnityEngine.Object.Destroy(panel); }
            panel = null;
            bodyRoot = null;
            status = null;
            editing = null;
            ConfigUIPicker.Close();
        }

        private static void Show(View next) {
            view = next;
            editing = null;
            Render();
        }

        private static void Render() {
            if (panel == null) { return; }
            if (bodyRoot != null) { UnityEngine.Object.Destroy(bodyRoot); }
            bodyRoot = ConfigUI.NewRect("IFF_WishlistBody", panel.transform, 0f, 0f, Width, Height);
            switch (view) {
                case View.Others: RenderOthers(); break;
                case View.Edit: RenderEditor(); break;
                default: RenderMine(); break;
            }
            RenderStatus();
        }

        private static Transform NewScroll(out float contentWidth) {
            ConfigUI.CreateScroll(bodyRoot.transform, BodyX, BodyY, BodyW, BodyH, out Transform content, out contentWidth);
            return content;
        }

        // ---------------------------------------------------------------------------------------------
        // Mine
        // ---------------------------------------------------------------------------------------------

        private static void RenderMine() {
            if (WishlistStore.TryGet(out WishlistData data) == false) { return; }
            ConfigUI.AddButton(bodyRoot.transform, Width - 24f - 220f, 58f, 220f, ConfigUI.L("$iff_wishlist_add", "Add item..."), PickNewItem, 34f);

            Transform content = NewScroll(out float cw);
            if (data.Entries.Count == 0) {
                GameObject empty = ConfigUI.NewLayoutRow(content, cw, 70f);
                ConfigUI.AddText(empty.transform, 8f, 8f, cw - 16f, 60f,
                    ConfigUI.L("$iff_wishlist_empty", "Your wishlist is empty. Hold Alt + Shift and click an item, or use Add item."), 15, TextAnchor.UpperLeft);
                return;
            }
            foreach (WishlistEntry entry in data.Entries.ToList()) {
                WishlistEntry current = entry;
                GameObject row = ConfigUI.NewLayoutRow(content, cw, 40f);
                AddIcon(row.transform, 4f, 4f, 32f, current.Prefab);
                ConfigUI.AddText(row.transform, 44f, 0f, cw - 44f - 300f, 40f, Wishlists.Describe(current), 15, TextAnchor.MiddleLeft);
                ConfigUI.AddToggle(row.transform, cw - 290f, 8f, 24f, current.AutoNeed, on => {
                    WishlistEntry copy = current.Clone();
                    copy.AutoNeed = on;
                    Wishlists.Update(copy);
                    // The rows hold the entries they were built from; rebuild so Edit sees the new value.
                    Render();
                });
                ConfigUI.AddText(row.transform, cw - 260f, 0f, 120f, 40f, ConfigUI.L("$iff_wishlist_auto_need", "Auto Need"), 14, TextAnchor.MiddleLeft);
                ConfigUI.AddButton(row.transform, cw - 136f, 6f, 80f, ConfigUI.L("$iff_wishlist_edit", "Edit"), () => BeginEdit(current.Clone(), isNew: false), 28f);
                ConfigUI.AddButton(row.transform, cw - 48f, 6f, 40f, "x", () => {
                    Wishlists.Remove(current.Id);
                    Render();
                }, 28f);
            }
        }

        private static void PickNewItem() {
            List<string> options = ItemOptions();
            ConfigUIPicker.ShowPicker(ConfigUI.L("$iff_wishlist_add", "Add item..."), options, "", picked => {
                string prefab = PrefabFromOption(picked);
                if (string.IsNullOrEmpty(prefab)) { return; }
                BeginEdit(new WishlistEntry { Prefab = prefab }, isNew: true);
            });
        }

        private static List<string> cachedOptions;
        private static int cachedOptionsCount = -1;

        // "Iron sword [SwordIron]" for every item with an icon. The bracketed prefab is what is stored.
        private static List<string> ItemOptions() {
            ObjectDB db = ObjectDB.instance;
            if (db == null) { return new List<string>(); }
            if (cachedOptions != null && cachedOptionsCount == db.m_items.Count) { return cachedOptions; }
            List<string> options = new List<string>();
            foreach (GameObject go in db.m_items) {
                ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData?.m_shared == null) { continue; }
                Sprite[] icons = drop.m_itemData.m_shared.m_icons;
                if (icons == null || icons.Length == 0) { continue; }
                options.Add($"{ConfigUI.L(drop.m_itemData.m_shared.m_name)} [{go.name}]");
            }
            options.Sort(StringComparer.CurrentCultureIgnoreCase);
            cachedOptions = options;
            cachedOptionsCount = db.m_items.Count;
            return options;
        }

        private static string PrefabFromOption(string option) {
            if (string.IsNullOrEmpty(option)) { return null; }
            int open = option.LastIndexOf('[');
            int close = option.LastIndexOf(']');
            if (open >= 0 && close > open) { return option.Substring(open + 1, close - open - 1).Trim(); }
            return option.Trim();
        }

        private static void AddIcon(Transform parent, float x, float y, float size, string prefab) {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null) { return; }
            GameObject icon = ConfigUI.NewRect("Icon", parent, x, y, size, size);
            Image image = icon.AddComponent<Image>();
            image.sprite = drop.m_itemData.GetIcon();
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        // ---------------------------------------------------------------------------------------------
        // Others
        // ---------------------------------------------------------------------------------------------

        private static void RenderOthers() {
            Transform content = NewScroll(out float cw);
            if (ClientSession.WishlistsEnabled == false || WishlistRoster.Count == 0) {
                GameObject row = ConfigUI.NewLayoutRow(content, cw, 60f);
                string text = ClientSession.WishlistsEnabled
                    ? ConfigUI.L("$iff_wishlist_others_none", "No other player on this server shares a wishlist.")
                    : ConfigUI.L("$iff_wishlist_others_off", "This server does not share wishlists.");
                ConfigUI.AddText(row.transform, 8f, 8f, cw - 16f, 50f, text, 15, TextAnchor.UpperLeft);
                return;
            }
            foreach (WishlistRoster.Shared shared in WishlistRoster.All.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)) {
                GameObject header = ConfigUI.NewLayoutRow(content, cw, ConfigUI.RowHeight);
                ConfigUI.AddText(header.transform, 0f, 0f, cw, ConfigUI.RowHeight, $"{shared.Name} ({shared.Data.Entries.Count})", 18,
                    TextAnchor.MiddleLeft, GUIManager.Instance.ValheimYellow);
                foreach (WishlistEntry entry in shared.Data.Entries) {
                    GameObject row = ConfigUI.NewLayoutRow(content, cw, 32f);
                    AddIcon(row.transform, 16f, 2f, 28f, entry.Prefab);
                    ConfigUI.AddText(row.transform, 52f, 0f, cw - 60f, 32f, Wishlists.Describe(entry), 14, TextAnchor.MiddleLeft);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Editing one entry
        // ---------------------------------------------------------------------------------------------

        private static void BeginEdit(WishlistEntry entry, bool isNew) {
            editing = entry;
            editingIsNew = isNew;
            editValues = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (WishlistCriterion criterion in entry.Criteria) {
                Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
                IDictionary<string, object> parsed = Json.ParseObject(criterion.Json);
                if (parsed != null) {
                    foreach (KeyValuePair<string, object> pair in parsed) {
                        // "v" is the format version CriteriaJson writes itself.
                        if (pair.Key == "v") { continue; }
                        if (pair.Value is IList<object> list) {
                            values[pair.Key] = list.Select(FieldText).ToList();
                        } else if (pair.Value != null) {
                            values[pair.Key] = FieldText(pair.Value);
                        }
                    }
                }
                editValues[criterion.Provider] = values;
            }
            view = View.Edit;
            Render();
        }

        // How a stored value reads back into a field: bools the way the bool field writes them.
        private static string FieldText(object value) {
            if (value is bool b) { return b ? "true" : ""; }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private sealed class Field {
            internal string Key;
            internal string Label;
            internal string Type;
            // (value, label)
            internal List<KeyValuePair<string, string>> Options = new List<KeyValuePair<string, string>>();

            internal string LabelOf(string value) {
                foreach (KeyValuePair<string, string> option in Options) {
                    if (option.Key == value) { return option.Value; }
                }
                return value;
            }

            internal string ValueOf(string label) {
                foreach (KeyValuePair<string, string> option in Options) {
                    if (option.Value == label) { return option.Key; }
                }
                return label;
            }
        }

        private static List<Field> ParseSchema(string schemaJson, out string title) {
            title = "";
            List<Field> fields = new List<Field>();
            IDictionary<string, object> schema = Json.ParseObject(schemaJson);
            if (schema == null) { return fields; }
            title = Json.GetString(schema, "title");
            if (schema.TryGetValue("fields", out object raw) == false || !(raw is IList<object> list)) { return fields; }
            foreach (object element in list) {
                if (!(element is IDictionary<string, object> obj)) { continue; }
                Field field = new Field {
                    Key = Json.GetString(obj, "key"),
                    Label = Json.GetString(obj, "label"),
                    Type = Json.GetString(obj, "type", "text").ToLowerInvariant(),
                };
                if (string.IsNullOrEmpty(field.Key)) { continue; }
                if (obj.TryGetValue("options", out object options) && options is IList<object> optionList) {
                    foreach (object option in optionList) {
                        if (option is IList<object> pair && pair.Count >= 2) {
                            field.Options.Add(new KeyValuePair<string, string>(Convert.ToString(pair[0], CultureInfo.InvariantCulture), Convert.ToString(pair[1], CultureInfo.InvariantCulture)));
                        } else if (option != null) {
                            string value = Convert.ToString(option, CultureInfo.InvariantCulture);
                            field.Options.Add(new KeyValuePair<string, string>(value, value));
                        }
                    }
                }
                fields.Add(field);
            }
            return fields;
        }

        private static void RenderEditor() {
            if (editing == null) {
                Show(View.Mine);
                return;
            }
            const float labelW = 230f;
            Transform content = NewScroll(out float cw);
            float ctrlW = cw - labelW - 20f;

            GameObject head = ConfigUI.NewLayoutRow(content, cw, 44f);
            AddIcon(head.transform, 4f, 4f, 36f, editing.Prefab);
            ConfigUI.AddText(head.transform, 50f, 0f, cw - 50f, 44f, $"{Wishlists.DisplayName(editing.Prefab)}  [{editing.Prefab}]", 19,
                TextAnchor.MiddleLeft, GUIManager.Instance.ValheimYellow);

            ConfigUI.AddToggleRow(ConfigUI.NewLayoutRow(content, cw, ConfigUI.RowHeight).transform, cw, labelW,
                ConfigUI.L("$iff_wishlist_auto_need_long", "Vote Need automatically"), editing.AutoNeed, on => editing.AutoNeed = on);

            bool anyProvider = false;
            renderedProviders.Clear();
            foreach (WishlistProvider provider in WishlistProviders.All) {
                List<Field> fields = ParseSchema(WishlistProviders.Schema(provider, editing.Prefab), out string title);
                if (fields.Count == 0) { continue; }
                anyProvider = true;
                renderedProviders.Add(provider.Id);
                if (editValues.TryGetValue(provider.Id, out Dictionary<string, object> values) == false) {
                    values = new Dictionary<string, object>(StringComparer.Ordinal);
                    editValues[provider.Id] = values;
                }
                ConfigUI.AddHeaderRow(ConfigUI.NewLayoutRow(content, cw, ConfigUI.RowHeight).transform, cw,
                    string.IsNullOrEmpty(title) ? provider.Id : title);
                foreach (Field field in fields) { AddField(content, cw, labelW, ctrlW, field, values); }
            }
            if (anyProvider == false) {
                GameObject none = ConfigUI.NewLayoutRow(content, cw, 50f);
                ConfigUI.AddText(none.transform, 8f, 8f, cw - 16f, 40f,
                    ConfigUI.L("$iff_wishlist_no_conditions", "No installed mod adds conditions for this item; any one of it matches."), 14, TextAnchor.UpperLeft);
            }

            ConfigUI.AddButton(bodyRoot.transform, Width - 24f - 260f, BodyY + BodyH + 12f, 120f, ConfigUI.L("$iff_wishlist_save", "Save"), SaveEdit, 36f);
            ConfigUI.AddButton(bodyRoot.transform, Width - 24f - 130f, BodyY + BodyH + 12f, 120f, ConfigUI.L("$iff_wishlist_cancel", "Cancel"), () => Show(View.Mine), 36f);
        }

        private static void AddField(Transform content, float cw, float labelW, float ctrlW, Field field, Dictionary<string, object> values) {
            string label = string.IsNullOrEmpty(field.Label) ? field.Key : field.Label;
            string current = values.TryGetValue(field.Key, out object raw) && raw is string s ? s : "";
            switch (field.Type) {
                case "enum": {
                    List<KeyValuePair<string, string>> options = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("", ConfigUI.L("$iff_wishlist_any", "Any")) };
                    options.AddRange(field.Options);
                    int index = Math.Max(0, options.FindIndex(o => o.Key == current));
                    ConfigUI.AddEnumCycleRow(ConfigUI.NewLayoutRow(content, cw, ConfigUI.RowHeight).transform, cw, labelW, Math.Min(ctrlW, 260f), label,
                        options.Select(o => o.Value).ToArray(), index, i => Set(values, field.Key, options[i].Key));
                    break;
                }
                case "pick":
                    ConfigUI.AddPickerRow(ConfigUI.NewLayoutRow(content, cw, ConfigUI.RowHeight).transform, cw, labelW, ctrlW, label, field.LabelOf(current),
                        () => field.Options.Select(o => o.Value).ToList(), picked => Set(values, field.Key, field.ValueOf(picked)));
                    break;
                case "multi": {
                    // A re-render (after the list's Add or x) finds the live list from the last one.
                    values.TryGetValue(field.Key, out object existing);
                    List<string> labels = existing is LabelList live ? live.Labels
                        : existing is List<string> stored ? stored.Select(field.LabelOf).ToList()
                        : new List<string>();
                    values[field.Key] = new LabelList(labels, field);
                    ConfigUI.AddStringListEditor(content, cw, label, labels, Render,
                        field.Options.Count > 0 ? (Func<IList<string>>)(() => field.Options.Select(o => o.Value).ToList()) : null);
                    break;
                }
                case "bool":
                    ConfigUI.AddToggleRow(ConfigUI.NewLayoutRow(content, cw, ConfigUI.RowHeight).transform, cw, labelW, label,
                        string.Equals(current, "true", StringComparison.OrdinalIgnoreCase) || current == "1", on => Set(values, field.Key, on ? "true" : ""));
                    break;
                default:
                    ConfigUI.AddTextFieldRow(ConfigUI.NewLayoutRow(content, cw, ConfigUI.RowHeight).transform, cw, labelW, Math.Min(ctrlW, 300f), label, current,
                        text => Set(values, field.Key, (text ?? "").Trim()));
                    break;
            }
        }

        // A multi field's live list of labels, mapped back to values on save. Kept in the values dictionary so
        // a re-render (after Add or remove) keeps what was typed.
        private sealed class LabelList {
            internal readonly List<string> Labels;
            internal readonly Field Field;

            internal LabelList(List<string> labels, Field field) {
                Labels = labels;
                Field = field;
            }
        }

        private static void Set(Dictionary<string, object> values, string key, string value) {
            if (string.IsNullOrEmpty(value)) {
                values.Remove(key);
            } else {
                values[key] = value;
            }
        }

        private static void SaveEdit() {
            if (editing == null) { return; }
            List<WishlistCriterion> criteria = new List<WishlistCriterion>();
            foreach (KeyValuePair<string, Dictionary<string, object>> provider in editValues) {
                if (renderedProviders.Contains(provider.Key) == false) { continue; }
                string json = CriteriaJson(provider.Value);
                if (json != null) { criteria.Add(Wishlists.MakeCriterion(provider.Key, json)); }
            }
            // Conditions whose provider showed no fields here (not installed, or nothing to offer for this
            // item) stay exactly as they were, json and description included.
            foreach (WishlistCriterion old in editing.Criteria) {
                if (renderedProviders.Contains(old.Provider) == false) { criteria.Add(old); }
            }
            if (criteria.Count > WishlistData.MaxCriteria) { criteria = criteria.Take(WishlistData.MaxCriteria).ToList(); }

            WishResult result;
            if (editingIsNew) {
                result = Wishlists.Add(editing.Prefab, criteria, editing.AutoNeed);
            } else {
                WishlistEntry updated = new WishlistEntry { Id = editing.Id, Prefab = editing.Prefab, AutoNeed = editing.AutoNeed };
                updated.Criteria.AddRange(criteria);
                result = Wishlists.Update(updated);
            }
            if (result == WishResult.ReadOnly || result == WishResult.Full || result == WishResult.InvalidItem) {
                if (status != null) { status.text = "<color=#F87171>" + result + "</color>"; }
                return;
            }
            Show(View.Mine);
        }

        // {"v":1,"key":"value","list":["a","b"]}, or null when nothing was set.
        private static string CriteriaJson(Dictionary<string, object> values) {
            JsonBuilder json = new JsonBuilder().BeginObject().Num("v", 1);
            int set = 0;
            foreach (KeyValuePair<string, object> pair in values.OrderBy(p => p.Key, StringComparer.Ordinal)) {
                if (pair.Value is string s && s.Length > 0) {
                    json.Str(pair.Key, s);
                    set++;
                } else if (pair.Value is LabelList labels) {
                    List<string> picked = labels.Labels.Where(l => string.IsNullOrWhiteSpace(l) == false).Select(l => labels.Field.ValueOf(l.Trim())).Distinct().ToList();
                    if (picked.Count == 0) { continue; }
                    json.BeginArray(pair.Key);
                    foreach (string value in picked) { json.Str(null, value); }
                    json.EndArray();
                    set++;
                } else if (pair.Value is List<string> list && list.Count > 0) {
                    json.BeginArray(pair.Key);
                    foreach (string value in list) { json.Str(null, value); }
                    json.EndArray();
                    set++;
                }
            }
            return set == 0 ? null : json.EndObject().ToString();
        }

        // ---------------------------------------------------------------------------------------------

        private static void RenderStatus() {
            if (status == null) { return; }
            if (WishlistStore.ReadOnly) {
                status.text = "<color=#F87171>" + WishlistStore.Problem + "</color>";
                return;
            }
            int count = WishlistStore.TryGet(out WishlistData data) ? data.Entries.Count : 0;
            string sharing;
            if (ClientSession.WishlistsEnabled == false) {
                sharing = ConfigUI.L("$iff_wishlist_sharing_unavailable", "not shared (the server does not share wishlists)");
            } else if (LootConfig.ShareWishlist.Value) {
                sharing = ConfigUI.L("$iff_wishlist_sharing_on", "shared with everyone on this server");
            } else {
                sharing = ConfigUI.L("$iff_wishlist_sharing_off", "not shared (ShareWishlist is off)");
            }
            status.text = $"{count} / {WishlistData.MaxEntries} · {sharing}";
        }
    }
}
