# ShaderDIY

面向美术的 Unity URP Shader 拼装工具。用下拉换光照模块、调参数、左边预览立刻更新。插件按当前组合在**编译期**缝成一份真正的 URP Shader，不是运行时一堆 `if`。

本仓库**只包含插件**（`Assets/ShaderDIY`）和这份说明，不含完整 Unity 工程（没有 `ProjectSettings/`、`Packages/`、场景、URP 资源）。  
别人拿到后应拷进自己已有的 URP 工程，而不是当成一个可直接打开的 Unity 项目。

## 环境

- 以 **Unity 2021.3 + URP 12** 为主
- 第 1 区可切换 Unity 2022 / Unity 6 宏（未作为正式环境验证）
- 目标工程必须已经装好 Universal Render Pipeline

## 安装

1. 把仓库里的 `Assets/ShaderDIY` 整个文件夹拷进你工程的 `Assets/` 下  
   **连同 `.meta` 一起拷**，不要只拷脚本。
2. 等 Unity 导入结束。
3. 菜单：`Window → ShaderDIY → 材质工坊`

也可以把本仓库当 Git 子模块，或打成 `.unitypackage` 再导入，同样只要保留 `Assets/ShaderDIY`。

目录用途：

| 路径 | 内容 |
|------|------|
| `Shaders/Sources/` | Host 骨架（`.sdiy`） |
| `Shaders/Modules/大类/` | 可替换模块（`.sdiym`） |
| `Shaders/Templates/` | 新建 Host / Module 用的模板 |
| `Shaders/Library/ShaderDIYCore.hlsl` | `ShadingContext`、`ShaderDIYVaryings`、采样辅助 |
| `Generated/` | 编译产物，禁止手改，下次编译覆盖 |
| `UserMaterials/` | 工坊里新建的材质 Graph + `.mat` |
| `Documentation/SdiyFormat.txt` | 更短的语法备忘 |

---

## 给美术：怎么用

打开材质工坊后，界面分成几块：

| 区域 | 作用 |
|------|------|
| 左上预览 | 转模型、换网格、新建/删除材质；把材质图标拖到场景物体上即可赋予 |
| 第 1 区 渲染状态 | 大类、模板、正反面、队列、写深度、接阴影、单/多光源、Unity 版本 |
| 第 2 区 贴图 | 页签换贴图槽；缩放、偏移、通道、Mip、反相 |
| 第 3 区 模块与参数 | 换算法、调参数；PBR 顶部有叠加按钮（经验 SSS / 视差 / 折射） |
| 第 4 区 性能 | 负载估算和当前公式；换模块时对应行会高亮 |
| 时间轴 | 调好参数按 `K` 记下，播放会在两帧之间平滑过渡 |

正反面按「画哪一面」理解：正面 = 画朝外面；背面 = 画朝里面（大气球）；双面 = 两面都画。

参数、贴图、模块、叠加、渲染状态支持 **Ctrl+Z / Ctrl+Y**。

### 自带大类

- **无光照**：颜色 + 主贴图
- **PBR**：D / G / F / F0、直接高光、直接漫反、环境漫反、环境高光可换；光向量可换（主光 / 主光阴影）；可叠加经验 SSS、内层视差、抓屏折射（折射会改成透明队列，URP 需开启 Opaque Texture）
- **卡渲**：模板可选身体 / 脸 / 头发
- **大气**：透射率（路径积分 / 查表）× 散射（单次 / 多次）；查表要拖 Transmittance Lut，多次再拖 Multi Scattering Lut
- **自定义**：第 1 区选「自定义」，用你自己写的 Host

只改数值和贴图不会重编译；换模块、换大类、改会影响变体的渲染状态才会重新缝 Shader。

### 工坊里会自动配对的项

- PBR 直接漫反选 **SSS** 时，环境漫反会跟着切到 SSS（关掉则回到 SH）
- 大气散射选 **多次** 时，透射率会强制改成 **查表**（多次积分需要 LUT）
- 光向量选 **主光阴影** 时，第 1 区「接阴影」会打开
- PBR 叠加 **折射** 时，队列改透明、关掉写深度；关掉叠加则恢复不透明

---

## 给开发：写自定义 Shader

插件读两种文本，HLSL 仍是 URP HLSL。外面只多一层 Meta / Properties / SLOT / Formula / Cost。  
**换模块 = 换这段 HLSL + 换这段 Properties。** 公式保持你自己的写法，插件只负责拼接。

