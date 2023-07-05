#ifndef FAST_LINE_RENDERER_SHADER_INCLUDE
#define FAST_LINE_RENDERER_SHADER_INCLUDE

#include "UnityCG.cginc"

#define WM_BASE_VERTEX_INPUT UNITY_VERTEX_INPUT_INSTANCE_ID
#define WM_BASE_VERTEX_TO_FRAG UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
#define WM_INSTANCE_VERT(v, type, o) type o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(type, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
#define WM_INSTANCE_FRAG(i) UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

float _JitterMultiplier;
float _Turbulence;
float _ScreenRadiusMultiplier;
float _FastLineRendererTimeBase;

fixed4 _TintColor;
float _AnimationSpeed;
float _UVXScale;
float _UVYScale;
sampler2D _MainTex;
float4 _MainTex_ST;
float4 _MainTex_TexelSize;
sampler2D _MainTexStartCap;
float4 _MainTexStartCap_ST;
float4 _MainTexStartCap_TexelSize;
sampler2D _MainTexEndCap;
float4 _MainTexEndCap_ST;
float4 _MainTexEndCap_TexelSize;
sampler2D _MainTexRoundJoin;
float4 _MainTexRoundJoinCap_ST;
float4 _MainTexRoundJoin_TexelSize;

#if defined(SOFTPARTICLES_ON)

float _InvFade;
sampler2D _CameraDepthTexture;

#endif

struct appdata_t
{
	float4 vertex : POSITION;
	fixed4 color : COLOR;
	float4 dir : TANGENT;
	float3 velocity : NORMAL;

#if defined(USE_CANVAS)

	float2 texcoord_xy : TEXCOORD0;
	float2 texcoord_zw : TEXCOORD1;
	float2 lifeTime_xy : TEXCOORD2;
	float2 lifeTime_zw : TEXCOORD3;

#else

	float4 texcoord : TEXCOORD0;
	float4 lifeTime : TEXCOORD1;

#endif

	WM_BASE_VERTEX_INPUT
};

struct v2f
{
	float4 pos : SV_POSITION;
	fixed2 texcoord : TEXCOORD0;
	fixed4 color : TEXCOORD1;

#if !defined(DISABLE_CAPS)

	fixed4 texcoord2 : TEXCOORD2;

#endif

#if defined(SOFTPARTICLES_ON)

	float4 projPos : TEXCOORD3;

#endif

	WM_BASE_VERTEX_TO_FRAG
};

