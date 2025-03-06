Shader "Airship/AirshipCharacterAlpha"
{
    Properties
    {
        _Transparency ("Alpha", Range(0,1)) = 1.0
        _FresnelStrength ("Fresnel Strength", Range(0,5)) = 1.0
        _DitherSize ("Dither Size", float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Geometry-1" "RenderPipeline" = "UniversalPipeline"}
        ZWrite On

        Pass
        {
            Name "Alpha Only"
            Tags {"LightMode" = "CharacterAlpha"}
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            //#include "UnityCG.cginc"
            // The Core.hlsl file contains definitions of frequently used HLSL
            // macros and functions, and also contains #include references to other
            // HLSL files (for example, Common.hlsl, SpaceTransforms.hlsl, etc.).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct ModelInputData 
            {
                float4 vertexPos: POSITION;
                float4 normal: NORMAL;
            };
        
            struct VertToFrag
            {
                float4 vertexPos      : SV_POSITION;
                float4 screenPos     : TEXCOORD1;
                float4 worldPos     : TEXCOORD2;
                float4 fresnelDelta : TEXCOORD3;
            };

            float _FresnelStrength;
            float _Transparency;
            float _DitherSize;

            VertToFrag vert(ModelInputData modelData)
            {
                VertToFrag output;

                //Positions
                output.vertexPos = TransformObjectToHClip(modelData.vertexPos.xyz);
                output.screenPos = ComputeScreenPos(output.vertexPos);
                output.worldPos = mul(unity_ObjectToWorld, modelData.vertexPos);
            
                //Vectors
                const float3 worldNormal = TransformObjectToWorldNormal(modelData.normal);
                const float3 viewDir = normalize(GetWorldSpaceViewDir(output.worldPos));
                output.fresnelDelta = (1-dot(viewDir, worldNormal)) * _FresnelStrength;

                return output;
            }

            half4 frag(VertToFrag input) : SV_Target
            {
                //return half4(1,0,0,1);
                const float alpha = 1-saturate(_Transparency + _Transparency * (input.fresnelDelta * input.fresnelDelta));
                //alpha = _Transparency;
                //return input.fresnelDelta;

                float DITHER_THRESHOLDS[16] =
                {
                    1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
                    13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
                    4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
                    16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
                };
            
                float2 uv = input.vertexPos.xy / _ScaledScreenParams.xy;
                uv *= _ScreenParams.xy / _DitherSize;   
                uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;

                //return alpha;
                // Returns > 0 if not clipped, < 0 if clipped based
                // on the dither
                clip(alpha - DITHER_THRESHOLDS[index]);
            
                //return input.fresnelDelta;
                return half4(alpha,0,0,1);
            }
            ENDHLSL
        }
    }
}