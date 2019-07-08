using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Mathematics;
namespace MPipeline
{
    public abstract unsafe class CustomDrawRequest : MonoBehaviour
    {
        public struct ObjectContainer
        {
            public CustomDrawRequest obj;
        }
        private static int AddToList(List<CustomDrawRequest> targetLst, CustomDrawRequest ths)
        {
            int index = targetLst.Count;
            targetLst.Add(ths);
            return index;
        }

        private static int AddToList(NativeList_ulong targetLst, ulong targetInd)
        {
            int index = targetLst.Length;
            targetLst.Add(targetInd);
            return index;
        }

        private static CustomDrawRequest RemoveFromList(List<CustomDrawRequest> targetLst, int targetIndex)
        {
            targetLst[targetIndex] = targetLst[targetLst.Count - 1];
            CustomDrawRequest cdr = targetLst[targetIndex];
            targetLst.RemoveAt(targetLst.Count - 1);
            return cdr;
        }

        private static ulong RemoveFromList(NativeList_ulong targetLst, int targetIndex)
        {
            targetLst[targetIndex] = targetLst[targetLst.Length - 1];
            ulong cdr = targetLst[targetIndex];
            targetLst.RemoveLast();
            return cdr;
        }
        protected virtual void OnEnableFunc() { }
        protected virtual void OnDisableFunc() { }
        public virtual void PrepareJob(PipelineResources resources) { }
        public virtual void FinishJob() { }
        public abstract bool Cull(float4* frustumPlanes);
        protected abstract void DrawCommand(out bool drawGBuffer, out bool drawShadow, out bool drawTransparent);
        public virtual void DrawDepthPrepass(CommandBuffer buffer) { }
        public virtual void DrawGBuffer(CommandBuffer buffer) { }
        public virtual void DrawShadow(CommandBuffer buffer) { }
        public virtual void DrawMotionVector(CommandBuffer buffer) { }
        public virtual void DrawTransparent(CommandBuffer buffer) { }
        public static NativeList_ulong drawGBufferList { get; private set; }
        public static NativeList_ulong drawShadowList { get; private set; }
        public static NativeList_ulong drawTransparentList { get; private set; }
        public static List<CustomDrawRequest> allEvents { get; private set; }
        private int gbufferIndex, shadowIndex, mvIndex, transIndex;
        public int index { get; private set; }
        private bool drawGBuffer, drawShadow, drawTransparent;
        private static bool initialized = false;
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            drawGBufferList = new NativeList_ulong(30, Unity.Collections.Allocator.Persistent);
            drawShadowList = new NativeList_ulong(30, Unity.Collections.Allocator.Persistent);
            drawTransparentList = new NativeList_ulong(30, Unity.Collections.Allocator.Persistent);
            allEvents = new List<CustomDrawRequest>(30);
        }
        private void OnEnable()
        {
            Initialize();
            DrawCommand(out drawGBuffer, out drawShadow, out drawTransparent);
            index = AddToList(allEvents, this);
            if (drawGBuffer) gbufferIndex = AddToList(drawGBufferList, (ulong)MUnsafeUtility.GetManagedPtr(this));
            if (drawShadow) shadowIndex = AddToList(drawShadowList, (ulong)MUnsafeUtility.GetManagedPtr(this));
            if (drawTransparent) transIndex = AddToList(drawTransparentList, (ulong)MUnsafeUtility.GetManagedPtr(this));
            OnEnableFunc();
        }
        public static void Dispose()
        {
            drawGBufferList.Dispose();
            drawShadowList.Dispose();
            drawTransparentList.Dispose();
            allEvents = null;
            initialized = false;
        }
        private void OnDisable()
        {
            if (initialized)
            {
                if (drawGBuffer)
                {
                    var a = RemoveFromList(drawGBufferList, gbufferIndex);
                    MUnsafeUtility.GetObject<CustomDrawRequest>((void*)a).gbufferIndex = gbufferIndex;
                }
                if (drawShadow)
                {
                    var a = RemoveFromList(drawShadowList, shadowIndex);
                    MUnsafeUtility.GetObject<CustomDrawRequest>((void*)a).shadowIndex = shadowIndex;
                }
                if (drawTransparent)
                {
                    var a = RemoveFromList(drawTransparentList, transIndex);
                    MUnsafeUtility.GetObject<CustomDrawRequest>((void*)a).transIndex = transIndex;
                }
                var b = RemoveFromList(allEvents, index);
                b.index = index;
            }
        }
    }
}