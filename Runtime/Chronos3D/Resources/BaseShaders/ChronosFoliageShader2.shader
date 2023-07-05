//Shader for grass etc

Shader "Chronos/FoliageShader2"
{
    
    Properties
    {
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        [HDR] _DarkColor("DarkColor", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
        
        //lightmix range from 0..1
		_LightMix("LightMix", Range(0,1)) = 0.5

        _DeformScale("DeformScale", Range(0,1)) = 0.1
        
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

            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;
            float4x4 unity_WorldToObject;
            float4 unity_WorldTransformParams;
            float3 _WorldSpaceCameraPos;
            
            SamplerState my_sampler_point_repeat;
            Texture2D _MainTex;
            Texture2D _DitherTexture;
            float4 _DitherTexture_TexelSize;

            float4 _MainTex_TexelSize;

            float _LightMix;
            float _DeformScale;
            
            float4 _Color;
            float4 _DarkColor;
            float4 _Time;
            float4 _ProjectionParams;
            float4 _MainTex_ST;
            half _Alpha = 1;
            
            //Lights
            float4 globalDynamicLightColor[2];
            float4 globalDynamicLightPos[2];
            float globalDynamicLightRadius[2];
           
            //properties from the system
            half3 globalAmbientLight[9];
            half3 globalAmbientTint;

            half3 globalSunLight[9];
            half3 globalSunDirection = normalize(half3(-1, -3, 1.5));
            half globalAmbientOcclusion = 0;
            float2 _ScreenParams;

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

            inline half3 SampleAmbientSphericalHarmonics(half3 direction)
            {
                half3 color = globalAmbientLight[0]  * 0.282095; // Constant term

                color += globalAmbientLight[1] * 0.488603 * direction.y; // Y-direction
                color += globalAmbientLight[2] * 0.488603 * direction.z; // Z-direction
                color += globalAmbientLight[3] * 0.488603 * direction.x; // X-direction

                color += globalAmbientLight[4] * 1.092548 * direction.x * direction.y; // XY-direction
                color += globalAmbientLight[5] * 1.092548 * direction.y * direction.z; // YZ-direction
                color += globalAmbientLight[6] * 0.315392 * (3.0 * direction.z * direction.z - 1.0); // Z^2-direction
                color += globalAmbientLight[7] * 1.092548 * direction.x * direction.z; // XZ-direction
                color += globalAmbientLight[8] * 0.546274 * (direction.x * direction.x - direction.y * direction.y); // X^2-Y^2 direction

                return color;
            }

            inline half3 SampleSunSphericalHarmonics(half3 direction)
            {
                half3 color = globalSunLight[0] * 0.282095; // Constant term

                color += globalSunLight[1] * 0.488603 * direction.y; // Y-direction
                color += globalSunLight[2] * 0.488603 * direction.z; // Z-direction
                color += globalSunLight[3] * 0.488603 * direction.x; // X-direction

                color += globalSunLight[4] * 1.092548 * direction.x * direction.y; // XY-direction
                color += globalSunLight[5] * 1.092548 * direction.y * direction.z; // YZ-direction
                color += globalSunLight[6] * 0.315392 * (3.0 * direction.z * direction.z - 1.0); // Z^2-direction
                color += globalSunLight[7] * 1.092548 * direction.x * direction.z; // XZ-direction
                color += globalSunLight[8] * 0.546274 * (direction.x * direction.x - direction.y * direction.y); // X^2-Y^2 direction

                return color;
            }

            

            inline float3 UnityObjectToWorldNormal(in float3 dir)
            {
                return normalize(mul(dir, (float3x3)unity_WorldToObject));
            }

            inline float3 UnityObjectToWorldDir(in float3 dir)
            {
                return normalize(mul((float3x3)unity_ObjectToWorld, dir));
            }

            //Make this vertex flap around in the wind using sin and cos
            float4 Wind(float4 input, float scale)
            {
                
                scale = (1 - scale) * _DeformScale;
                float speed = 10;
                float windSpeed = 30;
                
                float phase = _Time.x * speed;
                float windPhase = _Time.x * windSpeed;
                 
                //global wind flows
                float stretch = 0.25;
                float windStrength = clamp(1+ (1+sin(windPhase + input.x * stretch))*0.5,0,1);

                 //Subtle motion
                //use a sin circle to move this vertex
                input.x += (sin(phase + input.x * 200 + input.z * 100) * 0.1) * (windStrength * scale);
                input.y += (cos(phase + input.x * 150 + input.z * 100) * 0.1) * (windStrength* scale);
                input.z += (cos(phase + input.x *  50 + input.z * 50) * 0.1) * (windStrength * scale);

                //major wind motion (offset around a point+ sin)
                float windMotion = 0.1;
                input.x += (-windMotion*0.5) + (sin((phase*4) + (input.x * 0.3) ) * windMotion) * scale;

                return input;
            }


            half4 SRGBtoLinear(half4 srgb)
            {
                // return srgb;
                // return half4(0,0.5,0,1);
                return pow(srgb, 0.4545454545);
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
                output.color.rgb = GrassColor(saturate( 1-input.uv_MainTex.y), SRGBtoLinear(bright), SRGBtoLinear(dark)) * lighting;

                

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

            half PhongApprox(half Roughness, half RoL)
            {
                half a = Roughness * Roughness;
                half a2 = a * a;
                float rcp_a2 = rcp(a2);
                // 0.5 / ln(2), 0.275 / ln(2)
                half c = 0.72134752 * rcp_a2 + 0.39674113;
                return rcp_a2 * exp2(c * RoL - c);
            }
            
            half3 ProcessReflectionSample(half3 img)
            {
                return (img * img) * 2;
            }
         
            half4 LinearToSRGB(half4 srgb)
            {
                return pow(srgb, 2.2333333);
            }
            
             half3 LinearToSRGB(half3 srgb)
            {
                return pow(srgb, 2.2333333);
            }

            half3 CalculatePointLightForPoint(float3 worldPos, half3 normal, half3 albedo, half roughness, half3 specularColor, half3 reflectionVector, float3 lightPos, half4 color, half lightRange)
            {
                float3 lightVec = lightPos - worldPos;
                half distance = length(lightVec);
                half3 lightDir = normalize(lightVec);

                float RoL = max(0, dot(reflectionVector, lightDir));
                float NoL = max(0, dot(normal, lightDir));

                float distanceNorm = saturate(distance / lightRange);
                float falloff = distanceNorm * distanceNorm * distanceNorm;
                falloff = 1.0 - falloff;

                falloff *= NoL;

                half3 result = falloff * (albedo * color + specularColor * PhongApprox(roughness, RoL));
                
                return result;
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
                MRT0 = half4(input.color.rgb, 1);
                
                MRT1 = half4(0, 0, 0, 0);
            }


             
            ENDHLSL
        }
    }
}