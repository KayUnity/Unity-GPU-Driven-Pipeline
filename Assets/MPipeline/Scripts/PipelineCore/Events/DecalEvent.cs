using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Rendering;
namespace MPipeline
{
    [System.Serializable]
    public unsafe struct DecalEvent
    {
        private const int maxDecalPerCluster = 16;
        private DecalCullJob cullJob;
        private NativeArray<DecalStrct> decalCullResults;
        private NativeArray<DecalIndexCompare> decalCompareResults;
        private JobHandle handle;
        private PropertySetEvent proper;
        private LightingEvent lightingEvt;
        private ComputeBuffer decalBuffer;
        private ComputeBuffer decalIndexBuffer;
        private ComputeShader cbdrShader;
        private Material copyMat;
        public string[] decalAlbedoAtlasNames;
        public string[] decalNormalAtlasNames;
        private RenderTexture decalAlbedoAtlas;
        private RenderTexture decalNormalAtlas;
        public int atlasWidth;
        public int atlasHeight;
        const int INITCOUNT = 20;
        private struct DecalStrct
        {
            public float3x3 rotation;
            public float3x4 worldToLocal;
            public float3 position;
            public float4 albedoScaleOffset;
            public float4 normalScaleOffset;
            public int2 texIndex;
        }
        public void Init(PipelineResources res)
        {
            cbdrShader = res.shaders.cbdrShader;
            copyMat = new Material(res.shaders.copyShader);
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            lightingEvt = RenderPipeline.GetEvent<LightingEvent>();
            decalBuffer = new ComputeBuffer(INITCOUNT, sizeof(DecalStrct));
            decalIndexBuffer = new ComputeBuffer(CBDRSharedData.XRES * CBDRSharedData.YRES * CBDRSharedData.ZRES * (CBDRSharedData.MAXLIGHTPERCLUSTER + 1), sizeof(int));
            if(decalAlbedoAtlasNames.Length > 0)
            {
                decalAlbedoAtlas = new RenderTexture(new RenderTextureDescriptor
                {
                    width = atlasWidth,
                    height = atlasHeight,
                    volumeDepth = decalAlbedoAtlasNames.Length,
                    dimension = TextureDimension.Tex2DArray,
                    msaaSamples = 1,
                    colorFormat = RenderTextureFormat.ARGB32
                });
            }
            if(decalNormalAtlasNames.Length > 0)
            {
                decalNormalAtlas= new RenderTexture(new RenderTextureDescriptor
                {
                    width = atlasWidth,
                    height = atlasHeight,
                    volumeDepth = decalAlbedoAtlasNames.Length,
                    dimension = TextureDimension.Tex2DArray,
                    msaaSamples = 1,
                    colorFormat = RenderTextureFormat.RGHalf
                });
            }
            for(int i = 0; i < decalAlbedoAtlasNames.Length; ++i)
            {
                Graphics.SetRenderTarget(decalAlbedoAtlas.colorBuffer, decalAlbedoAtlas.depthBuffer, 0, CubemapFace.Unknown, i);
                Texture tex = Resources.Load<Texture>(decalAlbedoAtlasNames[i]);
                copyMat.SetTexture(ShaderIDs._MainTex, tex);
                copyMat.SetPass(0);
                Graphics.DrawMeshNow(GraphicsUtility.mesh, Matrix4x4.identity);
                Resources.UnloadAsset(tex);
            }
            for(int i = 0; i < decalNormalAtlasNames.Length; ++i)
            {
                Graphics.SetRenderTarget(decalNormalAtlas.colorBuffer, decalNormalAtlas.depthBuffer, 0, CubemapFace.Unknown, i);
                Texture tex = Resources.Load<Texture>(decalNormalAtlasNames[i]);
                copyMat.SetTexture(ShaderIDs._MainTex, tex);
                copyMat.SetPass(1);
                Graphics.DrawMeshNow(GraphicsUtility.mesh, Matrix4x4.identity);
                Resources.UnloadAsset(tex);
            }

            Object.DestroyImmediate(copyMat); 
        }

        public void Dispose()
        {
            decalBuffer.Dispose();
            decalIndexBuffer.Dispose();
        }
        private struct DecalIndexCompare : IFunction<DecalIndexCompare, int>
        {
            public int importance;
            public int index;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public int Run(ref DecalIndexCompare comp)
            {
                return importance - comp.importance;
            }
        }
        public void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (!Decal.decalDatas.isCreated) return;
            decalCullResults = new NativeArray<DecalStrct>(Decal.allDecalCount, Allocator.Temp);
            decalCompareResults = new NativeArray<DecalIndexCompare>(Decal.allDecalCount, Allocator.Temp);
            float2 albedoSize;
            float2 normalSize;
            cullJob = new DecalCullJob
            {
                count = 0,
                decalDatas = decalCullResults.Ptr(),
                frustumPlanes = (float4*)proper.frustumPlanes.Ptr(),
                availiableDistanceSqr = lightingEvt.cbdrDistance * lightingEvt.cbdrDistance,
                camPos = cam.cam.transform.position,
                indexCompares = decalCompareResults.Ptr(),
                allDatas = Decal.decalDatas.unsafePtr,
                albedoAtlasSize = atlasWidth,
                normalAtlasSize = atlasHeight,
            };
            handle = cullJob.ScheduleRefBurst(Decal.allDecalCount, max(1, Decal.allDecalCount / 4));
            handle = new DecalSortJob
            {
                compares = decalCompareResults.Ptr(),
                count = cullJob.count.Ptr(),
                decalDatas = decalCullResults.Ptr(),
            }.Schedule(handle);
        }

