Shader "Custom/SpriteShadowCaster"
{
    Properties
    {
        _MainTex        ("Sprite Texture", 2D)              = "white" {}
        _Cutoff         ("Alpha Cutoff",   Range(0.01,1.0)) = 0.25

        [Header(Shadow Quality)]
        _ShadowSoftness ("Shadow Softness", Range(0.0, 1.0)) = 0.5
        // Kéo vertices shadow ra ngoài 1 chút -> cạnh mềm hơn
        _ShadowSpread   ("Shadow Spread",   Range(0.0, 0.05)) = 0.008
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Geometry"
            "RenderType"     = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ══════════════════════════════════════════════════
        // PASS 1 – Render sprite, màu gốc, không self-shadow
        // ══════════════════════════════════════════════════
        Pass
        {
            Name "SpriteForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Cutoff;
                float  _ShadowSoftness;
                float  _ShadowSpread;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Vary { float4 hcs : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Vary vert(Attr IN)
            {
                Vary OUT;
                OUT.hcs   = TransformObjectToHClip(IN.pos.xyz);
                OUT.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Vary IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(col.a - _Cutoff);
                return col * IN.color;
            }
            ENDHLSL
        }

        // ══════════════════════════════════════════════════
        // PASS 2 – ShadowCaster
        //   _ShadowSpread: phình nhẹ mesh shadow ra ngoài
        //   -> cạnh bóng mềm hơn, tự nhiên hơn
        //   -> phối hợp với Soft Shadow của URP
        // ══════════════════════════════════════════════════
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite    On
            ZTest     LEqual
            ColorMask 0
            Cull      Off

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
                float  _ShadowSoftness;
                float  _ShadowSpread;
            CBUFFER_END

            float3 _LightDirection;

            struct SAttr { float4 pos : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; };
            struct SVary { float4 hcs : SV_POSITION; float2 uv : TEXCOORD0; };

            SVary ShadowVert(SAttr IN)
            {
                SVary OUT;
                float3 wsPos    = TransformObjectToWorld(IN.pos.xyz);
                float3 wsNormal = TransformObjectToWorldNormal(IN.normal);

                // Phình shadow spread theo normal -> cạnh bóng mềm hơn
                wsPos += wsNormal * _ShadowSpread;

                float3 biased = ApplyShadowBias(wsPos, wsNormal, _LightDirection);
                OUT.hcs = TransformWorldToHClip(biased);
                OUT.uv  = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 ShadowFrag(SVary IN) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

                // Fade alpha ra cạnh biên -> bóng không bị cứng đột ngột
                // Vùng alpha gần ngưỡng cutoff sẽ mờ dần
                float edge = smoothstep(_Cutoff - 0.15, _Cutoff + 0.05, a);
                clip(edge - 0.01);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
