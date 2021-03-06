﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using System;
using static Unity.Mathematics.math;
using System.Runtime.CompilerServices;
namespace MPipeline
{
    public struct RenderClusterOptions
    {
        public Vector4[] frustumPlanes;
        public CommandBuffer command;
        public ComputeShader cullingShader;
        public ComputeShader terrainCompute;
    }
    [Serializable]
    public unsafe static class SceneController
    {
        public struct DrawSceneSettings
        {
            public RenderClusterOptions clusterOptions;
            public Camera targetCam;
            public RenderQueueRange renderRange;
            public string passName;
            public CullingOptions flag;
            public PerObjectData configure;
            public Material clusterMat;
        }
        public const int overrideShadowmapPass = 0;
        public const int overrideDepthPrePass = 1;
        public static bool gpurpEnabled { get; private set; }
        private static bool singletonReady = false;
        private static PipelineResources resources;
        public static PipelineBaseBuffer baseBuffer { get; private set; }
        private static ClusterMatResources clusterResources;
        private static List<SceneStreaming> allScenes;

        public static NativeList<ulong> addList;
        private static Dictionary<int, ComputeBuffer> allTempBuffers = new Dictionary<int, ComputeBuffer>(11);
        public static void SetState()
        {
            if (singletonReady && baseBuffer.clusterCount > 0)
            {
                gpurpEnabled = true;
            }
            else
            {
                gpurpEnabled = false;
            }
        }
        public static ComputeBuffer GetTempPropertyBuffer(int length, int stride)
        {
            ComputeBuffer target;
            if (allTempBuffers.TryGetValue(stride, out target))
            {
                if (target.count < length)
                {
                    target.Dispose();
                    target = new ComputeBuffer(length, stride);
                    allTempBuffers[stride] = target;
                }
                return target;
            }
            else
            {
                target = new ComputeBuffer(length, stride);
                allTempBuffers[stride] = target;
                return target;
            }
        }
        public static void Awake(PipelineResources resources, ClusterMatResources mapResources)
        {
            singletonReady = true;
            SceneController.resources = resources;
            addList = new NativeList<ulong>(10, Allocator.Persistent);
            baseBuffer = new PipelineBaseBuffer();
            clusterResources = mapResources;
            int clusterCount = 0;
            if (clusterResources)
            {
                allScenes = new List<SceneStreaming>(clusterResources.clusterProperties.Count);

                for (int i = 0; i < clusterResources.clusterProperties.Count; ++i)
                {
                    var cur = clusterResources.clusterProperties[i];
                    clusterCount += cur.clusterCount;
                    allScenes.Add(new SceneStreaming(cur, i));
                }
            }
            PipelineFunctions.InitBaseBuffer(baseBuffer, clusterCount);

        }

        public static void Dispose()
        {
            singletonReady = false;
            PipelineFunctions.Dispose(baseBuffer);

            addList.Dispose();
            var values = allTempBuffers.Values;
            foreach (var i in values)
            {
                i.Dispose();
            }
        }
        //Press number load scene

        public static void Update()
        {
            int value;
            if (int.TryParse(Input.inputString, out value))
            {
                TransformScene((uint)value);
            }
        }

        public static void TransformScene(uint value)
        {
            if (value < allScenes.Count)
            {
                SceneStreaming str = allScenes[(int)value];

                if (str.state == SceneStreaming.State.Loaded)
                    str.DeleteSync();
                //    behavior.StartCoroutine(str.Delete());
                else if (str.state == SceneStreaming.State.Unloaded)
                    str.GenerateSync();
                //behavior.StartCoroutine(str.Generate()); 

            }
        }
        private static bool GetBaseBuffer(out PipelineBaseBuffer result)
        {
            result = baseBuffer;
            return result.clusterCount > 0;
        }

