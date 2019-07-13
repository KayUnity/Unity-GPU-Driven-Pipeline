using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using MPipeline;
public unsafe sealed class Test : MonoBehaviour
{

    [SerializeField] VirtualTexture vt;
    public Texture t0;
    public Texture t1;
    public Texture t2;
    public Texture t3;
    private void Start()
    {
        NativeArray<RenderTextureFormat> formats = new NativeArray<RenderTextureFormat>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        formats[0] = RenderTextureFormat.ARGBHalf;
        vt.Init(256, 10, 2, formats);
        StartCoroutine(run());
       
    }
    
    IEnumerator run()
    {
        RenderTexture curVt = vt.GetTexture(0);
        yield return null;
        Graphics.Blit(t0, curVt, Vector2.one, Vector2.zero, 0, vt.LoadNewTexture(0, 1));
        yield return null;
        Graphics.Blit(t1, curVt, Vector2.one, Vector2.zero, 0, vt.LoadNewTexture(int2(0, 1), 1));
        yield return null;
        Graphics.Blit(t2, curVt, Vector2.one, Vector2.zero, 0, vt.LoadNewTexture(int2(1, 0), 1));
        yield return null;
        Graphics.Blit(t3, curVt, Vector2.one, Vector2.zero, 0, vt.LoadNewTexture(int2(1, 1), 1));
        Debug.Log(vt.LeftedTextureElement);
    }

    private void Update()
    {
        SceneController.Update();
        vt.Update();
        Shader.SetGlobalTexture("_AlbedoVT", vt.GetTexture(0));
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
