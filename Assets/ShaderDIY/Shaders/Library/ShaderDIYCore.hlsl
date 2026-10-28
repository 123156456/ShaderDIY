#ifndef SHADERDIY_CORE_INCLUDED
#define SHADERDIY_CORE_INCLUDED

#ifndef PI
#define PI 3.14159265358979323846
#endif

struct ShadingContext
{
    float3 positionWS;
    float4 positionCS;
    float2 uv;
    float3 albedo;
    float alpha;
    float metallic;
    float smoothness;
    float perceptualRoughness;
    float roughness;
    float occlusion;
    float3 emission;
    float3 f0;
    float3 specularColor;
    float3 N;
    float3 V;
    float3 L;
    float3 H;
    float3 T;
    float3 B;
    float3 Ng;
    half NdotV;
    half NdotL;
    half NdotH;
    half VdotH;
    half TdotH;
    half BdotH;
    half TdotL;
    half BdotL;
    half3 lightColor;
    half attenuation;
    half3 bakedGI;
};

struct ShaderDIYVaryings
{
    float4 positionCS : SV_POSITION;
#ifdef SHADERDIY_FX
    float2 uv : TEXCOORD0;
    half4 vertexColor : COLOR;
#if defined(_FX_CLIP)
    float3 positionWS : TEXCOORD1;
#endif
#if defined(_FX_RIM) || defined(_FX_NOISE)
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    half3 viewDirWS : TEXCOORD4;
#endif
#else
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 tangentWS : TEXCOORD2;
    float2 uv : TEXCOORD3;
    float3 viewDirWS : TEXCOORD4;
    half3 vertexSH : TEXCOORD5;
    half fogFactor : TEXCOORD6;
#endif
};

inline half4 ShaderDIY_Swizzle(half4 c, int channel)
{
    if (channel <= 0) return c;
    if (channel == 1) return c.r;
    if (channel == 2) return c.g;
    if (channel == 3) return c.b;
    return c.a;
}

inline void ShaderDIY_FillLight(inout ShadingContext ctx, half3 L, half3 lightColor, half attenuation)
{
    ctx.L = L;
    ctx.H = normalize(ctx.V + L);
    ctx.NdotL = saturate(dot(ctx.N, L));
    ctx.NdotH = saturate(dot(ctx.N, ctx.H));
    ctx.VdotH = saturate(dot(ctx.V, ctx.H));
    ctx.TdotH = dot(ctx.T, ctx.H);
    ctx.BdotH = dot(ctx.B, ctx.H);
    ctx.TdotL = dot(ctx.T, L);
    ctx.BdotL = dot(ctx.B, L);
    ctx.lightColor = lightColor;
    ctx.attenuation = attenuation;
}

inline half ShaderDIY_PerceptualRoughnessToRoughness(half r)
{
    return max(r * r, 0.002h);
}

#endif
