Shader "Chronos/WorldShaderPBR"
{
    
    Properties
    {
        [HDR] _Color("Color", Color) = (1,1,1,1)
        _Alpha("Alpha", Float) = 1.0
        [HDR] _OverrideColor("Override Color", Color) = (1,1,1,1)
        _OverrideStrength("Override Strength", Range(0,1)) = 0
        
        [Toggle(EXPLICIT_MAPS)] EXPLICIT_MAPS("Not using atlas", Float) = 1.0
        _MainTex("Albedo", 2D) = "white" {}
        _NormalTex("Normal", 2D) = "gray" {}
        _MetalTex("Metal", 2D) = "black" {}
        _RoughTex("Rough", 2D) = "white" {}
        _CubeTex("Cube", Cube) = "white" {}
        _EmissiveMaskTex("Emissive Mask", 2D) = "white" {}
        
        [Toggle] _ZWrite("Z-Write", Float) = 1.0
       
        [Toggle] POINT_FILTER("Use Stylized Point Filtering", Float) = 1.0
        [KeywordEnum(OFF, LOCAL, WORLD)] TRIPLANAR_STYLE("Triplanar", Float) = 0.0
        _TriplanarScale("TriplanarScale", Range(0.0, 16)) = 0.0
            
        [KeywordEnum(LIGHTS0, LIGHTS1, LIGHTS2)] NUM_LIGHTS("NumLights", Float) = 0.0
        [Toggle] SLIDER_OVERRIDE("Use Metal/Rough Sliders", Float) = 1.0
 
        _MetalOverride("Metal", Range(0.0, 1)) = 0.0
        _RoughOverride("Rough", range(0.04, 1)) = 0.04 
      
        [Toggle] EMISSIVE("Emissive", Float) = 0.0
        [HDR] _EmissiveColor("Emissive Color", Color) = (1,1,1,1) 
        _EmissiveMix("Emissive/Albedo Mix", range(0, 1)) = 1.0
            
        [Toggle] VERTEX_LIGHT("Has Baked Vertex Shadows", Float) = 0.0
    }

    SubShader
    {
        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Name "Forward"
            Tags { "LightMode" = "ChronosForwardPass" "Queue" = "Opaque"}

            //Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
              
            HLSLPROGRAM
            #pragma multi_compile NUM_LIGHTS_LIGHTS0 NUM_LIGHTS_LIGHTS1 NUM_LIGHTS_LIGHTS2
            #pragma multi_compile TRIPLANAR_STYLE_OFF TRIPLANAR_STYLE_LOCAL TRIPLANAR_STYLE_WORLD
            #pragma multi_compile _ SLIDER_OVERRIDE_ON
            #pragma multi_compile _ VERTEX_LIGHT_ON
            #pragma multi_compile _ EXPLICIT_MAPS_ON
            #pragma multi_compile _ POINT_FILTER_ON
			#pragma multi_compile _ EMISSIVE_ON
	   
            #include "ChronosWorldShaderIncludes.hlsl"
                         
            ENDHLSL
        }
      
    }
    
}