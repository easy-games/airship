Shader "Airship/WaterPlaneShader"
{
    Properties
    {
        [Header(Coloring)]
        [HDR] _ColorA("Color A", Color) = (.1, .25, .1, 1)
        [HDR] _ColorB("Color B", Color) = (.2,.6,.1,1)
                    
        _NormalMap("Normals", 2D) = "bump" {}
        
        [Header(Mix)]
        _Brightness("BaseColor", Range(0,1)) = 1
        _Reflective("Reflective", Range(0,1)) = 1
        _Specular("Specular", Range(0,1)) = 1

        [Header(Deformation)]
        _DeformSpeed("Global Speed", Range(0,10)) = 1

        _Layer0Scale("Layer0Scale", Range(0,1)) = 1
        _Layer1Scale("Layer1Scale", Range(0,1)) = 1
        _Layer2Scale("Layer2Scale", Range(0,1)) = 1

        _Layer0Speed("Layer0Speed", Range(-3,3)) = 1
        _Layer1Speed("Layer1Speed", Range(-3,3)) = 1
        _Layer2Speed("Layer2Speed", Range(-3,3)) = 1
        
        _Layer0Rotation("Layer0Rotation", Range(0,360)) = 0
        _Layer1Rotation("Layer1Rotation", Range(0,360)) = 45
        _Layer2Rotation("Layer2Rotation", Range(0,360)) = 120

        [Header(Consts)]
        _WaterSpecular("Specpow", Range(0,100)) = 4
        _WaterFresnel("Fresnel", Range(0,100)) = 2
    }

    SubShader
    {
        Cull off

        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Name "Forward"
            Tags { "LightMode" = "AirshipForwardPass" "Queue" = "Opaque" }

            //Blend[_SrcBlend][_DstBlend]
            ZWrite On
            Cull   Off

            HLSLPROGRAM

            sampler2D _NormalMap;
            float4 _NormalMap_ST;

            samplerCUBE _CubeTex;
            
            half4 _ColorA;
            half4 _ColorB;
            half4 _ShadowColor;
            half4 _RealTime;
            half _DeformSpeed;

            half _Layer0Scale;
            half _Layer1Scale;
            half _Layer2Scale;
            
            half _Layer0Speed;
            half _Layer1Speed;
            half _Layer2Speed;

            half _Layer0Rotation;
            half _Layer1Rotation;
            half _Layer2Rotation;

            half _Brightness;
            half _Reflective;
            half _Specular;

            half _WaterFresnel;
            half _WaterSpecular;

            //Multi shader vars
            #pragma vertex simpleVertFunction
            #pragma fragment fragFunction
            
            #include "UnityCG.cginc"
            #include "Packages/gg.easy.airship/Runtime/Code/Airship/Resources/BaseShaders/AirshipShaderIncludes.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 uv_NormalTex : TEXCOORD0;
            };

            struct vertToFrag
            {
                float4 positionCS : SV_POSITION;
                float4 color0 : COLOR0;
                float4 color1 : COLOR1;
                
                float4 uv_NormalTex0 : TEXCOORD0;
                float4 uv_NormalTex1 : TEXCOORD1;
                float4 uv_NormalTex2 : TEXCOORD2;
                
                float3 worldPos : TEXCOORD3;
                half3 worldNormal : TEXCOORD4;
                              
                float4 shadowCasterPos0 : TEXCOORD5;
                float4 shadowCasterPos1 : TEXCOORD6;

                half3 tspace0 : TEXCOORD7; // tangent.x, bitangent.x, normal.x
                half3 tspace1 : TEXCOORD8; // tangent.y, bitangent.y, normal.y
                half3 tspace2 : TEXCOORD9; // tangent.z, bitangent.z, normal.z
            };
            
            float2 Rotate(float2 uv, float rotDeg)
            {
				float rot = radians(rotDeg);
				float2x2 rotationMatrix = float2x2(cos(rot), -sin(rot), sin(rot), cos(rot));
				return mul(uv, rotationMatrix);
            }

            float4 CalculateWaterUvs(float3 worldPos, float scale, float rotation, float speed)
            {
				float2 uv = float2(worldPos.x, worldPos.z);
				uv = uv * scale;
				
				uv = Rotate(uv, rotation);
                uv.x = uv.x + _RealTime.x * speed;

				return float4(uv.x, uv.y, 0,0);
            }
        
            
            vertToFrag simpleVertFunction(Attributes input)
            {
                vertToFrag output;

                float4 originalWorldPos = mul(unity_ObjectToWorld, input.positionOS);

                float4 worldPos = originalWorldPos;

                output.positionCS = mul(unity_MatrixVP, worldPos);
             
				float2 offset = _RealTime.x * _DeformSpeed;

                output.uv_NormalTex0 = CalculateWaterUvs(originalWorldPos, _NormalMap_ST.x * _Layer0Scale, _Layer0Rotation, _Layer0Speed * _DeformSpeed);
                output.uv_NormalTex1 = CalculateWaterUvs(originalWorldPos, _NormalMap_ST.x * _Layer1Scale, _Layer1Rotation, _Layer1Speed * _DeformSpeed);
                output.uv_NormalTex2 = CalculateWaterUvs(originalWorldPos, _NormalMap_ST.x * _Layer2Scale, _Layer2Rotation, _Layer2Speed * _DeformSpeed);
                
                //More accurate shadows (normal biased + lightmap resolution)
                float3 shadowNormal = normalize(mul(float4(input.normal, 0.0), unity_WorldToObject).xyz);
                // Apply the adjusted offset
                output.shadowCasterPos0 = mul(_ShadowmapMatrix0, worldPos + float4((shadowNormal * 0.03), 0));
                output.shadowCasterPos1 = mul(_ShadowmapMatrix1, worldPos + float4((shadowNormal * 0.06), 0));
                                
                output.worldPos = worldPos;
                output.worldNormal = normalize(UnityObjectToWorldNormal(input.normal));

                //Normal Matrix
                half3 wNormal = output.worldNormal;// UnityObjectToWorldNormal(input.normal);
                half3 wTangent = UnityObjectToWorldDir(input.tangent.xyz);
                // compute bitangent from cross product of normal and tangent
                half tangentSign = input.tangent.w * unity_WorldTransformParams.w;
                half3 wBitangent = cross(wNormal, wTangent) * tangentSign;

                // output the tangent space matrix
                output.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
                output.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
                output.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);


                float3 viewDir = normalize(WorldSpaceViewDir(input.positionOS));

                //float4 lighting = half4(max(input.color.rrr, SampleAmbientSphericalHarmonics(half3(0, 1, 0))), 1);
                float4 colorA = pow(_ColorA, 0.4545454545);
                float4 colorB = pow(_ColorB, 0.4545454545);
                

                output.color0 = colorA * _Brightness;
                output.color1 = colorB * _Brightness;

                return output;
            }

            void fragFunction(vertToFrag input, bool frontFacing : SV_IsFrontFace, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
            {
                float3 normalTex0 = UnpackNormal(tex2D(_NormalMap, input.uv_NormalTex0.xy));
                float3 normalTex1 = UnpackNormal(tex2D(_NormalMap, input.uv_NormalTex1.xy));
                float3 normalTex2 = UnpackNormal(tex2D(_NormalMap, input.uv_NormalTex2.xy));
                
                float3 summedNormal =  normalize(normalTex0 + normalTex1 + normalTex2);

                float3 worldNormal = summedNormal;
                worldNormal.x = dot(input.tspace0, summedNormal);
                worldNormal.y = dot(input.tspace1, summedNormal);
                worldNormal.z = dot(input.tspace2, summedNormal);
                
                
                half3 viewVector = _WorldSpaceCameraPos.xyz - input.worldPos;
                half3 viewDirection = normalize(viewVector);
                float viewDistance = length(viewVector);

                half3 reflectVector = reflect(-viewDirection, worldNormal);
                
                //Shadows
                float shadowMask = GetShadow(input.shadowCasterPos0, input.shadowCasterPos1, input.worldNormal, globalSunDirection);
                
                //Calculate specular from globalSunDirection
                half3 sunReflection = reflect(-globalSunDirection, worldNormal);
                half3 specular = pow(saturate(dot(sunReflection, -viewDirection)), _WaterSpecular);
                half3 finalSpecular = specular * shadowMask * _Specular;
                
                //Calculate basecolor toon ramp
				half halfLambert = saturate(dot(worldNormal, -globalSunDirection) );
                half3 baseColor = lerp(input.color0, input.color1, halfLambert);

                //Calculate skybox reflect
                half3 skyboxSample = texCUBE(_CubeTex, reflectVector).rgb;
                skyboxSample = lerp(skyboxSample * 0.75, skyboxSample, shadowMask); //shadow
                
                //Fresnel
                half fresnel = pow(1 - dot(worldNormal, viewDirection), _WaterFresnel);
                skyboxSample *= fresnel;
                skyboxSample *= _Reflective;


                //Composite
                half3 composite = baseColor + finalSpecular + skyboxSample;

                //fog
                half3 finalColor = CalculateAtmosphericFog(composite, viewDistance);
               
                MRT0 = half4(finalColor, 1);
                MRT1 = half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
         
    }
}
