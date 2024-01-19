Shader "Airship/AirshipToonHair"
{
    Properties
    {
        [HDR]_ColorTop ("Top Color", Color) = (1,1,1,1)
        [HDR]_ColorMid ("Middle Color", Color) = (1,0,1,1)
        [HDR]_ColorBot ("Bottom Color", Color) = (1,1,0,1)
        [HDR]_ColorBottom ("Bottom Color", Color) = (1,1,1,1)
        [HDR]_SpecColor ("Specular Color", Color) = (.5,.5,.5,1)
        [HDR]_ShadowColor ("Shadow Color", Color) = (0,0,0,1)
        [HDR]_RimColor ("Rim Color", Color) = (0,1,1,1)
        [HDR]_RimColorShadow ("Rim Color Shadowed", Color) = (.25,.5,.5,1)
        _MainTex ("Diffuse", 2D) = "white" {}
        _Normal ("Normal Map", 2D) = "bump" {}
        _ShadowRamp ("ShadowRamp", 2D) = "white" {}
        _RimPower ("Rim Power", float) = 10
        _RimIntensity("Rim Intensity", float) = 1
        _SpecMod ("Specular Intensity", Range(0,1)) = 1
        _SaturationMod("Saturation Increase", float) = 1
        _AmbientMod("Ambient Mod", float) = 1

        _AnisoNoise("Hair Shine Noise", 2D) = "white" {}
        _AnisoNoiseFreq("Hair Noise Freq", float) = 1
        _AnisoNoiseStrength("Hair Shine Noise Strength", float) = 1
        _AnisoStrength("Hair Shine Vertical Mod", float) = 1
        _AnisoOffset("Hair Shine Vertical Offset", float) = .5
        _AnisoRampMod("Hair Shine Ramp Mod", float) = 2
        _AnisoRampIntensity("Hair Shine Ramp Intensity", float) = 1

        _OverrideStrength("Override Color Strength", Range(0,1)) = 0

        [Toggle] INSTANCE_DATA("Has Baked Instance Data", Float) = 0.0
    }
    SubShader
    {
        Name "Forward"
        Tags { "LightMode" = "AirshipForwardPass" }

        Pass
        {
            CGPROGRAM
            
			#include "UnityCG.cginc"
            #include "AirshipShaderIncludes.cginc"
            
            static float4 RimDirTest = float4(1,1,0,1);
            static float LightingBlend = .5;
            
            //Main programs
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ INSTANCE_DATA_ON

            //Multi shader vars (you need these even if you're not using them, so that material properties can survive editor script reloads)
            float VERTEX_LIGHT;  
            float SLIDER_OVERRIDE;
            float POINT_FILTER;
            float EXPLICIT_MAPS;
            float EMISSIVE;
            float RIM_LIGHT;

            struct VertData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 UV : TEXCOORD0;
                float4 color      : COLOR;
                
                float2 instanceIndex : TEXCOORD7;
            };

            struct VertToFrag
            {
                float4 vertex : SV_POSITION;
                float4 localVertex: TEXCOORD10;
                float2 uv : TEXCOORD0;
                float4 color      : COLOR;
                // these three vectors will hold a 3x3 rotation matrix
                // that transforms from tangent to world space
                half3 tspace0 : TEXCOORD1; // tangent.x, bitangent.x, normal.x
                half3 tspace1 : TEXCOORD2; // tangent.y, bitangent.y, normal.y
                half3 tspace2 : TEXCOORD3; // tangent.z, bitangent.z, normal.z
                float3 viewDir : TEXCOORD4;
                float cameraDistance: TEXCOORD5;
                float3 rimDot : TEXCOORD6;
                float4 shadowCasterPos0 :TEXCOORD7;
                float4 shadowCasterPos1 :TEXCOORD8;
                half3 worldNormal :TEXCOORD9;
                
            };

            sampler2D _MainTex;
            sampler2D _Normal;
            sampler2D _ShadowRamp;
            float4 _ColorTop;
            float4 _ColorMid;
            float4 _ColorBot;
            float4 _SpecColor;
            float4 _ShadowColor;
            float4 _RimColorShadow;
            float _SpecMod;
            float _SaturationMod;
            float _AmbientMod;
            float _OverrideStrength;

            sampler2D _AnisoNoise;
            float _AnisoNoiseFreq;
            float _AnisoStrength;
            float _AnisoOffset;
            float _AnisoRampMod;
            float _AnisoRampIntensity;
            float _AnisoNoiseStrength;
            
            
            VertToFrag vert (VertData v)
            {
                VertToFrag o;
                o.uv = v.UV;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.localVertex = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                o.color = v.color;

                //Normal Matrix
                half3 wNormal = UnityObjectToWorldNormal(v.normal);
                half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
                // compute bitangent from cross product of normal and tangent
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
                
                // output the tangent space matrix
                o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
                o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
                o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);

                o.worldNormal = wNormal;

                //Custom angles
                o.cameraDistance = length(ObjSpaceViewDir(v.vertex));
                o.rimDot = saturate(dot(UnityObjectToWorldDir(normalize(RimDirTest)), wNormal));

                //More accurate shadows (normal biased + lightmap resolution)
                float3 shadowNormal = normalize(mul(float4(v.normal, 0.0), unity_WorldToObject).xyz);
                // Apply the adjusted offset
                o.shadowCasterPos0 = mul(_ShadowmapMatrix0, worldPos + float4((shadowNormal * 0.03), 0));
                o.shadowCasterPos1 = mul(_ShadowmapMatrix1, worldPos + float4((shadowNormal * 0.06), 0));
                               
                #if INSTANCE_DATA_ON
		            float4 instanceColor = _ColorInstanceData[v.instanceIndex.x];
                    o.color *= instanceColor;
                #endif
                
                return o;
            }

            float3 RGBtoHSV(float3 c)
            {
                const half4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = c.g < c.b ? float4(c.bg, K.wz) : float4(c.gb, K.xy);
                float4 q = c.r < p.x ? float4(p.xyw, c.r) : float4(c.r, p.yzx);

                const float d = q.x - min(q.w, q.y);
                const float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            float3 SetColorSaturation(float3 color, float saturation)
            {
                float3 convertedColor = RGBtoHSV(color);
                convertedColor.y *= saturation;
                return HSVtoRGB(convertedColor);
            }

            void frag (VertToFrag i, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
            {

                //Multi color lerp
                half4 colors[3] = {_ColorBot, _ColorMid, _ColorTop};
                float scaledTime = saturate(i.uv.y) * (float) (3 - 1);
                half4 oldColor = colors[floor(scaledTime)];
                half4 newColor = colors[floor(scaledTime+ 1)];
                float newT = scaledTime - floor(scaledTime);
                half4 hairColor = lerp(oldColor, newColor, newT);
                
                //COMMON VARIABLES
                float distanceDelta = saturate(i.cameraDistance / 8);
                // sample the normal map, and decode from the Unity encoding
                half3 tnormal = UnpackNormal(tex2D(_Normal, i.uv));
                // transform normal from tangent to world space
                half3 worldNormal;
                worldNormal.x = dot(i.tspace0, tnormal);
                worldNormal.y = dot(i.tspace1, tnormal);
                worldNormal.z = dot(i.tspace2, tnormal);

                half3 worldReflect = reflect(-i.viewDir, worldNormal);
                half RoL = max(0, dot(worldReflect, -globalSunDirection));
                half NoL = max(dot(-globalSunDirection, worldNormal), 0);
                half FillDot = max(dot(globalSunDirection, worldNormal), 0);
                half3 flippedSun = UnityWorldToObjectDir(globalSunDirection);
                flippedSun.z *= -1;
                flippedSun = UnityObjectToWorldDir(flippedSun);
                half Reflection = max(dot(flippedSun, worldNormal), 0);
                half NoV = max(dot(i.viewDir, worldNormal), 0);
                
                //DIFFUSE COLOR
                fixed4 textureColor = tex2D(_MainTex, i.uv);

                //LIGHTING
                worldNormal = i.worldNormal;
                float lightStrength = (dot(-globalSunDirection, worldNormal));// * globalSunBrightness;
                float shadowMask = GetShadow(i.shadowCasterPos0, i.shadowCasterPos1, worldNormal, globalSunDirection);
                shadowMask = shadowMask*.5+.5;
                //shadowMask = 1;
                
                //Specular//Specular
                float metallicLevel = .2;
                float roughnessLevel = .5;
                half3 specularColor;
                half dielectricSpecular = .3; //0.3 is the industry standard
                half3 diffuseColor = textureColor * hairColor * i.color;
                half3 metallicColor = diffuseColor - diffuseColor * metallicLevel;// * _TestFloat;	// 1 mad
                specularColor = (dielectricSpecular - dielectricSpecular * hairColor * metallicLevel) + textureColor * hairColor * metallicLevel;	// 2 mad
                specularColor = EnvBRDFApprox(specularColor * _SpecColor, hairColor * textureColor.y, NoV) * _SpecMod;
                
                half3 specularLight = NoL * (metallicColor + specularColor * PhongApprox(saturate(roughnessLevel + roughnessLevel * (1-_SpecMod)), RoL)) * _SpecColor;
                specularLight = saturate(specularLight);// min(specularLight, half3(_SpecMod,_SpecMod,_SpecMod));
                lightStrength = saturate(lightStrength + specularLight);
                
                float halfLambertLightStrength = saturate((lightStrength + 1)*.5);
                //Sample the shadow ramp
                float lightDelta = lerp(tex2D(_ShadowRamp, float2(halfLambertLightStrength * shadowMask, 0)), halfLambertLightStrength * shadowMask, LightingBlend);

                float middleStrength = saturate((lightStrength)/2 * (1-lightDelta));
                //middleStrength *= middleStrength;
                half3 colorWithSpec = saturate(diffuseColor + diffuseColor*specularColor*specularLight);
                half3 finalDiffuse = SetColorSaturation(colorWithSpec,  1+middleStrength*_SaturationMod);

                //RIM
                half3 rimColor = (RimLightDelta(worldNormal, i.viewDir, _RimPower, _RimIntensity) + half3(.1,.1,.1)) * saturate(i.cameraDistance*i.cameraDistance);
                half3 finalRimColor = i.rimDot *  round(rimColor) * lerp(_RimColorShadow, _RimColor, lightDelta);


                //FINAL COLOR
                half3 shadowColor = saturate(_ShadowColor + globalAmbientTint * _AmbientMod) * diffuseColor;
                half3 finalColor = lerp(shadowColor, finalDiffuse, saturate(lightDelta)) + finalRimColor;

                //finalColor =lerp(saturate((_ShadowColor + globalAmbientTint * _AmbientMod) + .35) * colorWithSpec, colorWithSpec, saturate(lightDelta))  ;
                //finalColor = diffuseColor;
                //finalColor = _Color;

                float localY = i.uv.y;
                //localY = i.vertex.y;
                float localX = i.uv.x;
                //localX = i.viewDir.x;
                
                float specStrength = (specularLight+1)/2;
                float noiseStrength1 = tex2D(_AnisoNoise, localX * _AnisoNoiseFreq);
                float noiseStrength2 = tex2D(_AnisoNoise, localX * _AnisoNoiseFreq * _AnisoNoiseFreq);
                float noiseStrength = 2 * ((noiseStrength1 * noiseStrength2)*2-1) * _AnisoStrength;
                float anisoDelta = localY - i.viewDir.y + (_AnisoNoiseStrength * noiseStrength);
                float ramp = saturate((1-abs((anisoDelta-_AnisoOffset) *_AnisoRampMod)) * _AnisoRampIntensity);

                finalColor = finalColor +  (ramp * specStrength);
                //finalColor = i.localVertex.y;
                //finalColor = localY - i.viewDir.y;
                //finalColor = hairColor;
                
                MRT0 = lerp(half4(finalColor, 1), half4(1,0,0,1), _OverrideStrength);
                MRT1 = half4(0,0,0,1);
            }
            ENDCG
        }
         
        Pass
        {
			Name "ShadowCaster"
            Tags
            {
                "RenderType" = "Opaque"
                "LightMode" = "AirshipShadowPass"
            }
            ZWrite On
            CGPROGRAM
                #include "Packages/gg.easy.airship/Runtime/Code/Airship/Resources/BaseShaders/AirshipSimpleShadowPass.hlsl"
            ENDCG
        }
    }
}
