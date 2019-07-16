using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [RequireEvent(typeof(LightingEvent), typeof(ReflectionEvent))]
    [CreateAssetMenu(menuName = "GPURP Events/Volumetric Scattering")]
    public unsafe class VolumetricLightEvent : PipelineEvent
    {
        public float availableDistance = 64;
        [Range(0.01f, 100f)]
        public float indirectIntensity = 1;
        const int marchStep = 64;
        const int scatterPass = 16;
        const bool useEmissionGeometry = false;
        const bool useNoiseEvents = false;
        static readonly int3 downSampledSize = new int3(160, 90, 128);
        public float linearFogDensity = 0.02f;
        private JobHandle jobHandle;
        private NativeArray<FogVolume> resultVolume;
        private int fogCount = 0;
        private LightingEvent lightingData;
        private ReflectionEvent reflectData;
        private Material lightingMat;
        private VolumetricGeometry geometry;
        private VolumetricNoise noiseEvents;
        private PropertySetEvent proper;
        private ComputeBuffer cameraNormalBuffer;
        private Cubemap blackCB;
        private RenderTextureDescriptor volumeDesc;
        private RenderTexture volumeTex;
        [Range(0f, 1f)]
        public float darkerWeight = 0.75f;
        [Range(0f, 1f)]
        public float brighterWeight = 0.95f;

        public override bool CheckProperty()
        {
            if (useNoiseEvents && !noiseEvents.Check())
            {
                return false;
            }
            if (useEmissionGeometry && !geometry.Check())
            {
                return false;
            }
            return lightingMat && blackCB;
        }
        protected override void Init(PipelineResources resources)
        {
            proper = RenderPipeline.GetEvent<PropertySetEvent>();
            if (useEmissionGeometry)
                geometry.Init();
            if (useNoiseEvents)
                noiseEvents.Init(resources);
            lightingData = RenderPipeline.GetEvent<LightingEvent>();
            reflectData = RenderPipeline.GetEvent<ReflectionEvent>();
            lightingMat = new Material(resources.shaders.volumetricShader);
            cameraNormalBuffer = new ComputeBuffer(3, sizeof(float3));
            blackCB = new Cubemap(1, TextureFormat.ARGB32, false);
            blackCB.SetPixel(CubemapFace.NegativeX, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.NegativeY, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.NegativeZ, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.PositiveX, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.PositiveX, 0, 0, Color.black);
            blackCB.SetPixel(CubemapFace.PositiveX, 0, 0, Color.black);
            volumeDesc = new RenderTextureDescriptor
            {
                autoGenerateMips = false,
                bindMS = false,
                colorFormat = RenderTextureFormat.ARGBHalf,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex3D,
                enableRandomWrite = true,
                height = downSampledSize.y,
                width = downSampledSize.x,
                memoryless = RenderTextureMemoryless.None,
                msaaSamples = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                sRGB = false,
                useMipMap = false,
                volumeDepth = downSampledSize.z,
                vrUsage = VRTextureUsage.None
            };
            volumeTex = new RenderTexture(volumeDesc);
            volumeTex.filterMode = FilterMode.Bilinear;
            volumeTex.wrapMode = TextureWrapMode.Clamp;
            volumeTex.Create();
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            ref CBDRSharedData cbdr = ref lightingData.cbdr;
            cbdr.availiableDistance = availableDistance;
            fogCount = 0;

            if (FogVolumeComponent.allVolumes.isCreated && FogVolumeComponent.allVolumes.Length > 0)
            {
                resultVolume = new NativeArray<FogVolume>(FogVolumeComponent.allVolumes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                float4* frustumPlanes = (float4*)UnsafeUtility.Malloc(6 * sizeof(float4), 16, Allocator.Temp);
                UnsafeUtility.MemCpy(frustumPlanes, proper.frustumPlanes.Ptr(), 6 * sizeof(float4));
                Transform camTrans = cam.transform;
                float3 inPoint = camTrans.position + camTrans.forward * cbdr.availiableDistance;
                float3 normal = camTrans.forward;
                float4 plane = float4(normal, -dot(normal, inPoint));
                frustumPlanes[5] = plane;
                jobHandle = (new FogVolumeCalculate
                {
                    allVolume = resultVolume.Ptr(),
                    frustumPlanes = frustumPlanes,
                    fogVolumeCount = fogCount.Ptr(),
                    fogVolume = FogVolumeComponent.allVolumes.unsafePtr
                }).Schedule(FogVolumeComponent.allVolumes.Length,max(1,  FogVolumeComponent.allVolumes.Length / 4));
            }
            if (useEmissionGeometry)
                geometry.PreRender(ref data, proper);
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            bool useIBLIndirect = reflectData.reflectionCount > 0;
            CommandBuffer buffer = data.buffer;
            ComputeShader scatter = data.resources.shaders.volumetricScattering;
            ref CBDRSharedData cbdr = ref lightingData.cbdr;
            int pass = 0;
            if (cbdr.dirLightShadowmap != null)
                pass |= 0b0010;
            if (cbdr.pointshadowCount > 0)
                pass |= 0b0001;
            if (cbdr.spotShadowCount > 0)
                pass |= 0b0100;
            if (useIBLIndirect)
                pass |= 0b1000;
            //TODO
            //Enable fourth bit as Global Illumination

            HistoryVolumetric historyVolume = IPerCameraData.GetProperty(cam, () => new HistoryVolumetric());
            //Volumetric Light
            buffer.SetGlobalTexture(ShaderIDs._VolumeTex, volumeTex);
            if (!historyVolume.lastVolume)
            {
                historyVolume.lastVolume = new RenderTexture(volumeDesc);
                historyVolume.lastVolume.filterMode = FilterMode.Bilinear;
                historyVolume.lastVolume.wrapMode = TextureWrapMode.Clamp;
                historyVolume.lastVolume.Create();
                buffer.SetGlobalVector(ShaderIDs._TemporalWeight, Vector4.zero);
            }
            else
            {
                const float clipBounce = 30f * 100f / 32f;
                buffer.SetGlobalVector(ShaderIDs._TemporalWeight, new Vector4(darkerWeight, brighterWeight));
            }

            jobHandle.Complete();
            if (fogCount > 0)
            {
                cbdr.allFogVolumeBuffer.SetData(resultVolume, 0, 0, fogCount);
            }
            buffer.SetGlobalFloat(ShaderIDs._LinearFogDensity, linearFogDensity);
            buffer.SetGlobalVector(ShaderIDs._VolumetricLightVar, new Vector4(cam.cam.nearClipPlane, availableDistance - cam.cam.nearClipPlane, availableDistance, indirectIntensity));
            buffer.SetGlobalVector(ShaderIDs._Screen_TexelSize, new Vector4(1f / cam.cam.pixelWidth, 1f / cam.cam.pixelHeight, cam.cam.pixelWidth, cam.cam.pixelHeight));
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllFogVolume, cbdr.allFogVolumeBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllPointLight, cbdr.allPointLightBuffer);
            buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AllSpotLight, cbdr.allSpotLightBuffer);
            buffer.SetComputeIntParam(scatter, ShaderIDs._FogVolumeCount, fogCount);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._VolumeTex, volumeTex);
            buffer.SetComputeTextureParam(scatter, scatterPass, ShaderIDs._VolumeTex, volumeTex);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._LastVolume, historyVolume.lastVolume);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._DirShadowMap, cbdr.dirLightShadowmap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._SpotMapArray, cbdr.spotArrayMap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._CubeShadowMapArray, cbdr.cubeArrayMap);
            buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._IESAtlas, lightingData.iesAtlas);
            if (useIBLIndirect)
            {
                NativeArray<float3> cameraNormals = new NativeArray<float3>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                cameraNormals[0] = cam.cam.transform.forward;
                cameraNormals[1] = cam.cam.transform.right;
                cameraNormals[2] = cam.cam.transform.up;
                cameraNormalBuffer.SetData(cameraNormals);
                cameraNormals.Dispose();
                buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._CameraNormals, cameraNormalBuffer);
                reflectData.SetComputeShaderIBLBuffer(scatter, pass, buffer, blackCB);
            }
            if (useNoiseEvents)
            {
                noiseEvents.Update(Time.deltaTime * 0.3f, buffer);
                buffer.SetComputeFloatParam(scatter, ShaderIDs._TimeVar, noiseEvents.value);
                buffer.SetComputeTextureParam(scatter, pass, ShaderIDs._VolumetricNoise, noiseEvents.noiseTexture);
            }
            if (useEmissionGeometry)
            {
                ComputeBuffer areaLightBuffer; int areaLightCount;
                geometry.GetAreaLightBuffer(out areaLightBuffer, out areaLightCount);
                buffer.SetComputeBufferParam(scatter, pass, ShaderIDs._AreaLightBuffer, areaLightBuffer);
                buffer.SetComputeIntParam(scatter, ShaderIDs._AreaLightCount, areaLightCount);
            }
            cbdr.dirLightShadowmap = null;
            buffer.SetComputeIntParam(scatter, ShaderIDs._LightFlag, (int)cbdr.lightFlag);
            int3 dispatchCount = int3(downSampledSize.x / 2, downSampledSize.y / 2, downSampledSize.z / marchStep);
            buffer.DispatchCompute(scatter, pass, dispatchCount.x, dispatchCount.y, dispatchCount.z);
            buffer.CopyTexture(volumeTex, historyVolume.lastVolume);
            buffer.DispatchCompute(scatter, scatterPass, downSampledSize.x / 32, downSampledSize.y / 2, 1);
            cbdr.lightFlag = 0;
            buffer.BlitSRT(cam.targets.renderTargetIdentifier, lightingMat, 0);
        }

        protected override void OnEnable()
        {
            RenderPipeline.AfterFrameBuffer.EnableShaderKeyword("ENABLE_VOLUMETRIC");
        }

        protected override void OnDisable()
        {
            RenderPipeline.AfterFrameBuffer.DisableShaderKeyword("ENABLE_VOLUMETRIC");
        }

        protected override void Dispose()
        {
            if (useEmissionGeometry)
                geometry.Dispose();
            if (useNoiseEvents)
                noiseEvents.Dispose();
            DestroyImmediate(lightingMat);
            cameraNormalBuffer.Dispose();
            DestroyImmediate(blackCB);
            DestroyImmediate(volumeTex);
        }
        [Unity.Burst.BurstCompile]
        public unsafe struct FogVolumeCalculate : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public FogVolume* allVolume;
            [NativeDisableUnsafePtrRestriction]
            public int* fogVolumeCount;
            [NativeDisableUnsafePtrRestriction]
            public float4* frustumPlanes;
            [NativeDisableUnsafePtrRestriction]
            public FogVolumeComponent.FogVolumeContainer* fogVolume;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool BoxUnderPlane(ref float4 plane, ref FogVolume fog, int i)
            {
                float3 absNormal = abs(normalize(mul(plane.xyz, fog.localToWorld)));
                return dot(fog.position, plane.xyz) - dot(absNormal, fog.extent) < -plane.w;
            }
            public void Execute(int index)
            {
                ref FogVolume vol = ref fogVolume[index].volume;
                for (int i = 0; i < 6; ++i)
                {
                    if (!BoxUnderPlane(ref frustumPlanes[i], ref vol, i))
                        return;
                }
                int last = Interlocked.Increment(ref *fogVolumeCount) - 1;
                allVolume[last] = vol;
            }
        }

    }
    public class HistoryVolumetric : IPerCameraData
    {
        public RenderTexture lastVolume = null;
        public override void DisposeProperty()
        {
            if (lastVolume != null)
            {
                lastVolume.Release();
                lastVolume = null;
            }
        }
    }
}
