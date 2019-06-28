using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;
using System.Reflection;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    public unsafe sealed class RenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        #region STATIC_AREA

        public static RenderPipeline current { get; private set; }
        public static PipelineCommandData data;
        #endregion
        private struct PtrEqual : IFunction<UIntPtr, UIntPtr, bool>
        {
            public bool Run(ref UIntPtr a, ref UIntPtr b)
            {
                return a == b;
            }
        }
        public PipelineResources resources;
        private static CommandBuffer m_afterFrameBuffer;
        private static CommandBuffer m_beforeFrameBuffer;
        private static bool useAfterFrameBuffer = false;
        private static bool useBeforeFrameBuffer = false;
        public static CommandBuffer AfterFrameBuffer
        {
            get
            {
                if (m_afterFrameBuffer == null) m_afterFrameBuffer = new CommandBuffer();
                useAfterFrameBuffer = true;
                return m_afterFrameBuffer;
            }
        }
        public static CommandBuffer BeforeFrameBuffer
        {
            get
            {
                if (m_beforeFrameBuffer == null) m_beforeFrameBuffer = new CommandBuffer();
                useBeforeFrameBuffer = true;
                return m_beforeFrameBuffer;
            }
        }
        public static bool renderingEditor { get; private set; }
        public static PipelineResources.CameraRenderingPath currentPath { get; private set; }
        private static NativeDictionary<UIntPtr, int, PtrEqual> eventsGuideBook;
        private static NativeList<int> waitReleaseRT;
        private static List<PipelineCamera> preFrameRenderCamera = new List<PipelineCamera>(10);
        public static void AddPreRenderCamera(PipelineCamera tar)
        {
            preFrameRenderCamera.Add(tar);
        }
        public static void ReleaseRTAfterFrame(int targetRT)
        {
            waitReleaseRT.Add(targetRT);
        }
#if UNITY_EDITOR
        private struct EditorBakeCommand
        {
            public NativeList<float4x4> worldToCamera;
            public NativeList<float4x4> projection;
            public PipelineCamera pipelineCamera;
            public RenderTexture texArray;
            public CommandBuffer buffer;
        }
        private static List<EditorBakeCommand> bakeList = new List<EditorBakeCommand>();
        public static void AddRenderingMissionInEditor(
            NativeList<float4x4> worldToCameras,
            NativeList<float4x4> projections,
            PipelineCamera targetCameras,
            RenderTexture texArray,
            CommandBuffer buffer)
        {
            bakeList.Add(new EditorBakeCommand
            {
                worldToCamera = worldToCameras,
                projection = projections,
                texArray = texArray,
                pipelineCamera = targetCameras,
                buffer = buffer
            });
        }
#else
        public static void AddRenderingMissionInEditor(NativeList<float4x4> worldToCameras, NativeList<float4x4> projections, PipelineCamera targetCameras, RenderTexture texArray, RenderTexture tempTexture, CommandBuffer buffer)
        {
        //Shouldn't do anything in runtime
        }