| 扩展名 | 是什么 | 放哪 |
|--------|--------|------|
| `.sdiy` | Host 骨架（顶点/片元、贴图、槽位声明） | `Assets/ShaderDIY/Shaders/Sources/` |
| `.sdiym` | 可替换模块（一个槽的函数 + 自己的参数） | `Assets/ShaderDIY/Shaders/Modules/大类名/` |

工坊第 1 区齿轮也可以「新建 Host Shader / 新建 Module」，会从 `Shaders/Templates/` 拷模板。  
第 1 区选大类 **自定义**，就会列出 `Category = Custom` 的 Host。

### 编译时发生了什么

1. 读当前材质 Graph 选中的 Host + 每个槽的模块名
2. 合并 Host 和模块的 Properties（模块参数只在选中时进第 3 区）
3. 把各模块的 HLSL 函数插进 Host 里 `// #SLOT_INJECT` 的位置
4. 框架补上 Pass、Cull/ZWrite/Queue、阴影/多光 `#pragma`、Unity 版本宏、CBUFFER、贴图采样函数
5. 写出 `Generated/名字_guid.shader`，赋给材质

不要手改 `Generated/`。也不要在 Host 里写 `SubShader` / `Pass` / `Cull` / `Features`：第 1 区才生效。

### 原则

- 有 `[SLOT]` 时，`Default` 必须是已经存在的模块名，否则材质编不过
- 模块函数名必须和槽的 `Signature` 里那个名字一致
- 属性名用英文；显示名可以中文
- 文件 UTF-8；插件写出时用 CRLF
- 模块按 `Slot` + `Category` 匹配；同一个槽可以有多个模块，工坊用下拉切换

### Host（`.sdiy`）最小例子

```
ShaderDIY "ShaderDIY/Custom/MyHost"
{
    Meta
    {
        Category = Custom
        Pipeline = URP
        Description = "说明"
        CompanionScripts =
    }

    Properties
    {
        [Tab 主贴图]
        [Region 2][TexOps(TilingOffset, Channel, Invert, MipBias)]
        _BaseMap ("主贴图", 2D) = "white" {}

        [Region 3] _BaseColor ("颜色", Color) = (1,1,1,1)
    }

    Formula
    {
        C = albedo · _BaseColor
    }

    [SLOT CustomTerm Default="MyTerm" Signature="float3 EvaluateCustomTerm(ShadingContext ctx)"]

    HLSLINCLUDE
    {
        // #SLOT_INJECT

        ShaderDIYVaryings Vert(float4 positionOS : POSITION, float3 normalOS : NORMAL, float4 tangentOS : TANGENT, float2 uv : TEXCOORD0)
        {
            ShaderDIYVaryings o;
            VertexPositionInputs vpi = GetVertexPositionInputs(positionOS.xyz);
            o.positionCS = vpi.positionCS;
            o.positionWS = vpi.positionWS;
            o.normalWS = TransformObjectToWorldNormal(normalOS);
            o.tangentWS = float4(1, 0, 0, 1);
            o.uv = uv;
            o.viewDirWS = normalize(_WorldSpaceCameraPos - vpi.positionWS);
            o.vertexSH = SampleSH(o.normalWS);
            o.fogFactor = ComputeFogFactor(vpi.positionCS.z);
            return o;
        }

        float4 Frag(ShaderDIYVaryings i) : SV_Target
        {
            float4 albedo = ShaderDIYSample_BaseMap(i.uv) * _BaseColor;
            ShadingContext ctx = (ShadingContext)0;
            ctx.albedo = albedo.rgb;
            ctx.N = normalize(i.normalWS);
            ctx.V = normalize(i.viewDirWS);
            ctx.positionWS = i.positionWS;
            float3 color = EvaluateCustomTerm(ctx);
            color = MixFog(color, i.fogFactor);
            return float4(color, albedo.a);
        }
    }
}
```

- `ShaderDIY "路径"` 会变成生成 Shader 的名字前缀
- `Category`：`PBR` / `Toon` / `Unlit` / `Custom` / `Atmosphere`，对应第 1 区大类
- `CompanionScripts`：赋材质时自动挂的 MonoBehaviour 全名，逗号分隔，可空
- `// #SLOT_INJECT`：模块函数插在这里（必须放在 Vert/Frag 前面，模块才能互相调用）
- 没有 SLOT 也可以（例如 Unlit），第 3 区只显示基础参数
- 叠加类槽加上 `Kind="Addon"`，会在第 3 区顶部变成开关而不是下拉；默认模块名建议叫 `无`，打开时切到另一个同槽模块

槽位写法：

```
[SLOT 槽名 Default="默认模块名" Signature="返回值 函数名(参数)" Kind="Addon"]
```

