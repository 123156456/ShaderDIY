using System.Collections.Generic;
using System.IO;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class ShaderDIYGraphFactory
    {
        public const string DefaultGraphName = "DefaultPBR";

        public static ShaderDIYGraph[] LoadAll()
        {
            ShaderDIYPaths.EnsureFolders();
            var guids = AssetDatabase.FindAssets("t:ShaderDIYGraph", new[] { ShaderDIYPaths.Root });
            var list = new List<ShaderDIYGraph>();
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var graph = AssetDatabase.LoadAssetAtPath<ShaderDIYGraph>(path);
                if (graph != null)
                    list.Add(graph);
            }
            return list.ToArray();
        }

        public static ShaderDIYGraph EnsureDefault()
        {
            var all = LoadAll();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == DefaultGraphName)
                    return all[i];
            }
            if (all.Length > 0)
                return all[0];

            ModuleRegistry.Invalidate();
            var host = ModuleRegistry.FindHostByName("ShaderDIY/PBR/Lit");
            if (host == null)
            {
                host = ModuleRegistry.FindByPath("Assets/ShaderDIY/Shaders/Sources/PBR_Lit.sdiy");
            }
            if (host == null)
            {
                var hosts = ModuleRegistry.Hosts();
                if (hosts.Count > 0)
                    host = hosts[0];
            }
            if (host == null)
            {
                Debug.LogWarning("[ShaderDIY] 找不到默认 Host，无法创建 DefaultPBR。");
                return null;
            }
            return Create(DefaultGraphName, host);
        }

        public static ShaderDIYGraph Create(string displayName, SdiyAsset host)
        {
            ShaderDIYPaths.EnsureFolders();
            if (host == null || host.document == null)
                throw new System.Exception("必须选择一个 Host Shader");

            var file = ShaderDIYPaths.SanitizeFileName(displayName);
            var graphPath = AssetDatabase.GenerateUniqueAssetPath(ShaderDIYPaths.UserMaterials + "/" + file + ".asset");
            var graph = ScriptableObject.CreateInstance<ShaderDIYGraph>();
            graph.name = Path.GetFileNameWithoutExtension(graphPath);
            BindHost(graph, host);

            AssetDatabase.CreateAsset(graph, graphPath);
            ShaderDIYCompiler.Compile(graph, true);
            AssetDatabase.SaveAssets();
            return graph;
        }

        public static void Delete(ShaderDIYGraph graph)
        {
            if (graph == null)
                return;
            var graphPath = AssetDatabase.GetAssetPath(graph);
            if (graph.material != null)
            {
                var matPath = AssetDatabase.GetAssetPath(graph.material);
                if (!string.IsNullOrEmpty(matPath))
                    AssetDatabase.DeleteAsset(matPath);
            }
            if (graph.generatedShader != null)
            {
                var shPath = AssetDatabase.GetAssetPath(graph.generatedShader);
                if (!string.IsNullOrEmpty(shPath) &&
                    shPath.Replace('\\', '/').StartsWith(ShaderDIYPaths.Generated, System.StringComparison.Ordinal))
                    AssetDatabase.DeleteAsset(shPath);
            }
            if (!string.IsNullOrEmpty(graphPath))
                AssetDatabase.DeleteAsset(graphPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BindHost(ShaderDIYGraph graph, SdiyAsset host)
        {
            var hostPath = AssetDatabase.GetAssetPath(host);
            if (string.IsNullOrEmpty(hostPath))
                hostPath = host.sourcePath;
            graph.hostPath = hostPath;
            graph.hostGuid = string.IsNullOrEmpty(hostPath) ? "" : AssetDatabase.AssetPathToGUID(hostPath);
            graph.hostName = host.document.name;
            graph.category = host.document.CategoryEnum;
            graph.slotChoices.Clear();
            for (int i = 0; i < host.document.slots.Count; i++)
            {
                var s = host.document.slots[i];
                graph.SetSlotModule(s.name, s.defaultModule);
            }
        }

        public static void ResetRenderState(ShaderDIYGraph graph)
        {
            if (graph == null)
                return;
            graph.SetFeature(ShaderDIYRenderState.KeyCull, ShaderDIYRenderState.DefaultCull);
            graph.SetFeature(ShaderDIYRenderState.KeyZWrite, ShaderDIYRenderState.DefaultZWrite);
            graph.SetFeature(ShaderDIYRenderState.KeyQueue, ShaderDIYRenderState.DefaultQueue);
            graph.SetFeature(ShaderDIYRenderState.KeyReceiveShadows, ShaderDIYRenderState.DefaultReceiveShadows);
            graph.SetFeature(ShaderDIYRenderState.KeyLightingMode, ShaderDIYRenderState.DefaultLighting);
            graph.SetFeature(ShaderDIYRenderState.KeyEngine, ShaderDIYRenderState.DefaultEngine);
        }
    }
}
