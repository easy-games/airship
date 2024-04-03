Shader "Airship/PostProcess/ColorGrade"
{

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        ZWrite Off Cull Off
        ZTest Always
        Pass
        {
            Name "ColorBlitPass" 

            HLSLPROGRAM
            #pragma multi_compile _ CONVERT_COLOR_ON
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
         

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

            half3 ACESToneMapping(half3 color)
            {
                float3x3 m1 = float3x3(
                    0.59719, 0.07600, 0.02840,
                    0.35458, 0.90834, 0.13383,
                    0.04823, 0.01566, 0.83777
                );
                float3x3 m2 = float3x3(
                    1.60475, -0.10208, -0.00327,
                    -0.53108, 1.10813, -0.07276,
                    -0.07367, -0.00605, 1.07602
                );
                half3 v = mul(color, m1);
                half3 a = v * (v + 0.0245786) - 0.000090537;
                half3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
                return pow(clamp( mul( (a / b),m2), 0.0, 1.0), half3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2));
            }

            float3 ReinhardToneMapping(float3 color)
            {
                // Apply Reinhard tonemapping
                float3 mappedColor = color / (color + 1);
                // Clamp the color to the 0-1 range
                return saturate(mappedColor);
            }

            half3 Uncharted2ToneMapping(half3 color)
            {
                float gamma = 2.22;
                float A = 0.15;
                float B = 0.50;
                float C = 0.10;
                float D = 0.20;
                float E = 0.02;
                float F = 0.30;
                float W = 11.2;
                float exposure = 2.;
                color *= exposure;
                color = ((color * (A * color + C * B) + D * E) / (color * (A * color + B) + D * F)) - E / F;
                float white = ((W * (A * W + C * B) + D * E) / (W * (A * W + B) + D * F)) - E / F;
                color /= white;
                //color = pow(color, half3(1. / gamma, 1. / gamma, 1. / gamma));
                return color;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Note: The pass is setup with a mesh already in clip
                // space, that's why, it's enough to just output vertex
                // positions
                output.positionCS = float4(input.positionHCS.xy, 0, 1.0);
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

            //Our internal color conversions
            half3 SRGBtoLinear(half3 srgb)
            {
                return pow(srgb, 0.4545454545);
            }
            half3 LinearToSRGB(half3 srgb)
            {
                return pow(srgb, 2.233333);
            }

            
            //UNITY's Color Conversions
            inline half3 GammaToLinearSpace (half3 sRGB)
            {
                // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
                return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);
            
                // Precise version, useful for debugging.
                //return half3(GammaToLinearSpaceExact(sRGB.r), GammaToLinearSpaceExact(sRGB.g), GammaToLinearSpaceExact(sRGB.b));
            }

            inline half3 LinearToGammaSpace (half3 linRGB)
            {
                linRGB = max(linRGB, half3(0.h, 0.h, 0.h));
                // An almost-perfect approximation from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
                return max(1.055h * pow(linRGB, 0.416666667h) - 0.055h, 0.h);

                // Exact version, useful for debugging.
                //return half3(LinearToGammaSpaceExact(linRGB.r), LinearToGammaSpaceExact(linRGB.g), LinearToGammaSpaceExact(linRGB.b));
            }

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            TEXTURE2D_X(_BloomColorTexture);
            SAMPLER(sampler_BloomColorTexture);
            float BloomScale;
            
            float Contrast;
			float Saturation;
            float Hue;
            float Value;
            float Master;

            float CONVERT_COLOR; 
            
            half3 rgb2hsv(half3 c)
            {
                half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                half4 p = c.g < c.b ? half4(c.bg, K.wz) : half4(c.gb, K.xy);
                half4 q = c.r < p.x ? half4(p.xyw, c.r) : half4(c.r, p.yzx);

                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
        
            half3 hsv2rgb(half3 c)
            {
                half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            half3 BlendMode_Screen(half3 base, half3 blend)
            {
                return base + blend - base * blend;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 colorSample = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.uv);
                
                float4 bloomSample = SAMPLE_TEXTURE2D_X(_BloomColorTexture, sampler_BloomColorTexture, input.uv) * BloomScale * Master;
                
                
#ifdef CONVERT_COLOR_ON
                //half3 gradedColor = BlendMode_Screen( LinearToSRGB(colorSample.xyz), bloomSample.rgb);
                half3 gradedColor = BlendMode_Screen( GammaToLinearSpace(colorSample.xyz), bloomSample.rgb);
#else
                half3 gradedColor = BlendMode_Screen( colorSample.xyz, bloomSample.rgb);
#endif
                //Contrast
				half3 modifedColor = lerp(half3(0.5, 0.5, 0.5), gradedColor, Contrast);
                
                //Saturation, value
                half3 hsv = rgb2hsv(modifedColor);
	        	hsv = half3(hsv.x + Hue, hsv.y * Saturation, hsv.z * Value);
                hsv = saturate(hsv);
				
                //Master switch
				half3 finalColor = lerp(gradedColor, hsv2rgb(hsv), Master);
                               
                ///Pick your poison
                //finalColor = ACESToneMapping(finalColor);
				//finalColor = Uncharted2ToneMapping(finalColor);
                
                return half4(finalColor.r,finalColor.g, finalColor.b, colorSample.a);


            }
            ENDHLSL
        }
    }
}