`Kind` 可省略。`Signature` 可省略（大气透射率那种只换整段辅助函数、没有统一入口时）。

### Module（`.sdiym`）

```
Module "MyTerm" Slot="CustomTerm" Category="Custom"
{
    Properties
    {
        [Region 3] _TermScale ("模块强度", Range(0,2)) = 1
    }

    Cost { ALU = 6 Texture = 0 Interpolator = 0 }
    Keywords { _SLOT_MYTERM }
    Formula { F = albedo · N·L · _TermScale }

    HLSL
    {
        float3 EvaluateCustomTerm(ShadingContext ctx)
        {
            return ctx.albedo * ctx.NdotL * _TermScale;
        }
    }
}
```

- `Module "名字"` = 工坊下拉里显示的名字 = Host 里 `Default="名字"`
- `Slot` 必须和 Host 的 `[SLOT 名字]` 完全一致
- `Category` 建议和 Host 大类一致，文件放在 `Modules/该大类/` 下
- `Keywords` 会编译成 `#define 名字 1`，Host 或其他模块可用 `#ifdef`
- `Cost` 只给第 4 区估算，不参与编译
- `Formula` 只给第 4 区显示

给已有 Host 加新算法：不必改 Host，只要新建一个同样 `Slot`、同样函数签名的 `.sdiym`。保存后工坊下拉会出现它。

### Properties 标记

| 标记 | 作用 |
|------|------|
| `[Region 2]` | 进贴图区 |
| `[Region 3]` | 进参数区（模块参数跟在对应槽下面；Host 的进「基础参数」） |
| `[Tab 名字]` | 第 2 区页签文字 |
| `[TexOps(TilingOffset, Channel, Invert, MipBias)]` | 贴图卡片右侧控件，可只写需要的项，或 `TexOps(All)` |
| `[ShowIf(_Prop, 1)]` | `_Prop` 接近该值才显示 |
| `[Tooltip("……")]` | 悬停说明，覆盖默认提示 |
| `[Invert]` / `[OneMinus]` | 参数旁 1- 开关 |
| `[HDR]` 等 | 原样写进生成的 Shader |

类型：`2D` / `Color` / `Range(a,b)` / `Float` / `Vector` / `Toggle` / `Enum(A,B)`

卡渲描边：Host 里若有 `_OutlineWidth`，框架会自动加一个正面剔除的描边 Pass（用 `_OutlineColor`）。

### 采样

编译器会给每个 2D 属性生成 `ShaderDIYSample_属性名(float2 uv)`（内部含 `_ST`、Mip、通道、反相）。  
第 2 区这些控件要生效，贴图应走这个函数，而不是自己 `SAMPLE_TEXTURE2D`。  
LUT、Matcap、用算出来的 UV 采样时，可以继续用 `SAMPLE_TEXTURE2D` / `SAMPLE_TEXTURE2D_LOD`。

法线示例：`UnpackNormalScale(ShaderDIYSample_BumpMap(uv), _BumpScale)`

### 内置宏（Frag 里可用）

- `SHADERDIY_RECEIVE_SHADOWS` 接阴影
- `SHADERDIY_MULTI_LIGHT` 多光源
- `SHADERDIY_URP12` / `URP14` / `URP17`
- `SHADERDIY_Unity2021` / `Unity2022` / `Unity6`

建议主光：

```
#if SHADERDIY_RECEIVE_SHADOWS
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(ctx.positionWS));
#else
    Light mainLight = GetMainLight();
#endif
ShaderDIY_FillLight(ctx, mainLight.direction, mainLight.color,
    mainLight.distanceAttenuation * mainLight.shadowAttenuation);
```

### ShadingContext / Varyings

定义在 `Shaders/Library/ShaderDIYCore.hlsl`，生成 Shader 会自动 `#include`。Host 的 Vert 应填 `ShaderDIYVaryings`，Frag 里组装 `ShadingContext` 再调模块。

- 几何：`positionWS` `positionCS` `uv` `N` `V` `L` `H` `T` `B` `Ng`
- 材质：`albedo` `alpha` `metallic` `smoothness` `perceptualRoughness` `roughness` `occlusion` `emission` `f0` `specularColor`
- 点积：`NdotV` `NdotL` `NdotH` `VdotH` `TdotH` `BdotH` `TdotL` `BdotL`
- 灯光：`lightColor` `attenuation` `bakedGI`

`ShaderDIY_FillLight(ctx, L, lightColor, attenuation)` 会填 `L`/`H` 和点积。各向异性 NDF 用 T/B 点积。

