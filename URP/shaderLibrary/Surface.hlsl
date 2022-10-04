#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface {
	float3 position;
	float3 normal;
	float3 interpolatedNormal;
	float3 viewDir;
	float depth;
	float3 color;
	float alpha;
	float metallic;
	float smoothness;
	float fresnel;
	float occlusion;
	float dither;
	uint renderingLayerMask;
};


#endif