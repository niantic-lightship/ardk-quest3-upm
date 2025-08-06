Shader "Lightship/OcclusionMeshStereo"
{
    Properties
    {
        _ColorMask ("Color Mask", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Offset 1, 1
            ColorMask [_ColorMask]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ STEREO_DEPTH
            #pragma multi_compile _ NON_LINEAR_DEPTH

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                uint vid : SV_VertexID;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

#if defined(STEREO_DEPTH)
            UNITY_DECLARE_TEX2DARRAY(_DepthTexture);
#else
            sampler2D _DepthTexture;
#endif

            float4 _NdcToLinearDepthParams;
            float4 _Intrinsics[2];
            float4x4 _Extrinsics[2];

            int _ImageWidth;
            int _ImageHeight;
            float _UnityCameraForwardScale;

            inline float4 WorldToClipPos(float3 posWorld)
            {
              float4 clipPos;
              #if defined(STEREO_CUBEMAP_RENDER_ON) || defined(UNITY_SINGLE_PASS_STEREO)
                float3 offset = ODSOffset(posWorld, unity_HalfStereoSeparation.x);
                clipPos = mul(UNITY_MATRIX_VP, float4(posWorld + offset, 1.0));
              #else
                clipPos = mul(UNITY_MATRIX_VP, float4(posWorld, 1.0));
              #endif
              return clipPos;
            }

            inline float ScaleEyeDepth(float d)
            {
                d = _UnityCameraForwardScale > 0.0 ? _UnityCameraForwardScale * d : d;
                return d < _ProjectionParams.y ? 0.0f : d;
            }

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Get the pixel coordinates for the vertex
                uint2 pixel = uint2(v.vid % (uint)_ImageWidth, v.vid / (uint)_ImageHeight);

                // Sample the depth texture
#if defined(STEREO_DEPTH)
                float3 uv = float3(pixel.x / (float)_ImageWidth, pixel.y / (float)_ImageHeight, (float)unity_StereoEyeIndex);
                float depth = UNITY_SAMPLE_TEX2DARRAY_LOD(_DepthTexture, uv, 0).r;
#else
                float2 uv = float2(pixel.x / (float)_ImageWidth, pixel.y / (float)_ImageHeight);
                float depth = tex2Dlod(_DepthTexture, float4(uv.x, 1.0f - uv.y, 0, 0)).r;
#endif

                // Linearize the depth value
#if defined(NON_LINEAR_DEPTH)
                float ndcDepth = depth * 2.0 - 1.0;
                float linearDepth = _NdcToLinearDepthParams.x / (ndcDepth + _NdcToLinearDepthParams.y);
#else
                float linearDepth = depth;
#endif

                // Convert from image space to view space
                float4 viewPosition;
                float4 intrinsics = _Intrinsics[unity_StereoEyeIndex];
                viewPosition.x = (pixel.x - intrinsics.z) * linearDepth / intrinsics.x;
                viewPosition.y = (pixel.y - intrinsics.w) * linearDepth / intrinsics.y;
                viewPosition.z = -ScaleEyeDepth(linearDepth);
                viewPosition.w = 1.0f;

                // Transform to world space
                float4 worldPosition = mul(_Extrinsics[unity_StereoEyeIndex], viewPosition);

                // Transform to clip space
                o.pos = WorldToClipPos(worldPosition.xyz);

                // Set colors to debug UV coordinates
                o.color.x = uv.x;
                o.color.z = uv.y;

                // Set colors to debug eye depth
                const float max_view_disp = 1.0f;
                o.color.y = (1.0f / linearDepth) / max_view_disp;

                // For debug visualization, we need opaque
                o.color.w = 1.0f;

                return o;
            }

            half4 frag(v2f i) : COLOR
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Use the input color from the vertex, in the event we're using debug visualization.
                return i.color;
            }

            ENDCG
        }
    }
}