        public static void RenderScene(ref PipelineCommandData data, ref FilteringSettings filterSettings, ref DrawingSettings drawSettings, ref CullingResults cullResults)
        {
            data.ExecuteCommandBuffer();
            data.context.DrawRenderers(cullResults, ref drawSettings, ref filterSettings);
        }
        public static void DrawSpotLight(MLight mlight, int mask, ComputeShader cullingShader, ref PipelineCommandData data, Camera currentCam, ref SpotLight spotLights, ref RenderSpotShadowCommand spotcommand, bool inverseRender, Material opaqueOverride, NativeList_Int culledResult)
        {
            if (mlight.ShadowIndex < 0) return;
            CommandBuffer buffer = data.buffer;
            ref SpotLightMatrix spotLightMatrix = ref spotcommand.shadowMatrices[spotLights.shadowIndex];
            buffer.SetInvertCulling(true);
            buffer.DisableShaderKeyword("POINT_LIGHT_SHADOW");
            currentCam.orthographic = false;
            currentCam.fieldOfView = spotLights.angle;
            currentCam.nearClipPlane = spotLights.nearClip;
            currentCam.farClipPlane = spotLights.lightCone.height;
            currentCam.aspect = 1;
            currentCam.cullingMatrix = spotLightMatrix.projectionMatrix * currentCam.worldToCameraMatrix;
            buffer.SetRenderTarget(spotcommand.renderTarget, 0, CubemapFace.Unknown, mlight.ShadowIndex);
            buffer.ClearRenderTarget(true, false, new Color(float.PositiveInfinity, 1, 1, 1));
            buffer.SetGlobalMatrix(ShaderIDs._ShadowMapVP, GL.GetGPUProjectionMatrix(spotLightMatrix.projectionMatrix, true) * spotLightMatrix.worldToCamera);
            ScriptableCullingParameters cullParams;
            if (!currentCam.TryGetCullingParameters(out cullParams))
            {
                buffer.SetInvertCulling(inverseRender);
                return;
            }
            if (gpurpEnabled)
            {
                float4* frustumPlanes = stackalloc float4[6];
                for (int i = 0; i < 6; ++i)
                {
                    Plane p = cullParams.GetCullingPlane(i);
                    frustumPlanes[i] = new float4(-p.normal, -p.distance);
                }
                buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                PipelineFunctions.SetBaseBuffer(baseBuffer, cullingShader, frustumPlanes, buffer);
                PipelineFunctions.RunCullDispatching(baseBuffer, cullingShader, buffer);
                PipelineFunctions.RenderProceduralCommand(baseBuffer, spotcommand.clusterShadowMaterial, buffer);
            }
            if(culledResult.isCreated)
            foreach (var i in culledResult)
            {
                CustomDrawRequest.allEvents[i].DrawShadow(buffer);
            }
            data.ExecuteCommandBuffer();
            FilteringSettings renderSettings = new FilteringSettings()
            {
                renderQueueRange = new RenderQueueRange(2000, 2450),
                layerMask = mask,
                renderingLayerMask = 1
            };
            DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("Shadow"), new SortingSettings { criteria = SortingCriteria.QuantizedFrontToBack })
            {
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
                overrideMaterial = opaqueOverride,
                overrideMaterialPassIndex = overrideShadowmapPass
            };
            cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
            CullingResults results = data.context.Cull(ref cullParams);
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            renderSettings.renderQueueRange = new RenderQueueRange(2451, 2499);
            dsettings.overrideMaterial = null;
            dsettings.overrideMaterialPassIndex = 0;
            data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            buffer.SetInvertCulling(inverseRender);
        }

