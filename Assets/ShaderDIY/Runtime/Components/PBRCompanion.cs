using UnityEngine;

namespace ShaderDIY
{
    /// <summary>
    /// 示例伴随脚本：当材质的 Host 声明了 CompanionScripts 时，赋予材质会一并挂上。
    /// 真正的材质逻辑由你后续在此类中扩展。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ShaderDIY/PBR Companion")]
    public class PBRCompanion : MonoBehaviour
    {
        [Tooltip("由 ShaderDIY 赋予材质时自动添加，可在此写与材质配套的运行时逻辑。")]
        public Material boundMaterial;

        void Reset()
        {
            var r = GetComponent<Renderer>();
            if (r != null)
                boundMaterial = r.sharedMaterial;
        }
    }
}
