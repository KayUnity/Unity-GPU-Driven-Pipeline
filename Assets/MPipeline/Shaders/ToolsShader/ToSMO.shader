﻿Shader "Hidden/ToSMO"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            sampler2D _RoughnessTexture;
            sampler2D _MetallicTexture;
            sampler2D _OcclusionTexture;
            float3 frag (v2f i) : SV_Target
            {
                float smooth = 1 - tex2D(_RoughnessTexture, i.uv).x;
                float meta = tex2D(_MetallicTexture, i.uv).x;
                float occ = tex2D(_OcclusionTexture, i.uv).x;
                return float3(smooth, meta, occ);
            }
            ENDCG
        }
    }
}
