# ShaderDIY

Artist-friendly Unity URP shader assembly tool. Swap lighting modules from dropdowns, tweak parameters, and the left preview updates immediately. The plugin **stitches** the current combination into a real URP Shader at **compile time**—not a pile of runtime `if`s.

This repository contains **only the plugin** (`Assets/ShaderDIY`) and this guide. It is **not** a full Unity project (no `ProjectSettings/`, `Packages/`, scenes, or URP assets).  
Copy the folder into an existing URP project; do not open this repo as a standalone Unity project.

## Requirements

- Primary target: **Unity 2021.3 + URP 12**
- Region 1 can switch Unity 2022 / Unity 6 macros (not formally validated)
- The target project must already have Universal Render Pipeline installed

## Install

1. Copy the entire `Assets/ShaderDIY` folder from this repo into your project's `Assets/`  
   **Include `.meta` files**—do not copy scripts only.
2. Wait for Unity to finish importing.
3. Menu: `Window → ShaderDIY → Material Workshop`

You can also add this repo as a Git submodule, or export a `.unitypackage`, as long as `Assets/ShaderDIY` is preserved.

Folder layout:

| Path | Contents |
|------|----------|
| `Shaders/Sources/` | Host skeletons (`.sdiy`) |
| `Shaders/Modules/<Category>/` | Swappable modules (`.sdiym`) |
| `Shaders/Templates/` | Templates for new Host / Module |
| `Shaders/Library/ShaderDIYCore.hlsl` | `ShadingContext`, `ShaderDIYVaryings`, sampling helpers |
| `Generated/` | Compile output—do not edit by hand; overwritten on next compile |
| `UserMaterials/` | Materials / Graphs created in the workshop |
| `Documentation/SdiyFormat.txt` | Short syntax cheat sheet |

---

## For artists: how to use

After opening Material Workshop, the UI is split into regions:

| Region | Role |
|--------|------|
| Top-left preview | Orbit the mesh, change mesh, create/delete materials; drag the material icon onto a scene object to assign |
| Region 1 — Render state | Category, template, cull faces, queue, ZWrite, receive shadows, single/multi light, Unity version |
| Region 2 — Textures | Tabs for texture slots; tiling, offset, channel, mip, invert |
| Region 3 — Modules & params | Swap algorithms, tweak params; PBR has addon toggles at the top (empirical SSS / parallax / refraction) |
| Region 4 — Performance | Cost estimates and current formulas; the matching row highlights when you change a module |
| Timeline | Press `K` to key a pose; playback lerps smoothly between keys |

Cull faces = which side to draw: Front = outward faces; Back = inward faces (e.g. sky dome); Both = double-sided.

Parameters, textures, modules, addons, and render state support **Ctrl+Z / Ctrl+Y**.

### Built-in categories

- **Unlit**: color + main texture
- **PBR**: swappable D / G / F / F0, direct specular, direct diffuse, indirect diffuse, indirect specular; light vector (main / main+shadow); optional empirical SSS, interior parallax, grab-pass refraction (refraction forces transparent queue; URP needs Opaque Texture enabled)
- **Toon**: templates for Body / Face / Hair
- **Atmosphere**: transmittance (path integrate / LUT) × scattering (single / multi); LUT mode needs a Transmittance Lut; multi also needs a Multi Scattering Lut
- **Custom**: pick Custom in Region 1 and use your own Host

Changing numbers and textures alone does **not** recompile. Swapping modules/categories, or render-state changes that affect variants, re-stitches the Shader.

### Automatic pairings in the workshop

- PBR direct diffuse set to **SSS** also switches indirect diffuse to SSS (turning it off restores SH)
- Atmosphere scattering set to **multi** forces transmittance to **LUT** (multi integrate needs LUTs)
- Light vector set to **main+shadow** turns on Region 1 “Receive Shadows”
- PBR **refraction** addon sets transparent queue and disables ZWrite; turning the addon off restores opaque

---

## For developers: custom shaders

The plugin reads two text formats. HLSL stays URP HLSL—Meta / Properties / SLOT / Formula / Cost wrap it.  
**Swapping a module = swapping that HLSL block + those Properties.** Keep your own math; the plugin only stitches.

| Extension | What it is | Where |
|-----------|------------|-------|
| `.sdiy` | Host skeleton (vert/frag, textures, slot declarations) | `Assets/ShaderDIY/Shaders/Sources/` |
| `.sdiym` | Swappable module (one slot’s function + its params) | `Assets/ShaderDIY/Shaders/Modules/<Category>/` |

Region 1 gear menu can also **New Host Shader / New Module** from `Shaders/Templates/`.  
Pick category **Custom** in Region 1 to list Hosts with `Category = Custom`.

### What compile does

