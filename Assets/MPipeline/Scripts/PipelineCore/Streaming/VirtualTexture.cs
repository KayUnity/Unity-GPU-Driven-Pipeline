using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Mathematics.math;
using Unity.Mathematics;
namespace MPipeline
{
    [System.Serializable]
    public unsafe struct VirtualTexture
    {
        private struct SetIndexCommand
        {
            public float4 targetFloat;
            public uint2 pos;
        };
        private struct TexturePool
        {
            private NativeArray<bool> marks;
            private NativeList<int> arrayPool;

            public TexturePool(int capacity)
            {
                marks = new NativeArray<bool>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                arrayPool = new NativeList<int>(capacity, Allocator.Persistent);
                for (int i = 0; i < capacity; ++i)
                {
                    arrayPool.Add(i);
                }
            }
            public void Dispose()
            {
                marks.Dispose();
                arrayPool.Dispose();
            }
            public void Return(float index)
            {
                int i = (int)index;
                if (index >= 0 && marks[i])
                {
                    marks[i] = false;
                    arrayPool.Add(i);
                }
            }
            public int Get()
            {
                int t;
                do
                {
                    if (arrayPool.Length <= 0) return -1;
                    t = arrayPool[arrayPool.Length - 1];
                    arrayPool.RemoveLast();
                } while (marks[t]);
                marks[t] = true;
                return t;
            }
        }
        public ComputeShader shader;
        public RenderTexture indexTex { get; private set; }
        public RenderTexture[] textures { get; private set; }
        private Native2DArray<float4> indexBuffers;
        private TexturePool pool;
        private ComputeBuffer commandListBuffer;
        private int loadNewTexFrameCount;

        public void Init(int2 perTextureSize, int maximumSize, int2 indexSize, NativeArray<RenderTextureFormat> formats)
        {
            commandListBuffer = new ComputeBuffer(64, sizeof(SetIndexCommand));
            indexBuffers = new Native2DArray<float4>(indexSize, Allocator.Persistent);
            pool = new TexturePool(maximumSize);
            indexTex = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                width = indexSize.x,
                height = indexSize.y,
                volumeDepth = 1,
                msaaSamples = 1
            });
            indexTex.Create();
            textures = new RenderTexture[formats.Length];
            for (int i = 0; i < formats.Length; ++i)
            {
                textures[i] = new RenderTexture(new RenderTextureDescriptor
                {
                    colorFormat = formats[i],
                    depthBufferBits = 0,
                    dimension = TextureDimension.Tex2DArray,
                    enableRandomWrite = true,
                    width = perTextureSize.x,
                    height = perTextureSize.y,
                    volumeDepth = maximumSize,
                    msaaSamples = 1
                });
                textures[i].Create();
            }
            loadNewTexFrameCount = -1;
            indexBuffers.SetAll(float4(0, 0, 0, -1));
        }
        public void Dispose()
        {
            Object.DestroyImmediate(indexTex);
            foreach (var i in textures)
            {
                Object.DestroyImmediate(i);
            }
            indexBuffers.Dispose();
            pool.Dispose();
            commandListBuffer.Dispose();
        }
        /// <summary>
        /// load a new texture into virtual texture
        /// </summary>
        /// <param name="startIndex">Start Index in the index texture</param>
        /// <param name="size">Pixel count in index texture</param>
        /// <returns>The target array index in TextureArray, return -1 if the pool is full</returns>
        public int LoadNewTexture(int2 startIndex, int size)
        {
#if UNITY_EDITOR
            if(loadNewTexFrameCount == Time.frameCount)
            {
                throw new System.Exception("Can't Call this function more than one times per frame!");
            }
            loadNewTexFrameCount = Time.frameCount;
#endif
            for (int x = 0; x < size; ++x)
                for (int y = 0; y < size; ++y)
                {
                    ref float4 v = ref indexBuffers[int2(x, y) + startIndex];
                    pool.Return(v.w);
                    v.w = -1;

                }
            int targetIndex = pool.Get();
            NativeArray<SetIndexCommand> lsts = new NativeArray<SetIndexCommand>(size * size, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int x = 0; x < size; ++x)
                for (int y = 0; y < size; ++y)
                {
                    ref float4 v = ref indexBuffers[int2(x, y) + startIndex];
                    v = float4(1f / size, float2((float)x / size, (float)y / size), targetIndex + 0.2f);
                    lsts[y * size + x] = new SetIndexCommand
                    {
                        pos = (uint2)(int2(x, y) + startIndex),
                        targetFloat = v
                    };
                }
            commandListBuffer.SetData(lsts);
            CommandBuffer beforeFrameBuffer = RenderPipeline.BeforeFrameBuffer;
            beforeFrameBuffer.SetComputeBufferParam(shader, 0, ShaderIDs._CommandBuffer, commandListBuffer);
            beforeFrameBuffer.SetComputeTextureParam(shader, 0, ShaderIDs._IndexTexture, indexTex);
            ComputeShaderUtility.Dispatch(shader, beforeFrameBuffer, 0, lsts.Length);
            lsts.Dispose();
#if UNITY_EDITOR
            if (targetIndex < 0) throw new System.Exception("Virtual Texture Pool is out of range!!");
#endif
            return targetIndex;
        }
        public void UnloadTexture(int2 startIndex, int size)
        {
#if UNITY_EDITOR
            if (loadNewTexFrameCount == Time.frameCount)
            {
                throw new System.Exception("Can't Call this function more than one times per frame!");
            }
            loadNewTexFrameCount = Time.frameCount;
#endif
            NativeArray<SetIndexCommand> lsts = new NativeArray<SetIndexCommand>(size * size, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int x = 0; x < size; ++x)
                for (int y = 0; y < size; ++y)
                {
                    ref float4 v = ref indexBuffers[int2(x, y) + startIndex];
                    pool.Return(v.w);
                    v.w = -1;
                    lsts[y * size + x] = new SetIndexCommand
                    {
                        pos = (uint2)(int2(x, y) + startIndex),
                        targetFloat = v
                    };
                }
            commandListBuffer.SetData(lsts);
            CommandBuffer beforeFrameBuffer = RenderPipeline.BeforeFrameBuffer;
            beforeFrameBuffer.SetComputeBufferParam(shader, 0, ShaderIDs._CommandBuffer, commandListBuffer);
            beforeFrameBuffer.SetComputeTextureParam(shader, 0, ShaderIDs._IndexTexture, indexTex);
            ComputeShaderUtility.Dispatch(shader, beforeFrameBuffer, 0, lsts.Length);
            lsts.Dispose();
        }
    }
}
