// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

#ifndef CHRONOSSHADER_INCLUDE
#define CHRONOSSHADER_INCLUDE

//Lighting variables
half3 globalSunDirection = normalize(half3(-1, -3, 1.5));
float _SunScale = 1; //How much the sun is hitting you
half3 globalAmbientLight[9];//Global ambient values
//Point Lights
float NUM_LIGHTS; //Required for dynamic lights
float4 globalDynamicLightColor[2];
float4 globalDynamicLightPos[2];
float globalDynamicLightRadius[2];

half4 SRGBtoLinear(half4 srgb)
{
    return pow(srgb, 0.4545454545);
}
half4 LinearToSRGB(half4 srgb)
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
#endif