//Shader for grass etc

Shader "Chronos/FoliageShader2"
{
    
    Properties
    {
        [Header(Coloring)]
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        [HDR] _DarkColor("Dark Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
        
        //lightmix range from 0..1
		_LightMix("Light Mix", Range(0,1)) = 0.5

        [Header(Deformation)]
        _DeformSpeed("Global Speed", Range(0,1)) = 1
        _DeformStrength("Global Strength", Range(0,1)) = 1
        _WindSpeed("Wind Speed", Float) = 3
        _WindStrength("Wind Strength", Float) = 1
        _FlutterSpeed("Flutter Speed", Float) = 1
        _FlutterStrength("Flutter Strength", Float) = 1
        
        [Header(Chronos)]
        [Toggle] EXPLICIT_MAPS_ON("Use Normal/Metal/Rough Maps", Float) = 1.0
        _Alpha("Dither Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Cull off
            
        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Name "Forward"
            Tags { "LightMode" = "ChronosForwardPass" "Queue" = "Opaque"}

            //Blend[_SrcBlend][_DstBlend]
            ZWrite On
            Cull 	Off
 
            HLSLPROGRAM
            
            //Multi shader vars
            #pragma vertex vertFunction
            #pragma fragment fragFunction
            #pragma shader_feature EXPLICIT_MAPS_ON
			
			#include "UnityCG.cginc"
            #include "Packages/gg.easy.airship/Runtime/Code/Chronos3D/Resources/BaseShaders/ChronosShaderIncludes.cginc"
            
            SamplerState my_sampler_point_repeat;
            Texture2D _MainTex;
            Texture2D _DitherTexture;
            float4 _DitherTexture_TexelSize;

            float4 _MainTex_TexelSize;

            float _LightMix;
            
            //Deformation
            float _DeformSpeed;
            float _DeformStrength;
            float _WindSpeed;
            float _WindStrength;
            float _FlutterSpeed;
            float _FlutterStrength;
            
            float4 _Color;
            float4 _DarkColor;
            float4 _MainTex_ST;
            half _Alpha = 1;
            half globalAmbientOcclusion = 0;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color   : COLOR;
                float3 normal : NORMAL;
                float4 tangent: TANGENT;
                float4 uv_MainTex : TEXCOORD0;
            };

            struct vertToFrag
            {
                float4 positionCS : SV_POSITION;
                float4 color    : COLOR;
                float4 uv_MainTex : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                half3 worldNormal : TEXCOORD6;
            };

            //Make this vertex flap around in the wind using sin and cos
            float4 Wind(float4 input, float scale)
            {
                
                scale = (1 - scale) * _DeformStrength;
                
                float flutterPhase = _Time.x * _FlutterSpeed * _DeformSpeed;
                float windPhase = _Time.x * _WindSpeed * _DeformSpeed;
                 
                //global wind flows
                float stretch = 0.25;
                float flutterDelta = clamp(1+ (1+sin(input.x * stretch))*0.5,0,1);

                 //Flutter - Subtle motion
                //use a sin circle to move this vertex
                input.x += (sin(flutterPhase + input.x * 200 + input.z * 100) * 0.1) * (flutterDelta * scale * _FlutterStrength);
                input.y += (cos(flutterPhase + input.x * 150 + input.z * 100) * 0.1) * (flutterDelta * scale * _FlutterStrength);
                input.z += (cos(flutterPhase + input.x *  50 + input.z * 50) * 0.1) * (flutterDelta * scale * _FlutterStrength);

                //Wind - major wind motion (offset around a point+ sin)
                float windMotion = 0.1 * _WindStrength;
                input.x += (-windMotion*0.5) + (sin((windPhase*4) + (input.x * 0.3) ) * windMotion) * scale;

                return input;
            }
          
            half3 GrassColor(float lerpVal, half3 colora, half3 colorb)
            {
                
                half3 color = lerp(colora,colorb, lerpVal);
				//half3 color = lerp(_Color, _DarkColor, lerpVal);
                return color;
            }

            vertToFrag vertFunction(Attributes input)
            {
                vertToFrag output;
                                
                float4 originalWorldPos = mul(unity_ObjectToWorld, input.positionOS);
                
                
                float4 worldPos = Wind(originalWorldPos, 1 - input.uv_MainTex.y);
                float delta = length(worldPos - originalWorldPos) % 1;
                delta = 0;
                    
                output.positionCS = mul(unity_MatrixVP, worldPos);
                output.uv_MainTex = input.uv_MainTex;
                
                output.uv_MainTex = float4((input.uv_MainTex * _MainTex_ST.xy + _MainTex_ST.zw).xy,1,1);
          
                output.worldPos = worldPos;
                float4 lighting = half4(max(input.color.rrr, SampleAmbientSphericalHarmonics(half3(0, 1, 0))), 1);
                float4 bright = _Color - (float4(1,1,1,1) * delta * 1.1);
                float4 dark = _DarkColor;
                output.color.rgb = GrassColor(saturate( 1-input.uv_MainTex.y), bright, (dark));

                //output.color.g = clamp(output.color.g + (1-globalAmbientOcclusion), 0, 1);
                output.worldNormal =  UnityObjectToWorldNormal(input.normal);

                return output;
            }

            half3 DecodeNormal(half3 norm)
            {
                return norm * 2.0 - 1.0;
            }
 
            half3 EncodeNormal(half3 norm)
            {
                return norm * 0.5 + 0.5;
            }

            //Two channel packed normals (assumes never negative z)
            half3 TextureDecodeNormal(half3 norm)
            {
                half3 n;
                n.xy = norm.xy * 2 - 1;
                n.z = sqrt(1 - dot(n.xy, n.xy));
                return n;
            }
        

            half3 EnvBRDFApprox(half3 SpecularColor, half Roughness, half NoV)
            {
             
                const half4 c0 = { -1, -0.0275, -0.572, 0.022 };
                const half4 c1 = { 1, 0.0425, 1.04, -0.04 };
                half4 r = Roughness * c0 + c1;
                half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
                half2 AB = half2(-1.04, 1.04) * a004 + r.zw;
                return SpecularColor * AB.x + AB.y;
            }

            half EnvBRDFApproxNonmetal(half Roughness, half NoV)
            {
                // Same as EnvBRDFApprox( 0.04, Roughness, NoV )
                const half2 c0 = { -1, -0.0275 };
                const half2 c1 = { 1, 0.0425 };
                half2 r = Roughness * c0 + c1;
                return min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
            }
            
            half3 ProcessReflectionSample(half3 img)
            {
                return (img * img) * 2;
            }
     
            struct Coordinates
            {
                half2 ddx;
                half2 ddy;
                half lod;
                half2 uvs;
            };	 
              
            void fragFunction(vertToFrag input, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
            {

                half4 texSample = _MainTex.Sample(my_sampler_point_repeat, input.uv_MainTex.xy);
                
                if (texSample.r < 0.5)
                {
                    discard;
                }

                float2 screenPos = (input.positionCS.xy * 0.5 + 0.5) * _ScreenParams.xy;
                //screenPos.xy = floor(screenPos.xy * 0.25) * 0.5;
                //float checker = -frac(screenPos.r + screenPos.g);

                //Cull based on global _Alpha
                half4 ditherTextureSample =   _DitherTexture.Sample(my_sampler_point_repeat, screenPos.xy * _DitherTexture_TexelSize.xy);
                // clip HLSL instruction stops rendering a pixel if value is negative
                clip(ditherTextureSample.r - (1 - _Alpha));
                
                 
                //MRT0 = half4(SRGBtoLinear(input.color).rgb, 1);
                MRT0 = half4(input.color.rgb, .5);
                
                MRT1 = half4(0, 0, 0, 0);
            }


             
            ENDHLSL
        }
    }
}