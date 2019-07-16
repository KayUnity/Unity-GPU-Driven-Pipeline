using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using MPipeline;
public unsafe sealed class Test : MonoBehaviour
{
    float repeat(float v, float length)
    {
        float left = v % length;
        return left > 0 ? left : length - left;
    }
    [EasyButtons.Button]
    void Run()
    {
    }
    private void Update()
    {

    }

}
