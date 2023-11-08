// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

#ifndef AIRSHIPSHADER_INCLUDE
#define AIRSHIPSHADER_INCLUDE

float INSTANCE_DATA;//Instance of baked mesh
float4 _ColorInstanceData[16];//Instance data (for this material)

//Lighting variables
half3 globalSunDirection = normalize(half3(-1, -3, 1.5));
float _SunScale = 1; //How much the sun is hitting you
half3 globalSunColor;
float globalSunBrightness;
half3 globalAmbientLight[9];//Global ambient values
half3 globalAmbientTint;

//Point Lights
float NUM_LIGHTS; //Required for dynamic lights
float4 globalDynamicLightColor[2];
float4 globalDynamicLightPos[2];
float globalDynamicLightRadius[2];

float globalFogStart;
float globalFogEnd;
half3 globalFogColor;


half _RimPower;
half _RimIntensity;
half4 _RimColor;

//Shadow stuff///////
float4x4 _ShadowmapMatrix0;
float4x4 _ShadowmapMatrix1;
Texture2D _GlobalShadowTexture0;
Texture2D _GlobalShadowTexture1;
SamplerComparisonState sampler_GlobalShadowTexture0;
SamplerComparisonState sampler_GlobalShadowTexture1;
/////////////////////

half4 SRGBtoLinear(half4 srgb)
{
    return pow(srgb, 0.4545454545);
}
half4 LinearToSRGB(half4 srgb)
{
    return pow(srgb, 2.2333333);
}
            
