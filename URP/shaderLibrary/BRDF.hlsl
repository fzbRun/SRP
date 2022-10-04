#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#define MIN_REFLECTIVITY 0.04

struct BRDF {
	float3 diffuse;
	float3 specular;
	float roughness;
	float perceptualRoughness;
	float fresnel;
};

float OnMinusReflectivity(float metallic) {
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

//看不懂这个公式，好像直接将所有项统一了，就当brdf算好了。
float SpecularStrength(Surface surface, BRDF brdf, Light light) {
	float3 h = normalize(surface.viewDir + light.direction);
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1) + 1.001f);
	float normalization = 4.0f * brdf.roughness + 2.0f;
	return r2 / (d2 * max(0.1f, nh2) * normalization);
}

float3 DirectBRDF(Surface surface, BRDF brdf, Light light) {
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

//diffuse表示漫反射光照，brdf.diffuse表示材质对漫反射光照的反射率。
float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular) {

	float fresnelStrength = surface.fresnel * Pow4(1.0f - saturate(dot(surface.normal, surface.viewDir)));
	float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);

	reflection /= brdf.roughness * brdf.roughness + 1.0f;
	return (diffuse * brdf.diffuse + reflection) * surface.occlusion;

}

BRDF getBRDF(Surface surface) {

	float oneMinusReflectivity = OnMinusReflectivity(surface.metallic);	//计算漫反射率

	BRDF brdf;
	brdf.diffuse = surface.color * oneMinusReflectivity * surface.alpha;
	brdf.specular = lerp(MIN_REFLECTIVITY * surface.color, surface.color, surface.metallic);	//根据金属值得到夹角为90度时的镜面反射率
	brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);	//通过光滑度获得粗糙度
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
	brdf.fresnel = saturate(surface.smoothness + 1.0f - oneMinusReflectivity);
	return brdf;
}

#endif