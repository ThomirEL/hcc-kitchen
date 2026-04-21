Shader "Custom/URP_DOI_Master"
{
    Properties
    {
        // ── Standard URP Lit properties ───────────────────────────────────
        _BaseMap        ("Albedo (High Res)",    2D)            = "white" {}
        _BaseColor      ("Base Color",           Color)         = (1,1,1,1)
        _Metallic       ("Metallic",             Range(0,1))    = 0.0
        _Smoothness     ("Smoothness",           Range(0,1))    = 0.5
        _BumpMap        ("Normal Map",           2D)            = "bump" {}
        _BumpScale      ("Normal Scale",         Float)         = 1.0
        _OcclusionMap   ("Occlusion",            2D)            = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1))    = 1.0

        // ── DOI-specific ──────────────────────────────────────────────────
        _LowResMap      ("Low Res Texture",      2D)            = "gray" {}
        _DOI            ("Degree of Interest",   Range(0,1))    = 0.75

        _MinValue       ("Min Brightness",       Range(0,1))    = 0.6
        _MaxValue       ("Max Brightness",       Range(0,1))    = 0.1
        _MinSaturation  ("Min Saturation",       Range(0,1))    = 0.5
        _MaxSaturation  ("Max Saturation",       Range(0,1))    = 0.15
        _MinContrast    ("Min Contrast",         Range(0,2))    = 1.0
        _MaxContrast    ("Max Contrast",         Range(0,2))    = 1.2

        _EmissionColor  ("Emission Color",       Color)         = (1,1,1,1)
        _MaxEmission    ("Max Emission",         Range(0,2))    = 0.3
        _BlurStrength   ("Blur Strength",        Range(0,0.02)) = 0.005

        // ── Toggle floats (set by IARManager) ────────────────────────────
        _EnableDesaturation  ("Enable Desaturation",   Float) = 1
        _EnableDarkening     ("Enable Darkening",      Float) = 1
        _EnableSaturationBoost("Enable Sat Boost",     Float) = 1
        _EnableBrightnessBoost("Enable Bright Boost",  Float) = 1
        _EnableContrast      ("Enable Contrast",       Float) = 1
        _EnableEmission      ("Enable Emission",       Float) = 0
    }

    SubShader
    {
        // Transparent queue so alpha blending works
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            // Alpha blending — required for transparency
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma vertex   vert
            #pragma fragment frag

            // URP Lit feature keywords — needed for lighting to work correctly
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            // ── Texture / sampler declarations ────────────────────────────
            // After — _BaseMap and _BumpMap are already declared by SurfaceInput.hlsl
            TEXTURE2D(_LowResMap);     SAMPLER(sampler_LowResMap);
            TEXTURE2D(_OcclusionMap);  SAMPLER(sampler_OcclusionMap);

            // ── CBUFFER — must match property names exactly ───────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _LowResMap_ST;
                float4 _BaseColor;

                float  _Metallic;
                float  _Smoothness;
                float  _BumpScale;
                float  _OcclusionStrength;

                float  _DOI;
                float  _MinValue,  _MaxValue;
                float  _MinSaturation, _MaxSaturation;
                float  _MinContrast,   _MaxContrast;

                float4 _EmissionColor;
                float  _MaxEmission;
                float  _BlurStrength;

                float  _EnableDesaturation;
                float  _EnableDarkening;
                float  _EnableSaturationBoost;
                float  _EnableBrightnessBoost;
                float  _EnableContrast;
                float  _EnableEmission;
            CBUFFER_END

            // ── Vertex input/output ───────────────────────────────────────
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 lightmapUV   : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 tangentWS    : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
                float  fogFactor    : TEXCOORD5;
            };

            // ── Helpers ───────────────────────────────────────────────────
            float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0., -1./3., 2./3., -1.);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.*d+e)), d/(q.x+e), q.x);
            }

            float3 HSVToRGB(float3 c)
            {
                float4 K = float4(1., 2./3., 1./3., 3.);
                float3 p = abs(frac(c.xxx + K.xyz) * 6. - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            float4 SampleBlurred(float2 uv, float strength)
            {
                float4 c  = SAMPLE_TEXTURE2D(_LowResMap, sampler_LowResMap, uv + float2( strength,  strength));
                       c += SAMPLE_TEXTURE2D(_LowResMap, sampler_LowResMap, uv + float2(-strength,  strength));
                       c += SAMPLE_TEXTURE2D(_LowResMap, sampler_LowResMap, uv + float2( strength, -strength));
                       c += SAMPLE_TEXTURE2D(_LowResMap, sampler_LowResMap, uv + float2(-strength, -strength));
                return c * 0.25;
            }

            // ── Vertex shader ─────────────────────────────────────────────
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.positionHCS = posInputs.positionCS;
                o.positionWS  = posInputs.positionWS;
                o.normalWS    = nrmInputs.normalWS;
                o.tangentWS   = float4(nrmInputs.tangentWS, v.tangentOS.w);
                o.uv          = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUV);
                OUTPUT_SH(o.normalWS, o.vertexSH);

                return o;
            }

            // ── Fragment shader ───────────────────────────────────────────
            float4 frag(Varyings i) : SV_Target
            {
                float doi = saturate(_DOI);
                float2 uv = i.uv;

                // ── 1. Sample albedo (hi-res vs lo-res blend) ─────────────
                float4 colHigh = SAMPLE_TEXTURE2D(_BaseMap,   sampler_BaseMap,   uv) * _BaseColor;
                float4 colLow  = (_BlurStrength > 0.0001
                                    ? SampleBlurred(uv, _BlurStrength)
                                    : SAMPLE_TEXTURE2D(_LowResMap, sampler_LowResMap, uv))
                                 * _BaseColor;

                float4 albedo = lerp(colLow, colHigh, smoothstep(0.0, 0.75, doi));

                // ── 2. Normal map ─────────────────────────────────────────
                float4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
                float3 normalTS     = UnpackNormalScale(normalSample, _BumpScale);

                float3 bitangentWS  = cross(i.normalWS, i.tangentWS.xyz) * i.tangentWS.w;
                float3x3 TBN        = float3x3(i.tangentWS.xyz, bitangentWS, i.normalWS);
                float3 normalWS     = normalize(mul(normalTS, TBN));

                // ── 3. Occlusion ──────────────────────────────────────────
                float occlusion = lerp(1.0,
                    SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g,
                    _OcclusionStrength);

                // ── 4. Build SurfaceData + InputData for URP Lit ──────────
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = albedo.rgb;
                surfaceData.alpha       = albedo.a;
                surfaceData.metallic    = _Metallic;
                surfaceData.smoothness  = _Smoothness;
                surfaceData.normalTS    = normalTS;
                surfaceData.occlusion   = occlusion;
                surfaceData.emission    = 0;

                InputData inputData = (InputData)0;
                inputData.positionWS        = i.positionWS;
                inputData.normalWS          = normalWS;
                inputData.viewDirectionWS   = normalize(GetWorldSpaceViewDir(i.positionWS));
                inputData.shadowCoord       = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord          = i.fogFactor;
                inputData.vertexLighting    = 0;
                inputData.bakedGI           = SAMPLE_GI(i.lightmapUV, i.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionHCS);
                inputData.shadowMask        = SAMPLE_SHADOWMASK(i.lightmapUV);

                // ── 5. Full URP Lit lighting ──────────────────────────────
                // At DOI = 0.75 this is exactly what URP/Lit produces
                float4 col = UniversalFragmentPBR(inputData, surfaceData);

                // ── 6. DOI effects layered on top ─────────────────────────
                float lowFactor = saturate((0.75 - doi) * 4.0);
                float gray      = dot(col.rgb, float3(0.299, 0.587, 0.114));

                if (_EnableDesaturation > 0.5)
                    col.rgb = lerp(col.rgb, gray.xxx, lowFactor * _MinSaturation);

                if (_EnableDarkening > 0.5)
                    col.rgb *= lerp(1.0, _MinValue, lowFactor);

                float popFactor = saturate((doi - 0.75) * 4.0);
                float3 hsv = RGBToHSV(col.rgb);

                if (_EnableSaturationBoost > 0.5)
                    hsv.y = saturate(hsv.y + popFactor * _MaxSaturation);

                if (_EnableBrightnessBoost > 0.5)
                    hsv.z = saturate(hsv.z + popFactor * _MaxValue);

                col.rgb = HSVToRGB(hsv);

                if (_EnableContrast > 0.5)
                {
                    float contrast = lerp(_MinContrast, _MaxContrast, popFactor);
                    col.rgb = saturate(((col.rgb - 0.5) * contrast) + 0.5);
                }

                if (_EnableEmission > 0.5)
                {
                    float emissionIntensity = popFactor * _MaxEmission;
                    col.rgb += _EmissionColor.rgb * emissionIntensity;
                }

                // ── 7. Alpha: DOI drives fade, fog applied ────────────────
                // At DOI=0.75: col.a = original alpha (fully opaque if texture has no transparency)
                // Below 0.75: fades out toward transparent
                col.a   *= saturate(doi / 0.75);
                col.rgb  = MixFog(col.rgb, i.fogFactor);

                return col;
            }

            ENDHLSL
        }

        // Shadow caster pass — needed so the object still casts shadows
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}