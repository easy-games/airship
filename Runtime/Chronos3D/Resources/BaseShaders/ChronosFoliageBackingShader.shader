//Shader for grass etc

Shader "Chronos/FoliageShaderBacking"
{
    
    Properties
    {
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        
        //lightmix range from 0..1
		_LightMix("LightMix", Range(0,1)) = 0.5
        
    }

    SubShader
    {
        Cull off
            
        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Name "Forward"
            Tags { "LightMode" = "ChronosForwardPass" "Queue" = "Opaque"}

            ZWrite On
 
            HLSLPROGRAM
            
            //Multi shader vars
            #pragma vertex vertFunction
            #pragma fragment fragFunction
            #pragma shader_feature EXPLICIT_MAPS_ON

            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;
            float4x4 unity_WorldToObject;
            
            
            float4 _Color;
            float4 _Time;
            half _Alpha = 1;
            
            //Ambient
            half3 globalAmbientLight[9];

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color   : COLOR;
            };

            struct vertToFrag
            {
                float4 positionCS : SV_POSITION;
                float4 color    : COLOR;
            };
 
            

            half4 SRGBtoLinear(half4 srgb)
            {
                return pow(srgb, 0.4545454545);
            }
            inline half3 SampleAmbientSphericalHarmonics(half3 direction)
            {
                half3 color = globalAmbientLight[0] * 0.282095; // Constant term

                color += globalAmbientLight[1] * 0.488603 * direction.y; // Y-direction
                color += globalAmbientLight[2] * 0.488603 * direction.z; // Z-direction
                color += globalAmbientLight[3] * 0.488603 * direction.x; // X-direction

                color += globalAmbientLight[4] * 1.092548 * direction.x * direction.y; // XY-direction
                color += globalAmbientLight[5] * 1.092548 * direction.y * direction.z; // YZ-direction
                color += globalAmbientLight[6] * 0.315392 * (3.0 * direction.z * direction.z - 1.0); // Z^2-direction
                color += globalAmbientLight[7] * 1.092548 * direction.x * direction.z; // XZ-direction
                color += globalAmbientLight[8] * 0.546274 * (direction.x * direction.x - direction.y * direction.y); // X^2-Y^2 direction

                return color;
            }

 

            vertToFrag vertFunction(Attributes input)
            {
                vertToFrag output;
                                
                float4 worldPos = mul(unity_ObjectToWorld, input.positionOS);
                
            
                    
                output.positionCS = mul(unity_MatrixVP, worldPos);
               
                half4 lighting = half4(max(input.color.rrr, SampleAmbientSphericalHarmonics(half3(0, 1, 0))),1) ;
                output.color = SRGBtoLinear(_Color) * lighting;
           
                
                return output;
            }

           

         
	 
              
            void fragFunction(vertToFrag input, out half4 MRT0 : SV_Target0, out half4 MRT1 : SV_Target1)
            {
   
                //MRT0 = half4(SRGBtoLinear(input.color).rgb, 1);
                MRT0 = half4(input.color.rgb, 1);
                
                MRT1 = half4(0, 0, 0, 0);
            }


             
            ENDHLSL
        }
    }
}