using ShaderDIY;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class ShaderDIYTips
    {
        public static GUIContent L(string text, string tip)
        {
            return new GUIContent(text ?? "", tip ?? "");
        }

        public static GUIContent Prop(SdiyProperty p)
        {
            if (p == null)
                return L("", "");
            var name = string.IsNullOrEmpty(p.displayName) ? p.name : p.displayName;
            return L(name, ForProperty(p));
        }

        public static void Hover(Rect r, string tip)
        {
            if (string.IsNullOrEmpty(tip) || Event.current == null)
                return;
            if (r.Contains(Event.current.mousePosition))
                GUI.tooltip = tip;
        }

        public static string ForProperty(SdiyProperty p)
        {
            if (p == null)
                return "";
            if (!string.IsNullOrEmpty(p.tooltip))
                return p.tooltip + Suffix(p);
            string known;
            if (TryKnown(p.name, out known))
                return known + Suffix(p);
            return Fallback(p);
        }

        static string Suffix(SdiyProperty p)
        {
            if (string.IsNullOrEmpty(p.name))
                return "";
            return "\n变量：" + p.name;
        }

        static string Fallback(SdiyProperty p)
        {
            var title = string.IsNullOrEmpty(p.displayName) ? p.name : p.displayName;
            if (p.IsTexture)
                return title + "。拖一张贴图到这里，预览立刻跟着变。" + Suffix(p);
            if (p.IsColor)
                return title + "。点色块改颜色，预览立刻跟着变。" + Suffix(p);
            if (p.IsToggle)
                return title + "。开关。打开或关掉这项效果。" + Suffix(p);
            if (p.IsRange)
                return title + "。拖滑条改强度，预览立刻跟着变。" + Suffix(p);
            if (p.IsEnum)
                return title + "。点开选一种算法或模式。" + Suffix(p);
            if (p.IsVector)
                return title + "。四个数（XYZW），一般是偏移或方向。" + Suffix(p);
            return title + "。改这个数，预览立刻跟着变。" + Suffix(p);
        }

        static bool TryKnown(string name, out string tip)
        {
            tip = null;
            if (string.IsNullOrEmpty(name))
                return false;
            switch (name)
            {
                case "_MainTex":
                case "_BaseMap":
                    tip = "主颜色贴图。模型表面看到的底色。"; return true;
                case "_BaseColor":
                case "_Color":
                    tip = "叠加在贴图上的颜色。脸部自发光也会乘这个颜色。"; return true;
                case "_NormalMap":
                case "_NormalTex":
                    tip = "法线贴图。让表面看起来有凹凸，影响光照方向。"; return true;
                case "_MetallicMap":
                    tip = "金属度贴图（一般用红色通道）。越亮越像金属。"; return true;
                case "_RoughnessMap":
                    tip = "粗糙度贴图。越亮越糊、高光越散。"; return true;
                case "_SpecularMap":
                    tip = "高光贴图。乘在直接高光上，压暗或提亮反射。"; return true;
                case "_EmissionMap":
                    tip = "自发光贴图。不受灯光影响的亮部。"; return true;
                case "_OcclusionMap":
                    tip = "环境遮蔽贴图。缝里更暗，让间接光更扎实。"; return true;
                case "_Metallic":
                    tip = "金属度总开关。再乘金属度贴图。0 是非金属，1 是金属。"; return true;
                case "_Roughness":
                    tip = "粗糙度总开关。再乘粗糙度贴图。小=镜子，大=磨砂。"; return true;
                case "_EmissionColor":
                    tip = "自发光颜色。和自发光贴图或遮罩相乘。"; return true;
                case "_MainColor":
                    tip = "特效主颜色。再乘顶点色和颜色贴图。"; return true;
                case "_Intensity":
                    tip = "特效颜色强度。加法粒子可以拉过 1。"; return true;
                case "_Alpha":
                    tip = "整体透明度。再乘贴图和顶点色的 A。"; return true;
                case "_SoftFade":
                    tip = "软粒子淡出距离。贴墙、贴地时边缘会虚掉。URP 要开 Depth Texture。"; return true;
                case "_MainTex_Rot":
                    tip = "主贴图旋转（度）。绕 UV 中心转。"; return true;
                case "_MainTex_USpeed":
                case "_MaskTex_USpeed":
                case "_NoiseTex_USpeed":
                case "_EmissionMap_USpeed":
                case "_EmissionMask_USpeed":
                case "_ClipNoise_USpeed":
                    tip = "U 方向流动速度。正负决定左右。"; return true;
                case "_MainTex_VSpeed":
                case "_MaskTex_VSpeed":
                case "_NoiseTex_VSpeed":
                case "_EmissionMap_VSpeed":
                case "_EmissionMask_VSpeed":
                case "_ClipNoise_VSpeed":
                    tip = "V 方向流动速度。正负决定上下。"; return true;
                case "_MaskTex":
                    tip = "遮罩贴图。第 2 区可选通道，乘到颜色和透明度上。"; return true;
                case "_NoiseTex":
                    tip = "Noise 贴图。用来扭 UV，也可做视差和方向颜色。"; return true;
                case "_NoiseIntensity":
                    tip = "Noise 扰动强度。越大主贴图和遮罩抖得越厉害。"; return true;
                case "_Reverse":
                    tip = "反向。打开后 Noise 取 1-x。"; return true;
                case "_DirColor":
                    tip = "方向颜色。按 Noise 亮度叠上去，可 HDR。"; return true;
                case "_UseParallax":
                    tip = "Noise 视差。用切线空间视线去偏噪声 UV。"; return true;
                case "_ParallaxH":
                    tip = "视差高度采样。"; return true;
                case "_ParallaxHeight":
                    tip = "视差高度缩放。"; return true;
                case "_EmissionMask":
                    tip = "自发光遮罩。用通道乘主色再乘自发光颜色。"; return true;
                case "_RimPower":
                    tip = "菲涅尔次幂。越大边缘越窄。"; return true;
                case "_SrcBlend":
                    tip = "源混合。和原版 FX 一样，默认 SrcAlpha。"; return true;
                case "_DstBlend":
                    tip = "目标混合。和原版 FX 一样，默认 OneMinusSrcColor。"; return true;
                case "_ClipAxis":
                    tip = "裁切方向。按物体世界坐标：上/下/前/后/左/右。"; return true;
                case "_ClipPos":
                    tip = "裁切平面位置（世界坐标）。"; return true;
                case "_ClipNoise":
                    tip = "裁切边缘 Noise。让切开的边不那么齐。"; return true;
                case "_ClipNoiseStrength":
                    tip = "裁切 Noise 强度。"; return true;
                case "_ClipEdgeWidth":
                    tip = "裁切边缘光宽度。靠裁切面越近越亮。"; return true;
                case "_ClipEdgeColor":
                    tip = "裁切边缘光颜色，HDR。"; return true;
                case "_Occlusion":
                    tip = "环境遮蔽强度。1 用贴图原值，往下调会减轻暗缝。"; return true;
                case "_Cutoff":
                    tip = "镂空阈值。贴图透明度低于这个值的像素会被裁掉。"; return true;
                case "_LightMap":
                    tip = "角色 LightMap：R 金属区、G AO、B 高光形状、A 选 Ramp 第几行。"; return true;
                case "_RampTex":
                    tip = "明暗过渡色带。横轴是亮暗，纵轴用 LightMap 的 A 选行。"; return true;
                case "_MetalMap":
                    tip = "Matcap 金属球贴图。用视线空间法线去采样，做出卡通金属高光。"; return true;
                case "_FaceLightMap":
                    tip = "脸部 SDF 阴影贴图。按左右脸朝向翻转 UV，用来画脸阴影。"; return true;
                case "_ShadowOffset":
                    tip = "阴影阈值偏移。往右推阴影变少（更亮），往左阴影变多。"; return true;
                case "_ShadowSmoothness":
                    tip = "阴影边缘软硬。越小越刀切，越大越糊。"; return true;
                case "_MetallicIntensity":
                    tip = "金属区高光强度。只作用在 LightMap.R 接近 1 的区域。"; return true;
                case "_NonMetallicIntensity":
                    tip = "非金属区高光强度。布料、皮肤那一块的闪点。"; return true;
                case "_Shininess":
                    tip = "高光锐度。越大越尖、越小越散。"; return true;
                case "_EmissionIntensity":
                    tip = "自发光强度。身体用 LightMap.G 当发光形状。"; return true;
                case "_RimThreshold":
                    tip = "边缘光范围。越小边缘光越宽。"; return true;
                case "_RimIntensity":
                    tip = "边缘光亮度。"; return true;
                case "_RimColor":
                    tip = "边缘光颜色。还会再乘金属 Matcap。"; return true;
                case "_OutlineWidth":
                    tip = "描边宽度。再乘顶点色 A，没涂顶点色就几乎看不见描边。"; return true;
                case "_OutlineColor":
                    tip = "描边颜色。从背面挤出一层再上这个颜色。"; return true;
                case "_Anisotropy":
                    tip = "各向异性。正负控制高光沿切线还是副切线拉长，头发丝会用到。"; return true;
                case "_SSSBlurWidth":
                    tip = "SSS 散射宽度。越大皮下透光糊得越开。"; return true;
                case "_Subsurface":
                    tip = "Disney 次表面。让漫反射边缘更软，有一点透光感。"; return true;
                case "_AmbientColor":
                    tip = "扁平环境光颜色。不采样球谐，整片乘这个色。"; return true;
                case "_TermScale":
                    tip = "这一项的总倍率。调试模块亮度用。"; return true;
                case "_MieG":
                    tip = "Mie 相位的 g。越大越往太阳方向聚，天空看起来越「日晕」。"; return true;
                case "_TransmittanceLut":
                    tip = "透射率 LUT。查表版用来代替沿光线那 16 步积分。"; return true;
                case "_MultiScatteringLut":
                    tip = "多次散射 LUT。只在多次散射里加到积分上。"; return true;
                case "_ThicknessMap":
                    tip = "厚度图。越亮越厚、透光越少，视差内层也会用。"; return true;
                case "_Thickness":
                    tip = "厚度。经验SSS/视差是乘厚度图的0-1；PBR六瓣SSS是毫米，越厚透光越弱。"; return true;
                case "_TransNormalDistortion":
                    tip = "透射时法线把光线掰弯多少。"; return true;
                case "_TransScattering":
                    tip = "透射高光有多尖。越大越聚成一束。"; return true;
                case "_TransDirect":
                    tip = "主光透射强度。"; return true;
                case "_TransAmbient":
                    tip = "环境光透射强度。"; return true;
                case "_Translucency":
                    tip = "背光透射颜色。"; return true;
                case "_FrontTranslucency":
                    tip = "顺光散射颜色。"; return true;
                case "_InnerColor":
                    tip = "冰块内层颜色。"; return true;
                case "_InnerColorThicknessPower":
                    tip = "厚度怎么压内层。越大，厚的地方内层越弱。"; return true;
                case "_ScreenUVDistor":
                    tip = "抓屏 UV 按法线扭多少。"; return true;
                case "_GrabIntensity":
                    tip = "折射混多少抓屏。正对看得更多后面的东西。"; return true;
                default:
                    return false;
            }
        }

        public static string RegionRenderState
        {
            get { return "渲染状态。改正反面、队列、阴影、光源。改完会重新编译 Shader。"; }
        }

        public static string RegionTextures
        {
            get { return "贴图区。上面的页签换贴图槽，左边拖贴图，右边改缩放/偏移/通道。"; }
        }

        public static string RegionParams
        {
            get { return "模块与参数。上面叠加按钮在原效果上加 SSS/视差/折射；下面下拉换 D/G/F 和光向量。"; }
        }

        public static string RegionPerf
        {
            get { return "性能估算。不是真机帧率，是按公式复杂度估的负载，用来比较换模块前和换完。"; }
        }

        public static string RegionFormula
        {
            get { return "当前拼出来的公式。换第 3 区模块时，对应那一行会高亮。"; }
        }

        public static string Timeline
        {
            get { return "时间轴。调好参数按 K 记下，再改参数打下一帧，播放会在两帧之间平滑过渡。"; }
        }

        public static string Category
        {
            get { return "大类。无光照 / PBR / 卡渲 / 自定义 / 大气 / 特效。换大类会换成该大类的第一套模板。"; }
        }

        public static string Host
        {
            get { return "模板。同一大类下的不同骨架，例如卡渲的身体、脸、头发。换模板会重新编译。"; }
        }

        public static string Surface
        {
            get { return "表面怎么画：正面还是背面、排在不透明还是透明队列。"; }
        }

        public static string Lighting
        {
            get { return "深度、阴影和打光方式。改这些会重新编译。"; }
        }

        public static string Cull
        {
            get { return "画哪一面。正面=只画朝外的面；背面=只画朝里的面（大气球、描边常用）；双面=两面都画（脸常用）。"; }
        }

        public static string Queue
        {
            get { return "渲染队列。不透明最先画；镂空带裁切；透明后画、能看到后面的东西。"; }
        }

        public static string ZWrite
        {
            get { return "写深度。打开后挡住后面的物体。透明材质通常关掉。"; }
        }

        public static string ReceiveShadows
        {
            get { return "接阴影。打开后能接到主光影子。"; }
        }

        public static string LightingMode
        {
            get { return "光源。单光源只要主光；多光源会加额外点光/聚光（更贵）。"; }
        }

        public static string Engine
        {
            get { return "按你的 Unity 版本选。现在工程是 2021 就选 Unity 2021，不要乱选。"; }
        }

        public static string Settings
        {
            get { return "设置：新建 Host/模块、打开当前骨架、强制重编译、看格式说明。"; }
        }

        public static string MaterialPopup
        {
            get { return "当前正在编的材质球。下拉可换成另一个。"; }
        }

        public static string CreateMaterial
        {
            get { return "新建一个材质球，默认用 PBR 模板。"; }
        }

        public static string DeleteMaterial
        {
            get { return "删除当前材质球，以及它生成的 Shader。"; }
        }

        public static string AssignChip
        {
            get { return "按住拖到场景或 Hierarchy 的物体上，把当前材质赋上去。"; }
        }

        public static string PreviewOrbit
        {
            get { return "左键拖：转模型。滚轮：拉近拉远。"; }
        }

        public static string MeshKind
        {
            get { return "预览用的模型。自定义可以拖一张 Mesh。"; }
        }

        public static string CustomMesh
        {
            get { return "拖入自定义网格，用来看衣服、头之类真实模型。"; }
        }

        public static string TexScale
        {
            get { return "UV 缩放。X/Y 大于 1 会把贴图缩小重复。"; }
        }

        public static string TexOffset
        {
            get { return "UV 偏移。把贴图左右/上下挪一挪。"; }
        }

        public static string TexChannel
        {
            get { return "只用贴图的哪一个通道。RGBA 是全彩，R/G/B/A 是单通道。"; }
        }

        public static string TexMip
        {
            get { return "Mip 偏移。正数更糊（远看），负数更锐（近看）。"; }
        }

        public static string TexInvert
        {
            get { return "反相采样。黑变白、白变黑，法线/粗糙度接错图时能应急。"; }
        }

        public static string TexSlot
        {
            get { return "把贴图拖到这个方块，或点它从工程里选。"; }
        }

        public static string BaseParams
        {
            get { return "这套模板自己的参数，不属于下面任何一个可替换模块。"; }
        }

        public static string SlotPopup(string slot)
        {
            return SlotTitle(slot) + "：点这里换算法。换完会重新编译，第 4 区公式会跟着变。";
        }

        public static string SlotHead(string slot)
        {
            switch (slot)
            {
                case "DirectDiffuse":
                    return "直接漫反：灯光打到表面的颜色部分。选 SSS 时环境漫反会自动一起换成 SSS。";
                case "DirectSpecular":
                    return "直接高光：灯光反射出来的亮斑。";
                case "IndirectDiffuse":
                    return "环境漫反：天空/间接光的底色。用了 SSS 时这项会藏起来，避免重复算。";
                case "IndirectSpecular":
                    return "环境高光：周围环境映在表面上的反射。";
                case "NDF":
                    return "D（法线分布）：高光有多尖、多散。";
                case "G":
                    return "G（几何遮蔽）：微表面互相挡住的程度。";
                case "F":
                    return "F（菲涅尔）：从侧面看会更亮的那一层。";
                case "F0":
                    return "F0：正对看时的反射底色。金属用贴图色，非金属接近灰白。";
                case "Transmittance":
                    return "透射率。路径积分是沿光线 16 步累加消光；查表是采样 Transmittance Lut。多次散射必须用查表。";
                case "InScattering":
                    return "散射积分。单次是视线 16 步累加一次散射；多次会再加 Multi Scattering Lut。选多次会自动改成查表透射率。";
                case "Light":
                    return "光向量。主光=原来的 GetMainLight()；主光阴影=冰那版 GetMainLight(shadowCoord)，方向会 normalize。";
                case "LayerSSS":
                    return "叠在 PBR 上面的经验 SSS：背光透射 + 正向散射，厚度越大透得越少。";
                case "LayerParallax":
                    return "叠在 PBR 上面的内层视差：用切线空间视线去偏噪声 UV，当冰块里面的颜色。";
                case "LayerRefraction":
                    return "叠在 PBR 上面的抓屏折射。打开后队列会改成透明。URP 要开 Opaque Texture。";
                case "MainTex":
                    return "颜色贴图。关掉只用主颜色；打开后可调 Tiling/Offset 和 UV 流动。";
                case "Mask":
                    return "遮罩。打开后乘到颜色上，第 2 区可选用哪个通道。";
                case "Noise":
                    return "Noise 扰动 UV。可反向、叠方向颜色，也可开视差去偏噪声 UV。";
                case "Emission":
                    return "自发光。可选贴图或遮罩，再乘自发光颜色。";
                case "Rim":
                    return "边缘光。用菲涅尔 pow(1-N·V)。";
                case "Outline":
                    return "描边。打开后多一个正面剔除 Pass，调颜色和宽度。";
                case "Clip":
                    return "世界坐标裁切。选上下前后左右，可加边缘 Noise 和 HDR 边缘光。";
                case "Blend":
                    return "混合模式。无=不混合；透明=SrcAlpha/OneMinusSrcAlpha；叠加=加法；相乘=正片叠底；自定义=自己选 Src/Dst，和原版 FX 一样。";
                case "Soft":
                    return "软粒子。按场景深度把贴边的粒子淡掉。URP 要开 Depth Texture。";
                default:
                    return slot + "：可替换的光照一块。右边下拉换实现。";
            }
        }

        public static string SlotTitle(string slot)
        {
            switch (slot)
            {
                case "DirectDiffuse": return "直接漫反";
                case "DirectSpecular": return "直接高光";
                case "IndirectDiffuse": return "环境漫反";
                case "IndirectSpecular": return "环境高光";
                case "NDF": return "D";
                case "G": return "G";
                case "F": return "F";
                case "F0": return "F0";
                case "Light": return "光向量";
                case "LayerSSS": return "经验SSS";
                case "LayerParallax": return "视差";
                case "LayerRefraction": return "折射";
                case "MainTex": return "颜色贴图";
                case "Mask": return "遮罩";
                case "Noise": return "Noise";
                case "Emission": return "自发光";
                case "Rim": return "边缘光";
                case "Outline": return "描边";
                case "Clip": return "裁切";
                case "Blend": return "混合";
                case "Soft": return "软粒子";
                default: return slot;
            }
        }

        public static string AddonBar
        {
            get { return "可选功能。点开才会编译进去，关掉就没有。"; }
        }

        public static string Score
        {
            get { return "综合负载。绿偏低、红偏高。用来比较换模块前和换完，不是绝对帧率。"; }
        }

        public static string Chart
        {
            get { return "最近几次编译的走势。点下面图例可以单独显示某一条。"; }
        }

        public static string Alu
        {
            get { return "ALU：算术指令大概数量。公式越复杂这条越高。点它可在图里开关。"; }
        }

        public static string Tex
        {
            get { return "TEX：贴图采样次数。贴图槽越多越高。点它可在图里开关。"; }
        }

        public static string Variants
        {
            get { return "变体：关键字组合数。变体多，打包和切换更慢。点它可在图里开关。"; }
        }

        public static string Interpolators
        {
            get { return "插值：顶点传到像素的通道数。太多会占带宽。点它可在图里开关。"; }
        }

        public static string CompileMeta
        {
            get { return "上次编译花了多久，以及这个 Shader 有几个 Pass（身体描边会多一个）。"; }
        }

        public static string Play
        {
            get { return "播放时间轴。至少要有两帧关键帧才会动。"; }
        }

        public static string Stop
        {
            get { return "停止播放。"; }
        }

        public static string Rewind
        {
            get { return "回到 0 秒，并套上第一帧的参数。"; }
        }

        public static string InsertKey
        {
            get { return "插入关键帧：把此刻第 3 区所有可动画参数记下来。"; }
        }

        public static string DeleteKey
        {
            get { return "删除选中的那一帧（黄点）。先在轨道上点一颗钻石再删。"; }
        }

        public static string Loop
        {
            get { return "循环播放。播到结尾会跳回开头。"; }
        }

        public static string Duration
        {
            get { return "整段动画长度（秒）。"; }
        }

        public static string Track
        {
            get { return "点空白处拖播放头；点黄点选中关键帧。"; }
        }

        public static string NewName
        {
            get { return "文件名。会写到 Sources 或 Modules 文件夹。"; }
        }

        public static string NewCategory
        {
            get { return "大类，要和第 1 区对得上：PBR / Toon / Unlit / Custom / Atmosphere / FX。卡渲填 Toon，大气填 Atmosphere，特效填 FX。"; }
        }

        public static string NewSlot
        {
            get { return "这个模块插在哪一个槽。函数名会按槽自动生成。"; }
        }

        public static string CustomSlot
        {
            get { return "自定义槽名。Host 里要用同样的名字去调。"; }
        }

        public static string WithSlot
        {
            get { return "生成 Host 时顺手加一个可替换槽，并写一个默认模块。没有默认模块材质编不过。"; }
        }

        public static string CreateFile
        {
            get { return "按模板写文件并打开。不会改你已有的 Shader 公式。"; }
        }

        public static string OpenSource
        {
            get { return "用文本打开这份 .sdiy / .sdiym 源文件。"; }
        }

        public static string OpenWorkshop
        {
            get { return "打开材质工坊窗口，在预览里拼 Shader。"; }
        }
    }
}
