﻿Shader "ShouShouPBR"
{
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_ClearCoat("Clearcoat", Range(0, 1)) = 0.5
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_ClearCoatSmoothness("Secondary Smoothness", Range(0, 1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_Cutoff("Cut off", Range(0, 1)) = 0
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.04
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1
		_MinDist("Min Tessellation Dist", float) = 20
		_MaxDist("Max Tessellation Dist", float) = 50
		_Tessellation("Tessellation Intensity", Range(1, 63)) = 1
		_HeightmapIntensity("Heightmap Intensity", Range(0, 10)) = 0.1
		_MainTex ("Albedo (RGB)DetailMask(A)", 2D) = "white" {}
		[NoScaleOffset]_BumpMap("Normal Map", 2D) = "bump" {}
		[NoScaleOffset]_SpecularMap("R(Smooth)G(Spec)B(Occ)", 2D) = "white"{}
		[NoScaleOffset]_HeightMap("Height Map", 2D) = "black"{}
		_DetailAlbedo("Detail Albedo", 2D) = "white"{}
		[NoScaleOffset]_DetailNormal("Detail Normal", 2D) = "bump"{}
		_EmissionMultiplier("Emission Multiplier", Range(0, 128)) = 1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		[NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white"{}
		[HideInInspector]_LightingModel("lm", Int) = 1
	}
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf StandardSpecular fullforwardshadows
#pragma shader_feature DETAIL_ON
#pragma multi_compile _ CUT_OFF
#pragma multi_compile __ LIT_ENABLE
#pragma multi_compile __ DEFAULT_LIT SKIN_LIT CLOTH_LIT CLEARCOAT_LIT
#pragma multi_compile __ USE_DECAL
#pragma multi_compile __ USE_TESSELLATION
        #pragma target 5.0
            struct Input
        {
            float2 uv_MainTex;
			float2 uv_DetailAlbedo;
        };
    	float _SpecularIntensity;
		float _MetallicIntensity;
    	float4 _EmissionColor;
		float _Occlusion;
		float _VertexScale;
		float _VertexOffset;
		float _Cutoff;
		float _EmissionMultiplier;
		sampler2D _BumpMap;
		sampler2D _SpecularMap;
		sampler2D _MainTex;
		sampler2D _DetailAlbedo; 
		sampler2D _DetailNormal;
		sampler2D _EmissionMap;

		float _Glossiness;
		float4 _Color;


        void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			// Albedo comes from a texture tinted by color
			float2 uv = IN.uv_MainTex;// - parallax_mapping(IN.uv_MainTex,IN.viewDir);
			float2 detailUV = IN.uv_DetailAlbedo;
			float4 spec = tex2D(_SpecularMap,uv);
			float4 c = tex2D (_MainTex, uv);
			#if CUT_OFF
			clip(c.a * _Color.a - _Cutoff);
			#endif
#if DETAIL_ON
			float3 detailNormal = UnpackNormal(tex2D(_DetailNormal, detailUV));
			float4 detailColor = tex2D(_DetailAlbedo, detailUV);
#endif
			o.Normal = UnpackNormal(tex2D(_BumpMap,uv));
			o.Albedo = c.rgb;
#if DETAIL_ON
			o.Albedo = lerp(detailColor.rgb, o.Albedo, c.a) * _Color.rgb;
			o.Normal = lerp(detailNormal, o.Normal, c.a);
#else
			o.Albedo *= _Color.rgb;
#endif
			o.Alpha = 1;
			o.Occlusion = lerp(1, spec.b, _Occlusion);
			o.Specular = lerp(_SpecularIntensity, o.Albedo, _MetallicIntensity * spec.g); 
			o.Smoothness = _Glossiness * spec.r;
			o.Emission = _EmissionColor * tex2D(_EmissionMap, uv) * _EmissionMultiplier;
		}
        ENDCG
    }
    FallBack "Diffuse"
	CustomEditor "ShouShouEditor"
}
