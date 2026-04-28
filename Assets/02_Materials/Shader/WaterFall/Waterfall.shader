Shader "Custom/Waterfall"
{
    Properties
    {
        [Header(Base)]
        _Color ("Water Color", Color) = (0.2, 0.5, 0.7, 0.9)
        _Color2 ("Deep Color", Color) = (0.08, 0.25, 0.45, 0.95)
        _ToonSteps ("Toon Steps", Range(2, 6)) = 3

        [Header(Flow)]
        _FlowSpeed ("Flow Speed", Float) = 2.0
        _FlowNoiseScale ("Flow Noise Scale", Float) = 3.0
        _FlowDistortion ("Flow Distortion", Range(0, 0.3)) = 0.1

        [Header(Streaks)]
        _StreakScale ("Streak Scale", Float) = 8.0
        _StreakSharpness ("Streak Sharpness", Range(1, 20)) = 6.0
        _StreakColor ("Streak Color", Color) = (0.6, 0.85, 0.95, 1)
        _StreakSteps ("Streak Toon Steps", Range(1, 4)) = 2

        [Header(Edge Foam)]
        _EdgeFoamColor ("Edge Foam Color", Color) = (1, 1, 1, 0.9)
        _EdgeFoamWidth ("Edge Foam Width", Range(0.01, 2.0)) = 0.5
        _EdgeFoamNoiseScale ("Edge Foam Noise", Float) = 6.0

        [Header(Splash at Base)]
        _SplashFoamWidth ("Splash Foam Width", Range(0.01, 3.0)) = 1.2
        _SplashFoamColor ("Splash Foam Color", Color) = (1, 1, 1, 0.95)

        [Header(Specular)]
        _SpecColor2 ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecSize ("Specular Size", Range(1, 128)) = 48
        _SpecSteps ("Specular Steps", Range(1, 4)) = 2

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.0
        _FresnelColor ("Fresnel Tint", Color) = (0.7, 0.9, 1, 1)
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
            Name "WaterfallFront"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
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
                float4 screenPos   : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Color2;
                float  _ToonSteps;

                float  _FlowSpeed;
                float  _FlowNoiseScale;
                float  _FlowDistortion;

                float  _StreakScale;
                float  _StreakSharpness;
                float4 _StreakColor;
                float  _StreakSteps;

                float4 _EdgeFoamColor;
                float  _EdgeFoamWidth;
                float  _EdgeFoamNoiseScale;

                float  _SplashFoamWidth;
                float4 _SplashFoamColor;

                float4 _SpecColor2;
                float  _SpecSize;
                float  _SpecSteps;

                float  _FresnelPower;
                float4 _FresnelColor;
            CBUFFER_END

            //Noise
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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;


                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float flowOffset = _Time.y * _FlowSpeed;
                float n = fbm2D(float2(worldPos.x * _FlowNoiseScale, worldPos.y * _FlowNoiseScale - flowOffset));
                IN.positionOS.xyz += IN.normalOS * (n - 0.5) * 0.08;

                OUT.positionCS  = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                OUT.screenPos   = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - IN.worldPos);
                float3 normal  = normalize(IN.worldNormal);

                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);


                float flowOffset = _Time.y * _FlowSpeed;


                float2 flowUV1 = float2(IN.uv.x, IN.uv.y - flowOffset);
                float2 flowUV2 = float2(IN.uv.x * 1.2 + 0.3, IN.uv.y * 1.1 - flowOffset * 0.85);


                float distort1 = fbm2D(flowUV1 * _FlowNoiseScale);
                float distort2 = fbm2D(flowUV2 * _FlowNoiseScale);

                float2 distortedUV1 = flowUV1 + (distort1 - 0.5) * _FlowDistortion;
                float2 distortedUV2 = flowUV2 + (distort2 - 0.5) * _FlowDistortion;


                float n1 = fbm2D(distortedUV1 * 2.0);
                float n2 = fbm2D(distortedUV2 * 2.0);
                float blendNoise = (n1 + n2) * 0.5;

                float3 baseColor = lerp(_Color.rgb, _Color2.rgb, blendNoise);
                
                //Diffuse
                float NdotL = dot(normal, lightDir);
                float toon  = floor(NdotL * _ToonSteps) / _ToonSteps;
                toon = saturate(toon * 0.5 + 0.5);
                float3 diffuse = lerp(baseColor * 0.5, baseColor, toon);


                float streak1 = noise2D(float2(IN.uv.x * _StreakScale, distortedUV1.y * _StreakScale));
                float streak2 = noise2D(float2(IN.uv.x * _StreakScale * 1.3 + 0.5, distortedUV2.y * _StreakScale * 0.9));


                float streak = max(streak1, streak2);
                streak = pow(streak, _StreakSharpness);


                streak = floor(streak * _StreakSteps) / _StreakSteps;
                float3 streakCol = _StreakColor.rgb * streak;


                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH    = max(0, dot(normal, halfDir));
                float spec     = pow(NdotH, _SpecSize);
                float toonSpec = floor(spec * _SpecSteps) / _SpecSteps;
                float3 specular = toonSpec * _SpecColor2.rgb;


                float fresnel = pow(1.0 - saturate(dot(viewDir, normal)), _FresnelPower);
                float3 fresnelCol = _FresnelColor.rgb * fresnel;


                float edgeDist = min(IN.uv.x, 1.0 - IN.uv.x);
                float edgeNoise = fbm2D(float2(IN.uv.x * _EdgeFoamNoiseScale,
                                               distortedUV1.y * _EdgeFoamNoiseScale));
                float edgeFoam = 1.0 - smoothstep(0.0, _EdgeFoamWidth, edgeDist);
                edgeFoam *= lerp(0.4, 1.0, edgeNoise);
                edgeFoam = step(0.4, edgeFoam);
                float3 edgeFoamCol = _EdgeFoamColor.rgb * edgeFoam * _EdgeFoamColor.a;


                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float waterEyeDepth = IN.screenPos.w;
                float depthDiff = sceneEyeDepth - waterEyeDepth;

                float minThreshold = 0.05;
                float splash = 0.0;
                if (depthDiff > minThreshold)
                {
                    splash = 1.0 - smoothstep(minThreshold, _SplashFoamWidth, depthDiff);
                }

                float splashNoise = fbm2D(float2(IN.worldPos.x * 6.0 + _Time.y * 1.5,
                                                  IN.worldPos.y * 6.0 - flowOffset));
                splash *= lerp(0.5, 1.0, splashNoise);
                splash = step(0.3, splash);
                float3 splashCol = _SplashFoamColor.rgb * splash * _SplashFoamColor.a;


                float3 col = diffuse + streakCol + specular + fresnelCol + edgeFoamCol + splashCol;
                float alpha = _Color.a;
                alpha = max(alpha, max(edgeFoam, splash) * 0.95);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    //FallBack "Universal Render Pipeline/Lit"
}