### 已有槽位（写替换模块时函数名必须对上）

**PBR**（`PBR_Lit.sdiy`）

| 槽 | 函数 | 说明 |
|----|------|------|
| NDF | `float EvaluateNDF(ShadingContext ctx)` | 高光模块内部再调 |
| G | `float EvaluateG(ShadingContext ctx)` | |
| F | `float3 EvaluateF(ShadingContext ctx)` | |
| F0 | `float3 EvaluateF0(ShadingContext ctx)` | |
| Light | `void EvaluateLight(inout ShadingContext ctx)` | 填 `ctx.L` / `lightColor` / `attenuation` |
| DirectSpecular | `float3 EvaluateDirectSpecular(ShadingContext ctx)` | |
| DirectDiffuse | `float3 EvaluateDirectDiffuse(ShadingContext ctx)` | 选 SSS 会配对 IndirectDiffuse |
| IndirectDiffuse | `float3 EvaluateIndirectDiffuse(ShadingContext ctx)` | |
| IndirectSpecular | `float3 EvaluateIndirectSpecular(ShadingContext ctx)` | |
| LayerSSS | `float3 EvaluateLayerSSS(ShadingContext ctx)` | `Kind=Addon` |
| LayerParallax | `float3 EvaluateLayerParallax(ShadingContext ctx)` | `Kind=Addon` |
| LayerRefraction | `float4 EvaluateLayerRefraction(ShadingContext ctx, float3 result)` | `Kind=Addon`，返回最终色；打开时 Host 用 `_LAYER_REFRACTION` 跳过 clip |

PBR Host 在 Frag 里调用这些函数。给 NDF 写新模型时，高光模块内部会再调 `EvaluateNDF`。叠加项不要去改 D/G/F，加在最终色上。

**大气**（`Atmosphere.sdiy`）

| 槽 | 函数 | 说明 |
|----|------|------|
| Transmittance | 无统一 Signature，模块提供 `GetTransmittance` 等 | 路径积分 / 查表 |
| InScattering | `float3 EvaluateAtmosphere(float3 positionWS)` | 单次 / 多次 |

多次散射模块会采样 Host 里已有的 `RayleiCoefficient` 等函数，所以 `// #SLOT_INJECT` 必须放在这些辅助函数后面、Vert 前面。

**卡渲**：三个独立 Host（身体 / 脸 / 头发），第 1 区用模板切换，不是 PBR 那种槽替换。

**自定义**：自己声明 `[SLOT]`，函数名自定，只要模块对得上。

### 建议流程

1. 齿轮 → 新建 Host，得到能编过的骨架（贴图 + 颜色 + 强度）
2. 需要可替换时勾「同时生成一个可替换槽 + 默认模块」，或单独新建 Module
3. 在 Host 的 Frag 里组装 `ShadingContext` 并调用模块函数（参考 `PBR_Lit.sdiy`）
4. 材质工坊第 1 区选对应大类（自定义选 Custom）→ 换模块看预览；第 3 区参数、第 4 区公式应跟着变
5. 给已有槽加第二种算法：只新建 `.sdiym`，不要复制整份 Host

### 常见错误

- SLOT 写了但 Default 模块不存在 → 缺少模块
- 模块函数名和 Signature 不一致 → 未实现签名
- 贴图没用 `ShaderDIYSample_*` → 第 2 区缩放/通道/Mip/反相无效
- 在 Host 里写正反面/Unity 版本 → 会被忽略，用第 1 区
- 手改 `Generated/*.shader` → 下次编译覆盖
- 属性名不要用中文
- `// #SLOT_INJECT` 放在 Vert 后面 → 模块函数变成局部，编不过
- 大气多次散射却不提供 LUT 贴图 → 采样黑图

### 参考文件

- 完整 Host：`Shaders/Sources/PBR_Lit.sdiy`
- 简单 Host：`Shaders/Sources/Unlit_Color.sdiy`
- 大气 Host：`Shaders/Sources/Atmosphere.sdiy`
- 卡渲 Host：`Shaders/Sources/Toon_Body.sdiy`（以及 Face / Hair）
- 模块：`Shaders/Modules/PBR/DirectDiffuse_Lambert.sdiym`、`NDF_Anisotropic.sdiym`、`Light_MainShadow.sdiym`
- 叠加模块：`Shaders/Modules/PBR/LayerSSS_Ice.sdiym` 等
- 模板：`Shaders/Templates/HostTemplate.sdiy`、`ModuleTemplate.sdiym`
- 更短的备忘：`Assets/ShaderDIY/Documentation/SdiyFormat.txt`
