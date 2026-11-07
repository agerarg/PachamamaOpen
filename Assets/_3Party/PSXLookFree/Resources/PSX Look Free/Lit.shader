// Lit per vertex: ambient, the main light, and up to 8 point and spot lights.
Shader "PSX Look Free/Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _UVScroll("UV Scroll (per second)", Vector) = (0, 0, 0, 0)
        _SnapPixels("Vertex Snap (pixels, 0 is off)", Range(0, 8)) = 1
        _Affine("Affine Textures", Range(0, 1)) = 1
        [ToggleUI] _VertexColors("Vertex Colors", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex PSXVertex
            #pragma fragment PSXFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "PSX.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex PSXVertex
            #pragma fragment PSXDepth
            #pragma multi_compile_instancing
            #define PSX_DEPTH
            #include "PSX.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            HLSLPROGRAM
            #pragma vertex PSXVertex
            #pragma fragment PSXDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define PSX_DEPTH_NORMALS
            #include "PSX.hlsl"
            ENDHLSL
        }
    }
}
