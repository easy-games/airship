Shader "Chronos/PostProcess/DownScale"
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
                output.uv = input.uv;
                                
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

            TEXTURE2D_X(_CameraOpaqueTextureMrt);
            SAMPLER(sampler_CameraOpaqueTextureMrt);

            float _Intensity;

            void frag(Varyings input, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                MRT0 = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv);
                MRT1 = SAMPLE_TEXTURE2D_X(_CameraOpaqueTextureMrt, sampler_CameraOpaqueTextureMrt, input.uv);
                
            }
            ENDHLSL
        }
    }
}