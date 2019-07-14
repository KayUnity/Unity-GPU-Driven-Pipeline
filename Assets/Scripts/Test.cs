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
    private void Update()
    {
        SceneController.Update();
    }

}
