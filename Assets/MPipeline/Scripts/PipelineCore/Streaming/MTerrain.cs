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
    public unsafe sealed class MTerrain : MonoBehaviour
    {
        public static MTerrain current { get; private set; }
        public struct TerrainChunkBuffer
        {
            public int2 position;
            public int chunkSize;
            public float minHeight;
            public float maxHeight;
        }
        public int chunkCount = 80;
        [Range(0f, 100f)]
        public float chunkSize = 10;
        public int planarResolution;
        public Material drawTerrainMaterial;
        //private VirtualTexture virtualTexture;
        private Native2DArray<int> indexBuffer;
        private ComputeBuffer meshBuffer;
        private ComputeBuffer constSettings;
        private ComputeBuffer removeIndexBuffer;
        private ComputeBuffer culledResultsBuffer;
        private ComputeBuffer loadedBuffer;
        private ComputeBuffer dispatchDrawBuffer;
        private ComputeShader shader;
        private NativeList<TerrainChunkBuffer> loadedBufferList;
        public int2 chunkOffset;
        private static Vector4[] planes = new Vector4[6];
        struct TerrainSettings
        {
            public int2 terrainOffset;
            public float perChunkSize;
            float useless;
        };

        struct LODJudging
        {
            private bool sb;
            //Get LOD Level of a loaded terrain;
            public int Run(ref TerrainChunkBuffer buffer, int index)
            {
                if (index == 0)
                {
                    if (!sb)
                    {
                        sb = true;
                        return 0;
                    }
                }
                return 1;
            }
        }

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

        void OnEnable()
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
            InitializeMesh();
            constSettings = new ComputeBuffer(1, sizeof(TerrainSettings), ComputeBufferType.Constant);
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
                chunkSize = 1,
                maxHeight = 0,
                minHeight = 0,
                position = 0
            });

            Add(inBuf);
        }

        private void Update()
        {
            NativeArray<TerrainSettings> setting = new NativeArray<TerrainSettings>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            setting[0] = new TerrainSettings
            {
                perChunkSize = chunkSize,
                terrainOffset = chunkOffset
            };
            constSettings.SetData(setting);
            setting.Dispose();
        }

        void Add(NativeList<TerrainChunkBuffer> targetBuffers)
        {
            int oldCount = loadedBufferList.Length;
            loadedBufferList.AddRange(targetBuffers);
            if(loadedBuffer.count < loadedBufferList.Length)
            {
                loadedBuffer.Dispose();
                culledResultsBuffer.Dispose();
                loadedBuffer = new ComputeBuffer(loadedBufferList.Length, sizeof(TerrainChunkBuffer));
                culledResultsBuffer = new ComputeBuffer(loadedBufferList.Length, sizeof(int));
                loadedBuffer.SetDataPtr(loadedBufferList.unsafePtr, loadedBufferList.Length);
            }else
            {
                loadedBuffer.SetDataPtr(targetBuffers.unsafePtr, oldCount, targetBuffers.Length);
            }
        }

        void Remove(LODJudging functor)
        {
            NativeList<uint2> removeList = new NativeList<uint2>(10, Allocator.Temp);

            TerrainChunkBuffer* arrayPtr = loadedBufferList.unsafePtr;
            for (int i = loadedBufferList.Length - 1; i >= 0; --i)
            {
                int res = functor.Run(ref arrayPtr[i], i);
                switch (res)
                {
                    case 0:
                        loadedBufferList.RemoveLast();
                        if (i < loadedBufferList.Length)
                        {
                            removeList.Add(uint2((uint)i, (uint)loadedBufferList.Length));
                        }
                        break;
                }
            }
            if (removeIndexBuffer.count < removeList.Length)
            {
                removeIndexBuffer.Dispose();
                removeIndexBuffer = new ComputeBuffer(removeList.Length, sizeof(uint2));
            }
            if (removeList.Length > 0)
            {
                removeIndexBuffer.SetDataPtr<uint2>(removeList.unsafePtr, 0, removeList.Length);
                CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
                buffer.SetComputeBufferParam(shader, 0, ShaderIDs._TerrainChunks, loadedBuffer);
                buffer.SetComputeBufferParam(shader, 0, ShaderIDs._IndexBuffer, removeIndexBuffer);
                ComputeShaderUtility.Dispatch(shader, buffer, 0, removeList.Length);
            }
            removeList.Dispose();
        }

        public void DrawTerrain(CommandBuffer buffer, int pass, Vector4[] planes)
        {
            if (loadedBufferList.Length <= 0) return;
            buffer.SetComputeBufferParam(shader, 2, ShaderIDs._DispatchBuffer, dispatchDrawBuffer);
            buffer.SetComputeBufferParam(shader, 1, ShaderIDs._DispatchBuffer, dispatchDrawBuffer);
            buffer.SetComputeBufferParam(shader, 1, ShaderIDs._CullResultBuffer, culledResultsBuffer);
            buffer.SetComputeBufferParam(shader, 1, ShaderIDs._TerrainChunks, loadedBuffer);
            buffer.SetGlobalConstantBuffer(constSettings, ShaderIDs.TerrainSettings, 0, sizeof(TerrainSettings));
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

        void OnDisable()
        {
            if (current != this) return;
            current = null;
            indexBuffer.Dispose();
            if (meshBuffer != null) meshBuffer.Dispose();
            if (constSettings != null) constSettings.Dispose();
            if (removeIndexBuffer != null) removeIndexBuffer.Dispose();
            if (culledResultsBuffer != null) culledResultsBuffer.Dispose();
            if (loadedBuffer != null) loadedBuffer.Dispose();
            if (dispatchDrawBuffer != null) dispatchDrawBuffer.Dispose();
            if (loadedBufferList.isCreated) loadedBufferList.Dispose();
        }
    }
}
