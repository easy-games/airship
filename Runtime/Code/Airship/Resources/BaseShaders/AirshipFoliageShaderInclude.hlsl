SamplerState my_sampler_point_repeat;
SamplerState my_sampler_Linear_repeat;
Texture2D _MainTex;
Texture2D _DitherTexture;
float4 _DitherTexture_TexelSize;
float _TexColorStrength;
float _LightShimmerStrength;

float4 _MainTex_TexelSize;
float4 _RealTime;

float _MinLight;
float _FresnelPower;
float _FresnelStrength;

//Deformation
float _DeformSpeed;
float _DeformStrength;
float _WindSpeed;
float _WindStrength;
float _FlutterSpeed;
float _FlutterStrength;

float4 _ColorA;
float4 _ColorB;
float4 _ShadowColor;
float4 _FresnelColor;
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
    float4 color    : COLOR0;
    float4 backFaceColor : COLOR1;
    float4 uv_MainTex : TEXCOORD1;
    float3 worldPos : TEXCOORD2;
    half3 worldNormal : TEXCOORD3;
    float fresnelValue : TEXCOORD4;
    float sunStrength: TEXCOORD5;

    float4 shadowCasterPos0 : TEXCOORD6;
    float4 shadowCasterPos1 : TEXCOORD7;
    
};

//Make this vertex flap around in the wind using sin and cos
float4 Wind(float4 input, float scale, float vertexWindStrength, float vertexFlutterStrength)
{

    scale = (1 - scale) * _DeformStrength;

    float flutterPhase = _RealTime.x * _FlutterSpeed * _DeformSpeed;
    float windPhase = _RealTime.x * _WindSpeed * _DeformSpeed;

    //global wind flows
    float stretch = 0.25;
    float flutterStrength = clamp(1 + (1 + sin(input.x * stretch)) * 0.5, 0, 1) * scale * _FlutterStrength * vertexFlutterStrength;

    //Flutter - Subtle motion
    //use a sin circle to move this vertex
    input.x += (sin(flutterPhase + input.x * 200 + input.z * 100) * 0.2) * flutterStrength;
    input.y += (cos(flutterPhase + input.x * 250 + input.y * 350) * 0.3) * flutterStrength;
    input.z += (cos(flutterPhase + input.x * 150 + input.z * 150) * 0.2) * flutterStrength;

    //Wind - major wind motion (offset around a point+ sin)
    //float windMotion = 0.1 * _WindStrength * vertexWindStrength;
    //input.x += (-windMotion * 0.5 * scale) + (sin((windPhase * 4) + (input.x * 0.3)) * windMotion) * scale;

    return input;
}

float4 GrassColor(float lerpVal, float4 colora, float4 colorb)
{

    float4 color = lerp(colora, colorb, lerpVal);
    //half3 color = lerp(_ColorA, _ColorB, lerpVal);
    return color;
}

vertToFrag vertFunction(Attributes input)
{
    vertToFrag output;

    float4 originalWorldPos = mul(unity_ObjectToWorld, input.positionOS);
    
    float4 worldPos = Wind(originalWorldPos, 1 - input.uv_MainTex.y, 0.7, 0.7);
    
    output.positionCS = mul(unity_MatrixVP, worldPos);
    output.uv_MainTex = input.uv_MainTex;

    output.uv_MainTex = float4((input.uv_MainTex * _MainTex_ST.xy + _MainTex_ST.zw).xy, 1, 1);

    output.worldPos = worldPos;
    output.worldNormal = normalize( UnityObjectToWorldNormal(input.normal));
    
    float3 viewDir = normalize(WorldSpaceViewDir(input.positionOS));
     
    //float4 lighting = half4(max(input.color.rrr, SampleAmbientSphericalHarmonics(half3(0, 1, 0))), 1);
    float4 colorA = pow(_ColorA, 0.4545454545);
    float4 colorB = pow(_ColorB, 0.4545454545);
    float4 shadowColor = pow(_ShadowColor, 0.4545454545);
    
    float dotp0 = dot(normalize(-globalSunDirection), output.worldNormal);
    dotp0 = 0.5 * dotp0 + 0.5; // Remap from [-1,1] to [0,1]
    
    if (dotp0 > 0.5)
    {
        // Remap dotp0 from [0.5, 1] to [0, 1]
        float remappedValue = (dotp0 - 0.5) * 2.0;
        output.color.rgb = lerp(colorB, colorA, remappedValue);
    }
    else
    {
        // Remap dotp0 from [0, 0.5] to [0, 1]
        float remappedValue = dotp0 * 2.0;
        output.color.rgb = lerp(shadowColor, colorB, remappedValue);
    }
   
    //backcolor just has an assumed normal of -viewDir (imagine the object is just facing you)
    float dotp1 = dot(normalize(-globalSunDirection), viewDir);
    dotp1 = 0.5 * dotp1 + 0.5; // Remap from [-1,1] to [0,1]

    if (dotp1 > 0.5)
    {
        // Remap dotp0 from [0.5, 1] to [0, 1]
        float remappedValue = (dotp1 - 0.5) * 2.0;
        output.backFaceColor.rgb = lerp(colorB, colorA, remappedValue);
    }
    else
    {
        // Remap dotp0 from [0, 0.5] to [0, 1]
        float remappedValue = dotp1 * 2.0;
        output.backFaceColor.rgb = lerp(shadowColor, colorB, remappedValue);
    }
     
    //shadows
    // Transform the normal to world space and normalize it
    float3 shadowNormal = normalize(mul(float4(input.normal, 0.0), unity_WorldToObject).xyz);
    // Apply the adjusted offset
    output.shadowCasterPos0 = mul(_ShadowmapMatrix0, worldPos + float4((shadowNormal * 0.03), 0));
    output.shadowCasterPos1 = mul(_ShadowmapMatrix1, worldPos + float4((shadowNormal * 0.06), 0));
    

    //output.color.g = clamp(output.color.g + (1-globalAmbientOcclusion), 0, 1);
   
    //Sun Light
    float sunDot = saturate(dot(-globalSunDirection, output.worldNormal)); 
    output.sunStrength = max(_MinLight, sunDot);
    
    //Fresnel Outline
    
    //saturate(dot(globalSunDirection, viewDir)
    output.fresnelValue = (sunDot)  * RimLightDelta(output.worldNormal, viewDir, _FresnelPower, 1);

    return output;
}

vertToFrag simpleVertFunction(Attributes input)
{
    vertToFrag output;

    float4 originalWorldPos = mul(unity_ObjectToWorld, input.positionOS);
    
    float4 worldPos = Wind(originalWorldPos, 1 - input.uv_MainTex.y, 0.7, 0.7);
    
    output.positionCS = mul(unity_MatrixVP, worldPos);
    output.uv_MainTex = input.uv_MainTex;

    output.uv_MainTex = float4((input.uv_MainTex * _MainTex_ST.xy + _MainTex_ST.zw).xy, 1, 1);

    output.worldPos = worldPos;
    output.worldNormal = normalize(UnityObjectToWorldNormal(input.normal));

    float3 viewDir = normalize(WorldSpaceViewDir(input.positionOS));

    //float4 lighting = half4(max(input.color.rrr, SampleAmbientSphericalHarmonics(half3(0, 1, 0))), 1);
    float4 colorA = pow(_ColorA, 0.4545454545);
    float4 colorB = pow(_ColorB, 0.4545454545);
    float4 shadowColor = pow(_ShadowColor, 0.4545454545);
     
	output.color = GrassColor(input.uv_MainTex.y, colorA, colorB);

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

