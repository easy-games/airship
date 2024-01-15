Shader "Airship/Skybox" 
{
    Properties{
        _CubemapTex("Cubemap", Cube) = "black" {}
        [HDR] _FogColor("Fog Color", Color) = (1,1,1,1)
        _FogSize("Fog Size", Float) = 3.5
        _FogPower("Fog Pow", Float) = 1
    }

        SubShader{
            Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
            Cull Off ZWrite Off Fog { Mode Off }

                Pass {
                    CGPROGRAM
                    #pragma vertex vert
                    #pragma fragment frag
                    #include "UnityCG.cginc"

					float _FogSize;
		            float4 _FogColor;   
                    float _FogPower;

                    struct appdata {
                        float4 vertex : POSITION;
                    };

                    struct v2f {
                        float4 vertex : SV_POSITION;
                        float4 worldDirection : TEXCOORD0;
                        float4 worldPos : TEXCOORD1;
                    };

                    samplerCUBE _CubemapTex;
                    v2f vert(appdata v) 
                    {
                        v2f o;
                        o.vertex = UnityObjectToClipPos(v.vertex);
                        o.worldDirection = float4(normalize(mul(unity_ObjectToWorld, v.vertex).xyz),0);
						o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                  

                        return o;
                    }

                    float4 frag(v2f i) : SV_Target
                    {
                        half3 viewVector = _WorldSpaceCameraPos.xyz - i.worldPos;
                        float viewDistance = length(viewVector);
                        half3 viewDirection = normalize(viewVector);
                        
                        //if the view vector is basically level with the horizon, blend in fog color
                        float fogPower = pow(abs(viewDirection.y), _FogPower);
						float fogBlend = saturate(fogPower * _FogSize);
						half3 fogColor = lerp(_FogColor.rgb, texCUBE(_CubemapTex, -viewDirection).rgb, fogBlend);
                        
						return float4(fogColor, 0);
                        

                        //return float4(texCUBElod(_CubemapTex, i.worldDirection).xyz,0);
                    }
                    ENDCG
                }
              
        }
       
    }
 