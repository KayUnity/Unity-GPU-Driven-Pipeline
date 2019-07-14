#ifndef __MPIPEDEFERRED_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __MPIPEDEFERRED_INCLUDE__

#define UNITY_PASS_DEFERRED
#include "UnityStandardUtils.cginc"
#include "Lighting.cginc"
#include "DecalShading.cginc"
#include "Shader_Include/ImageBasedLighting.hlsl"
#include "Terrain.cginc"

#define GetScreenPos(pos) ((float2(pos.x, pos.y) * 0.5) / pos.w + 0.5)

float4 ProceduralStandardSpecular_Deferred (inout SurfaceOutputStandardSpecular s, out float4 outGBuffer0, out float4 outGBuffer1, out float4 outGBuffer2)
{
    // energy conservation
    float oneMinusReflectivity;
    s.Albedo = EnergyConservationBetweenDiffuseAndSpecular (s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);
    // RT0: diffuse color (rgb), occlusion (a) - sRGB rendertarget
    outGBuffer0 = float4(s.Albedo, s.Occlusion);

    // RT1: spec color (rgb), smoothness (a) - sRGB rendertarget
    outGBuffer1 = float4(s.Specular, s.Smoothness);

    // RT2: normal (rgb), --unused, very low precision-- (a)

    outGBuffer2 = float4(s.Normal * 0.5f + 0.5f, 1);


		float4 emission = float4(s.Emission, 1);
    return emission;
}
float4x4 _LastVp;
float4x4 _NonJitterVP;
float3 _SceneOffset;

struct v2f_surf {
  UNITY_POSITION(pos);
  float2 pack0 : TEXCOORD0; 
  float4 worldTangent : TEXCOORD1;
  float4 worldBinormal : TEXCOORD2;
  float4 worldNormal : TEXCOORD3;
	float3 screenUV : TEXCOORD6;
};

StructuredBuffer<float3x4> _LastFrameModel;
uint _OffsetIndex;
v2f_surf vert_surf (uint instanceID : SV_INSTANCEID, uint vertexID : SV_VERTEXID) 
{
	Terrain_Appdata v = GetTerrain(instanceID, vertexID);
  	v2f_surf o;
  	o.pack0 = v.uv;
  	o.pos = mul(UNITY_MATRIX_VP, float4(v.position, 1));
	  const float3 tangent = float3(0,0,1);
	  const float3 normal = float3(0,1,0);
	  const float3 binormal = float3(1, 0, 0);
/*		#if UNITY_UV_STARTS_AT_TOP
		o.pos.y = -o.pos.y;
		#endif*/
	  float3 worldPos = v.position;
  	o.worldTangent = float4( tangent.xyz, worldPos.x);
	o.worldNormal =float4(normal, worldPos.z);
  	o.worldBinormal = float4(binormal, worldPos.y);
	o.screenUV = ComputeScreenPos(o.pos).xyw;

  	return o;
}

void frag_surf (v2f_surf IN,
		out float4 outGBuffer0 : SV_Target0,
    out float4 outGBuffer1 : SV_Target1,
    out float4 outGBuffer2 : SV_Target2,
    out float4 outEmission : SV_Target3
) {
	
  // prepare and unpack data
	float depth = IN.pos.z;
	float linearEye = LinearEyeDepth(depth);
  Input surfIN;
	float2 screenUV = IN.screenUV.xy / IN.screenUV.z;
  surfIN.uv_MainTex = IN.pack0.xy;
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
  SurfaceOutputStandardSpecular o;
  float3x3 wdMatrix= float3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
  // call surface function
  surf (surfIN, o);
  #if USE_DECAL
  CalculateDecal(screenUV, linearEye, worldPos, o.Albedo, o.Normal);
  #endif
  o.Normal = normalize(mul(o.Normal, wdMatrix));
  outEmission = ProceduralStandardSpecular_Deferred (o, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!

	#if LIT_ENABLE
	float Roughness = clamp(1 - outGBuffer1.a, 0.02, 1);
					  float3 multiScatter;
  					float3 preint = PreintegratedDGF_LUT(_PreIntDefault, multiScatter, outGBuffer1.xyz, Roughness, dot(o.Normal, worldViewDir));
					outGBuffer1.xyz *= multiScatter;
	UnityStandardData standardData;
	            standardData.occlusion = outGBuffer0.a;
	            standardData.diffuseColor = outGBuffer0.rgb;
	            standardData.specularColor = outGBuffer1.rgb;
	            standardData.smoothness = outGBuffer1.a;
					
					GeometryBuffer buffer;
					buffer.AlbedoColor = standardData.diffuseColor;
					buffer.SpecularColor = standardData.specularColor;
					buffer.Roughness = Roughness;
#if CLEARCOAT_LIT
					buffer.ClearCoat_MultiScatterEnergy = multiScatter;
					buffer.ClearCoat = _ClearCoat;
					buffer.ClearCoat_Roughness = clamp(1 - _ClearCoatSmoothness, 0.02, 1);
#endif
#if SKIN_LIT
					buffer.Skin_Roughness = clamp(1 - _ClearCoatSmoothness, 0.02, 1);
#endif


#if SPOTLIGHT || POINTLIGHT
                    outEmission.xyz += max(0, CalculateLocalLight(screenUV, float4(worldPos,1 ), linearEye, o.Normal, worldViewDir, buffer));
#endif
#if ENABLE_SUN
#if ENABLE_SUNSHADOW
					outEmission.xyz +=max(0,  CalculateSunLight(o.Normal, depth, float4(worldPos,1 ), worldViewDir, buffer));
#else
					outEmission.xyz +=max(0,  CalculateSunLight_NoShadow(o.Normal, worldViewDir, buffer));
#endif
#endif
					outGBuffer1.xyz *= preint;
#endif
}

/////////////
//Shadow pass
/////////////
float4x4 _ShadowMapVP;
			struct appdata_shadow
			{
				float4 vertex : POSITION;
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};
			struct v2f_shadow
			{
				float4 vertex : SV_POSITION;
				#if POINT_LIGHT_SHADOW
				float3 worldPos : TEXCOORD1;
				#endif
				#if CUT_OFF
				float2 texcoord : TEXCOORD0;
				#endif
			};

			v2f_shadow vert_shadow (appdata_shadow v)
			{
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				v2f_shadow o;
				#if POINT_LIGHT_SHADOW
				o.worldPos = worldPos.xyz;
				#endif
				o.vertex = mul(_ShadowMapVP, worldPos);
				#if CUT_OFF
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				#endif
				return o;
			}

			
			float frag_shadow (v2f_shadow i)  : SV_TARGET
			{
				#if CUT_OFF
				float4 c = tex2D(_MainTex, i.texcoord);
				clip(c.a * _Color.a - _Cutoff);
				#endif
				#if POINT_LIGHT_SHADOW
				return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
				#else
				return i.vertex.z;
				#endif
			}
#endif