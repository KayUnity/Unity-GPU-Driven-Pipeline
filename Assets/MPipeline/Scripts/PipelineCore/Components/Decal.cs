using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;

namespace MPipeline
{
    [ExecuteInEditMode]
    public unsafe class Decal : MonoBehaviour
    {
        public static NativeList<DecalData> decalDatas { get; private set; }
        public static int allDecalCount
        {
            get
            {
                if (decalDatas.isCreated)
                    return decalDatas.Length;
                return 0;
            }
        }
        private int index;
        public float avaliableDistance = 100;
        public int4 albedoScaleOffset = 0;
        public int4 normalScaleOffset = 0;
        public int importance = 0;
        public int albedoIndex = -1;
        public int normalIndex = -1;
        private void OnEnable()
        {
            if (!decalDatas.isCreated) decalDatas = new NativeList<DecalData>(10, Unity.Collections.Allocator.Persistent);
            index = decalDatas.Length;
            float4x4 localToWorld = transform.localToWorldMatrix;
            float4x4 worldToLocal = transform.worldToLocalMatrix;
            decalDatas.Add(
                new DecalData
                {
                    position = transform.position,
                    rotation = float3x4(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz, localToWorld.c3.xyz),
                    worldToLocal = float3x4(worldToLocal.c0.xyz, worldToLocal.c1.xyz, worldToLocal.c2.xyz, worldToLocal.c3.xyz),
                    albedoScaleOffset = albedoScaleOffset,
                    normalScaleOffset = normalScaleOffset,
                    importance = importance,
                    comp = MUnsafeUtility.GetManagedPtr(this),
                    texIndex = int2(albedoIndex, normalIndex),
                    avaliableDistance = avaliableDistance
                });
        }
        [EasyButtons.Button]
        void UpdateData()
        {
            ref DecalData da = ref decalDatas[index];
            da.position = transform.position;
            float4x4 localToWorld = transform.localToWorldMatrix;
            float4x4 worldToLocal = transform.worldToLocalMatrix;
            da.rotation = float3x4(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz, localToWorld.c3.xyz);
            da.worldToLocal = float3x4(worldToLocal.c0.xyz, worldToLocal.c1.xyz, worldToLocal.c2.xyz, worldToLocal.c3.xyz);
            da.albedoScaleOffset = albedoScaleOffset;
            da.normalScaleOffset = normalScaleOffset;
            da.importance = importance;
            da.texIndex = int2(albedoIndex, normalIndex);
            da.avaliableDistance = avaliableDistance;
        }

        private void OnDisable()
        {
            if (!decalDatas.isCreated) return;
            Decal lastDec = MUnsafeUtility.GetObject<Decal>(decalDatas[decalDatas.Length - 1].comp);
            lastDec.index = index;
            decalDatas[index] = decalDatas[decalDatas.Length - 1];
            decalDatas.RemoveLast();
        }

        private void OnDrawGizmos()
        {
            GizmosHelper gz = new GizmosHelper();
            gz.Init();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 1, 1, 0.4f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            gz.Dispose();
        }

        private void OnDrawGizmosSelected()
        {
            GizmosHelper gz = new GizmosHelper();
            gz.Init();
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0, 0, 1, 0.2f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            gz.Dispose();
        }
    }
}
