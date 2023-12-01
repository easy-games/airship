Shader "Airship/AirshipCrystal"
{
	Properties
	{
		[Header(Colors)]
		_MainColor("Main Color", Color) = (1,1,1,.5)
		_ShineColor("Shine Color", Color) = (1,1,1,.5)
		_DepthColor("Depth Color", Color) = (1,1,1,1)
		_EmissionColor("Emissive Color", Color) = (0,0,0,1)
		_OverlayColor("Overlay Color", Color) = (1,0,0,0)
		
		[Header(Textures)]
		_MainTex("Main Texture", 2D) = "white" {}
		_DepthMainTex("Depth Texture", 2D) = "white" {}
		_NormalMap("Normal Texture", 2D) = "bump" {}
		_NormalIntensity("Normal Intensity", Range(0, 1)) = 1
		
		[Header(Fresnel)]
		_FresnelPower("Fresnel Power", Float) = 3
		_FresnelStrength("Fresnel Strength", Float) = 1
		_ShineFresnelPower("Shine Fresnel Power", Float) = 3
		_ShineFresnelStrength("Shine Fresnel Strength", Float) = 1
		
		[Header(Lighting)]
		_DepthScale("Depth Scale", Float) = 1
		_MinDepthHeight("Depth Height Min", Range(0,1)) = .01
		_MaxDepthHeight("Depth Height Max", Range(0,1)) = .1
		_MinLight("Minimum Light", Range(0, 1)) = .2
		_HueShift("Hue Shift", Range(0, 1)) = .2
		
		// Controls the size of the specular reflection.
		_Glossiness("Glossiness", Range(0,3)) = 1
		
	
        [KeywordEnum(LIGHTS0, LIGHTS1, LIGHTS2)] NUM_LIGHTS("NumLights", Float) = 0.0
	}
	SubShader
	{
		Pass
		{
			Tags
			{
				"LightMode" = "AirshipForwardPass"
				"Queue" = "Transparent"
			}

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile NUM_LIGHTS_LIGHTS0 NUM_LIGHTS_LIGHTS1 NUM_LIGHTS_LIGHTS2
			
			#include "UnityCG.cginc"
            #include "../AirshipShaderIncludes.cginc"

			struct appdata
			{
				float4 vertex : POSITION;	
				float3 normal : NORMAL;
				float4 tangent : TANGENT;			
				float4 uv : TEXCOORD0;
				float4 vertColor: COLOR;
			};

			struct Interp
			{
				float4 pos : SV_POSITION;
				float4 vertColor: COLOR;
				float3 worldNormal : NORMAL;
				float2 uv : TEXCOORD0;
				float2 screenUV: TEXCOOR6;
				float2 viewUV: TEXCOOR7;
				float3 viewDir : TEXCOORD1;	
				float3 worldTangent : TEXCOORD2;	
				float3 worldBiTangent : TEXCOORD3;	
				float4 worldPos: TEXCOORD4;
				half3 ambientColor: TEXCOORD5;
                float4 vectexPosScreenspace: TEXCOORD6;
			};

			//Diffuse
			float4 _MainColor;
			float4 _ShineColor;
			sampler2D _MainTex;
			float4 _DepthColor;
			sampler2D _DepthMainTex;
			float _HueShift;		

			//Emissive
			float4 _OverlayColor;
			float4 _EmissionColor;

			//Normal
			sampler2D _NormalMap;
			float _NormalIntensity;

			//Fresnel
			float _FresnelPower;
			float _FresnelStrength;
			float _ShineFresnelPower;
			float _ShineFresnelStrength;

			//Lighting
			float _DepthScale;
			float _MinLight;
			float _MinDepthHeight;
			float _MaxDepthHeight;
			float _AmbientStrength;
			float _Glossiness;

			//Refraction
			sampler2D _BlurColorTexture;
			
			float4 _MainTex_ST;
			
			Interp vert (appdata v)
			{
				Interp o;
				o.pos = UnityObjectToClipPos(v.vertex);
                o.vectexPosScreenspace = ComputeScreenPos(o.pos);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.worldTangent	= UnityObjectToWorldDir(v.tangent);
				o.worldBiTangent = cross(o.worldNormal, o.worldTangent) * (v.tangent.w * unity_WorldTransformParams.w);
				o.viewDir = normalize(UnityWorldSpaceViewDir(o.worldPos));

				
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				float3 viewSpace = UnityObjectToViewPos(v.vertex);
				o.viewUV = viewSpace * _DepthScale;// float4(viewSpace, clamp(.01, 1, -viewSpace.z)) * -_DepthScale;
				o.screenUV = ComputeScreenPos(o.pos);
				o.ambientColor = SampleAmbientSphericalHarmonics(o.worldNormal);
				o.vertColor = v.vertColor;
				return o;
			}
			
			float4 frag(Interp i, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1) : SV_Target2
			{
				//return i.vertColor;
				const half4 overlayColor = SRGBtoLinear(_OverlayColor);
				const half4 color = lerp( SRGBtoLinear(_MainColor), overlayColor, _OverlayColor.a);
				const half4 shineColor = SRGBtoLinear(_ShineColor);
				const half4 depthColor = SRGBtoLinear(_DepthColor);
				const half4 emissiveColor =  lerp(SRGBtoLinear(_EmissionColor), overlayColor, _OverlayColor.a);
				
				//Normal Mapping
                half3 tangentNormal = UnpackNormal(tex2D(_NormalMap, i.uv));
				tangentNormal = lerp(float3(0,0,1), tangentNormal, _NormalIntensity);
				float3x3 mTangToWorld = {
					i.worldTangent.x,i.worldBiTangent.x,i.worldNormal.x,
					i.worldTangent.y,i.worldBiTangent.y,i.worldNormal.y,
					i.worldTangent.z,i.worldBiTangent.z,i.worldNormal.z,
				};
                half3 worldNormal = mul(mTangToWorld, tangentNormal);

				//View Dir
				float3 viewDir = normalize(i.viewDir);

				// Calculate illumination from directional light.
				float NdotL = dot(-globalSunDirection, worldNormal);
				NdotL = NdotL * .5 + .5;
				float brightness = NdotL;// min(_SunScale, i.ambientColor.g) * NdotL;
				//brightness += (1-brightness) * _AmbientStrength;
				
				//Do the fog
				half3 viewVector = _WorldSpaceCameraPos.xyz - i.worldPos;
				float viewDistance = length(viewVector);
				
				//Point lights
#ifdef NUM_LIGHTS_LIGHTS1
			    
			    brightness += CalculatePointLight(i.worldPos, worldNormal, globalDynamicLightPos[0], globalDynamicLightColor[0], globalDynamicLightRadius[0]);
#endif			    
#ifdef NUM_LIGHTS_LIGHTS2
			    brightness += CalculatePointLight(i.worldPos, worldNormal, globalDynamicLightPos[0], globalDynamicLightColor[0], globalDynamicLightRadius[0]);
			    brightness += CalculatePointLight(i.worldPos, worldNormal, globalDynamicLightPos[1], globalDynamicLightColor[1], globalDynamicLightRadius[1]);
#endif
				brightness = max(_MinLight, brightness * _Glossiness);


				// Calculate specular reflection.
				float3 halfVector = normalize(-globalSunDirection + viewDir);
				float NdotH = dot(worldNormal, halfVector);
				float specularIntensity = pow(NdotH, 100+ _Glossiness * 32);
				float specularIntensitySmooth = smoothstep(0.005, 0.01, specularIntensity);
				float4 specular = specularIntensitySmooth * shineColor;	

				//Surface Colors
				float4 mainTex = tex2D(_MainTex, i.uv);
				float diffuse = mainTex.r;
				float shine = mainTex.g;
				float surfaceOpacity = color.a;
				float fresnel = RimLightDelta(worldNormal, i.viewDir, _FresnelPower, _FresnelStrength) * surfaceOpacity;
				half4 finalDiffuseColor = fresnel + diffuse * color;
				
				float shineFresnel = RimLightDelta(worldNormal, i.viewDir, _ShineFresnelPower, _ShineFresnelStrength) * surfaceOpacity;
				half4 finalShineColor = shineFresnel * shine * shineColor;

				half4 finalSurfaceColor = saturate(finalDiffuseColor + finalShineColor + specular);

				float surfaceMask = saturate(saturate(fresnel + shineFresnel)+ specular) * surfaceOpacity;
				//finalSurfaceColor = lerp(color, finalSurfaceColor, surfaceAlpha);

				//Depth Colors
				float fresnelNegative = (fresnel * 2 - 1);
				float uvDot = dot(i.viewDir, worldNormal);
				half2 depthUV =  lerp(_MinDepthHeight, _MaxDepthHeight, fresnelNegative) + i.viewUV;
				float depthTex = max(_MinLight, tex2D(_DepthMainTex, depthUV));
				
                float2 screenUV = i.vectexPosScreenspace.xy / i.vectexPosScreenspace.w;
				screenUV.y = 1-screenUV.y;
				float4 screenColor = tex2D(_BlurColorTexture, screenUV);//(_MinDepthHeight, _MaxDepthHeight, fresnelNegative) + screenUV);
				half4 finalDepthColor = saturate(lerp(screenColor * depthColor, depthTex * depthColor, depthColor.a));

				half4 depthBlend = surfaceOpacity * color + finalDepthColor;
				half4 finalColor = lerp(finalDepthColor, finalSurfaceColor, surfaceMask) * brightness;
				
				//fog
				finalColor.xyz = CalculateAtmosphericFog(finalColor.xyz, viewDistance);
				
				//finalColor = mainTex.b * half4(0,1,0,1);
				//finalColor = half4(i.viewDir,1);
				MRT0 = finalColor;
				MRT1 = emissiveColor * brightness * surfaceMask * finalShineColor;
				//MRT1 = half4(0,0,0,1);
				return MRT0;
			}
			ENDCG
		}

		// Shadow casting support.
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