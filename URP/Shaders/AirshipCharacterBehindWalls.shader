Shader "Airship/AirshipCharacterBehindWalls"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _ColorDelta ("Use Sceen Color", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Geometry-1" "RenderPipeline" = "UniversalPipeline"}
        ZWrite On        

        Pass
        {
            Name "BehindObject"
            Tags{"LightMode" = "CharacterBehindOpaque"}

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            
            float4 _BaseColor;
            float _ColorDelta;

            struct ModelInputData 
            {
                float4 vertexPos: POSITION;
            };
            
            struct VertToFrag
            {
                float4 vertexPos : SV_POSITION;
            };

            VertToFrag vert(ModelInputData modelData)
            {
                VertToFrag output;
                
                //Positions
                output.vertexPos = TransformObjectToHClip(modelData.vertexPos.xyz);

                return output;
            }

            half4 frag(VertToFrag input) : SV_Target
            {
                float2 color_sample_uv = GetNormalizedScreenSpaceUV(input.vertexPos);
                float4 screenColor = float4(SampleSceneColor(color_sample_uv), 1);
                return lerp(_BaseColor, screenColor * screenColor, _ColorDelta);
            }
            ENDHLSL
        }
    }

}