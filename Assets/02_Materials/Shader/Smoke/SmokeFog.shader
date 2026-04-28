Shader "Hidden/SmokeFog"
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
            Name "SmokeFogPass"

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

            float  _SmokeFogDensity;
            float4 _SmokeFogColor;
            float  _SmokeFogNoiseScale;
            float  _SmokeFogNoiseSpeed;

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

                if (_SmokeFogDensity < 0.01)
                    return screen;

                float2 noiseUV = IN.uv * _SmokeFogNoiseScale + _Time.y * _SmokeFogNoiseSpeed;
                float n = fbm2D(noiseUV);

                float2 center = IN.uv - 0.5;
                float vignette = 1.0 + dot(center, center) * 2.0;

                float fogAmount = _SmokeFogDensity * lerp(0.7, 1.0, n) * vignette;
                fogAmount = saturate(fogAmount);

                half4 result = lerp(screen, _SmokeFogColor, fogAmount);
                return result;
            }
            ENDHLSL
        }
    }
}
