Shader "Hidden/WaterFog"
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
            Name "WaterFogPass"

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


            float  _WaterFogDensity;
            float4 _WaterFogColor;
            float  _WaterFogNoiseScale;
            float  _WaterFogNoiseSpeed;
            float  _WaterCausticScale;
            float  _WaterCausticSpeed;
            float  _WaterCausticIntensity;


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


            float caustic(float2 uv)
            {
                float2 p = uv * _WaterCausticScale;
                float t = _Time.y * _WaterCausticSpeed;


                float n1 = fbm2D(p + float2(t * 0.7, t * 0.3));
                float n2 = fbm2D(p * 1.4 + float2(-t * 0.5, t * 0.6));


                float c = abs(n1 - n2);
                c = pow(c, 0.6); 
                c = saturate(c * 2.0);


                c = floor(c * 3.0) / 3.0;

                return c * _WaterCausticIntensity;
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

                if (_WaterFogDensity < 0.01)
                    return screen;


                float2 noiseUV = IN.uv * _WaterFogNoiseScale + _Time.y * _WaterFogNoiseSpeed;
                float n = fbm2D(noiseUV);


                float vertGrad = lerp(1.2, 0.6, IN.uv.y);


                float fogAmount = _WaterFogDensity * lerp(0.6, 1.0, n) * vertGrad;
                fogAmount = saturate(fogAmount);


                half3 tinted = screen.rgb * lerp(half3(1,1,1), _WaterFogColor.rgb, fogAmount * 0.5);


                half3 fogged = lerp(tinted, _WaterFogColor.rgb, fogAmount * 0.7);


                float caust = caustic(IN.uv);

                caust *= saturate(1.0 - IN.uv.y) * fogAmount;
                fogged += caust * half3(0.7, 0.9, 1.0);

                return half4(fogged, 1.0);
            }
            ENDHLSL
        }
    }
}
