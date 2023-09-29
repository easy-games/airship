Shader "Airship/CubemapPreviewShader" 
{
    Properties{
        _CubemapTex("Cubemap", Cube) = "black" {}
 
    }

        SubShader{
            Tags {"LightMode" = "AirshipForwardPass" "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
            //Cull Off ZWrite Off Fog { Mode Off }

                Pass {
                    CGPROGRAM
                    #pragma vertex vert
                    #pragma fragment frag
                    #include "UnityCG.cginc"

                    struct appdata {
                        float4 vertex : POSITION;
                    };

                    struct v2f {
                        float4 vertex : SV_POSITION;
                        float4 worldDirection : TEXCOORD0;
                        
                    };

                    samplerCUBE _CubemapTex;
                    v2f vert(appdata v) {
                        v2f o;
                        o.vertex = UnityObjectToClipPos(v.vertex);
                        o.worldDirection = float4(normalize(mul(unity_ObjectToWorld, v.vertex).xyz),0);

                    
                        return o;
                    }

                    float4 frag(v2f i) : SV_Target{
                         //no alpha, so it doesnt write to emissive
                        return float4(texCUBElod(_CubemapTex, i.worldDirection).xyz,0);
                        
                    }
                    ENDCG
                }
              
        }
       
    }
 