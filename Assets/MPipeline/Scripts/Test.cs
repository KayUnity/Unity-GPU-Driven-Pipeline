using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using static Unity.Mathematics.math;
using MPipeline;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
public unsafe class Test : MonoBehaviour
{
    private float3 initPos;
    private void Start()
    {
        initPos = transform.position;
    }
    private void Update()
    {
        transform.position = initPos + float3(0, sin(Time.time * 10), 0);
    }
}
#endif
