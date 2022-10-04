#ifndef CUSTM_LIT_INPUT_INCLUDED
#define CUSTM_LIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_DistortionMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DetailMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
	UNITY_DEFINE_INSTANCED_PROP(float, _CutOff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
	UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct InputConfig {
	float2 baseUV;
	float2 detailUV;
	bool useMask;
	bool useDetail;
};

InputConfig GetInputConfig(float2 baseUV, float2 detailUV = 0.0f) {
	InputConfig inputConfig;
	inputConfig.baseUV = baseUV;
	inputConfig.detailUV = detailUV;
	inputConfig.useMask = false;
	inputConfig.useDetail = false;
	return inputConfig;
}

float2 TransformBaseUV(float2 baseUV) {
	float4 baseST = INPUT_PROP( _BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV(float2 detailUV) {
	float4 detailST = INPUT_PROP(_DetailMap_ST);
	return detailUV * detailST.xy + detailST.zw;
}

float4 GetDetail(InputConfig c) {
	if (c.useDetail) {
		float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, c.detailUV);
		return map * 2.0 - 1.0;
	}
	return 0.0;
}

float4 GetMask(InputConfig c) {
	if (c.useMask) {
		return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, c.baseUV);
	}
	return 1.0;
}

float4 GetBase(InputConfig c) {

	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_BaseColor);

	if (c.useDetail) {
		float detailMap = GetDetail(c).r * INPUT_PROP(_DetailAlbedo);
		float mask = GetMask(c).b;

		map.rgb = lerp(sqrt(map.rgb), detailMap < 0.0f ? 0.0f : 1.0f, abs(detailMap)* mask);	//根据r项（影响反射率），将暗的地方更暗，量的地方更亮
		map.rgb *= map.rgb;
	}

	return map * color;
}

float3 GetNormal(InputConfig c) {

	float2 baseUV = c.baseUV;
	float2 detailUV = c.detailUV;

	float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, baseUV);
	float scale = INPUT_PROP(_NormalScale);
	float3 normal = DecodeNormal(map, scale);

	if (c.useDetail) {
		map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, detailUV);
		scale = INPUT_PROP(_DetailNormalScale) * GetMask(c).b;
		float3 detail = DecodeNormal(map, scale);
		normal = BlendNormalRNM(normal, detail);
	}

	return normal;
}

float GetCutOff(InputConfig c) {
	return INPUT_PROP(_CutOff);
}

float GetMetallic(InputConfig c) {
	float metallic = INPUT_PROP(_Metallic);;
	metallic *= GetMask(c).r;
	return metallic;
}

float GetSmoothness(InputConfig c) {

	float smoothness = INPUT_PROP(_Smoothness);;
	smoothness *= GetMask(c).a;

	if (c.useDetail) {
		float detail = GetDetail(c).b * INPUT_PROP(_DetailSmoothness);
		float mask = GetMask(c).b;
		smoothness = lerp(smoothness, detail < 0.0f ? 0.0f : 1.0f, abs(detail)* mask);
	}

	return smoothness;
}

float GetFresnel(InputConfig c) {
	return INPUT_PROP(_Fresnel);
}

float GetOcclusion(InputConfig c) {
	float occlusion = INPUT_PROP(_Occlusion);;
	occlusion *= GetMask(c).g;
	return occlusion;
}

float3 getEmission(InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP(_EmissionColor);
	return map.rgb * color.rgb;
}

void ClipLOD(float2 position, float fade) {
#if defined(LOD_FADE_CROSSFADE)
	//float dirth = (position.y % 32) / 32;
	float dirth = InterleavedGradientNoise(position.xy, 0);
	clip(fade + (fade < 0.0f ? dirth : - dirth));
#endif
}

float getFinalAlpha(float alpha) {
	return INPUT_PROP(_ZWrite) ? 1.0f : alpha;
}

#endif