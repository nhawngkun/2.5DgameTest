Shader "Custom/SpriteShadowOnly"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Cutoff  ("Alpha Cutoff", Range(0.0, 1.0)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Geometry"
            "RenderType"      = "TransparentCutout"
            "RenderPipeline"  = "UniversalPipeline"
        }

        // ─────────────────────────────────────────────────────────────────
        // Pass 1: Render sprite bình thường, nhận bóng từ vật khác
        //         KHÔNG xoay, KHÔNG đổi gì hình dạng sprite
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "SpriteLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Cutoff;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 hcs : SV_POSITION; float2 uv : TEXCOORD0; float3 wsPos : TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Không xoay gì cả – dùng nguyên positionOS
                OUT.wsPos = TransformObjectToWorld(IN.pos.xyz);
                OUT.hcs   = TransformWorldToHClip(OUT.wsPos);
                OUT.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(col.a - _Cutoff);
                return col;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────
        // Pass 2: ShadowCaster – chỉ đổ bóng đúng hình alpha sprite
        //         Bóng = ĐEN, không có texture, không có màu
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Cutoff;
            CBUFFER_END

            float3 _LightDirection;

            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; };
            struct Vary { float4 hcs : SV_POSITION; float2 uv  : TEXCOORD0; };

            Vary ShadowVert(Attr IN)
            {
                Vary OUT;
                float3 wsPos    = TransformObjectToWorld(IN.pos.xyz);
                float3 wsNormal = TransformObjectToWorldNormal(IN.normal);
                // ApplyShadowBias giúp tránh shadow acne
                float3 biased   = ApplyShadowBias(wsPos, wsNormal, _LightDirection);
                OUT.hcs = TransformWorldToHClip(biased);
                OUT.uv  = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 ShadowFrag(Vary IN) : SV_Target
            {
                // Chỉ cắt alpha – bóng hoàn toàn không có texture/màu
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(a - _Cutoff);
                return 0; // output rỗng – shadow map chỉ cần depth
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────
        // Pass 3: DepthNormals (SSAO / depth prepass)
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex   DN_Vert
            #pragma fragment DN_Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Cutoff;
            CBUFFER_END

            struct DNAttr { float4 pos : POSITION; float2 uv : TEXCOORD0; float3 n : NORMAL; };
            struct DNVary { float4 hcs : SV_POSITION; float2 uv : TEXCOORD0; float3 nWS : TEXCOORD1; };

            DNVary DN_Vert(DNAttr IN)
            {
                DNVary OUT;
                OUT.hcs = TransformObjectToHClip(IN.pos.xyz);
                OUT.uv  = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.nWS = TransformObjectToWorldNormal(IN.n);
                return OUT;
            }

            float4 DN_Frag(DNVary IN) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a - _Cutoff);
                return float4(normalize(IN.nWS) * 0.5 + 0.5, 1);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
