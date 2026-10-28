using UnityEngine;

namespace ShaderDIY
{
    public class SdiyAsset : ScriptableObject
    {
        [TextArea(4, 12)]
        public string sourceText;
        public SdiyDocument document;
        public string sourcePath;
        public bool isModule;
    }
}
