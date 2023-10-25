Shader "Airship/AirshipSpriteSimple"
{
    Properties
    {
        _Color("Tint", Color) = (1,1,1,1)
        _Emissive("Emissive", Range(0,1)) = 1
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
        Name "Forward"
        Tags { "LightMode" = "AirshipForwardPass" 
            "Queue" = "Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"}
            
        //Tags { "RenderType"="Transparent"  "LightMode" = "AirshipForwardPass"  "Queue"="Transparent"}

		Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "../AirshipShaderIncludes.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Emissive;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = SRGBtoLinear(v.color);
                return o;
            }

            fixed4 frag (v2f i, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1) : SV_Target2
            {
                float4 texColor = tex2D(_MainTex, i.uv);
                float4 finalColor = texColor * SRGBtoLinear(_Color) * i.color;
                clip(texColor.a-.1);
                //finalColor.a =texColor.a;
				MRT0 = finalColor;
				MRT1 = _Emissive * finalColor;
                return finalColor;
            }
            ENDCG
        }
    }
}
