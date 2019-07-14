using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using Unity.Jobs;
using System.Threading;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe sealed class MTerrain : JobProcessEvent
    {
        public static MTerrain current { get; private set; }
        public struct TerrainChunkBuffer
        {
            public int2 position;
            public int chunkSize;
            public float minHeight;
            public float maxHeight;
        }
        public struct TerrainDrawData
        {
            public float2 worldPos;
            public float scale;
            public uint2 chunkPos;
        }
        public int chunkCount = 80;
        [Range(0f, 100f)]
        public float chunkSize = 10;
        public int planarResolution;
        public Material drawTerrainMaterial;
        //private VirtualTexture virtualTexture;
        private Native2DArray<int> indexBuffer;
        private NativeList<TerrainChunkBuffer> loadedChunks;
        private NativeArray<TerrainDrawData> culledResults;
        private ComputeBuffer meshBuffer;
        private ComputeBuffer drawBuffer;
        private JobHandle cullHandle;
        private int drawCount;
        public int2 chunkOffset;

        public void InitializeMesh()
        {
            meshBuffer = new ComputeBuffer(6 * planarResolution * planarResolution, sizeof(float2));
            NativeArray<float2> arr = new NativeArray<float2>(6 * planarResolution * planarResolution, Allocator.Temp);
            int getLength(int x, int y)
            {
                return (y * planarResolution + x) * 6;
            }
            for (int x = 0; x < planarResolution; ++x)
                for (int y = 0; y < planarResolution; ++y)
                {
                    arr[getLength(x, y)] = float2(x, y) / planarResolution;
                    arr[getLength(x, y) + 1] = float2(x, y + 1) / planarResolution;
                    arr[getLength(x, y) + 2] = float2(x + 1, y) / planarResolution;
                    arr[getLength(x, y) + 3] = float2(x, y + 1) / planarResolution;
                    arr[getLength(x, y) + 4] = float2(x + 1, y + 1) / planarResolution;
                    arr[getLength(x, y) + 5] = float2(x + 1, y) / planarResolution;
                }
            meshBuffer.SetData(arr);
            arr.Dispose();
        }

        public override void OnEnableFunc()
        {
            if (current && current != this)
            {
                enabled = false;
                Debug.LogError("Only One Terrain allowed!");
                return;
            }
            current = this;
            //virtualTexture = new VirtualTexture()
            indexBuffer = new Native2DArray<int>(chunkCount, Allocator.Persistent);
            UnsafeUtility.MemClear(indexBuffer.ptr, sizeof(int) * indexBuffer.Length.x * indexBuffer.Length.y);
            loadedChunks = new NativeList<TerrainChunkBuffer>(100, Allocator.Persistent);
            drawBuffer = new ComputeBuffer(100, sizeof(TerrainDrawData));
            InitializeMesh();
            loadedChunks.Add(new TerrainChunkBuffer
            {
                chunkSize = 1,
                maxHeight = 0,
                minHeight = 0,
                position = 0
            });
        }
        public override void PrepareJob()
        {

        }
        public override void FinishJob()
        {
            if (drawBuffer.count < loadedChunks.Capacity)
            {
                drawBuffer.Dispose();
                drawBuffer = new ComputeBuffer(loadedChunks.Capacity, sizeof(TerrainDrawData));
            }

        }
        public void PreRender(PipelineCamera cam, ref PipelineCommandData data, float4* frustumPlanes)
        {
            culledResults = new NativeArray<TerrainDrawData>(loadedChunks.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            drawCount = 0;
            cullHandle = new CullJob
            {
                chunkOffset = chunkOffset,
                chunkSize = chunkSize,
                culledResult = culledResults.Ptr(),
                count = drawCount.Ptr(),
                frustumPlanes = frustumPlanes,
                loadedChunks = loadedChunks
            }.Schedule(loadedChunks.Length, max(1, loadedChunks.Length / 8));
        }
        public void PostRender(PipelineCamera cam, ref PipelineCommandData data)
        {
            cullHandle.Complete();
            drawBuffer.SetData(culledResults, 0, 0, culledResults.Length);
            CommandBuffer buffer = data.buffer;
            buffer.SetGlobalBuffer(ShaderIDs._TerrainChunk, meshBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._TerrainData, drawBuffer);
            if(drawCount > 0)
                buffer.DrawProcedural(Matrix4x4.identity, drawTerrainMaterial, 0, MeshTopology.Triangles, meshBuffer.count, drawCount);
            culledResults.Dispose();

        }

        public override void OnDisableFunc()
        {
            if (current != this) return;
            current = null;
            indexBuffer.Dispose();
            loadedChunks.Dispose();
            if (drawBuffer != null) meshBuffer.Dispose();
            if (drawBuffer != null) drawBuffer.Dispose();
        }
        [Unity.Burst.BurstCompile]
        private struct CullJob : IJobParallelFor
        {
            public NativeList<TerrainChunkBuffer> loadedChunks;
            public float chunkSize;
            public int2 chunkOffset;
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            [NativeDisableUnsafePtrRestriction]
            public TerrainDrawData* culledResult;
            [NativeDisableUnsafePtrRestriction]
            public int* count;
            public void Execute(int index)
            {
                ref TerrainChunkBuffer buffer = ref loadedChunks[index];
                float curChunkSize = buffer.chunkSize * chunkSize;
                float3 minPos = float3(chunkSize * (float2)(buffer.position + chunkOffset), buffer.minHeight);
                float3 maxPos = float3(minPos.xy + curChunkSize, buffer.maxHeight);
                float3 position = ((maxPos + minPos) * 0.5f).xzy;
                float3 extent = ((maxPos - minPos) * 0.5f).xzy;
                if (MathLib.BoxIntersect(position, extent, frustumPlanes, 6))
                {
                    int v = Interlocked.Increment(ref *count) - 1;
                    ref TerrainDrawData r = ref culledResult[v];
                    r.chunkPos = (uint2)buffer.position;
                    r.worldPos = minPos.xy;
                    r.scale = curChunkSize;
                }
            }
        }
    }
}
