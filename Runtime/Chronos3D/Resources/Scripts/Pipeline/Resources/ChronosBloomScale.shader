Shader "Chronos/PostProcess/BloomScale"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        ZTest Always
        Pass
        {
            Name "ColorBlitPass" 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
         

            float4 _CameraOpaqueTexture_TexelSize;
            float BloomCutoff;
    
            struct Attributes
            {
                float4 positionHCS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            }; 

            struct Varyings
            {
                float4  positionCS  : SV_POSITION; 
                float2  uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

      
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Note: The pass is setup with a mesh already in clip
                // space, that's why, it's enough to just output vertex
                // positions 
                output.positionCS = float4(input.positionHCS.xyz, 1.0);

                float2 texelSize = float2(_CameraOpaqueTexture_TexelSize.x, _CameraOpaqueTexture_TexelSize.y);
                output.uv = input.uv - (texelSize / 2);
                                
                //Flip things based on the render target (editor or gamewindow!)
#if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1.0 - output.uv.y;
#endif
                if (_ProjectionParams.x < 0.0)
                {
                    output.uv.y = 1.0 - output.uv.y;
                }
                
                return output;
            }
            

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

      
            half4 thresholdedValue(half4 value, float threshold)
            {
                half4 ratio = saturate((value - threshold) / threshold);
                return ratio * value;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 colorSample = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv);

                //Perceptual luminance
                //float luminance = dot(colorSample.rgb, float3(0.299, 0.587, 0.114));
           
                //Soften the falloff just a bit
                //float threshold = BloomCutoff - 0.01;
                //float softThreshold = smoothstep(threshold, BloomCutoff, luminance);
                ///float3 thresholdedColor = softThreshold * colorSample.rgb;
                       
                return colorSample;
                //return half4(thresholdedColor, 1.0);
            }
            ENDHLSL
        }
    }
}