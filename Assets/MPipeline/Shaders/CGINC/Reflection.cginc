#ifndef REFLECTION
#define REFLECTION
#define MAXIMUM_PROBE 8
#include "Shader_Include/ImageBasedLighting.hlsl"
    int DownDimension(uint3 id, const uint2 size, const int multiply){
        const uint3 multiValue = uint3(1, size.x, size.x * size.y) * multiply;
        return dot(id, multiValue);
    }
    float RoughnessToMipLevel(float rough)
    {
        return rough * UNITY_SPECCUBE_LOD_STEPS;
    }
    TextureCube<float4> _ReflectionCubeMap; SamplerState sampler_ReflectionCubeMap;
    TextureCube<float4> _ReflectionCubeMap0; SamplerState sampler_ReflectionCubeMap0;
    TextureCube<float4> _ReflectionCubeMap1; SamplerState sampler_ReflectionCubeMap1;
    TextureCube<float4> _ReflectionCubeMap2; SamplerState sampler_ReflectionCubeMap2;
    TextureCube<float4> _ReflectionCubeMap3; SamplerState sampler_ReflectionCubeMap3;
    TextureCube<float4> _ReflectionCubeMap4; SamplerState sampler_ReflectionCubeMap4;
    TextureCube<float4> _ReflectionCubeMap5; SamplerState sampler_ReflectionCubeMap5;
    TextureCube<float4> _ReflectionCubeMap6; SamplerState sampler_ReflectionCubeMap6;
    TextureCube<float4> _ReflectionCubeMap7; SamplerState sampler_ReflectionCubeMap7;
    void GetColor(int index, float3 normal, float lod, out float4 result)
    {
        switch(index)
        {
            case 0:
            result = _ReflectionCubeMap0.SampleLevel(sampler_ReflectionCubeMap0, normal, lod);
            break;
            case 1:
            result = _ReflectionCubeMap1.SampleLevel(sampler_ReflectionCubeMap1, normal, lod);
            break;
            case 2:
            result = _ReflectionCubeMap2.SampleLevel(sampler_ReflectionCubeMap2, normal, lod);
            break;
            case 3:
            result = _ReflectionCubeMap3.SampleLevel(sampler_ReflectionCubeMap3, normal, lod);
            break;
            case 4:
            result = _ReflectionCubeMap4.SampleLevel(sampler_ReflectionCubeMap4, normal, lod);
            break;
            case 5:
            result = _ReflectionCubeMap5.SampleLevel(sampler_ReflectionCubeMap5, normal, lod);
            break;
            case 6:
            result = _ReflectionCubeMap6.SampleLevel(sampler_ReflectionCubeMap6, normal, lod);
            break;
            default:
            result = _ReflectionCubeMap7.SampleLevel(sampler_ReflectionCubeMap7, normal, lod);
            break;
        }
    }
  
    struct ReflectionData
    {
        float3 position;
        float3 minExtent;
        float3 maxExtent;
        float4 hdr;
        float blendDistance;
        int boxProjection;
    };

#ifndef COMPUTE_SHADER

