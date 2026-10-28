using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class Region4PerfPanel
    {
        static readonly Color Alu = new Color(0.42f, 0.86f, 0.58f, 1f);
        static readonly Color Tex = new Color(0.45f, 0.72f, 1f, 1f);
        static readonly Color Var = new Color(0.98f, 0.74f, 0.34f, 1f);
        static readonly Color Itr = new Color(0.78f, 0.58f, 0.98f, 1f);
        static readonly Color Load = new Color(0.95f, 0.42f, 0.48f, 1f);
        static readonly Color ChartBg = new Color(0.1f, 0.1f, 0.12f, 1f);
        static readonly Color Grid = new Color(1f, 1f, 1f, 0.06f);
        static readonly Vector3[] Line = new Vector3[ShaderDIYPerfHistory.Capacity];
        static GUIStyle _chip;
        static GUIStyle _num;
        static GUIStyle _formula;
        static GUIStyle _formulaHi;
        static GUIStyle _slotTag;
        static int _seriesMask = 31;
        static Vector2 _formulaScroll;
        static string _lastCombo;

        public static void Draw(Rect rect, ShaderDIYGraph graph, SdiyAsset host)
        {
            ShaderDIYStyles.DrawBox(rect);
            var header = new Rect(rect.x, rect.y, rect.width, 22);
            EditorGUI.DrawRect(header, ShaderDIYStyles.HeaderBg);
            EditorGUI.LabelField(header, ShaderDIYTips.L("性能", ShaderDIYTips.RegionPerf), ShaderDIYStyles.HeaderBar);

            if (graph == null)
            {
                EditorGUI.LabelField(new Rect(rect.x + 8, header.yMax + 8, rect.width - 16, 40), "—", ShaderDIYStyles.Tiny);
                return;
            }

            float split = Mathf.Lerp(rect.yMax, header.yMax, 0.46f);
            var top = new Rect(rect.x, header.yMax, rect.width, split - header.yMax);
            var bottom = new Rect(rect.x, split, rect.width, rect.yMax - split);

            DrawPerfTop(top, graph);
            DrawFormula(bottom, graph, host);
        }

        static void DrawPerfTop(Rect rect, ShaderDIYGraph graph)
        {
            var hist = ShaderDIYPerfHistory.For(graph);
            hist.Push(graph);
            var now = ShaderDIYPerfHistory.FromGraph(graph);
            float pad = 8f;
            float y = rect.y + 6f;
            var score = new Rect(rect.x + pad, y, rect.width - pad * 2, 32);
            DrawScore(score, now);
            y = score.yMax + 6f;
            float remain = rect.yMax - y - 70f;
            var chart = new Rect(rect.x + pad, y, rect.width - pad * 2, Mathf.Clamp(remain, 48f, 78f));
            DrawChart(chart, hist);
            y = chart.yMax + 4f;
            y = DrawLegend(new Rect(rect.x + pad, y, rect.width - pad * 2, 64), now);
            DrawMeta(new Rect(rect.x + pad, y + 2, rect.width - pad * 2, 16), graph);
        }

        static void DrawFormula(Rect rect, ShaderDIYGraph graph, SdiyAsset host)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), new Color(0.28f, 0.28f, 0.32f));
            var title = new Rect(rect.x, rect.y, rect.width, 22);
            EditorGUI.DrawRect(title, ShaderDIYStyles.HeaderBg);
            EditorGUI.LabelField(title, ShaderDIYTips.L("公式", ShaderDIYTips.RegionFormula), ShaderDIYStyles.HeaderBar);

            var inner = new Rect(rect.x + 6, title.yMax + 4, rect.width - 12, rect.height - 28);
            GUILayout.BeginArea(inner);
            _formulaScroll = EditorGUILayout.BeginScrollView(_formulaScroll);
            Ensure();

            string combo = "";
            if (host != null && host.document != null && !string.IsNullOrEmpty(host.document.formula))
            {
                EditorGUILayout.LabelField(Collapse(host.document.formula), _formula);
                EditorGUILayout.Space(4);
            }

            if (host != null && host.document != null && host.document.slots != null)
            {
                for (int i = 0; i < host.document.slots.Count; i++)
                {
                    var slot = host.document.slots[i];
                    var modName = graph.GetSlotModule(slot.name) ?? slot.defaultModule;
                    combo += slot.name + "=" + modName + ";";
                    var mod = ModuleRegistry.FindModule(slot.name, modName);
                    bool hi = _lastCombo != null && _lastCombo.IndexOf(slot.name + "=" + modName, System.StringComparison.Ordinal) < 0;
                    DrawSlotFormula(slot.name, modName, mod, hi);
                }
            }
            else
            {
                EditorGUILayout.LabelField("当前大类没有可替换项。", ShaderDIYStyles.Tiny);
            }

            _lastCombo = combo;

            if (!string.IsNullOrEmpty(graph.lastCompileError))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(FirstLine(graph.lastCompileError), ShaderDIYStyles.Error);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void DrawSlotFormula(string slot, string modName, SdiyAsset mod, bool highlight)
        {
            EditorGUILayout.BeginVertical(highlight ? "selectionRect" : "box");
            EditorGUILayout.LabelField(ShaderDIYTips.L(SlotTitle(slot) + "  ·  " + modName, ShaderDIYTips.SlotHead(slot)), highlight ? _formulaHi : _slotTag);
            var fx = mod != null && mod.document != null ? Collapse(mod.document.formula) : "";
            if (string.IsNullOrEmpty(fx))
                fx = "（未写 Formula）";
            EditorGUILayout.LabelField(fx, highlight ? _formulaHi : _formula);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        static string SlotTitle(string slot)
        {
            switch (slot)
            {
                case "DirectDiffuse": return "Dd 直接漫反";
                case "DirectSpecular": return "Ds 直接高光";
                case "IndirectDiffuse": return "Id 环境漫反";
                case "IndirectSpecular": return "Is 环境高光";
                case "NDF": return "D";
                case "G": return "G";
                case "F": return "F";
                case "F0": return "F0";
                case "Transmittance": return "T 透射率";
                case "InScattering": return "S 散射";
                case "Light": return "L 光向量";
                case "LayerSSS": return "SSS";
                case "LayerParallax": return "视差";
                case "LayerRefraction": return "折射";
                case "MainTex": return "颜色贴图";
                case "Mask": return "遮罩";
                case "Noise": return "Noise";
                case "Emission": return "自发光";
                case "Rim": return "边缘光";
                case "Outline": return "描边";
                case "Clip": return "裁切";
                case "Blend": return "混合";
                case "Soft": return "软粒子";
                default: return slot;
            }
        }

        static string Collapse(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            return s.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        static void DrawScore(Rect r, ShaderDIYPerfHistory.Sample now)
        {
            EditorGUI.DrawRect(r, new Color(0.145f, 0.145f, 0.16f, 1f));
            ShaderDIYStyles.DrawBorder(r, new Color(0.3f, 0.3f, 0.34f, 0.55f));
            int pct = Mathf.RoundToInt(now.load * 100f);
            var tone = Color.Lerp(Alu, Load, Mathf.Clamp01(now.load * 1.2f));
            Ensure();
            EditorGUI.LabelField(new Rect(r.x + 8, r.y + 3, r.width - 16, 14), ShaderDIYTips.L("负载", ShaderDIYTips.Score), _chip);
            var num = new GUIStyle(_num) { normal = { textColor = tone } };
            EditorGUI.LabelField(new Rect(r.x + 8, r.y + 14, r.width - 16, 20), pct + "%", num);
            ShaderDIYTips.Hover(r, ShaderDIYTips.Score);
            var bar = new Rect(r.x + r.width * 0.42f, r.y + 14, r.width * 0.52f, 8);
            EditorGUI.DrawRect(bar, new Color(0.08f, 0.08f, 0.09f, 1f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(now.load), bar.height), tone);
        }

        static void DrawChart(Rect r, ShaderDIYPerfHistory hist)
        {
            EditorGUI.DrawRect(r, ChartBg);
            ShaderDIYStyles.DrawBorder(r, new Color(0.28f, 0.28f, 0.32f, 0.8f));
            ShaderDIYTips.Hover(r, ShaderDIYTips.Chart);
            if (Event.current.type != EventType.Repaint)
                return;

            for (int i = 1; i <= 3; i++)
            {
                float gy = r.y + r.height * i / 4f;
                EditorGUI.DrawRect(new Rect(r.x + 1, gy, r.width - 2, 1), Grid);
            }

            if (hist.Count < 2)
            {
                DrawSeriesLine(r, hist, 4, Load, 1f, true);
                return;
            }

            DrawSeriesLine(r, hist, 4, new Color(Load.r, Load.g, Load.b, 0.18f), 6f, true);
            if ((_seriesMask & 16) != 0) DrawSeriesLine(r, hist, 4, Load, 2.6f, false);
            if ((_seriesMask & 1) != 0) DrawSeriesLine(r, hist, 0, Alu, 1.8f, false);
            if ((_seriesMask & 2) != 0) DrawSeriesLine(r, hist, 1, Tex, 1.8f, false);
            if ((_seriesMask & 4) != 0) DrawSeriesLine(r, hist, 2, Var, 1.8f, false);
            if ((_seriesMask & 8) != 0) DrawSeriesLine(r, hist, 3, Itr, 1.8f, false);
        }

        static void DrawSeriesLine(Rect r, ShaderDIYPerfHistory hist, int series, Color col, float width, bool fill)
        {
            int n = Mathf.Max(1, hist.Count);
            int used = 0;
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0.5f : i / (float)(n - 1);
                float v = Normalize(hist.Get(i), series);
                Line[used++] = new Vector3(r.x + 4 + (r.width - 8) * t, r.yMax - 4 - (r.height - 8) * v, 0f);
            }
            if (fill && used > 0)
            {
                Handles.color = col;
                for (int i = 0; i < used; i++)
                {
                    float h = r.yMax - 3 - Line[i].y;
                    if (h > 1f)
                        EditorGUI.DrawRect(new Rect(Line[i].x - 1f, Line[i].y, 2.2f, h), col);
                }
                return;
            }
            if (used < 2)
                return;
            Handles.color = col;
            Handles.DrawAAPolyLine(width, Slice(used));
        }

        static Vector3[] Slice(int used)
        {
            if (used == Line.Length)
                return Line;
            var pts = new Vector3[used];
            for (int i = 0; i < used; i++)
                pts[i] = Line[i];
            return pts;
        }

        static float Normalize(ShaderDIYPerfHistory.Sample s, int series)
        {
            switch (series)
            {
                case 0: return Mathf.Clamp01(s.alu / 80f);
                case 1: return Mathf.Clamp01(s.tex / 8f);
                case 2: return Mathf.Clamp01(s.variants / 16f);
                case 3: return Mathf.Clamp01(s.interpolators / 12f);
                default: return Mathf.Clamp01(s.load);
            }
        }

        static float DrawLegend(Rect r, ShaderDIYPerfHistory.Sample now)
        {
            float y = r.y;
            y += Chip(new Rect(r.x, y, r.width, 16), 0, "ALU", ShaderDIYTips.Alu, now.alu, Alu);
            y += Chip(new Rect(r.x, y, r.width, 16), 1, "TEX", ShaderDIYTips.Tex, now.tex, Tex);
            y += Chip(new Rect(r.x, y, r.width, 16), 2, "变体", ShaderDIYTips.Variants, now.variants, Var);
            y += Chip(new Rect(r.x, y, r.width, 16), 3, "插值", ShaderDIYTips.Interpolators, now.interpolators, Itr);
            return y;
        }

        static float Chip(Rect r, int bit, string name, string tip, float value, Color col)
        {
            bool on = (_seriesMask & (1 << bit)) != 0;
            var dot = new Rect(r.x, r.y + 4, 8, 8);
            EditorGUI.DrawRect(dot, on ? col : new Color(col.r, col.g, col.b, 0.25f));
            Ensure();
            var label = new Rect(r.x + 12, r.y, r.width * 0.45f, r.height);
            var val = new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, r.height);
            EditorGUI.LabelField(label, ShaderDIYTips.L(name, tip), _chip);
            ShaderDIYTips.Hover(r, tip);
            var ns = new GUIStyle(_num) { fontSize = 10, alignment = TextAnchor.MiddleRight, normal = { textColor = on ? Color.white : new Color(1, 1, 1, 0.35f) } };
            EditorGUI.LabelField(val, Mathf.RoundToInt(value).ToString(), ns);
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                _seriesMask ^= 1 << bit;
                if (_seriesMask == 0)
                    _seriesMask = 1 << bit;
                Event.current.Use();
            }
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            return r.height;
        }

        static void DrawMeta(Rect r, ShaderDIYGraph graph)
        {
            Ensure();
            string pass = graph.generatedShader != null ? graph.generatedShader.passCount.ToString() : "—";
            EditorGUI.LabelField(r, ShaderDIYTips.L("编译  " + graph.lastCompileMs.ToString("0.0") + " ms    Pass  " + pass, ShaderDIYTips.CompileMeta), _chip);
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            int n = s.IndexOf('\n');
            return n < 0 ? s : s.Substring(0, n);
        }

        static void Ensure()
        {
            if (_chip != null)
                return;
            _chip = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.62f, 0.64f, 0.68f) }
            };
            _num = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };
            _formula = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                fontSize = 10,
                normal = { textColor = new Color(0.78f, 0.82f, 0.88f) }
            };
            _formulaHi = new GUIStyle(_formula)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.72f, 0.86f, 1f) }
            };
            _slotTag = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.55f, 0.72f, 0.95f) }
            };
        }
    }
}
