Shader "Custom/OutlineEdgeShader"
{
    Properties
    {
        // --- Pass 1 (Main Object) Properties ---
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        // --- Pass 2 (Outline) Properties from your Graph ---
        _outlinethickness("outlinethickness", Float) = 0.03
        _outlinecolor("outlinecolor", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // ----------------------------------------------------------------
        // PASS 1: Renders the base object normally so the outline stays on the edges
        // ----------------------------------------------------------------
        UsePass "Universal Render Pipeline/Lit/ForwardLit"

        // ----------------------------------------------------------------
        // PASS 2: The Outline Edge Pass (Recreating your graph setup)
        // ----------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // Render backfaces only and write safely behind/around the main object
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Properties must be defined in this CBUFFER for SRP batching compatibility
            CBUFFER_START(UnityPerMaterial)
                float _outlinethickness;
                float4 _outlinecolor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // FIXED MATHEMATICS:
                // Your graph multiplied Position * Thickness, which collapses the model to (0,0,0) if thickness is 0.
                // To get an outline edge, we shift vertices outward along their Normal, then ADD it to the position.
                float3 normalOffset = input.normalOS * _outlinethickness;
                float3 finalPositionOS = input.positionOS.xyz + normalOffset;

                // Transform to clip space
                output.positionCS = TransformObjectToHClip(finalPositionOS);
                
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                // Returns your flat outline color variable mapped to the Fragment Base Color slot
                return _outlinecolor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}