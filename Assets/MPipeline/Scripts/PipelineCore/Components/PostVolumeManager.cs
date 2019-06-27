using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using static Unity.Mathematics.math;
namespace MPipeline {
    public sealed unsafe class PostVolumeManager : JobProcessEvent
    {
        public static NativeList<PostVolume.PostVolumeDataPackage> postVolumes { get; private set; }
        private JobHandle handle;
        private int resultIndex;
        public override void PrepareJob()
        {
            handle = new CullJob
            {
                allPackages = postVolumes.unsafePtr,
                count = postVolumes.Length,
                position = transform.position,
                resultIndex = resultIndex.Ptr()
            }.Schedule();
            resultIndex = -1;
        }
        public override void FinishJob()
        {
            handle.Complete();
            if(resultIndex >= 0)
            {
                //TODO
            }
        }
        [Unity.Burst.BurstCompile]
        public struct CullJob : IJob
        {
            public PostVolume.PostVolumeDataPackage* allPackages;
            public float3 position;
            public int count;
            public int* resultIndex;
            public void Execute()
            {
                for(int i = 0; i < count; ++i)
                {
                    float3 localPos = mul(allPackages[i].worldToLocal, float4(position, 1)) + 0.5f;
                    if(lengthsq(localPos - saturate(localPos)) > 1e-5f)
                    {
                        *resultIndex = i;
                        return;
                    }
                }
            }
        }
    }
}
