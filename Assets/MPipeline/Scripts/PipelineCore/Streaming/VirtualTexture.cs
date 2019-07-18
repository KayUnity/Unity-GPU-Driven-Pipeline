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
    public enum VirtualTextureSize
    {
        x8 = 8,
        x16 = 16,
        x32 = 32,
        x64 = 64,
        x128 = 128,
        x256 = 256,
        x512 = 512,
        x1024 = 1024,
        x2048 = 2048,
        x4096 = 4096,
        x8192 = 8192
    };
    public struct VirtualTextureFormat
    {
        public VirtualTextureSize perElementSize { get; private set; }
        public RenderTextureFormat format { get; private set; }
        public int rtPropertyID { get; private set; }
        public VirtualTextureFormat(VirtualTextureSize size, RenderTextureFormat format, string rtName)
        {
            perElementSize = size;
            this.format = format;
            rtPropertyID = Shader.PropertyToID(rtName);
        }
    }
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
            public int LeftedElement
            {
                get
                {
                    return arrayPool.Length;
                }
            }
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
        private ComputeShader shader;
        public RenderTexture indexTex { get; private set; }
        private RenderTexture[] textures;
        private Native2DArray<float4> indexBuffers;
        private TexturePool pool;
        private ComputeBuffer commandListBuffer;
        private int loadNewTexFrameCount;
        private NativeArray<VirtualTextureFormat> allFormats;
        public RenderTexture GetTexture(int index)
        {
            return textures[index];
        }
        public int LeftedTextureElement
        {
            get
            {
                return pool.LeftedElement;
            }
        }
        public void Update()
        {
            CommandBuffer beforeFrameBuffer = RenderPipeline.BeforeFrameBuffer;
            beforeFrameBuffer.SetGlobalTexture(ShaderIDs._IndexTexture, indexTex);
            for(int i = 0; i < allFormats.Length; ++i)
            {
                beforeFrameBuffer.SetGlobalTexture(allFormats[i].rtPropertyID, textures[i]);
            }
        }
        /// <summary>
        /// Init Virtual Texture
        /// </summary>
        /// <param name="perTextureSize">Virtual texture's basic size</param>
        /// <param name="maximumSize">Virtual texture's array size</param>
        /// <param name="indexSize">Index Texture's size</param>
        /// <param name="formats">Each VT's format</param>
        public VirtualTexture(int maximumSize, int indexSize, NativeArray<VirtualTextureFormat> formats)
        {
            allFormats = new NativeArray<VirtualTextureFormat>(formats.Length, Allocator.Persistent);
            UnsafeUtility.MemCpy(allFormats.GetUnsafePtr(), formats.GetUnsafePtr(), sizeof(VirtualTextureFormat) * formats.Length);
            shader = Resources.Load<ComputeShader>("VirtualTexture");
            commandListBuffer = new ComputeBuffer(64, sizeof(SetIndexCommand));
            indexBuffers = new Native2DArray<float4>(indexSize, Allocator.Persistent);
            pool = new TexturePool(maximumSize);
            indexTex = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                width = indexSize,
                height = indexSize,
                volumeDepth = 1,
                msaaSamples = 1
            });
            indexTex.filterMode = FilterMode.Point;
            indexTex.Create();
            textures = new RenderTexture[formats.Length];
            for (int i = 0; i < formats.Length; ++i)
            {
                VirtualTextureFormat format = formats[i];
                textures[i] = new RenderTexture(new RenderTextureDescriptor
                {
                    colorFormat = format.format,
                    depthBufferBits = 0,
                    dimension = TextureDimension.Tex2DArray,
                    enableRandomWrite = true,
                    width = (int)format.perElementSize,
                    height = (int)format.perElementSize,
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
            allFormats.Dispose();
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
            if (loadNewTexFrameCount == Time.frameCount)
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
            CommandBuffer beforeFrameBuffer = RenderPipeline.BeforeFrameBuffer;
            commandListBuffer.SetData(lsts);
            beforeFrameBuffer.SetComputeBufferParam(shader, 0, ShaderIDs._CommandBuffer, commandListBuffer);
            beforeFrameBuffer.SetComputeTextureParam(shader, 0, ShaderIDs._IndexTexture, indexTex);

            ComputeShaderUtility.Dispatch(shader, beforeFrameBuffer, 0, lsts.Length);
            lsts.Dispose();
#if UNITY_EDITOR
            if (targetIndex < 0) throw new System.Exception("Virtual Texture Pool is out of range!!");
#endif
            return targetIndex;
        }
        /// <summary>
        /// Unload space
        /// </summary>
        /// <param name="startIndex">Start Index in IndexTexture </param>
        /// <param name="size">Target Size in IndexTexture</param>
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
        /// <summary>
        /// Combine Textures in a fixed space
        /// </summary>
        /// <param name="startIndex">Start Index in IndexTexture </param>
        /// <param name="size">Target Size in IndexTexture</param>
        public void CombineTexture(int2 startIndex, int size)
        {
#if UNITY_EDITOR
            if (loadNewTexFrameCount == Time.frameCount)
            {
                throw new System.Exception("Can't Call this function more than one times per frame!");
            }
            loadNewTexFrameCount = Time.frameCount;
#endif
            int targetIndex = pool.Get();
#if UNITY_EDITOR
            if (targetIndex < 0) throw new System.Exception("Virtual Texture Pool is out of range!!");
#endif
            NativeArray<SetIndexCommand> lsts = new NativeArray<SetIndexCommand>(size * size, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int x = 0; x < size; ++x)
                for (int y = 0; y < size; ++y)
                {
                    ref float4 v = ref indexBuffers[int2(x, y) + startIndex];
                    pool.Return(v.w);
                    v = float4(1f / size, float2((float)x / size, (float)y / size), targetIndex + 0.2f);
                    lsts[y * size + x] = new SetIndexCommand
                    {
                        pos = (uint2)(int2(x, y) + startIndex),
                        targetFloat = v
                    };
                }
            CommandBuffer buffer = RenderPipeline.BeforeFrameBuffer;
            buffer.SetComputeTextureParam(shader, 1, ShaderIDs._IndexTexture, indexTex);
            buffer.SetComputeIntParam(shader, ShaderIDs._TargetElement, targetIndex);
            
            for(int i = 0; i < textures.Length; ++i)
            {
                int perTextureSize = (int)allFormats[i].perElementSize;
                buffer.SetComputeVectorParam(shader, ShaderIDs._TextureSize, float4(perTextureSize, indexTex.width, startIndex));
                buffer.SetComputeTextureParam(shader, 1, ShaderIDs._VirtualTexture, textures[i]);
                buffer.DispatchCompute(shader, 1, perTextureSize / 8, perTextureSize / 8, 1);
            }
            commandListBuffer.SetData(lsts);
            buffer.SetComputeBufferParam(shader, 0, ShaderIDs._CommandBuffer, commandListBuffer);
            buffer.SetComputeTextureParam(shader, 0, ShaderIDs._IndexTexture, indexTex);
            ComputeShaderUtility.Dispatch(shader, buffer, 0, lsts.Length);
            lsts.Dispose();
        }
    }
}
