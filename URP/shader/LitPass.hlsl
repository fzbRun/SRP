#ifndef CUSTOM_LIT_PASS_INCLUDE
#define CUSTOM_LIT_PASS_INCLUDE

#include "../shaderLibrary/Surface.hlsl"
#include "../shaderLibrary/Shadow.hlsl"
#include "../shaderLibrary/Light.hlsl"
#include "../shaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../shaderLibrary/Lighting.hlsl"

struct Attributes {
	float3 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord : TEXCOORD0;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 position : SV_POSITION;
	float3 worldPosition : VAR_POSITION;
	float3 normal : VAR_NORMAL;
#if defined(_NORMAL_MAP)
	float4 tangent : VAR_TANGENT;
#endif
	float2 uv : VAR_BASE_UV;
	float2 detailUV : VAR_DETAIL_UV;
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert(Attributes i) {

	Varyings o;

	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, o);
	TRANSFER_GI_DATA(i, o);

	o.worldPosition = TransformObjectToWorld(i.vertex.xyz);
	o.position = TransformWorldToHClip(o.worldPosition);
	o.normal = TransformObjectToWorldNormal(i.normal);
#if defined(_NORMAL_MAP)
	o.tangent = float4(TransformObjectToWorldDir(i.tangent.xyz), i.tangent.w);
#endif
	o.uv = TransformBaseUV(i.texcoord);
#if defined(_DETAIL_MAP)
	o.detailUV = TransformDetailUV(i.texcoord);
#endif

#if UNITY_REVERSED_Z	//相机朝向负z轴,如果点在近平面外，则取近平面
	o.position.z = min(o.position.z, o.position.w * UNITY_NEAR_CLIP_VALUE);
#else
	o.position.z = max(o.position.z, o.position.w * UNITY_NEAR_CLIP_VALUE);
#endif

	return o;

}

float4 frag(Varyings i) : SV_TARGET{

	UNITY_SETUP_INSTANCE_ID(i);

	ClipLOD(i.position, unity_LODFade.x);

	float3 normal = normalize(i.normal);

	InputConfig c = GetInputConfig(i.uv);
#if defined(_MASK_MAP)
	c.useMask = true;
#endif
#if defined(_DETAIL_MAP)
	c.detailUV = i.detailUV;
	c.useDetail = true;
#endif

	float4 base = GetBase(c);

#if defined(_CLIPPING)
	clip(base.a - GetCutOff(c));
#endif

	Surface surface;
	surface.position = i.worldPosition;
#if defined(_NORMAL_MAP)
	surface.normal = NormalTangentToWorld(GetNormal(c), i.normal, i.tangent);
	surface.interpolatedNormal = i.normal;
#else
	surface.normal = normal;
	surface.interpolatedNormal = normal;
#endif
	surface.viewDir = normalize(_WorldSpaceCameraPos - i.worldPosition);
	//文章中说这里要用这个函数，但是我这里使用会出错，会有两个视角阴影消失，所以直接左乘。
	//surface.depth = -TransformWorldToView(i.position).z;
	surface.depth = mul(UNITY_MATRIX_V, i.position.z);
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(c);
	surface.smoothness = GetSmoothness(c);
	surface.fresnel = GetFresnel(c);
	surface.occlusion = GetOcclusion(c);
	surface.dither = InterleavedGradientNoise(i.position.xy, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

	BRDF brdf = getBRDF(surface);

	GI gi = getGI(GI_FRAGMENT_DATA(i), surface, brdf);

	float3 color = diffuse(surface, brdf, gi);
	color += 0.1f * base;
	color += getEmission(c);

	return float4(color, getFinalAlpha(surface.alpha));

}
#endif