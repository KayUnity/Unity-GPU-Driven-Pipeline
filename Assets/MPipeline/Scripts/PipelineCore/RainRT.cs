using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using System;
using Random = Unity.Mathematics.Random;
namespace MPipeline
{
    public unsafe sealed class RainRT : MonoBehaviour
    {
        public ComputeShader shader;
        public float rainSpeed = 10;
        private ComputeBuffer rippleBuffer;
        private RenderTexture rippleTexture;
        private const int SIZE = 256;
        private const int RIPPLECOUNT = 128;
        private const int RIPPLESIZE = 8;
        private static readonly int _RippleBuffer = Shader.PropertyToID("_RippleBuffer");
        private static readonly int _RainRipple = Shader.PropertyToID("_RainRipple");
        private struct Ripple
        {
            public uint2 position;
            public uint range;
            public float timeLine;
        };
        private void Awake()
        {
            rippleTexture = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = RenderTextureFormat.RGHalf,
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                width = SIZE,
                height = SIZE,
                volumeDepth = 1,
                msaaSamples = 1
            });
            rippleTexture.filterMode = FilterMode.Bilinear;
            rippleTexture.wrapMode = TextureWrapMode.Repeat;
            rippleTexture.Create();
            rippleBuffer = new ComputeBuffer(RIPPLECOUNT, sizeof(Ripple));
            Random r = new Random((uint)Guid.NewGuid().GetHashCode());
            NativeArray<Ripple> ripplesArray = new NativeArray<Ripple>(RIPPLECOUNT, Allocator.Temp);
            for(int i = 0; i < RIPPLECOUNT; ++i)
            {
                Ripple rip = new Ripple
                {
                    position = r.NextUInt2(0, 511),
                    range = r.NextUInt(1, 5),
                    timeLine = r.NextFloat() * 0.8f
                };
                ripplesArray[i] = rip;
            }
            rippleBuffer.SetData(ripplesArray);
            ripplesArray.Dispose();
        }
        private void OnDestroy()
        {
            DestroyImmediate(rippleTexture);
            rippleBuffer.Dispose();
        }
        private void Update()
        {
            CommandBuffer cb = RenderPipeline.BeforeFrameBuffer;
            cb.SetRenderTarget(color: rippleTexture.colorBuffer, depth: rippleTexture.depthBuffer);
            cb.ClearRenderTarget(false, true, Color.black);
            cb.SetComputeTextureParam(shader, 0, ShaderIDs._MainTex, rippleTexture);
            cb.SetComputeBufferParam(shader, 0, _RippleBuffer, rippleBuffer);
            cb.SetComputeBufferParam(shader, 1, _RippleBuffer, rippleBuffer);
            cb.SetComputeFloatParam(shader, ShaderIDs._DeltaTime, Time.deltaTime * rainSpeed);
            cb.DispatchCompute(shader, 0, 5, 5, 128);
            cb.DispatchCompute(shader, 1, RIPPLECOUNT / 64, 1, 1);
            cb.SetGlobalTexture(_RainRipple, rippleTexture);
        }
    }
}