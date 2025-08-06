Shader "Lightship/Unlit/DrawImageYUV"
{
    Properties
    {
        // Retrievable properties
        _TextureY ("Luma", 2D) = "white" {}
        _TextureUV ("Chroma", 2D) = "gray" {}

        // To be able to use with the RawImage component
        _MainTex ("Image", 2D) = "black" {}
    }

    // URP SubShader
    SubShader
    {
      PackageRequirements
        {
            "com.unity.render-pipelines.universal": "12.0"
        }

        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "ForceNoShadowCasting" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Cull Off
            ZTest Always
            ZWrite Off
            Lighting Off
            LOD 100
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float3 position : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _TextureY;
            sampler2D _TextureUV;
            float4x4 _DisplayMatrix;

            inline half3 GammaToLinearSpace (half3 sRGB)
            {
                // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
                return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.position = TransformObjectToHClip(v.position);
                o.texcoord = mul(float4(v.texcoord, 1.0f, 1.0f), _DisplayMatrix).xy;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Sample the y texture
                float y = tex2D(_TextureY, i.texcoord).r;

                // Sample the uv texture (interpret NV21 VU)
                float v = tex2D(_TextureUV, i.texcoord).r - 0.5f;
                float u = tex2D(_TextureUV, i.texcoord).g - 0.5f;

                // BT.601 conversion
                half r = y + 1.403h * v;
                half g = y - 0.344h * u - 0.714h * v;
                half b = y + 1.770h * u;
                half3 rgb = half3(r, g, b);

                // Convert from sRGB to RGB in linear color space
                #if !UNITY_COLORSPACE_GAMMA
                rgb = GammaToLinearSpace(rgb);
                #endif

                return half4(rgb, 1.0f);
            }

            ENDHLSL
        }
    }

    // Built-in Render Pipeline SubShader
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Cull Off
            ZTest Always
            ZWrite Off
            Lighting Off
            LOD 100
            Tags
            {
                "LightMode" = "Always"
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float3 position : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4x4 _DisplayMatrix;
            sampler2D _TextureY;
            sampler2D _TextureUV;

            v2f vert (appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos (v.position);
                o.uv = mul(float4(v.texcoord, 1.0f, 1.0f), _DisplayMatrix).xy;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Sample the y texture
                float y = tex2D(_TextureY, i.uv).r;

                // Sample the uv texture (interpret NV21 VU)
                float v = tex2D(_TextureUV, i.uv).r - 0.5f;
                float u = tex2D(_TextureUV, i.uv).g - 0.5f;

                // BT.601 conversion
                half r = y + 1.403h * v;
                half g = y - 0.344h * u - 0.714h * v;
                half b = y + 1.770h * u;
                half3 rgb = half3(r, g, b);

                // Convert from sRGB to RGB in linear color space
                #if !UNITY_COLORSPACE_GAMMA
                rgb = GammaToLinearSpace(rgb);
                #endif

                return half4(rgb, 1.0f);
            }

            ENDHLSL
        }
    }
}
