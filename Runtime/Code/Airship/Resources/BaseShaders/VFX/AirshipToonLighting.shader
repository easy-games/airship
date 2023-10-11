Shader "Airship/ToonLighting"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_OverlayColor("Overlay Color", Color) = (0,0,0,0)
		_EmissiveColor("Emissive Color", Color) = (0,0,0,1)
		_ShadowColor("Shadow Color", Color) = (0,0,0,1)
		_ShadowCutoff("Shadow Cutoff", Range(0, 1)) = .2
		_MainTex("Main Texture", 2D) = "white" {}
		_NormalMap("Normal Texture", 2D) = "bump" {}
		_NormalIntensity("Normal Intensity", Range(0, 1)) = 1
		// Ambient light is applied uniformly to all surfaces on the object.
		//_AmbientColor("Ambient Color", Color) = (0.4,0.4,0.4,1)
		_AmbientStrength("Ambient Strength", Float) = .2
		_SpecularColor("Specular Color", Color) = (0.9,0.9,0.9,1)
		// Controls the size of the specular reflection.
		_Glossiness("Glossiness", Float) = 32
		_RimColor("Rim Color", Color) = (1,1,1,1)
		_RimAmount("Rim Amount", Range(0, 1)) = 0.716
		// Control how smoothly the rim blends when approaching unlit
		// parts of the surface.
		_RimThreshold("Rim Threshold", Range(0, 1)) = 0.1	
        [KeywordEnum(LIGHTS0, LIGHTS1, LIGHTS2)] NUM_LIGHTS("NumLights", Float) = 0.0
		_UVRotation("UV Rotation", Range(0,360)) = 0
		_UVCenter("UV Center", Range(0,1)) = 0.5
	}
	SubShader
	{
		Pass
		{
			Tags
			{
				"LightMode" = "AirshipForwardPass"
				"Queue" = "Opaque"
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
				float4 uv : TEXCOORD0;
				float4 vertColor: COLOR;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
			};

			struct Interp
			{
				float4 pos : SV_POSITION;
				float4 vertColor: COLOR;
				float3 worldNormal : NORMAL;
				float2 uv : TEXCOORD0;
				float3 viewDir : TEXCOORD1;	
				float3 worldTangent : TEXCOORD2;	
				float3 worldBiTangent : TEXCOORD3;	
				float3 worldPos: TEXCOORD4;
				half3 ambientColor: TEXCOORD5;
			};
			
			sampler2D _MainTex;
			sampler2D _NormalMap;
			float _NormalIntensity;
			float4 _MainTex_ST;
			
			float4 _Color;
			float4 _ShadowColor;
			float _ShadowCutoff;		

			//float4 _AmbientColor;
			float _AmbientStrength;

			float4 _EmissiveColor;
			float4 _SpecularColor;
			float4 _OverlayColor;
			float _Glossiness;	
			
			float _RimAmount;
			float _RimThreshold;
			float _UVRotation;
			float _UVCenter;
			
			Interp vert (appdata v)
			{
				Interp o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.worldTangent	= UnityObjectToWorldDir(v.tangent);
				o.worldBiTangent = cross(o.worldNormal, o.worldTangent) * (v.tangent.w * unity_WorldTransformParams.w);
				o.viewDir = WorldSpaceViewDir(v.vertex);

				float uvRadians =  _UVRotation * (UNITY_PI / 180);
				float2 uv = cos(uvRadians) * (v.uv.x - _UVCenter) + sin(uvRadians) * (v.uv.y - _UVCenter) + _UVCenter;
				
				//float2x2 rotationMatrix = float2x2( 0, _UVRotation, _UVRotation, 0);
				//float2 uv = mul(v.uv, rotationMatrix);
				o.uv = TRANSFORM_TEX(uv, _MainTex);
				//o.uv = mul(TRANSFORM_TEX(uv, _MainTex), rotationMatrix);
				o.ambientColor = SampleAmbientSphericalHarmonics(o.worldNormal);
				o.vertColor = v.vertColor;
				return o;
			}
			
			float4 frag(Interp i, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1) : SV_Target2
			{
				//return i.vertColor;
				const half4 overlayColor = SRGBtoLinear(_OverlayColor);
				const half4 color = lerp( SRGBtoLinear(_Color), overlayColor, _OverlayColor.a);
				const half4 shadowColor = SRGBtoLinear(_ShadowColor);
				const half4 rimColor = SRGBtoLinear(_RimColor);
				const half4 specColor = SRGBtoLinear(_SpecularColor);
				const half4 emissiveColor =  lerp( SRGBtoLinear(_EmissiveColor), overlayColor, _OverlayColor.a);
				
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
				float brightness = NdotL;// min(_SunScale, i.ambientColor.g) * NdotL;
				//brightness += (1-brightness) * _AmbientStrength;
				
				//Do the fog
				half3 viewVector = _WorldSpaceCameraPos.xyz - i.worldPos;
				float viewDistance = length(viewVector);
				

				//MRT0 = half4 (globalDynamicLightRadius[0],globalDynamicLightRadius[0],globalDynamicLightRadius[0],1);
				//MRT1 = half4(0,0,0,0);
				//return float4(1,1,1,1);
				
				//Point lights
#ifdef NUM_LIGHTS_LIGHTS1
			    
			    brightness += CalculatePointLight(i.worldPos, worldNormal, globalDynamicLightPos[0], globalDynamicLightColor[0], globalDynamicLightRadius[0]);
#endif			    
#ifdef NUM_LIGHTS_LIGHTS2
			    brightness += CalculatePointLight(i.worldPos, worldNormal, globalDynamicLightPos[0], globalDynamicLightColor[0], globalDynamicLightRadius[0]);
			    brightness += CalculatePointLight(i.worldPos, worldNormal, globalDynamicLightPos[1], globalDynamicLightColor[1], globalDynamicLightRadius[1]);
#endif
				//return brightness;

				// Partition the intensity into light and dark, smoothly interpolated
				// between the two to avoid a jagged break.
				float lightIntensity =  smoothstep(0, _ShadowCutoff, brightness);


				// Calculate specular reflection.
				float3 halfVector = normalize(-globalSunDirection + viewDir);
				float NdotH = dot(worldNormal, halfVector);
				float specularIntensity = pow(NdotH * lightIntensity, 100+_Glossiness);
				float specularIntensitySmooth = smoothstep(0.005, 0.01, specularIntensity);
				float4 specular = specularIntensitySmooth * specColor;				

				// Calculate rim lighting.
				float rimDot = 1 - dot(viewDir, worldNormal);
				// We only want rim to appear on the lit side of the surface,
				// so multiply it by NdotL, raised to a power to smoothly blend it.
				float rimIntensity = rimDot * pow(NdotL, _RimThreshold);
				rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimIntensity);
				float4 rim = rimIntensity * rimColor;

				float4 diffuse = tex2D(_MainTex, i.uv);

				float4 shadow = (1-lightIntensity) * shadowColor;
				float4 light = lightIntensity + (1-lightIntensity) * _AmbientStrength;
				
				half4 finalColor = (shadow + light + specular + rim) * color * diffuse;
				//fog
				finalColor.xyz = CalculateAtmosphericFog(finalColor.xyz, viewDistance);

				//regular code
				MRT0 = finalColor;
				MRT1 = emissiveColor * rim;
				return MRT0;
			}
			ENDCG
		}
		

		// Shadow casting support.
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
	}
}