1. Read the Host + each slot’s module name from the material Graph
2. Merge Host and module Properties (module params only appear in Region 3 when selected)
3. Inject each module’s HLSL at `// #SLOT_INJECT` in the Host
4. Framework adds Pass, Cull/ZWrite/Queue, shadow/multi-light `#pragma`, Unity version macros, CBUFFER, texture sample helpers
5. Writes `Generated/<name>_<guid>.shader` and assigns it to the material

Do not edit `Generated/` by hand. Do not put `SubShader` / `Pass` / `Cull` / `Features` in the Host—Region 1 owns those.

### Rules

- If a `[SLOT]` exists, `Default` must name an existing module or compile fails
- Module function name must match the name in the slot `Signature`
- Property **names** in English; display names may be localized
- Files UTF-8; plugin writes CRLF
- Modules match by `Slot` + `Category`; one slot can have many modules, switched via dropdown

### Minimal Host (`.sdiy`)

```
ShaderDIY "ShaderDIY/Custom/MyHost"
{
    Meta
    {
        Category = Custom
        Pipeline = URP
        Description = "Description"
        CompanionScripts =
    }

    Properties
    {
        [Tab Base Map]
        [Region 2][TexOps(TilingOffset, Channel, Invert, MipBias)]
        _BaseMap ("Base Map", 2D) = "white" {}

        [Region 3] _BaseColor ("Color", Color) = (1,1,1,1)
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

- `ShaderDIY "path"` becomes the generated Shader name prefix
- `Category`: `PBR` / `Toon` / `Unlit` / `Custom` / `Atmosphere` — matches Region 1
- `CompanionScripts`: comma-separated MonoBehaviour full names auto-added on assign; may be empty
- `// #SLOT_INJECT`: module functions land here (must be **before** Vert/Frag so modules can call each other)
- Hosts without SLOT are fine (e.g. Unlit); Region 3 only shows base params
- Addon slots use `Kind="Addon"` → Region 3 top toggles instead of dropdowns; default module name is often `None` / `无`, switched to another same-slot module when on

Slot syntax:

```
[SLOT SlotName Default="DefaultModule" Signature="ReturnType FuncName(Args)" Kind="Addon"]
```

`Kind` is optional. `Signature` is optional (e.g. atmosphere transmittance that only swaps helper blocks with no shared entry).

### Module (`.sdiym`)

```
Module "MyTerm" Slot="CustomTerm" Category="Custom"
{
    Properties
    {
        [Region 3] _TermScale ("Module Scale", Range(0,2)) = 1
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

- `Module "Name"` = dropdown label = Host `Default="Name"`
- `Slot` must exactly match Host `[SLOT Name]`
- `Category` should match the Host category; put the file under `Modules/<Category>/`
- `Keywords` become `#define NAME 1` for Host/other modules via `#ifdef`
- `Cost` is Region 4 estimate only—not used in compile
- `Formula` is Region 4 display only

To add a new algorithm to an existing Host: no Host edit needed—add a `.sdiym` with the same `Slot` and signature. After save it appears in the dropdown.

### Property attributes

| Attribute | Effect |
|-----------|--------|
| `[Region 2]` | Texture region |
| `[Region 3]` | Param region (module params under their slot; Host params under “Base”) |
| `[Tab Name]` | Region 2 tab label |
| `[TexOps(TilingOffset, Channel, Invert, MipBias)]` | Controls on the texture card; list only what you need, or `TexOps(All)` |
| `[ShowIf(_Prop, 1)]` | Show when `_Prop` is near that value |
| `[Tooltip("...")]` | Hover tip; overrides default |
| `[Invert]` / `[OneMinus]` | 1− toggle next to the param |
| `[HDR]` etc. | Passed through into the generated Shader |

Types: `2D` / `Color` / `Range(a,b)` / `Float` / `Vector` / `Toggle` / `Enum(A,B)`

Toon outline: if the Host has `_OutlineWidth`, the framework adds a front-cull outline Pass (uses `_OutlineColor`).

### Sampling

The compiler emits `ShaderDIYSample_<PropName>(float2 uv)` for each 2D property (handles `_ST`, mip, channel, invert).  
For Region 2 controls to work, sample through that helper—not raw `SAMPLE_TEXTURE2D`.  
LUTs, Matcaps, and procedurally built UVs may still use `SAMPLE_TEXTURE2D` / `SAMPLE_TEXTURE2D_LOD`.

Normal example: `UnpackNormalScale(ShaderDIYSample_BumpMap(uv), _BumpScale)`

### Built-in macros (usable in Frag)

- `SHADERDIY_RECEIVE_SHADOWS` — receive shadows
- `SHADERDIY_MULTI_LIGHT` — additional lights
- `SHADERDIY_URP12` / `URP14` / `URP17`
- `SHADERDIY_Unity2021` / `Unity2022` / `Unity6`

Suggested main light:

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

Defined in `Shaders/Library/ShaderDIYCore.hlsl`; generated Shaders `#include` it automatically. Host Vert should fill `ShaderDIYVaryings`; Frag builds `ShadingContext` then calls modules.

