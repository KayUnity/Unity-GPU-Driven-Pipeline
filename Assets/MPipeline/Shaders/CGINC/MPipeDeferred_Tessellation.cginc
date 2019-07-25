#ifndef __MPIPEDEFERRED_INCLUDE__
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#define __MPIPEDEFERRED_INCLUDE__

#define UNITY_PASS_DEFERRED
#include "UnityStandardUtils.cginc"
#include "Lighting.cginc"
#include "DecalShading.cginc"
#include "Shader_Include/ImageBasedLighting.hlsl"

#ifdef UNITY_CAN_COMPILE_TESSELLATION
struct UnityTessellationFactors {
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};
#endif // UNITY_CAN_COMPILE_TESSELLATION
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
inline float3 UnityCalcTriEdgeTessFactors (float3 triVertexFactors)
{
    float3 tess;
    tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
    tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
    tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
    return tess;
}


inline float UnityCalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess)
{
    float3 wpos = mul(unity_ObjectToWorld,vertex).xyz;
    float dist = distance (wpos, _WorldSpaceCameraPos);
    float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
    return f;
}

inline float3 tessDist (float4 v0, float4 v1, float4 v2)
{
    float3 f;
    f.x = UnityCalcDistanceTessFactor (v0,_MinDist,_MaxDist,_Tessellation);
    f.y = UnityCalcDistanceTessFactor (v1,_MinDist,_MaxDist,_Tessellation);
    f.z = UnityCalcDistanceTessFactor (v2,_MinDist,_MaxDist,_Tessellation);
   	return UnityCalcTriEdgeTessFactors (f);

}


float4x4 _LastVp;
float4x4 _NonJitterVP;
float3 _SceneOffset;
///////////////
//Geometry Pass
///////////////
struct InternalTessInterp_appdata_full {
  float4 vertex : INTERNALTESSPOS;
  float4 tangent : TANGENT;
  float3 normal : NORMAL;
  float2 texcoord : TEXCOORD0;
  #if LIGHTMAP_ON
  float2 lightmapUV : TEXCOORD1;
  #endif
  #ifdef USE_UV4
  float2 uv4 : TEXCOORD3;
  #endif
};
struct v2f_surf {
  UNITY_POSITION(pos);
  float2 pack0 : TEXCOORD0; 
  float4 worldTangent : TEXCOORD1;
  float4 worldBinormal : TEXCOORD2;
  float4 worldNormal : TEXCOORD3;
	float3 screenUV : TEXCOORD6;
	#if LIGHTMAP_ON
	float2 lightmapUV : TEXCOORD5;
	#endif
	#ifdef USE_UV4
	float2 uv4 : TEXCOORD7;
	#endif
};
struct appdata
{
	float4 vertex : POSITION;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD0;
	#if LIGHTMAP_ON
	float2 lightmapUV : TEXCOORD1;
	#endif
	#ifdef USE_UV4
	float2 uv4 : TEXCOORD3;
	#endif
};



StructuredBuffer<float3x4> _LastFrameModel;
uint _OffsetIndex;

inline InternalTessInterp_appdata_full tessvert_surf (appdata v) {
  InternalTessInterp_appdata_full o;
  o.vertex = v.vertex;
  o.tangent = v.tangent;
  o.normal = v.normal;
  o.texcoord = v.texcoord;
  #if LIGHTMAP_ON
  o.lightmapUV = v.lightmapUV;
  #endif
  #ifdef USE_UV4
  o.uv4 = v.uv4;
  #endif
  return o;
}

inline UnityTessellationFactors hsconst_surf (InputPatch<InternalTessInterp_appdata_full,3> v) {
  UnityTessellationFactors o;
  float3 tf = (tessDist(v[0].vertex, v[1].vertex, v[2].vertex));
  float3 objCP = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)).xyz;
  bool3 dir = 0 < float3(dot(objCP - v[0].vertex, v[0].normal), dot(objCP - v[1].vertex, v[1].normal), dot(objCP - v[2].vertex, v[2].normal));
  tf = (dir.x + dir.y + dir.z) ? tf : 0;
  o.edge[0] = tf.x;
  o.edge[1] = tf.y;
  o.edge[2] = tf.z;
  o.inside = (tf.x + tf.y + tf.z) * 0.33333333;
  return o;
}

