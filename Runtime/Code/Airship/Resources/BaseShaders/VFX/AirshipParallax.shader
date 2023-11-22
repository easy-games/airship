Shader "Airship/AirshipParallax"
{
    Properties
    {
        _ColorFG("Foreground Color", Color) = (0,1,1,.5)
        _ColorBG("Background Color", Color) = (1,0,1,1)
        _Emissive("Emissive", Range(0,1)) = 1
        _MainTexBG ("Background Texture", 2D) = "white" {}
        _MainTexFG ("Foreground Texture", 2D) = "black" {}
        _FresnelStrength("Fresnel Strength", Range(0,5)) = 1
        _FresnelPower("Fresnel Power", Range(0,20)) = 1
        _ParallaxScale("Parallax Scale", Float) = 1
        _TexScrollSpeedX ("Parallax Scroll X", Float) = 0
        _TexScrollSpeedY ("Parallax Scroll Y", Float) = 0
    }
    SubShader
    {
        // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
        Name "Forward"
        Tags { "LightMode" = "AirshipForwardPass" 
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "IgnoreProjector"="True"}
            
        //Tags { "RenderType"="Transparent"  "LightMode" = "AirshipForwardPass"  "Queue"="Transparent"}

		Cull front
        Lighting On
        ZWrite On
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "../AirshipShaderIncludes.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertexPos : SV_POSITION;
                float4 vectexPosScreenspace: TEXCOORD1;
                fixed4 color : COLOR;
                float3 worldPos: TEXCOORD2;
                float3 normal : TEXCOORD3;
                float3 viewDir : TEXCOORD4;
            };

            sampler2D _MainTexBG;
            sampler2D _MainTexFG;
            float4 _MainTexFG_ST;
            float4 _ColorFG;
            float4 _ColorBG;
            float4 _BGColor;
            float _Emissive;
            float _ParallaxScale;
            float _FresnelStrength;
            float _FresnelPower;
            float _TexScrollSpeedX;
            float _TexScrollSpeedY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertexPos = UnityObjectToClipPos(v.vertex);
                o.vectexPosScreenspace = ComputeScreenPos(o.vertexPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTexFG);
                o.color = SRGBtoLinear(v.color);
                o.normal = normalize(mul(float4(v.normal, 0.0), unity_WorldToObject).xyz);
                o.viewDir = WorldSpaceViewDir(mul(unity_ObjectToWorld, v.vertex));
                return o;
            }

            fixed4 frag (v2f i, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1) : SV_Target2
            {
                float2 uvTimeOffset = _Time.x * float2(_TexScrollSpeedX, _TexScrollSpeedY);
                float4 fgTexColor = tex2D(_MainTexFG, i.uv + uvTimeOffset * -.5);
                float2 screenUV = i.vectexPosScreenspace.xy / i.vectexPosScreenspace.w;
                float4 bgTexColor = tex2D(_MainTexBG, screenUV * _ParallaxScale + uvTimeOffset);
                float2 convertedUV = float2(ConvertFromNormalizedRange(i.uv.y), ConvertFromNormalizedRange(i.uv.x));
                float4 fgColor = SRGBtoLinear(_ColorFG);
                float4 bgColor = SRGBtoLinear(_ColorBG);

                float fresnel = RimLightDelta(i.normal, i.viewDir, _FresnelPower, _FresnelStrength);
                float4 fgFinal = fgColor * fgTexColor * fresnel;

                float4 bgFinal = bgColor * bgTexColor;

                float4 finalColor = fgFinal * fgColor.a + bgFinal;
                
				MRT0 = finalColor;
				MRT1 = _Emissive * finalColor;// * i.uv.y;
                return finalColor;
            }
            ENDCG
        }
    }
}
