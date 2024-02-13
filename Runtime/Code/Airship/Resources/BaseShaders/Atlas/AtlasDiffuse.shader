Shader "Airship/AtlasDiffuse" {
    
    Properties{
        _MainTex("Texture", 2D) = "white" {}
        _NormalMap("Texture", 2D) = "white" {}
        _RoughnessMap("Texture", 2D) = "white" {}
        _MetalMap("Texture", 2D) = "black" {}
        _EmissiveMap("Texture", 2D) = "black" {}

        _Brightness("Brightness", Float) = 1
        _FlipHorizontal("Flip Horizontal", Float) = 0
        _FlipVertical("Flip Vertical", Float) = 0
    }

        SubShader{
            Tags { "RenderType" = "Opaque" }

            Cull Off

            Pass {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                struct appdata {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                };

                Texture2D _MainTex;
                Texture2D _NormalMap;
                Texture2D _RoughnessMap;
				Texture2D _MetalMap;
                Texture2D _EmissiveMap;

                SamplerState my_sampler_point_repeat;

                float _FlipHorizontal;
                float _FlipVertical;
                float _Brightness;

                float _Emissive;
                float _Metallic;
                float _Roughness;

                v2f vert(appdata v) {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;

                    //Flip the vertex position on Y
                    o.vertex.y *= -1;

                    //Flip the UVs on Y too
					o.uv.y = 1 - o.uv.y;

                    return o;
                }
                       

                void frag(v2f i, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
                {
                    float2 uv = i.uv;
                    uv.x = lerp(uv.x, 1 - uv.x, _FlipHorizontal);
                    uv.y = lerp(uv.y, 1 - uv.y, _FlipVertical);
                    
                    //Herp derp derp easy case
                    float4 textureSample = _MainTex.Sample(my_sampler_point_repeat, uv);
					textureSample.rgb *= _Brightness;

                    MRT0 = textureSample;

                    //Not so easy case
                    half4 packed = half4(0, 0, 0, 0);

                    half4 normalSample = _NormalMap.Sample(my_sampler_point_repeat, uv);
                    half3 normal;
                    
                    
                    normal = UnpackNormalmapRGorAG(normalSample);
                    
					half metal = _MetalMap.Sample(my_sampler_point_repeat, uv).r;
					half emissive = _EmissiveMap.Sample(my_sampler_point_repeat, uv).r;
                    half roughness = _RoughnessMap.Sample(my_sampler_point_repeat, uv);

                    if (_Metallic > -1) {
                        metal = _Metallic;
                    }
                    if (_Emissive > -1) {
                        emissive = _Emissive;
                    }
                    if (_Roughness > -1) {
                        roughness = _Roughness;
                    }


                    half metalAndEmissive = metal / 2;
					if (emissive > 0) 
                    {
                        metalAndEmissive += 0.51;
					}
					 
                  
                    packed.r = normal.x * 0.5 + 0.5;
                    packed.g = normal.y * 0.5 + 0.5;
                    packed.b = metalAndEmissive;
                    packed.a = roughness;

                    
                    MRT1 = packed;
                }
                ENDCG
            }
        }
    }
 