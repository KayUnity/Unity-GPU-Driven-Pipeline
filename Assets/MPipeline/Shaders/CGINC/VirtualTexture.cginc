#ifndef VIRTUAL_TEXTURE
#define VIRTUAL_TEXTURE
Texture2D<half4> _IndexTexture;
SamplerState sampler_IndexTexture;
float4 _IndexTexture_TexelSize;
float4 SampleVirtualTexture(Texture2DArray<float4> tex, SamplerState samp, float2 uv)
{
    float4 scaleOffset = _IndexTexture.Sample(sampler_IndexTexture, uv);
    uv *= _IndexTexture_TexelSize.zw;
    float2 realUV = frac(uv) * scaleOffset.x + scaleOffset.yz;
    return tex.Sample(samp, float3(realUV, scaleOffset.w));
}

float4 SampleVirtualTexture(Texture2DArray<float4> tex, SamplerState samp, uint2 targetChunk, float2 localUV)
{
    float4 scaleOffset = _IndexTexture[targetChunk];
    localUV = localUV * scaleOffset.x + scaleOffset.yz;
    return tex.Sample(samp, float3(localUV, scaleOffset.w));
}
#endif