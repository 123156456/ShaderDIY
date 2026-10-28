using System.IO;
using ShaderDIY;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ShaderDIY.Editor
{
    // 一个导入器管 .sdiy / .sdiym，避免两个类挤在同一脚本导致 meta 写成 instanceID:0
    [ScriptedImporter(3, new[] { "sdiy", "sdiym" })]
    public class SdiyImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = File.ReadAllText(ctx.assetPath);
            var doc = SdiyParser.Parse(text, ctx.assetPath);
            bool module = ctx.assetPath.EndsWith(".sdiym", System.StringComparison.OrdinalIgnoreCase);
            if (module)
                doc.kind = SdiyAssetKind.Module;
            else
                doc.kind = SdiyAssetKind.Host;

            var asset = ScriptableObject.CreateInstance<SdiyAsset>();
            asset.sourceText = text;
            asset.document = doc;
            asset.sourcePath = ctx.assetPath;
            asset.isModule = doc.kind == SdiyAssetKind.Module;
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset("main", asset);
            ctx.SetMainObject(asset);
        }
    }
}
