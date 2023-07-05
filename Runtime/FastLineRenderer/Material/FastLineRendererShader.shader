Shader "Custom/FastLineRendererShader"
{
	Properties
	{
		[PerRendererData] _MainTex ("Line Texture (RGB) Alpha (A)", 2D) = "white" {}
		[PerRendererData] _MainTexStartCap ("Line Texture Start Cap (RGB) Alpha (A)", 2D) = "transparent" {}
		[PerRendererData] _MainTexEndCap ("Line Texture End Cap (RGB) Alpha (A)", 2D) = "transparent" {}
		[PerRendererData] _MainTexRoundJoin ("Line Texture Round Join (RGB) Alpha (A)", 2D) = "transparent" {}
		[PerRendererData] _TintColor("Tint Color (RGB)", Color) = (1, 1, 1, 1)
		[PerRendererData] _AnimationSpeed("Animation Speed (Float)", Float) = 0
		[PerRendererData] _UVXScale("UV X Scale (Float)", Float) = 1.0
		[PerRendererData] _UVYScale("UV Y Scale (Float)", Float) = 1.0

		[PerRendererData] _GlowIntensityMultiplier ("Glow Intensity (Float)", Float) = 1.0
		[PerRendererData] _GlowWidthMultiplier("Glow Width Multiplier (Float)", Float) = 1.0
		[PerRendererData] _GlowLengthMultiplier ("Glow Length Multiplier (Float)", Float) = 0.33
		[PerRendererData] _AnimationSpeedGlow("Glow Animation Speed (Float)", Float) = 0
		[PerRendererData] _UVXScaleGlow("Glow UV X Scale (Float)", Float) = 1.0
		[PerRendererData] _UVYScaleGlow("Glow UV Y Scale (Float)", Float) = 1.0

		[PerRendererData] _InvFade("Soft Particles Factor", Range(0.01, 3.0)) = 1.0
		[PerRendererData] _JitterMultiplier("Jitter Multiplier (Float)", Float) = 0.0
		[PerRendererData] _Turbulence("Turbulence (Float)", Float) = 0.0

		[PerRendererData] _ScreenRadiusMultiplier("Screen Radius Multiplier (Float)", Float) = 0.0
    }

    SubShader
	{
        Tags { "Queue" = "Transparent" }
		Cull Off
		Lighting Off
		ZWrite Off
		ColorMask RGBA

		CGINCLUDE
		
		#include "FastLineRendererShaderInclude.cginc"

		#pragma target 3.0
		#pragma vertex vert
        #pragma fragment frag
		#pragma multi_compile_particles // for soft particles
		#pragma fragmentoption ARB_precision_hint_fastest
		#pragma glsl_no_auto_normalization
		#pragma multi_compile_instancing
		#pragma multi_compile __ DISABLE_CAPS
		#pragma multi_compile __ ORTHOGRAPHIC_MODE
		#pragma multi_compile __ USE_CANVAS
		#pragma multi_compile __ DISABLE_FADE
		#pragma multi_compile __ DISABLE_ROTATION

		ENDCG

		// glow pass
		Pass
		{
			Name "GlowPass"
			Blend SrcAlpha One

			CGPROGRAM

			float _GlowIntensityMultiplier;
			float _GlowWidthMultiplier;
			float _GlowLengthMultiplier;

            v2f vert(appdata_t v)
            {
				v.dir.w *= _GlowWidthMultiplier;

#if defined(USE_CANVAS)

				return computeVertexOutput(v, _AnimationSpeed, _UVXScale, _UVYScale, fixed4(1, 1, 1, v.texcoord_zw.y * _GlowIntensityMultiplier), _TintColor, v.texcoord_zw.x, _GlowLengthMultiplier);

#else

				return computeVertexOutput(v, _AnimationSpeed, _UVXScale, _UVYScale, fixed4(1, 1, 1, v.texcoord.w * _GlowIntensityMultiplier), _TintColor, v.texcoord.z, _GlowLengthMultiplier);

#endif

            }
			
            fixed4 frag(v2f i) : SV_Target
			{

#if !defined(DISABLE_CAPS)

				return computeFragmentOutput(i, _MainTex, _MainTexStartCap, _MainTexEndCap, _MainTexRoundJoin);

#else

				return computeFragmentOutput(i, _MainTex);

#endif

            }

            ENDCG
        }
    }
}