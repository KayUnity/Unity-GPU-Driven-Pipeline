#ifndef TERRAIN_INCLUDE
#define TERRAIN_INCLUDE
struct TerrainChunkBuffer
{
    float2 worldPos;
    float2 minMaxHeight;
    float scale;
};
struct Terrain_Appdata
{
    float3 position;
    float2 uv;
};
StructuredBuffer<float2> _TerrainMeshBuffer;
StructuredBuffer<TerrainChunkBuffer> _TerrainChunks;
StructuredBuffer<uint> _CullResultBuffer;
Terrain_Appdata GetTerrain(uint instanceID, uint vertexID)
{
    TerrainChunkBuffer data = _TerrainChunks[_CullResultBuffer[instanceID]];
    float2 uv = _TerrainMeshBuffer[vertexID];
    float3 worldPos = float3(data.worldPos + data.scale * uv, 0);
    Terrain_Appdata o;
    o.uv = uv;
    o.position = worldPos.xzy;
    return o;
}
#endif