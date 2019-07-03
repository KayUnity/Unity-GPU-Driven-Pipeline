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
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
[ExecuteInEditMode]
public unsafe class Test : MonoBehaviour
{
    [EasyButtons.Button]
    void RunTest()
    {
        NativeList_Int lst = new NativeList_Int(10, Allocator.Temp);
        for(int i = 0; i < 20; ++i)
        {
            lst.Add(i);
        }
        lst.Remove(5);
        Debug.Log(lst.Length);
        foreach(var i in lst)
        {
            Debug.Log(i);
        }
    }
}
#endif
