using System.Collections.Generic;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class RegionTimelinePanel
    {
        public const float Height = 118f;
        static readonly Color Track = new Color(0.12f, 0.12f, 0.14f, 1f);
        static readonly Color TickCol = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color Playhead = new Color(1f, 0.45f, 0.18f, 1f);
        static readonly Color KeyCol = new Color(0.95f, 0.78f, 0.22f, 1f);
        static readonly Color KeySel = new Color(1f, 0.95f, 0.45f, 1f);
        static int _sel = -1;
        static bool _scrub;
        static GUIStyle _timeLbl;

        public static bool Draw(Rect rect, ShaderDIYGraph graph, List<SdiyProperty> props, ref bool playing)
        {
            EditorGUI.DrawRect(rect, ShaderDIYStyles.FooterBg);
            ShaderDIYStyles.DrawBorder(rect, new Color(0.3f, 0.3f, 0.34f, 0.7f));
            var header = new Rect(rect.x, rect.y, rect.width, 20);
            EditorGUI.DrawRect(header, ShaderDIYStyles.HeaderBg);
            EditorGUI.LabelField(new Rect(header.x + 6, header.y, 80, 20), ShaderDIYTips.L("时间轴", ShaderDIYTips.Timeline), ShaderDIYStyles.HeaderBar);

            if (graph == null)
            {
                EditorGUI.LabelField(new Rect(rect.x + 8, header.yMax + 8, rect.width - 16, 24), "没有材质。", ShaderDIYStyles.Tiny);
                return false;
            }

            if (graph.animKeys == null)
                graph.animKeys = new List<AnimKey>();
            if (graph.animLength < 0.5f)
                graph.animLength = 4f;

            var bar = new Rect(rect.x + 4, header.yMax + 2, rect.width - 8, 18);
            bool changed = DrawTransport(bar, graph, props, ref playing);

            var track = new Rect(rect.x + 6, bar.yMax + 4, rect.width - 12, 52);
            EditorGUI.DrawRect(track, Track);
            ShaderDIYTips.Hover(track, ShaderDIYTips.Track);
            DrawRuler(track, graph);
            changed |= HandleTrack(track, graph, ref playing);
            DrawKeys(track, graph);
            DrawPlayhead(track, graph);

            var hint = new Rect(rect.x + 6, track.yMax + 2, rect.width - 12, 16);
            int n = graph.animKeys.Count;
            EditorGUI.LabelField(hint, n == 0
                ? "调好参数点 K 记下，再改参数打下一帧，播放会平滑过渡。"
                : string.Format("{0:0.00}s / {1:0.0}s · {2} 帧", graph.animTime, graph.animLength, n), ShaderDIYStyles.Tiny);
            return changed;
        }

        static bool DrawTransport(Rect r, ShaderDIYGraph graph, List<SdiyProperty> props, ref bool playing)
        {
            bool changed = false;
            float x = r.x;
            if (Btn(ref x, r.y, r.height, playing ? "■" : "▶", playing ? ShaderDIYTips.Stop : ShaderDIYTips.Play))
            {
                playing = !playing;
                if (playing && graph.animKeys.Count < 2)
                    playing = false;
            }
            if (Btn(ref x, r.y, r.height, "|◀", ShaderDIYTips.Rewind))
            {
                playing = false;
                graph.animTime = 0f;
                Apply(graph);
                changed = true;
            }
            if (Btn(ref x, r.y, r.height, "K", ShaderDIYTips.InsertKey))
            {
                ShaderDIYUndo.Record(graph, graph.material, "ShaderDIY 关键帧");
                Insert(graph, props);
                changed = true;
            }
            if (Btn(ref x, r.y, r.height, "X", ShaderDIYTips.DeleteKey))
            {
                if (_sel >= 0 && _sel < graph.animKeys.Count)
                {
                    ShaderDIYUndo.Record(graph, "ShaderDIY 删除关键帧");
                    graph.animKeys.RemoveAt(_sel);
                    _sel = -1;
                    changed = true;
                }
            }

            var loopR = new Rect(x, r.y, 36, r.height);
            EditorGUI.BeginChangeCheck();
            bool loop = GUI.Toggle(loopR, graph.animLoop, ShaderDIYTips.L("循环", ShaderDIYTips.Loop), EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                ShaderDIYUndo.Record(graph, "ShaderDIY 循环");
                graph.animLoop = loop;
                changed = true;
            }
            x += 40f;
            EditorGUI.LabelField(new Rect(x, r.y, 14, r.height), ShaderDIYTips.L("秒", ShaderDIYTips.Duration), ShaderDIYStyles.Tiny);
            x += 16f;
            EditorGUI.BeginChangeCheck();
            var durR = new Rect(x, r.y + 1, 34, r.height - 2);
            float len = Mathf.Clamp(EditorGUI.FloatField(durR, graph.animLength), 0.5f, 60f);
            ShaderDIYTips.Hover(durR, ShaderDIYTips.Duration);
            if (EditorGUI.EndChangeCheck())
            {
                ShaderDIYUndo.Record(graph, "ShaderDIY 时长");
                graph.animLength = len;
                changed = true;
            }
            return changed;
        }

        static bool Btn(ref float x, float y, float h, string label, string tip)
        {
            var r = new Rect(x, y, 22, h);
            x = r.xMax + 2f;
            return GUI.Button(r, new GUIContent(label, tip), EditorStyles.miniButton);
        }

        static void DrawRuler(Rect track, ShaderDIYGraph graph)
        {
            Ensure();
            float len = Mathf.Max(0.5f, graph.animLength);
            int steps = Mathf.Clamp(Mathf.RoundToInt(len), 2, 8);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float px = Mathf.Lerp(track.x + 6, track.xMax - 6, t);
                EditorGUI.DrawRect(new Rect(px, track.y + 2, 1, track.height - 4), TickCol);
                GUI.Label(new Rect(px - 10, track.y + 2, 22, 12), (t * len).ToString("0.#"), _timeLbl);
            }
        }

        static bool HandleTrack(Rect track, ShaderDIYGraph graph, ref bool playing)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && track.Contains(e.mousePosition))
            {
                int hit = HitKey(track, graph, e.mousePosition);
                if (hit >= 0)
                    _sel = hit;
                else
                {
                    _sel = -1;
                    _scrub = true;
                    playing = false;
                    graph.animTime = XToTime(track, graph, e.mousePosition.x);
                    Apply(graph);
                }
                e.Use();
                GUI.changed = true;
                return true;
            }
            if (_scrub && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                graph.animTime = XToTime(track, graph, e.mousePosition.x);
                Apply(graph);
                if (e.type == EventType.MouseUp)
                    _scrub = false;
                e.Use();
                return true;
            }
            return false;
        }

        static void DrawKeys(Rect track, ShaderDIYGraph graph)
        {
            for (int i = 0; i < graph.animKeys.Count; i++)
            {
                float px = TimeToX(track, graph, graph.animKeys[i].time);
                DrawDiamond(new Vector2(px, track.y + track.height * 0.62f), 5f, i == _sel ? KeySel : KeyCol);
            }
        }

        static void DrawPlayhead(Rect track, ShaderDIYGraph graph)
        {
            float px = TimeToX(track, graph, graph.animTime);
            EditorGUI.DrawRect(new Rect(px - 0.5f, track.y, 2f, track.height), Playhead);
        }

        static void DrawDiamond(Vector2 c, float s, Color col)
        {
            EditorGUI.DrawRect(new Rect(c.x - s * 0.35f, c.y - s, s * 0.7f, s * 2f), col);
            EditorGUI.DrawRect(new Rect(c.x - s, c.y - s * 0.35f, s * 2f, s * 0.7f), col);
        }

        static int HitKey(Rect track, ShaderDIYGraph graph, Vector2 p)
        {
            for (int i = 0; i < graph.animKeys.Count; i++)
            {
                float px = TimeToX(track, graph, graph.animKeys[i].time);
                if (Mathf.Abs(p.x - px) <= 7f && Mathf.Abs(p.y - (track.y + track.height * 0.62f)) <= 10f)
                    return i;
            }
            return -1;
        }

        static float TimeToX(Rect track, ShaderDIYGraph graph, float time)
        {
            float u = Mathf.Clamp01(time / Mathf.Max(0.5f, graph.animLength));
            return Mathf.Lerp(track.x + 6, track.xMax - 6, u);
        }

        static float XToTime(Rect track, ShaderDIYGraph graph, float x)
        {
            float u = Mathf.InverseLerp(track.x + 6, track.xMax - 6, x);
            return Mathf.Clamp(u * graph.animLength, 0f, graph.animLength);
        }

        public static void Insert(ShaderDIYGraph graph, List<SdiyProperty> props)
        {
            if (graph == null || graph.material == null)
                return;
            var snap = Capture(graph.material, props);
            float t = graph.animTime;
            for (int i = 0; i < graph.animKeys.Count; i++)
            {
                if (Mathf.Abs(graph.animKeys[i].time - t) < 0.04f)
                {
                    graph.animKeys[i].values = snap;
                    _sel = i;
                    EditorUtility.SetDirty(graph);
                    return;
                }
            }
            graph.animKeys.Add(new AnimKey { time = t, values = snap });
            graph.animKeys.Sort((a, b) => a.time.CompareTo(b.time));
            _sel = graph.animKeys.FindIndex(k => Mathf.Abs(k.time - t) < 0.0001f);
            EditorUtility.SetDirty(graph);
        }

        public static void Tick(ShaderDIYGraph graph, float dt)
        {
            if (graph == null || graph.animKeys == null || graph.animKeys.Count < 2)
                return;
            graph.animTime += dt;
            if (graph.animTime > graph.animLength)
            {
                if (graph.animLoop)
                    graph.animTime = Mathf.Repeat(graph.animTime, graph.animLength);
                else
                    graph.animTime = graph.animLength;
            }
            Apply(graph);
        }

        public static void Apply(ShaderDIYGraph graph)
        {
            if (graph == null || graph.material == null || graph.animKeys == null || graph.animKeys.Count == 0)
                return;
            var keys = graph.animKeys;
            float t = graph.animTime;
            AnimKey a = keys[0];
            AnimKey b = keys[keys.Count - 1];
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].time <= t)
                    a = keys[i];
                if (keys[i].time >= t)
                {
                    b = keys[i];
                    break;
                }
            }
            float span = Mathf.Max(0.0001f, b.time - a.time);
            float u = a == b ? 0f : Mathf.Clamp01((t - a.time) / span);
            u = u * u * (3f - 2f * u);
            ApplyLerp(graph.material, a, b, u);
        }

        static List<AnimParam> Capture(Material mat, List<SdiyProperty> props)
        {
            var list = new List<AnimParam>();
            if (props == null)
                return list;
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                if (p == null || p.IsTexture || p.isFeature || !mat.HasProperty(p.name))
                    continue;
                var item = new AnimParam { name = p.name };
                if (p.IsColor)
                {
                    var c = mat.GetColor(p.name);
                    item.kind = 1;
                    item.value = new Vector4(c.r, c.g, c.b, c.a);
                }
                else if (p.IsVector)
                {
                    item.kind = 2;
                    item.value = mat.GetVector(p.name);
                }
                else
                {
                    item.kind = 0;
                    item.value = new Vector4(mat.GetFloat(p.name), 0, 0, 0);
                }
                list.Add(item);
            }
            return list;
        }

        static void ApplyLerp(Material mat, AnimKey a, AnimKey b, float u)
        {
            var names = new HashSet<string>();
            CollectNames(a, names);
            CollectNames(b, names);
            var it = names.GetEnumerator();
            while (it.MoveNext())
            {
                var n = it.Current;
                if (!mat.HasProperty(n))
                    continue;
                AnimParam pa, pb;
                bool ha = Find(a, n, out pa);
                bool hb = Find(b, n, out pb);
                if (!ha && !hb)
                    continue;
                var p = ha ? pa : pb;
                var q = hb ? pb : pa;
                var v = Vector4.Lerp(p.value, q.value, u);
                if (p.kind == 1)
                    mat.SetColor(n, new Color(v.x, v.y, v.z, v.w));
                else if (p.kind == 2)
                    mat.SetVector(n, v);
                else
                    mat.SetFloat(n, v.x);
            }
        }

        static void CollectNames(AnimKey k, HashSet<string> dst)
        {
            if (k == null || k.values == null)
                return;
            for (int i = 0; i < k.values.Count; i++)
            {
                if (k.values[i] != null && !string.IsNullOrEmpty(k.values[i].name))
                    dst.Add(k.values[i].name);
            }
        }

        static bool Find(AnimKey k, string name, out AnimParam p)
        {
            p = null;
            if (k == null || k.values == null)
                return false;
            for (int i = 0; i < k.values.Count; i++)
            {
                if (k.values[i] != null && k.values[i].name == name)
                {
                    p = k.values[i];
                    return true;
                }
            }
            return false;
        }

        static void Ensure()
        {
            if (_timeLbl != null)
                return;
            _timeLbl = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 8,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(0.55f, 0.57f, 0.6f) }
            };
        }
    }
}
