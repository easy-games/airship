#ifndef WORLDSHADER_INCLUDE
#define WORLDSHADER_INCLUDE
    //Main programs
    #pragma vertex vertFunction
    #pragma fragment fragFunction

    //Multi shader vars (you need these even if you're not using them, so that material properties can survive editor script reloads)
    float VERTEX_LIGHT;  
    float SLIDER_OVERRIDE;
    float POINT_FILTER;
    float EXPLICIT_MAPS;
    float EMISSIVE;
    float RIM_LIGHT;
    float INSTANCE_DATA;
    
    //Unity stuff
    float4x4 unity_MatrixVP;
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_WorldTransformParams;
    float3 _WorldSpaceCameraPos;

    //SamplerState sampler_MainTex;
    SamplerState my_sampler_point_repeat;
    SamplerState my_sampler_trilinear_repeat;
    
    Texture2D _MainTex;
    float4 _MainTex_TexelSize;
    Texture2D _NormalTex;
    Texture2D _MetalTex;
    Texture2D _RoughTex;
    Texture2D _EmissiveMaskTex;
    
    
    Texture2D _GlobalShadowTexture0;
    Texture2D _GlobalShadowTexture1;
    SamplerComparisonState sampler_GlobalShadowTexture0;
    SamplerComparisonState sampler_GlobalShadowTexture1;
 

    samplerCUBE _CubeTex;

    float  _Alpha;
    float4 _Color;
    float4 _SpecularColor;
    float4 _OverrideColor;
    float _OverrideStrength;
    float4 _EmissiveColor;
    half _EmissiveMix;
    float4 _Time;

    float4 _ProjectionParams;
    float4 _MainTex_ST;

    float _SkyShineStrength;
    float4 _SkyShineColor;

    half3 globalFogColor;
    float globalFogStart;
    float globalFogEnd;

    half _MetalOverride;
    half _RoughOverride;
    
    half _TriplanarScale;
     

    half _RimPower;
    half _RimIntensity;
    half4 _RimColor;

    
    //Lights
    float4 globalDynamicLightColor[2];
    float4 globalDynamicLightPos[2];
    float globalDynamicLightRadius[2];

    //Instance data (for this material)
    float4 _ColorInstanceData[16];

    //properties from the system
    half3 globalAmbientLight[9];
    half3 globalAmbientTint;

    half3 globalSunLight[9];
    half3 globalSunDirection = normalize(half3(-1, -3, 1.5));

    float globalAmbientBrightness;
    float globalSunBrightness;

    half3 globalSunColor;

    half globalAmbientOcclusion = 0;

    static const float emissiveConstant = 4.0;
    static const half3 specularColor = half3(1, 1, 1);
    static const float maxMips = 8;//This is how many miplevels the cubemaps have

    //shadows
    float4x4 _ShadowmapMatrix0;
    float4x4 _ShadowmapMatrix1;

    struct Attributes
    {
        float4 positionOS   : POSITION;
        float4 color   : COLOR;
        float3 normal : NORMAL;
        float4 tangent: TANGENT;
        float4 uv_MainTex : TEXCOORD0;
        float2 bakedLightA : TEXCOORD1;
        float2 bakedLightB : TEXCOORD2;

        float2 instanceIndex : TEXCOORD7;
    };

    struct vertToFrag
    {
        float4 positionCS : SV_POSITION;

        float4 color      : COLOR;
        float4 baseColor : TEXCOORD0;
        float4 uv_MainTex : TEXCOORD1;
        float3 worldPos   : TEXCOORD2;

        half3  tspace0 : TEXCOORD3;
        half3  tspace1 : TEXCOORD4;
        half3  tspace2 : TEXCOORD5;
        
        float4 bakedLight : TEXCOORD6;

        half3 triplanarBlend : TEXCOORD7;
        float3 triplanarPos : TEXCOORD8;

        float4 shadowCasterPos0 :TEXCOORD9;
        float4 shadowCasterPos1 :TEXCOORD10;

        half3 worldNormal : TEXCOORD11;
        

    };

    inline float3 UnityObjectToWorldNormal(in float3 dir)
    {
        return normalize(mul(dir, (float3x3)unity_WorldToObject));
    }

    inline float3 UnityObjectToWorldDir(in float3 dir)
    {
        return normalize(mul((float3x3)unity_ObjectToWorld, dir));
    }

    inline float4 ComputeScreenPos(float4 pos) {
        float4 o = pos * 0.5f;
        o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
        o.zw = pos.zw;
        return o;
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


    vertToFrag vertFunction(Attributes input)
    {
        vertToFrag output;
        float4 worldPos = mul(unity_ObjectToWorld, input.positionOS);
        output.positionCS = mul(unity_MatrixVP, worldPos);

        // Transform the normal to world space and normalize it
        float3 shadowNormal = normalize(mul(float4(input.normal, 0.0), unity_WorldToObject).xyz);

        // Apply the adjusted offset
        output.shadowCasterPos0 = mul(_ShadowmapMatrix0, worldPos +float4((shadowNormal * 0.03),0));
        output.shadowCasterPos1 = mul(_ShadowmapMatrix1, worldPos +float4((shadowNormal * 0.06),0));
                
        output.uv_MainTex = input.uv_MainTex;
        output.uv_MainTex = float4((input.uv_MainTex * _MainTex_ST.xy + _MainTex_ST.zw).xy, 1, 1);

    //Local triplanar
#ifdef TRIPLANAR_STYLE_LOCAL
        float3 scale;
        scale.x = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
        scale.y = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
        scale.z = length(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
        output.triplanarPos = float4((input.positionOS * _TriplanarScale + _MainTex_ST.zzz).xyz * scale, 1);
#endif

        //World triplanar
#ifdef TRIPLANAR_STYLE_WORLD
        output.triplanarPos = float4((worldPos * _TriplanarScale + _MainTex_ST.zzz).xyz, 1);
#endif    

        //tex.uv* _MainTex_ST.xy + _MainTex_ST.zw;

        output.bakedLight = float4(input.bakedLightA.x, input.bakedLightA.y, input.bakedLightB.x, 0);

        output.worldPos = worldPos;
        output.color = input.color;
        output.baseColor = lerp(_Color, _OverrideColor, _OverrideStrength);

        //Do ambient occlusion at the vertex level, encode it into vertex color g
        //But only if we're part of the world geometry...
#if VERTEX_LIGHT_ON
        output.color.g = clamp(output.color.g + (1 - globalAmbientOcclusion), 0, 1);
#endif        


#if INSTANCE_DATA_ON
		float4 instanceColor = _ColorInstanceData[input.instanceIndex.x];
        output.color *= instanceColor;
#endif


        //output.screenPosition = ComputeScreenPos(output.positionCS);
 
        float3 normalWorld = normalize(mul(float4(input.normal, 0.0), unity_WorldToObject).xyz);
        float3 tangentWorld = normalize(mul(unity_ObjectToWorld, input.tangent.xyz));
         
        half tangentSign = input.tangent.w * unity_WorldTransformParams.w;
        float3 binormalWorld = cross(normalWorld, tangentWorld) * tangentSign;

        output.tspace0 = half3(tangentWorld.x, binormalWorld.x, normalWorld.x);
        output.tspace1 = half3(tangentWorld.y, binormalWorld.y, normalWorld.y);
        output.tspace2 = half3(tangentWorld.z, binormalWorld.z, normalWorld.z);

        output.worldNormal = normalWorld;
    
        //calculate the triplanar blend based on the local normal   
#ifdef TRIPLANAR_STYLE_LOCAL
        //Localspace triplanar
        output.triplanarBlend = normalize(abs(input.normal));
        output.triplanarBlend /= dot(output.triplanarBlend, (half3)1);
        
        
#endif

#ifdef TRIPLANAR_STYLE_WORLD
        //Worldspace triplanar
        output.triplanarBlend = normalize(abs(normalWorld));
        output.triplanarBlend /= dot(output.triplanarBlend, (half3)1);
        
#endif    
        return output;
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

    half3 SampleSunSphericalHarmonics(half3 nor)
    {
        const float c1 = 0.429043;
        const float c2 = 0.511664;
        const float c3 = 0.743125;
        const float c4 = 0.886227;
        const float c5 = 0.247708;
        return (
            c1 * globalSunLight[8].xyz * (nor.x * nor.x - nor.y * nor.y) +
            c3 * globalSunLight[6].xyz * nor.z * nor.z +
            c4 * globalSunLight[0].xyz -
            c5 * globalSunLight[6].xyz +
            2.0 * c1 * globalSunLight[4].xyz * nor.x * nor.y +
            2.0 * c1 * globalSunLight[7].xyz * nor.x * nor.z +
            2.0 * c1 * globalSunLight[5].xyz * nor.y * nor.z +
            2.0 * c2 * globalSunLight[3].xyz * nor.x +
            2.0 * c2 * globalSunLight[1].xyz * nor.y +
            2.0 * c2 * globalSunLight[2].xyz * nor.z
            );
    }

    half Smooth(half inputValue, half transitionWidth)
    {
        half thresholdMin = 0.0;
        half thresholdMax = 1.0 - transitionWidth;
        half t = saturate((inputValue - thresholdMin) / transitionWidth);
        return smoothstep(0, 1, t);
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

    static float4 color1 = float4(1, 0, 0, 1); // red
    static float4 color2 = float4(1, 0.5, 0, 1); // orange
    static float4 color3 = float4(1, 1, 0, 1); // yellow
    static float4 color4 = float4(0, 1, 0, 1); // green
    static float4 color5 = float4(0, 0, 1, 1); // blue
    static float4 color6 = float4(0.5, 0, 1, 1); // purple
    static float4 color7 = float4(1, 0, 1, 1); // pink
    static float4 color8 = float4(0, 1, 1, 1);  // teal
    static float4 color9 = float4(1, 0, 1, 1); //more purple
    static float4 color10 = float4(0, 1, 0.5, 1); // ,0. 5,1); //more green
    static float4 color11 = float4(1, 1, 1, 1); // white

    float4 debugColor(float blendValue)
    {
        float4 color;
        if (blendValue < 1)
            color = color1;
        else if (blendValue < 2)
            color = lerp(color2, color3, blendValue - 1);
        else if (blendValue < 3)
            color = lerp(color3, color4, blendValue - 2);
        else if (blendValue < 4)
            color = lerp(color4, color5, blendValue - 3);
        else if (blendValue < 5)
            color = lerp(color5, color6, blendValue - 4);
        else if (blendValue < 6)
            color = lerp(color6, color7, blendValue - 5);
        else if (blendValue < 7)
            color = lerp(color7, color8, blendValue - 6);
        else if (blendValue < 8)
            color = lerp(color8, color9, blendValue - 7);
        else if (blendValue < 9)
            color = lerp(color9, color10, blendValue - 8);
        else
            color = color11;
        return color;
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
    half4 SRGBtoLinear(half4 srgb)
    {
        return pow(srgb, 0.4545454545);
    }
    half4 LinearToSRGB(half4 srgb)
    {
        return pow(srgb, 2.2333333);
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
        float3 pos;
        half3 triplanarBlend;
    };

    //Fancy filter that blends between point sampling and bilinear sampling
    half4 Tex2DSampleTexture(Texture2D tex, Coordinates coords)
    {
#if defined(TRIPLANAR_STYLE_LOCAL) || defined(TRIPLANAR_STYLE_WORLD)
		//float2 tx = coords.pos.yz; --Why is zy correct here for some things, and yz for others??
        //
        float2 tx = coords.pos.zy;
        float2 ty = coords.pos.xz;
        float2 tz = coords.pos.xy;

        half4 cx = tex.Sample(my_sampler_trilinear_repeat, tx) * coords.triplanarBlend.x;
        half4 cy = tex.Sample(my_sampler_trilinear_repeat, ty) * coords.triplanarBlend.y;
        half4 cz = tex.Sample(my_sampler_trilinear_repeat, tz) * coords.triplanarBlend.z;
        half4 color = (cx + cy + cz);
        return color;
#endif

#ifdef POINT_FILTER_ON

        const half bias = 0.0;//magic number, bigger value pushes the transition back further
        half blendValue = saturate((coords.lod) - bias);
        half4 pixelSample = tex.Sample(my_sampler_point_repeat, coords.uvs);

        float mipMap = coords.lod;
        mipMap = min(mipMap, 5); //clamp it because metal doesnt support partial mipmap chains (also possibly others)
        
        half4 filteredSample = tex.SampleLevel(my_sampler_trilinear_repeat, coords.uvs, mipMap);

        half4 blend = lerp(pixelSample, filteredSample, blendValue);

        //return half4(blend.x, blend.y, blendValue ,1);
        return blend;
#elif !defined(EXPLICIT_MAPS_ON) && defined(SHADER_API_METAL) //Metal specific fix
        float mipMap = coords.lod;
        mipMap = min(mipMap, 5); //clamp it because metal doesnt support partial mipmap chains (also possibly others)
        
        half4 filteredSample = tex.SampleLevel(my_sampler_trilinear_repeat, coords.uvs, mipMap);
        return filteredSample;
#else
        return tex.Sample(my_sampler_trilinear_repeat, coords.uvs);
#endif
    }
    //Unity encoded normals (the pink ones)
    half3 UnpackNormalmapRGorAG(half4 packednormal)
    {
        packednormal.x *= packednormal.w;

        half3 normal;
        normal.xy = packednormal.xy * 2 - 1;
        normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));

        return normal;
    }

    float3 BlendTriplanarNormal(float3 mappedNormal, float3 surfaceNormal) 
    {
        float3 n;
        n.xy = mappedNormal.xy + surfaceNormal.xy;
        n.z = mappedNormal.z * surfaceNormal.z;
        return n;
    }

    half4 TriplanarMapNormal(Texture2D tex, Coordinates coords, half3 worldNormal)
    {
        //The trick here is to realize we can just swizzle the normals to get the correct X Y or Z transform
        half2 triUVx = coords.pos.zy;
        half2 triUVy = coords.pos.xz;
        half2 triUVz = coords.pos.xy;
        
        //Todo: Flip for the negative faces?
        half3 tangentNormalX = UnpackNormalmapRGorAG(tex.Sample(my_sampler_trilinear_repeat, triUVx));
        half3 tangentNormalY = UnpackNormalmapRGorAG(tex.Sample(my_sampler_trilinear_repeat, triUVy));
        half3 tangentNormalZ = UnpackNormalmapRGorAG(tex.Sample(my_sampler_trilinear_repeat, triUVz));

        //Blend with the 
        half3 worldNormalX = BlendTriplanarNormal(tangentNormalX, worldNormal.zyx).zyx * coords.triplanarBlend.x;
        half3 worldNormalY = BlendTriplanarNormal(tangentNormalY, worldNormal.xzy).xzy * coords.triplanarBlend.y;
        half3 worldNormalZ = BlendTriplanarNormal(tangentNormalZ, worldNormal) * coords.triplanarBlend.z;
        
        return half4(normalize(worldNormalX + worldNormalY + worldNormalZ),1);
    }


    half4 Tex2DSampleTextureUV(Texture2D tex, float4 uvs)
    {
        half4 cx = tex.Sample(my_sampler_trilinear_repeat, uvs);
        return cx;
    }




    half4 Tex2DSampleTextureDebug(Texture2D tex, Coordinates coords)
    {
        return debugColor(coords.lod);
    }

    half UnpackMetal(float metal)
    {
        return metal / 2.0;
    }

    half UnpackEmission(float metal)
    {
        float result = metal > 0.5;
        return result;
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

    half GetShadow(vertToFrag input, half3 worldNormal, half3 lightDir)
    {
        //Shadows
        //Todo: move this to vertex interpolator
        half3 shadowPos0 = input.shadowCasterPos0.xyz / input.shadowCasterPos0.w;
        half2 shadowUV0 = shadowPos0.xy * 0.5 + 0.5;
                
        if (shadowUV0.x < 0 || shadowUV0.x > 1 || shadowUV0.y < 0 || shadowUV0.y > 1)
        {
            //Check the distant cascade
            half3 shadowPos1 = input.shadowCasterPos1.xyz / input.shadowCasterPos1.w;
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
            //float range = 0.42;
            //shadowFactor = min(smoothstep(range, range + 0.05, saturate(dot(worldNormal, -lightDir))), shadowFactor);
            
            return shadowFactor;
        }
        else
        {
            // Compare depths (shadow caster and current pixel)
            half sampleDepth0 = -shadowPos0.z * 0.5f + 0.5f;
            //half bias = 0.0001;
            half shadowFactor0 =  GetShadowSample(_GlobalShadowTexture0, sampler_GlobalShadowTexture0, shadowUV0, 0, sampleDepth0);

            //Quick darkening
            //float range = 0.42;
            //shadowFactor0 = min(smoothstep(range, range+0.05, saturate(dot(worldNormal, -lightDir))), shadowFactor0);
            
            return shadowFactor0;
        }
    }



    void fragFunction(vertToFrag input, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
    {
        Coordinates coords;  
        coords.uvs = input.uv_MainTex.xy; 
#ifdef POINT_FILTER_ON
    float2 texture_coordinate = input.uv_MainTex.xy * _MainTex_TexelSize.zw;
    coords.ddx = ddx(texture_coordinate);
    coords.ddy = ddy(texture_coordinate);
    float delta_max_sqr = max(dot(coords.ddx, coords.ddx), dot(coords.ddy, coords.ddy));
    coords.lod = 0.5 * log2(delta_max_sqr);
#elif !defined(EXPLICIT_MAPS_ON) && defined(SHADER_API_METAL) //Metal specific fix
    float2 texture_coordinate = input.uv_MainTex.xy * _MainTex_TexelSize.zw;
    coords.ddx = ddx(texture_coordinate);
    coords.ddy = ddy(texture_coordinate);
    float delta_max_sqr = max(dot(coords.ddx, coords.ddx), dot(coords.ddy, coords.ddy));
    coords.lod = 0.5 * log2(delta_max_sqr);
#else
    coords.ddx = half2(0, 0);
    coords.ddy = half2(0, 0);
    coords.lod = 0;
#endif



#if defined(TRIPLANAR_STYLE_LOCAL) || defined(TRIPLANAR_STYLE_WORLD)
        coords.pos = input.triplanarPos;
        coords.triplanarBlend = input.triplanarBlend;
#endif    
   
        
        half4 texSample = Tex2DSampleTexture(_MainTex, coords);

        half3 textureNormal;
        half3 worldNormal;
        half4 skyboxSample;

        half metallicLevel;
        half roughnessLevel;
        half emissiveLevel = 0;
        half4 reflectedCubeSample;
        half3 worldReflect;
        half alpha = _Alpha;

        half3 viewVector = _WorldSpaceCameraPos.xyz - input.worldPos;
        float viewDistance = length(viewVector);
        half3 viewDirection = normalize(viewVector);

    
#if EXPLICIT_MAPS_ON

        half4 metalSample = Tex2DSampleTexture(_MetalTex, coords);
        half4 roughSample = Tex2DSampleTexture(_RoughTex, coords);

#if defined(TRIPLANAR_STYLE_LOCAL) || defined(TRIPLANAR_STYLE_WORLD)
        worldNormal = TriplanarMapNormal(_NormalTex, coords, input.worldNormal);
        
#else
        //Path used by anything passing in explicit maps like triplanar materials
        half4 normalSample = (Tex2DSampleTexture(_NormalTex, coords));
        textureNormal = (UnpackNormalmapRGorAG(normalSample)); 
        textureNormal = normalize(textureNormal);
        
        worldNormal.x = dot(input.tspace0, textureNormal);
        worldNormal.y = dot(input.tspace1, textureNormal);
        worldNormal.z = dot(input.tspace2, textureNormal);
#endif
        
        alpha = texSample.a * _Alpha;

        //worldNormal = (worldNormal); //Normalize?
        worldReflect = reflect(-viewDirection, worldNormal);

        //Note to self: should try and sample reflectedCubeSample as early as possible
        roughnessLevel = max(roughSample.r, 0.04);
        metallicLevel = metalSample.r;
#if EMISSIVE_ON
        emissiveLevel = Tex2DSampleTexture(_EmissiveMaskTex, coords).r;
#else
		emissiveLevel = 0;
#endif
        
#else   //Path used by atlas rendering

#if defined(TRIPLANAR_STYLE_LOCAL) || defined(TRIPLANAR_STYLE_WORLD)
        worldNormal = TriplanarMapNormal(_NormalTex, coords, input.worldNormal);
        half4 specialSample = Tex2DSampleTexture(_NormalTex, coords);
#else
        //Path used by atlas rendering
        half4 specialSample = Tex2DSampleTexture(_NormalTex, coords);
        textureNormal = (TextureDecodeNormal(specialSample.xyz));
        textureNormal = normalize(textureNormal);

        worldNormal.x = dot(input.tspace0, textureNormal);
        worldNormal.y = dot(input.tspace1, textureNormal);
        worldNormal.z = dot(input.tspace2, textureNormal);
#else
        
#endif
        worldReflect = reflect(-viewDirection, worldNormal);

        //Note to self: should try and sample reflectedCubeSample as early as possible
        roughnessLevel = max(specialSample.a, 0.04);

        //metallic is packed
        metallicLevel = UnpackMetal(specialSample.b);
#if EMISSIVE_ON
        emissiveLevel = UnpackEmission(specialSample.b);
        _EmissiveMix = 0;
#endif        
 
#endif
   

        // Finish doing ALU calcs while the cubemap fetches in
#ifdef SLIDER_OVERRIDE_ON
        metallicLevel = (metallicLevel + _MetalOverride) / 2;
        roughnessLevel = (roughnessLevel + _RoughOverride) / 2;
#endif
        reflectedCubeSample = texCUBElod(_CubeTex, half4(worldReflect, roughnessLevel * maxMips));
        skyboxSample = texCUBE(_CubeTex, -viewDirection);
 
        half3 complexAmbientSample = SampleAmbientSphericalHarmonics(worldNormal);
        //half3 complexSunSample = SampleSunSphericalHarmonics(worldNormal);// *globalBrightness;
        
        
        //Shadows and light masks
        half sunShadowMask = GetShadow(input, worldNormal, globalSunDirection);
        //sunShadowMask = sunShadowMask *.8 + .2;//Never have shadows go full black
        half pointLight0Mask = 1;
        half pointLight1Mask = 1;
        half ambientShadowMask = 1;
    
        //half2 textureCoordinate = input.screenPosition.xy / input.screenPosition.w;

        //Sun
        half RoL = max(0, dot(worldReflect, -globalSunDirection));
        half NoV = max(dot(viewDirection, worldNormal), 0);
        half NoL = dot(-globalSunDirection, worldNormal); // -1 to 1
        
        NoL = saturate((NoL + 1)*.5); //Half Lambert
        // NoL = pow(NoL,4);

        half3 textureColor = texSample.xyz;

#if VERTEX_LIGHT_ON
        //If we're using baked shadows (voxel world geometry)
        //The input diffuse gets multiplied by the vertex color.r
        
        //Previously, this was the sun mask 
        //textureColor.rgb *= input.color.r;
        
        ambientShadowMask = input.color.g; //Creases
        pointLight0Mask = input.color.b;
        pointLight1Mask = input.color.a;
#else
        //Otherwise it gets multiplied by the whole thing
        textureColor.rgb *= input.color.rgb;
#endif  

        //Specular
        half3 specularColor;
        half3 diffuseColor;
        half dielectricSpecular = 0.08 * 0.3; //0.3 is the industry standard
        diffuseColor = textureColor - textureColor * metallicLevel;	// 1 mad
        specularColor = (dielectricSpecular - dielectricSpecular * metallicLevel) + textureColor * metallicLevel;	// 2 mad
        specularColor = EnvBRDFApprox(specularColor * _SpecularColor, roughnessLevel, NoV);
        /*        //Alternate material for when metal is totally ignored
            diffuseColor = textureColor;
            half specLevel = EnvBRDFApproxNonmetal(roughnessLevel, NoV);
            specularColor = half3(specLevel, specLevel, specLevel);
        */
        half3 imageSpecular = ProcessReflectionSample(reflectedCubeSample.xyz);

        //Start compositing it all now
        half3 finalColor = half3(0, 0, 0);
        half3 sunIntensity = half3(0, 0, 0);

        //Diffuse colors
        diffuseColor *= input.baseColor;
        half3 ibl = globalSunColor;
 
        half3 sunShine = (ibl * NoL * (diffuseColor + specularColor * PhongApprox(roughnessLevel, RoL)));
        sunShine += (NoL * imageSpecular * specularColor);
        
        //SH ambient 
        half3 ambientLight = (complexAmbientSample * globalAmbientTint);
        
#if VERTEX_LIGHT_ON
        half3 bakedLighting = input.bakedLight.xyz;
        ambientLight = max(ambientLight, bakedLighting);
#endif        
                

        //Incident Color
        float incidentAngle =  1-NoV;
        half3 skyShineColor = lerp(diffuseColor, _SkyShineColor, incidentAngle * incidentAngle * _SkyShineStrength * min(worldNormal.y + .3, 1));

        //Final color before lighting application
        half3 ambientFinal = (ambientLight * skyShineColor) + (imageSpecular * specularColor * ambientLight);

        //Sun mask
        float sunMask = sunShadowMask;
        if (NoL < 0.01) 
        {
          //  sunMask = 0;
        }
        // sunShadowMask = saturate(sunShadowMask + 0.5);
        finalColor = ((sunShine * sunShadowMask) + ambientFinal) * ambientShadowMask;
        

        //Point lights
#ifdef NUM_LIGHTS_LIGHTS1
        finalColor.xyz += CalculatePointLightForPoint(input.worldPos, worldNormal, diffuseColor, roughnessLevel, specularColor, worldReflect, globalDynamicLightPos[0], globalDynamicLightColor[0], globalDynamicLightRadius[0]) * pointLight0Mask;
#endif
#ifdef NUM_LIGHTS_LIGHTS2
        finalColor.xyz += CalculatePointLightForPoint(input.worldPos, worldNormal, diffuseColor, roughnessLevel, specularColor, worldReflect, globalDynamicLightPos[0], globalDynamicLightColor[0], globalDynamicLightRadius[0]) * pointLight0Mask;
        finalColor.xyz += CalculatePointLightForPoint(input.worldPos, worldNormal, diffuseColor, roughnessLevel, specularColor, worldReflect, globalDynamicLightPos[1], globalDynamicLightColor[1], globalDynamicLightRadius[1]) * pointLight1Mask;
#endif

        //Rim light
#ifdef RIM_LIGHT_ON
        finalColor.xyz += RimLightSimple(worldNormal, viewDirection);
#endif


        //Mix in fog
		finalColor = CalculateAtmosphericFog(finalColor, viewDistance);
        
        {
            //Assorted debug functions

            //finalColor.xyz = skyboxSample.xyz;
            //if (textureCoordinate.x < 0.5)
            //{
                //worldReflect = reflect(-viewDirection, worldNormal);
            //}
            //else
            //{
                //worldReflect = reflect(-viewDirection, normalize(input.worldNormal));
            //}

            //finalColor = cubeSample;
            //finalColor = texSample.rgb;
            //finalColor = texCUBE(_CubeTex, worldReflect);
            //half4 trueNormal = float4(EncodeNormal(normalize(input.worldNormal)), 1);
            //half4 texNormal = float4(EncodeNormal(normalize(worldNormal)), 1);

            //finalColor = float4(EncodeNormal(input.triplanarNormal), 1);
            //finalColor = texCUBE(_CubeTex, worldNormal);
            //finalColor = normalSample;

            //finalColor = float4(EncodeNormal(input.rawVertexNormal.xyz),0);
            //finalColor = float4(EncodeNormal(textureNormal.xyz), 0);
            //finalColor =  float4(EncodeNormal(worldNormal.xyz), 0);
            //finalColor.r = 0;// coords.uvs.x % 1;
            //finalColor.b = 0;// coords.uvs.y % 1;
            
            //finalColor =  ambientFinal;
            //finalColor = diffuseColor;
            //finalColor = half3(roughnessLevel, roughnessLevel, roughnessLevel);
            //finalColor = half3(metallicLevel, metallicLevel, metallicLevel);  
            //finalColor = half3(sunShadowMask, sunShadowMask, sunShadowMask);  
            //finalColor = half3(ambientShadowMask, ambientShadowMask, ambientShadowMask);
            //finalColor = diffuseColor;

            // ambientShadowMask
            //finalColor = half3(input.color.g, input.color.g, input.color.g); //occlusion

            //finalColor = complexSunSample;
            //finalColor = input.bakedLight.xyz;
            //finalColor = input.color;

            //finalColor = half3(specialSample.b, specialSample.b, specialSample.b);
        }
        
#ifdef EMISSIVE_ON  
        if (emissiveLevel > 0)
        {
            float3 colorMix = lerp(finalColor, textureColor * input.baseColor, _EmissiveMix);
            MRT0 = half4(colorMix.r, colorMix.g, colorMix.b, alpha);

            float3 emissiveMix = lerp(diffuseColor.rgb, _EmissiveColor.rgb, _EmissiveMix);
            MRT1 = half4(emissiveMix * _EmissiveColor.a, alpha);
        }
        else
        {
            MRT0 = half4(finalColor.r, finalColor.g, finalColor.b, alpha);

            //Choose emissive based on brightness values
            half brightness = max(max(finalColor.r, finalColor.g), finalColor.b) * (1 - roughnessLevel) * alpha;
       
            ///if (brightness > globalSunBrightness + globalAmbientBrightness)
            if (brightness > 0.85)
            {
                MRT1 = half4(finalColor.r, finalColor.g, finalColor.b, alpha);
            }
            else
            {
                MRT1 = half4(0, 0, 0, alpha);
            }
		
        }
#else
        MRT0 = half4(finalColor.r, finalColor.g, finalColor.b, alpha);

        //Choose emissive based on brightness values
        half brightness = max(max(finalColor.r, finalColor.g), finalColor.b) * (1 - roughnessLevel) * alpha;

        ///if (brightness > globalSunBrightness + globalAmbientBrightness)
        if (brightness > 0.85)
        {
            MRT1 = half4(finalColor.r, finalColor.g, finalColor.b, alpha);
        }
        else
        {
            MRT1 = half4(0, 0, 0, alpha);
        }
#endif        

        
    }

#endif