        public static void DrawDirectionalShadow(PipelineCamera cam, ref StaticFit staticFit, ref PipelineCommandData data, ref RenderClusterOptions opts, float* clipDistances, OrthoCam* camCoords, Matrix4x4[] shadowVPs, Material opaqueOverride)
        {
            SunLight sunLight = SunLight.current;
            opts.command.DisableShaderKeyword("POINT_LIGHT_SHADOW");
            opts.command.SetInvertCulling(true);
            Camera currentCam = cam.cam;
            Vector4 bias = sunLight.bias / currentCam.farClipPlane;
            opts.command.SetGlobalVector(ShaderIDs._ShadowOffset, bias);
            for (int pass = 0; pass < SunLight.CASCADELEVELCOUNT; ++pass)
            {
                float4* vec = (float4*)opts.frustumPlanes.Ptr();
                ref OrthoCam orthoCam = ref camCoords[pass];
                SunLight.shadowCam.cullingMatrix = shadowVPs[pass];
                SunLight.shadowCam.orthographicSize = orthoCam.size;
                SunLight.shadowCam.nearClipPlane = orthoCam.nearClipPlane;
                SunLight.shadowCam.farClipPlane = orthoCam.farClipPlane;
                Transform tr = SunLight.shadowCam.transform;
                tr.position = orthoCam.position;
                tr.up = orthoCam.up;
                tr.right = orthoCam.right;
                tr.forward = orthoCam.forward;
                ScriptableCullingParameters cullParams;
                if (!SunLight.shadowCam.TryGetCullingParameters(out cullParams))
                {
                    opts.command.SetInvertCulling(cam.inverseRender);
                    return;
                }
                for (int i = 0; i < 6; ++i)
                {
                    Plane p = cullParams.GetCullingPlane(i);
                    vec[i] = -float4(p.normal, p.distance);
                }
                Matrix4x4 vpMatrix;
                PipelineFunctions.UpdateCascadeState(sunLight, ref orthoCam.projectionMatrix, ref orthoCam.worldToCameraMatrix, opts.command, pass, out vpMatrix);
                NativeList_Int culledResult = SunLight.customCullResults[pass];
                foreach (var i in culledResult)
                {
                    CustomDrawRequest.allEvents[i].DrawShadow(opts.command);
                }
                if (gpurpEnabled)
                {
                    opts.command.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                    opts.command.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                    PipelineFunctions.SetBaseBuffer(baseBuffer, opts.cullingShader, opts.frustumPlanes, opts.command);
                    PipelineFunctions.RunCullDispatching(baseBuffer, opts.cullingShader, opts.command);
                    opts.command.DrawProceduralIndirect(Matrix4x4.identity, sunLight.shadowDepthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
                }
                if(MTerrain.current)
                {
                    MTerrain.current.DrawTerrain(opts.command, 1, vec);
                }
                data.ExecuteCommandBuffer();
                FilteringSettings renderSettings = new FilteringSettings()
                {
                    renderQueueRange = new RenderQueueRange(2000, 2450),
                    layerMask = sunLight.shadowMask,
                    renderingLayerMask = 1
                };
                SortingSettings sorting = new SortingSettings(SunLight.shadowCam);
                sorting.criteria = SortingCriteria.QuantizedFrontToBack;
                DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("Shadow"), sorting)
                {
                    perObjectData = UnityEngine.Rendering.PerObjectData.None,
                    overrideMaterial = opaqueOverride,
                    overrideMaterialPassIndex = overrideShadowmapPass
                };
                cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;
                CullingResults results = data.context.Cull(ref cullParams);
                data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
                renderSettings.renderQueueRange = new RenderQueueRange(2451, 2499);
                dsettings.overrideMaterial = null;
                dsettings.overrideMaterialPassIndex = 0;
                data.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            }
            opts.command.SetInvertCulling(cam.inverseRender);
        }

