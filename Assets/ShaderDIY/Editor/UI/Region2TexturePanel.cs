using System.Collections.Generic;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class Region2TexturePanel
    {
        static readonly string[] ChannelLabels = { "RGBA", "R", "G", "B", "A" };
        static readonly Color Accent = new Color(0.55f, 0.72f, 0.95f, 1f);
        static readonly Color Card = new Color(0.145f, 0.145f, 0.155f, 1f);
        static readonly Color Slot = new Color(0.2f, 0.2f, 0.215f, 1f);
        static GUIStyle _quiet;
        static GUIStyle _tabOn;
        static GUIStyle _tabOff;

        public static bool Draw(Rect rect, ShaderDIYGraph graph, List<SdiyProperty> props, ref int selected)
        {
            ShaderDIYStyles.DrawBox(rect);
            var header = new Rect(rect.x, rect.y, rect.width, 22);
            EditorGUI.DrawRect(header, ShaderDIYStyles.HeaderBg);
            EditorGUI.LabelField(header, ShaderDIYTips.L("贴图", ShaderDIYTips.RegionTextures), ShaderDIYStyles.HeaderBar);

            if (graph == null || graph.material == null)
            {
                EditorGUI.LabelField(new Rect(rect.x + 8, header.yMax + 8, rect.width - 16, 36), "没有可编辑的材质。", ShaderDIYStyles.Tiny);
                return false;
            }

            var maps = Collect(graph.material, props);
            if (maps.Count == 0)
            {
                EditorGUI.LabelField(new Rect(rect.x + 8, header.yMax + 8, rect.width - 16, 36), "当前没有贴图槽。", ShaderDIYStyles.Tiny);
                return false;
            }

            if (selected < 0 || selected >= maps.Count)
                selected = 0;

            var tabBar = new Rect(rect.x + 8, header.yMax + 5, rect.width - 16, 20);
            selected = DrawTabs(tabBar, maps, selected);

            var body = new Rect(rect.x + 8, tabBar.yMax + 6, rect.width - 16, rect.yMax - tabBar.yMax - 10);
            EditorGUI.DrawRect(body, Card);
            ShaderDIYStyles.DrawBorder(body, new Color(0.3f, 0.3f, 0.34f, 0.6f));
            return DrawCard(new Rect(body.x + 8, body.y + 6, body.width - 16, body.height - 12), graph.material, maps[selected]);
        }

        static List<SdiyProperty> Collect(Material mat, List<SdiyProperty> props)
        {
            var list = new List<SdiyProperty>();
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                if (p == null || p.isFeature || !p.IsTexture)
                    continue;
                if (p.region != PanelRegion.Textures && p.region != PanelRegion.Auto)
                    continue;
                if (!Visible(mat, p))
                    continue;
                list.Add(p);
            }
            return list;
        }

        static int DrawTabs(Rect bar, List<SdiyProperty> maps, int selected)
        {
            EnsureStyles();
            float x = bar.x;
            for (int i = 0; i < maps.Count; i++)
            {
                var label = TabLabel(maps[i]);
                var size = _tabOff.CalcSize(new GUIContent(label));
                var r = new Rect(x, bar.y, Mathf.Max(48f, size.x + 16f), bar.height);
                if (x + r.width > bar.xMax)
                    break;
                bool on = selected == i;
                if (GUI.Toggle(r, on, ShaderDIYTips.L(label, ShaderDIYTips.ForProperty(maps[i])), on ? _tabOn : _tabOff))
                    selected = i;
                x = r.xMax + 4f;
            }
            return selected;
        }

        static string TabLabel(SdiyProperty p)
        {
            if (!string.IsNullOrEmpty(p.tabName))
                return p.tabName;
            if (!string.IsNullOrEmpty(p.displayName))
                return p.displayName;
            return p.name;
        }

        static bool DrawCard(Rect body, Material mat, SdiyProperty p)
        {
            bool changed = false;
            var ops = p.texOps == TexOps.None ? TexOps.All : p.texOps;
            float thumb = Mathf.Min(72f, body.height);
            var texRect = new Rect(body.x, body.y + (body.height - thumb) * 0.5f, thumb, thumb);
            EditorGUI.DrawRect(new Rect(texRect.x - 2, texRect.y - 2, texRect.width + 4, texRect.height + 4), Slot);

            EditorGUI.BeginChangeCheck();
            var cur = mat.HasProperty(p.name) ? mat.GetTexture(p.name) : null;
            var ntex = (Texture)EditorGUI.ObjectField(texRect, GUIContent.none, cur, typeof(Texture), false);
            ShaderDIYTips.Hover(texRect, ShaderDIYTips.TexSlot + "\n" + ShaderDIYTips.ForProperty(p));
            if (EditorGUI.EndChangeCheck() && mat.HasProperty(p.name))
            {
                ShaderDIYUndo.Record(mat, "ShaderDIY 贴图");
                mat.SetTexture(p.name, ntex);
                changed = true;
            }

            float colX = texRect.xMax + 14f;
            float rowH = 22f;
            float y0 = body.y + Mathf.Max(0f, (body.height - rowH * 2 - 6f) * 0.5f);
            float y1 = y0 + rowH + 6f;
            float stW = 168f;

            if ((ops & TexOps.TilingOffset) != 0 && mat.HasProperty(p.name))
            {
                var st = mat.GetTextureScale(p.name);
                var of = mat.GetTextureOffset(p.name);
                EditorGUI.BeginChangeCheck();
                DrawPair(new Rect(colX, y0, stW, rowH), "缩放", ShaderDIYTips.TexScale, "X", "Y", ref st.x, ref st.y);
                DrawPair(new Rect(colX, y1, stW, rowH), "偏移", ShaderDIYTips.TexOffset, "Z", "W", ref of.x, ref of.y);
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY 贴图缩放");
                    mat.SetTextureScale(p.name, st);
                    mat.SetTextureOffset(p.name, of);
                    changed = true;
                }
            }

            float right = colX + stW + 12f;
            float rightW = body.xMax - right;
            if (rightW < 120f)
                rightW = 120f;

            if ((ops & TexOps.Channel) != 0 && mat.HasProperty(p.name + "_Channel"))
            {
                var chRect = new Rect(right, y0, Mathf.Min(130f, rightW - 30f), rowH);
                EditorGUI.LabelField(new Rect(chRect.x, chRect.y, 32, rowH), ShaderDIYTips.L("通道", ShaderDIYTips.TexChannel), Quiet());
                EditorGUI.BeginChangeCheck();
                int ch = (int)mat.GetFloat(p.name + "_Channel");
                var chField = new Rect(chRect.x + 32, chRect.y + 1, chRect.width - 32, rowH - 2);
                ch = EditorGUI.Popup(chField, ch, ChannelLabels);
                ShaderDIYTips.Hover(chRect, ShaderDIYTips.TexChannel);
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY 贴图通道");
                    mat.SetFloat(p.name + "_Channel", ch);
                    changed = true;
                }
            }

            bool canInvert = (ops & TexOps.Invert) != 0 && mat.HasProperty(p.name + "_Invert");
            float mipRight = canInvert ? 28f : 0f;
            if ((ops & TexOps.MipBias) != 0 && mat.HasProperty(p.name + "_MipBias"))
            {
                var mipRect = new Rect(right, y1, Mathf.Max(80f, rightW - mipRight - 4f), rowH);
                EditorGUI.LabelField(new Rect(mipRect.x, mipRect.y, 28, rowH), ShaderDIYTips.L("Mip", ShaderDIYTips.TexMip), Quiet());
                EditorGUI.BeginChangeCheck();
                float mip = mat.GetFloat(p.name + "_MipBias");
                mip = EditorGUI.Slider(new Rect(mipRect.x + 28, mipRect.y + 2, mipRect.width - 28, rowH - 4), mip, -4f, 8f);
                ShaderDIYTips.Hover(mipRect, ShaderDIYTips.TexMip);
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY Mip");
                    mat.SetFloat(p.name + "_MipBias", mip);
                    changed = true;
                }
            }

            if (canInvert)
            {
                bool inv = mat.GetFloat(p.name + "_Invert") > 0.5f;
                var ir = new Rect(body.xMax - 24f, y0 + 1f, 22f, 22f);
                EditorGUI.BeginChangeCheck();
                inv = InvertChip(ir, inv);
                if (EditorGUI.EndChangeCheck())
                {
                    ShaderDIYUndo.Record(mat, "ShaderDIY 反相");
                    mat.SetFloat(p.name + "_Invert", inv ? 1f : 0f);
                    changed = true;
                }
            }

            return changed;
        }

        static void DrawPair(Rect r, string title, string tip, string a, string b, ref float va, ref float vb)
        {
            EditorGUI.DrawRect(r, Slot);
            EditorGUI.LabelField(new Rect(r.x + 6, r.y, 28, r.height), ShaderDIYTips.L(title, tip), Quiet());
            ShaderDIYTips.Hover(r, tip);
            float fx = r.x + 36f;
            float fw = (r.width - 42f) * 0.5f - 4f;
            EditorGUI.LabelField(new Rect(fx, r.y, 12, r.height), a, Quiet());
            va = EditorGUI.FloatField(new Rect(fx + 12, r.y + 2, fw - 12, r.height - 4), va);
            fx += fw + 6f;
            EditorGUI.LabelField(new Rect(fx, r.y, 12, r.height), b, Quiet());
            vb = EditorGUI.FloatField(new Rect(fx + 12, r.y + 2, fw - 12, r.height - 4), vb);
        }

        static bool InvertChip(Rect r, bool on)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition) && e.button == 0)
            {
                on = !on;
                e.Use();
                GUI.changed = true;
            }
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, on ? new Color(0.28f, 0.42f, 0.62f, 1f) : Slot);
                ShaderDIYStyles.DrawBorder(r, on ? Accent : new Color(0.38f, 0.38f, 0.42f));
                float mid = r.x + r.width * 0.5f;
                var left = new Rect(r.x + 3, r.y + 3, r.width * 0.5f - 3, r.height - 6);
                var right = new Rect(mid, r.y + 3, r.width * 0.5f - 3, r.height - 6);
                var light = new Color(0.92f, 0.92f, 0.94f, 1f);
                var dark = new Color(0.12f, 0.12f, 0.14f, 1f);
                EditorGUI.DrawRect(left, on ? dark : light);
                EditorGUI.DrawRect(right, on ? light : dark);
            }
            ShaderDIYTips.Hover(r, ShaderDIYTips.TexInvert);
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            return on;
        }

        static GUIStyle Quiet()
        {
            EnsureStyles();
            return _quiet;
        }

        static void EnsureStyles()
        {
            if (_quiet != null)
                return;
            _quiet = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                normal = { textColor = new Color(0.62f, 0.64f, 0.68f) }
            };
            _tabOff = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal
            };
            _tabOn = new GUIStyle(_tabOff)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Accent },
                onNormal = { textColor = Accent }
            };
        }

        public static bool Visible(Material mat, SdiyProperty p)
        {
            if (string.IsNullOrEmpty(p.showIfProperty) || mat == null)
                return true;
            if (!mat.HasProperty(p.showIfProperty))
                return true;
            var v = mat.GetFloat(p.showIfProperty);
            float expect;
            if (!float.TryParse(p.showIfValue, out expect))
                expect = 1f;
            return Mathf.Abs(v - expect) < 0.01f;
        }
    }
}