        public void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (!Decal.decalDatas.isCreated) return;
            CommandBuffer buffer = data.buffer;
            handle.Complete();
            DecalStrct* resulPtr = decalCullResults.Ptr();
            if (cullJob.count > decalBuffer.count)
            {
                int oldCount = decalBuffer.count;
                decalBuffer.Dispose();
                decalBuffer = new ComputeBuffer((int)max(oldCount * 1.5f, cullJob.count), sizeof(DecalStrct));
            }
            decalBuffer.SetData(decalCullResults, 0, 0, cullJob.count);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._XYPlaneTexture, lightingEvt.cbdr.xyPlaneTexture);
            buffer.SetComputeTextureParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._ZPlaneTexture, lightingEvt.cbdr.zPlaneTexture);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._AllDecals, decalBuffer);
            buffer.SetComputeBufferParam(cbdrShader, CBDRSharedData.DecalCull, ShaderIDs._DecalIndexBuffer, decalIndexBuffer);
            buffer.SetComputeIntParam(cbdrShader, ShaderIDs._DecalCount, cullJob.count);
            buffer.DispatchCompute(cbdrShader, CBDRSharedData.DecalCull, 1, 1, CBDRSharedData.ZRES);
            buffer.SetGlobalTexture(ShaderIDs._DecalAtlas, decalAlbedoAtlas);
            buffer.SetGlobalTexture(ShaderIDs._DecalNormalAtlas, decalNormalAtlas);
            buffer.SetGlobalBuffer(ShaderIDs._DecalIndexBuffer, decalIndexBuffer);
            buffer.SetGlobalBuffer(ShaderIDs._AllDecals, decalBuffer);
            decalCullResults.Dispose();
            decalCompareResults.Dispose();
        }
        [Unity.Burst.BurstCompile]
        private struct DecalCullJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public DecalData* allDatas;
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            [NativeDisableUnsafePtrRestriction]
            public DecalIndexCompare* indexCompares;
            public DecalStrct* decalDatas;
            public int count;
            public float availiableDistanceSqr;
            public float3 camPos;
            public float2 albedoAtlasSize;
            public float2 normalAtlasSize;
            public void Execute(int index)
            {
                ref DecalData data = ref allDatas[index];
                float3x3 rotation = float3x3(data.rotation.c0.xyz, data.rotation.c1.xyz, data.rotation.c2.xyz);
                float dist = data.avaliableDistance * data.avaliableDistance;
                if (lengthsq(camPos - data.position) < min(dist, availiableDistanceSqr) && MathLib.BoxIntersect(rotation, data.position, frustumPlanes, 6))
                {
                    int currentInd = System.Threading.Interlocked.Increment(ref count) - 1;
                    ref DecalStrct str = ref decalDatas[currentInd];
                    str.rotation = rotation;
                    str.position = data.position;
                    str.albedoScaleOffset = (data.albedoScaleOffset + float4(0,0,0.5f,0.5f)) / float4(albedoAtlasSize, albedoAtlasSize);
                    str.normalScaleOffset = (data.normalScaleOffset + float4(0, 0, 0.5f, 0.5f)) / float4(normalAtlasSize, normalAtlasSize);
                    str.texIndex = data.texIndex;
                    str.worldToLocal = data.worldToLocal;
                    indexCompares[currentInd] = new DecalIndexCompare
                    {
                        importance = data.importance,
                        index = currentInd
                    };
                }
            }
        }
        [Unity.Burst.BurstCompile]
        private struct DecalSortJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public int* count;
            [NativeDisableUnsafePtrRestriction]
            public DecalIndexCompare* compares;
            [NativeDisableUnsafePtrRestriction]
            public DecalStrct* decalDatas;
            public void Execute()
            {
                MathLib.Quicksort(compares, 0, *count - 1);
                DecalStrct* tempDatas = stackalloc DecalStrct[*count];
                for (int i = 0; i < *count; ++i)
                {
                    tempDatas[i] = decalDatas[compares[i].index];
                }
                UnsafeUtility.MemCpy(decalDatas, tempDatas, *count * sizeof(DecalStrct));
            }
        }

    }
}
