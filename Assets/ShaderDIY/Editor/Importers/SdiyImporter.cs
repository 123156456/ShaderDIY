using System.IO;
using ShaderDIY;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ShaderDIY.Editor
{
    [ScriptedImporter(2, "sdiy")]
    public class SdiyHostImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            SdiyImportUtility.Import(ctx, false);
        }
    }

    [ScriptedImporter(2, "sdiym")]
    public class SdiyModuleImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            SdiyImportUtility.Import(ctx, true);
        }
    }

    static class SdiyImportUtility
    {
        public static void Import(AssetImportContext ctx, bool expectModule)
        {
            var text = File.ReadAllText(ctx.assetPath);
            var doc = SdiyParser.Parse(text, ctx.assetPath);
            if (expectModule)
                doc.kind = SdiyAssetKind.Module;
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
