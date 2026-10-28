using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class SourceOpener
    {
        public static void Open(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;
            var abs = ShaderDIYPaths.ToAbsolute(assetPath);
            InternalEditorUtility.OpenFileAtLineExternal(abs, 1);
        }

        public static void OpenAsset(Object obj)
        {
            if (obj == null)
                return;
            Open(AssetDatabase.GetAssetPath(obj));
        }
    }
}