half3 LinearToSRGB(half3 srgb)
{
    return pow(srgb, 2.2333333);
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

#define SAMPLE_TEXTURE2D_SHADOW(textureName, samplerName, coord3) textureName.SampleCmpLevelZero(samplerName, (coord3).xy, (coord3).z)

half GetShadowSample(Texture2D tex, SamplerComparisonState textureSampler, half2 uv, half bias, half comparison)
{
    const float scale = (1.0 / 2048.0) * 0.5;
    half3 offset0 = half3(-1.5 * scale, 0 * scale, 0);
    half3 offset1 = half3(1.5 * scale, 0 * scale, 0);
    half3 offset2 = half3(0 * scale, 1.5 * scale, 0);
    half3 offset3 = half3(0 * scale, -1.5 * scale, 0);

    half3 input = half3(uv.x, 1 - uv.y, comparison + bias);

    half shadowDepth0 = SAMPLE_TEXTURE2D_SHADOW(tex, textureSampler, input + offset0).r;
    half shadowDepth1 = SAMPLE_TEXTURE2D_SHADOW(tex, textureSampler, input + offset1).r;
    half shadowDepth2 = SAMPLE_TEXTURE2D_SHADOW(tex, textureSampler, input + offset2).r;
    half shadowDepth3 = SAMPLE_TEXTURE2D_SHADOW(tex, textureSampler, input + offset3).r;

    return (shadowDepth0 + shadowDepth1 + shadowDepth2 + shadowDepth3) * 0.25;
}

half GetShadow(float4 shadowCasterPos0, float4 shadowCasterPos1, half3 worldNormal, half3 lightDir)
{
    //Shadows
    //Todo: move this to vertex interpolator
    half3 shadowPos0 = shadowCasterPos0.xyz / shadowCasterPos0.w;
    half2 shadowUV0 = shadowPos0.xy * 0.5 + 0.5;

    if (shadowUV0.x < 0 || shadowUV0.x > 1 || shadowUV0.y < 0 || shadowUV0.y > 1)
    {
        //Check the distant cascade
        half3 shadowPos1 = shadowCasterPos1.xyz / shadowCasterPos1.w;
        half2 shadowUV1 = shadowPos1.xy * 0.5 + 0.5;

        if (shadowUV1.x < 0 || shadowUV1.x > 1 || shadowUV1.y < 0 || shadowUV1.y > 1)
        {
            return 1;
        }
        
         // Compare depths (shadow caster and current pixel)
        half sampleDepth1 = -shadowPos1.z * 0.5f + 0.5f;
        //half bias = 0.0001;
        //sampleDepth1 += bias;

        half3 input = half3(shadowUV1.x, 1 - shadowUV1.y, sampleDepth1);
        half shadowFactor = SAMPLE_TEXTURE2D_SHADOW(_GlobalShadowTexture1, sampler_GlobalShadowTexture1, input);
        
        //Quick darkening
        //float range = 0.42;
        //shadowFactor = min(smoothstep(range, range + 0.05, saturate(dot(worldNormal, -lightDir))), shadowFactor);
        
        return shadowFactor;
    }
    else
    {
        // Compare depths (shadow caster and current pixel)
        half sampleDepth0 = -shadowPos0.z * 0.5f + 0.5f;
        //half bias = 0.0001;
        half shadowFactor0 = GetShadowSample(_GlobalShadowTexture0, sampler_GlobalShadowTexture0, shadowUV0, 0, sampleDepth0);
        
        //Quick darkening
        //float range = 0.42;
        //shadowFactor0 = min(smoothstep(range, range + 0.05, saturate(dot(worldNormal, -lightDir))), shadowFactor0);
        
        return shadowFactor0;
    }
}

float CalculatePointLight(float3 worldPos, float3 normal, float3 lightPos, float4 lightColor,  float lightRange)
{
    float3 lightVec = lightPos - worldPos;
    half distance = length(lightVec);
    half3 lightDir = normalize(lightVec);

    //float RoL = max(0, dot(reflectionVector, lightDir));
    float NoL = max(0, dot(normal, lightDir));

    float distanceNorm = saturate(distance / lightRange);
    float falloff = distanceNorm * distanceNorm * distanceNorm;
    falloff = 1.0 - falloff;

    //falloff *= NoL;

    //half3 result = falloff * (albedo * lightColor + specularColor * PhongApprox(roughness, RoL));
    float result = NoL * falloff;
    return result;
}

inline half3 CalculateAtmosphericFog(half3 currentFragColor, float viewDistance)
{
    // Calculate fog factor
    float fogFactor = saturate((globalFogEnd - viewDistance) / (globalFogEnd - globalFogStart));

    fogFactor = pow(fogFactor, 2);

    // Mix current fragment color with fog color
    half3 finalColor = lerp(globalFogColor, currentFragColor, fogFactor);

    return finalColor;
}


half3 RimLight(half3 normal, half3 viewDir, half3 lightDir)
{
    float rim = 1 - dot(normal, lightDir);
    rim = pow(rim, _RimPower);
    rim *= _RimIntensity;
    rim = saturate(rim);

    return _RimColor.rgb * rim;
}

half3 RimLightSimple(half3 normal, half3 viewDir)
{
    float rim = 1 - dot(normal, viewDir);
    rim = pow(rim, _RimPower);
    rim *= _RimIntensity;
    rim = saturate(rim);

    return _RimColor.rgb * rim;
}

float RimLightDelta(half3 normal, half3 viewDir, float power, float intensity)
{
    float rim = 1 - dot(normal, viewDir);
    rim = pow(rim, power);
    rim *= intensity;
    rim = saturate(rim);
    return rim;
}

inline half3 SampleAmbientSphericalHarmonics(half3 nor)
{
    const float c1 = 0.429043;
    const float c2 = 0.511664;
    const float c3 = 0.743125;
    const float c4 = 0.886227;
    const float c5 = 0.247708;
    return (
        c1 * globalAmbientLight[8].xyz * (nor.x * nor.x - nor.y * nor.y) +
        c3 * globalAmbientLight[6].xyz * nor.z * nor.z +
        c4 * globalAmbientLight[0].xyz -
        c5 * globalAmbientLight[6].xyz +
        2.0 * c1 * globalAmbientLight[4].xyz * nor.x * nor.y +
        2.0 * c1 * globalAmbientLight[7].xyz * nor.x * nor.z +
        2.0 * c1 * globalAmbientLight[5].xyz * nor.y * nor.z +
        2.0 * c2 * globalAmbientLight[3].xyz * nor.x +
        2.0 * c2 * globalAmbientLight[1].xyz * nor.y +
        2.0 * c2 * globalAmbientLight[2].xyz * nor.z
        );
}

float ConvertFromNormalizedRange(float normalizedNumber)
{
    return normalizedNumber * 2 - 1;
}

float ConvertToNormalizedRange(float negativeRangeNumber)
{
    return (negativeRangeNumber + 1) / 2;
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
#endif