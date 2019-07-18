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
    public Texture tex0;
    public Texture tex1;
    public Texture tex2;
    public Texture tex3;
    private VirtualTexture vt;
    private void Start()
    {
        NativeArray<VirtualTextureFormat> formats = new NativeArray<VirtualTextureFormat>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        formats[0] = new VirtualTextureFormat(VirtualTextureSize.x256, RenderTextureFormat.ARGB32, "_SampledVT");
        vt = new VirtualTexture(5, 2, formats);
        StartCoroutine(run());
    }
    IEnumerator run()
    {
        int a;
        yield return null;
        a = vt.LoadNewTexture(0, 1);
        Graphics.Blit(tex0, vt.GetTexture(0), 0, a);
        yield return null;
        a = vt.LoadNewTexture(int2(0, 1), 1);
        Graphics.Blit(tex1, vt.GetTexture(0), 0, a);
        yield return null;
        a = vt.LoadNewTexture(int2(1, 0), 1);
        Graphics.Blit(tex2, vt.GetTexture(0), 0, a);
        yield return null;
        a = vt.LoadNewTexture(1, 1);
        Graphics.Blit(tex3, vt.GetTexture(0), 0, a);
    }

    private void Update()
    {
        vt.Update();
        if(Input.GetKeyDown(KeyCode.Space))
        {
            vt.CombineTexture(0, 2);
        }
    }

    private void OnDestroy()
    {
        vt.Dispose();
    }

}
