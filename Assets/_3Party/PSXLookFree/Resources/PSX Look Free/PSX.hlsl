// The PS1 look every PSX Look Free shader shares: vertices snapped to the pixel grid of the target they
// draw to, affine (not perspective-correct) texture mapping, point-sampled textures, lighting per
// vertex. The including pass may #define PSX_UNLIT, PSX_CUTOUT, PSX_TRANSPARENT or PSX_CHROME, and
// PSX_DEPTH or PSX_DEPTH_NORMALS for the depth passes.
#ifndef PSX_LOOK_INCLUDED
#define PSX_LOOK_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/AmbientProbe.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    float4 _UVScroll;
    float _SnapPixels;
    float _Affine;
    float _VertexColors;
    half _Cutoff;
    half _Reflection;
CBUFFER_END

TEXTURE2D(_BaseMap);
TEXTURE2D(_ReflectionTex);

// Point and spot lights, uploaded by PSXLights: URP lights them per pixel or per cluster, not per vertex.
#define PSX_MAX_LIGHTS 8
int _PSXLightCount;
float4 _PSXLightPosition[PSX_MAX_LIGHTS];  // xyz, and 1 / range squared
half4 _PSXLightColor[PSX_MAX_LIGHTS];
half4 _PSXLightSpot[PSX_MAX_LIGHTS];       // the direction it shines back along, and URP's angle scale in w
half4 _PSXLightSpotOffset[PSX_MAX_LIGHTS]; // x: URP's angle offset (1 with a scale of 0 for a point light)

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 uvw : TEXCOORD0;  // uv times w, and w: dividing per pixel undoes the perspective correction
    half4 color : COLOR;
    half4 light : TEXCOORD1; // rgb, and the fog factor in a
#ifdef PSX_CHROME
    float3 env : TEXCOORD2;
#endif
#ifdef PSX_DEPTH_NORMALS
    half3 normalWS : TEXCOORD3;
#endif
};

half3 PSXVertexLight(float3 positionWS, half3 n)
{
    half3 c = SampleSH(n) + _MainLightColor.rgb * saturate(dot(n, half3(_MainLightPosition.xyz)));
    for (int i = 0; i < min(_PSXLightCount, PSX_MAX_LIGHTS); i++)
    {
        float3 d = _PSXLightPosition[i].xyz - positionWS;
        float d2 = max(dot(d, d), 1e-4);
        half3 l = half3(d * rsqrt(d2));
        half fade = saturate(1.0 - d2 * _PSXLightPosition[i].w * d2 * _PSXLightPosition[i].w);
        half cone = saturate(dot(_PSXLightSpot[i].xyz, l) * _PSXLightSpot[i].w + _PSXLightSpotOffset[i].x);
        c += _PSXLightColor[i].rgb * (fade * fade / d2 * cone * cone * saturate(dot(n, l)));
    }
    return c;
}

Varyings PSXVertex(Attributes v)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    float4 clip = TransformWorldToHClip(positionWS);
    if (_SnapPixels > 0 && clip.w > 0)
    {
        float2 grid = _ScreenParams.xy * 0.5 / _SnapPixels;
        clip.xy = round(clip.xy / clip.w * grid) / grid * clip.w;
    }
    o.positionCS = clip;
    float w = lerp(1.0, clip.w, _Affine);
    o.uvw = float3((TRANSFORM_TEX(v.uv, _BaseMap) + _UVScroll.xy * _Time.y) * w, w);
    o.color = _VertexColors > 0.5 ? v.color : half4(1, 1, 1, 1);
#ifndef UNITY_COLORSPACE_GAMMA
    o.color.rgb = SRGBToLinear(o.color.rgb);
#endif
    half3 n = half3(TransformObjectToWorldNormal(v.normalOS));
#if defined(PSX_UNLIT) || defined(PSX_DEPTH) || defined(PSX_DEPTH_NORMALS)
    o.light.rgb = 1;
#else
    o.light.rgb = PSXVertexLight(positionWS, n);
#endif
    o.light.a = ComputeFogFactor(clip.z);
#ifdef PSX_CHROME
    // a sphere map looked up by the view-space normal, per vertex as on the PS1
    float3 nv = normalize(TransformWorldToViewDir(n));
    o.env = float3((nv.xy * 0.5 + 0.5) * w, w);
#endif
#ifdef PSX_DEPTH_NORMALS
    o.normalWS = n;
#endif
    return o;
}

half4 PSXColor(Varyings i)
{
    half4 c = _BaseColor * SAMPLE_TEXTURE2D(_BaseMap, sampler_PointRepeat, i.uvw.xy / i.uvw.z) * i.color;
#if defined(PSX_CUTOUT)
    clip(c.a - _Cutoff);
#endif
#ifdef PSX_CHROME
    c.rgb = lerp(c.rgb, SAMPLE_TEXTURE2D(_ReflectionTex, sampler_PointClamp, i.env.xy / i.env.z).rgb, _Reflection);
#endif
    c.rgb = MixFog(c.rgb * i.light.rgb, i.light.a);
#ifndef PSX_TRANSPARENT
    c.a = 1;
#endif
    return c;
}

half4 PSXDepth(Varyings i) : SV_Target
{
#ifdef PSX_CUTOUT
    PSXColor(i);
#endif
    return i.positionCS.z;
}

#ifdef PSX_DEPTH_NORMALS
half4 PSXDepthNormals(Varyings i) : SV_Target
{
#ifdef PSX_CUTOUT
    PSXColor(i);
#endif
    float3 n = normalize(float3(i.normalWS));
#ifdef _GBUFFER_NORMALS_OCT
    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0);
#else
    return half4(n, 0);
#endif
}
#endif

half4 PSXFragment(Varyings i) : SV_Target
{
    return PSXColor(i);
}

#endif