inline float rand(float4 pos)
{
	return frac(sin(dot(_Time.yzw * pos.xyz, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
}

inline fixed lerpFade(float4 lifeTime)
{

#if defined(DISABLE_FADE)

	return 1.0;

#else

	// the vertex will fade in, stay at full color, then fade out
	// x = creation time seconds
	// y = fade time in seconds
	// z = life time seconds

	// debug
	// return 1;
	float peakFadeIn = lifeTime.x + lifeTime.y;
	float startFadeOut = lifeTime.x + lifeTime.z - lifeTime.y;
	float endTime = lifeTime.x + lifeTime.z;
	float lerpMultiplier = saturate(ceil(_FastLineRendererTimeBase - peakFadeIn));
	float lerp1Scalar = saturate(((_FastLineRendererTimeBase - lifeTime.x + 0.000001) / max(0.000001, (peakFadeIn - lifeTime.x))));
	float lerp2Scalar = saturate(max(0, ((_FastLineRendererTimeBase - startFadeOut) / max(0.000001, (endTime - startFadeOut)))));
	float lerp1 = lerp1Scalar * (1.0 - lerpMultiplier);
	float lerp2 = (1.0 - lerp2Scalar) * lerpMultiplier;
	return lerp1 + lerp2;

#endif

}

inline float3 rotate_vertex_position(float3 position, float3 origin, float3 axis, float angle)
{

#if defined(DISABLE_ROTATION)

	return position;

#else

	float half_angle = angle * 0.5;
	float4 q = float4(axis.xyz * sin(half_angle), cos(half_angle));
	position -= origin;
	return position + origin + (2.0 * cross(cross(position, q.xyz) + q.w * position, q.xyz));

#endif

}

inline v2f computeVertexOutput(appdata_t v, float animationSpeed, float uvxScale, float uvyScale, fixed4 color, fixed4 tintColor, float radiusMultiplier, float lengthMultiplier)
{
	WM_INSTANCE_VERT(v, v2f, o);

	float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
	float radius, radius2;
	float4 clipPos = UnityObjectToClipPos(v.vertex);
	radius = (v.dir.w * _ScreenRadiusMultiplier) * (clipPos.w * _ProjectionParams.w);
	radius2 = v.dir.w;
	radius = lerp(radius2, radius, _ScreenRadiusMultiplier > 0.0);

#if defined(USE_CANVAS)

	uint lineType = abs(round(v.texcoord_xy.x));
	float2 texcoord = float2(fmod(lineType, 2), v.texcoord_xy.y);

#else

	uint lineType = abs(round(v.texcoord.x));
	float2 texcoord = float2(fmod(lineType, 2), v.texcoord.y);

#endif

	float dirModifier = (texcoord.x * 2) - 1;
	float jitter = 1.0 + (_JitterMultiplier * rand(worldPos));

#if defined(USE_CANVAS)

	float elapsedSeconds = 0;// (_Time.y - v.lifeTime_xy.x);

#else

	float elapsedSeconds = (_Time.y - v.lifeTime.x);

#endif

	float3 dirNorm = normalize(v.dir.xyz);
	float4 velocityOffset = float4((v.velocity + (dirNorm * _Turbulence)) * elapsedSeconds, 0);
	float3 center = worldPos.xyz + (v.dir.xyz * 0.5 * -dirModifier);
	float4 pos;

#if defined(ORTHOGRAPHIC_MODE)

	float2 tangent = normalize(float2(-v.dir.y, v.dir.x));
	float4 offset = float4((tangent * radius * radiusMultiplier) + (lengthMultiplier * dirNorm.xy * dirModifier * radiusMultiplier), 0, 0);

#if defined(USE_CANVAS)

	if (v.lifeTime_zw.y != 0.0)

#else

	if (v.lifeTime.w != 0.0)

#endif

	{

#if defined(USE_CANVAS)

		float modifier = v.lifeTime_zw.y * elapsedSeconds;

#else

		float modifier = v.lifeTime.w * elapsedSeconds;

#endif

		offset.xy = rotate_vertex_position(offset.xyz, float3(0, 0, 0), float3(0, 0, 1), modifier).xy;
		worldPos.xy = rotate_vertex_position(worldPos.xyz, center, float3(0, 0, 1), modifier).xy;
	}
	pos = worldPos + (offset * jitter) + velocityOffset;

#else

	float3 directionToCamera = normalize(_WorldSpaceCameraPos - center);
	float3 tangent = cross(dirNorm.xyz, directionToCamera.xyz);
	float4 offset = float4((tangent * radius * radiusMultiplier) + (lengthMultiplier * dirNorm * dirModifier * radiusMultiplier), 0);

#if defined(USE_CANVAS)

	if (v.lifeTime_zw.y != 0.0)

#else

	if (v.lifeTime.w != 0.0)

#endif

	{
		// to rotate around the lines perpendicular, use tangent instead of directionToCamera for the axis

#if defined(USE_CANVAS)

		float modifier = v.lifeTime_zw.y * elapsedSeconds;
		offset.xyz = rotate_vertex_position(offset.xyz, float3(0, 0, 0), directionToCamera, v.lifeTime_zw.y * modifier);
		worldPos.xyz = rotate_vertex_position(worldPos.xyz, center, directionToCamera, v.lifeTime_zw.y * modifier);

#else

		float modifier = v.lifeTime.w * elapsedSeconds;
		offset.xyz = rotate_vertex_position(offset.xyz, float3(0, 0, 0), directionToCamera, v.lifeTime.w * modifier);
		worldPos.xyz = rotate_vertex_position(worldPos.xyz, center, directionToCamera, v.lifeTime.w * modifier);

#endif


	}
	pos = worldPos + (offset * jitter) + velocityOffset;

#endif

	o.pos = UnityObjectToClipPos(mul(unity_WorldToObject, pos));

#if defined(USE_CANVAS)

	// unity bug in canvas renderer does not pass texcoord2 or texcoord3 to the shader, so we can't do fade or lifetime
	// if Unity fixes canvas renderer to pass all the texcoords and/or pass the zw values, we can get rid of USE_CANVAS
	// o.color = lerpFade(float4(v.lifeTime_xy.x, v.lifeTime_xy.y, v.lifeTime_zw.x, v.lifeTime_zw.y)) * color * tintColor;
	o.color = color * tintColor;

#else

	o.color = lerpFade(v.lifeTime) * color * tintColor;

#endif

	o.texcoord = fixed2(texcoord.x * uvxScale, texcoord.y * uvyScale);

#if defined(DISABLE_CAPS)

	o.texcoord.x += (_Time.y * animationSpeed);

#else

	{
		// check each bit of the 4 possible line type bits

		// full line
		lineType /= 2;
		o.texcoord2.x = fmod(lineType, 2);
		o.texcoord.x += (_Time.y * animationSpeed * o.texcoord2.x);

		// start cap
		lineType /= 2;
		o.texcoord2.y = fmod(lineType, 2);

		// end cap
		lineType /= 2;
		o.texcoord2.z = fmod(lineType, 2);

		// round join
		lineType /= 2;
		o.texcoord2.w = fmod(lineType, 2);
	}

#endif

#if defined(SOFTPARTICLES_ON)

	o.projPos = ComputeScreenPos(o.pos);
	COMPUTE_EYEDEPTH(o.projPos.z);

#endif

	return o;
}

inline fixed4 computeFragmentOutput(v2f i, sampler2D texMain

#if !defined(DISABLE_CAPS)

	, sampler2D texStartCap, sampler2D texEndCap, sampler2D texRoundJoin

#endif

)

{
	WM_INSTANCE_FRAG(i);

#if defined(SOFTPARTICLES_ON)

	float sceneZ = LinearEyeDepth(UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos))));
	float partZ = i.projPos.z;
	i.color.a *= saturate(_InvFade * (sceneZ - partZ));

#endif

#if !defined(DISABLE_CAPS)

	// was using an if starement and returning a different sampler, but this had glitches in 3D mode
	// once texture arrays are supported, use those on supported targets
	return
		(
		(i.texcoord2.x * tex2D(texMain, i.texcoord)) +
		(i.texcoord2.y * tex2D(texStartCap, i.texcoord)) +
		(i.texcoord2.z * tex2D(texEndCap, i.texcoord)) +
		(i.texcoord2.w * tex2D(texRoundJoin, i.texcoord))
		) * i.color;

#else

	return tex2D(texMain, i.texcoord) * i.color;

#endif

}

#endif
