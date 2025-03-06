Shader "Airship/AirshipCharacterStencil"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Geometry-1" "RenderPipeline" = "UniversalPipeline"}
        ZWrite On

        Pass
        {
            Name "Depth Only"
            Tags {"LightMode" = "CharacterDepth"}
            ColorMask 0
        }
    }

}