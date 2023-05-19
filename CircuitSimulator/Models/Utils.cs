using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Media;
using System.Text.Json;
using CircuitSimulator.ViewModels;
using System.Collections;
using System.Diagnostics;
using System;
using Avalonia.Controls.Shapes;

using System.Data.SQLite;
using System.Data;
namespace CircuitSimulator.Models {
    public static class Utils {

        /*
         * Base64 абилка
         */

        public static string Base64Encode(string plainText) {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData) {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        /*
         * JSON абилка
         */

        public static string JsonEscape(string str) {
            StringBuilder sb = new();
            foreach (char i in str) {
                sb.Append(i switch {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '$' => "{$", // Чисто по моей части ;'-}
                    _ => i
                });
            }
            return sb.ToString();
        }
        public static string Obj2json(object? obj) { // Велосипед ради поддержки своей сериализации классов по типу Point, SolidColorBrush и т.д.
            switch (obj) {
            case null: return "null";
            case string @str: return '"' + JsonEscape(str) + '"';
            case bool @bool: return @bool ? "true" : "false";
            case short @short: return @short.ToString();
            case int @int: return @int.ToString();
            case long @long: return @long.ToString();
            case float @float: return @float.ToString().Replace(',', '.');
            case double @double: return @double.ToString().Replace(',', '.');

            case Point @point: return "\"$p$" + (int) @point.X + "," + (int) @point.Y + '"';
            case Size @size: return "\"$s$" + (int) @size.Width + "," + (int) @size.Height + '"';
            case Points @points: return "\"$P$" + string.Join("|", @points.Select(p => (int) p.X + "," + (int) p.Y)) + '"';
            case SolidColorBrush @color: return "\"$C$" + @color.Color + '"';
            case Thickness @thickness: return "\"$T$" + @thickness.Left + "," + @thickness.Top + "," + @thickness.Right + "," + @thickness.Bottom + '"';

            case Dictionary<string, object?> @dict: {
                StringBuilder sb = new();
                sb.Append('{');
                foreach (var entry in @dict) {
                    if (sb.Length > 1) sb.Append(", ");
                    sb.Append(Obj2json(entry.Key));
                    sb.Append(": ");
                    sb.Append(Obj2json(entry.Value));
                }
                sb.Append('}');
                return sb.ToString();
            }
            case IEnumerable @list: {
                StringBuilder sb = new();
                sb.Append('[');
                foreach (object? item in @list) {
                    if (sb.Length > 1) sb.Append(", ");
                    sb.Append(Obj2json(item));
                }
                sb.Append(']');
                return sb.ToString();
            }
            default: return "(" + obj.GetType() + " ???)";
            }
        }

        private static object JsonHandler(string str) {
            if (str.Length < 3 || str[0] != '$' || str[2] != '$') return str.Replace("{$", "$");
            string data = str[3..];
            string[] thick = str[1] == 'T' ? data.Split(',') : System.Array.Empty<string>();
            return str[1] switch {
                'p' => Point.Parse(data),
                's' => Size.Parse(data),
                // 'P' => new SafePoints(data.Replace('|', ' ')).Points,
                'C' => new SolidColorBrush(Color.Parse(data)),
                'T' => new Thickness(double.Parse(thick[0]), double.Parse(thick[1]), double.Parse(thick[2]), double.Parse(thick[3])),
                _ => str,
            };
        }
        private static object? JsonHandler(object? obj) {
            if (obj == null) return null;

            if (obj is List<object?> @list) return @list.Select(JsonHandler).ToList();
            if (obj is Dictionary<string, object?> @dict) {
                return new Dictionary<string, object?>(@dict.Select(pair => new KeyValuePair<string, object?>(pair.Key, JsonHandler(pair.Value))));
            }
            if (obj is JsonElement @item) {
                switch (@item.ValueKind) {
                case JsonValueKind.Undefined: return null;
                case JsonValueKind.Object:
                    Dictionary<string, object?> res = new();
                    foreach (var el in @item.EnumerateObject()) res[el.Name] = JsonHandler(el.Value);
                    return res;
                case JsonValueKind.Array:
                    List<object?> res2 = @item.EnumerateArray().Select(item => JsonHandler((object?) item)).ToList();
                    return res2;
                case JsonValueKind.String:
                    var s = JsonHandler(@item.GetString() ?? "");
                    // Log.Write("JS: '" + @item.GetString() + "' -> '" + s + "'");
                    return s;
                case JsonValueKind.Number:
                    if (@item.ToString().Contains('.')) return @item.GetDouble();
                    // Иначе это целое число
                    long a = @item.GetInt64();
                    int b = @item.GetInt32();
                    // short c = @item.GetInt16();
                    if (a != b) return a;
                    // if (b != c) return b;
                    return b;
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null: return null;
                }
            }
            Log.Write("JT: " + obj.GetType());

            return obj;
        }
        public static object? Json2obj(string json) {
            json = json.Trim();
            if (json.Length == 0) return null;

            object? data;
            if (json[0] == '[') data = JsonSerializer.Deserialize<List<object?>>(json);
            else if (json[0] == '{') data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            else return null;

            return JsonHandler(data);
        }

        /*
         * XML абилка
         */

        public static string XMLEscape(string str) {
            StringBuilder sb = new();
            foreach (char i in str) {
                sb.Append(i switch {
                    '"' => "&quot;",
                    '\'' => "&apos;",
                    '>' => "&gt;",
                    '<' => "&lt;",
                    '&' => "&amp;",
                    _ => i
                });
            }
            return sb.ToString();
        }

        private static bool IsComposite(object? obj) {
            if (obj == null) return false;
            if (obj is List<object?> || obj is Dictionary<string, object?> || obj is not JsonElement @item) return true;
            var T = @item.ValueKind;
            return T == JsonValueKind.Object || T == JsonValueKind.Array;
        }
        private static string Dict2XML(Dictionary<string, object?> dict, string level) {
            StringBuilder attrs = new();
            StringBuilder items = new();
            foreach (var entry in dict)
                if (IsComposite(entry.Value))
                    items.Append(level + "\t<" + entry.Key + ">" + ToXMLHandler(entry.Value, level + "\t\t") + level + "\t</" + entry.Key + ">");
                else attrs.Append(" " + entry.Key + "=\"" + ToXMLHandler(entry.Value, "{err}") + "\"");

            if (items.Length == 0) return level + "<Dict" + attrs.ToString() + "/>";
            return level + "<Dict" + attrs.ToString() + ">" + items.ToString() + level + "</Dict>";
        }
        private static string List2XML(List<object?> list, string level) {
            StringBuilder attrs = new();
            StringBuilder items = new();
            int num = 0;
            foreach (var entry in list) {
                if (IsComposite(entry)) items.Append(ToXMLHandler(entry, level + "\t"));
                else attrs.Append($" _{num}='" + ToXMLHandler(entry, "{err}") + "'");
                num++;
            }

            if (items.Length == 0) return level + "<List" + attrs.ToString() + "/>";
            return level + "<List" + attrs.ToString() + ">" + items.ToString() + level + "</List>";
        }

        private static string ToXMLHandler(object? obj, string level) {
            if (obj == null) return "null";

            if (obj is List<object?> @list) return List2XML(@list, level);
            if (obj is Dictionary<string, object?> @dict) return Dict2XML(@dict, level);
            if (obj is JsonElement @item) {
                switch (@item.ValueKind) {
                case JsonValueKind.Undefined: return "undefined";
                case JsonValueKind.Object:
                    return Dict2XML(new Dictionary<string, object?>(@item.EnumerateObject().Select(pair => new KeyValuePair<string, object?>(pair.Name, pair.Value))), level);
                case JsonValueKind.Array:
                    return List2XML(@item.EnumerateArray().Select(item => (object?) item).ToList(), level);
                case JsonValueKind.String:
                    var s = XMLEscape(@item.GetString() ?? "null");
                    // Log.Write("XS: '" + @item.GetString() + "' -> '" + s + "'");
                    return s;
                case JsonValueKind.Number: return "$" + @item.ToString(); // escape NUM
                case JsonValueKind.True: return "_BOOL_yeah";
                case JsonValueKind.False: return "_BOOL_nop";
                case JsonValueKind.Null: return "null";
                }
            }
            Log.Write("XT: " + obj.GetType());

            return "<UnknowType>" + obj.GetType() + "</UnknowType>";
        }
        public static string? Json2xml(string json) {
            json = json.Trim();
            if (json.Length == 0) return null;

            object? data;
            if (json[0] == '[') data = JsonSerializer.Deserialize<List<object?>>(json);
            else if (json[0] == '{') data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            else return null;

            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + ToXMLHandler(data, "\n");
        }

        private static string ToJSONHandler(string str) {
            if (str.Length > 1 && str[0] == '$' && str[1] <= '9' && str[1] >= '0') return str[1..]; // unescape NUM
            return str switch {
                "null" => "null",
                "undefined" => "undefined",
                "_BOOL_yeah" => "true",
                "_BOOL_nop" => "false",
                _ => '"' + str.Replace("\\", "\\\\") + '"',
            };
        }
        private static string ToJSONHandler(XElement xml) {
            var name = xml.Name.LocalName;
            StringBuilder sb = new();
            if (name == "Dict") {
                sb.Append('{');
                foreach (var attr in xml.Attributes()) {
                    if (sb.Length > 1) sb.Append(", ");
                    sb.Append(ToJSONHandler(attr.Name.LocalName));
                    sb.Append(": ");
                    sb.Append(ToJSONHandler(attr.Value));
                }
                foreach (var el in xml.Elements()) {
                    if (sb.Length > 1) sb.Append(", ");
                    sb.Append(ToJSONHandler(el.Name.LocalName));
                    sb.Append(": ");
                    sb.Append(ToJSONHandler(el.Elements().ToArray()[0]));
                }
                sb.Append('}');
            } else if (name == "List") {
                var attrs = xml.Attributes().ToArray();
                var els = xml.Elements().ToArray();
                int count = attrs.Length + els.Length;
                var res = new string[count];
                var used = new bool[count];
                int num;
                foreach (var attr in attrs) {
                    num = int.Parse(attr.Name.LocalName[1..]);
                    res[num] = ToJSONHandler(attr.Value);
                    used[num] = true;
                }
                num = 0;
                foreach (var el in els) {
                    while (used[num]) num++;
                    res[num++] = ToJSONHandler(el);
                }
                sb.Append('[');
                foreach (var item in res) {
                    if (sb.Length > 1) sb.Append(", ");
                    sb.Append(item);
                }
                sb.Append(']');
            } else sb.Append("Type??" + name);
            return sb.ToString();
        }
        public static string Xml2json(string xml) => ToJSONHandler(XElement.Parse(xml));

        /*
         * YAML абилка
         */

        public static string YAMLEscape(string str) {
            string[] arr = new[] { "true", "false", "null", "undefined", "" };
            if (arr.Contains(str)) return '"' + str + '"';

            string black_list = " -:\"\n\t";
            bool escape = "0123456789[{".Contains(str[0]);
            if (!escape)
                foreach (char i in str)
                    if (black_list.Contains(i)) { escape = true; break; }
            if (!escape) return str;

            StringBuilder sb = new();
            sb.Append('"');
            foreach (char i in str) {
                sb.Append(i switch {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    _ => i
                });
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string Dict2YAML(Dictionary<string, object?> dict, string level) {
            if (dict.Count == 0) return " {}";
            StringBuilder res = new();
            foreach (var entry in dict)
                res.Append(level + YAMLEscape(entry.Key) + ":" + (IsComposite(entry.Value) ? "" : " ") + ToYAMLHandler(entry.Value, level + "\t"));
            return res.ToString();
        }
        private static string List2YAML(List<object?> list, string level) {
            if (list.Count == 0) return " []";
            StringBuilder res = new();
            foreach (var entry in list)
                res.Append(level + "-" + (IsComposite(entry) ? "" : " ") + ToYAMLHandler(entry, level + "\t"));
            return res.ToString();
        }

        private static string ToYAMLHandler(object? obj, string level) {
            if (obj == null) return "null";

            if (obj is List<object?> @list) return List2YAML(@list, level);
            if (obj is Dictionary<string, object?> @dict) return Dict2YAML(@dict, level);
            if (obj is JsonElement @item) {
                switch (@item.ValueKind) {
                case JsonValueKind.Undefined: return "undefined";
                case JsonValueKind.Object:
                    return Dict2YAML(new Dictionary<string, object?>(@item.EnumerateObject().Select(pair => new KeyValuePair<string, object?>(pair.Name, pair.Value))), level);
                case JsonValueKind.Array:
                    return List2YAML(@item.EnumerateArray().Select(item => (object?) item).ToList(), level);
                case JsonValueKind.String:
                    var s = YAMLEscape(@item.GetString() ?? "null");
                    // Log.Write("YS: '" + @item.GetString() + "' -> " + s);
                    return s;
                case JsonValueKind.Number: return @item.ToString();
                case JsonValueKind.True: return "true";
                case JsonValueKind.False: return "false";
                case JsonValueKind.Null: return "null";
                }
            }
            Log.Write("YT: " + obj.GetType());
            throw new Exception("Чё?!");
        }

        public static string? Json2yaml(string json) {
            json = json.Trim();
            if (json.Length == 0) return null;

            object? data;
            if (json[0] == '[') data = JsonSerializer.Deserialize<List<object?>>(json);
            else if (json[0] == '{') data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            else return null;

            return "---" + ToYAMLHandler(data, "\n") + "\n"; // Конец будет обязателен, как в питоне!
        }


        private static void YAML_Log(string mess, int level = 0) {
            if (level >= 4) Log.Write(mess);
        }
        private static string YAML_ParseString(ref string yaml, ref int pos) {
            char first = ' ';
            while (" \n\t".Contains(first)) first = yaml[pos++];
            bool quote = first == '"';
            StringBuilder sb = new();
            if (quote) {
                char c = yaml[pos++];
                while (c != '"') {
                    sb.Append(c);
                    c = yaml[pos++];
                }
                c = yaml[pos++];
                if (c != ':' && c != '\n') throw new Exception("После '\"' может быть только ':', либо '\n'");
                if (c == ':') pos--;
            } else {
                sb.Append(first);
                char c = yaml[pos++];
                while (c != ':' && c != '\n') {
                    sb.Append(c);
                    c = yaml[pos++];
                }
                if (c == ':') pos--;
            }
            YAML_Log("Parsed str: " + sb.ToString(), 1);
            return sb.ToString();
        }
        private static string YAML_ParseNum(ref string yaml, ref int pos) {
            char c = yaml[pos++];
            StringBuilder sb = new();
            while ("0123456789.".Contains(c)) {
                sb.Append(c);
                c = yaml[pos++];
            }
            if (c != '\n') throw new Exception("После числа всяко должен быть '\n");
            YAML_Log("Parsed num: " + sb.ToString(), 1);
            return sb.ToString();
        }
        private static string YAML_ParseItem(ref string yaml, ref int pos) {
            char first = ' ';
            while (" \n\t".Contains(first)) first = yaml[pos++];
            pos--;
            if (first == '"')
                return '"' + YAML_ParseString(ref yaml, ref pos) + '"';
            if ("0123456789".Contains(first))
                return YAML_ParseNum(ref yaml, ref pos);

            string str = YAML_ParseString(ref yaml, ref pos);
            string[] arr = new[] { "true", "false", "null", "undefined", "", "[]", "{}" };
            if (arr.Contains(str)) return str;
            return '"' + str + '"';
        }
        private static string YAML_ParseLayer(ref string yaml, ref int pos) {
            if (pos == yaml.Length) return ""; // Конец файла
            StringBuilder sb = new();
            char first = yaml[pos++];
            while (" \t".Contains(first)) {
                sb.Append(first);
                first = yaml[pos++];
            }
            pos--;
            return sb.ToString();
        }
        private static string YAML_ToJSONHandler(ref string yaml, ref int pos) {
            var layer = YAML_ParseLayer(ref yaml, ref pos);
            if (pos == yaml.Length) return ""; // Конец файла
            char first = yaml[pos++];

            switch (first) {
            case '[':
                if (yaml[pos++] != ']' || yaml[pos++] != '\n') throw new Exception("После [ ожидалось ]\\n");
                return "[]";
            case '{':
                if (yaml[pos++] != '}' || yaml[pos++] != '\n') throw new Exception("После { ожидалось }\\n");
                return "{}";
            case '-': {
                StringBuilder res = new();
                res.Append('[');
                bool First = true;
                pos--;
                while (true) {
                    if (pos == yaml.Length) break; // Конец файла

                    if (First) First = false;
                    else {
                        var saved_pos2 = pos;
                        var layer3 = YAML_ParseLayer(ref yaml, ref pos);
                        YAML_Log("DOWN_LAYER: '" + layer + "', '" + layer3 + "'");
                        if (layer != layer3) {
                            if (layer3.Length > layer.Length) throw new Exception("Ожидался элемент списка вместо подъёма");
                            if (!layer.StartsWith(layer3)) throw new Exception("Странность в упавшем layer'е");
                            YAML_Log("Падение"); pos = saved_pos2; break;
                        }

                        res.Append(", ");
                    }

                    if (yaml[pos++] != '-') throw new Exception("Ожидалось '-' в следующем элементе списка");

                    char c = yaml[pos++];
                    if (c == ' ') {
                        var value = YAML_ParseItem(ref yaml, ref pos);
                        res.Append(value);
                    } else if (c == '\n') {
                    } else throw new Exception("После '-' ожидалось ' ', либо '\n'");

                    int saved_pos = pos;
                    var layer2 = YAML_ParseLayer(ref yaml, ref pos);
                    YAML_Log("LAYER: '" + layer + "', '" + layer2 + "'");
                    if (layer2.Length < layer.Length) {
                        if (!layer.StartsWith(layer2)) throw new Exception("Странность в упавшем layer'е");
                        YAML_Log("Падение"); pos = saved_pos; break;
                    }
                    if (!layer2.StartsWith(layer)) throw new Exception("Странность в следующем layer'е");
                    if (layer == layer2) { YAML_Log("Сохранение"); pos = saved_pos; continue; }
                    YAML_Log("Подъём");
                    if (c == '\n') {
                        pos = saved_pos;
                        var value = YAML_ToJSONHandler(ref yaml, ref pos);
                        res.Append(value);
                    } else throw new Exception("Здесь не может быть подъёма");
                }
                res.Append(']');
                YAML_Log("Список рождён: " + res.ToString(), 2);
                return res.ToString(); }
            case '"':
            default: {
                pos--;
                StringBuilder res = new();
                res.Append('{');
                bool First = true;
                while (true) {
                    if (pos == yaml.Length) break; // Конец файла

                    if (First) First = false;
                    else {
                        var saved_pos2 = pos;
                        var layer3 = YAML_ParseLayer(ref yaml, ref pos);
                        YAML_Log("DICT_LAYER: '" + layer + "', '" + layer3 + "'");
                        if (layer != layer3) {
                            if (layer3.Length > layer.Length) throw new Exception("Ожидался элемент словаря вместо подъёма");
                            if (!layer.StartsWith(layer3)) throw new Exception("Странность в упавшем layer'е");
                            YAML_Log("Падение"); pos = saved_pos2; break;
                        }

                        res.Append(", ");
                    }

                    var key = YAML_ParseString(ref yaml, ref pos);
                    res.Append('"');
                    res.Append(key);
                    res.Append("\": ");
                    if (yaml[pos++] != ':') throw new Exception("После ключа ожидалось ':'");

                    char c = yaml[pos++];
                    if (c == ' ') {
                        var value = YAML_ParseItem(ref yaml, ref pos);
                        res.Append(value);
                    } else if (c == '\n') {
                    } else throw new Exception("После ключа и ':' ожидалось ' ', либо '\n'");

                    int saved_pos = pos;
                    var layer2 = YAML_ParseLayer(ref yaml, ref pos);
                    YAML_Log("LAYER: '" + layer + "', '" + layer2 + "'");
                    if (layer2.Length < layer.Length) {
                        if (!layer.StartsWith(layer2)) throw new Exception("Странность в упавшем layer'е");
                        YAML_Log("Падение"); pos = saved_pos; break;
                    }
                    if (!layer2.StartsWith(layer)) throw new Exception("Странность в следующем layer'е");
                    if (layer == layer2) { YAML_Log("Сохранение"); pos = saved_pos; continue; }
                    YAML_Log("Подъём");
                    if (c == '\n') {
                        pos = saved_pos;
                        var value = YAML_ToJSONHandler(ref yaml, ref pos);
                        res.Append(value);
                    } else throw new Exception("Здесь не может быть подъёма");
                }
                res.Append('}');
                YAML_Log("Словарь рождён: " + res.ToString(), 2);
                return res.ToString(); }
            }
        }
        public static string Yaml2json(string yaml) {
            try {
                yaml = yaml.Replace("\r", "");
                if (!yaml.StartsWith("---\n")) throw new Exception("Это не YAML");
                int pos = 4;
                var res = YAML_ToJSONHandler(ref yaml, ref pos);
                YAML_Log("data: " + res, 3);
                return res;
            } catch (Exception e) { Log.Write("Ошибка YAML парсера: " + e); throw; }
        }

        /*
         * Misc
         */

        public static string? Obj2xml(object? obj) => Json2xml(Obj2json(obj)); // Чёт припомнилось свойство транзитивности с дискретной матеши...
        public static object? Xml2obj(string xml) => Json2obj(Xml2json(xml));
        public static string? Obj2yaml(object? obj) => Json2yaml(Obj2json(obj));
        public static object? Yaml2obj(string xml) => Json2obj(Yaml2json(xml));

        public static void RenderToFile(Control target, string path) {
            // var target = (Control?) tar.Parent;
            // if (target == null) return;

            double w = target.Bounds.Width, h = target.Bounds.Height;
            var pixelSize = new PixelSize((int) w, (int) h);
            var size = new Size(w, h);
            using RenderTargetBitmap bitmap = new(pixelSize);
            target.Measure(size);
            target.Arrange(new Rect(size));
            bitmap.Render(target);
            bitmap.Save(path);
        }

        public static string TrimAll(this string str) { // Помимо пробелов по бокам, убирает повторы пробелов внутри
            StringBuilder sb = new();
            for (int i = 0; i < str.Length; i++) {
                if (i > 0 && str[i] == ' ' && str[i - 1] == ' ') continue;
                sb.Append(str[i]);
            }
            return sb.ToString().Trim();
        }

        // Странно, почему оригинальный Split() работает, как обычный Split(' '),
        // ведь во всех языках (по крайней мере в тех, которые я видел до C#) он
        // игнорирует добавления в ответ пустых строк.
        public static string[] NormSplit(this string str) => str.TrimAll().Split(' ');

        public static string GetStackInfo() {
            var st = new StackTrace();
            var sb = new StringBuilder();
            for (int i = 1; i < 11; i++) {
                var frame = st.GetFrame(i);
                if (frame == null) continue;

                var method = frame.GetMethod();
                if (method == null || method.ReflectedType == null) continue;

                sb.Append(method.ReflectedType.Name + " " + method.Name + " | ");
                if (i == 5) sb.Append("\n    ");
            }
            return sb.ToString();
        }

        public static int Normalize(this int num, int min, int max) {
            if (num < min) return min;
            if (num > max) return max;
            return num;
        }
        public static double Normalize(this double num, double min, double max) {
            if (num < min) return min;
            if (num > max) return max;
            return num;
        }

        public static double Hypot(this Point delta) {
            return Math.Sqrt(Math.Pow(delta.X, 2) + Math.Pow(delta.Y, 2));
        }
        public static double Hypot(this Point A, Point B) {
            Point delta = A - B;
            return Math.Sqrt(Math.Pow(delta.X, 2) + Math.Pow(delta.Y, 2));
        }

        public static double? ToDouble(this object num) {
            return num switch {
                int @int => @int,
                long @long => @long,
                double @double => @double,
                _ => null,
            };
        }

        public static int Min(this int A, int B) => A < B ? A : B;
        public static int Max(this int A, int B) => A > B ? A : B;
        public static double Min(this double A, double B) => A < B ? A : B;
        public static double Max(this double A, double B) => A > B ? A : B;

        public static void Remove(this Control item) {
            var p = (Panel?) item.Parent; // Именно Panel и добавляет понятие Children ;'-}}}}}}}}}}
            p?.Children.Remove(item);
        }

        public static Point Center(this Visual item, Visual? parent) {
            var tb = item.TransformedBounds;
            if (tb == null) return new(); // Обычно так не бывает
            var bounds = tb.Value.Bounds.TransformToAABB(tb.Value.Transform);
            var res = bounds.Center;
            if (parent == null) return res; // parent в качестве точки отсчёта, например холст

            var tb2 = parent.TransformedBounds;
            if (tb2 == null) return res; // Обычно так не бывает
            var bounds2 = tb2.Value.Bounds.TransformToAABB(tb2.Value.Transform);
            return res - bounds2.TopLeft;
        }

        public static DateTime UnixTimeStampToDateTime(this long unixTimeStamp) {
            DateTime dateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }
        public static string UnixTimeStampToString(this long unixTimeStamp) {
            return UnixTimeStampToDateTime(unixTimeStamp).ToString("yyyy/MM/dd H:mm:ss");
        }
        
        /*
         * SQLite_proj абилка
         */

        private static string Repr(this double d) => d.ToString().Replace(',', '.');
        private static string Repr(this double? d) => d is not double value ? "0" : value.Repr();
        private static string Repr(this Point? p) => p is not Point value ? "0, 0" : $"{value.X.Repr()}, {value.Y.Repr()}";
        private static string Repr(this Size? p) => p is not Size value ? "0, 0" : $"{value.Width.Repr()}, {value.Height.Repr()}";

        internal static void Obj2sqlite_proj(Project proj, string path) {
            // Log.Write(Obj2yaml(proj.Export()) + "");
            using var con = new SQLiteConnection("Data Source=" + path);

            con.Open();
            if (con.State != ConnectionState.Open) { Log.Write("Не удалось открыть SQLite: " + con.State); return; }

            /* string comm = @"
CREATE TABLE header (
    name     TEXT    NOT NULL,
    created  INTEGER NOT NULL,
    modified INTEGER NOT NULL,
    schemes  INTEGER NOT NULL
);";
            new SQLiteCommand(comm, con).ExecuteReader().Close();

            comm = @"
CREATE TABLE items (
    id        INTEGER NOT NULL,
    pos_x     REAL    NOT NULL,
    pos_y     REAL    NOT NULL,
    size_x    REAL    NOT NULL,
    size_y    REAL    NOT NULL,
    base_size REAL    NOT NULL,
    extra     TEXT    NOT NULL
);";
            new SQLiteCommand(comm, con).ExecuteReader().Close();

            comm = @"
CREATE TABLE joins (
    num_a INTEGER NOT NULL,
    pin_a INTEGER NOT NULL,
    tag_a TEXT    NOT NULL,
    num_b INTEGER NOT NULL,
    pin_b INTEGER NOT NULL,
    tag_b TEXT    NOT NULL
);";
            new SQLiteCommand(comm, con).ExecuteReader().Close();
            return; Так создал базу с пустыми DB... через dead-code после этого return ;'-}

            Внутри FileHandler.cs теперь лежит эта пустая база-данных, но с генерированными таблицами,
            по этому сохранение ускорится где-то в 3 раза ;'-}
            Хотя не, ничего не поменялось))) Всё равно это самый тормознутый формат хранения данных :/
            Ибо загрузка таблиц банально текстом вшивается в файл, из-за чего трудоёмкость ни на планковскую частицу не смещается XD
            */

            string comm = $"INSERT INTO header VALUES ('{proj.Name}', {proj.Created}, {proj.Modified}, {proj.schemes.Count});";
            new SQLiteCommand(comm, con).ExecuteReader().Close();

            int n = 0;
            foreach (var scheme in proj.schemes) {
                List<string> res = new();
                foreach (var item in scheme.items) {
                    if (item is not Dictionary<string, object> @dict) { Log.Write("Не верный тип элемента: " + item); continue; }
                    int? item_id = null;
                    Point? pos = null;
                    Size? size = null;
                    double? base_size = null;
                    List<string> extra = new();
                    foreach (var pair in dict) {
                        object value = pair.Value;
                        switch (pair.Key) {
                        case "id":
                            if (value is int @id) item_id = @id;
                            else Log.Write("Неверный тип id-записи элемента: " + value);
                            break;
                        case "pos":
                            if (value is Point _pos) pos = _pos;
                            else Log.Write("Неверный тип pos-записи элемента: " + value);
                            break;
                        case "base_size":
                            double? b_size = value.ToDouble();
                            if (b_size != null) base_size = (double) b_size;
                            else Log.Write("Неверный тип base_size-записи элемента: " + value);
                            break;
                        case "size":
                            if (value is Size _size) size = _size;
                            else Log.Write("Неверный тип size-записи элемента: " + value);
                            break;
                        default:
                            value = value switch {
                                bool @bool => "$" + (@bool ? "1" : "0"),
                                string @str => "s" + @str,
                                _ => throw new Exception("Не известный тип extra-данных: " + value.GetType().Name),
                            };
                            extra.Add(pair.Key + "=" + value);
                            break;
                        }
                    }
                    if (item_id == null) { Log.Write("Не обнаружена id-запись элемента схемы :/"); continue; }
                    if (pos == null) { Log.Write("Не обнаружена pos-запись элемента схемы :/"); continue; }
                    if (size == null) { Log.Write("Не обнаружена size-запись элемента схемы :/"); continue; }
                    if (base_size == null) { Log.Write("Не обнаружена base_size-запись элемента схемы :/"); continue; }

                    string extra_s = string.Join('&', extra);
                    res.Add($"({item_id}, {pos.Repr()}, {size.Repr()}, {base_size.Repr()}, '{extra_s}')");
                }
                res.Add($"(-1234, {n}, {n}, {n}, {n}, {n}, '{scheme.states}')");

                comm = $"INSERT INTO items VALUES {string.Join(", ", res)};";
                // Log.Write(comm);
                // File.WriteAllText("../../../check.txt", comm);
                new SQLiteCommand(comm, con).ExecuteReader().Close();

                res.Clear();
                foreach (var obj in scheme.joins) {
                    object[] join;
                    if (obj is List<object> @j) join = @j.ToArray();
                    else if (obj is object[] @j2) join = @j2;
                    else { Log.Write("Одно из соединений не того типа: " + obj + " " + Obj2json(obj)); continue; }
                    if (join.Length != 6 ||
                        join[0] is not int @num_a || join[1] is not int @pin_a || join[2] is not string @tag_a ||
                        join[3] is not int @num_b || join[4] is not int @pin_b || join[5] is not string @tag_b) { Log.Write("Содержимое списка соединения ошибочно"); continue; }

                    res.Add($"({@num_a}, {@pin_a}, '{@tag_a}', {@num_b}, {@pin_b}, '{@tag_b}')");
                }
                res.Add($"(-1234, {n++}, '', {scheme.Created}, {scheme.Modified}, '{scheme.Name}')");

                comm = $"INSERT INTO joins VALUES {string.Join(", ", res)};";
                // Log.Write(comm);
                new SQLiteCommand(comm, con).ExecuteReader().Close();
            }

            con.Dispose();
        }

        internal static object SQLite_proj2obj(string path) {
            using var con = new SQLiteConnection("Data Source=" + path);
            con.Open();
            if (con.State != ConnectionState.Open) throw new Exception("Не удалось открыть SQLite: " + con.State);

            StringBuilder sb = new();
            object res;
            List<Dictionary<string, object>> schemes = new();

            var sql_comm = new SQLiteCommand("SELECT * FROM header", con);
            sql_comm.ExecuteNonQuery();
            using (var reader = sql_comm.ExecuteReader()) {
                if (!reader.HasRows) throw new Exception("Не вышло считать заголовочную таблицу SQLite :/");
                if (!reader.Read()) throw new Exception("Заголовочная таблица пустует");

                var row = Enumerable.Range(0, reader.VisibleFieldCount).Select(x => reader[x]).ToArray();
                // Log.Write("row: " + Obj2json(row));

                long count = (long) row[3];
                List<object> @void = new();

                for (int i = 0; i < count; i++)
                    schemes.Add(new() {
                        ["name"] = "Новая схема",
                        ["created"] = 0,
                        ["modified"] = 0,
                        ["items"] = @void,
                        ["joins"] = @void,
                        ["states"] = "",
                    });

                res = new Dictionary<string, object> {
                    ["name"] = row[0],
                    ["created"] = (int) (long) row[1],
                    ["modified"] = (int) (long) row[2],
                    ["schemes"] = schemes.Cast<object>().ToList(),
                };
            }

            using (var reader = new SQLiteCommand("SELECT * FROM items", con).ExecuteReader()) {
                if (!reader.HasRows) throw new Exception("Не вышло считать таблицу элементов :/");
                int n = 0;
                List<object> items = new();
                while (reader.Read()) {
                    var row = Enumerable.Range(0, reader.VisibleFieldCount).Select(x => reader[x]).ToArray();
                    // Log.Write("item: " + Obj2json(row));

                    int id = (int) (long) row[0];
                    string extra = (string) row[6];

                    if (id == -1234) {
                        var scheme = schemes[n++];
                        scheme["items"] = items;
                        scheme["states"] = extra;
                        items = new();
                        continue;
                    }

                    var item = new Dictionary<string, object> {
                        ["id"] = id,
                        ["pos"] = new Point((double) row[1], (double) row[2]),
                        ["size"] = new Size((double) row[3], (double) row[4]),
                        ["base_size"] = (double) row[5],
                    };
                    foreach (var pair in extra.Split('&')) {
                        if (pair == "") continue;
                        string[] kv = pair.Split('=');
                        string value = kv[1], data = value[1..];
                        char type = value[0];
                        object yeah = type switch {
                            '$' => data == "1" || (data == "0" ? false : throw new Exception($"Плохая extra (bool): {value}")),
                            's' => data,
                            _ => throw new Exception($"Не известный тип extra-данных: '{type}'. Сами данные: {data}"),
                        };
                        item[kv[0]] = yeah;
                    }

                    items.Add(item);
                }
            }

            using (var reader = new SQLiteCommand("SELECT * FROM joins", con).ExecuteReader()) {
                if (!reader.HasRows) throw new Exception("Не вышло считать таблицу соединений :/");

                int n = 0;
                List<object> joins = new();
                while (reader.Read()) {
                    var row = Enumerable.Range(0, reader.VisibleFieldCount).Select(x => reader[x]).ToArray();
                    // Log.Write("join: " + Obj2json(row));

                    int num_a = (int) (long) row[0];
                    int pin_a = (int) (long) row[1];
                    var tag_a = (string) row[2];
                    int num_b = (int) (long) row[3];
                    int pin_b = (int) (long) row[4];
                    var tag_b = (string) row[5];
                    
                    if (num_a == -1234) {
                        var scheme = schemes[n++];
                        scheme["joins"] = joins;
                        scheme["created"] = num_b;
                        scheme["modified"] = pin_b;
                        scheme["name"] = tag_b;
                        joins = new();
                        continue;
                    }

                    var join = new object[] { num_a, pin_a, tag_a, num_b, pin_b, tag_b };
                    joins.Add(join);
                }
            }
            // File.WriteAllText("../../../LOL.json", Obj2json(res));
            // Log.Write(Obj2yaml(res) ?? "");

            con.Dispose();
            return res;
        }
    }
}