[UNITY_domain("tri")]
[UNITY_partitioning("fractional_odd")]
[UNITY_outputtopology("triangle_cw")]
[UNITY_patchconstantfunc("hsconst_surf")]
[UNITY_outputcontrolpoints(3)]
inline InternalTessInterp_appdata_full hs_surf (InputPatch<InternalTessInterp_appdata_full,3> v, uint id : SV_OutputControlPointID) {
  return v[id];
}

v2f_surf vert_surf (appdata v) 
{
  	v2f_surf o;
  	o.pack0 = v.texcoord;
	  #ifdef USE_UV4
	  o.uv4 = v.uv4;
	  #endif
  	o.pos = UnityObjectToClipPos(v.vertex);
/*		#if UNITY_UV_STARTS_AT_TOP
		o.pos.y = -o.pos.y;
		#endif*/
	  float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
		v.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
  	o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	  v.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
		o.worldNormal =float4(v.normal, worldPos.z);
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
	  o.screenUV = ComputeScreenPos(o.pos).xyw;
		#if LIGHTMAP_ON 
		o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
		#endif
  	return o;
}


[UNITY_domain("tri")]
inline v2f_surf ds_surf (UnityTessellationFactors tessFactors, const OutputPatch<InternalTessInterp_appdata_full,3> vi, float3 bary : SV_DomainLocation) {
  appdata v;

  v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;

  v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
  v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
  v.texcoord = vi[0].texcoord*bary.x + vi[1].texcoord*bary.y + vi[2].texcoord*bary.z;
  #if LIGHTMAP_ON
  v.lightmapUV = vi[0].lightmapUV*bary.x + vi[1].lightmapUV*bary.y + vi[2].lightmapUV*bary.z;
  #endif
  #ifdef USE_UV4
  v.uv4 = vi[0].uv4*bary.x + vi[1].uv4*bary.y + vi[2].uv4*bary.z;
  #endif
  VertexOffset(v.vertex, v.normal, v.texcoord);
  v2f_surf o = vert_surf (v);
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
  #ifdef USE_UV4
  surfIN.uv4 = IN.uv4.xy;
  #endif
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(_WorldSpaceCameraPos - worldPos.xyz);
  SurfaceOutputStandardSpecular o;
  float3x3 wdMatrix= float3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
  surfIN.viewDir = worldViewDir;
  // call surface function
  surf (surfIN, o);
  #if USE_DECAL
  CalculateDecal(screenUV, linearEye, worldPos, o.Albedo, o.Normal);
  #endif
  o.Normal = normalize(mul(o.Normal, wdMatrix));
  outEmission = ProceduralStandardSpecular_Deferred (o, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!

	#if LIT_ENABLE
	#if LIGHTMAP_ON
	outGBuffer2.w = 0;
	UnityGIInput giInput = (UnityGIInput)0;
	giInput.atten = 1;
	giInput.worldPos = worldPos;
	giInput.lightmapUV = float4(IN.lightmapUV, 1, 1);
	UnityGI giResult = UnityGI_Base(giInput, o.Occlusion, o.Normal);
	outEmission.xyz += giResult.indirect.diffuse * outGBuffer0;
  //outEmission.xyz += unity_Lightmap.Sample(samplerunity_Lightmap, IN.lightmapUV).xyz* o.Albedo;
	#endif
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
					outGBuffer1.xyz = preint * multiScatter;
#endif
}


//////////////////
//Motion Vector Pass
//////////////////

			struct tessappdata_mv
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
			};
			struct appdata_mv
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 texcoord : TEXCOORD0;
			};
			struct v2f_mv
			{
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
#if CUT_OFF
				float2 texcoord : TEXCOORD0;
#endif
				float3 nonJitterScreenPos : TEXCOORD1;
				float3 lastScreenPos : TEXCOORD2;
			};
			inline tessappdata_mv tessvert_mv (appdata_mv v) {
				tessappdata_mv o;
				o.vertex = v.vertex;
				o.normal = v.normal;
				o.texcoord = v.texcoord;
				return o;
			}

			inline UnityTessellationFactors hsconst_mv (InputPatch<tessappdata_mv,3> v) {
  			UnityTessellationFactors o;
  			float3 tf = (tessDist(v[0].vertex, v[1].vertex, v[2].vertex));
  			float3 objCP = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)).xyz;
  			bool3 dir = 0 < float3(dot(objCP - v[0].vertex, v[0].normal), dot(objCP - v[1].vertex, v[1].normal), dot(objCP - v[2].vertex, v[2].normal));
  			tf = (dir.x + dir.y + dir.z) ? tf : 0;
  			o.edge[0] = tf.x;
  			o.edge[1] = tf.y;
  			o.edge[2] = tf.z;
  			o.inside = (tf.x + tf.y + tf.z) * 0.33333333;
  			return o;
			}

			[UNITY_domain("tri")]
