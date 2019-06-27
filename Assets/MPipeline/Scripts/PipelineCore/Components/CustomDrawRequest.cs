using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Mathematics;
namespace MPipeline {
    public abstract unsafe class CustomDrawRequest : MonoBehaviour
    {
        protected virtual void OnEnableFunc() { }
        protected virtual void OnDisableFunc() { }
        public virtual void PrepareJob(PipelineResources resources) { }
        public virtual void FinishJob() { }
        public abstract bool Cull(float4* frustumPlanes);
        public virtual void DrawDepthPrepass(CommandBuffer buffer) { }
        public virtual void DrawGBuffer(CommandBuffer buffer) { }
        public virtual void DrawShadow(CommandBuffer buffer) { }
        public virtual void DrawMotionVector(CommandBuffer buffer) { }
        public virtual void DrawTransparent(CommandBuffer buffer) { }
        private static List<CustomDrawRequest> allDrawRequests = new List<CustomDrawRequest>(10);
        public static List<CustomDrawRequest> AllEvents { get { return allDrawRequests; } }
        private int currentIndex = 0;
        private void OnEnable()
        {
            currentIndex = allDrawRequests.Count;
            allDrawRequests.Add(this);
            OnEnableFunc();
        }
        private void OnDisable()
        {
            if (allDrawRequests.Count <= 1) allDrawRequests.Clear();
            else
            {
                allDrawRequests[currentIndex] = allDrawRequests[allDrawRequests.Count - 1];
                CustomDrawRequest cdr = allDrawRequests[currentIndex];
                cdr.currentIndex = currentIndex;
                allDrawRequests.RemoveAt(allDrawRequests.Count - 1);
            }
            OnDisableFunc();
        }
    }
}