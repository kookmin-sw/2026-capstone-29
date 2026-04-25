Shader "Custom/SmokeCloud"
{
    Properties
    {
        _Color ("Smoke Color", Color) = (1, 1, 1, 0.85)
        _ShadowColor ("Shadow Color", Color) = (0.6, 0.6, 0.65, 1)
        _NoiseScale ("Noise Scale", Float) = 2.0
        _NoiseSpeed ("Noise Speed", Float) = 0.3
        _DisplacementStrength ("Displacement", Float) = 0.5
        _ToonSteps ("Toon Shading Steps", Range(2, 6)) = 3
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.0
        _FresnelAlpha ("Fresnel Edge Alpha", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 200

        // ===== Pass 1: Back faces (volume depth) =====
        Pass
        {
            Name "SmokeBack"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ShadowColor;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _DisplacementStrength;
                float  _ToonSteps;
                float  _FresnelPower;
                float  _FresnelAlpha;
            CBUFFER_END

            // ---- noise helpers ----
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                         lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                         lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                float3 shift = float3(100, 100, 100);
                for (int idx = 0; idx < 4; idx++)
                {
                    v += a * noise3D(p);
                    p = p * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 noisePos = worldPos * _NoiseScale + _Time.y * _NoiseSpeed;
                float n = fbm(noisePos) - 0.5;
                IN.positionOS.xyz += IN.normalOS * n * _DisplacementStrength;

                OUT.positionCS  = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - IN.worldPos);
                float3 normal  = normalize(-IN.worldNormal); // flip for back face

                Light mainLight = GetMainLight();
                float NdotL = dot(normal, normalize(mainLight.direction));
                float toon  = floor(NdotL * _ToonSteps) / _ToonSteps;
                toon = saturate(toon * 0.5 + 0.5);

                float3 col = lerp(_ShadowColor.rgb, _Color.rgb, toon);
                float fresnel = pow(1.0 - saturate(dot(viewDir, normal)), _FresnelPower);
                float alpha   = lerp(_Color.a, _FresnelAlpha, fresnel);

                return half4(col, alpha);
            }
            ENDHLSL
        }

        // ===== Pass 2: Front faces =====
        Pass
        {
            Name "SmokeFront"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ShadowColor;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _DisplacementStrength;
                float  _ToonSteps;
                float  _FresnelPower;
                float  _FresnelAlpha;
            CBUFFER_END

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                         lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                         lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                float3 shift = float3(100, 100, 100);
                for (int idx = 0; idx < 4; idx++)
                {
                    v += a * noise3D(p);
                    p = p * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 noisePos = worldPos * _NoiseScale + _Time.y * _NoiseSpeed;
                float n = fbm(noisePos) - 0.5;
                IN.positionOS.xyz += IN.normalOS * n * _DisplacementStrength;

                OUT.positionCS  = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - IN.worldPos);
                float3 normal  = normalize(IN.worldNormal);

                Light mainLight = GetMainLight();
                float NdotL = dot(normal, normalize(mainLight.direction));
                float toon  = floor(NdotL * _ToonSteps) / _ToonSteps;
                toon = saturate(toon * 0.5 + 0.5);

                float3 col = lerp(_ShadowColor.rgb, _Color.rgb, toon);
                float fresnel = pow(1.0 - saturate(dot(viewDir, normal)), _FresnelPower);
                float alpha   = lerp(_Color.a, _FresnelAlpha, fresnel);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
