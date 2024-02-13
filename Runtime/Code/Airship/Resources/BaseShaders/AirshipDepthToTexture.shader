//Very raw shader that really just makes sure a fragment gets emitted for the shadowmap
Shader "Airship/DepthToTexture"
{
    SubShader
    {
        Pass 
        {
            Tags 
            { 
                "RenderType" = "Opaque" 
                "LightMode" = "AirshipShadowPass" 
            }
            ZWrite On
            CGPROGRAM
        
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
    
            struct v2f 
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata_base v) 
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(0,0,0,0);
            }
            ENDCG
        }
    }
}