- Geometry: `positionWS` `positionCS` `uv` `N` `V` `L` `H` `T` `B` `Ng`
- Material: `albedo` `alpha` `metallic` `smoothness` `perceptualRoughness` `roughness` `occlusion` `emission` `f0` `specularColor`
- Dot products: `NdotV` `NdotL` `NdotH` `VdotH` `TdotH` `BdotH` `TdotL` `BdotL`
- Lighting: `lightColor` `attenuation` `bakedGI`

`ShaderDIY_FillLight(ctx, L, lightColor, attenuation)` fills `L`/`H` and dots. Anisotropic NDF uses T/B dots.

### Existing slots (replacement modules must match these names)

**PBR** (`PBR_Lit.sdiy`)

| Slot | Function | Notes |
|------|----------|-------|
| NDF | `float EvaluateNDF(ShadingContext ctx)` | Called again inside specular modules |
| G | `float EvaluateG(ShadingContext ctx)` | |
| F | `float3 EvaluateF(ShadingContext ctx)` | |
| F0 | `float3 EvaluateF0(ShadingContext ctx)` | |
| Light | `void EvaluateLight(inout ShadingContext ctx)` | Fills `ctx.L` / `lightColor` / `attenuation` |
| DirectSpecular | `float3 EvaluateDirectSpecular(ShadingContext ctx)` | |
| DirectDiffuse | `float3 EvaluateDirectDiffuse(ShadingContext ctx)` | SSS pairs IndirectDiffuse |
| IndirectDiffuse | `float3 EvaluateIndirectDiffuse(ShadingContext ctx)` | |
| IndirectSpecular | `float3 EvaluateIndirectSpecular(ShadingContext ctx)` | |
| LayerSSS | `float3 EvaluateLayerSSS(ShadingContext ctx)` | `Kind=Addon` |
| LayerParallax | `float3 EvaluateLayerParallax(ShadingContext ctx)` | `Kind=Addon` |
| LayerRefraction | `float4 EvaluateLayerRefraction(ShadingContext ctx, float3 result)` | `Kind=Addon`, returns final color; Host uses `_LAYER_REFRACTION` to skip clip when on |

PBR Host calls these in Frag. When writing a new NDF, specular modules still call `EvaluateNDF`. Addons should not rewrite D/G/F—add on top of the final color.

**Atmosphere** (`Atmosphere.sdiy`)

| Slot | Function | Notes |
|------|----------|-------|
| Transmittance | No shared Signature; modules provide `GetTransmittance` etc. | Path integrate / LUT |
| InScattering | `float3 EvaluateAtmosphere(float3 positionWS)` | Single / multi |

Multi-scatter modules sample Host helpers like `RayleiCoefficient`, so `// #SLOT_INJECT` must sit **after** those helpers and **before** Vert.

**Toon**: three separate Hosts (Body / Face / Hair); Region 1 switches templates—not PBR-style slot swaps.

**Custom**: declare your own `[SLOT]`s; any function names as long as modules match.

### Suggested workflow

1. Gear → New Host → get a compiling skeleton (texture + color + scale)
2. For swappable logic, check “also create a slot + default module”, or create Module separately
3. In Host Frag, build `ShadingContext` and call module functions (see `PBR_Lit.sdiy`)
4. In Material Workshop Region 1 pick the category (Custom → Custom) → swap modules in preview; Region 3 params and Region 4 formulas should follow
5. Second algorithm for an existing slot: only add a `.sdiym`—do not duplicate the whole Host

### Common mistakes

- SLOT declared but Default module missing → missing module
- Module function name ≠ Signature → unimplemented signature
- Textures not sampled via `ShaderDIYSample_*` → Region 2 tiling/channel/mip/invert do nothing
- Cull / Unity version written in Host → ignored; use Region 1
- Hand-editing `Generated/*.shader` → overwritten next compile
- Do not use Chinese property **names**
- `// #SLOT_INJECT` after Vert → module funcs become local; compile fails
- Atmosphere multi without LUT textures → samples black

### Reference files

- Full Host: `Shaders/Sources/PBR_Lit.sdiy`
- Simple Host: `Shaders/Sources/Unlit_Color.sdiy`
- Atmosphere Host: `Shaders/Sources/Atmosphere.sdiy`
- Toon Host: `Shaders/Sources/Toon_Body.sdiy` (also Face / Hair)
- Modules: `Shaders/Modules/PBR/DirectDiffuse_Lambert.sdiym`, `NDF_Anisotropic.sdiym`, `Light_MainShadow.sdiym`
- Addon modules: `Shaders/Modules/PBR/LayerSSS_Ice.sdiym`, etc.
- Templates: `Shaders/Templates/HostTemplate.sdiy`, `ModuleTemplate.sdiym`
- Short cheat sheet: `Assets/ShaderDIY/Documentation/SdiyFormat.txt`
