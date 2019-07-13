﻿using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;


public static class ComputeShaderUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dispatch(ComputeShader shader, CommandBuffer buffer, int kernal, int count)
    {
        uint x, y, z;
        shader.GetKernelThreadGroupSizes(kernal, out x, out y, out z);
        int threadPerGroup = Mathf.CeilToInt(count / (float)x);
        buffer.SetComputeIntParam(shader, ShaderIDs._Count, count);
        buffer.DispatchCompute(shader, kernal, threadPerGroup, 1, 1);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DispatchDirect(ComputeShader shader, int kernal, int count)
    {
        uint x, y, z;
        shader.GetKernelThreadGroupSizes(kernal, out x, out y, out z);
        int threadPerGroup = Mathf.CeilToInt(count / (float)x);
        shader.SetInt(ShaderIDs._Count, count);
        shader.Dispatch(kernal, threadPerGroup, 1, 1);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetThreadPerGroup(ComputeShader shader, int kernal, int count)
    {
        uint x, y, z;
        shader.GetKernelThreadGroupSizes(kernal, out x, out y, out z);
        return Mathf.CeilToInt(count / (float)x);
    }
}