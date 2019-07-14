using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using MPipeline;
public class MultiLayerTex : EditorWindow
{
    [MenuItem("MPipeline/MultiLayer Texture")]
    private static void CreateWizard()
    {
        MultiLayerTex window = (MultiLayerTex)GetWindow(typeof(MultiLayerTex));
        window.Show();
    }
    [System.Serializable]
    struct TextureSettings
    {
        public bool isOpen;
        public Texture targetTexture;
        public float2 scale;
        public float2 offset;
        public float blendAlpha;
    }
    [SerializeField] private Material targetShowMat;
    [SerializeField] private ComputeShader drawShader;
    [SerializeField] private List<TextureSettings> allTextures = new List<TextureSettings>();
    [SerializeField] private RenderTexture rt;
    [SerializeField] private Vector2Int rtSize = new Vector2Int(1024, 1024);
    [SerializeField] private bool sRGB = false;
    [SerializeField] private Color initColor = Color.black;
    [SerializeField] private RenderTextureFormat format = RenderTextureFormat.ARGB32;
    private void OnGUI()
    {

        if (!drawShader) drawShader = Resources.Load<ComputeShader>("ShaderBlend");
        targetShowMat = EditorGUILayout.ObjectField("Target Material: ", targetShowMat, typeof(Material), true) as Material;
        //TODO
        rtSize = EditorGUILayout.Vector2IntField("Texture Size: ", rtSize);
        format = (RenderTextureFormat)EditorGUILayout.EnumPopup("Texture Format: ", format);
        sRGB = EditorGUILayout.Toggle("sRGB", sRGB);

        //Set RT
        if (!rt)
        {
            rt = new RenderTexture(new RenderTextureDescriptor
            {
                colorFormat = format,
                depthBufferBits = 0,
                enableRandomWrite = true,
                height = rtSize.y,
                width = rtSize.x,
                volumeDepth = 1,
                msaaSamples = 1,
                sRGB = false,
                dimension = TextureDimension.Tex2D
            });
            rt.Create();
        }
        else if (rt.format != format || rt.width != rtSize.x || rt.height != rtSize.y)
        {
            rt.Release();
            rt.width = rtSize.x;
            rt.height = rtSize.y;
            rt.format = format;
            rt.Create();
        }
        if (targetShowMat) targetShowMat.SetTexture(ShaderIDs._MainTex, rt);
        drawShader.SetTexture(0, ShaderIDs._MainTex, rt);
        drawShader.SetTexture(1, ShaderIDs._MainTex, rt);
        drawShader.SetVector("_MainTex_TexelSize: ", float4(1f / rtSize.x, 1f / rtSize.y, rtSize.x - 0.1f, rtSize.y - 0.1f));
        initColor = EditorGUILayout.ColorField("Initial Color: ", initColor);
        drawShader.SetVector("_InitialColor", new Vector4(initColor.r, initColor.g, initColor.b, initColor.a));
        drawShader.Dispatch(1, Mathf.CeilToInt(rtSize.x / 8f), Mathf.CeilToInt(rtSize.y / 8f), 1);
        for (int i = 0; i < allTextures.Count; ++i)
        {
            var e = allTextures[i];
            EditorGUILayout.BeginHorizontal();
            e.isOpen = EditorGUILayout.Foldout(e.isOpen, "Texture Layer " + i);
            bool remove = GUILayout.Button("Remove");
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                allTextures.RemoveAt(i);
                i--;
            }
            else
            {
                
                if (e.isOpen)
                {
                    EditorGUI.indentLevel++;
                    e.targetTexture = EditorGUILayout.ObjectField("Texture: ", e.targetTexture, typeof(Texture), true) as Texture;
                    e.blendAlpha = EditorGUILayout.Slider("Blend Alpha: ", e.blendAlpha, 0, 1);
                    e.scale = EditorGUILayout.Vector2Field("Tiling Scale: ", e.scale);
                    e.offset = EditorGUILayout.Vector2Field("Tiling Offset: ", e.offset);
                    EditorGUI.indentLevel--;
                }
                allTextures[i] = e;
            }
        }
        if(GUILayout.Button("Add New Texture"))
        {
            allTextures.Add(new TextureSettings
            {
                blendAlpha = 1f,
                isOpen = false,
                offset = 0,
                scale = 1,
                targetTexture = null
            });
        }
    }
}
