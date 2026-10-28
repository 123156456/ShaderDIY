using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShaderDIY;
using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class ShaderDIYCompiler
    {
        static bool _dirtyGraphs;

        public static void MarkAllGraphsDirty()
        {
            _dirtyGraphs = true;
        }

        public static void RebuildDirtyGraphsIfNeeded()
        {
            if (!_dirtyGraphs)
                return;
            _dirtyGraphs = false;
            var guids = AssetDatabase.FindAssets("t:ShaderDIYGraph");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var graph = AssetDatabase.LoadAssetAtPath<ShaderDIYGraph>(path);
                if (graph != null)
                    Compile(graph, false);
            }
        }

        public static bool Compile(ShaderDIYGraph graph, bool force)
        {
            if (graph == null)
                return false;

            ShaderDIYPaths.EnsureFolders();
            var host = ModuleRegistry.FindHostByGuid(graph.hostGuid);
            if (host == null)
                host = ModuleRegistry.FindByPath(graph.hostPath);
            if (host == null)
                host = ModuleRegistry.FindHostByName(graph.hostName);
            if (host == null || host.document == null)
            {
                graph.lastCompileError = "找不到 Host Shader";
                EditorUtility.SetDirty(graph);
                return false;
            }

            var hostDoc = host.document;
            var modules = new Dictionary<string, SdiyDocument>();
            var mergedProps = new List<SdiyProperty>();
            AddRange(mergedProps, hostDoc.features, null);
            AddRange(mergedProps, hostDoc.properties, null);

            int alu = 8, tex = 0, interp = 4;
            var keywords = new List<string>();
            var slotOrder = new List<SdiySlot>();
            for (int i = 0; i < hostDoc.slots.Count; i++)
                slotOrder.Add(hostDoc.slots[i]);
            slotOrder.Sort(CompareSlotEmitOrder);

            bool fxHost = IsFxHost(hostDoc);
            var sbSlots = new StringBuilder();
            for (int i = 0; i < slotOrder.Count; i++)
            {
                var slot = slotOrder[i];
                var chosen = graph.GetSlotModule(slot.name);
                if (string.IsNullOrEmpty(chosen))
                    chosen = slot.defaultModule;
                graph.SetSlotModule(slot.name, chosen);

                var modAsset = ModuleRegistry.FindModule(slot.name, chosen);
                if (modAsset == null || modAsset.document == null)
                {
                    graph.lastCompileError = "槽位 " + slot.name + " 缺少模块 " + chosen;
                    EditorUtility.SetDirty(graph);
                    return false;
                }

                var mod = modAsset.document;
                modules[slot.name] = mod;
                AddRange(mergedProps, mod.properties, slot.name);
                alu += mod.cost != null ? mod.cost.alu : EstimateAlu(mod.hlslInclude);
                tex += mod.cost != null ? mod.cost.texture : 0;
                interp += mod.cost != null ? mod.cost.interpolator : 0;
                if (mod.keywords != null)
                    keywords.AddRange(mod.keywords);

                bool emitHlsl = ShouldEmitModuleHlsl(mod, fxHost);
                if (emitHlsl && !string.IsNullOrEmpty(slot.signature) &&
                    (mod.hlslInclude == null || mod.hlslInclude.IndexOf(ExtractFuncName(slot.signature), StringComparison.Ordinal) < 0))
                {
                    graph.lastCompileError = "模块 " + mod.name + " 未实现签名 " + slot.signature;
                    EditorUtility.SetDirty(graph);
                    return false;
                }
                if (emitHlsl)
                {
                    sbSlots.AppendLine("// ---- SLOT " + slot.name + " = " + mod.name + " ----");
                    sbSlots.AppendLine(mod.hlslInclude ?? "");
                    sbSlots.AppendLine();
                }
            }

            tex += CountTextures(mergedProps);
            var features = ResolveFeatures(graph, hostDoc);
            int variants = 1;
            bool fx = IsFxHost(hostDoc);
            if (!fx)
            {
                if (features.receiveShadows)
                    variants *= 4;
                if (features.multiLight)
                    variants *= 2;
            }
            interp = fx ? EstimateFxInterpolators(keywords) : interp;

            var hash = ComputeHash(host.sourcePath, modules, features, host.sourceText);
            if (!force && graph.lastCompositionHash == hash && graph.generatedShader != null && string.IsNullOrEmpty(graph.lastCompileError))
            {
                graph.lastAluCost = alu;
                graph.lastTexCost = tex;
                graph.lastInterpolatorCost = interp;
                graph.lastVariantEstimate = variants;
                ApplyNonCompileFeatures(graph.material, features);
                return true;
            }

            var shaderName = "ShaderDIY/User/" + ShaderDIYPaths.SanitizeFileName(graph.name);
            var src = BuildUnityShader(shaderName, hostDoc, mergedProps, sbSlots.ToString(), features, keywords);
            var fileName = ShaderDIYPaths.SanitizeFileName(graph.name) + "_" + hash.Substring(0, 8) + ".shader";
            var assetPath = ShaderDIYPaths.Generated + "/" + fileName;
            var abs = ShaderDIYPaths.ToAbsolute(assetPath);

            if (graph.generatedShader != null)
            {
                var oldPath = AssetDatabase.GetAssetPath(graph.generatedShader);
                if (!string.IsNullOrEmpty(oldPath) && oldPath != assetPath &&
                    oldPath.Replace('\\', '/').StartsWith(ShaderDIYPaths.Generated, StringComparison.Ordinal))
                    AssetDatabase.DeleteAsset(oldPath);
            }

            var sw = Stopwatch.StartNew();
            src = src.Replace("\r\n", "\n").Replace("\n", "\r\n");
            File.WriteAllText(abs, src, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            sw.Stop();

            if (shader == null)
            {
                graph.lastCompileError = "生成 Shader 资源失败: " + assetPath;
                EditorUtility.SetDirty(graph);
                return false;
            }

            if (graph.material == null)
            {
                graph.material = CreateMaterialAsset(graph, shader);
            }
            else
            {
                graph.material.shader = shader;
                EditorUtility.SetDirty(graph.material);
            }

            ApplyNonCompileFeatures(graph.material, features);

            graph.generatedShader = shader;
            graph.hostName = hostDoc.name;
            graph.category = hostDoc.CategoryEnum;
            graph.lastCompositionHash = hash;
            graph.lastCompileError = FormatShaderMessages(shader);
            graph.lastAluCost = alu;
            graph.lastTexCost = tex;
            graph.lastInterpolatorCost = interp;
            graph.lastVariantEstimate = variants;
            graph.lastCompileMs = sw.Elapsed.TotalMilliseconds;
            EditorUtility.SetDirty(graph);
            return string.IsNullOrEmpty(graph.lastCompileError) || graph.lastCompileError.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public struct FeatureState
        {
            public string cull;
            public bool zwrite;
            public string queue;
            public bool receiveShadows;
            public bool multiLight;
            public string engine;
            public bool transparent;
        }

        public static FeatureState ResolveFeatures(ShaderDIYGraph graph, SdiyDocument host)
        {
            var f = new FeatureState
            {
                cull = graph.GetFeature(ShaderDIYRenderState.KeyCull, ShaderDIYRenderState.DefaultCull),
                zwrite = ParseOn(graph.GetFeature(ShaderDIYRenderState.KeyZWrite, ShaderDIYRenderState.DefaultZWrite)),
                queue = graph.GetFeature(ShaderDIYRenderState.KeyQueue, ShaderDIYRenderState.DefaultQueue),
                receiveShadows = ParseOn(graph.GetFeature(ShaderDIYRenderState.KeyReceiveShadows, ShaderDIYRenderState.DefaultReceiveShadows)),
                multiLight = graph.GetFeature(ShaderDIYRenderState.KeyLightingMode, ShaderDIYRenderState.DefaultLighting)
                    .IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0,
                engine = ShaderDIYRenderState.NormalizeEngine(
                    graph.GetFeature(ShaderDIYRenderState.KeyEngine, ShaderDIYRenderState.DefaultEngine))
            };
            f.transparent = f.queue.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;
            if (f.transparent)
                f.zwrite = false;
            return f;
        }

        static bool ParseOn(string v)
        {
            if (string.IsNullOrEmpty(v))
                return false;
            return v == "1" || v.Equals("On", StringComparison.OrdinalIgnoreCase) ||
                   v.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        static string BuildUnityShader(string shaderName, SdiyDocument host, List<SdiyProperty> props,
            string slotHlsl, FeatureState f, List<string> keywords)
        {
            var sb = new StringBuilder(16 * 1024);
            sb.Append("Shader \"").Append(shaderName).AppendLine("\"");
            sb.AppendLine("{");
            sb.AppendLine("    Properties");
            sb.AppendLine("    {");
            for (int i = 0; i < props.Count; i++)
                AppendProperty(sb, props[i]);
            sb.AppendLine("    }");
            sb.AppendLine("    SubShader");
            sb.AppendLine("    {");
            sb.Append("        Tags { \"RenderPipeline\"=\"UniversalPipeline\" \"RenderType\"=\"");
            sb.Append(f.transparent ? "Transparent" : "Opaque");
            sb.Append("\" \"Queue\"=\"").Append(f.queue).AppendLine("\" }");
            sb.Append("        Cull ").Append(f.cull).AppendLine();
            sb.Append("        ZWrite ").Append(f.zwrite ? "On" : "Off").AppendLine();

            sb.AppendLine("        Pass");
            sb.AppendLine("        {");
            sb.AppendLine("            Name \"ForwardLit\"");
            sb.AppendLine("            Tags { \"LightMode\"=\"UniversalForward\" }");
            AppendBlend(sb, props, keywords, f);
            sb.AppendLine("            HLSLPROGRAM");
            bool fx = IsFxHost(host);
            sb.AppendLine(fx ? "            #pragma target 3.0" : "            #pragma target 3.5");
            sb.AppendLine("            #pragma vertex Vert");
            sb.AppendLine("            #pragma fragment Frag");
            if (!fx)
            {
                if (f.receiveShadows)
                {
                    sb.AppendLine("            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE");
                    sb.AppendLine("            #pragma multi_compile_fragment _ _SHADOWS_SOFT");
                }
                if (f.multiLight)
                    sb.AppendLine("            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS");
                sb.AppendLine("            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING");
                sb.AppendLine("            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION");
                sb.AppendLine("            #pragma multi_compile_fog");
                sb.AppendLine("            #define SHADERDIY_RECEIVE_SHADOWS " + (f.receiveShadows ? "1" : "0"));
                sb.AppendLine("            #define SHADERDIY_MULTI_LIGHT " + (f.multiLight ? "1" : "0"));
            }
            else
                sb.AppendLine("            #define SHADERDIY_FX 1");
            sb.AppendLine("            #define SHADERDIY_" + ShaderDIYRenderState.ToUrpDefine(f.engine) + " 1");
            sb.AppendLine("            #define SHADERDIY_" + SanitizeIdent(f.engine) + " 1");
            for (int i = 0; i < keywords.Count; i++)
            {
                if (!string.IsNullOrEmpty(keywords[i]))
                    sb.AppendLine("            #define " + keywords[i] + " 1");
            }
            sb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\"");
            if (!fx)
            {
                sb.AppendLine("            #include \"Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl\"");
                sb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl\"");
            }
            if (HasKeyword(keywords, "_LAYER_REFRACTION"))
                sb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl\"");
            if (HasKeyword(keywords, "_FX_SOFT"))
                sb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl\"");
            sb.AppendLine("            #include \"" + ShaderDIYPaths.GeneratedRelativeInclude + "\"");
            sb.AppendLine("            CBUFFER_START(UnityPerMaterial)");
            for (int i = 0; i < props.Count; i++)
                AppendCbufferField(sb, props[i]);
            sb.AppendLine("            CBUFFER_END");
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] != null && props[i].IsTexture)
                {
                    sb.Append("            TEXTURE2D(").Append(props[i].name).Append("); SAMPLER(sampler").Append(props[i].name).AppendLine(");");
                    sb.Append(BuildSampleHelper(props[i]));
                }
            }

            if (!string.IsNullOrEmpty(host.hlslInclude) &&
                host.hlslInclude.IndexOf("#SLOT_INJECT", StringComparison.Ordinal) >= 0)
            {
                sb.AppendLine(IndentHlsl(host.hlslInclude.Replace("// #SLOT_INJECT", slotHlsl), 12));
            }
            else
            {
                sb.AppendLine(IndentHlsl(slotHlsl, 12));
                if (!string.IsNullOrEmpty(host.hlslInclude))
                    sb.AppendLine(IndentHlsl(host.hlslInclude, 12));
            }
            if (host.passes != null && host.passes.Count > 0 && !string.IsNullOrEmpty(host.passes[0].hlsl))
                sb.AppendLine(IndentHlsl(host.passes[0].hlsl, 12));
            sb.AppendLine("            ENDHLSL");
            sb.AppendLine("        }");
            if (HasOutline(props))
                AppendOutlinePass(sb, props, f);
            sb.AppendLine("    }");
            sb.AppendLine("    FallBack Off");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static bool HasOutline(List<SdiyProperty> props)
        {
            if (props == null)
                return false;
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] != null && props[i].name == "_OutlineWidth")
                    return true;
            }
            return false;
        }

        static void AppendOutlinePass(StringBuilder sb, List<SdiyProperty> props, FeatureState f)
        {
            sb.AppendLine("        Pass");
            sb.AppendLine("        {");
            sb.AppendLine("            Name \"Outline\"");
            sb.AppendLine("            Tags { \"LightMode\"=\"SRPDefaultUnlit\" }");
            sb.AppendLine("            Cull Front");
            sb.AppendLine("            ZWrite On");
            sb.AppendLine("            HLSLPROGRAM");
            sb.AppendLine("            #pragma target 3.5");
            sb.AppendLine("            #pragma vertex OutlineVert");
            sb.AppendLine("            #pragma fragment OutlineFrag");
            sb.AppendLine("            #include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\"");
            sb.AppendLine("            CBUFFER_START(UnityPerMaterial)");
            for (int i = 0; i < props.Count; i++)
                AppendCbufferField(sb, props[i]);
            sb.AppendLine("            CBUFFER_END");
            sb.AppendLine("            struct OA { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };");
            sb.AppendLine("            struct OV { float4 positionCS : SV_POSITION; };");
            sb.AppendLine("            OV OutlineVert(OA v) {");
            sb.AppendLine("                OV o;");
            sb.AppendLine("                float3 offset = v.normalOS * _OutlineWidth * v.color.a;");
            sb.AppendLine("                float4 posOSOffset = float4(v.positionOS.xyz + offset, v.positionOS.w);");
            sb.AppendLine("                o.positionCS = TransformObjectToHClip(posOSOffset.xyz);");
            sb.AppendLine("                return o;");
            sb.AppendLine("            }");
            sb.AppendLine("            half4 OutlineFrag(OV i) : SV_Target { return _OutlineColor; }");
            sb.AppendLine("            ENDHLSL");
            sb.AppendLine("        }");
        }

        static void AppendProperty(StringBuilder sb, SdiyProperty p)
        {
            if (p == null || p.isFeature)
                return;
            sb.Append("        ");
            bool wroteEnum = false;
            if (p.extraAttributes != null)
            {
                for (int i = 0; i < p.extraAttributes.Count; i++)
                {
                    sb.Append("[").Append(p.extraAttributes[i]).Append("] ");
                    if (p.extraAttributes[i] != null &&
                        p.extraAttributes[i].StartsWith("Enum", StringComparison.OrdinalIgnoreCase))
                        wroteEnum = true;
                }
            }
            var type = p.type;
            if (p.IsToggle)
                type = "Float";
            if (p.IsEnum)
            {
                if (!wroteEnum)
                {
                    var enumAttr = ExpandEnumAttribute(p.type);
                    if (!string.IsNullOrEmpty(enumAttr))
                        sb.Append("[").Append(enumAttr).Append("] ");
                }
                type = "Float";
            }
            sb.Append(p.name).Append(" (\"").Append(p.displayName).Append("\", ").Append(type).Append(") = ").AppendLine(p.defaultValue);
            if (p.IsTexture)
            {
                sb.Append("        [HideInInspector] ").Append(p.name).Append("_MipBias (\"mip\", Float) = 0\n");
                sb.Append("        [HideInInspector] ").Append(p.name).Append("_Channel (\"ch\", Float) = 0\n");
                sb.Append("        [HideInInspector] ").Append(p.name).Append("_Invert (\"inv\", Float) = 0\n");
            }
            if (p.invertOp)
                sb.Append("        [HideInInspector] ").Append(p.name).Append("_InvertOp (\"inv\", Float) = 0\n");
        }

        static void AppendCbufferField(StringBuilder sb, SdiyProperty p)
        {
            if (p == null || p.isFeature)
                return;
            if (p.IsTexture)
            {
                sb.Append("            float4 ").Append(p.name).AppendLine("_ST;");
                sb.Append("            float ").Append(p.name).AppendLine("_MipBias;");
                sb.Append("            float ").Append(p.name).AppendLine("_Channel;");
                sb.Append("            float ").Append(p.name).AppendLine("_Invert;");
                return;
            }
            if (p.IsColor || p.IsVector)
                sb.Append("            float4 ").Append(p.name).AppendLine(";");
            else
                sb.Append("            float ").Append(p.name).AppendLine(";");
            if (p.invertOp)
                sb.Append("            float ").Append(p.name).AppendLine("_InvertOp;");
        }

        static string BuildSampleHelper(SdiyProperty p)
        {
            var n = p.name;
            var sb = new StringBuilder();
            sb.Append("            half4 ShaderDIYSample").Append(n).Append("(float2 uv) {\n");
            sb.Append("                uv = uv * ").Append(n).Append("_ST.xy + ").Append(n).Append("_ST.zw;\n");
            sb.Append("                half4 c = SAMPLE_TEXTURE2D_BIAS(").Append(n).Append(", sampler").Append(n).Append(", uv, ").Append(n).Append("_MipBias);\n");
            sb.Append("                c = ShaderDIY_Swizzle(c, (int)").Append(n).Append("_Channel);\n");
            sb.Append("                if (").Append(n).Append("_Invert > 0.5h) c = 1.h - c;\n");
            sb.Append("                return c;\n");
            sb.Append("            }\n");
            return sb.ToString();
        }

        static string IndentHlsl(string src, int spaces)
        {
            if (string.IsNullOrEmpty(src))
                return string.Empty;
            var pad = new string(' ', spaces);
            var lines = src.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder(src.Length + lines.Length * spaces);
            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(pad);
                sb.AppendLine(lines[i]);
            }
            return sb.ToString();
        }

        static void ApplyDefaults(Material mat, List<SdiyProperty> props)
        {
            if (mat == null)
                return;
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                if (p == null || p.isFeature)
                    continue;
                if (p.IsTexture)
                {
                    if (!mat.HasProperty(p.name + "_ST"))
                        continue;
                    continue;
                }
                if (!mat.HasProperty(p.name))
                    continue;
                if (p.IsColor)
                    mat.SetColor(p.name, SdiyParser.ParseColor(p.defaultValue, Color.white));
                else if (p.IsVector)
                    mat.SetVector(p.name, SdiyParser.ParseVector(p.defaultValue, Vector4.zero));
            }
        }

        static void ApplyNonCompileFeatures(Material mat, FeatureState f)
        {
            if (mat == null)
                return;
            mat.renderQueue = f.queue.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0 ? 3000
                : f.queue.IndexOf("AlphaTest", StringComparison.OrdinalIgnoreCase) >= 0 ? 2450 : 2000;
            mat.SetOverrideTag("RenderType", f.transparent ? "Transparent" : "Opaque");
        }

        static Material CreateMaterialAsset(ShaderDIYGraph graph, Shader shader)
        {
            var mat = new Material(shader) { name = graph.name };
            var path = ShaderDIYPaths.UserMaterials + "/" + ShaderDIYPaths.SanitizeFileName(graph.name) + ".mat";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static string ComputeHash(string hostPath, Dictionary<string, SdiyDocument> modules, FeatureState f, string hostText)
        {
            var sb = new StringBuilder();
            sb.Append("v10|");
            sb.Append(hostPath).Append('|').Append(hostText).Append('|');
            sb.Append(f.cull).Append(f.zwrite).Append(f.queue).Append(f.receiveShadows).Append(f.multiLight).Append(f.engine);
            foreach (var kv in modules)
                sb.Append(kv.Key).Append('=').Append(kv.Value.name).Append(kv.Value.hlslInclude);
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                    hex.Append(bytes[i].ToString("x2"));
                return hex.ToString();
            }
        }

        static int CountTextures(List<SdiyProperty> props)
        {
            int n = 0;
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] != null && props[i].IsTexture)
                    n++;
            }
            return n;
        }

        static int EstimateAlu(string hlsl)
        {
            if (string.IsNullOrEmpty(hlsl))
                return 4;
            return Mathf.Max(4, hlsl.Length / 80);
        }

        static string ExtractFuncName(string signature)
        {
            if (string.IsNullOrEmpty(signature))
                return "";
            int p = signature.IndexOf('(');
            var head = p > 0 ? signature.Substring(0, p) : signature;
            var parts = head.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "" : parts[parts.Length - 1];
        }

        static string SanitizeIdent(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "URP12";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsLetterOrDigit(s[i]) || s[i] == '_')
                    sb.Append(s[i]);
            }
            return sb.Length == 0 ? "URP12" : sb.ToString();
        }

        static int CompareSlotEmitOrder(SdiySlot a, SdiySlot b)
        {
            int wa = SlotWeight(a);
            int wb = SlotWeight(b);
            if (wa != wb)
                return wa.CompareTo(wb);
            return string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : "");
        }

        static bool IsFxHost(SdiyDocument host)
        {
            return host != null &&
                   (string.Equals(host.category, "FX", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(host.category, "特效", StringComparison.OrdinalIgnoreCase));
        }

        // Unity MaterialEnumDrawer 要 Name,Value 成对。含中文的 Enum 不能写进 ShaderLab（会 Parse error），工坊 UI 自己画下拉。
        static string ExpandEnumAttribute(string type)
        {
            if (string.IsNullOrEmpty(type))
                return null;
            int a = type.IndexOf('(');
            int b = type.LastIndexOf(')');
            if (a < 0 || b <= a)
                return type;
            var head = type.Substring(0, a).Trim();
            var inner = type.Substring(a + 1, b - a - 1);
            if (inner.IndexOf('.') >= 0)
                return type;
            var parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return type;
            for (int i = 0; i < parts.Length; i++)
            {
                var t = parts[i].Trim();
                for (int c = 0; c < t.Length; c++)
                {
                    if (t[c] > 127)
                        return null;
                }
            }
            bool alreadyPaired = parts.Length % 2 == 0;
            if (alreadyPaired)
            {
                for (int i = 1; i < parts.Length; i += 2)
                {
                    int n;
                    if (!int.TryParse(parts[i].Trim(), out n))
                    {
                        alreadyPaired = false;
                        break;
                    }
                }
            }
            if (alreadyPaired)
                return type;
            var sb = new StringBuilder();
            sb.Append(head).Append('(');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(parts[i].Trim()).Append(',').Append(i);
            }
            sb.Append(')');
            return sb.ToString();
        }

        // 特效 Host 用 #ifdef 调用模块：选「无」时不注入空函数。PBR/卡渲等始终注入（含「无」桩）。
        static bool ShouldEmitModuleHlsl(SdiyDocument mod, bool fxHost)
        {
            if (mod == null || string.IsNullOrEmpty(mod.hlslInclude))
                return false;
            if (fxHost && string.Equals(mod.name, "无", StringComparison.Ordinal))
                return false;
            return true;
        }

        static int EstimateFxInterpolators(List<string> keywords)
        {
            int n = 2;
            if (HasKeyword(keywords, "_FX_CLIP"))
                n += 1;
            if (HasKeyword(keywords, "_FX_RIM") || HasKeyword(keywords, "_FX_NOISE"))
                n += 3;
            return n;
        }

        static void AppendBlend(StringBuilder sb, List<SdiyProperty> props, List<string> keywords, FeatureState f)
        {
            if (HasKeyword(keywords, "_FX_BLEND_OFF"))
                sb.AppendLine("            Blend Off");
            else if (HasKeyword(keywords, "_FX_BLEND_ADD"))
                sb.AppendLine("            Blend SrcAlpha One");
            else if (HasKeyword(keywords, "_FX_BLEND_MUL"))
                sb.AppendLine("            Blend DstColor Zero");
            else if (HasKeyword(keywords, "_FX_BLEND_CUSTOM") &&
                     HasProp(props, "_SrcBlend") && HasProp(props, "_DstBlend"))
                sb.AppendLine("            Blend [_SrcBlend] [_DstBlend]");
            else if (HasKeyword(keywords, "_FX_BLEND_ALPHA"))
                sb.AppendLine("            Blend SrcAlpha OneMinusSrcAlpha");
            else if (HasProp(props, "_SrcBlend") && HasProp(props, "_DstBlend"))
                sb.AppendLine("            Blend [_SrcBlend] [_DstBlend]");
            else if (f.transparent)
                sb.AppendLine("            Blend SrcAlpha OneMinusSrcAlpha");
        }

        static bool HasProp(List<SdiyProperty> props, string name)
        {
            if (props == null || string.IsNullOrEmpty(name))
                return false;
            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] != null && props[i].name == name)
                    return true;
            }
            return false;
        }

        static bool HasKeyword(List<string> keywords, string name)
        {
            if (keywords == null || string.IsNullOrEmpty(name))
                return false;
            for (int i = 0; i < keywords.Count; i++)
            {
                if (keywords[i] == name)
                    return true;
            }
            return false;
        }

        static int SlotWeight(SdiySlot s)
        {
            if (s == null || string.IsNullOrEmpty(s.name))
                return 10;
            if (s.name == "F0")
                return 0;
            if (s.name == "NDF" || s.name == "D" || s.name == "F" || s.name == "G" || s.name == "DFG")
                return 1;
            if (s.name == "Transmittance" || s.name == "Light")
                return 2;
            if (s.name == "InScattering")
                return 3;
            if (s.name == "Noise")
                return 4;
            if (s.name == "MainTex")
                return 5;
            if (s.name == "Mask")
                return 6;
            if (s.name == "Emission")
                return 7;
            if (s.name == "Rim")
                return 8;
            if (s.name == "Clip")
                return 9;
            if (s.name == "Soft")
                return 11;
            return 10;
        }

        static string FormatShaderMessages(Shader shader)
        {
            try
            {
                var msgs = ShaderUtil.GetShaderMessages(shader);
                if (msgs == null || msgs.Length == 0)
                    return string.Empty;
                var sb = new StringBuilder();
                for (int i = 0; i < msgs.Length; i++)
                {
                    sb.Append(msgs[i].severity).Append(": ").Append(msgs[i].message).AppendLine();
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        static void AddRange(List<SdiyProperty> dst, List<SdiyProperty> src, string sourceSlot)
        {
            if (src == null)
                return;
            for (int i = 0; i < src.Count; i++)
            {
                if (src[i] == null)
                    continue;
                bool exists = false;
                for (int j = 0; j < dst.Count; j++)
                {
                    if (dst[j] != null && dst[j].name == src[i].name)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    src[i].sourceSlot = sourceSlot;
                    dst.Add(src[i]);
                }
            }
        }

        public static List<SdiyProperty> CollectMergedProperties(ShaderDIYGraph graph)
        {
            var list = new List<SdiyProperty>();
            var host = ModuleRegistry.FindHostByGuid(graph.hostGuid);
            if (host == null)
                host = ModuleRegistry.FindByPath(graph.hostPath);
            if (host == null)
                host = ModuleRegistry.FindHostByName(graph.hostName);
            if (host == null || host.document == null)
                return list;
            AddRange(list, host.document.features, null);
            AddRange(list, host.document.properties, null);
            for (int i = 0; i < host.document.slots.Count; i++)
            {
                var slot = host.document.slots[i];
                var name = graph.GetSlotModule(slot.name) ?? slot.defaultModule;
                var mod = ModuleRegistry.FindModule(slot.name, name);
                if (mod != null && mod.document != null)
                    AddRange(list, mod.document.properties, slot.name);
            }
            return list;
        }
    }
}
