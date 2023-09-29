 

Shader "Hidden/AirshipErrorShader"
{

    Properties
    {
        // Blending state
      [HideInInspector] _ZWrite("__zw", Float) = 1.0
    }
    
    SubShader
    {
        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Name "Error"
            Tags { "LightMode" = "AirshipForwardPass" "Queue" = "Opaque"}
        
            ZWrite[_ZWrite]

            HLSLPROGRAM
            #pragma vertex vertFunction
            #pragma fragment fragFunction

            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;
 
        
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normal   : NORMAL;
            };

            struct vertToFrag
            {
                float4 positionCS : SV_POSITION;
                float4 localPos : TEXCOORD1;
				float3 color: COLOR;
            };

            float3 DoLight(float3 direction, float3 color, float3 normal)
            {
                
                return clamp(dot(-normalize(direction), normal), 0, 1) * color;
            }

            vertToFrag vertFunction(Attributes input)
            {
                vertToFrag output;
                float4 worldPos = mul(unity_ObjectToWorld, input.positionOS);

                output.positionCS = mul(unity_MatrixVP, worldPos);
			 
                //do a couple fake lights
                float3 lighting = float3(0, 0, 0);

                //Ambient
                lighting += DoLight(float3(-1.5, -1, -1), float3(0.5, 0.5, 0.49), input.normal);
                lighting += DoLight(float3(1.5, 1, 1), float3(0.49, 0.49, 0.5), input.normal);

                //top
				lighting += DoLight(float3(0, -1, 0), float3(0.9,0.9,1)*0.2, input.normal);
                
                output.color = lighting;
                output.localPos = float4(input.positionOS.xyz,0);
                return output;
            }

        
        
             
            float4 fragFunction(vertToFrag input) : SV_TARGET
            {
			   float3 color = input.color;
                                
                return float4(color.x, color.y, color.z ,1);
            }
            ENDHLSL
        }
    }
}