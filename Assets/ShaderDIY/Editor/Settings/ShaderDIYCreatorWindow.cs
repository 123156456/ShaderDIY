using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public class ShaderDIYCreatorWindow : EditorWindow
    {
        static readonly string[] KnownSlots =
        {
            "DirectDiffuse",
            "DirectSpecular",
            "IndirectDiffuse",
            "IndirectSpecular",
            "NDF",
            "G",
            "F",
            "F0",
            "Transmittance",
            "InScattering",
            "Light",
            "LayerSSS",
            "LayerParallax",
            "LayerRefraction",
            "MainTex",
            "Mask",
            "Noise",
            "Emission",
            "Rim",
            "Outline",
            "Clip",
            "Blend",
            "Soft",
            "CustomTerm"
        };

        bool _module;
        string _name = "NewShader";
        string _slot = "DirectDiffuse";
        string _customSlot = "CustomTerm";
        int _slotIdx;
        string _category = "PBR";
        bool _withSlot;
        Vector2 _scroll;

        void OnEnable()
        {
            wantsMouseMove = true;
        }

        public static void Open(bool module)
        {
            var w = GetWindow<ShaderDIYCreatorWindow>(true, module ? "新建 Module" : "新建 Host Shader", true);
            w._module = module;
            w._name = module ? "NewModule" : "NewHost";
            w._category = module ? "PBR" : "Custom";
            w._slot = "DirectDiffuse";
            w._slotIdx = 0;
            w._customSlot = "CustomTerm";
            w._withSlot = false;
            w.minSize = new Vector2(460, 320);
            w.Show();
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
                Repaint();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(_module ? "新建可替换模块 (.sdiym)" : "新建 Host 骨架 (.sdiy)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _module
                    ? "模块只负责一个槽位的函数和它自己的参数。参数会出现在第 3 区对应模块下面，公式会出现在第 4 区。"
                    : "Host 是材质骨架：贴图进第 2 区，自身参数进第 3 区「基础参数」。第 1 区不用写。生成后即可编译，不必先做模块。",
                MessageType.Info);

            _name = EditorGUILayout.TextField(ShaderDIYTips.L("名字", ShaderDIYTips.NewName), _name);
            _category = EditorGUILayout.TextField(ShaderDIYTips.L("大类 Category", ShaderDIYTips.NewCategory), _category);
            EditorGUILayout.LabelField("建议填 PBR / Toon / Unlit / Custom，和第 1 区大类对应。", EditorStyles.miniLabel);

            if (_module)
            {
                DrawSlotPopup();
                EditorGUILayout.LabelField("函数签名：" + SignatureForSlot(_slot), EditorStyles.miniLabel);
            }
            else
            {
                _withSlot = EditorGUILayout.Toggle(ShaderDIYTips.L("同时生成一个可替换槽 + 默认模块", ShaderDIYTips.WithSlot), _withSlot);
                if (_withSlot)
                {
                    DrawSlotPopup();
                    EditorGUILayout.HelpBox("会在 Host 里写入 [SLOT]，并在 Modules/" + _category + " 下生成同名默认模块。Default 必须能找到，否则材质编不过。", MessageType.None);
                }
            }

            EditorGUILayout.Space(12);
            if (GUILayout.Button(ShaderDIYTips.L("创建并打开", ShaderDIYTips.CreateFile), GUILayout.Height(28)))
                Create();
            EditorGUILayout.EndScrollView();
        }

        void DrawSlotPopup()
        {
            _slotIdx = EditorGUILayout.Popup(ShaderDIYTips.L("槽位 Slot", ShaderDIYTips.NewSlot), _slotIdx, KnownSlots);
            if (_slotIdx < 0 || _slotIdx >= KnownSlots.Length)
                _slotIdx = 0;
            if (KnownSlots[_slotIdx] == "CustomTerm")
            {
                _customSlot = EditorGUILayout.TextField(ShaderDIYTips.L("自定义槽名", ShaderDIYTips.CustomSlot), _customSlot);
                _slot = string.IsNullOrEmpty(_customSlot) ? "CustomTerm" : _customSlot.Trim();
            }
            else
                _slot = KnownSlots[_slotIdx];
        }

        void Create()
        {
            ShaderDIYPaths.EnsureFolders();
            if (string.IsNullOrEmpty(_name))
            {
                EditorUtility.DisplayDialog("ShaderDIY", "请填写名字。", "好");
                return;
            }

            if (_module)
            {
                WriteModule(_name, _slot, _category);
            }
            else
            {
                WriteHost(_name, _category, _withSlot ? _slot : null);
                if (_withSlot)
                    WriteModule(_name, _slot, _category);
            }
            Close();
        }

        void WriteHost(string name, string category, string slot)
        {
            var templatePath = ShaderDIYPaths.Templates + "/HostTemplate.sdiy";
            if (!File.Exists(ShaderDIYPaths.ToAbsolute(templatePath)))
            {
                EditorUtility.DisplayDialog("ShaderDIY", "找不到模板: " + templatePath, "好");
                return;
            }

            var text = File.ReadAllText(ShaderDIYPaths.ToAbsolute(templatePath));
            text = text.Replace("ShaderDIY/Custom/NewHost", "ShaderDIY/" + category + "/" + name);
            text = text.Replace("Category = Custom", "Category = " + category);
            text = text.Replace("NewHost", name);

            if (!string.IsNullOrEmpty(slot))
            {
                var sig = SignatureForSlot(slot);
                var slotLine = "[SLOT " + slot + " Default=\"" + name + "\" Signature=\"" + sig + "\"]";
                text = text.Replace(
                    "// [SLOT CustomTerm Default=\"DefaultTerm\" Signature=\"half3 EvaluateCustomTerm(ShadingContext ctx)\"]",
                    slotLine);
                var call = FuncName(slot) + "(ctx)";
                var use = slot == "NDF"
                    ? "            ctx.roughness = 0.5h;\n            color *= " + call + ";"
                    : "            color = " + call + ";";
                var inject =
                    "            ShadingContext ctx = (ShadingContext)0;\n" +
                    "            ctx.albedo = albedo.rgb;\n" +
                    "            ctx.N = normalize(i.normalWS);\n" +
                    "            ctx.V = normalize(i.viewDirWS);\n" +
                    "            ctx.NdotV = saturate(dot(ctx.N, ctx.V));\n" +
                    "            Light mainLight = GetMainLight();\n" +
                    "            ShaderDIY_FillLight(ctx, mainLight.direction, mainLight.color,\n" +
                    "                mainLight.distanceAttenuation * mainLight.shadowAttenuation);\n" +
                    use;
                text = text.Replace(
                    "            // 声明槽位后，在这里组装 ShadingContext 并调用模块函数，例如：\r\n" +
                    "            // ShadingContext ctx = (ShadingContext)0;\r\n" +
                    "            // ctx.albedo = albedo.rgb; ctx.N = normalize(i.normalWS); ctx.V = normalize(i.viewDirWS);\r\n" +
                    "            // color += EvaluateCustomTerm(ctx);",
                    inject);
                text = text.Replace(
                    "            // 声明槽位后，在这里组装 ShadingContext 并调用模块函数，例如：\n" +
                    "            // ShadingContext ctx = (ShadingContext)0;\n" +
                    "            // ctx.albedo = albedo.rgb; ctx.N = normalize(i.normalWS); ctx.V = normalize(i.viewDirWS);\n" +
                    "            // color += EvaluateCustomTerm(ctx);",
                    inject);
            }

            var dest = UniquePath(ShaderDIYPaths.Sources, name, ".sdiy");
            SaveAndOpen(dest, text);
        }

        void WriteModule(string name, string slot, string category)
        {
            var templatePath = ShaderDIYPaths.Templates + "/ModuleTemplate.sdiym";
            if (!File.Exists(ShaderDIYPaths.ToAbsolute(templatePath)))
            {
                EditorUtility.DisplayDialog("ShaderDIY", "找不到模板: " + templatePath, "好");
                return;
            }

            var text = File.ReadAllText(ShaderDIYPaths.ToAbsolute(templatePath));
            text = text.Replace("NewModule", name);
            text = text.Replace("CustomTerm", slot);
            text = text.Replace("Category=\"Custom\"", "Category=\"" + category + "\"");
            text = text.Replace("_SLOT_NEW", "_" + Ident(slot).ToUpperInvariant() + "_" + Ident(name).ToUpperInvariant());
            text = text.Replace("FUNC_SIGNATURE", SignatureForSlot(slot));
            text = text.Replace("FUNC_BODY", BodyForSlot(slot));

            var folder = ShaderDIYPaths.Modules + "/" + ShaderDIYPaths.SanitizeFileName(category);
            ShaderDIYPaths.EnsureFolder(folder);
            var dest = UniquePath(folder, name, ".sdiym");
            SaveAndOpen(dest, text);
        }

        static void SaveAndOpen(string dest, string text)
        {
            text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllText(ShaderDIYPaths.ToAbsolute(dest), text);
            AssetDatabase.ImportAsset(dest);
            ModuleRegistry.Invalidate();
            var asset = AssetDatabase.LoadAssetAtPath<Object>(dest);
            if (asset != null)
                Selection.activeObject = asset;
            SourceOpener.Open(dest);
        }

        static string UniquePath(string folder, string name, string ext)
        {
            return AssetDatabase.GenerateUniqueAssetPath(folder + "/" + ShaderDIYPaths.SanitizeFileName(name) + ext);
        }

        public static string SignatureForSlot(string slot)
        {
            switch (slot)
            {
                case "DirectDiffuse": return "float3 EvaluateDirectDiffuse(ShadingContext ctx)";
                case "DirectSpecular": return "float3 EvaluateDirectSpecular(ShadingContext ctx)";
                case "IndirectDiffuse": return "float3 EvaluateIndirectDiffuse(ShadingContext ctx)";
                case "IndirectSpecular": return "float3 EvaluateIndirectSpecular(ShadingContext ctx)";
                case "NDF": return "float EvaluateNDF(ShadingContext ctx)";
                case "G": return "float EvaluateG(ShadingContext ctx)";
                case "F": return "float3 EvaluateF(ShadingContext ctx)";
                case "F0": return "float3 EvaluateF0(ShadingContext ctx)";
                case "Transmittance": return "float3 Transmittance(float3 p1, float3 p2, float3 planetCenter)";
                case "InScattering": return "float3 EvaluateAtmosphere(float3 positionWS)";
                case "Light": return "void EvaluateLight(inout ShadingContext ctx)";
                case "LayerSSS": return "float3 EvaluateLayerSSS(ShadingContext ctx)";
                case "LayerParallax": return "float3 EvaluateLayerParallax(ShadingContext ctx)";
                case "LayerRefraction": return "float4 EvaluateLayerRefraction(ShadingContext ctx, float3 result)";
                case "MainTex": return "float4 EvaluateFXMain(float2 uv)";
                case "Mask": return "float EvaluateFXMask(float2 uv)";
                case "Noise": return "void EvaluateFXNoise(inout float2 uv, ShaderDIYVaryings i, inout float3 dirTint)";
                case "Emission": return "float3 EvaluateFXEmission(float2 uv, float4 col)";
                case "Rim": return "float3 EvaluateFXRim(ShaderDIYVaryings i)";
                case "Outline": return "void EvaluateFXOutline()";
                case "Clip": return "float4 EvaluateFXClip(float3 positionWS, float2 uv, float4 col)";
                case "Blend": return "void EvaluateFXBlend()";
                case "Soft": return "float4 EvaluateFXSoft(ShaderDIYVaryings i, float4 col)";
                default: return "float3 Evaluate" + Ident(slot) + "(ShadingContext ctx)";
            }
        }

        static string FuncName(string slot)
        {
            var sig = SignatureForSlot(slot);
            int sp = sig.LastIndexOf(' ');
            int par = sig.IndexOf('(');
            if (sp >= 0 && par > sp)
                return sig.Substring(sp + 1, par - sp - 1);
            return "Evaluate" + Ident(slot);
        }

        static string BodyForSlot(string slot)
        {
            switch (slot)
            {
                case "NDF":
                    return "half a = max(ctx.roughness * ctx.roughness, 0.002h);\n            half d = (ctx.NdotH * a - ctx.NdotH) * ctx.NdotH + 1.0h;\n            return a / max(3.14159265h * d * d, 0.0001h);";
                case "IndirectDiffuse":
                    return "return ctx.albedo * (1.0h - ctx.metallic) * ctx.bakedGI * _TermScale;";
                case "IndirectSpecular":
                    return "return ctx.f0 * ctx.bakedGI * _TermScale;";
                case "DirectSpecular":
                    return "return ctx.f0 * ctx.lightColor * (ctx.NdotH * ctx.attenuation * _TermScale);";
                default:
                    return "half3 diffuse = ctx.albedo * (1.0h - ctx.metallic);\n            return diffuse * ctx.lightColor * (ctx.NdotL * ctx.attenuation * _TermScale);";
            }
        }

        static string Ident(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "Term";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '_';
            }
            if (!char.IsLetter(chars[0]) && chars[0] != '_')
                return "T" + new string(chars);
            return new string(chars);
        }

    }
}
