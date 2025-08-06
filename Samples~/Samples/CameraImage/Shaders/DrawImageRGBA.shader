Shader "Lightship/Unlit/DrawImageRGBA"
{
    Properties
    {
        // Retrievable properties
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

            float4x4 _DisplayMatrix;
            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.position = TransformObjectToHClip(v.position);
                o.texcoord = mul(float4(v.texcoord, 1.0f, 1.0f), _DisplayMatrix).xy;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
              return tex2D(_MainTex, i.texcoord);
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
            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos (v.position);
                o.uv = mul(float4(v.texcoord, 1.0f, 1.0f), _DisplayMatrix).xy;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }

            ENDHLSL
        }
    }
}
