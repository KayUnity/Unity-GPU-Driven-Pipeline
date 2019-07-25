using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class TextureCombiner : EditorWindow
{
    [MenuItem("MPipeline/Combine Texture")]
    private static void CreateWizard()
    {
        TextureCombiner window = (TextureCombiner)GetWindow(typeof(TextureCombiner));
        window.Show();
    }
    public enum ChannelUsage
    {
        R, G, B, A
    };
    public Texture2D r;
    public ChannelUsage rTextureChannel;
    public Texture2D g;
    public ChannelUsage gTextureChannel;
    public Texture2D b;
    public ChannelUsage bTextureChannel;
    public Texture2D a;
    public ChannelUsage aTextureChannel;
    public Vector2Int size = new Vector2Int(1024, 1024);
    public bool srgb = false;
    public string path = "Assets/MyTexture.png";

    private float GetColor(ChannelUsage usage, Color c)
    {
        switch(usage)
        {
            case ChannelUsage.A:
                return c.a;
            case ChannelUsage.R:
                return c.r;
            case ChannelUsage.B:
                return c.b;
            case ChannelUsage.G:
                return c.g;
        }
        return 0;
    }

    private void OnGUI()
    {
        r = EditorGUILayout.ObjectField("R Channel Texture: ", r, typeof(Texture2D), false) as Texture2D;
        rTextureChannel = (ChannelUsage)EditorGUILayout.EnumPopup("R Texture Channel: ", rTextureChannel);
        g = EditorGUILayout.ObjectField("G Channel Texture: ", g, typeof(Texture2D), false) as Texture2D;
        gTextureChannel = (ChannelUsage)EditorGUILayout.EnumPopup("G Texture Channel: ", gTextureChannel);
        b = EditorGUILayout.ObjectField("B Channel Texture: ", b, typeof(Texture2D), false) as Texture2D;
        bTextureChannel = (ChannelUsage)EditorGUILayout.EnumPopup("B Texture Channel: ", bTextureChannel);
        a = EditorGUILayout.ObjectField("A Channel Texture: ", a, typeof(Texture2D), false) as Texture2D;
        aTextureChannel = (ChannelUsage)EditorGUILayout.EnumPopup("A Texture Channel: ", aTextureChannel);
        size = EditorGUILayout.Vector2IntField("Texture target Size", size);
        path = EditorGUILayout.TextField("Path: ", path);
        srgb = EditorGUILayout.Toggle("SRGB", srgb);
        if (GUILayout.Button("Combine Texture"))
        {
            if (r && !r.isReadable)
            {
                Debug.LogError("You have to set R channel Texture as readable!");
                return;
            }
            if (g && !g.isReadable)
            {
                Debug.LogError("You have to set G channel Texture as readable!");
                return;
            }
            if (b && !b.isReadable)
            {
                Debug.LogError("You have to set B channel Texture as readable!");
                return;
            }
            if (a && !a.isReadable)
            {
                Debug.LogError("You have to set A channel Texture as readable!");
                return;
            }
            Texture2D resultTex = new Texture2D(size.x, size.y, TextureFormat.ARGB32, false, !srgb);
            for (int x = 0; x < size.x; ++x)
            {
                for (int y = 0; y < size.y; ++y)
                {
                    Color cl = new Color
                    {
                        r = r ? GetColor(rTextureChannel, r.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y)) : 0,
                        g = g ? GetColor(gTextureChannel, g.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y)) : 0,
                        b = b ? GetColor(bTextureChannel, b.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y)) : 0,
                        a = a ? GetColor(aTextureChannel, a.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y)) : 0
                    };
                    resultTex.SetPixel(x, y, cl);
                }
            }
            resultTex.Apply();
            System.IO.File.WriteAllBytes(path, resultTex.EncodeToPNG());

        }
    }
}