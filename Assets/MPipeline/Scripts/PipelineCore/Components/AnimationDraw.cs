using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Collections;
using static Unity.Mathematics.math;
namespace MPipeline
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public unsafe sealed class AnimationDraw : CustomDrawRequest
    {
        private SkinnedMeshRenderer skinRenderer;
        private ComputeBuffer verticesBuffer;
        private ComputeBuffer lastVerticesBuffer;
        private ComputeBuffer skinVerticesBuffer;
        private ComputeBuffer bonesBuffer;
        private List<ComputeBuffer> triangleBuffers;
        private Material[] allMats;
        private float4x4 localMatrix;
        private NativeArray<float3x4> skinResults;
        private NativeArray<float4x4> bindPoses;
        private NativeArray<float3> boneLossyScales;
        private TransformAccessArray bones;
        private JobHandle handle;
        private ComputeShader skinShader;
        public Vector3 boundingBoxPosition = Vector3.zero;
        public Vector3 boundingBoxExtents = new Vector3(0.5f, 0.5f, 0.5f);
        struct Vertex
        {
            public float4 tangent;
            public float3 normal;
            public float3 position;
            public float2 uv;
        }
        struct SkinVertex
        {
            public float4 tangent;
            public float3 normal;
            public float3 position;
            public float2 uv;
            public int4 boneIndex;
            public float4 boneWeight;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.white;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boundingBoxPosition, boundingBoxExtents * 2);
        }
        private void Awake()
        {
            skinRenderer = GetComponent<SkinnedMeshRenderer>();
            
            triangleBuffers = new List<ComputeBuffer>(skinRenderer.sharedMesh.subMeshCount);
            for(int i = 0; i < triangleBuffers.Capacity; ++i)
            {
                int[] tris = skinRenderer.sharedMesh.GetTriangles(i);
                var triBuffer = new ComputeBuffer(tris.Length, sizeof(int));
                triBuffer.SetData(tris);
                triangleBuffers.Add(triBuffer);
            }
            
            
            Vector3[] verts = skinRenderer.sharedMesh.vertices;
            Vector4[] tans = skinRenderer.sharedMesh.tangents;
            Vector3[] norms = skinRenderer.sharedMesh.normals;
            Vector2[] uvs = skinRenderer.sharedMesh.uv;
            NativeArray<SkinVertex> allVertices = new NativeArray<SkinVertex>(verts.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            BoneWeight[] weights = skinRenderer.sharedMesh.boneWeights;
            SkinVertex* vertsPtr = allVertices.Ptr();
            var bonesTrans = skinRenderer.bones;
            bindPoses = new NativeArray<float4x4>(bonesTrans.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            boneLossyScales = new NativeArray<float3>(bonesTrans.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Matrix4x4[] bindArr = skinRenderer.sharedMesh.bindposes;
            for (int i = 0; i < bindArr.Length; ++i)
            {
                bindPoses[i] = bindArr[i];
                Transform part = bonesTrans[i].parent;
                boneLossyScales[i] = part ? (float3)part.lossyScale : float3(1, 1, 1);
            }
            skinResults = new NativeArray<float3x4>(bonesTrans.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            bones = new TransformAccessArray(bonesTrans);
            for (int i = 0; i < allVertices.Length; ++i)
            {
                ref var sk = ref vertsPtr[i];
                sk.tangent = tans[i];
                sk.position = verts[i];
                sk.normal = norms[i];
                sk.uv = uvs[i];
                BoneWeight bw = weights[i];
                sk.boneIndex = int4(bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3);
                sk.boneWeight = float4(bw.weight0, bw.weight1, bw.weight2, bw.weight3);
            }
            verticesBuffer = new ComputeBuffer(allVertices.Length, sizeof(Vertex));
            lastVerticesBuffer = new ComputeBuffer(allVertices.Length, sizeof(Vertex));
            skinVerticesBuffer = new ComputeBuffer(allVertices.Length, sizeof(SkinVertex));
            bonesBuffer = new ComputeBuffer(bonesTrans.Length, sizeof(float3x4));
            skinVerticesBuffer.SetData(allVertices);
            allVertices.Dispose();
            allMats = skinRenderer.sharedMaterials;
        }
        protected override void OnEnableFunc()
        {
            skinRenderer.enabled = false;
        }
        private void OnDestroy()
        {
            verticesBuffer.Dispose();
            lastVerticesBuffer.Dispose();
            skinVerticesBuffer.Dispose();
            boneLossyScales.Dispose();
            bones.Dispose();
            bindPoses.Dispose();
            skinResults.Dispose();
            bonesBuffer.Dispose();
            foreach(var i in triangleBuffers)
            {
                i.Dispose();
            }
            triangleBuffers.Clear();
        }
        public override bool Cull(float4* frustumPlanes)
        {
            return MathLib.BoxIntersect(ref localMatrix, boundingBoxPosition, boundingBoxExtents, frustumPlanes, 6);
        }
        private void DrawPass(CommandBuffer buffer, int pass)
        {
            buffer.SetGlobalBuffer(ShaderIDs.verticesBuffer, verticesBuffer);
            int len = min(triangleBuffers.Count, allMats.Length);
            for (int i = 0; i < len; ++i)
            {
                buffer.SetGlobalBuffer(ShaderIDs.triangleBuffer, triangleBuffers[i]);
                buffer.DrawProcedural(Matrix4x4.identity, allMats[i], pass, MeshTopology.Triangles, triangleBuffers[i].count, 1);
            }
        }
        public override void DrawGBuffer(CommandBuffer buffer)
        {
            DrawPass(buffer, 0);
        }
        public override void DrawMotionVector(CommandBuffer buffer)
        {
            buffer.SetGlobalBuffer(ShaderIDs._LastVerticesBuffer, lastVerticesBuffer);
            DrawPass(buffer, 2);
        }
        public override void DrawShadow(CommandBuffer buffer)
        {
            DrawPass(buffer, 1);
        }
        public override void PrepareJob(PipelineResources resources)
        {
            localMatrix = transform.localToWorldMatrix;
            skinShader = resources.shaders.gpuSkin;
            handle = new BonesTransform
            {
                bindPoses = bindPoses.Ptr(),
                boneLossyScales = boneLossyScales.Ptr(),
                results = skinResults.Ptr()
            }.Schedule(bones);
        }

        public override void FinishJob()
        {
            ComputeBuffer temp = verticesBuffer;
            verticesBuffer = lastVerticesBuffer;
            lastVerticesBuffer = temp;
            CommandBuffer bf = RenderPipeline.BeforeFrameBuffer;
            bf.SetComputeBufferParam(skinShader, 0, ShaderIDs._SkinVerticesBuffer, skinVerticesBuffer);
            bf.SetComputeBufferParam(skinShader, 0, ShaderIDs._BonesBuffer, bonesBuffer);
            bf.SetComputeBufferParam(skinShader, 0, ShaderIDs.verticesBuffer, verticesBuffer);
            ComputeShaderUtility.Dispatch(skinShader, bf, 0, skinVerticesBuffer.count);
            handle.Complete();
            bonesBuffer.SetData(skinResults);
        }
        [Unity.Burst.BurstCompile]
        private struct BonesTransform : IJobParallelForTransform
        {
            [NativeDisableUnsafePtrRestriction]
            public float3x4* results;
            [NativeDisableUnsafePtrRestriction]
            public float4x4* bindPoses;
            [NativeDisableUnsafePtrRestriction]
            public float3* boneLossyScales;
            public void Execute(int index, TransformAccess access)
            {
                float4x4 bonesLocal = Matrix4x4.TRS(access.position, access.rotation, boneLossyScales[index]);
                bonesLocal = mul(bonesLocal, bindPoses[index]);
                results[index] = float3x4(bonesLocal.c0.xyz, bonesLocal.c1.xyz, bonesLocal.c2.xyz, bonesLocal.c3.xyz);
            }
        }
    }
}