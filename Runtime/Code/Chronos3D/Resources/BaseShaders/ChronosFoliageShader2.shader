Shader "Chronos/FoliageShader2"
{
    Properties
    {
        [Header(Coloring)]
        [HDR] _ColorA("Color A", Color) = (.1, .25, .1, 1)
        [HDR] _ColorB("Color B", Color) = (.2,.6,.1,1)
        [HDR] _ShadowColor("Shadow Color", Color) = (.1,.2,.1,1)
        [HDR] _FresnelColor("Rim Color", Color) = (.1,1,.1,1)
        _MainTex("Albedo", 2D) = "white" {}
        _TexColorStrength("Texture Color Strength", Range(0,1)) = 0

        [Header(Lighting)]
        _MinLight("Minimum Light", Range(0,1)) = .2
        _LightShimmerStrength("Light Shimmer Strength", Float) = 0
        _FresnelPower("Rim Power", Float) = 10
        _FresnelStrength("Rim Strength", Range(0,1)) = 1

        [Header(Deformation)]
        _DeformSpeed("Global Speed", Range(0,1)) = 1
        _DeformStrength("Global Strength", Range(0,1)) = 1
        _WindSpeed("Wind Speed", Float) = 3
        _WindStrength("Wind Strength", Float) = 1
        _FlutterSpeed("Flutter Speed", Float) = 1
        _FlutterStrength("Flutter Strength", Float) = 1

        [Header(Chronos)]
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
            Cull   Off

            HLSLPROGRAM

            //Multi shader vars
            #pragma vertex vertFunction
            #pragma fragment fragFunction
            
            #include "UnityCG.cginc"
            #include "Packages/gg.easy.airship/Runtime/Code/Chronos3D/Resources/BaseShaders/ChronosShaderIncludes.cginc"

            //Most of this shader lives in an include file
            #include "Packages/gg.easy.airship/Runtime/Code/Chronos3D/Resources/BaseShaders/ChronosFoliageShaderInclude.hlsl"

            void fragFunction(vertToFrag input, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
            {
                //Cutout alpha
                half4 texSample = _MainTex.Sample(my_sampler_point_repeat, input.uv_MainTex.xy);
                clip(texSample.a - 0.5);
                                
                //Cull based on global _Alpha
                float2 screenPos = (input.positionCS.xy * 0.5 + 0.5) * _ScreenParams.xy;
                half4 ditherTextureSample = _DitherTexture.Sample(my_sampler_point_repeat, screenPos.xy * _DitherTexture_TexelSize.xy);
                clip(ditherTextureSample.r - (1 - _Alpha));
                                   
                float fresnelDelta =  (1 - texSample.a) * input.fresnelValue;
                half4 diffuseColor =  lerp(half4(input.color.rgb, .5), texSample * input.color, _TexColorStrength);
                half4 fresnelColor = _FresnelColor * _FresnelStrength * fresnelDelta;
                half3 finalColor = lerp(_ShadowColor, diffuseColor, input.sunStrength) + fresnelColor;

                //fog
                half3 viewVector = _WorldSpaceCameraPos.xyz - input.worldPos;
                float viewDistance = length(viewVector);
                finalColor.xyz = CalculateAtmosphericFog(finalColor.xyz, viewDistance);

                MRT0 = half4(finalColor, 1);
                MRT1 = half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Name "Forward"
            Tags { "LightMode" = "ChronosShadowPass" "Queue" = "Opaque"}

            //Blend[_SrcBlend][_DstBlend]
            ZWrite On
            Cull   Off

            HLSLPROGRAM

            //Multi shader vars
            #pragma vertex vertFunction
            #pragma fragment fragFunction
            
            #include "UnityCG.cginc"
            #include "Packages/gg.easy.airship/Runtime/Code/Chronos3D/Resources/BaseShaders/ChronosShaderIncludes.cginc"

            //Most of this shader lives in an include file
            #include "Packages/gg.easy.airship/Runtime/Code/Chronos3D/Resources/BaseShaders/ChronosFoliageShaderInclude.hlsl"
                
            void fragFunction(vertToFrag input, out half4 MRT0 : SV_Target0 )
            {
                //Cutout alpha
                half4 texSample = _MainTex.Sample(my_sampler_point_repeat, input.uv_MainTex.xy);
                clip(texSample.a - 0.5);
                                                
                //Cull based on global _Alpha
                float2 screenPos = (input.positionCS.xy * 0.5 + 0.5) * _ScreenParams.xy;
                half4 ditherTextureSample = _DitherTexture.Sample(my_sampler_point_repeat, screenPos.xy * _DitherTexture_TexelSize.xy);
                clip(ditherTextureSample.r - (1 - _Alpha));
                
                MRT0 = half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