        public static void DrawPointLight(MLight lit,
            int mask,
            ref PointLightStruct light,
            Material depthMaterial,
            ComputeShader cullingShader,
            ref PipelineCommandData data,
            ref CubemapViewProjMatrix vpMatrices,
            RenderTexture renderTarget,
            bool inverseRender,
            Material opaqueOverride)
        {
            if (lit.ShadowIndex < 0) return;
            NativeList_Int culledResult = vpMatrices.customCulledResult;
            CommandBuffer cb = data.buffer;
            cb.SetGlobalVector(ShaderIDs._LightPos, light.sphere);
            cb.SetInvertCulling(true);
            cb.EnableShaderKeyword("POINT_LIGHT_SHADOW");
            FilteringSettings renderSettings = new FilteringSettings()
            {
                renderQueueRange = new RenderQueueRange(2451, 2499),
                layerMask = mask,
                renderingLayerMask = 1
            };
            FilteringSettings opaqueRenderSettings = new FilteringSettings()
            {
                renderQueueRange = new RenderQueueRange(2000, 2450),
                layerMask = mask,
                renderingLayerMask = 1
            };
            DrawingSettings dsettings = new DrawingSettings(new ShaderTagId("Shadow"), new SortingSettings { criteria = SortingCriteria.None })
            {
                enableDynamicBatching = false,
                enableInstancing = false,
                perObjectData = UnityEngine.Rendering.PerObjectData.None,
            };
            DrawingSettings opaqueRender = dsettings;
            opaqueRender.overrideMaterial = opaqueOverride;
            opaqueRender.overrideMaterialPassIndex = overrideShadowmapPass;
            int depthSlice = lit.ShadowIndex * 6;
            float size = light.sphere.w;
            lit.shadowCam.orthographic = true;
            lit.shadowCam.nearClipPlane = -size;
            lit.shadowCam.farClipPlane = size;
            lit.shadowCam.aspect = 1;
            lit.shadowCam.orthographicSize = size;
            lit.shadowCam.cullingMatrix = Matrix4x4.Ortho(-size, size, -size, size, -size, size) * lit.shadowCam.worldToCameraMatrix;
            ScriptableCullingParameters cullParams;
            if (!lit.shadowCam.TryGetCullingParameters(out cullParams))
            {
                cb.SetInvertCulling(inverseRender);
                return;
            }
            cullParams.cullingOptions = CullingOptions.ForceEvenIfCameraIsNotActive;

            CullingResults results = data.context.Cull(ref cullParams);
            if (gpurpEnabled)
            {
                PipelineFunctions.SetBaseBuffer(baseBuffer, cullingShader, vpMatrices.frustumPlanes, cb);
                PipelineFunctions.RunCullDispatching(baseBuffer, cullingShader, cb);

            }
            void DrawFace(int renderSlice, ref Matrix4x4 shadowmapVP, ref PipelineCommandData commandData)
            {
                cb.SetRenderTarget(renderTarget, 0, CubemapFace.Unknown, renderSlice);
                cb.ClearRenderTarget(true, true, new Color(float.PositiveInfinity, 1, 1, 1));
                cb.SetGlobalMatrix(ShaderIDs._ShadowMapVP, shadowmapVP);
                if (gpurpEnabled)
                {
                    cb.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
                    cb.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
                    cb.DrawProceduralIndirect(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer);
                }
                if(culledResult.isCreated)
                foreach (var i in culledResult)
                {
                    CustomDrawRequest.allEvents[i].DrawShadow(cb);
                }
                commandData.ExecuteCommandBuffer();
                commandData.context.DrawRenderers(results, ref opaqueRender, ref opaqueRenderSettings);
                commandData.context.DrawRenderers(results, ref dsettings, ref renderSettings);
            }
            //X
            DrawFace(depthSlice + 1, ref vpMatrices.rightProjView, ref data);
            //-X
            DrawFace(depthSlice, ref vpMatrices.leftProjView, ref data);
            //Y
            DrawFace(depthSlice + 3, ref vpMatrices.upProjView, ref data);
            //-Y
            DrawFace(depthSlice + 2, ref vpMatrices.downProjView, ref data);
            //Z
            DrawFace(depthSlice + 5, ref vpMatrices.forwardProjView, ref data);
            //-Z
            DrawFace(depthSlice + 4, ref vpMatrices.backProjView, ref data);
            cb.SetInvertCulling(inverseRender);
        }
        public static void DrawCluster_LastFrameDepthHiZ(ref RenderClusterOptions options, HizOcclusionData hizOpts, Material targetMat, PipelineCamera pipeCam)
        {
            if (!gpurpEnabled) return;
            ref RenderTargets rendTargets = ref pipeCam.targets;
            Camera cam = pipeCam.cam;
            CommandBuffer buffer = options.command;
            ComputeShader gpuFrustumShader = options.cullingShader;
            buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            buffer.SetComputeBufferParam(gpuFrustumShader, PipelineBaseBuffer.ClearCluster_Kernel, ShaderIDs.instanceCountBuffer, baseBuffer.instanceCountBuffer);
            buffer.DispatchCompute(gpuFrustumShader, PipelineBaseBuffer.ClearCluster_Kernel, 1, 1, 1);
            PipelineFunctions.UpdateOcclusionBuffer(
baseBuffer, gpuFrustumShader,
buffer,
hizOpts,
options.frustumPlanes);
            //First Draw
            buffer.DrawProceduralIndirect(Matrix4x4.identity, targetMat, 0, MeshTopology.Triangles, baseBuffer.instanceCountBuffer, 0);
        }

        public static void DrawCluster_RecheckHiz(ref RenderClusterOptions options, ref HizDepth hizDepth, HizOcclusionData hizOpts, Material targetMat, Material linearLODMaterial, PipelineCamera pipeCam)
        {
            if (!gpurpEnabled) return;
            ref RenderTargets rendTargets = ref pipeCam.targets;
            Camera cam = pipeCam.cam;
            CommandBuffer buffer = options.command;
            ComputeShader gpuFrustumShader = options.cullingShader;

            buffer.BlitSRT(hizOpts.historyDepth, linearLODMaterial, 0);
            hizDepth.GetMipMap(hizOpts.historyDepth, buffer);
            //double check
            PipelineFunctions.ClearOcclusionData(baseBuffer, buffer, gpuFrustumShader);
            hizOpts.lastFrameCameraUp = cam.transform.up;
            PipelineFunctions.OcclusionRecheck(baseBuffer, gpuFrustumShader, buffer, hizOpts);
            buffer.SetGlobalBuffer(ShaderIDs.resultBuffer, baseBuffer.resultBuffer);
            buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, baseBuffer.verticesBuffer);
            //double draw
            buffer.SetRenderTarget(colors: rendTargets.gbufferIdentifier, depth: ShaderIDs._DepthBufferTexture);
            buffer.DrawProceduralIndirect(Matrix4x4.identity, targetMat, 0, MeshTopology.Triangles, baseBuffer.reCheckCount, 0);
            buffer.BlitSRT(hizOpts.historyDepth, linearLODMaterial, 0);
            hizDepth.GetMipMap(hizOpts.historyDepth, buffer);
        }
    }
}