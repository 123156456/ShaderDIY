using System;
using System.Collections.Generic;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class Region1FeaturePanel
    {
        const float HeaderH = 26f;
        const float FooterH = 28f;
        const float LabelW = 72f;

        public static bool Draw(Rect rect, ShaderDIYGraph graph, ref Vector2 scroll, Action onSettings, List<SdiyProperty> props, ref bool animPlaying)
        {
            ShaderDIYStyles.DrawBox(rect);

            var header = new Rect(rect.x, rect.y, rect.width, HeaderH);
            EditorGUI.DrawRect(header, ShaderDIYStyles.HeaderBg);
            EditorGUI.LabelField(header, ShaderDIYTips.L("渲染状态", ShaderDIYTips.RegionRenderState), ShaderDIYStyles.HeaderBar);
            ShaderDIYStyles.DrawBorder(new Rect(rect.x, header.yMax - 1, rect.width, 1), new Color(0.28f, 0.28f, 0.28f));

            var footer = new Rect(rect.x, rect.yMax - FooterH, rect.width, FooterH);
            float tlh = RegionTimelinePanel.Height;
            var timeline = new Rect(rect.x + 3, footer.y - tlh - 2, rect.width - 6, tlh);
            var body = new Rect(rect.x + 6, header.yMax + 4, rect.width - 12, timeline.y - header.yMax - 8);

            GUILayout.BeginArea(body);
            bool changed = false;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (graph == null)
            {
                EditorGUILayout.HelpBox("请先在预览区创建或选择材质球。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                RegionTimelinePanel.Draw(timeline, null, props, ref animPlaying);
                DrawFooter(footer, onSettings);
                return false;
            }

            float old = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelW;

            if (DrawEnum(graph, null, "大类", ShaderDIYTips.Category, ShaderDIYRenderState.CategoryLabels, null, (int)graph.category, v =>
                {
                    ShaderDIYUndo.Record(graph, graph.material, "ShaderDIY 大类");
                    var next = (ShaderDIYCategory)v;
                    if (next != graph.category)
                        ShaderDIYGraphFactory.ResetRenderState(graph);
                    graph.category = next;
                    var catHosts = ModuleRegistry.Hosts(graph.category);
                    if (catHosts.Count > 0)
                        ShaderDIYGraphFactory.BindHost(graph, catHosts[0]);
                }))
                changed = true;

            var hosts = ModuleRegistry.Hosts(graph.category);
            if (hosts.Count == 0)
                EditorGUILayout.HelpBox("该大类还没有可用 Shader。", MessageType.Warning);
            else if (DrawHostPopup(graph, hosts))
                changed = true;

            EditorGUILayout.Space(8);
            DrawSectionTitle("表面", ShaderDIYTips.Surface);
            if (DrawEnum(graph, ShaderDIYRenderState.KeyCull, "正反面", ShaderDIYTips.Cull,
                    ShaderDIYRenderState.CullLabels, ShaderDIYRenderState.CullValues, ShaderDIYRenderState.DefaultCull))
                changed = true;
            if (DrawEnum(graph, ShaderDIYRenderState.KeyQueue, "队列", ShaderDIYTips.Queue,
                    ShaderDIYRenderState.QueueLabels, ShaderDIYRenderState.QueueValues, ShaderDIYRenderState.DefaultQueue))
                changed = true;

            EditorGUILayout.Space(6);
            DrawSectionTitle("光照", ShaderDIYTips.Lighting);
            if (DrawToggle(graph, ShaderDIYRenderState.KeyZWrite, "写深度", ShaderDIYTips.ZWrite, ShaderDIYRenderState.DefaultZWrite))
                changed = true;
            if (DrawToggle(graph, ShaderDIYRenderState.KeyReceiveShadows, "接阴影", ShaderDIYTips.ReceiveShadows, ShaderDIYRenderState.DefaultReceiveShadows))
                changed = true;
            if (DrawEnum(graph, ShaderDIYRenderState.KeyLightingMode, "光源", ShaderDIYTips.LightingMode,
                    ShaderDIYRenderState.LightLabels, ShaderDIYRenderState.LightValues, ShaderDIYRenderState.DefaultLighting))
                changed = true;
            if (DrawEngine(graph))
                changed = true;

            EditorGUIUtility.labelWidth = old;
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            if (RegionTimelinePanel.Draw(timeline, graph, props, ref animPlaying))
                EditorUtility.SetDirty(graph);

            DrawFooter(footer, onSettings);
            return changed;
        }

        static bool DrawHostPopup(ShaderDIYGraph graph, List<SdiyAsset> hosts)
        {
            var labels = new string[hosts.Count];
            int sel = 0;
            for (int i = 0; i < hosts.Count; i++)
            {
                labels[i] = HostLabel(hosts[i]);
                var path = hosts[i].sourcePath ?? "";
                if (path == graph.hostPath || (hosts[i].document != null && hosts[i].document.name == graph.hostName))
                    sel = i;
            }
            EditorGUI.BeginChangeCheck();
            int nsel = EditorGUILayout.Popup(ShaderDIYTips.L("模板", ShaderDIYTips.Host), sel, labels);
            if (!EditorGUI.EndChangeCheck())
                return false;
            nsel = Mathf.Clamp(nsel, 0, hosts.Count - 1);
            ShaderDIYUndo.Record(graph, graph.material, "ShaderDIY 模板");
            ShaderDIYGraphFactory.BindHost(graph, hosts[nsel]);
            return true;
        }

        static string HostLabel(SdiyAsset host)
        {
            if (host == null || host.document == null || string.IsNullOrEmpty(host.document.name))
                return host != null ? host.name : "未命名";
            var n = host.document.name;
            int slash = n.LastIndexOf('/');
            return slash >= 0 ? n.Substring(slash + 1) : n;
        }

        static void DrawSectionTitle(string title, string tip)
        {
            var r = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
            EditorGUI.LabelField(r, ShaderDIYTips.L(title, tip), EditorStyles.miniBoldLabel);
        }

        static void DrawFooter(Rect footer, Action onSettings)
        {
            EditorGUI.DrawRect(footer, ShaderDIYStyles.FooterBg);
            EditorGUI.DrawRect(new Rect(footer.x, footer.y, footer.width, 1), new Color(0.28f, 0.28f, 0.28f));
            var btn = new Rect(footer.x + 6, footer.y + 3, 22, 22);
            var icon = ShaderDIYStyles.SettingsIcon;
            var content = icon != null
                ? new GUIContent(icon, ShaderDIYTips.Settings)
                : new GUIContent("...", ShaderDIYTips.Settings);
            if (GUI.Button(btn, content, ShaderDIYStyles.IconButton) && onSettings != null)
                onSettings();
        }

        static bool DrawToggle(ShaderDIYGraph graph, string key, string label, string tip, string fallback)
        {
            var cur = graph.GetFeature(key, fallback);
            bool on = cur == "1" || cur == "On" || cur == "True";
            EditorGUI.BeginChangeCheck();
            on = EditorGUILayout.Toggle(ShaderDIYTips.L(label, tip), on);
            if (!EditorGUI.EndChangeCheck())
                return false;
            ShaderDIYUndo.Record(graph, "ShaderDIY 渲染状态");
            graph.SetFeature(key, on ? "On" : "Off");
            return true;
        }

        static bool DrawEnum(ShaderDIYGraph graph, string key, string label, string tip, string[] labels, string[] values, string fallback)
        {
            var cur = graph.GetFeature(key, fallback);
            int idx = ShaderDIYRenderState.IndexOf(values, cur, 0);
            return DrawEnum(graph, key, label, tip, labels, values, idx, null);
        }

        static bool DrawEnum(ShaderDIYGraph graph, string key, string label, string[] labels, string[] values, int idx, Action<int> onIndex)
        {
            return DrawEnum(graph, key, label, null, labels, values, idx, onIndex);
        }

        static bool DrawEnum(ShaderDIYGraph graph, string key, string label, string tip, string[] labels, string[] values, int idx, Action<int> onIndex)
        {
            EditorGUI.BeginChangeCheck();
            idx = EditorGUILayout.Popup(ShaderDIYTips.L(label, tip), idx, labels);
            if (!EditorGUI.EndChangeCheck())
                return false;
            if (onIndex == null)
                ShaderDIYUndo.Record(graph, "ShaderDIY 渲染状态");
            if (onIndex != null)
                onIndex(idx);
            else if (values != null)
                graph.SetFeature(key, values[Mathf.Clamp(idx, 0, values.Length - 1)]);
            return true;
        }

        static bool DrawEngine(ShaderDIYGraph graph)
        {
            var cur = ShaderDIYRenderState.NormalizeEngine(
                graph.GetFeature(ShaderDIYRenderState.KeyEngine, ShaderDIYRenderState.DefaultEngine));
            int idx = ShaderDIYRenderState.IndexOf(ShaderDIYRenderState.EngineValues, cur, 0);
            EditorGUI.BeginChangeCheck();
            idx = EditorGUILayout.Popup(ShaderDIYTips.L("引擎", ShaderDIYTips.Engine), idx, ShaderDIYRenderState.EngineLabels);
            if (!EditorGUI.EndChangeCheck())
                return false;
            ShaderDIYUndo.Record(graph, "ShaderDIY 引擎");
            graph.SetFeature(ShaderDIYRenderState.KeyEngine, ShaderDIYRenderState.EngineValues[idx]);
            return true;
        }
    }
}
