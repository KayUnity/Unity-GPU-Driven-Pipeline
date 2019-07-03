using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Mathematics;
namespace MPipeline
{
    public abstract unsafe class CustomDrawRequest : MonoBehaviour
    {
        private static int AddToList(List<CustomDrawRequest> targetLst, CustomDrawRequest ths)
        {
            int index = targetLst.Count;
            targetLst.Add(ths);
            return index;
        }

        private static int AddToList(NativeList_Int targetLst, int targetInd)
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

        private static int RemoveFromList(NativeList_Int targetLst, int targetIndex)
        {
            targetLst[targetIndex] = targetLst[targetLst.Length - 1];
            int cdr = targetLst[targetIndex];
            targetLst.RemoveAt(targetLst.Length - 1);
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
        public static NativeList_Int drawGBufferList;
        public static NativeList_Int drawShadowList;
        public static NativeList_Int drawTransparentList;
        private static bool initialized = false;
        public static List<CustomDrawRequest> allEvents = new List<CustomDrawRequest>(30);
        private int gbufferIndex, shadowIndex, mvIndex, transIndex;
        private int index;
        private bool drawGBuffer, drawShadow, drawTransparent;
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            drawGBufferList = new NativeList_Int(30, Unity.Collections.Allocator.Persistent);
            drawShadowList = new NativeList_Int(30, Unity.Collections.Allocator.Persistent);
            drawTransparentList = new NativeList_Int(30, Unity.Collections.Allocator.Persistent);
        }
        private void OnEnable()
        {
            Initialize();
            DrawCommand(out drawGBuffer, out drawShadow, out drawTransparent);
            index = AddToList(allEvents, this);
            if (drawGBuffer) gbufferIndex = AddToList(drawGBufferList, index);
            if (drawShadow) shadowIndex = AddToList(drawShadowList, index);
            if (drawTransparent) transIndex = AddToList(drawTransparentList, index);
            OnEnableFunc();
        }
        private void OnDisable()
        {
            if (drawGBuffer)
            {
                var a = RemoveFromList(drawGBufferList, index);
                allEvents[a].gbufferIndex = gbufferIndex;
            }
            if (drawShadow)
            {
                var a = RemoveFromList(drawShadowList, index);
                allEvents[a].shadowIndex = shadowIndex;
            }
            if (drawTransparent)
            {
                var a = RemoveFromList(drawTransparentList, index);
                allEvents[a].transIndex = transIndex;
            }
            var b = RemoveFromList(allEvents, index);
            b.index = index;
        }
    }
}