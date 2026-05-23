Shader "Custom/OutlineShader"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        // Width is now in screen-space pixels, not world units.
        // 1–4 is a good range.
        _OutlineWidth ("Outline Width (px)", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 1. Get the clip-space position as normal
                float4 posCS = TransformObjectToHClip(IN.positionOS.xyz);

                // 2. Transform the normal into view space, take only XY
                //    (the screen-plane direction we want to push toward)
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 normalVS = TransformWorldToViewDir(normalWS);
                float2 normalSS = normalize(normalVS.xy);

                // 3. Offset in clip space.
                //    Multiplying by posCS.w converts from NDC to clip coords,
                //    which keeps the outline the same pixel width at any depth.
                posCS.xy += normalSS * (_OutlineWidth * 0.001) * posCS.w;

                OUT.positionCS = posCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1.0);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
