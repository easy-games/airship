 

Shader "Airship/LightBulb"
{
    Properties
    {

        _Color("Color", Color) = (1,1,1,1)
     
        _Alpha("Alpha", float) = 1
    }

    SubShader
    {
        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Name "Forward"
            Tags { "LightMode" = "AirshipForwardPass"}

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            
             

            HLSLPROGRAM
            #pragma vertex vertFunction
            #pragma fragment fragFunction

            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;
        
          
            float4 _Color;
            float _Alpha;
        
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color   : COLOR;
            };

            struct vertToFrag
            {
                float4 positionCS : SV_POSITION;
                float4  color : COLOR;
            };

            vertToFrag vertFunction(Attributes input)
            {
                vertToFrag output;
                float4 worldPos = mul(unity_ObjectToWorld, input.positionOS);

                output.positionCS = mul(unity_MatrixVP, worldPos);
				output.color = input.color;
                return output;
            }

        
             
            float4 fragFunction(vertToFrag input) : SV_TARGET
            {

                float4 finalColor = _Color * input.color;
                return float4(finalColor.r, finalColor.g, finalColor.b, _Alpha * input.color.a);
            }
            ENDHLSL
        }
    }
}