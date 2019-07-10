using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    [System.Serializable]
    public class GPURPScene
    {
        public ClusterMatResources clusterResources;
        private GPURPScene current;

     
        private static void SetBuffer(Transform trans, TransformAccessArray array)
        {
            if (trans.childCount > 0)
            {
                for (int i = 0; i < trans.childCount; ++i)
                {
                    SetBuffer(trans.GetChild(i), array);
                }
            }
            else
            {
                array.Add(trans);
            }
        }
        private JobHandle jobHandle;
        public float3 offset;
        public void Init(PipelineResources resources)
        {
            if (current != null)
            {
                Debug.LogError("GPU RP Scene should be singleton!");
                return;
            }
            current = this;
            SceneController.Awake(resources, clusterResources);
        }

        public int targetVolume = 0;

        public void Dispose()
        {
            SceneController.Dispose();
            current = null;
        }
    }
}