[UNITY_partitioning("fractional_odd")]
[UNITY_outputtopology("triangle_cw")]
[UNITY_patchconstantfunc("hsconst_mv")]
[UNITY_outputcontrolpoints(3)]
inline tessappdata_mv hs_mv (InputPatch<tessappdata_mv,3> v, uint id : SV_OutputControlPointID) {
  return v[id];
}

			v2f_mv vert_mv (appdata_mv v)
			{
				v2f_mv o;
				o.vertex = UnityObjectToClipPos(v.vertex);
			 	float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.nonJitterScreenPos = ComputeScreenPos(mul(_NonJitterVP, worldPos)).xyw;
				float4 lastWorldPos = float4(mul(_LastFrameModel[_OffsetIndex], v.vertex), 1);
        		o.lastScreenPos = ComputeScreenPos(mul(_LastVp, lastWorldPos)).xyw;
#if CUT_OFF
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
#endif
				return o;
			}

			
[UNITY_domain("tri")]
inline v2f_mv ds_mv (UnityTessellationFactors tessFactors, const OutputPatch<tessappdata_mv,3> vi, float3 bary : SV_DomainLocation) {
  appdata_mv v;

  v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
  v.texcoord = vi[0].texcoord*bary.x + vi[1].texcoord*bary.y + vi[2].texcoord*bary.z;
  v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
  VertexOffset(v.vertex, v.normal, v.texcoord);
  v2f_mv o = vert_mv (v);
  return o;
}

			
			float2 frag_mv (v2f_mv i)  : SV_TARGET
			{
#if CUT_OFF
				float4 c = tex2D(_MainTex, i.texcoord);
				clip(c.a * _Color.a - _Cutoff);
#endif
				float4 velocity = float4(i.nonJitterScreenPos.xy, i.lastScreenPos.xy) / float4(i.nonJitterScreenPos.zz, i.lastScreenPos.zz);
#if UNITY_UV_STARTS_AT_TOP
				return velocity.xw - velocity.zy;
#else
				return velocity.xy - velocity.zw;
#endif

			}
/////////////
//Shadow pass
/////////////
float4x4 _ShadowMapVP;
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

			

			v2f_shadow vert_shadow (appdata_mv v)
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
[UNITY_domain("tri")]
inline v2f_shadow ds_shadow (UnityTessellationFactors tessFactors, const OutputPatch<tessappdata_mv,3> vi, float3 bary : SV_DomainLocation) {
  appdata_mv v;

  v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
  v.texcoord = vi[0].texcoord*bary.x + vi[1].texcoord*bary.y + vi[2].texcoord*bary.z;
  v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
  VertexOffset(v.vertex, v.normal, v.texcoord);
  v2f_shadow o = vert_shadow (v);
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