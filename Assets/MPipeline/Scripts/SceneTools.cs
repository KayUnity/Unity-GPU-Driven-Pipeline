#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using MPipeline;
using Unity.Mathematics;
using System;
using System.IO;
using static Unity.Mathematics.math;

public class CombineMesh : ScriptableWizard
{
    [MenuItem("MPipeline/Combine Mesh")]
    private static void CreateWizard()
    {
        DisplayWizard<CombineMesh>("Scene Tools", "Create");
    }
    public string combineMeshPath = "Assets/";
    private static Mesh CombineAllMesh(List<MeshFilter> meshes)
    {
        List<Vector3> verts = new List<Vector3>(1000);
        List<Vector3> norms = new List<Vector3>(1000);
        List<Vector4> tans = new List<Vector4>(1000);
        List<Vector2> uv0s = new List<Vector2>(1000);
        List<int> tris = new List<int>(1000);
        float4x4 worldToLocal = meshes[0].transform.worldToLocalMatrix;

        foreach (var i in meshes)
        {
            float4x4 localToWorld = mul(worldToLocal, i.transform.localToWorldMatrix);
            float3x3 localToWorldRot = float3x3(localToWorld.c0.xyz, localToWorld.c1.xyz, localToWorld.c2.xyz);
            Vector3[] vertices = i.sharedMesh.vertices;
            for (int j = 0; j < vertices.Length; ++j)
            {
                vertices[j] = mul(localToWorld, float4(vertices[j], 1)).xyz;
            }
            Vector3[] normals = i.sharedMesh.normals;
            for (int j = 0; j < vertices.Length; ++j)
            {
                normals[j] = mul(localToWorldRot, normals[j]);
            }
            Vector4[] tangents = i.sharedMesh.tangents;
            for (int j = 0; j < vertices.Length; ++j)
            {
                float3 tan = (Vector3)tangents[j];
                float tanW = tangents[j].w;
                tangents[j] = (Vector3)mul(localToWorldRot, tan);
                tangents[j].w = tanW;
            }
            Vector2[] uv0 = i.sharedMesh.uv;
            int[] triangles = i.sharedMesh.triangles;
            for (int j = 0; j < triangles.Length; ++j)
            {
                triangles[j] += verts.Count;
            }
            tris.AddRange(triangles);
            verts.AddRange(vertices);
            norms.AddRange(normals.Length == vertices.Length ? normals : new Vector3[vertices.Length]);
            tans.AddRange(tangents.Length == vertices.Length ? tangents : new Vector4[vertices.Length]);
            uv0s.AddRange(uv0.Length == vertices.Length ? uv0 : new Vector2[vertices.Length]);
        }
        Mesh newMesh = new Mesh();
        newMesh.SetVertices(verts);
        newMesh.SetUVs(0, uv0s);
        newMesh.SetNormals(norms);
        newMesh.SetTangents(tans);
        newMesh.SetTriangles(tris, 0);
        Unwrapping.GenerateSecondaryUVSet(newMesh);
        return newMesh;
    }
    private void OnWizardCreate()
    {
        Transform[] transes = Selection.GetTransforms(SelectionMode.Unfiltered);
        List<MeshFilter> renderers = new List<MeshFilter>();
        foreach (var i in transes)
        {
            renderers.AddRange(i.GetComponentsInChildren<MeshFilter>());
        }
        if (renderers.Count == 0) return;
        Mesh combinedMesh = CombineAllMesh(renderers);
        AssetDatabase.CreateAsset(combinedMesh, combineMeshPath + combinedMesh.GetInstanceID() + ".asset");
        renderers[0].sharedMesh = combinedMesh;
        for (int i = 1; i < renderers.Count; ++i)
        {
            DestroyImmediate(renderers[i].gameObject);
        }
    }
}
public class ColliderClear : ScriptableWizard
{
    [MenuItem("MPipeline/Collider Cleaner")]
    private static void CreateWizard()
    {
        DisplayWizard<ColliderClear>("Collider", "Clean");
    }
    private void OnWizardCreate()
    {
        Transform[] trans = Selection.GetTransforms(SelectionMode.Unfiltered);
        foreach (var t in trans)
        {
            Collider[] cs = t.GetComponentsInChildren<Collider>();
            foreach (var c in cs)
            {
                DestroyImmediate(c);
            }
        }
    }
}

