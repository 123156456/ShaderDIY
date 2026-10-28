using UnityEngine;

namespace ShaderDIY
{
    /// <summary>
    /// 任意物体被 ShaderDIY 赋材质后会带上此标记，便于识别来源与记录 Graph。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ShaderDIY/Binder")]
    public class ShaderDIYBinder : MonoBehaviour
    {
        public ShaderDIYGraph graph;
        public Material assignedMaterial;
    }
}
