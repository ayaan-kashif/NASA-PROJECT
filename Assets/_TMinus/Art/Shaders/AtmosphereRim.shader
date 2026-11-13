// Thin blue atmosphere glow around the planet: additive, brightest at the limb (Fresnel) and on the day side.
// Unlit with no LightMode tag, so URP draws it in its default unlit pass.
Shader "TMinus/AtmosphereRim"
{
    Properties
    {
        _Color ("Glow colour", Color) = (0.35, 0.6, 1, 1)
        _Power ("Rim power", Range(0.5, 12)) = 4
        _Intensity ("Intensity", Range(0, 8)) = 2.2
        _SunDir ("Sun direction (world)", Vector) = (0, 1, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color; float _Power, _Intensity; float4 _SunDir;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; };

            v2f vert (appdata_base i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                float3 w = mul(unity_ObjectToWorld, i.vertex).xyz;
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = normalize(_WorldSpaceCameraPos - w);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float rim = pow(saturate(1 - dot(n, normalize(i.v))), _Power);
                float day = saturate(dot(n, normalize(_SunDir.xyz)) * 0.8 + 0.35);
                return _Color * rim * day * _Intensity;
            }
            ENDCG
        }
    }
}
