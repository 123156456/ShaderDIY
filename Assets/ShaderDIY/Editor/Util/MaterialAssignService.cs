using System;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    [InitializeOnLoad]
    public static class MaterialAssignService
    {
        public const string DragKey = "ShaderDIY.Graph";

        static MaterialAssignService()
        {
            SceneView.duringSceneGui += OnScene;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchy;
        }

        public static void BeginDrag(ShaderDIYGraph graph)
        {
            if (graph == null || graph.material == null)
                return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new UnityEngine.Object[] { graph.material };
            DragAndDrop.SetGenericData(DragKey, graph);
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            DragAndDrop.StartDrag("ShaderDIY Material");
        }

        public static void ApplyTo(GameObject go, ShaderDIYGraph graph)
        {
            if (go == null || graph == null || graph.material == null)
                return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                ApplyToRenderer(renderers[i], graph);
        }

        public static void ApplyToRenderer(Renderer renderer, ShaderDIYGraph graph)
        {
            if (renderer == null || graph == null || graph.material == null)
                return;
            Undo.RecordObject(renderer, "ShaderDIY 赋予材质");
            renderer.sharedMaterial = graph.material;

            var binder = renderer.GetComponent<ShaderDIYBinder>();
            if (binder == null)
                binder = Undo.AddComponent<ShaderDIYBinder>(renderer.gameObject);
            binder.graph = graph;
            binder.assignedMaterial = graph.material;
            EditorUtility.SetDirty(binder);

            var host = ModuleRegistry.FindHostByGuid(graph.hostGuid);
            if (host == null || host.document == null || host.document.companionScripts == null)
                return;
            for (int i = 0; i < host.document.companionScripts.Count; i++)
            {
                var typeName = host.document.companionScripts[i];
                var type = FindType(typeName);
                if (type == null || !typeof(Component).IsAssignableFrom(type))
                    continue;
                if (renderer.GetComponent(type) == null)
                    Undo.AddComponent(renderer.gameObject, type);
            }
        }

        static void OnScene(SceneView view)
        {
            var e = Event.current;
            if (e == null)
                return;
            var graph = DragAndDrop.GetGenericData(DragKey) as ShaderDIYGraph;
            if (graph == null)
                return;
            if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
            {
                var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        ApplyTo(hit.collider.gameObject, graph);
                    }
                    e.Use();
                    return;
                }
                var picked = HandleUtility.PickGameObject(e.mousePosition, false);
                if (picked != null)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        ApplyTo(picked, graph);
                    }
                    e.Use();
                }
            }
        }

        static void OnHierarchy(int instanceId, Rect rect)
        {
            var e = Event.current;
            if (e == null)
                return;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return;
            if (!rect.Contains(e.mousePosition))
                return;
            var graph = DragAndDrop.GetGenericData(DragKey) as ShaderDIYGraph;
            if (graph == null)
                return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                ApplyTo(obj, graph);
            }
            e.Use();
        }

        static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            var t = Type.GetType(name);
            if (t != null)
                return t;
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                t = asms[i].GetType(name);
                if (t != null)
                    return t;
            }
            return null;
        }
    }
}
