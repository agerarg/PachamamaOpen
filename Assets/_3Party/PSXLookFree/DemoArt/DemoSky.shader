// The demo's dusk: Godot's procedural sky gradient, hazed by the fog colour.
Shader "PSX Look Free/Demo Sky"
{
    Properties
    {
        _Top("Top", Color) = (0.12, 0.08, 0.26, 1)
        _Horizon("Horizon", Color) = (0.85, 0.42, 0.38, 1)
        _Bottom("Bottom", Color) = (0.1, 0.07, 0.14, 1)
        _FogMix("Fog", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _Top, _Horizon, _Bottom;
            half _FogMix;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vertex(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 Fragment(Varyings i) : SV_Target
            {
                float y = normalize(i.dir).y;
                float c = 1.0 - acos(abs(y)) / (PI * 0.5);
                half3 sky = y > 0 ? lerp(_Horizon.rgb, _Top.rgb, saturate(1.0 - pow(1.0 - c, 1.0 / 0.15)))
                                  : lerp(_Horizon.rgb, _Bottom.rgb, saturate(1.0 - pow(1.0 - c, 1.0 / 0.02)));
                return half4(lerp(sky, unity_FogColor.rgb, _FogMix), 1);
            }
            ENDHLSL
        }
    }
}
