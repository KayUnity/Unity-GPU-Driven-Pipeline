#ifndef __STANDARDSURFACE_INCLUDE__
#define __STANDARDSURFACE_INCLUDE__

	struct Input {
			float2 uv_MainTex;
			float3 viewDir;
		};
cbuffer UnityPerMaterial
{
    float _SpecularIntensity;
		float _MetallicIntensity;
    float4 _EmissionColor;
		float _Occlusion;
		float4 _MainTex_ST;
		float4 _DetailAlbedo_ST;
		float _Glossiness;
		float4 _Color;
		float _EmissionMultiplier;
		float _Cutoff;
		float _ClearCoatSmoothness;
		float _ClearCoat;
		float _MinDist;
		float _MaxDist;
		float _Tessellation;
		float _HeightmapIntensity;
		float _NormalIntensity;
}

		sampler2D _BumpMap;
		sampler2D _SpecularMap;
		sampler2D _MainTex; 
		sampler2D _DetailAlbedo; 
		sampler2D _DetailNormal;
		sampler2D _EmissionMap;
		sampler2D _PreIntDefault;
		Texture2D<float> _HeightMap; SamplerState sampler_HeightMap;

		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			float2 uv = IN.uv_MainTex;// - parallax_mapping(IN.uv_MainTex,IN.viewDir);
			uv = TRANSFORM_TEX(uv, _MainTex);
#if LIT_ENABLE
			// Albedo comes from a texture tinted by color
			float2 detailUV = TRANSFORM_TEX(uv, _DetailAlbedo);
			float4 spec = tex2D(_SpecularMap,uv);
			float4 c = tex2D (_MainTex, uv);
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
			o.Normal.xy *= _NormalIntensity;
			o.Alpha = 1;
			o.Occlusion = lerp(1, spec.b, _Occlusion);
			o.Specular = lerp(_SpecularIntensity, o.Albedo, _MetallicIntensity * spec.g); 
			o.Smoothness = _Glossiness * spec.r;
			#else
			o = (SurfaceOutputStandardSpecular)0;
#endif
			o.Emission = _EmissionColor * tex2D(_EmissionMap, uv) * _EmissionMultiplier;
		}


void VertexOffset(inout float4 vertex, float3 normal, float2 uv)
{
	#ifdef USE_TESSELLATION
	vertex.xyz += _HeightMap.SampleLevel(sampler_HeightMap, uv, 0) * normal * _HeightmapIntensity;
	#endif
}

#endif