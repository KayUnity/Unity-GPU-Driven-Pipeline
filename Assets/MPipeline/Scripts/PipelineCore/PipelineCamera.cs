using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
namespace MPipeline
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public unsafe sealed class PipelineCamera : MonoBehaviour
    {
        public void ResetMatrix()
        {
            Camera cam = GetComponent<Camera>();
            cam.orthographic = !cam.orthographic;
            cam.orthographic = !cam.orthographic;
            cam.ResetCullingMatrix();
            cam.ResetProjectionMatrix();
            cam.ResetStereoProjectionMatrices();
            cam.ResetStereoViewMatrices();
            cam.ResetWorldToCameraMatrix();
        }
        public struct IntEqual : IFunction<int, int, bool>
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool Run(ref int a, ref int b)
            {
                return a == b;
            }
        }
        private struct PtrEqual : IFunction<ulong, ulong, bool>
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool Run(ref ulong a, ref ulong b)
            {
                return a == b;
            }
        }
        [System.NonSerialized]
        public Camera cam;
        [System.NonSerialized]
        public RenderTargets targets;
        public UnityEngine.Rendering.PostProcessing.PostProcessProfile postProfile;
        public PipelineResources.CameraRenderingPath renderingPath = PipelineResources.CameraRenderingPath.GPUDeferred;
        public Dictionary<Type, IPerCameraData> allDatas = new Dictionary<Type, IPerCameraData>(17);
        public bool inverseRender = false;
        public RenderTargetIdentifier cameraTarget = BuiltinRenderTextureType.CameraTarget;
        public static NativeDictionary<int, UIntPtr, IntEqual> allCamera;
        private Dictionary<Type, CommandBuffer> commandBuffers = new Dictionary<Type, CommandBuffer>(10);
        [HideInInspector]
        public float[] layerCullDistance = new float[32];

        public void EnableThis(PipelineResources res)
        {
            if (!targets.initialized)
            {
                targets = RenderTargets.Init();
                ResetMatrix();
            }
        }

        public CommandBuffer GetCommand<T>() where T : PipelineEvent
        {
            CommandBuffer bf;
            if (!commandBuffers.TryGetValue(typeof(T), out bf))
            {
                bf = new CommandBuffer();
                commandBuffers.Add(typeof(T), bf);
            }
            return bf;
        }

        private void OnEnable()
        {
            if (!allCamera.isCreated)
            {
                allCamera = new NativeDictionary<int, UIntPtr, IntEqual>(17, Allocator.Persistent, new IntEqual());
            }
            allCamera.Add(gameObject.GetInstanceID(), new UIntPtr(MUnsafeUtility.GetManagedPtr(this)));
            GetComponent<Camera>().layerCullDistances = layerCullDistance;
        }

        private void OnDisable()
        {
            allCamera.Remove(gameObject.GetInstanceID());
            if (allCamera.Length <= 0)
            {
                allCamera.Dispose();
            }
        }
        private void OnDestroy()
        {
            foreach (var i in allDatas.Values)
                i.DisposeProperty();
            allDatas.Clear();
            cam = null;
            foreach (var i in commandBuffers.Values)
            {
                i.Dispose();
            }
            commandBuffers = null;
        }
    }
}
