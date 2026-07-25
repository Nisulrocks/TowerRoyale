Shader "Universal Render Pipeline/2D/Sprite Outline"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _OutlineColor("Outline Color", Color) = (1,1,1,1)
        _OutlineThickness("Outline Thickness", Float) = 2
        _OutlineEnabled("Outline Enabled", Float) = 1
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
            Name "Sprite Outline"
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
                half  _OutlineEnabled;
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

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                if (_OutlineEnabled <= 0.0)
                    return tex;

                float2 texel = _MainTex_TexelSize.xy * _OutlineThickness;
                half alpha = tex.a;

                alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x, 0)).a);
                alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x, 0)).a);
                alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0,  texel.y)).a);
                alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, -texel.y)).a);

                alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x,  texel.y)).a);
                alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x,  texel.y)).a);
                alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2( texel.x, -texel.y)).a);
                alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-texel.x, -texel.y)).a);

                if (tex.a < 0.5 && alpha > 0.5)
                    return _OutlineColor;

                return tex;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
