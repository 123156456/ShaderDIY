using System.Collections.Generic;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class Region3ParamPanel
    {
        static readonly Color Accent = new Color(0.55f, 0.72f, 0.95f, 1f);
        static readonly Color Card = new Color(0.145f, 0.145f, 0.155f, 1f);
        static readonly Color Slot = new Color(0.2f, 0.2f, 0.215f, 1f);
        static GUIStyle _quiet;
        static GUIStyle _section;
        static GUIStyle _addonOn;
        static GUIStyle _addonOff;

        const float RowH = 20f;
        const float Gap = 4f;
        const float HeadH = 22f;
        const float Pad = 5f;
        const float BlockGap = 4f;

        public static bool Draw(Rect rect, ShaderDIYGraph graph, SdiyAsset host, List<SdiyProperty> props, ref Vector2 scroll)
        {
            ShaderDIYStyles.DrawBox(rect);
            var header = new Rect(rect.x, rect.y, rect.width, 22);
            EditorGUI.DrawRect(header, ShaderDIYStyles.HeaderBg);
            EditorGUI.LabelField(header, ShaderDIYTips.L("模块与参数", ShaderDIYTips.RegionParams), ShaderDIYStyles.HeaderBar);

            if (graph == null)
            {
                EditorGUI.LabelField(new Rect(rect.x + 8, header.yMax + 8, rect.width - 16, 36), "请先选择材质球。", ShaderDIYStyles.Tiny);
                return false;
            }

            var slots = host != null && host.document != null ? host.document.slots : null;
            var baseProps = Filter(props, null);
            float contentH = 6f;
            bool sssOn = IsSssDiffuse(graph);
            bool hasAddon = false;
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].IsAddon)
                    {
                        hasAddon = true;
                        if (AddonOn(graph, slots[i]))
                            contentH += SectionHeight(Filter(props, slots[i].name).Count) + BlockGap;
                        continue;
                    }
                    if (sssOn && slots[i].name == "IndirectDiffuse")
                        continue;
                    contentH += SectionHeight(Filter(props, slots[i].name).Count) + BlockGap;
                }
            }
            if (hasAddon)
                contentH += 26f + BlockGap;
            if (baseProps.Count > 0)
                contentH += BaseHeight(baseProps.Count) + BlockGap;

            var view = new Rect(rect.x, header.yMax, rect.width, rect.height - 22);
            var content = new Rect(0, 0, view.width - 18, Mathf.Max(view.height, contentH));
            scroll = GUI.BeginScrollView(view, scroll, content);

            bool compile = false;
            bool changed = false;
            float y = 4f;
            float x = 6f;
            float w = content.width - 12f;

            if (slots != null)
            {
                if (hasAddon)
                {
                    compile |= DrawAddonBar(new Rect(x, y, w, 24f), graph, slots);
                    y += 24f + BlockGap;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        var add = slots[i];
                        if (!add.IsAddon || !AddonOn(graph, add))
                            continue;
                        var owned = Filter(props, add.name);
                        float h = SectionHeight(owned.Count);
                        compile |= DrawSlotBlock(new Rect(x, y, w, h), graph, add, owned, ref changed, false);
                        y += h + BlockGap;
                    }
                }
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot.IsAddon)
                        continue;
                    if (sssOn && slot.name == "IndirectDiffuse")
                        continue;
                    var owned = Filter(props, slot.name);
                    float h = SectionHeight(owned.Count);
                    compile |= DrawSlotBlock(new Rect(x, y, w, h), graph, slot, owned, ref changed, true);
                    y += h + BlockGap;
                }
            }

            if (baseProps.Count > 0)
            {
                float h = BaseHeight(baseProps.Count);
                changed |= DrawBaseBlock(new Rect(x, y, w, h), graph.material, baseProps);
            }

            GUI.EndScrollView();
            if (changed && graph.material != null)
                EditorUtility.SetDirty(graph.material);
            return compile;
        }

        static bool IsSssDiffuse(ShaderDIYGraph graph)
        {
            return graph != null && graph.GetSlotModule("DirectDiffuse") == "SSS";
        }

        static float GridH(int paramCount)
        {
            if (paramCount <= 0)
                return 0f;
            int cols = paramCount == 1 ? 1 : 2;
            int rows = (paramCount + cols - 1) / cols;
            return Pad + rows * RowH + (rows - 1) * Gap + Pad;
        }

        static float SectionHeight(int paramCount)
        {
            return HeadH + GridH(paramCount);
        }

        static float BaseHeight(int paramCount)
        {
            return HeadH + GridH(paramCount);
        }

        static bool AddonOn(ShaderDIYGraph graph, SdiySlot slot)
        {
            var cur = graph.GetSlotModule(slot.name) ?? slot.defaultModule;
            return !string.IsNullOrEmpty(cur) && cur != "无";
        }

        static string AddonOnName(SdiySlot slot)
        {
            var mods = ModuleRegistry.ModulesForSlot(slot.name);
            for (int i = 0; i < mods.Count; i++)
            {
                var n = mods[i].document != null ? mods[i].document.name : mods[i].name;
                if (n != "无")
                    return n;
            }
            return slot.defaultModule;
        }

        static bool DrawAddonBar(Rect r, ShaderDIYGraph graph, List<SdiySlot> slots)
        {
            Ensure();
            EditorGUI.LabelField(new Rect(r.x, r.y, 72, r.height), ShaderDIYTips.L("叠加", ShaderDIYTips.AddonBar), _section);
            float x = r.x + 72f;
            bool compile = false;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsAddon)
                    continue;
                var label = SlotTitle(slot.name);
                var size = Mathf.Max(52f, _section.CalcSize(new GUIContent(label)).x + 16f);
                var btn = new Rect(x, r.y + 2, size, r.height - 4);
                bool on = AddonOn(graph, slot);
                ShaderDIYTips.Hover(btn, ShaderDIYTips.SlotHead(slot.name));
                if (GUI.Toggle(btn, on, ShaderDIYTips.L(label, ShaderDIYTips.SlotHead(slot.name)), on ? _addonOn : _addonOff) != on)
                {
                    ShaderDIYUndo.Record(graph, graph.material, "ShaderDIY 叠加");
                    graph.SetSlotModule(slot.name, on ? "无" : AddonOnName(slot));
                    ApplyAddonSideEffects(graph, slot.name, !on);
                    compile = true;
                }
                x = btn.xMax + 4f;
            }
            return compile;
        }

        static void ApplyAddonSideEffects(ShaderDIYGraph graph, string slot, bool on)
        {
            if (slot == "LayerRefraction")
            {
                if (on)
                {
                    graph.SetFeature(ShaderDIYRenderState.KeyQueue, "Transparent");
                    graph.SetFeature(ShaderDIYRenderState.KeyZWrite, "Off");
                }
                else
                {
                    graph.SetFeature(ShaderDIYRenderState.KeyQueue, ShaderDIYRenderState.DefaultQueue);
                    graph.SetFeature(ShaderDIYRenderState.KeyZWrite, ShaderDIYRenderState.DefaultZWrite);
                }
            }
        }

        static bool DrawSlotBlock(Rect r, ShaderDIYGraph graph, SdiySlot slot, List<SdiyProperty> owned, ref bool changed, bool showPopup)
        {
            Ensure();
            EditorGUI.DrawRect(r, Card);
            ShaderDIYStyles.DrawBorder(r, new Color(0.3f, 0.3f, 0.34f, 0.6f));

            var head = new Rect(r.x, r.y, r.width, HeadH);
            EditorGUI.DrawRect(head, ShaderDIYStyles.HeaderBg);
            EditorGUI.LabelField(new Rect(head.x + 6, head.y, 90, HeadH), ShaderDIYTips.L(SlotTitle(slot.name), ShaderDIYTips.SlotHead(slot.name)), _section);

            var mods = ModuleRegistry.ModulesForSlot(slot.name);
            var names = new string[Mathf.Max(1, mods.Count)];
            int sel = 0;
            var current = graph.GetSlotModule(slot.name) ?? slot.defaultModule;
            if (mods.Count == 0)
                names[0] = "无";
            else
            {
                for (int i = 0; i < mods.Count; i++)
                {
                    names[i] = mods[i].document != null ? mods[i].document.name : mods[i].name;
                    if (names[i] == current)
                        sel = i;
                }
            }

            float popW = 118f;
            var pop = new Rect(head.xMax - popW - 6, head.y + 2, popW, HeadH - 4);
            bool compile = false;
            if (showPopup && mods.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                int nsel = EditorGUI.Popup(pop, sel, names);
                ShaderDIYTips.Hover(pop, ShaderDIYTips.SlotPopup(slot.name));
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(graph, graph.material, "ShaderDIY 模块");
                    graph.SetSlotModule(slot.name, names[nsel]);
                    if (slot.name == "DirectDiffuse")
                    {
                        if (names[nsel] == "SSS")
                            graph.SetSlotModule("IndirectDiffuse", "SSS");
                        else if (graph.GetSlotModule("IndirectDiffuse") == "SSS")
                            graph.SetSlotModule("IndirectDiffuse", "SH");
                    }
                    if (slot.name == "Transmittance" && names[nsel] == "路径积分" &&
                        graph.GetSlotModule("InScattering") == "多次")
                        graph.SetSlotModule("InScattering", "单次");
                    if (slot.name == "InScattering" && names[nsel] == "多次")
                        graph.SetSlotModule("Transmittance", "查表");
                    if (slot.name == "Light" && names[nsel] == "主光阴影")
                        graph.SetFeature(ShaderDIYRenderState.KeyReceiveShadows, "On");
                    compile = true;
                }
            }
            else if (showPopup)
                EditorGUI.LabelField(pop, "无", Quiet());
            else
                EditorGUI.LabelField(pop, current ?? "", Quiet());

            if (owned.Count > 0)
            {
                var grid = new Rect(r.x + 6, head.yMax, r.width - 12, r.height - HeadH);
                changed |= DrawParamGrid(grid, graph.material, owned, null);
            }
            return compile;
        }

        static bool DrawBaseBlock(Rect r, Material mat, List<SdiyProperty> list)
        {
            Ensure();
            EditorGUI.DrawRect(r, Card);
            ShaderDIYStyles.DrawBorder(r, new Color(0.3f, 0.3f, 0.34f, 0.6f));
            var head = new Rect(r.x, r.y, r.width, HeadH);
            EditorGUI.DrawRect(head, ShaderDIYStyles.HeaderBg);
            EditorGUI.LabelField(new Rect(head.x + 6, head.y, r.width - 12, HeadH), ShaderDIYTips.L("基础参数", ShaderDIYTips.BaseParams), _section);
            var grid = new Rect(r.x + 6, head.yMax, r.width - 12, r.height - HeadH);
            return DrawParamGrid(grid, mat, list, "无参数");
        }

        static bool DrawParamGrid(Rect grid, Material mat, List<SdiyProperty> list, string empty)
        {
            if (list.Count == 0)
            {
                EditorGUI.LabelField(grid, empty, Quiet());
                return false;
            }
            if (mat == null)
            {
                EditorGUI.LabelField(grid, "等待编译…", Quiet());
                return false;
            }

            bool changed = false;
            int cols = list.Count == 1 ? 1 : 2;
            int rows = (list.Count + cols - 1) / cols;
            float colW = cols == 1 ? grid.width : (grid.width - Gap) * 0.5f;
            float y0 = grid.y + Pad;

            for (int i = 0; i < list.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                var cell = new Rect(grid.x + col * (colW + Gap), y0 + row * (RowH + Gap), colW, RowH);
                if (DrawCell(cell, mat, list[i]))
                    changed = true;
            }
            return changed;
        }

        static bool DrawCell(Rect r, Material mat, SdiyProperty p)
        {
            if (!mat.HasProperty(p.name) || !Region2TexturePanel.Visible(mat, p))
                return false;

            EditorGUI.DrawRect(r, Slot);
            var title = new Rect(r.x + 6, r.y, 68, r.height);
            var field = new Rect(r.x + 72, r.y + 1, r.width - 76, r.height - 2);
            EditorGUI.LabelField(title, ShaderDIYTips.Prop(p), Quiet());
            ShaderDIYTips.Hover(r, ShaderDIYTips.ForProperty(p));
            EditorGUI.BeginChangeCheck();

            if (p.IsColor)
            {
                var c = EditorGUI.ColorField(field, mat.GetColor(p.name));
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY " + p.displayName);
                    mat.SetColor(p.name, c);
                    return true;
                }
                return false;
            }
            if (p.IsToggle)
            {
                bool on = EditorGUI.Toggle(new Rect(field.x, field.y, 18, field.height), mat.GetFloat(p.name) > 0.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY " + p.displayName);
                    mat.SetFloat(p.name, on ? 1f : 0f);
                    return true;
                }
                return false;
            }
            if (p.IsRange)
            {
                float min, max;
                SdiyParser.ParseRange(p.type, out min, out max);
                float v = EditorGUI.Slider(field, mat.GetFloat(p.name), min, max);
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY " + p.displayName);
                    mat.SetFloat(p.name, v);
                    return true;
                }
                return false;
            }
            if (p.IsEnum)
            {
                var opts = SdiyParser.ParseEnumOptions(p.type);
                int idx = Mathf.Clamp((int)mat.GetFloat(p.name), 0, Mathf.Max(0, opts.Length - 1));
                idx = EditorGUI.Popup(field, idx, opts);
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY " + p.displayName);
                    mat.SetFloat(p.name, idx);
                    return true;
                }
                return false;
            }
            if (p.IsVector)
            {
                var v = mat.GetVector(p.name);
                float x = v.x, y = v.y, z = v.z, w = v.w;
                float half = (r.height - 2f) * 0.5f;
                if (r.height >= 40f)
                {
                    DrawPair(new Rect(r.x, r.y, r.width, half), "XY", "X", "Y", ref x, ref y);
                    DrawPair(new Rect(r.x, r.y + half + 2f, r.width, half), "ZW", "Z", "W", ref z, ref w);
                }
                else
                {
                    x = EditorGUI.FloatField(new Rect(field.x, field.y, field.width * 0.24f, field.height), x);
                    y = EditorGUI.FloatField(new Rect(field.x + field.width * 0.26f, field.y, field.width * 0.24f, field.height), y);
                    z = EditorGUI.FloatField(new Rect(field.x + field.width * 0.52f, field.y, field.width * 0.24f, field.height), z);
                    w = EditorGUI.FloatField(new Rect(field.x + field.width * 0.78f, field.y, field.width * 0.22f, field.height), w);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY " + p.displayName);
                    mat.SetVector(p.name, new Vector4(x, y, z, w));
                    return true;
                }
                return false;
            }

            float f = EditorGUI.FloatField(field, mat.GetFloat(p.name));
            if (EditorGUI.EndChangeCheck())
            {
                ShaderDIYUndo.Record(mat, "ShaderDIY " + p.displayName);
                mat.SetFloat(p.name, f);
                return true;
            }
            return false;
        }

        static void DrawPair(Rect r, string title, string a, string b, ref float va, ref float vb)
        {
            EditorGUI.DrawRect(r, Slot);
            EditorGUI.LabelField(new Rect(r.x + 6, r.y, 28, r.height), ShaderDIYTips.L(title, "向量分量 " + a + " / " + b), Quiet());
            ShaderDIYTips.Hover(r, "这是一组 XYZW 数。改完预览立刻更新。");
            float fx = r.x + 36f;
            float fw = (r.width - 42f) * 0.5f - 4f;
            EditorGUI.LabelField(new Rect(fx, r.y, 12, r.height), a, Quiet());
            va = EditorGUI.FloatField(new Rect(fx + 12, r.y + 2, fw - 12, r.height - 4), va);
            fx += fw + 6f;
            EditorGUI.LabelField(new Rect(fx, r.y, 12, r.height), b, Quiet());
            vb = EditorGUI.FloatField(new Rect(fx + 12, r.y + 2, fw - 12, r.height - 4), vb);
        }

        static List<SdiyProperty> Filter(List<SdiyProperty> props, string slot)
        {
            var list = new List<SdiyProperty>();
            if (props == null)
                return list;
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                if (p == null || p.isFeature || p.IsTexture)
                    continue;
                if (p.region != PanelRegion.Parameters && p.region != PanelRegion.Auto)
                    continue;
                bool fromSlot = !string.IsNullOrEmpty(p.sourceSlot);
                if (slot == null)
                {
                    if (!fromSlot)
                        list.Add(p);
                }
                else if (p.sourceSlot == slot)
                    list.Add(p);
            }
            return list;
        }

        static string SlotTitle(string slot)
        {
            switch (slot)
            {
                case "DirectDiffuse": return "直接漫反";
                case "DirectSpecular": return "直接高光";
                case "IndirectDiffuse": return "环境漫反";
                case "IndirectSpecular": return "环境高光";
                case "NDF": return "D";
                case "G": return "G";
                case "F": return "F";
                case "F0": return "F0";
                case "Transmittance": return "透射率";
                case "InScattering": return "散射";
                case "Light": return "光向量";
                case "LayerSSS": return "经验SSS";
                case "LayerParallax": return "视差";
                case "LayerRefraction": return "折射";
                default: return slot;
            }
        }

        static GUIStyle Quiet()
        {
            Ensure();
            return _quiet;
        }

        static void Ensure()
        {
            if (_quiet != null && _addonOn != null)
                return;
            _quiet = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                normal = { textColor = new Color(0.62f, 0.64f, 0.68f) }
            };
            _section = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                normal = { textColor = Accent }
            };
            _addonOff = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal
            };
            _addonOn = new GUIStyle(_addonOff)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Accent },
                onNormal = { textColor = Accent }
            };
        }
    }
}
