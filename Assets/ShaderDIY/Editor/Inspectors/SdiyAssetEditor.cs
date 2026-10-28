using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    [CustomEditor(typeof(SdiyAsset))]
    public class SdiyAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (SdiyAsset)target;
            var doc = asset.document;
            EditorGUILayout.LabelField("ShaderDIY", EditorStyles.boldLabel);
            if (doc == null)
            {
                EditorGUILayout.HelpBox("解析失败：document 为空", MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField(ShaderDIYTips.L("种类", "Host 是骨架，Module 是可替换的一块光照。"), doc.kind.ToString());
            EditorGUILayout.LabelField(ShaderDIYTips.L("名字", "这份文件在材质工坊里显示的名字。"), doc.name);
            EditorGUILayout.LabelField(ShaderDIYTips.L("大类", "和第 1 区大类对应：PBR / Toon / Unlit / Custom。"), doc.category);
            if (doc.kind == SdiyAssetKind.Module)
                EditorGUILayout.LabelField(ShaderDIYTips.L("槽位", "这个模块插在哪一个槽，例如直接漫反、D、F0。"), doc.slot);
            EditorGUILayout.LabelField(ShaderDIYTips.L("槽位数", "Host 里声明了几个可替换槽。"), doc.slots != null ? doc.slots.Count.ToString() : "0");
            EditorGUILayout.LabelField(ShaderDIYTips.L("属性数", "会出现在第 2 / 3 区的材质参数个数。"), doc.properties != null ? doc.properties.Count.ToString() : "0");
            if (!string.IsNullOrEmpty(doc.parseError))
                EditorGUILayout.HelpBox(doc.parseError, MessageType.Error);
            EditorGUILayout.Space();
            if (GUILayout.Button(ShaderDIYTips.L("用文本编辑源文件", ShaderDIYTips.OpenSource)))
                SourceOpener.OpenAsset(asset);
            if (GUILayout.Button(ShaderDIYTips.L("在材质工坊中打开", ShaderDIYTips.OpenWorkshop)))
                ShaderDIYWindow.Open();
        }
    }
}
