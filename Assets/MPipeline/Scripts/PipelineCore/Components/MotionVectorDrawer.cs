using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MPipeline;
[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class MotionVectorDrawer : MonoBehaviour
{
    private Renderer rend;
    private static MaterialPropertyBlock block = null;
    private Matrix4x4 lastLocalToWorld;
    private static List<MotionVectorDrawer> allDrawers = new List<MotionVectorDrawer>(50);
    public static List<MotionVectorDrawer> AllDrawers { get { return allDrawers; } }
    private int index;
    private void OnEnable()
    {
        if (!rend) rend = GetComponent<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
        lastLocalToWorld = transform.localToWorldMatrix;
        index = allDrawers.Count;
        allDrawers.Add(this);
        rend.GetPropertyBlock(block);
        block.SetInt(ShaderIDs._OffsetIndex, index);
        rend.SetPropertyBlock(block);
    }

    private void OnDisable()
    {
        if (rend) rend.SetPropertyBlock(null);
        var lastOne = allDrawers[allDrawers.Count - 1];
        allDrawers[index] = lastOne;
        lastOne.index = index;
        lastOne.rend.GetPropertyBlock(block);
        block.SetInt(ShaderIDs._OffsetIndex, index);
        lastOne.rend.SetPropertyBlock(block);
        allDrawers.RemoveAt(allDrawers.Count - 1);
    }
}
