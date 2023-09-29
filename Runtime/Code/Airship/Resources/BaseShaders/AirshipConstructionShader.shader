Shader "Airship/Construction"
{

    Properties
    {
      [HDR] _ColorA("Color", Color) = (0.8, 0.8, 0.9, 1)
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertFunction
            #pragma fragment fragFunction

            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;

            half4 _ColorA;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normal   : NORMAL;
                float2 uv : TEXCOORD0;
				float2 barycentricUV : TEXCOORD1;
            };

            struct vertToFrag
            {
                float4 positionCS : SV_POSITION;
                float4 localPos : TEXCOORD1;
                float3 color: COLOR;
                float3 worldNormal : TEXCOORD2;
                float3 barycentric : TEXCOORD3;
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


				output.worldNormal = mul(unity_ObjectToWorld, float4(input.normal, 0)).xyz;

                //do a couple fake lights
                float3 lighting = float3(0, 0, 0);

                //Ambient
                lighting += DoLight(float3(-1.5, -1, -1), float3(0.5, 0.5, 0.49), output.worldNormal);
                lighting += DoLight(float3(1.5, 1, 1), float3(0.49, 0.49, 0.5), output.worldNormal);

                //top
                lighting += DoLight(float3(0, -1, 0), float3(0.9,0.9,1) * 0.2, output.worldNormal);

                output.color = lighting * pow(_ColorA,0.454545454);
                output.localPos = float4(input.positionOS.xyz,0);

                if (input.barycentricUV.x < 1 && input.barycentricUV.y < 1)
                {
                    output.barycentric = float3(0, 0, 1);
                }
                else
                {
                    output.barycentric = float3(input.barycentricUV.x, input.barycentricUV.y, 0);
                }
               
                return output;
            }
            

            float4 fragFunction(vertToFrag input) : SV_TARGET
            {
                
                float wireframeWidth = 0.01;
                float f = fwidth(input.barycentric.x) + fwidth(input.barycentric.y) + fwidth(input.barycentric.z);
                float3 barycentric = smoothstep(0.5 * f, 1.5 * f, input.barycentric);

                float wireframe = min(min(barycentric.x, barycentric.y), barycentric.z);
                half3 color = lerp(half3(0, 0, 0), input.color,  saturate(0.95 + (wireframe / wireframeWidth)));


                //write the input.worldNormal debug colors
				color = input.worldNormal * 0.5 + 0.5;
                
                    
                return float4(color,1);
            }
            ENDHLSL
        }
    }
}