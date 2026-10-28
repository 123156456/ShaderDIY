using System.IO;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class ShaderDIYPaths
    {
        public const string Root = "Assets/ShaderDIY";
        public const string Generated = "Assets/ShaderDIY/Generated";
        public const string UserMaterials = "Assets/ShaderDIY/UserMaterials";
        public const string Sources = "Assets/ShaderDIY/Shaders/Sources";
        public const string Modules = "Assets/ShaderDIY/Shaders/Modules";
        public const string Templates = "Assets/ShaderDIY/Shaders/Templates";
        public const string Library = "Assets/ShaderDIY/Shaders/Library";
        public const string CoreHlsl = "Assets/ShaderDIY/Shaders/Library/ShaderDIYCore.hlsl";
        public const string FormatDoc = "Assets/ShaderDIY/Documentation/SdiyFormat.txt";

        public static string GeneratedRelativeInclude
        {
            get { return "../Shaders/Library/ShaderDIYCore.hlsl"; }
        }

        public static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(Generated);
            EnsureFolder(UserMaterials);
            EnsureFolder(Sources);
            EnsureFolder(Modules);
            EnsureFolder(Path.Combine(Modules, "PBR").Replace('\\', '/'));
            EnsureFolder(Path.Combine(Modules, "Unlit").Replace('\\', '/'));
            EnsureFolder(Templates);
            EnsureFolder(Library);
            EnsureFolder("Assets/ShaderDIY/Documentation");
            EnsureFolder("Assets/ShaderDIY/Editor");
            EnsureFolder("Assets/ShaderDIY/Runtime");
        }

        public static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;
            assetPath = assetPath.Replace('\\', '/');
            if (AssetDatabaseExists(assetPath))
                return;
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabaseExists(next))
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static bool AssetDatabaseExists(string path)
        {
            if (UnityEditor.AssetDatabase.IsValidFolder(path))
                return true;
            return Directory.Exists(ToAbsolute(path));
        }

        public static string ToAbsolute(string assetPath)
        {
            var root = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(root ?? "", assetPath).Replace('\\', '/');
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Untitled";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
        }
    }
}
