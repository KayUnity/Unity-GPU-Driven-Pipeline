
 Shader "Maxwell/StandardLit_VT" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_ClearCoat("Clearcoat", Range(0, 1)) = 0.5
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_ClearCoatSmoothness("Secondary Smoothness", Range(0, 1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_Cutoff("Cut off", Range(0, 1)) = 0
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.3
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1
		_MainTex ("Albedo (RGB)DetailMask(A)", 2D) = "white" {}
		[NoScaleOffset]_BumpMap("Normal Map", 2D) = "bump" {}
		[NoScaleOffset]_SpecularMap("R(Smooth)G(Spec)B(Occ)", 2D) = "white"{}
		_DetailAlbedo("Detail Albedo", 2D) = "white"{}
		[NoScaleOffset]_DetailNormal("Detail Normal", 2D) = "bump"{}
		_EmissionMultiplier("Emission Multiplier", Range(0, 128)) = 1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		[NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white"{}
		[HideInInspector]_LightingModel("lm", Int) = 1
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

	// ------------------------------------------------------------
	// Surface shader code generated out of a CGPROGRAM block:
CGINCLUDE
#pragma shader_feature DETAIL_ON
#pragma target 5.0
#define DECAL

#pragma multi_compile __ CUT_OFF
#pragma multi_compile __ LIT_ENABLE
#pragma multi_compile __ DEFAULT_LIT SKIN_LIT CLOTH_LIT CLEARCOAT_LIT
#pragma multi_compile __ USE_DECAL

//#define MOTION_VECTOR
#include "UnityCG.cginc"
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "CGINC/VoxelLight.cginc"
#include "CGINC/Shader_Include/Common.hlsl"
#include "CGINC/Shader_Include/BSDF_Library.hlsl"
#include "CGINC/Shader_Include/AreaLight.hlsl"
#include "CGINC/Sunlight.cginc"
#include "CGINC/Lighting.cginc"
#include "CGINC/StandardSurface_VT.cginc"
#include "CGINC/MPipeDeferred.cginc"
ENDCG

pass
{
	Stencil
	{
		Ref [_LightingModel]
		WriteMask 15
		Pass replace
		comp always
	}
Name "GBuffer"
Tags {"LightMode" = "GBuffer" "Name" = "GBuffer"}
ZTest Equal
ZWrite off
Cull back
CGPROGRAM
	#pragma multi_compile _ ENABLE_SUN
	#pragma multi_compile _ ENABLE_SUNSHADOW
	#pragma multi_compile _ POINTLIGHT
	#pragma multi_compile _ SPOTLIGHT
#pragma vertex vert_surf
#pragma fragment frag_surf
ENDCG
}
	Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "Shadow"}
			CGPROGRAM
			#pragma vertex vert_shadow
			#pragma fragment frag_shadow
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#pragma multi_compile __ POINT_LIGHT_SHADOW

			ENDCG
		}
		Pass
		{
			Stencil
			{
				Ref 128
				WriteMask 128
				Comp always
				Pass replace
			}
			ZTest Equal
			Cull back
			ZWrite off
			Tags {"LightMode" = "MotionVector"}
			CGPROGRAM
			#pragma vertex vert_mv
			#pragma fragment frag_mv
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles

			ENDCG
		}
		Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "Depth"}
			CGPROGRAM
			#pragma vertex vert_depth
			#pragma fragment frag_depth
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			ENDCG
		}
}
	CustomEditor "ShouShouEditor"
}

