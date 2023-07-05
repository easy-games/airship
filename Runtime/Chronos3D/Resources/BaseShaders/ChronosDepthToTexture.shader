
Shader "Chronos/DepthToTexture"
{
    SubShader{
           Tags { "RenderType" = "Opaque" }
           Pass {
               CGPROGRAM

               #pragma vertex vert
               #pragma fragment frag
               #include "UnityCG.cginc"

               struct v2f {
                   float4 pos : SV_POSITION;
                   float depth : TEXCOORD0;
               };

               v2f vert(appdata_base v) {
                   v2f o;
                   o.pos = UnityObjectToClipPos(v.vertex);
                   float4 clipPos = UnityObjectToClipPos(v.vertex);
                   o.depth = clipPos.z / clipPos.w;
                   return o;
               }

               half4 frag(v2f i) : SV_Target{
                   //UNITY_OUTPUT_DEPTH(i.depth);
                   return half4(i.depth.x,0,0,0);
               }
               ENDCG
           }
    }
}