#endif
        public static T GetEvent<T>() where T : PipelineEvent
        {
            Type type = typeof(T);
            int value;
            if (eventsGuideBook.Get(new UIntPtr(MUnsafeUtility.GetManagedPtr(type)), out value))
            {
                return current.resources.availiableEvents[value] as T;
            }
            return null;
        }

        public static PipelineEvent GetEvent(Type type)
        {
            int value;
            if (eventsGuideBook.Get(new UIntPtr(MUnsafeUtility.GetManagedPtr(type)), out value))
            {
                return current.resources.availiableEvents[value];
            }
            return null;
        }
        private ComputeBuffer motionVectorMatricesBuffer;
        private NativeArray<float3x4> allMatrices;
        public RenderPipeline(PipelineResources resources)
        {
            eventsGuideBook = new NativeDictionary<UIntPtr, int, PtrEqual>(resources.availiableEvents.Length, Allocator.Persistent, new PtrEqual());
            resources.SetRenderingPath();
            var allEvents = resources.allEvents;
            GraphicsUtility.UpdatePlatform();
            MLight.ClearLightDict();
            this.resources = resources;
            current = this;
            data.buffer = new CommandBuffer();
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].InitDependEventsList();
            }
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                eventsGuideBook.Add(new UIntPtr(MUnsafeUtility.GetManagedPtr(resources.availiableEvents[i].GetType())), i);
                resources.availiableEvents[i].Prepare();
            }
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].InitEvent(resources);
            }
            waitReleaseRT = new NativeList<int>(20, Allocator.Persistent);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (m_afterFrameBuffer != null)
            {
                m_afterFrameBuffer.Dispose();
                m_afterFrameBuffer = null;
            }
            if (m_beforeFrameBuffer != null)
            {
                m_beforeFrameBuffer.Dispose();
                m_beforeFrameBuffer = null;
            }
            if (current == this)
            {
                current = null;
            }
            try
            {
                eventsGuideBook.Dispose();
                waitReleaseRT.Dispose();
            }
            catch { }
            data.buffer.Dispose();
            var allEvents = resources.allEvents;
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].DisposeEvent();
            }
            for (int i = 0; i < resources.availiableEvents.Length; ++i)
            {
                resources.availiableEvents[i].DisposeDependEventsList();
            }
            if (PipelineCamera.allCamera.isCreated)
            {
                foreach (var i in PipelineCamera.allCamera)
                {
                    PipelineCamera cam = MUnsafeUtility.GetObject<PipelineCamera>(i.value.ToPointer());
                    var values = cam.allDatas.Values;
                    foreach (var j in values)
                    {
                        j.DisposeProperty();
                    }
                    cam.allDatas.Clear();
                }
            }
            if (motionVectorMatricesBuffer != null) motionVectorMatricesBuffer.Dispose();
            allMatrices.Dispose();
        }
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            bool* propertyCheckedFlags = stackalloc bool[resources.allEvents.Length];
            bool needSubmit = false;
            UnsafeUtility.MemClear(propertyCheckedFlags, resources.allEvents.Length);
            GraphicsSettings.useScriptableRenderPipelineBatching = resources.useSRPBatcher;
            SceneController.SetState();
            data.context = renderContext;
            data.resources = resources;
            if (!allMatrices.IsCreated) allMatrices = new NativeArray<float3x4>(50, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (motionVectorMatricesBuffer == null || !motionVectorMatricesBuffer.IsValid()) motionVectorMatricesBuffer = new ComputeBuffer(50, sizeof(float3x4));
            else if (motionVectorMatricesBuffer.count < MotionVectorDrawer.AllDrawers.Count)
            {
                int len = (int)(motionVectorMatricesBuffer.count * 1.2f);
                motionVectorMatricesBuffer.Dispose();
                motionVectorMatricesBuffer = new ComputeBuffer(Mathf.Max(len, MotionVectorDrawer.AllDrawers.Count), sizeof(float3x4));
                var newArr = new NativeArray<float3x4>(motionVectorMatricesBuffer.count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                UnsafeUtility.MemCpy(newArr.GetUnsafePtr(), allMatrices.GetUnsafePtr(), allMatrices.Length * sizeof(float3x4));
                allMatrices.Dispose();
                allMatrices = newArr;
            }
            motionVectorMatricesBuffer.SetData(allMatrices, 0, 0, MotionVectorDrawer.AllDrawers.Count);
            data.buffer.SetGlobalBuffer(ShaderIDs._LastFrameModel, motionVectorMatricesBuffer);
#if UNITY_EDITOR
            int tempID = Shader.PropertyToID("_TempRT");
            foreach (var pair in bakeList)
            {
                PipelineCamera pipelineCam = pair.pipelineCamera;
                for (int i = 0; i < pair.worldToCamera.Length; ++i)
                {
                    pipelineCam.cam.worldToCameraMatrix = pair.worldToCamera[i];
                    pipelineCam.cam.projectionMatrix = pair.projection[i];
                    pipelineCam.cameraTarget = tempID;
                    data.buffer.GetTemporaryRT(tempID, pair.texArray.width, pair.texArray.height, pair.texArray.depth, FilterMode.Point, pair.texArray.format, RenderTextureReadWrite.Linear);
                    Render(pipelineCam, ref renderContext, pipelineCam.cam, propertyCheckedFlags);
                    data.buffer.CopyTexture(tempID, 0, 0, pair.texArray, i, 0);
                    data.buffer.ReleaseTemporaryRT(tempID);
                    data.ExecuteCommandBuffer();
                    renderContext.Submit();
                    needSubmit = false;
                }
                pair.worldToCamera.Dispose();
                pair.projection.Dispose();
                renderContext.ExecuteCommandBuffer(pair.buffer);
                pair.buffer.Clear();
                renderContext.Submit();
                needSubmit = false;
            }
            bakeList.Clear();
#endif
            foreach (var cam in preFrameRenderCamera)
            {
                Render(cam, ref renderContext, cam.cam, propertyCheckedFlags);
                data.ExecuteCommandBuffer();
                renderContext.Submit();
                needSubmit = false;
            }
            preFrameRenderCamera.Clear();
            if (CustomDrawRequest.AllEvents.Count > 0 || JobProcessEvent.allEvents.Count > 0)
            {
                foreach (var i in CustomDrawRequest.AllEvents)
                {
                    i.PrepareJob(resources);
                }
                foreach (var i in JobProcessEvent.allEvents)
                {
                    i.PrepareJob();
                }
                JobHandle.ScheduleBatchedJobs();
                foreach (var i in CustomDrawRequest.AllEvents)
                {
                    i.FinishJob();
                }
                foreach (var i in JobProcessEvent.allEvents)
                {
                    i.FinishJob();
                }
            }
            if (useBeforeFrameBuffer)
            {
                renderContext.ExecuteCommandBuffer(m_beforeFrameBuffer);
                m_beforeFrameBuffer.Clear();
                needSubmit = true;
                useBeforeFrameBuffer = false;
            }
            if (PipelineCamera.allCamera.isCreated)
            {
                foreach (var cam in cameras)
                {
                    PipelineCamera pipelineCam;
                    UIntPtr pipelineCamPtr;
                    if (!PipelineCamera.allCamera.Get(cam.gameObject.GetInstanceID(), out pipelineCamPtr))
                    {
#if UNITY_EDITOR
                        renderingEditor = true;
                        var pos = cam.transform.eulerAngles;
                        pos.z = 0;
                        cam.transform.eulerAngles = pos;
                        if (!PipelineCamera.allCamera.Get(Camera.main.gameObject.GetInstanceID(), out pipelineCamPtr))
                            continue;
#else
                    continue;
#endif
                    }
                    else
                    {
                        renderingEditor = false;
                    }
                    pipelineCam = MUnsafeUtility.GetObject<PipelineCamera>(pipelineCamPtr.ToPointer());
                    Render(pipelineCam, ref renderContext, cam, propertyCheckedFlags);
                    data.ExecuteCommandBuffer();
#if UNITY_EDITOR
                    if (renderingEditor)
                        renderContext.DrawGizmos(cam, GizmoSubset.PostImageEffects);
#endif
                    renderContext.Submit();
                    needSubmit = false;
                }
            }
            if (useAfterFrameBuffer)
            {
                renderContext.ExecuteCommandBuffer(m_afterFrameBuffer);
                m_afterFrameBuffer.Clear();
                needSubmit = true;
                useAfterFrameBuffer = false;
            }
            if (needSubmit)
            {
                renderContext.Submit();
            }
            float3x4* lastMatPtrs = allMatrices.Ptr();
            var lst = MotionVectorDrawer.AllDrawers;
            for(int i = 0; i < lst.Count; ++i)
            {
                Matrix4x4 localToWorld = lst[i].transform.localToWorldMatrix;
                lastMatPtrs[i] = new float3x4((Vector3)localToWorld.GetColumn(0), (Vector3)localToWorld.GetColumn(1),(Vector3)localToWorld.GetColumn(2),(Vector3)localToWorld.GetColumn(3));
            }
        }

        private void Render(PipelineCamera pipelineCam, ref ScriptableRenderContext context, Camera cam, bool* pipelineChecked)
        {
            PipelineResources.CameraRenderingPath path = pipelineCam.renderingPath;
            currentPath = path;
            pipelineCam.cam = cam;
            pipelineCam.EnableThis(resources);
            context.SetupCameraProperties(cam);
            //Set Global Data
            var allEvents = resources.allEvents;
            var collect = allEvents[(int)path];
#if UNITY_EDITOR
            //Need only check for Unity Editor's bug!
            if (!pipelineChecked[(int)path])
            {
                pipelineChecked[(int)path] = true;
                foreach (var e in collect)
                {
                    if (!e.CheckProperty())
                    {
                        e.CheckInit(resources);
                    }
                }
            }
#endif
            foreach (var e in collect)
            {
                if (e.Enabled)
                {
                    e.PreRenderFrame(pipelineCam, ref data);
                }
            }
            JobHandle.ScheduleBatchedJobs();
            foreach (var e in collect)
            {
                if (e.Enabled)
                {
                    e.FrameUpdate(pipelineCam, ref data);
                }
            }
            foreach (var i in waitReleaseRT)
            {
                data.buffer.ReleaseTemporaryRT(i);
            }
            waitReleaseRT.Clear();
        }
    }
}