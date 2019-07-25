using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class ShouShouEditor : ShaderGUI
{
    public enum LightingModelType
    {
        Unlit = 0, DefaultLit = 1, SkinLit = 2, ClothLit = 3, ClearCoat = 4
    }
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material targetMat = materialEditor.target as Material;
        bool useMotionVector = targetMat.GetShaderPassEnabled("MotionVector");
        useMotionVector = EditorGUILayout.Toggle("MotionVector", useMotionVector);
        targetMat.SetShaderPassEnabled("MotionVector", useMotionVector);
        bool targetMatEnabled = targetMat.IsKeywordEnabled("CUT_OFF");
        bool targetUseDecal = targetMat.IsKeywordEnabled("USE_DECAL");
        targetMatEnabled = EditorGUILayout.Toggle("Cut off", targetMatEnabled);
        targetUseDecal = EditorGUILayout.Toggle("Use Decal", targetUseDecal);

        LightingModelType currentType = (LightingModelType)targetMat.GetInt("_LightingModel");
        currentType = (LightingModelType)EditorGUILayout.EnumPopup("Lighting Model", currentType);
        Undo.RecordObject(targetMat, targetMat.name);
        targetMat.SetInt("_LightingModel", (int)currentType);
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
            targetMat.DisableKeyword("CUT_OFF");
            if (targetMat.renderQueue > 2450)
                targetMat.renderQueue = 2000;
        }
        else
        {
            targetMat.EnableKeyword("CUT_OFF");
            if (targetMat.renderQueue < 2451)
                targetMat.renderQueue = 2451;
        }
        if (targetUseDecal)
        {
            targetMat.EnableKeyword("USE_DECAL");
        }
        else
        {
            targetMat.DisableKeyword("USE_DECAL");
        }
        base.OnGUI(materialEditor, properties);
        if (targetMat.GetTexture("_DetailAlbedo") == null && targetMat.GetTexture("_DetailNormal") == null)
        {
            targetMat.DisableKeyword("DETAIL_ON");
        }
        else
        {
            targetMat.EnableKeyword("DETAIL_ON");
        }
    }
}
