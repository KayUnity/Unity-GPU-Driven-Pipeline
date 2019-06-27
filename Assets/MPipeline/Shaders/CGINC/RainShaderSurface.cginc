#ifndef __RAINSURFACE_INCLUDE__
#define __RAINSURFACE_INCLUDE__
#define USE_UV4
	struct Input {
			float2 uv_MainTex;
			float2 uv4;
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
		float _RainTilling;
}
		sampler2D _BumpMap;
		sampler2D _SpecularMap;
		sampler2D _MainTex; 
		sampler2D _DetailAlbedo; 
		sampler2D _DetailNormal;
		sampler2D _EmissionMap;
		sampler2D _RainTexture;
		sampler2D _PreIntDefault;
		sampler2D _RainRipple;

		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			// Albedo comes from a texture tinted by color
			float2 uv = IN.uv_MainTex;// - parallax_mapping(IN.uv_MainTex,IN.viewDir);
			float2 detailUV = TRANSFORM_TEX(uv, _DetailAlbedo);
			uv = TRANSFORM_TEX(uv, _MainTex);
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
			o.Alpha = 1;
			o.Occlusion = lerp(1, spec.b, _Occlusion);
			o.Specular = lerp(_SpecularIntensity, o.Albedo, _MetallicIntensity * spec.g); 
			o.Smoothness = _Glossiness * spec.r;
			o.Emission = _EmissionColor * tex2D(_EmissionMap, uv) * _EmissionMultiplier;
			float rainValue = tex2D(_RainTexture, IN.uv4);
			float3 rainNormal;
			rainNormal.xy = tex2D(_RainRipple, IN.uv4 * _RainTilling);
			rainNormal.z = sqrt(1 - dot(rainNormal.xy, rainNormal.xy));
			o.Normal = normalize(lerp(rainNormal, o.Normal, rainValue));
			o.Specular = lerp(0.8, o.Specular, rainValue);
			o.Smoothness = lerp(1, o.Smoothness, rainValue);
		}

#endif