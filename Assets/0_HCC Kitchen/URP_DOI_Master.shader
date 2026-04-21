Shader "Custom/URP_DOI_Master"
{
    Properties
    {
        _BaseMap ("High Res Texture", 2D) = "white" {}
        _LowResMap ("Low Res Texture", 2D) = "gray" {}

        _Color ("Base Color", Color) = (1,1,1,1)

        _DOI ("Degree of Interest", Range(0,1)) = 0.75

        // Default values match what was hardcoded before
        _MinValue ("Min Brightness", Range(0,1)) = 0.6
        _MaxValue ("Max Brightness", Range(0,1)) = 0.1

        _MinSaturation ("Min Saturation", Range(0,1)) = 0.5
        _MaxSaturation ("Max Saturation", Range(0,1)) = 0.15

        _MinContrast ("Min Contrast", Range(0,2)) = 1.0
        _MaxContrast ("Max Contrast", Range(0,2)) = 1.2   // was: lerp(..., 1.35, popFactor)

        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _MaxEmission ("Max Emission", Range(0,2)) = 0.3

        _BlurStrength ("Blur Strength", Range(0,0.02)) = 0.005
        _DesaturateStrength ("Desaturate Strength", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            sampler2D _BaseMap;
            sampler2D _LowResMap;

            float4 _Color;
            float _DOI;

            float _MinValue, _MaxValue;
            float _MinSaturation, _MaxSaturation;
            float _MinContrast, _MaxContrast;

            float4 _EmissionColor;
            float _MaxEmission;

            float _BlurStrength;
            float _DesaturateStrength;

            float _EnableDesaturation;
            float _EnableDarkening;
            float _EnableSaturationBoost;
            float _EnableBrightnessBoost;
            float _EnableContrast;
            float _EnableEmission;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0., -1./3., 2./3., -1.);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1e-10;
                return float3(abs(q.z + (q.w - q.y) / (6. * d + e)), d / (q.x + e), q.x);
            }

            float3 HSVToRGB(float3 c)
            {
                float4 K = float4(1., 2./3., 1./3., 3.);
                float3 p = abs(frac(c.xxx + K.xyz) * 6. - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // Simple box blur using 4 offset samples scaled by _BlurStrength
            float4 SampleBlurred(sampler2D tex, float2 uv, float strength)
            {
                float4 c  = tex2D(tex, uv + float2( strength,  strength));
                       c += tex2D(tex, uv + float2(-strength,  strength));
                       c += tex2D(tex, uv + float2( strength, -strength));
                       c += tex2D(tex, uv + float2(-strength, -strength));
                return c * 0.25;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float doi = saturate(_DOI);

                // Low-res map is blurred according to _BlurStrength
                // (high-res is always sampled sharp)
                float4 colHigh = tex2D(_BaseMap, i.uv);
                float4 colLow  = _BlurStrength > 0.0001
                                    ? SampleBlurred(_LowResMap, i.uv, _BlurStrength)
                                    : tex2D(_LowResMap, i.uv);

                colHigh *= _Color;
                colLow  *= _Color;

                float4 col = lerp(colLow, colHigh, smoothstep(0.0, 0.75, doi));

                // ── BELOW 0.75: desaturate + darken ──────────────────────────
                float lowFactor = saturate((0.75 - doi) * 4.0);
                float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));

                // _MinSaturation controls how much desaturation is applied at DOI=0
                // Default 0.5 matches the old hardcoded "lowFactor * 0.5"
                if (_EnableDesaturation > 0.5)
                    col.rgb = lerp(col.rgb, gray.xxx, lowFactor * _MinSaturation);

                // _MinValue is the brightness floor at DOI=0
                // Default 0.6 matches the old hardcoded "lerp(1.0, 0.6, lowFactor)"
                if (_EnableDarkening > 0.5)
                    col.rgb *= lerp(1.0, _MinValue, lowFactor);

                // ── ABOVE 0.75: pop ───────────────────────────────────────────
                float popFactor = saturate((doi - 0.75) * 4.0);

                float3 hsv = RGBToHSV(col.rgb);

                // _MaxSaturation is the saturation boost added at DOI=1
                // Default 0.25 matches old hardcoded "popFactor * 0.25"
                if (_EnableSaturationBoost > 0.5)
                    hsv.y = saturate(hsv.y + popFactor * _MaxSaturation);

                // _MaxValue is the brightness boost added at DOI=1
                // Default 0.15 matches old hardcoded "popFactor * 0.15"
                if (_EnableBrightnessBoost > 0.5)
                    hsv.z = saturate(hsv.z + popFactor * _MaxValue);

                col.rgb = HSVToRGB(hsv);

                // _MinContrast / _MaxContrast define the contrast range across DOI
                // Defaults 1.0 / 1.35 match old hardcoded lerp(1.0, 1.35, popFactor)
                if (_EnableContrast > 0.5)
                {
                    float contrast = lerp(_MinContrast, _MaxContrast, popFactor);
                    col.rgb = saturate(((col.rgb - 0.5) * contrast) + 0.5);
                }

                // _MaxEmission controls peak emission intensity at DOI=1
               if (_EnableEmission > 0.5)
                {
                    float emissionIntensity = popFactor * _MaxEmission * 0.15;
                    emissionIntensity = min(emissionIntensity, _MaxEmission * 0.05);
                    col.rgb += _EmissionColor.rgb * emissionIntensity;
                }

                return col;
            }

            ENDHLSL
        }
    }
}