#ifndef TERRAIN_INCLUDE
#define TERRAIN_INCLUDE
struct TerrainDrawData
{
    float2 worldPos;
    float scale;
    uint2 chunkPos;
};
struct Terrain_Appdata
{
    float3 position;
    float2 uv;
    uint2 chunkPos;
};
StructuredBuffer<TerrainDrawData> _TerrainData;
StructuredBuffer<float2> _TerrainChunk;
Terrain_Appdata GetTerrain(uint instanceID, uint vertexID)
{
    TerrainDrawData data = _TerrainData[instanceID];
    Terrain_Appdata o;
    o.uv = _TerrainChunk[vertexID];
    o.position = float3(data.worldPos + data.scale * o.uv, 0);
    o.position.yz = o.position.zy;
    o.chunkPos = data.chunkPos;
    return o;
}
#endif