Shader "Hidden/UnpackDepth"
{
    Properties
    {
        _MainTex ("Depth Texture", 2DArray) = "" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // This shader is created to copy the native depth texture
            // to a Unity texture on the Meta Quest 3, which provides
            // a depth texture in the form of a 2D array.
            UNITY_DECLARE_TEX2DARRAY(_MainTex);

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 uvw = float3(i.uv.x, 1.0 - i.uv.y, 0); // vertical flip
                float depth = UNITY_SAMPLE_TEX2DARRAY(_MainTex, uvw).r;

                // Convert sampled depth [0,1] to NDC depth [-1,1] and then reconstruct
                // view-space distance using a perspective projection with near plane = 0.2
                float ndcDepth = depth * 2.0 - 1.0;
                float linearDepth = -0.2 / (ndcDepth - 1.0);
                return float4(linearDepth, 0.0, 0.0, 1.0);
            }

            ENDCG
        }
    }

    FallBack Off
}