public class ColliderHelper : EditorWindow
{
    [MenuItem("MPipeline/Collider Helper")]
    private static void CreateWizard()
    {
        ColliderHelper window = (ColliderHelper)GetWindow(typeof(ColliderHelper));
        window.Show();
    }
    [SerializeField]
    private Transform parent;
    private void OnGUI()
    {
        parent = (Transform)EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true);

        Transform[] trans = Selection.GetTransforms(SelectionMode.Unfiltered);
        if (GUILayout.Button("Disable Without"))
        {
            Transform[] ts = parent.GetComponentsInChildren<Transform>(true);
            foreach (var i in ts)
            {
                i.gameObject.SetActive(false);
            }
            foreach (var i in trans)
            {
                i.gameObject.SetActive(true);
            }
            parent.gameObject.SetActive(true);
        }
        if (GUILayout.Button("Enable All"))
        {
            Transform[] ts = parent.GetComponentsInChildren<Transform>(true);
            foreach (var i in ts)
            {
                i.gameObject.SetActive(true);
            }
            parent.gameObject.SetActive(true);
        }
    }
}
public class TransformShader : EditorWindow
{
    [MenuItem("MPipeline/Transform Shader")]
    private static void CreateWizard()
    {
        TransformShader window = (TransformShader)GetWindow(typeof(TransformShader));
        window.Show();
    }
    public enum LightingModelType
    {
        Unlit = 0, DefaultLit = 1, SkinLit = 2, ClothLit = 3, ClearCoat = 4
    }
    public static void SetMat(Material targetMat)
    {
        bool useMotionVector = targetMat.GetShaderPassEnabled("MotionVector");
        bool targetMatEnabled = targetMat.IsKeywordEnabled("CUT_OFF");
        bool targetUseDecal = targetMat.IsKeywordEnabled("USE_DECAL");
        bool targetUseTessellation = targetMat.IsKeywordEnabled("USE_TESSELLATION");
        LightingModelType currentType = (LightingModelType)targetMat.GetInt("_LightingModel");
        
        if (targetUseTessellation)
        {
            targetMat.EnableKeyword("USE_TESSELLATION");
        }
        else
        {
            targetMat.DisableKeyword("USE_TESSELLATION");
        }
        if (currentType != LightingModelType.Unlit)
        {
            targetMat.EnableKeyword("LIT_ENABLE");
        }
        else
        {
            targetMat.DisableKeyword("LIT_ENABLE");
        }
        switch (currentType)
        {
            case LightingModelType.DefaultLit:
                targetMat.EnableKeyword("DEFAULT_LIT");
                targetMat.DisableKeyword("SKIN_LIT");
                targetMat.DisableKeyword("CLOTH_LIT");
                targetMat.DisableKeyword("CLEARCOAT_LIT");
                break;
            case LightingModelType.SkinLit:
                targetMat.DisableKeyword("DEFAULT_LIT");
                targetMat.EnableKeyword("SKIN_LIT");
                targetMat.DisableKeyword("CLOTH_LIT");
                targetMat.DisableKeyword("CLEARCOAT_LIT");
                break;
            case LightingModelType.ClothLit:
                targetMat.DisableKeyword("DEFAULT_LIT");
                targetMat.DisableKeyword("SKIN_LIT");
                targetMat.EnableKeyword("CLOTH_LIT");
                targetMat.DisableKeyword("CLEARCOAT_LIT");
                break;
            case LightingModelType.ClearCoat:
                targetMat.DisableKeyword("DEFAULT_LIT");
                targetMat.DisableKeyword("SKIN_LIT");
                targetMat.DisableKeyword("CLOTH_LIT");
                targetMat.EnableKeyword("CLEARCOAT_LIT");
                break;
            default:
                targetMat.DisableKeyword("DEFAULT_LIT");
                targetMat.DisableKeyword("SKIN_LIT");
                targetMat.DisableKeyword("CLOTH_LIT");
                targetMat.DisableKeyword("CLEARCOAT_LIT");
                break;
        }
        if (!targetMatEnabled)
        {
            if (targetUseTessellation)
                targetMat.renderQueue = 2450;
            else
                targetMat.renderQueue = 2000;
        }
        else
        {
            targetMat.renderQueue = 2451;
        }
        if (targetUseTessellation)
        {
            targetMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Less);
            targetMat.SetInt("_ZWrite", 1);
            targetMat.SetShaderPassEnabled("Depth", false);
        }
        else
        {
            targetMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Equal);
            targetMat.SetInt("_ZWrite", 0);
            targetMat.SetShaderPassEnabled("Depth", true);
        }
        if (targetUseDecal)
        {
            targetMat.EnableKeyword("USE_DECAL");
        }
        else
        {
            targetMat.DisableKeyword("USE_DECAL");
        }
        if (targetMat.GetTexture("_DetailAlbedo") == null && targetMat.GetTexture("_DetailNormal") == null)
        {
            targetMat.DisableKeyword("DETAIL_ON");
        }
        else
        {
            targetMat.EnableKeyword("DETAIL_ON");
        }
    }
    private void Execute(Action<Material, MeshRenderer> func)
    {
        var lights = FindObjectsOfType<MeshRenderer>();
        Dictionary<Material, MeshRenderer> allMats = new Dictionary<Material, MeshRenderer>();
        foreach (var i in lights)
        {
            var mats = i.sharedMaterials;
            foreach (var j in mats)
            {
                if (!j) continue;
                if (allMats.ContainsKey(j))
                {
                    if (i.lightmapIndex >= 0) allMats[j] = i;
                }
                else
                    allMats[j] = i;
            }
        }

        foreach (var i in allMats.Keys)
        {
            func(i, allMats[i]);
        }
    }
    private void OnGUI()
    {
        Shader defaultShader = Shader.Find("ShouShouPBR");
        Shader srpLightmapShader = Shader.Find("Maxwell/StandardLit(Lightmap)");
        Shader srpNoLightmapShader = Shader.Find("Maxwell/StandardLit(No Lightmap)");
        if (GUILayout.Button("To Built-in Pipeline"))
        {
            Execute((mat, r) =>
            {
                if (mat.shader == srpLightmapShader || mat.shader == srpNoLightmapShader)
                    mat.shader = defaultShader;
            });
        }
        if (GUILayout.Button("To MPipeline"))
        {
            Execute((mat, rend) =>
            {
                if (mat.shader == defaultShader)
                {
                    mat.shader = rend.lightmapIndex >= 0 ? srpLightmapShader : srpNoLightmapShader;
                    SetMat(mat);
                    mat.SetShaderPassEnabled("MotionVector", false);
                }
            });
        }
        if (GUILayout.Button("Check Lightmap"))
        {
            Execute((mat, rend) =>
            {
                if (mat.shader == srpNoLightmapShader)
                {
                    if (rend.lightmapIndex >= 0)
                        mat.shader = srpLightmapShader;
                    SetMat(mat);
                }
            });
        }
    }
}
public class TextureToSMO : ScriptableWizard
{
    [MenuItem("MPipeline/Generate SMO Texture")]
    private static void CreateWizard()
    {
        DisplayWizard<TextureToSMO>("SMO", "Generate");
    }
    public Texture2D roughness;
    public Texture2D metallic;
    public Texture2D occlusion;
    public string path = "Assets/Test.png";
    public Vector2Int textureSize = new Vector2Int(1024, 1024);
    private void OnWizardCreate()
    {
        Material mat = new Material(Shader.Find("Hidden/ToSMO"));
        RenderTexture rt = RenderTexture.GetTemporary(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        mat.SetTexture("_RoughnessTexture", roughness);
        mat.SetTexture("_MetallicTexture", metallic);
        mat.SetTexture("_OcclusionTexture", occlusion);
        Graphics.Blit(null, rt, mat, 0);
        Texture2D tex = new Texture2D(textureSize.x, textureSize.y, TextureFormat.RGBA32, false, true);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, textureSize.x, textureSize.y), 0, 0);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.ReleaseTemporary(rt);
        DestroyImmediate(mat);
        DestroyImmediate(tex);
    }
}
#endif
