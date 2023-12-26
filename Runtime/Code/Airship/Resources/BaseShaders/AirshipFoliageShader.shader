Shader "Airship/FoliageShader"
{
    Properties
    {
        [Header(Coloring)]
        [HDR] _ColorA("Color A", Color) = (.1, .25, .1, 1)
        [HDR] _ColorB("Color B", Color) = (.2,.6,.1,1)
        
        _MainTex("Albedo", 2D) = "white" {}
        _TexColorStrength("Texture Color Strength", Range(0,1)) = 0

        [Header(Deformation)]
        _DeformSpeed("Global Speed", Range(0,1)) = 1
        _DeformStrength("Global Strength", Range(0,1)) = 1
        _WindSpeed("Wind Speed", Float) = 3
        _WindStrength("Wind Strength", Float) = 1
        _FlutterSpeed("Flutter Speed", Float) = 1
        _FlutterStrength("Flutter Strength", Float) = 1

        [Header(Airship)]
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
            Tags { "LightMode" = "AirshipForwardPass" "Queue" = "Opaque"}

            //Blend[_SrcBlend][_DstBlend]
            ZWrite On
            Cull   Off

            HLSLPROGRAM

            //Multi shader vars
            #pragma vertex simpleVertFunction
            #pragma fragment fragFunction
            
            #include "UnityCG.cginc"
            #include "Packages/gg.easy.airship/Runtime/Code/Airship/Resources/BaseShaders/AirshipShaderIncludes.cginc"

            //Most of this shader lives in an include file
            #include "Packages/gg.easy.airship/Runtime/Code/Airship/Resources/BaseShaders/AirshipFoliageShaderInclude.hlsl"

            void fragFunction(vertToFrag input, bool frontFacing : SV_IsFrontFace, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
            {
                //Cutout alpha
                half4 texSample = _MainTex.Sample(my_sampler_Linear_repeat, input.uv_MainTex.xy);
                
                clip(texSample.a - 0.1);
                                
                //Cull based on global _Alpha
                float2 screenPos = (input.positionCS.xy * 0.5 + 0.5) * _ScreenParams.xy;
                half4 ditherTextureSample = _DitherTexture.Sample(my_sampler_point_repeat, screenPos.xy * _DitherTexture_TexelSize.xy);
                clip(ditherTextureSample.r - (1 - _Alpha));
       
                //Color lerp
				half3 tex = lerp(half3(1, 1, 1), texSample.xyz, _TexColorStrength);
                
                //fog
                half3 viewVector = _WorldSpaceCameraPos.xyz - input.worldPos;
                half3 viewDirection = normalize(viewVector);
                
                float viewDistance = length(viewVector);
                half3 finalColor = CalculateAtmosphericFog(tex * input.color, viewDistance);
                
                MRT0 = half4(finalColor, 1);
                MRT1 = half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Name "Forward"
            Tags { "LightMode" = "AirshipShadowPass" "Queue" = "Opaque"}

            //Blend[_SrcBlend][_DstBlend]
            ZWrite On
            Cull   Off

            HLSLPROGRAM

            //Multi shader vars
            #pragma vertex simpleVertFunction
            #pragma fragment fragFunction
            
            #include "UnityCG.cginc"
            #include "Packages/gg.easy.airship/Runtime/Code/Airship/Resources/BaseShaders/AirshipShaderIncludes.cginc"

            //Most of this shader lives in an include file
            #include "Packages/gg.easy.airship/Runtime/Code/Airship/Resources/BaseShaders/AirshipFoliageShaderInclude.hlsl"
                
            void fragFunction(vertToFrag input, out half4 MRT0 : SV_Target0 )
            {
                //Cutout alpha
                half4 texSample = _MainTex.Sample(my_sampler_point_repeat, input.uv_MainTex.xy);
                clip(texSample.a - 0.1);
                                                
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
