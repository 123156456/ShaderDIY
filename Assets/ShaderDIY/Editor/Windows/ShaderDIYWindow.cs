using System.Collections.Generic;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public class ShaderDIYWindow : EditorWindow
    {
        ShaderDIYGraph _graph;
        ShaderDIYGraph[] _graphs = new ShaderDIYGraph[0];
        MaterialPreviewRenderer _preview;
        Vector2 _s1;
        Vector2 _s3;
        int _texTab;
        double _compileAt;
        bool _compileQueued;
        bool _animPlaying;
        double _animClock;
        string _newName = "NewMaterial";
        static readonly string[] MeshLabels = { "球", "立方", "胶囊", "圆柱", "面片", "平面", "自定义" };
        static readonly string PrefMesh = "ShaderDIY.PreviewMesh";
        static readonly string PrefCustomMeshPath = "ShaderDIY.PreviewCustomMeshPath";

        [MenuItem("Window/ShaderDIY/材质工坊")]
        public static void Open()
        {
            var w = GetWindow<ShaderDIYWindow>();
            w.titleContent = new GUIContent("ShaderDIY", "材质工坊：选模板、换模块、改参数，左边预览立刻更新。");
            w.minSize = new Vector2(1100, 640);
            w.Show();
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            _preview = new MaterialPreviewRenderer();
            RestorePreviewMesh();
            _graph = ShaderDIYGraphFactory.EnsureDefault();
            RefreshGraphs();
            if (_graph != null)
                QueueCompile();
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_preview != null)
            {
                _preview.Dispose();
                _preview = null;
            }
        }

        void OnEditorUpdate()
        {
            ShaderDIYCompiler.RebuildDirtyGraphsIfNeeded();
            if (_compileQueued && EditorApplication.timeSinceStartup >= _compileAt)
            {
                _compileQueued = false;
                if (_graph != null)
                {
                    ShaderDIYCompiler.Compile(_graph, true);
                    Repaint();
                }
            }
            if (_animPlaying && _graph != null)
            {
                float dt = (float)(EditorApplication.timeSinceStartup - _animClock);
                _animClock = EditorApplication.timeSinceStartup;
                RegionTimelinePanel.Tick(_graph, dt);
                if (!_graph.animLoop && _graph.animTime >= _graph.animLength - 0.0001f)
                    _animPlaying = false;
                Repaint();
            }
            else
                _animClock = EditorApplication.timeSinceStartup;
        }

        void RefreshGraphs()
        {
            _graphs = ShaderDIYGraphFactory.LoadAll();
            if (_graph == null && _graphs.Length > 0)
                _graph = _graphs[0];
        }

        void OnUndoRedo()
        {
            QueueCompile();
            Repaint();
        }

        void QueueCompile()
        {
            _compileQueued = true;
            _compileAt = EditorApplication.timeSinceStartup + 0.12;
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            var pos = position;
            float pad = 6f;
            float left = 250f;
            float right = 196f;
            float previewH = 220f;
            float gap = 4f;

            var previewRect = new Rect(pad, pad, left, previewH);
            var r1 = new Rect(pad, previewH + pad + gap, left, pos.height - previewH - pad * 2 - gap);
            var r4 = new Rect(pos.width - right - pad, pad, right, pos.height - pad * 2);
            var r2 = new Rect(left + pad + gap, pad, pos.width - left - right - pad * 2 - gap * 2, 136f);
            var r3 = new Rect(r2.x, r2.yMax + gap, r2.width, pos.height - r2.height - pad * 2 - gap);

            DrawPreview(previewRect);

            var host = ResolveHost();
            var props = _graph != null ? ShaderDIYCompiler.CollectMergedProperties(_graph) : new List<SdiyProperty>();
            if (Region1FeaturePanel.Draw(r1, _graph, ref _s1, ShowSettingsMenu, props, ref _animPlaying))
            {
                EditorUtility.SetDirty(_graph);
                QueueCompile();
                host = ResolveHost();
            }

            if (Region2TexturePanel.Draw(r2, _graph, props, ref _texTab))
            {
                if (_graph != null && _graph.material != null)
                    EditorUtility.SetDirty(_graph.material);
                Repaint();
            }

            if (Region3ParamPanel.Draw(r3, _graph, host, props, ref _s3))
            {
                EditorUtility.SetDirty(_graph);
                QueueCompile();
            }

            Region4PerfPanel.Draw(r4, _graph, host);
        }

        void DrawPreview(Rect rect)
        {
            ShaderDIYStyles.DrawBox(rect);

            var icon = new Rect(rect.x + 5, rect.y + 5, 28, 28);
            DrawAssignChip(icon);

            const float plusW = 18f;
            const float dropW = 92f;
            var plus = new Rect(rect.xMax - 6 - plusW, rect.y + 6, plusW, 18);
            var minus = new Rect(plus.x - 2 - plusW, rect.y + 6, plusW, 18);
            var drop = new Rect(minus.x - 4 - dropW, rect.y + 6, dropW, 18);
            DrawMaterialPopup(drop);
            if (GUI.Button(minus, ShaderDIYTips.L("−", ShaderDIYTips.DeleteMaterial), EditorStyles.miniButton))
                PromptDelete();
            if (GUI.Button(plus, ShaderDIYTips.L("+", ShaderDIYTips.CreateMaterial), EditorStyles.miniButton))
                PromptCreate();

            var meshDropW = 58f;
            var meshDrop = new Rect(rect.xMax - 6 - meshDropW, rect.yMax - 22, meshDropW, 16);
            DrawMeshPopup(meshDrop);

            var preview = new Rect(rect.x + 2, icon.yMax + 2, rect.width - 4, meshDrop.y - icon.yMax - 4);
            if (_preview != null)
                _preview.Draw(preview, _graph != null ? _graph.material : null);
            ShaderDIYTips.Hover(preview, ShaderDIYTips.PreviewOrbit);
        }

        void DrawAssignChip(Rect icon)
        {
            var mat = _graph != null ? _graph.material : null;
            var preview = mat != null
                ? AssetPreview.GetAssetPreview(mat) ?? AssetPreview.GetMiniThumbnail(mat)
                : null;
            GUI.Box(icon, GUIContent.none);
            if (preview != null)
                GUI.DrawTexture(icon, preview, ScaleMode.ScaleToFit);
            else
                EditorGUI.LabelField(icon, "Mat", EditorStyles.miniLabel);
            ShaderDIYTips.Hover(icon, ShaderDIYTips.AssignChip);

            var e = Event.current;
            EditorGUIUtility.AddCursorRect(icon, mat != null ? MouseCursor.Link : MouseCursor.Arrow);
            if (mat != null && icon.Contains(e.mousePosition) &&
                (e.type == EventType.MouseDrag || (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1)))
            {
                if (e.type == EventType.MouseDrag)
                {
                    MaterialAssignService.BeginDrag(_graph);
                    e.Use();
                }
            }
        }

        void DrawMaterialPopup(Rect drop)
        {
            int count = _graphs != null ? _graphs.Length : 0;
            var names = new string[Mathf.Max(1, count)];
            int sel = 0;
            if (count == 0)
            {
                names[0] = "无";
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    names[i] = _graphs[i] != null ? _graphs[i].name : "(missing)";
                    if (_graphs[i] == _graph)
                        sel = i;
                }
            }
            EditorGUI.BeginChangeCheck();
            int nsel = EditorGUI.Popup(drop, sel, names, EditorStyles.toolbarPopup);
            ShaderDIYTips.Hover(drop, ShaderDIYTips.MaterialPopup);
            if (EditorGUI.EndChangeCheck() && count > 0)
                _graph = _graphs[nsel];
        }

        void DrawMeshPopup(Rect meshDrop)
        {
            int kind = _preview != null ? (int)_preview.Kind : 0;
            EditorGUI.BeginChangeCheck();
            kind = EditorGUI.Popup(meshDrop, kind, MeshLabels, EditorStyles.miniPullDown);
            ShaderDIYTips.Hover(meshDrop, ShaderDIYTips.MeshKind);
            if (EditorGUI.EndChangeCheck() && _preview != null)
            {
                _preview.SetMesh((PreviewMeshKind)kind, _preview.CustomMesh);
                EditorPrefs.SetInt(PrefMesh, kind);
            }

            if (_preview != null && _preview.Kind == PreviewMeshKind.Custom)
            {
                var field = new Rect(meshDrop.x - 74, meshDrop.y, 70, 16);
                EditorGUI.BeginChangeCheck();
                var mesh = (Mesh)EditorGUI.ObjectField(field, _preview.CustomMesh, typeof(Mesh), false);
                ShaderDIYTips.Hover(field, ShaderDIYTips.CustomMesh);
                if (EditorGUI.EndChangeCheck())
                {
                    _preview.SetMesh(PreviewMeshKind.Custom, mesh);
                    EditorPrefs.SetString(PrefCustomMeshPath, mesh != null ? AssetDatabase.GetAssetPath(mesh) : "");
                }
            }
        }

        void RestorePreviewMesh()
        {
            if (_preview == null)
                return;
            var kind = (PreviewMeshKind)EditorPrefs.GetInt(PrefMesh, 0);
            Mesh custom = null;
            if (kind == PreviewMeshKind.Custom)
            {
                var path = EditorPrefs.GetString(PrefCustomMeshPath, "");
                if (!string.IsNullOrEmpty(path))
                    custom = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
            _preview.SetMesh(kind, custom);
        }

        void PromptCreate()
        {
            ModuleRegistry.Invalidate();
            var hosts = ModuleRegistry.Hosts();
            if (hosts.Count == 0)
            {
                EditorUtility.DisplayDialog("ShaderDIY", "还没有任何 Host Shader。请确认 Assets/ShaderDIY/Shaders/Sources 下有 .sdiy 文件。", "好");
                return;
            }
            var host = hosts[0];
            for (int i = 0; i < hosts.Count; i++)
            {
                if (hosts[i].document != null && hosts[i].document.CategoryEnum == ShaderDIYCategory.PBR)
                {
                    host = hosts[i];
                    break;
                }
            }
            var name = _newName + "_" + (_graphs.Length + 1);
            _graph = ShaderDIYGraphFactory.Create(name, host);
            RefreshGraphs();
        }

        void PromptDelete()
        {
            if (_graph == null)
                return;
            var name = _graph.name;
            if (!EditorUtility.DisplayDialog("删除材质", "删除「" + name + "」？会同时删掉对应的材质球和生成的 Shader。", "删除", "取消"))
                return;
            ShaderDIYGraphFactory.Delete(_graph);
            _graph = null;
            RefreshGraphs();
            if (_graph == null)
                _graph = ShaderDIYGraphFactory.EnsureDefault();
            Repaint();
        }

        void ShowSettingsMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("新建 Host Shader", "从模板生成一份骨架 .sdiy，不会改你现有公式。"), false, () => ShaderDIYCreatorWindow.Open(false));
            menu.AddItem(new GUIContent("新建 Module", "生成一个可替换模块 .sdiym，参数会出现在第 3 区。"), false, () => ShaderDIYCreatorWindow.Open(true));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("打开当前 Host", "用文本打开正在用的骨架源文件。"), false, OpenCurrentHost);
            menu.AddItem(new GUIContent("强制重新编译", "立刻按当前模块组合重新生成 Shader。"), false, () =>
            {
                if (_graph != null)
                    ShaderDIYCompiler.Compile(_graph, true);
            });
            menu.AddItem(new GUIContent("打开格式说明", "查看 .sdiy / .sdiym 怎么写。"), false, () =>
            {
                var obj = AssetDatabase.LoadAssetAtPath<TextAsset>(ShaderDIYPaths.FormatDoc);
                if (obj != null)
                    AssetDatabase.OpenAsset(obj);
                else
                    EditorUtility.RevealInFinder(ShaderDIYPaths.ToAbsolute(ShaderDIYPaths.FormatDoc));
            });
            menu.ShowAsContext();
        }

        SdiyAsset ResolveHost()
        {
            if (_graph == null)
                return null;
            var host = ModuleRegistry.FindHostByGuid(_graph.hostGuid);
            if (host == null)
                host = ModuleRegistry.FindHostByName(_graph.hostName);
            return host;
        }

        void OpenCurrentHost()
        {
            if (_graph == null)
                return;
            var host = ModuleRegistry.FindHostByGuid(_graph.hostGuid);
            if (host != null)
                SourceOpener.OpenAsset(host);
        }
    }
}
