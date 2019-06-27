Shader "Unlit/RainDrop"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_TransparentMap("Transparent Map", 2D) = "white"{}
		_SpecularColor("Specular Color", Color) = (0.3, 0.3, 0.3, 0.3)
		_NormalTex("Normal Texture", 2D) = "bump"{}
		_Width("Width", float) = 1
		_Height("Height", float) = 1
	}
		SubShader
		{
						ZTest less
						ZWrite off
						Cull off
						Blend oneMinusSrcAlpha srcAlpha
			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
	#pragma target 5.0
	#define DEFAULT_LIT
	#include "UnityCG.cginc"
	#include "UnityPBSLighting.cginc"
	#include "CGINC/VoxelLight.cginc"
	#include "CGINC/Shader_Include/Common.hlsl"
	#include "CGINC/Random.cginc"
	#include "CGINC/Shader_Include/BSDF_Library.hlsl"
	#include "CGINC/Shader_Include/AreaLight.hlsl"
	#include "CGINC/Lighting.cginc"
	#include "CGINC/Reflection.cginc"
	#include "CGINC/VolumetricLight.cginc"
	#include "CGINC/Sunlight.cginc"
	#pragma multi_compile __ ENABLE_VOLUMETRIC
	#pragma multi_compile __ ENABLE_REFLECTION
	#pragma multi_compile __ ENABLE_SUN
	#pragma multi_compile __ ENABLE_SUNSHADOW
	#pragma multi_compile __ POINTLIGHT
	#pragma multi_compile __ SPOTLIGHT

cbuffer UnityPerMaterial
{
float4 _Color;
float4 _SpecularColor;
float _Width;
float _Height;
}
sampler2D _TransparentMap;
sampler2D _NormalTex;

	struct v2f {
	  UNITY_POSITION(pos);
	  float3 worldNormal : TEXCOORD1;
	float3 worldPos : TEXCOORD3;
	  float3 screenUV : TEXCOORD4;
	  float2 uv : TEXCOORD2;
	 float3 tangent : TEXCOORD5;
	};
	struct appdata
	{
		float4 vertex : POSITION;
		float3 normal : NORMAL;
	};

	float4x4 _NonJitterTextureVP;
	sampler2D _GrabTexture; float4 _GrabTexture_TexelSize;
	StructuredBuffer<float3> _InstancePos;
	static const float2 posOffset[6] = {float2(-0.5, 0), float2(-0.5, 1), float2(0.5, 1),float2(0.5, 1), float2(0.5, 0), float2(-0.5, 0)};
	static const float2 uv[6] = {float2(0, 0), float2(0, 1), float2(1, 1), float2(1,1), float2(1, 0), float2(0, 0)};
	v2f vert(uint vert : SV_VERTEXID, uint inst : SV_INSTANCEID)
	{
		v2f o;
		float4 worldPos = float4(_InstancePos[inst], 1);
		o.uv = uv[vert];
		worldPos.xyz += posOffset[vert].y * float3(0, _Height, 0);
		float3 horiOffset = normalize(cross(float3(UNITY_MATRIX_V._m20, UNITY_MATRIX_V._m21, UNITY_MATRIX_V._m22), float3(0, 1, 0)));
		worldPos.xyz += posOffset[vert].x * horiOffset * _Width;
		o.pos = mul(_NonJitterTextureVP, worldPos);
		float4 screenUV = ComputeScreenPos(o.pos);
		o.screenUV = screenUV.xyw;
		o.worldPos = worldPos.xyz;
		o.worldNormal = -normalize(cross(horiOffset, float3(0, 1, 0)));
		o.tangent = normalize(cross(o.worldNormal, float3(0, 1, 0)));
		return o;
	}
				void frag(v2f i, out float4 outputColor:SV_TARGET0, out float depth : SV_TARGET1)
				{
					float4 color = _Color;
					float4 specular = _SpecularColor;
					float3 viewDir = normalize(i.worldPos - _WorldSpaceCameraPos);
					float3 tangent = normalize(i.tangent);
					float3 normal = normalize(i.worldNormal);
					float3x3 wdMatrix= float3x3(tangent, float3(0, 1, 0), normal);
					normal = normalize(mul(UnpackNormal(tex2D(_NormalTex, i.uv)), wdMatrix));
					float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normal).xyz;
					float2 screenUV = i.screenUV.xy/i.screenUV.z;
					float linearEyeDepth = LinearEyeDepth(i.pos.z);
					float Roughness = clamp(1 - specular.a, 0.02, 1);
					float4 screenColor = tex2D(_GrabTexture, frac(screenUV + viewNormal.xy * _GrabTexture_TexelSize.xy * 50));
					float3 finalColor = 0;
					#if ENABLE_REFLECTION
					finalColor += CalculateReflection(linearEyeDepth, i.worldPos, viewDir, specular, float4(normal, 1), color.rgb, 1, screenUV);
					#endif
					#if ENABLE_VOLUMETRIC
					float4 fogColor = Fog(linearEyeDepth, screenUV);
					finalColor = lerp(fogColor.rgb, finalColor, fogColor.a);
					#endif
					outputColor = float4(lerp(screenColor, finalColor, color.a), 1 - tex2D(_TransparentMap, i.uv).r);
					depth = i.pos.z;
				}
				ENDCG
			}
		}
}
