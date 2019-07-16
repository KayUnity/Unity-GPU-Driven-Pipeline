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
            public float2 worldPos;
            public float2 minMaxHeight;
            public float scale;
        }
        public int chunkCount = 8;
        [Range(0f, 1000f)]
        public double chunkSize = 10;
        public double[] lodDistances;
        private float basicChunkSize = 0;
        public int planarResolution;
        public Material drawTerrainMaterial;
        //private VirtualTexture virtualTexture;
        private ComputeBuffer meshBuffer;
        private ComputeBuffer removeIndexBuffer;
        private ComputeBuffer culledResultsBuffer;
        private ComputeBuffer loadedBuffer;
        private ComputeBuffer dispatchDrawBuffer;
        private ComputeShader shader;
        private NativeList<TerrainChunkBuffer> loadedBufferList;
        public int2 chunkOffset;
        private static Vector4[] planes = new Vector4[6];

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

        public override void PrepareJob()
        {

        }

        public override void FinishJob()
        {

        }

        protected override void OnEnableFunc()
        {
            if (current && current != this)
            {
                enabled = false;
                Debug.LogError("Only One Terrain allowed!");
                return;
            }
            current = this;
            //virtualTexture = new VirtualTexture()
            basicChunkSize = (float)(chunkSize / pow(2.0, max(1, lodDistances.Length)));
            InitializeMesh();
            dispatchDrawBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            removeIndexBuffer = new ComputeBuffer(10, sizeof(int2));
            const int INIT_LENGTH = 200;
            culledResultsBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(int));
            loadedBuffer = new ComputeBuffer(INIT_LENGTH, sizeof(TerrainChunkBuffer));
            loadedBufferList = new NativeList<TerrainChunkBuffer>(INIT_LENGTH, Allocator.Persistent);
            shader = Resources.Load<ComputeShader>("TerrainCompute");
            NativeArray<uint> dispatchDraw = new NativeArray<uint>(5, Allocator.Temp, NativeArrayOptions.ClearMemory);
            dispatchDraw[0] = (uint)meshBuffer.count;
            dispatchDrawBuffer.SetData(dispatchDraw);


            NativeList<TerrainChunkBuffer> inBuf = new NativeList<TerrainChunkBuffer>(1, Allocator.Temp);
            inBuf.Add(new TerrainChunkBuffer
            {
                minMaxHeight = 0,
                scale = 10,
                worldPos = 0
            });
            Add(inBuf);
        }

        void Add(NativeList<TerrainChunkBuffer> targetBuffers)
        {
            int oldCount = loadedBufferList.Length;
            loadedBufferList.AddRange(targetBuffers);
            if (loadedBuffer.count < loadedBufferList.Length)
            {
                loadedBuffer.Dispose();
                culledResultsBuffer.Dispose();
                loadedBuffer = new ComputeBuffer(loadedBufferList.Length, sizeof(TerrainChunkBuffer));
                culledResultsBuffer = new ComputeBuffer(loadedBufferList.Length, sizeof(int));
                loadedBuffer.SetDataPtr(loadedBufferList.unsafePtr, loadedBufferList.Length);
            }
            else
            {
                loadedBuffer.SetDataPtr(targetBuffers.unsafePtr, oldCount, targetBuffers.Length);
            }
        }

        void Remove(NativeList<int> removeCommands)
        {
            int removeCount = removeCommands.Length;
            int newCount = loadedBufferList.Length - removeCount;
            for (int i = 0; i < removeCommands.Length; ++i)
            {
                ref int index = ref removeCommands[i];
                if (index >= loadedBufferList.Length || index < 0)
                {
                    index = removeCommands[removeCommands.Length - 1];
                    removeCommands.RemoveLast();
                    --i;
                }
                else if (index >= newCount)
                {
                    loadedBufferList[index].scale = 0;
                    index = removeCommands[removeCommands.Length - 1];
                    removeCommands.RemoveLast();
                    --i;
                }
            }
            uint2* removeIndex = stackalloc uint2[removeCommands.Length];
            int removeCommandCount = 0;
            for (int i = 0; i < removeCommands.Length; ++i)
            {
                if (newCount >= loadedBufferList.Length) break;
                while (loadedBufferList[newCount].scale < 1e-8f)
                {
                    newCount++;
                    if (newCount >= loadedBufferList.Length) break;
                }
                int index = removeCommands[i];
                loadedBufferList[index] = loadedBufferList[newCount];
                removeIndex[i] = uint2((uint)index, (uint)newCount);
                newCount++;
                removeCommandCount++;

            }
            loadedBufferList.RemoveLast(removeCount);
            if (removeCommandCount > 0)
            {
                if (removeIndexBuffer.count < removeCommandCount)
                {
                    removeIndexBuffer.Dispose();
                    removeIndexBuffer = new ComputeBuffer(removeCommandCount, sizeof(uint2));
                }
                removeIndexBuffer.SetDataPtr<uint2>(removeIndex, 0, removeCommandCount);
                CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
                buffer.SetComputeBufferParam(shader, 0, ShaderIDs._TerrainChunks, loadedBuffer);
                buffer.SetComputeBufferParam(shader, 0, ShaderIDs._IndexBuffer, removeIndexBuffer);
                ComputeShaderUtility.Dispatch(shader, buffer, 0, removeCommandCount);
            }
        }

        public void DrawTerrain(CommandBuffer buffer, int pass, Vector4[] planes)
        {
            if (loadedBufferList.Length <= 0) return;
            buffer.SetComputeBufferParam(shader, 2, ShaderIDs._DispatchBuffer, dispatchDrawBuffer);
            buffer.SetComputeBufferParam(shader, 1, ShaderIDs._DispatchBuffer, dispatchDrawBuffer);
            buffer.SetComputeBufferParam(shader, 1, ShaderIDs._CullResultBuffer, culledResultsBuffer);
            buffer.SetComputeBufferParam(shader, 1, ShaderIDs._TerrainChunks, loadedBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._TerrainMeshBuffer, meshBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._TerrainChunks, loadedBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._CullResultBuffer, culledResultsBuffer);
            buffer.SetComputeVectorArrayParam(shader, ShaderIDs.planes, planes);
            buffer.DispatchCompute(shader, 2, 1, 1, 1);
            ComputeShaderUtility.Dispatch(shader, buffer, 1, loadedBufferList.Length);
            buffer.DrawProceduralIndirect(Matrix4x4.identity, drawTerrainMaterial, pass, MeshTopology.Triangles, dispatchDrawBuffer);
        }

        public void DrawTerrain(CommandBuffer buffer, int pass, float4* planePtr)
        {
            UnsafeUtility.MemCpy(planes.Ptr(), planePtr, sizeof(float4) * 6);
            DrawTerrain(buffer, pass, planes);
        }

        protected override void OnDisableFunc()
        {
            if (current != this) return;
            current = null;
            if (meshBuffer != null) meshBuffer.Dispose();
            if (removeIndexBuffer != null) removeIndexBuffer.Dispose();
            if (culledResultsBuffer != null) culledResultsBuffer.Dispose();
            if (loadedBuffer != null) loadedBuffer.Dispose();
            if (dispatchDrawBuffer != null) dispatchDrawBuffer.Dispose();
            if (loadedBufferList.isCreated) loadedBufferList.Dispose();
        }
    }
}
