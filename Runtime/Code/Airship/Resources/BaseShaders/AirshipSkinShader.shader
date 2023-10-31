Shader "Airship/AirshipSkin"
{
    Properties
    {
        [HDR]_Color ("Main Color", Color) = (1,1,1,1)
        [HDR]_SpecColor ("Specular Color", Color) = (1,1,1,1)
        [HDR]_ShadowColor ("Shadow Color", Color) = (0,0,0,1)
        [HDR]_RimColor ("Rim Color", Color) = (0,1,1,1)
        [HDR]_RimColorShadow ("Rim Color Shadowed", Color) = (.25,.5,.5,1)
        _MainTex ("Diffuse", 2D) = "white" {}
        _Normal ("Normal Map", 2D) = "bump" {}
        _ORMTex ("Occlusion, Rough, Metal", 2D) = "white" {}
        _ShadowRamp ("ShadowRamp", 2D) = "white" {}
        _RimDir("Rim Direction", Vector) = (1,0,0)
        _RimPower ("Rim Power", float) = 10
        _RimIntensity("Rim Intensity", float) = 1
        _RimDistanceOffset("Rim Distance Offset", float) = 10
        _SpecMod ("Specular Intensity", Range(0,1)) = 1
        _SaturationMod("Saturation Increase", float) = 1
        _AmbientMod("Ambient Mod", float) = 1
        _TestFloat("Test Float", float) = 1
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
            
            //Main programs
            #pragma vertex vert
            #pragma fragment frag

            //Multi shader vars (you need these even if you're not using them, so that material properties can survive editor script reloads)
            float VERTEX_LIGHT;  
            float SLIDER_OVERRIDE;
            float POINT_FILTER;
            float EXPLICIT_MAPS;
            float EMISSIVE;
            float RIM_LIGHT;
            float INSTANCE_DATA;

            struct VertData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 UV : TEXCOORD0;
            };

            struct VertToFrag
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
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
            };

            sampler2D _MainTex;
            sampler2D _Normal;
            sampler2D _ShadowRamp;
            sampler2D _ORMTex;
            float4 _Color;
            float4 _SpecColor;
            float4 _ShadowColor;
            float4 _RimColorShadow;
            float _SpecMod;
            float _SaturationMod;
            float4 _RimDir;
            float _RimDistanceOffset;
            float _TestFloat;
            float _AmbientMod;
            
            VertToFrag vert (VertData v)
            {
                VertToFrag o;
                o.uv = v.UV;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                
                half3 wNormal = UnityObjectToWorldNormal(v.normal);
                half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
                // compute bitangent from cross product of normal and tangent
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
                
                // output the tangent space matrix
                o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
                o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
                o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);

                o.cameraDistance = length(ObjSpaceViewDir(v.vertex));
                o.rimDot = saturate(dot(UnityObjectToWorldDir(normalize(_RimDir)), wNormal));

                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.shadowCasterPos0 = mul(_ShadowmapMatrix0, worldPos);
                o.shadowCasterPos1 = mul(_ShadowmapMatrix1, worldPos);
                
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
                //COMMON VARIABLES
                float distanceDelta = saturate(i.cameraDistance / 10);
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
                fixed4 ormSample = tex2D(_ORMTex, i.uv);

                //LIGHTING
                float lightStrength = (1-saturate(dot(globalSunDirection, worldNormal))) * globalSunBrightness;
                float shadowMask = GetShadow(i.shadowCasterPos0, i.shadowCasterPos1, worldNormal, globalSunDirection);
                //shadowMask = 1;
                
                //Specular//Specular
                float metallicLevel = ormSample.b;
                half3 specularColor;
                half dielectricSpecular = .3; //0.3 is the industry standard
                half3 diffuseColor = textureColor * _Color;
                //half3 diffuseColor = inputColor - inputColor * metallicLevel * _TestFloat;	// 1 mad
                specularColor = (dielectricSpecular - dielectricSpecular * _Color * metallicLevel) + textureColor * _Color * metallicLevel;	// 2 mad
                specularColor = EnvBRDFApprox(specularColor * _SpecColor, _Color * textureColor.y, NoV) * _SpecMod;
                
                half3 specularLight = NoL * (diffuseColor + specularColor * PhongApprox(saturate(ormSample.r + ormSample.r * (1-_SpecMod)), RoL)) * _SpecColor;
                specularLight = saturate(specularLight);// min(specularLight, half3(_SpecMod,_SpecMod,_SpecMod));
                lightStrength = lightStrength + (lightStrength * specularLight);
                
                //Sample the shadow ramp
                float lightDelta = tex2D(_ShadowRamp, float2(lightStrength * shadowMask, 0));

                float negativeRangelightStrength = saturate((lightStrength+0.02) * 2 - 1);
                float middleStrength = saturate((negativeRangelightStrength)/2 * (1-lightDelta));
                //middleStrength *= middleStrength;
                half3 colorWithSpec = saturate(diffuseColor + diffuseColor*specularColor*specularLight);
                half3 finalDiffuse = SetColorSaturation(colorWithSpec,  1+middleStrength*_SaturationMod);

                //RIM
                float rimDistanceOffset = max(.25, 1-distanceDelta) * _RimDistanceOffset;
                half3 rimColor = (RimLightDelta(worldNormal, i.viewDir, _RimPower + rimDistanceOffset, _RimIntensity) + half3(.1,.1,.1)) * saturate(i.cameraDistance*i.cameraDistance);
                half3 finalRimColor = i.rimDot *  round(rimColor) * lerp(_RimColorShadow, _RimColor, lightDelta);


                //FINAL COLOR
                half3 shadowColor = (_ShadowColor + globalAmbientTint * _AmbientMod) * finalDiffuse;
                half3 finalColor = lerp(shadowColor, finalDiffuse, saturate(lightDelta)) + finalRimColor;

                //finalColor = rimColor;

                
                MRT0 = half4(finalColor, 1);
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
