Shader "Lightship/Normals" {
	Properties {
		_AlphaAmount ("Alpha Amount", float) = 0.5
		_ColorAmount ("Color Amount", float) = 1.0
	}

    SubShader {

		  Pass {
			  Name "Normals"
			  ZWrite On
			  // ZTest LEqual

			  Tags
			  {
			    "Queue" = "Transparent"
			  }

			  Blend SrcAlpha OneMinusSrcAlpha

			  CGPROGRAM

        #pragma vertex vert
        #pragma fragment frag

        #include "UnityCG.cginc"

        struct appdata {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
        };

        struct v2f {
            float4 pos : POSITION;
            float4 color : COLOR;
        };

			  uniform float _AlphaAmount;
			  uniform float _ColorAmount;

			  v2f vert(appdata v) {
          v2f o;
          o.pos = UnityObjectToClipPos(v.vertex);
          o.color = float4(v.normal,1);
          return o;
			  }

			  half4 frag(v2f i) : COLOR {
				  float4 c = i.color*_ColorAmount;
			    c.a=_AlphaAmount;
				  return c;
			  }

			  ENDCG
		  }
	}
	Fallback "Diffuse"
}
