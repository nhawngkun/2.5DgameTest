Shader "NugPen/URPWaterShader"
{
    Properties
    {
        [Header(Water Colors)]
        _BaseColor("Shallow Water Color", Color) = (0.0, 0.7, 0.9, 0.6)
        _DeepColor("Deep Water Color", Color) = (0.0, 0.3, 0.6, 0.8)
        _FoamColor("Foam Color", Color) = (1.0, 1.0, 1.0, 0.9)
        _Opacity("Global Opacity", Range(0, 1)) = 0.7

        [Header(Wave Animation)]
        _WaveSpeed("Wave Speed", Float) = 1.0
        _WaveScale("Wave Scale/Frequency", Float) = 2.0
        _WaveStrength("Wave Strength/Height", Float) = 0.1

        [Header(Surface Ripples)]
        _NoiseScale("Noise Scale", Float) = 10.0
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.3
        _FoamThreshold("Foam Threshold", Range(0, 1)) = 0.65

        [Header(Rendering Settings)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10 // OneMinusSrcAlpha
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 0.0 // Off by default for transparent
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Culling", Float) = 0 // Off (Two-sided)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "ShaderModel" = "3.0"
        }
        LOD 100

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            Name "UnlitWater"

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float fogCoord : TEXCOORD3;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _DeepColor;
            half4 _FoamColor;
            half _Opacity;
            half _WaveSpeed;
            half _WaveScale;
            half _WaveStrength;
            half _NoiseScale;
            half _NoiseStrength;
            half _FoamThreshold;
            CBUFFER_END

            // Simple pseudo-random hash
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // 2D Value Noise
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(hash(i + float2(0.0, 0.0)), 
                                 hash(i + float2(1.0, 0.0)), u.x),
                            lerp(hash(i + float2(0.0, 1.0)), 
                                 hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Calculate displacement in world space to make waves uniform across objects
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                float3 worldPos = vertexInput.positionWS;

                // Vertex displacement waves (using sine/cosine based on world X and Z)
                float wave = sin(worldPos.x * _WaveScale + _Time.y * _WaveSpeed) * 
                             cos(worldPos.z * _WaveScale + _Time.y * _WaveSpeed) * 
                             _WaveStrength;

                // Move vertex along vertex normal in Object Space
                input.positionOS.xyz += input.normalOS * wave;

                // Recalculate position clip space with displaced position
                vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.worldPos = vertexInput.positionWS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Standard UVs
                float2 uv = input.uv;

                // Calculate scrolling noise layers to create moving ripples
                float2 uvNoise1 = uv * _NoiseScale + float2(_Time.y * _WaveSpeed * 0.05, _Time.y * _WaveSpeed * 0.02);
                float2 uvNoise2 = uv * _NoiseScale * 1.3 - float2(_Time.y * _WaveSpeed * 0.03, _Time.y * _WaveSpeed * 0.06);

                float n1 = valueNoise(uvNoise1);
                float n2 = valueNoise(uvNoise2);
                float combinedNoise = (n1 + n2) * 0.5;

                // Distortion vector based on noise to give organic look
                float2 distortion = float2(n1 - 0.5, n2 - 0.5) * _NoiseStrength * 0.2;
                float finalNoise = valueNoise(uv * _NoiseScale + distortion + float2(0.0, _Time.y * _WaveSpeed * 0.03));

                // Shallow vs Deep water lerp based on noise
                half4 waterColor = lerp(_BaseColor, _DeepColor, finalNoise);

                // Add foam details at higher noise thresholds
                half foamLerp = smoothstep(_FoamThreshold - 0.05, _FoamThreshold + 0.05, finalNoise);
                half4 finalColor = lerp(waterColor, _FoamColor, foamLerp);

                // Compute transparency
                half finalAlpha = waterColor.a * _Opacity;
                if (foamLerp > 0.0)
                {
                    // Foam increases opacity
                    finalAlpha = lerp(finalAlpha, _FoamColor.a * _Opacity, foamLerp);
                }

                // Apply Fog
                finalColor.rgb = MixFog(finalColor.rgb, input.fogCoord);

                return half4(finalColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
