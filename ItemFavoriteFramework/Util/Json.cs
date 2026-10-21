using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // The JSON that crosses the API boundary. Reading goes through Jotunn's bundled SimpleJson (objects come
    // back as IDictionary<string, object>, arrays as List<object>, numbers as long or double). Writing is
    // done by hand so the output is exactly what docs/API.md promises -- in particular player ids are
    // written as strings, because a 64-bit id loses precision in any parser that reads numbers as doubles.
    internal static class Json {

        internal static bool TryParse(string json, out object value) {
            value = null;
            if (string.IsNullOrEmpty(json)) { return false; }
            try {
                return SimpleJson.SimpleJson.TryDeserializeObject(json, out value);
            } catch (Exception) {
                return false;
            }
        }

        internal static IDictionary<string, object> ParseObject(string json) {
            return TryParse(json, out object value) ? value as IDictionary<string, object> : null;
        }

        // A player-id list: a JSON array of strings or numbers. Anything that is not a non-zero id is
        // skipped. Duplicates are dropped, order is kept.
        internal static List<long> ParseIdList(string json) {
            List<long> ids = new List<long>();
            if (TryParse(json, out object value) == false || !(value is IList<object> array)) { return ids; }
            foreach (object element in array) {
                if (TryReadLong(element, out long id) && id != 0 && ids.Contains(id) == false) { ids.Add(id); }
            }
            return ids;
        }

        internal static bool TryReadLong(object value, out long result) {
            result = 0;
            switch (value) {
                case long l:
                    result = l;
                    return true;
                case int i:
                    result = i;
                    return true;
                case double d when d >= long.MinValue && d <= long.MaxValue && Math.Floor(d) == d:
                    result = (long)d;
                    return true;
                case string s:
                    return long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
                default:
                    return false;
            }
        }

        internal static string GetString(IDictionary<string, object> obj, string key, string fallback = "") {
            if (obj == null || obj.TryGetValue(key, out object value) == false || value == null) { return fallback; }
            if (value is string s) { return s; }
            if (value is double d) { return d.ToString(CultureInfo.InvariantCulture); }
            if (value is bool b) { return b ? "true" : "false"; }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        internal static float GetFloat(IDictionary<string, object> obj, string key, float fallback) {
            if (obj == null || obj.TryGetValue(key, out object value) == false || value == null) { return fallback; }
            float result;
            switch (value) {
                case long l: result = l; break;
                case double d: result = (float)d; break;
                case string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f): result = f; break;
                default: return fallback;
            }
            // "NaN" and "Infinity" parse, and Mathf.Clamp passes NaN straight through.
            return float.IsNaN(result) || float.IsInfinity(result) ? fallback : result;
        }

        // A value that is either a string or an array of strings, as wishlist criteria use.
        internal static List<string> GetStrings(IDictionary<string, object> obj, string key) {
            List<string> result = new List<string>();
            if (obj == null || obj.TryGetValue(key, out object value) == false || value == null) { return result; }
            if (value is IList<object> array) {
                foreach (object element in array) {
                    if (element != null) { result.Add(Convert.ToString(element, CultureInfo.InvariantCulture)); }
                }
            } else {
                string single = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(single) == false) { result.Add(single); }
            }
            return result;
        }

        // ---------------------------------------------------------------------------------------------

        internal static string Quote(string value) {
            if (value == null) { return "null"; }
            StringBuilder sb = new StringBuilder(value.Length + 2);
            AppendQuoted(sb, value);
            return sb.ToString();
        }

        internal static void AppendQuoted(StringBuilder sb, string value) {
            sb.Append('"');
            foreach (char c in value) {
                switch (c) {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        } else {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }

    // A small writer for flat-ish objects: Begin/End nest, commas are tracked per level.
    internal sealed class JsonBuilder {
        private readonly StringBuilder sb = new StringBuilder();
        private readonly Stack<bool> first = new Stack<bool>();

        internal JsonBuilder BeginObject(string key = null) => Open(key, '{');

        internal JsonBuilder EndObject() => Close('}');

        internal JsonBuilder BeginArray(string key = null) => Open(key, '[');

        internal JsonBuilder EndArray() => Close(']');

        internal JsonBuilder Str(string key, string value) {
            Key(key);
            Json.AppendQuoted(sb, value ?? "");
            return this;
        }

        // Ids as strings; see Json.
        internal JsonBuilder Id(string key, long value) => Str(key, value.ToString(CultureInfo.InvariantCulture));

        internal JsonBuilder Num(string key, long value) {
            Key(key);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        internal JsonBuilder Num(string key, float value) {
            Key(key);
            sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            return this;
        }

        internal JsonBuilder Bool(string key, bool value) {
            Key(key);
            sb.Append(value ? "true" : "false");
            return this;
        }

        // Already-serialized JSON, inserted as a value.
        internal JsonBuilder Raw(string key, string json) {
            Key(key);
            sb.Append(string.IsNullOrEmpty(json) ? "null" : json);
            return this;
        }

        public override string ToString() => sb.ToString();

        private JsonBuilder Open(string key, char bracket) {
            Key(key);
            sb.Append(bracket);
            first.Push(true);
            return this;
        }

        private JsonBuilder Close(char bracket) {
            first.Pop();
            sb.Append(bracket);
            return this;
        }

        // key is null for array elements.
        private void Key(string key) {
            if (first.Count > 0) {
                if (first.Peek()) {
                    first.Pop();
                    first.Push(false);
                } else {
                    sb.Append(',');
                }
            }
            if (key != null) {
                Json.AppendQuoted(sb, key);
                sb.Append(':');
            }
        }
    }
}