float3 GetFroxelColor(float4 hdr, int index, StructuredBuffer<float3> normals, const int sampleTime)
    {
        int i;
        float3 color = 0;
        switch(index)
        {
            case 0:
            [unroll]
            for(i = 0; i < sampleTime; ++i)
            {
                float3 n = normals[i];
                color += DecodeHDR(_ReflectionCubeMap0.SampleLevel(sampler_ReflectionCubeMap0, n, 10), hdr);
                color += DecodeHDR(_ReflectionCubeMap0.SampleLevel(sampler_ReflectionCubeMap0, -n, 10), hdr);
            }
            return color;
            case 1:
            [unroll]
            for(i = 0; i < sampleTime; ++i)
            {
                float3 n = normals[i];
                color += DecodeHDR(_ReflectionCubeMap1.SampleLevel(sampler_ReflectionCubeMap1, n, 10), hdr);
                color += DecodeHDR(_ReflectionCubeMap1.SampleLevel(sampler_ReflectionCubeMap1, -n, 10), hdr);
            }
            return color;
            case 2:
            [unroll]
            for(i = 0; i < sampleTime; ++i)
            {
                float3 n = normals[i];
                color += DecodeHDR(_ReflectionCubeMap2.SampleLevel(sampler_ReflectionCubeMap2, n, 10), hdr);
                color += DecodeHDR(_ReflectionCubeMap2.SampleLevel(sampler_ReflectionCubeMap2, -n, 10), hdr);
            }
            return color;
            case 3:
            [unroll]
            for(i = 0; i < sampleTime; ++i)
            {
                float3 n = normals[i];
                color += DecodeHDR(_ReflectionCubeMap3.SampleLevel(sampler_ReflectionCubeMap3, n, 10), hdr);
                color += DecodeHDR(_ReflectionCubeMap3.SampleLevel(sampler_ReflectionCubeMap3, -n, 10), hdr);
            }
            return color;
            case 4:
            [unroll]
            for(i = 0; i < sampleTime; ++i)
            {
                float3 n = normals[i];
                color += DecodeHDR(_ReflectionCubeMap4.SampleLevel(sampler_ReflectionCubeMap4, n, 10), hdr);
                color += DecodeHDR(_ReflectionCubeMap4.SampleLevel(sampler_ReflectionCubeMap4, -n, 10), hdr);
            }
            return color;
            case 5:
            [unroll]
            for(i = 0; i < sampleTime; ++i)
            {
                float3 n = normals[i];
                color += DecodeHDR(_ReflectionCubeMap5.SampleLevel(sampler_ReflectionCubeMap5, n, 10), hdr);
                color += DecodeHDR(_ReflectionCubeMap5.SampleLevel(sampler_ReflectionCubeMap5, -n, 10), hdr);
            }
            return color;
            case 6:
            [unroll]
            for(i = 0; i < sampleTime; ++i)
            {
                float3 n = normals[i];
                color += DecodeHDR(_ReflectionCubeMap6.SampleLevel(sampler_ReflectionCubeMap6, n, 10), hdr);
                color += DecodeHDR(_ReflectionCubeMap6.SampleLevel(sampler_ReflectionCubeMap6, -n, 10), hdr);
            }
            return color;
            default:
            [unroll]
            for(i = 0; i < sampleTime; ++i)
            {
                float3 n = normals[i];
                color += DecodeHDR(_ReflectionCubeMap7.SampleLevel(sampler_ReflectionCubeMap7, n, 10), hdr);
                color += DecodeHDR(_ReflectionCubeMap7.SampleLevel(sampler_ReflectionCubeMap7, -n, 10), hdr);
            }
            return color;
        }
        
    }
