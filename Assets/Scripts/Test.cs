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
        Debug.Log(Mathf.Repeat(50.7f, 10));
        Debug.Log(repeat(50.7f, 10));
    }
    private void Update()
    {
        SceneController.Update();

    }

}
