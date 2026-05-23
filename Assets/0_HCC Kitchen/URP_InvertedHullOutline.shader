Shader "Custom/URP_InvertedHullOutline"
{
    Properties
    {
        _OutlineColor  ("Outline Color",  Color)              = (1, 0.85, 0, 1)
        _OutlineWidth  ("Outline Width",  Range(0.0, 0.05))   = 0.006
    }
    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry+1"   // Draw on top of the object's own surface
        }

        Pass
        {
            Name "InvertedHullOutline"
            Cull  Front     // Only render back-faces — that's what peeks out as the outline
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // Required for GPU instancing + single-pass stereo (Quest multiview)
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO          // Writes correct eye index for Quest multiview
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Push each vertex outward along its normal in object space.
                // This is the core of the inverted-hull technique.
                float3 expanded = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionHCS = TransformObjectToHClip(float4(expanded, 1.0));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;   // Solid flat colour — cheap on Quest GPU
            }
            ENDHLSL
        }
    }
}