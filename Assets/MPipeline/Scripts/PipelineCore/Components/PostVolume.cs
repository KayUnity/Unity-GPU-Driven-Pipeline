using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
namespace MPipeline
{
    public class PostVolume : MonoBehaviour
    {
        [System.Serializable]
        public struct PostVolumeDataPackage
        {
            public float3x4 worldToLocal;
        }
        public int packageIndex = -1;
        [EasyButtons.Button]
        void UpdateData()
        {
            ref PostVolumeDataPackage package = ref PostVolumeManager.postVolumes[packageIndex];
            float4x4 worldToLocal = transform.worldToLocalMatrix;
            package.worldToLocal = new float3x4(worldToLocal.c0.xyz, worldToLocal.c1.xyz, worldToLocal.c2.xyz, worldToLocal.c3.xyz);
        }
    }
}