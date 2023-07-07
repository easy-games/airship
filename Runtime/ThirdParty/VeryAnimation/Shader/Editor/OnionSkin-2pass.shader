Shader "Very Animation/OnionSkin-2pass" {
	Properties{
		_Color("Main Color", Color) = (1, 1, 1, 1)
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
		_AlphaTest("Alpha Test", Range(0.0, 1.0)) = 0.999
}

SubShader {
	Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="Transparent" "ForceNoShadowCasting" = "True"}
	LOD 100

	Lighting Off
	Offset 0, 3
	Cull Off

	Pass {  
		ZWrite On
		ColorMask 0

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
			};
			
			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _AlphaTest;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.texcoord);
			    clip(col.a - _AlphaTest);
				return col;
			}
		ENDCG
	}

	Pass {  
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float3 normal : NORMAL;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;
			float _AlphaTest;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.texcoord);
			    clip(col.a - _AlphaTest);
				col *= _Color; 
				
				float3 cameraForward = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
				float alpha = 1 - (abs(dot(cameraForward, i.worldNormal)));
				col.a *= alpha;
				
				return col;
			}
		ENDCG
	}
}

}