#ifndef FROXEL_LIGHT
float3 MPipelineGI_IndirectSpecular_Deferred(UnityGIInput data, float2 occlusion, Unity_GlossyEnvironmentData glossIn, ReflectionData reflData, float lod, samplerCUBE targetTex, out float3 gi)
{
    if(reflData.boxProjection > 0)
    {
        glossIn.reflUVW = BoxProjectedCubemapDirection (glossIn.reflUVW, data.worldPos, data.probePosition[0], data.boxMin[0], data.boxMax[0]);
    }
    float4 env0 = texCUBElod(targetTex, float4(glossIn.reflUVW, lod));
    float4 env1 = texCUBElod(targetTex, float4(glossIn.reflUVW, 10));
    gi = DecodeHDR(env1, data.probeHDR[0]) * occlusion.x;
    return DecodeHDR(env0, data.probeHDR[0]) * occlusion.y;
}
float3 MPipelineGI_IndirectSpecular(UnityGIInput data, float2 occlusion, Unity_GlossyEnvironmentData glossIn, ReflectionData reflData, int currentIndex, float lod)
{
    if(reflData.boxProjection > 0)
    {
        glossIn.reflUVW = BoxProjectedCubemapDirection (glossIn.reflUVW, data.worldPos, data.probePosition[0], data.boxMin[0], data.boxMax[0]);
    }
    float4 env0;
    GetColor(currentIndex, glossIn.reflUVW, lod, env0);
    return DecodeHDR(env0, data.probeHDR[0]) * occlusion.y;
}
#endif
#ifndef __LOCALLIGHTING_INCLUDE__
float2 _CameraClipDistance; //X: Near Y: Far - Near
#endif
StructuredBuffer<uint> _ReflectionIndices;
StructuredBuffer<ReflectionData> _ReflectionData;
#ifndef FROXEL_LIGHT
float3 CalculateGI(float linearDepth, float3 worldPos, float3 normal, float3 albedo , float ao, float2 screenUV)
{
    float rate = pow(max(0, (linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y), 1.0 / CLUSTERRATE);
    if(rate <= 1.0){
	float3 uv = float3(screenUV, rate);
	uint3 intUV = uv * float3(XRES, YRES, ZRES);
	int index = DownDimension(intUV, uint2(XRES, YRES), MAXIMUM_PROBE + 1);
	int target = _ReflectionIndices[index];
    float3 color = _ReflectionCubeMap.SampleLevel(sampler_ReflectionCubeMap, normal, 10).xyz;
	[loop]
	for (int a = 1; a < target; ++a)
	{
		int currentIndex = _ReflectionIndices[index + a];
		ReflectionData data = _ReflectionData[currentIndex];
		float3 leftDown = data.position - data.maxExtent;
		float3 cubemapUV = (worldPos.xyz - leftDown) / (data.maxExtent * 2);
		if (dot(abs(cubemapUV - saturate(cubemapUV)), 1) > 1e-8) continue;
        float4 env;
        GetColor(currentIndex, normal, 10, env);
        float3 distanceToMin = saturate((abs(worldPos.xyz - data.position) - data.minExtent) / data.blendDistance);
        float lerpValue = max(distanceToMin.x, max(distanceToMin.y, distanceToMin.z));
        color = lerp(DecodeHDR(env, data.hdr), color, lerpValue);
    }
    return color * ao * albedo;
    }
    return 0;
}
sampler2D _PreIntDefault;
float3 CalculateReflection_Skybox(float3 viewDir, float4 specular, float4 gbuffer1, float3 albedo, float2 aoro, out float3 gi)
{
    float3 normal = gbuffer1.xyz;
	Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(specular.w, -viewDir, normal, specular.xyz);
	float perceptualRoughness = g.roughness;
	perceptualRoughness = sqrt(perceptualRoughness);
	float lod = RoughnessToMipLevel(perceptualRoughness);;
    float3 refVec = reflect(viewDir, normal);
   
    float3 specColor = _ReflectionCubeMap.SampleLevel(sampler_ReflectionCubeMap, refVec, lod);
    gi = _ReflectionCubeMap.SampleLevel(sampler_ReflectionCubeMap, normal, 10) * albedo * aoro.x;
    return specColor * specular.xyz * aoro.y;
}

float4 CalculateReflection_Deferred(float3 worldPos, float3 viewDir, float4 specular, float4 gbuffer1, float3 albedo, float2 aoro, samplerCUBE targetTex, int index, out float3 gi)
{
    ReflectionData data = _ReflectionData[index];
	float3 leftDown = data.position - data.maxExtent;
	float3 cubemapUV = (worldPos.xyz - leftDown) / (data.maxExtent * 2);
    if (dot(abs(cubemapUV - saturate(cubemapUV)), 1) > 1e-8) 
    {
        discard;
        return 0;
    }

    float3 normal = gbuffer1.xyz;
	Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(specular.w, -viewDir, normal, specular.xyz);
	float perceptualRoughness = g.roughness;
	perceptualRoughness = perceptualRoughness * (1.7 - 0.7*perceptualRoughness);
	float lod = RoughnessToMipLevel(perceptualRoughness);;
	float oneMinusReflectivity = 1 - SpecularStrength(specular.xyz);
	UnityGIInput d;
	d.worldPos = worldPos.xyz;
	d.worldViewDir = -viewDir;
	

    d.probeHDR[0] = data.hdr;
		if (data.boxProjection > 0)
		{
			d.probePosition[0] = float4(data.position, 1);
			d.boxMin[0].xyz = leftDown;
			d.boxMax[0].xyz = (data.position + data.maxExtent);
		}
        float3 specColor = MPipelineGI_IndirectSpecular_Deferred(d, aoro, g, data, lod, targetTex, gi);
        float3 distanceToMin = saturate((abs(worldPos.xyz - data.position) - data.minExtent) / data.blendDistance);
        float lerpValue = max(distanceToMin.x, max(distanceToMin.y, distanceToMin.z));
         float3 rgb = specular.xyz * specColor;
        gi *= albedo;
	return float4(rgb, lerpValue);

}
float3 CalculateReflection(float linearDepth, float3 worldPos, float3 viewDir, float4 specular, float4 gbuffer1, float3 albedo, float2 aoro, float2 screenUV)
{
    float3 normal = gbuffer1.xyz;
	Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(specular.w, -viewDir, normal, specular.xyz);
	float perceptualRoughness = g.roughness;
	perceptualRoughness = perceptualRoughness * (1.7 - 0.7*perceptualRoughness);
	float lod = RoughnessToMipLevel(perceptualRoughness);;
	float oneMinusReflectivity = 1 - SpecularStrength(specular.xyz);
	UnityGIInput d;
	d.worldPos = worldPos.xyz;
	d.worldViewDir = -viewDir;
	UnityLight light;
	light.color = float3(0, 0, 0);
	light.dir = float3(0, 1, 0);
	UnityIndirect ind;
	ind.diffuse = 0;
    ind.specular =  _ReflectionCubeMap.SampleLevel(sampler_ReflectionCubeMap, g.reflUVW, lod).xyz;
	float rate = pow(max(0, (linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y), 1.0 / CLUSTERRATE);
    if(rate <= 1.0){
	float3 uv = float3(screenUV, rate);
	uint3 intUV = uv * float3(XRES, YRES, ZRES);
	int index = DownDimension(intUV, uint2(XRES, YRES), MAXIMUM_PROBE + 1);
	int target = _ReflectionIndices[index];
	[loop]
	for (int a = 1; a < target; ++a)
	{
		int currentIndex = _ReflectionIndices[index + a];
		ReflectionData data = _ReflectionData[currentIndex];
		float3 leftDown = data.position - data.maxExtent;
		float3 cubemapUV = (worldPos.xyz - leftDown) / (data.maxExtent * 2);
		if (dot(abs(cubemapUV - saturate(cubemapUV)), 1) > 1e-8) continue;
        
		d.probeHDR[0] = data.hdr;
		if (data.boxProjection > 0)
		{
			d.probePosition[0] = float4(data.position, 1);
			d.boxMin[0].xyz = leftDown;
			d.boxMax[0].xyz = (data.position + data.maxExtent);
		}
		float3 specColor = MPipelineGI_IndirectSpecular(d, aoro, g, data, currentIndex, lod);
		float3 distanceToMin = saturate((abs(worldPos.xyz - data.position) - data.minExtent) / data.blendDistance);
        float lerpValue = max(distanceToMin.x, max(distanceToMin.y, distanceToMin.z));
		ind.specular = lerp(specColor, ind.specular, lerpValue);
	}
    }
    float3 rgb = BRDF1_Unity_PBS(0, specular.xyz, oneMinusReflectivity, specular.w, normal, -viewDir, light, ind).rgb;
	return rgb;
}
#endif
float3 FroxelIndirect(float linearDepth, float3 worldPos, float2 screenUV, StructuredBuffer<float3> normals)
{
    float rate = pow(max(0, (linearDepth - _CameraClipDistance.x) / _CameraClipDistance.y), 1.0 / CLUSTERRATE);
    float3 color = 0;
    if(rate <= 1.0)
    {
        float3 uv = float3(screenUV, rate);
	    uint3 intUV = uv * float3(XRES, YRES, ZRES);
	    int index = DownDimension(intUV, uint2(XRES, YRES), MAXIMUM_PROBE + 1);
	    int target = _ReflectionIndices[index];
        [loop]
	    for (int a = 1; a < target; ++a)
	    {
		    int currentIndex = _ReflectionIndices[index + a];
		    ReflectionData data = _ReflectionData[currentIndex];
		    float3 leftDown = data.position - data.maxExtent;
		    float3 cubemapUV = (worldPos.xyz - leftDown) / (data.maxExtent * 2);
		    if (dot(abs(cubemapUV - saturate(cubemapUV)), 1) > 1e-8) continue;
		    //float3 specColor = MPipelineGI_IndirectSpecular(d, aoro, g, data, currentIndex, lod, normal, diffuseIndirect);
		    float3 distanceToMin = saturate((abs(worldPos.xyz - data.position) - data.minExtent) / data.blendDistance);
            float lerpValue = max(distanceToMin.x, max(distanceToMin.y, distanceToMin.z));
            float3 curCol = GetFroxelColor(data.hdr, currentIndex, normals, 3);
            color = lerp(curCol, color, lerpValue);
	    }
    }
    return color;
}
#endif
#endif