Shader "Universal Render Pipeline/2D/Card Rarity Outline"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _OutlineColor("Outline Color", Color) = (1,1,1,1)
        _OutlineThickness("Outline Thickness", Float) = 4
        _InteriorAlpha("Interior Alpha", Float) = 1
        _GlowColor("Glow Color", Color) = (1,1,1,0.5)
        _GlowRadius("Glow Radius", Float) = 8
        _GlowStrength("Glow Strength", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "Card Rarity Outline"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                half4   color       : COLOR;
                float2  uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                half  _OutlineThickness;
                half  _InteriorAlpha;
                half4 _GlowColor;
                half  _GlowRadius;
                half  _GlowStrength;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);

                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                o.color = v.color * _Color * unity_SpriteColor;
                return o;
            }

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                // Interior of the sprite (scaled by _InteriorAlpha so it can be hidden for a pure outline/glow pass)
                half4 final = tex;
                final.a *= _InteriorAlpha;

                float2 texel = _MainTex_TexelSize.xy;

                // ---- Hard outline around alpha edges ----
                float2 outlineStep = texel * _OutlineThickness;
                half maxAlpha = tex.a;

                maxAlpha = max(maxAlpha, SampleAlpha(i.uv + float2( outlineStep.x, 0)));
                maxAlpha = max(maxAlpha, SampleAlpha(i.uv + float2(-outlineStep.x, 0)));
                maxAlpha = max(maxAlpha, SampleAlpha(i.uv + float2(0,  outlineStep.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(i.uv + float2(0, -outlineStep.y)));

                maxAlpha = max(maxAlpha, SampleAlpha(i.uv + float2( outlineStep.x,  outlineStep.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(i.uv + float2(-outlineStep.x,  outlineStep.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(i.uv + float2( outlineStep.x, -outlineStep.y)));
                maxAlpha = max(maxAlpha, SampleAlpha(i.uv + float2(-outlineStep.x, -outlineStep.y)));

                if (tex.a < 0.5 && maxAlpha > 0.5)
                {
                    final = _OutlineColor;
                }

                // ---- Soft glow outside the shape ----
                half glow = 0;
                const int RINGS = 4;
                for (int ring = 1; ring <= RINGS; ring++)
                {
                    float t = (float)ring / (float)RINGS;
                    float2 glowStep = texel * _GlowRadius * t;

                    half ringAlpha = 0;
                    ringAlpha = max(ringAlpha, SampleAlpha(i.uv + float2( glowStep.x, 0)));
                    ringAlpha = max(ringAlpha, SampleAlpha(i.uv + float2(-glowStep.x, 0)));
                    ringAlpha = max(ringAlpha, SampleAlpha(i.uv + float2(0,  glowStep.y)));
                    ringAlpha = max(ringAlpha, SampleAlpha(i.uv + float2(0, -glowStep.y)));

                    ringAlpha = max(ringAlpha, SampleAlpha(i.uv + float2( glowStep.x,  glowStep.y)));
                    ringAlpha = max(ringAlpha, SampleAlpha(i.uv + float2(-glowStep.x,  glowStep.y)));
                    ringAlpha = max(ringAlpha, SampleAlpha(i.uv + float2( glowStep.x, -glowStep.y)));
                    ringAlpha = max(ringAlpha, SampleAlpha(i.uv + float2(-glowStep.x, -glowStep.y)));

                    glow += ringAlpha * (1.0 - t * 0.5);
                }

                // Only show glow on transparent pixels (outside the card shape)
                glow = saturate(glow * _GlowStrength * (1.0 - tex.a));

                half4 glowCol = _GlowColor;
                glowCol.a *= glow;

                final.rgb = final.rgb + glowCol.rgb;
                final.a = max(final.a, glowCol.a);

                return final;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
