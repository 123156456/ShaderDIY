using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ShaderDIY;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class SdiyParser
    {
        static readonly Regex PropertyRegex = new Regex(
            @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(\s*""(?<disp>[^""]*)""\s*,\s*(?<type>.+?)\s*\)\s*=\s*(?<def>.+)$",
            RegexOptions.Compiled);

        static readonly Regex SlotRegex = new Regex(
            @"\[SLOT\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<rest>[^\]]*)\]",
            RegexOptions.Compiled);

        static readonly Regex HeaderArgRegex = new Regex(
            @"(?<k>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<v>[^""]*)""",
            RegexOptions.Compiled);

        public static SdiyDocument Parse(string text, string sourcePath)
        {
            var doc = new SdiyDocument { sourcePath = sourcePath };
            if (string.IsNullOrEmpty(text))
            {
                doc.parseError = "空文件";
                return doc;
            }

            try
            {
                var trimmed = StripCommentsOutsideHlsl(text).Trim();
                if (trimmed.StartsWith("Module", StringComparison.Ordinal))
                    ParseModule(trimmed, doc);
                else if (trimmed.StartsWith("ShaderDIY", StringComparison.Ordinal))
                    ParseHost(trimmed, doc);
                else
                    doc.parseError = "文件必须以 ShaderDIY 或 Module 开头";
            }
            catch (Exception e)
            {
                doc.parseError = e.Message;
            }

            return doc;
        }

        static void ParseHost(string text, SdiyDocument doc)
        {
            doc.kind = SdiyAssetKind.Host;
            int brace = text.IndexOf('{');
            if (brace < 0)
                throw new Exception("缺少 Host 主体 { }");
            doc.name = ExtractQuoted(text, "ShaderDIY") ?? "ShaderDIY/Unnamed";
            var body = SliceBlock(text, brace);
            ParseBody(body, doc);
        }

        static void ParseModule(string text, SdiyDocument doc)
        {
            doc.kind = SdiyAssetKind.Module;
            int brace = text.IndexOf('{');
            if (brace < 0)
                throw new Exception("缺少 Module 主体 { }");
            var header = text.Substring(0, brace);
            doc.name = ExtractQuoted(header, "Module") ?? "UnnamedModule";
            foreach (Match m in HeaderArgRegex.Matches(header))
            {
                var k = m.Groups["k"].Value;
                var v = m.Groups["v"].Value;
                if (k == "Slot") doc.slot = v;
                else if (k == "Category") doc.category = v;
            }
            var body = SliceBlock(text, brace);
            ParseBody(body, doc);
        }

        static void ParseBody(string body, SdiyDocument doc)
        {
            ParseNamedBlock(body, "Meta", inner => ParseMeta(inner, doc));
            ParseNamedBlock(body, "Features", inner => ParsePropertyBlock(inner, doc.features, true));
            ParseNamedBlock(body, "Properties", inner => ParsePropertyBlock(inner, doc.properties, false));
            ParseNamedBlock(body, "Keywords", inner => ParseKeywords(inner, doc));
            ParseNamedBlock(body, "Cost", inner => ParseCost(inner, doc));
            ParseNamedBlock(body, "Formula", inner => { doc.formula = inner.Trim(); });
            ParseNamedBlock(body, "HLSLINCLUDE", inner => { doc.hlslInclude = inner; });
            ParseNamedBlock(body, "HLSL", inner =>
            {
                if (doc.kind == SdiyAssetKind.Module)
                    doc.hlslInclude = inner;
            });

            foreach (Match m in SlotRegex.Matches(body))
            {
                var slot = new SdiySlot { name = m.Groups["name"].Value };
                var rest = m.Groups["rest"].Value;
                foreach (Match arg in HeaderArgRegex.Matches(rest))
                {
                    var k = arg.Groups["k"].Value;
                    var v = arg.Groups["v"].Value;
                    if (k == "Default") slot.defaultModule = v;
                    else if (k == "Signature") slot.signature = v;
                    else if (k == "Category") slot.category = v;
                    else if (k == "Kind") slot.kind = v;
                }
                doc.slots.Add(slot);
            }

            int search = 0;
            while (true)
            {
                int passIdx = IndexOfIdentifier(body, "Pass", search);
                if (passIdx < 0)
                    break;
                int brace = body.IndexOf('{', passIdx);
                if (brace < 0)
                    break;
                var header = body.Substring(passIdx, brace - passIdx);
                var inner = SliceBlock(body, brace);
                var pass = new SdiyPass
                {
                    name = ExtractQuoted(header, "Pass") ?? "Forward",
                    hlsl = ExtractNamedBlock(inner, "HLSL"),
                    tags = ExtractNamedBlock(inner, "Tags"),
                    states = inner
                };
                doc.passes.Add(pass);
                search = MatchingBrace(body, brace) + 1;
            }
        }

        static void ParseMeta(string inner, SdiyDocument doc)
        {
            foreach (var rawLine in inner.Split('\n'))
            {
                var line = rawLine.Trim().TrimEnd(';');
                if (string.IsNullOrEmpty(line) || line.StartsWith("//", StringComparison.Ordinal))
                    continue;
                int eq = line.IndexOf('=');
                if (eq < 0)
                    continue;
                var key = line.Substring(0, eq).Trim();
                var val = Unquote(line.Substring(eq + 1).Trim());
                if (key.Equals("Category", StringComparison.OrdinalIgnoreCase))
                    doc.category = val;
                else if (key.Equals("Pipeline", StringComparison.OrdinalIgnoreCase))
                    doc.pipeline = val;
                else if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    doc.description = val;
                else if (key.Equals("CompanionScripts", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var p in val.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var s = p.Trim();
                        if (s.Length > 0)
                            doc.companionScripts.Add(s);
                    }
                }
            }
        }

        static void ParseKeywords(string inner, SdiyDocument doc)
        {
            foreach (var raw in inner.Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var k = raw.Trim();
                if (k.Length == 0 || k.StartsWith("//", StringComparison.Ordinal))
                    continue;
                doc.keywords.Add(k);
            }
        }

        static void ParseCost(string inner, SdiyDocument doc)
        {
            foreach (var rawLine in inner.Split('\n'))
            {
                var line = rawLine.Trim();
                int eq = line.IndexOf('=');
                if (eq < 0)
                    continue;
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                int n;
                if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    continue;
                if (key.Equals("ALU", StringComparison.OrdinalIgnoreCase)) doc.cost.alu = n;
                else if (key.Equals("Texture", StringComparison.OrdinalIgnoreCase)) doc.cost.texture = n;
                else if (key.Equals("Interpolator", StringComparison.OrdinalIgnoreCase)) doc.cost.interpolator = n;
            }
        }

        static void ParsePropertyBlock(string inner, List<SdiyProperty> list, bool asFeature)
        {
            var pending = new List<string>();
            var lines = inner.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//", StringComparison.Ordinal))
                    continue;
                if ((line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("@", StringComparison.Ordinal)) &&
                    !line.StartsWith("#include", StringComparison.OrdinalIgnoreCase))
                {
                    pending.Add("Tab " + line.Substring(1).Trim().Trim('"', '\'', '{', '}'));
                    continue;
                }

                while (line.StartsWith("[", StringComparison.Ordinal))
                {
                    int close = line.IndexOf(']');
                    if (close < 0)
                        break;
                    pending.Add(line.Substring(1, close - 1).Trim());
                    line = line.Substring(close + 1).Trim();
                }

                if (string.IsNullOrEmpty(line))
                    continue;

                var m = PropertyRegex.Match(line);
                if (!m.Success)
                {
                    pending.Clear();
                    continue;
                }

                var prop = new SdiyProperty
                {
                    name = m.Groups["name"].Value,
                    displayName = m.Groups["disp"].Value,
                    type = m.Groups["type"].Value.Trim(),
                    defaultValue = m.Groups["def"].Value.Trim().TrimEnd(';'),
                    isFeature = asFeature,
                    region = asFeature ? PanelRegion.Features : PanelRegion.Auto
                };

                ApplyAttributes(prop, pending);
                pending.Clear();

                if (prop.region == PanelRegion.Auto)
                    prop.region = GuessRegion(prop);

                list.Add(prop);
            }
        }

        static void ApplyAttributes(SdiyProperty prop, List<string> attrs)
        {
            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a.StartsWith("Region", StringComparison.OrdinalIgnoreCase))
                {
                    var n = ExtractParenInt(a, 0);
                    if (n == 1) prop.region = PanelRegion.Features;
                    else if (n == 2) prop.region = PanelRegion.Textures;
                    else if (n == 3) prop.region = PanelRegion.Parameters;
                    else if (n == 4) prop.region = PanelRegion.Performance;
                }
                else if (a.StartsWith("TexOps", StringComparison.OrdinalIgnoreCase))
                {
                    prop.texOps = ParseTexOps(a);
                    if (prop.region == PanelRegion.Auto)
                        prop.region = PanelRegion.Textures;
                }
                else if (a.StartsWith("ShowIf", StringComparison.OrdinalIgnoreCase))
                {
                    var inner = ExtractParen(a);
                    var parts = inner.Split(',');
                    if (parts.Length >= 1)
                        prop.showIfProperty = parts[0].Trim();
                    if (parts.Length >= 2)
                        prop.showIfValue = parts[1].Trim();
                }
                else if (a.Equals("Invert", StringComparison.OrdinalIgnoreCase) || a.Equals("OneMinus", StringComparison.OrdinalIgnoreCase))
                {
                    prop.invertOp = true;
                }
                else if (a.StartsWith("Tooltip", StringComparison.OrdinalIgnoreCase))
                {
                    var t = Unquote(ExtractParen(a));
                    if (string.IsNullOrEmpty(t))
                    {
                        int sp = a.IndexOf(' ');
                        t = sp >= 0 ? Unquote(a.Substring(sp + 1).Trim()) : "";
                    }
                    if (!string.IsNullOrEmpty(t))
                        prop.tooltip = t;
                }
                else if (a.StartsWith("Tab", StringComparison.OrdinalIgnoreCase) ||
                         a.StartsWith("Page", StringComparison.OrdinalIgnoreCase) ||
                         a.StartsWith("Header", StringComparison.OrdinalIgnoreCase))
                {
                    var name = Unquote(ExtractParen(a));
                    if (string.IsNullOrEmpty(name))
                    {
                        int sp = a.IndexOf(' ');
                        name = sp >= 0 ? Unquote(a.Substring(sp + 1).Trim()) : "";
                    }
                    if (!string.IsNullOrEmpty(name))
                        prop.tabName = name;
                    if (prop.region == PanelRegion.Auto)
                        prop.region = PanelRegion.Textures;
                }
                else
                {
                    prop.extraAttributes.Add(a);
                }
            }
        }

        static TexOps ParseTexOps(string attr)
        {
            var inner = ExtractParen(attr);
            if (string.IsNullOrEmpty(inner) || inner.IndexOf("All", StringComparison.OrdinalIgnoreCase) >= 0)
                return TexOps.All;
            TexOps ops = TexOps.None;
            if (inner.IndexOf("Tiling", StringComparison.OrdinalIgnoreCase) >= 0) ops |= TexOps.TilingOffset;
            if (inner.IndexOf("Channel", StringComparison.OrdinalIgnoreCase) >= 0) ops |= TexOps.Channel;
            if (inner.IndexOf("Invert", StringComparison.OrdinalIgnoreCase) >= 0) ops |= TexOps.Invert;
            if (inner.IndexOf("Mip", StringComparison.OrdinalIgnoreCase) >= 0) ops |= TexOps.MipBias;
            return ops == TexOps.None ? TexOps.All : ops;
        }

        static PanelRegion GuessRegion(SdiyProperty prop)
        {
            if (prop.isFeature)
                return PanelRegion.Features;
            if (prop.IsTexture)
                return PanelRegion.Textures;
            return PanelRegion.Parameters;
        }

        static string ExtractQuoted(string text, string keyword)
        {
            int k = IndexOfIdentifier(text, keyword, 0);
            if (k < 0)
                return null;
            int q = text.IndexOf('"', k);
            if (q < 0)
                return null;
            int q2 = text.IndexOf('"', q + 1);
            if (q2 < 0)
                return null;
            return text.Substring(q + 1, q2 - q - 1);
        }

        static string Unquote(string v)
        {
            v = v.Trim().TrimEnd(';');
            if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"')
                return v.Substring(1, v.Length - 2);
            return v;
        }

        static string ExtractParen(string attr)
        {
            int a = attr.IndexOf('(');
            int b = attr.LastIndexOf(')');
            if (a < 0 || b <= a)
                return string.Empty;
            return attr.Substring(a + 1, b - a - 1).Trim();
        }

        static int ExtractParenInt(string attr, int fallback)
        {
            var inner = ExtractParen(attr);
            if (string.IsNullOrEmpty(inner))
            {
                var parts = attr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    int n;
                    if (int.TryParse(parts[1], out n))
                        return n;
                }
                return fallback;
            }
            int v;
            return int.TryParse(inner, out v) ? v : fallback;
        }

        static void ParseNamedBlock(string body, string name, Action<string> onInner)
        {
            var inner = ExtractNamedBlock(body, name);
            if (inner != null)
                onInner(inner);
        }

        static string ExtractNamedBlock(string body, string name)
        {
            int idx = IndexOfIdentifier(body, name, 0);
            if (idx < 0)
                return null;
            int brace = body.IndexOf('{', idx);
            if (brace < 0)
                return null;
            int identEnd = idx + name.Length;
            var between = body.Substring(identEnd, brace - identEnd).Trim();
            if (between.Length > 0 && between[0] != '"' && !between.StartsWith("\"", StringComparison.Ordinal))
            {
                if (name != "Pass" && !string.IsNullOrEmpty(between) && char.IsLetterOrDigit(between[0]))
                    return null;
            }
            return SliceBlock(body, brace);
        }

        static int IndexOfIdentifier(string text, string ident, int start)
        {
            int i = start;
            while (i < text.Length)
            {
                int found = text.IndexOf(ident, i, StringComparison.Ordinal);
                if (found < 0)
                    return -1;
                bool leftOk = found == 0 || !IsIdentChar(text[found - 1]);
                int end = found + ident.Length;
                bool rightOk = end >= text.Length || !IsIdentChar(text[end]);
                if (leftOk && rightOk)
                    return found;
                i = found + ident.Length;
            }
            return -1;
        }

        static bool IsIdentChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        static string SliceBlock(string text, int openBrace)
        {
            int close = MatchingBrace(text, openBrace);
            if (close < 0)
                throw new Exception("花括号不匹配");
            return text.Substring(openBrace + 1, close - openBrace - 1);
        }

        public static int MatchingBrace(string s, int openIndex)
        {
            int depth = 0;
            bool inStr = false;
            bool lineComment = false;
            bool blockComment = false;
            for (int i = openIndex; i < s.Length; i++)
            {
                char c = s[i];
                char n = i + 1 < s.Length ? s[i + 1] : '\0';
                if (lineComment)
                {
                    if (c == '\n')
                        lineComment = false;
                    continue;
                }
                if (blockComment)
                {
                    if (c == '*' && n == '/')
                    {
                        blockComment = false;
                        i++;
                    }
                    continue;
                }
                if (inStr)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '"')
                        inStr = false;
                    continue;
                }
                if (c == '/' && n == '/')
                {
                    lineComment = true;
                    i++;
                    continue;
                }
                if (c == '/' && n == '*')
                {
                    blockComment = true;
                    i++;
                    continue;
                }
                if (c == '"')
                {
                    inStr = true;
                    continue;
                }
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        static string StripCommentsOutsideHlsl(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        public static Vector4 ParseVector(string raw, Vector4 fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            raw = raw.Trim();
            if (raw.StartsWith("(", StringComparison.Ordinal))
            {
                var inner = raw.Trim('(', ')', ' ', '\t');
                var parts = inner.Split(',');
                float x = fallback.x, y = fallback.y, z = fallback.z, w = fallback.w;
                if (parts.Length > 0) float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x);
                if (parts.Length > 1) float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
                if (parts.Length > 2) float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
                if (parts.Length > 3) float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out w);
                return new Vector4(x, y, z, w);
            }
            float f;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                return new Vector4(f, f, f, f);
            return fallback;
        }

        public static Color ParseColor(string raw, Color fallback)
        {
            var v = ParseVector(raw, new Vector4(fallback.r, fallback.g, fallback.b, fallback.a));
            return new Color(v.x, v.y, v.z, v.w);
        }

        public static float ParseFloat(string raw, float fallback)
        {
            if (string.IsNullOrEmpty(raw))
                return fallback;
            var first = raw.Trim().Split(new[] { ' ', '{' }, StringSplitOptions.RemoveEmptyEntries);
            if (first.Length == 0)
                return fallback;
            float f;
            return float.TryParse(first[0].TrimEnd(','), NumberStyles.Float, CultureInfo.InvariantCulture, out f)
                ? f
                : fallback;
        }

        public static string[] ParseEnumOptions(string type)
        {
            var inner = type;
            int a = type.IndexOf('(');
            int b = type.LastIndexOf(')');
            if (a >= 0 && b > a)
                inner = type.Substring(a + 1, b - a - 1);
            var parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
                list.Add(parts[i].Trim());
            return list.ToArray();
        }

        public static void ParseRange(string type, out float min, out float max)
        {
            min = 0f;
            max = 1f;
            int a = type.IndexOf('(');
            int b = type.LastIndexOf(')');
            if (a < 0 || b <= a)
                return;
            var inner = type.Substring(a + 1, b - a - 1);
            var parts = inner.Split(',');
            if (parts.Length >= 1)
                float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out min);
            if (parts.Length >= 2)
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out max);
        }
    }
}
