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

[ExecuteInEditMode]
public unsafe class Test : MonoBehaviour
{
    private AsyncOperation aop;
    private bool loaded;
    private void Start()
    {
        aop = SceneManager.LoadSceneAsync(1, LoadSceneMode.Additive);
        loaded = true;
    }
    private void Update()
    {
        if(aop.isDone)
        {
            if(loaded)
            {
                aop = SceneManager.UnloadSceneAsync(1, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                loaded = false;
            }
            else
            {
                aop = SceneManager.LoadSceneAsync(1, LoadSceneMode.Additive);
                loaded = true;
            }
        }
    }
}
