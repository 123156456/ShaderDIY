using System.Collections.Generic;
using System.IO;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class ModuleRegistry
    {
        static List<SdiyAsset> _cache;
        static Dictionary<string, SdiyAsset> _byPath;
        static double _lastScan;

        public static void Invalidate()
        {
            _cache = null;
            _byPath = null;
        }

        public static IReadOnlyList<SdiyAsset> All()
        {
            Ensure();
            return _cache;
        }

        public static List<SdiyAsset> Hosts()
        {
            Ensure();
            var list = new List<SdiyAsset>();
            for (int i = 0; i < _cache.Count; i++)
            {
                if (_cache[i] != null && _cache[i].document != null && _cache[i].document.kind == SdiyAssetKind.Host)
                    list.Add(_cache[i]);
            }
            return list;
        }

        public static List<SdiyAsset> Hosts(ShaderDIYCategory category)
        {
            var all = Hosts();
            var list = new List<SdiyAsset>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].document.CategoryEnum == category)
                    list.Add(all[i]);
            }
            return list;
        }

        public static SdiyAsset FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            Ensure();
            path = path.Replace('\\', '/');
            SdiyAsset asset;
            if (_byPath != null && _byPath.TryGetValue(path, out asset))
                return asset;
            return null;
        }

        public static SdiyAsset FindHostByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;
            return FindByPath(AssetDatabase.GUIDToAssetPath(guid));
        }

        public static SdiyAsset FindHostByName(string name)
        {
            Ensure();
            for (int i = 0; i < _cache.Count; i++)
            {
                var a = _cache[i];
                if (a != null && a.document != null && a.document.kind == SdiyAssetKind.Host &&
                    (a.document.name == name || a.name == name))
                    return a;
            }
            return null;
        }

        public static List<SdiyAsset> ModulesForSlot(string slotName)
        {
            Ensure();
            var list = new List<SdiyAsset>();
            for (int i = 0; i < _cache.Count; i++)
            {
                var a = _cache[i];
                if (a == null || a.document == null || a.document.kind != SdiyAssetKind.Module)
                    continue;
                if (string.Equals(a.document.slot, slotName, System.StringComparison.OrdinalIgnoreCase))
                    list.Add(a);
            }
            return list;
        }

        public static SdiyAsset FindModule(string slotName, string moduleName)
        {
            var list = ModulesForSlot(slotName);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].document != null && list[i].document.name == moduleName)
                    return list[i];
            }
            return null;
        }

        static void Ensure()
        {
            if (_cache != null && EditorApplication.timeSinceStartup - _lastScan < 1.5)
                return;
            _cache = new List<SdiyAsset>();
            _byPath = new Dictionary<string, SdiyAsset>();

            CollectFromImported();
            CollectFromDisk("*.sdiy", false);
            CollectFromDisk("*.sdiym", true);

            _lastScan = EditorApplication.timeSinceStartup;
        }

        static void CollectFromImported()
        {
            var guids = AssetDatabase.FindAssets("t:SdiyAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                var asset = AssetDatabase.LoadAssetAtPath<SdiyAsset>(path);
                Add(path, asset);
            }
        }

        static void CollectFromDisk(string pattern, bool module)
        {
            var root = ShaderDIYPaths.ToAbsolute(ShaderDIYPaths.Root);
            if (!Directory.Exists(root))
                return;
            var files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var abs = files[i].Replace('\\', '/');
                var assetPath = ToAssetPath(abs);
                if (string.IsNullOrEmpty(assetPath) || _byPath.ContainsKey(assetPath))
                    continue;
                if (assetPath.IndexOf("/Templates/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                try
                {
                    var text = File.ReadAllText(abs);
                    var doc = SdiyParser.Parse(text, assetPath);
                    if (module)
                        doc.kind = SdiyAssetKind.Module;
                    var asset = ScriptableObject.CreateInstance<SdiyAsset>();
                    asset.hideFlags = HideFlags.HideAndDontSave;
                    asset.sourceText = text;
                    asset.document = doc;
                    asset.sourcePath = assetPath;
                    asset.isModule = doc.kind == SdiyAssetKind.Module;
                    asset.name = Path.GetFileNameWithoutExtension(assetPath);
                    Add(assetPath, asset);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[ShaderDIY] 解析失败 " + assetPath + "\n" + e.Message);
                }
            }
        }

        static void Add(string path, SdiyAsset asset)
        {
            if (asset == null || string.IsNullOrEmpty(path))
                return;
            path = path.Replace('\\', '/');
            if (_byPath.ContainsKey(path))
                return;
            _byPath.Add(path, asset);
            _cache.Add(asset);
        }

        static string ToAssetPath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            var data = Application.dataPath.Replace('\\', '/');
            if (absolute.StartsWith(data, System.StringComparison.OrdinalIgnoreCase))
                return "Assets" + absolute.Substring(data.Length);
            var idx = absolute.IndexOf("/Assets/", System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return absolute.Substring(idx + 1);
            return null;
        }
    }

    class SdiyAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool dirty = false;
            for (int i = 0; i < imported.Length; i++)
            {
                if (IsSdiy(imported[i])) { dirty = true; break; }
            }
            if (!dirty)
            {
                for (int i = 0; i < deleted.Length; i++)
                {
                    if (IsSdiy(deleted[i])) { dirty = true; break; }
                }
            }
            if (!dirty)
                return;
            ModuleRegistry.Invalidate();
            ShaderDIYCompiler.MarkAllGraphsDirty();
        }

        static bool IsSdiy(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            return path.EndsWith(".sdiy", System.StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".sdiym", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
