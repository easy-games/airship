Shader "Airship/PostProcess/HorizontalBlurShader"
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
         
            float2 _BlurScale;
            float2 _TextureSize;
           
            struct Attributes
            {
                float4 positionHCS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            }; 

            struct Varyings
            {
                float4  positionCS  : SV_POSITION; 
                float2  uv0          : TEXCOORD0;
                float2  uv1          : TEXCOORD1;
                float2  uv2          : TEXCOORD2;
                float2  uv3          : TEXCOORD3;
                float2  uv4          : TEXCOORD4;
                float2  uv5          : TEXCOORD5;
                float2  uv6          : TEXCOORD6;
                float2  uv7          : TEXCOORD7;
                float2  uv8          : TEXCOORD8;
                
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

                
                output.uv0 = input.uv;
                
                //Flip things based on the render target (editor or gamewindow!)
#if UNITY_UV_STARTS_AT_TOP
                output.uv0.y = 1.0 - output.uv0.y;
#endif
                if (_ProjectionParams.x < 0.0)
                {
                    output.uv0.y = 1.0 - output.uv0.y;
                }
                
                //Calculate the offset of 1 pixel
				float2 offset = float2(1.0 / _TextureSize.x, 1.0 / _TextureSize.y);
                
                offset *= _BlurScale;

                output.uv1 = output.uv0 - offset * 7;
                output.uv2 = output.uv0 - offset * 5;
                output.uv3 = output.uv0 - offset * 3;
                output.uv4 = output.uv0 - offset * 1;
                                                    
                output.uv5 = output.uv0 + offset * 1;
                output.uv6 = output.uv0 + offset * 3;
                output.uv7 = output.uv0 + offset * 5;
                output.uv8 = output.uv0 + offset * 7;
 
                return output;
            }
            

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				
                //Kernel is the one used in the default bloom for unity
                half3 c0 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv1);
                half3 c1 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv2);
                half3 c2 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv3);
                half3 c3 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv4);
                half3 c4 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv0);
                half3 c5 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv5);
                half3 c6 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv6);
                half3 c7 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv7);
                half3 c8 = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv8);

                half3 colorSample = c0 * 0.01621622 + c1 * 0.05405405 + c2 * 0.12162162 + c3 * 0.19459459
                    + c4 * 0.22702703
                    + c5 * 0.19459459 + c6 * 0.12162162 + c7 * 0.05405405 + c8 * 0.01621622;

 
                return half4(colorSample,1);
            }
            ENDHLSL
        }
    }
}
