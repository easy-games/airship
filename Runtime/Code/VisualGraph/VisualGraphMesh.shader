Shader "Airship/VisualGraphMesh"
{
    Properties
    {
        [MainColor] _BaseColor ("BackgroundColor", Color) = (0,0,0,1)
        _LineColorA ("LineColorA", Color) = (1,0,1,1)
        _LineColorB ("LineColorB", Color) = (0,1,0,1)
        _LineColorC ("LineColorC", Color) = (0,0,1,1)
        _LineColorD ("LineColorD", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("DataTexture", 2D) = "black" {}
        _LineThickness ("LineThickness", Range(0.001, 1.0)) = .01
        _MaxValues ("MaxValues", Integer) = 128

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" "Queue"="Overlay" 
            "RenderPipeline" = "UniversalRenderPipeline"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest[unity_GUIZTestMode]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" 

            struct ModelData
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct VertToFrag
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 vertexObject: TEXCOORD1;
                float valueObjectSpace: TEXCOORD2;
            };

            float4 _BaseColor;
            float4 _LineColorA;
            float4 _LineColorB;
            float4 _LineColorC;
            float4 _LineColorD;
            sampler2D  _BaseMap;
            float4  _BaseMap_ST;
            float _LineThickness;
            int _MaxValues;

            //Model Color Alpha represents the value on the graph
            VertToFrag vert (ModelData model)
            {
                VertToFrag output;
                output.valueObjectSpace = ((model.color.a) * 2 - 1) * 10;
                output.vertexObject = model.vertex;
                //output.vertexObject.z = max(output.vertexObject.z, output.valueObjectSpace);
                output.vertex = UnityObjectToClipPos(output.vertexObject);

                // Calculate normalized UVs manually
                float2 localPos = output.vertex.xy;
                float2 min = float2(-0.5, -0.5);
                float2 max = float2(0.5, 0.5);
                //output.uv = (localPos - min) / (max - min);
                output.uv = TRANSFORM_TEX(model.uv, _BaseMap);

                //output.vertex = TransformObjectToHClip(model.vertex);

                output.color = model.color;
                return output;
            }

            float4 frag (VertToFrag input) : SV_Target
            {
                float xDelta = input.uv.x;//GammaToLinearSpace(input.uv.x);
                float yDelta = GammaToLinearSpace(input.uv.y);
                float textureValue = xDelta * _MaxValues;
                float floorValue = floor(textureValue);
                float ceilValue = ceil(textureValue);
                float valueRemainder = textureValue - floorValue;

                float4 valueData = lerp(tex2D(_BaseMap, floorValue / _MaxValues), tex2D(_BaseMap, ceilValue / _MaxValues), valueRemainder);
                //valueData = tex2D(_BaseMap, xDelta);
                float stepValueA = 1-step(valueData.r, yDelta - _LineThickness/2);
                float stepValueB = step(valueData.r - _LineThickness/2, yDelta);
                float4 lineColorA = lerp(_BaseColor, _LineColorA, stepValueA * stepValueB);

                
                float testValue = 0.0;
                testValue = yDelta;
                //testValue = (yDelta + 1) / 2;
                //return float4(testValue,testValue,testValue, 1);
                //return tex2D(_BaseMap, input.vertexObject.y).r;
                //return step(valueData.r, input.vertexObject.y);
                //return float4(valueData.r, valueData.r, valueData.r, 1);
                return float4(lineColorA.rgb, 1);
            }
            ENDHLSL
        }
        
        Pass {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" 

            struct ModelData
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct VertToFrag
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            VertToFrag vert (ModelData model)
            {
                VertToFrag output;
                output.vertex = TransformObjectToHClip(model.vertex.xyz);
                output.color = model.color;
                return output;
            }

            half4 frag (VertToFrag input) : SV_Target
            {
                //clip(input.vertex.y - input.color.a);
                return input.vertex;
            }
            ENDHLSL
        }
        Pass {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" 

            struct ModelData
            {
                float4 vertex : POSITION;
            };

            struct VertToFrag
            {
                float4 vertex : SV_POSITION;
            };

            VertToFrag vert (ModelData model)
            {
                VertToFrag output;
                output.vertex = TransformObjectToHClip(model.vertex.xyz);
                return output;
            }

            half4 frag (VertToFrag input) : SV_Target
            {
                return input.vertex;
            }
            ENDHLSL
        }
    }
}
