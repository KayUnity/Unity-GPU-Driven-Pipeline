#ifndef TERRAIN_INCLUDE
#define TERRAIN_INCLUDE
struct TerrainChunkBuffer
{
    int2 position;
    int chunkSize;
    float minHeight;
    float maxHeight;
};
cbuffer TerrainSettings
{
    int2 terrainOffset;
    float perChunkSize;
    float useless;
};
struct Terrain_Appdata
{
    float2 uv;
    float3 position;
    int2 chunkPos;
};
StructuredBuffer<float2> _TerrainMeshBuffer;
StructuredBuffer<TerrainChunkBuffer> _TerrainChunks;
StructuredBuffer<uint> _CullResultBuffer;
Terrain_Appdata GetTerrain(uint instanceID, uint vertexID)
{
    TerrainChunkBuffer data = _TerrainChunks[_CullResultBuffer[instanceID]];
    Terrain_Appdata o;
    o.uv = _TerrainMeshBuffer[vertexID];
    float2 startPos = (data.position + terrainOffset) * perChunkSize;
    float extent = data.chunkSize * perChunkSize;
    o.position = float3(startPos + extent * o.uv, 0);
    o.position.yz = o.position.zy;
    o.chunkPos = data.position;
    return o;
}
#endif