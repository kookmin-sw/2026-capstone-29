Shader "Hidden/MistFog"
{
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "MistFogPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // --- Mist parameters (set from C# script) ---
            float  _MistDensity;        // 0 ~ 1, controlled by C# fade-in
            float4 _MistColor;          // mist tint color
            float  _MistNoiseScale;     // noise UV scale
            float  _MistNoiseSpeed;     // noise scroll speed
            float  _MistLayerScale2;    // second noise layer scale multiplier
            float  _MistSoftness;       // how soft/smooth the mist blends (0.5~2.0)

            // ---- noise helpers ----
            float hash2D(float2 p)
            {
                p = frac(p * float2(443.8975, 397.2973));
                p += dot(p, p.yx + 19.19);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash2D(i);
                float b = hash2D(i + float2(1, 0));
                float c = hash2D(i + float2(0, 1));
                float d = hash2D(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm2D(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int idx = 0; idx < 4; idx++)
                {
                    v += a * noise2D(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 screen = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Early out if no mist
                if (_MistDensity < 0.001)
                    return screen;

                // --- Two-layer noise for natural mist feel ---
                float t = _Time.y * _MistNoiseSpeed;

                float2 noiseUV1 = IN.uv * _MistNoiseScale + float2(t * 0.3, t * 0.1);
                float n1 = fbm2D(noiseUV1);

                float2 noiseUV2 = IN.uv * _MistNoiseScale * _MistLayerScale2
                                + float2(-t * 0.2, t * 0.15);
                float n2 = fbm2D(noiseUV2);

                // Blend two layers for organic movement
                float mistNoise = (n1 + n2) * 0.5;

                // Soft vertical gradient: mist slightly thicker at bottom
                float vertGrad = lerp(1.1, 0.8, IN.uv.y);

                // Final mist amount
                float mistAmount = _MistDensity
                                 * lerp(1.0 - _MistSoftness * 0.3, 1.0, mistNoise)
                                 * vertGrad;
                mistAmount = saturate(mistAmount);

                // --- Color blending ---
                // Desaturate slightly first, then blend toward mist color
                half3 desaturated = lerp(screen.rgb,
                    dot(screen.rgb, half3(0.299, 0.587, 0.114)).xxx,
                    mistAmount * 0.3);

                half3 result = lerp(desaturated, _MistColor.rgb, mistAmount * 0.6);

                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
