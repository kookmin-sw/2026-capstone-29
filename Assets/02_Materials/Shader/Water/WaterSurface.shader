Shader "Custom/WaterSurface"
{
    Properties
    {
        _Color ("Water Color", Color) = (0.15, 0.55, 0.7, 0.85)
        _ShallowColor ("Shallow Color", Color) = (0.3, 0.75, 0.8, 0.85)
        _SpecColor2 ("Specular Color", Color) = (1, 1, 1, 1)
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _WaveScale ("Wave Scale", Float) = 1.5
        _WaveHeight ("Wave Height", Float) = 0.15
        _ToonSteps ("Toon Steps", Range(2, 6)) = 5
        _SpecSize ("Specular Size", Range(1, 128)) = 32
        _SpecSteps ("Specular Steps", Range(1, 4)) = 1.01
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 3.75
        _FresnelColor ("Fresnel Tint", Color) = (0.6, 0.85, 0.95, 1)

        [Header(Wave Crest Foam)]
        _CrestFoamThreshold ("Crest Foam Threshold", Range(0, 1)) = 0.75
        _CrestFoamColor ("Crest Foam Color", Color) = (1, 1, 1, 1)

        [Header(Intersection Foam)]
        _IntersectFoamColor ("Intersect Foam Color", Color) = (1, 1, 1, 0.9)
        _IntersectFoamWidth ("Intersect Foam Width", Range(0.01, 3.0)) = 0.8
        _IntersectFoamNoiseScale ("Foam Noise Scale", Float) = 8.0
        _IntersectFoamNoiseSpeed ("Foam Noise Speed", Float) = 0.5
        _IntersectFoamSteps ("Foam Toon Steps", Range(1, 4)) = 2
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

        Pass
        {
            Name "WaterFront"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  waveVal     : TEXCOORD3;
                float4 screenPos   : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ShallowColor;
                float4 _SpecColor2;
                float  _WaveSpeed;
                float  _WaveScale;
                float  _WaveHeight;
                float  _ToonSteps;
                float  _SpecSize;
                float  _SpecSteps;
                float  _FresnelPower;
                float4 _FresnelColor;
                float  _CrestFoamThreshold;
                float4 _CrestFoamColor;
                float4 _IntersectFoamColor;
                float  _IntersectFoamWidth;
                float  _IntersectFoamNoiseScale;
                float  _IntersectFoamNoiseSpeed;
                float  _IntersectFoamSteps;
            CBUFFER_END

            // --- 노이즈 ---
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
                for (int idx = 0; idx < 3; idx++)
                {
                    v += a * noise2D(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            // --- 파도 ---
            float waveFunction(float3 worldPos)
            {
                float t = _Time.y * _WaveSpeed;
                float w = 0.0;
                w += sin(worldPos.x * _WaveScale + t * 1.1) * 0.5;
                w += sin((worldPos.x * 0.7 + worldPos.z * 0.7) * _WaveScale * 1.3 + t * 0.9) * 0.3;
                w += sin(worldPos.z * _WaveScale * 0.8 + t * 1.4) * 0.2;
                return w;
            }

            float3 waveNormal(float3 worldPos)
            {
                float eps = 0.05;
                float h  = waveFunction(worldPos);
                float hx = waveFunction(worldPos + float3(eps, 0, 0));
                float hz = waveFunction(worldPos + float3(0, 0, eps));
                float3 dx = float3(eps, (hx - h) * _WaveHeight, 0);
                float3 dz = float3(0, (hz - h) * _WaveHeight, eps);
                return normalize(cross(dz, dx));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float w = waveFunction(worldPos);
                IN.positionOS.y += w * _WaveHeight;

                OUT.positionCS  = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = waveNormal(OUT.worldPos);
                OUT.uv          = IN.uv;
                OUT.waveVal     = w;
                OUT.screenPos   = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - IN.worldPos);
                float3 normal  = normalize(IN.worldNormal);

                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);

                // === 디퓨즈: 툰 단계화 ===
                float NdotL = dot(normal, lightDir);
                float toon  = floor(NdotL * _ToonSteps) / _ToonSteps;
                toon = saturate(toon * 0.5 + 0.5);

                float depthMix = saturate(IN.waveVal * 0.5 + 0.5);
                float3 baseColor = lerp(_Color.rgb, _ShallowColor.rgb, depthMix);
                float3 diffuse = lerp(baseColor * 0.6, baseColor, toon);

                // === 스펙큘러: 툰 단계화 ===
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH    = max(0, dot(normal, halfDir));
                float spec     = pow(NdotH, _SpecSize);
                float toonSpec = floor(spec * _SpecSteps) / _SpecSteps;
                float3 specular = toonSpec * _SpecColor2.rgb;

                // === 프레넬 ===
                float fresnel = pow(1.0 - saturate(dot(viewDir, normal)), _FresnelPower);
                float3 fresnelCol = _FresnelColor.rgb * fresnel;

                // === 파도 마루 거품 ===
                float crestFoam = 0.0;
                if (IN.waveVal > _CrestFoamThreshold)
                {
                    crestFoam = saturate((IN.waveVal - _CrestFoamThreshold) / (1.0 - _CrestFoamThreshold));
                    crestFoam = step(0.5, crestFoam);
                }
                float3 crestFoamCol = _CrestFoamColor.rgb * crestFoam;

                // === 교차 거품 (뎁스 기반) ===
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // 씬 뎁스 샘플링
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                // 물 표면의 eye depth
                float waterEyeDepth = IN.screenPos.w;

                // 씬과 물 표면의 깊이 차이
                float depthDiff = sceneEyeDepth - waterEyeDepth;

                // 음수(물 뒤에 아무것도 없음)이거나 최소 임계값 이하이면 무시
                // → 카메라 회전 시 뎁스 지연으로 인한 오탐 방지
                float minThreshold = 0.05;
                float intersect = 0.0;
                if (depthDiff > minThreshold)
                {
                    // smoothstep으로 부드러운 감쇠 (경계 번쩍임 방지)
                    intersect = 1.0 - smoothstep(minThreshold, _IntersectFoamWidth, depthDiff);
                }

                // 노이즈로 거품 가장자리를 불규칙하게
                float2 foamNoiseUV = IN.worldPos.xz * _IntersectFoamNoiseScale
                                   + _Time.y * _IntersectFoamNoiseSpeed;
                float foamNoise = fbm2D(foamNoiseUV);

                // 노이즈를 교차 값에 곱해서 들쑥날쑥한 거품 형태
                float foamMask = intersect * lerp(0.5, 1.0, foamNoise);

                // 툰 단계화: 거품도 계단형으로 끊어짐
                foamMask = floor(foamMask * _IntersectFoamSteps) / _IntersectFoamSteps;

                float3 intersectFoamCol = _IntersectFoamColor.rgb * foamMask * _IntersectFoamColor.a;

                // === 최종 합성 ===
                float3 col = diffuse + specular + fresnelCol + crestFoamCol + intersectFoamCol;
                float alpha = _Color.a;

                // 거품이 있는 곳은 불투명하게
                alpha = max(alpha, max(crestFoam, foamMask) * 0.95);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
