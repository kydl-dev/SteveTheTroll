// SteveTheTroll D-020: shell fur for the plush mini trolls (and later the costume). Built-in pipeline surface shader, so it gets the
// game's deferred lighting and shadows. Each shell is one extra draw of the same skinned mesh with its own material (_Shell 0..1):
// the vertex is pushed out along the WORLD normal by _FurLength (metres) * _Shell, strands are cut from a hash of a UV grid
// (_Density cells per UV unit), thinner and fewer toward the tips, darker at the roots (fake occlusion), drooping by _Gravity.
Shader "SteveTheTroll/ShellFur"
{
    Properties
    {
        _MainTex ("Albedo (troll skin)", 2D) = "white" {}
        _Color ("Tint", Color) = (0.55, 0.75, 0.95, 1)
        _TipColor ("Tip tint", Color) = (0.85, 0.92, 1, 1)
        _FurLength ("Fur length (m)", Range(0, 0.05)) = 0.006
        _Density ("Strands per UV unit", Range(10, 2000)) = 400
        _Thickness ("Strand thickness at the root (0-1 of a cell)", Range(0.1, 1.5)) = 0.9
        _Gravity ("Droop (m at the tip)", Range(0, 0.02)) = 0.002
        _Shell ("Shell (0 root .. 1 tip, set per material)", Range(0, 1)) = 0.5
        _Occlusion ("Root darkening", Range(0, 1)) = 0.55
        _Glossiness ("Smoothness", Range(0, 1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        LOD 300
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _TipColor;
        float _FurLength, _Density, _Thickness, _Gravity, _Shell, _Occlusion;
        half _Glossiness;

        struct Input { float2 uv_MainTex; };

        void vert(inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 wn = normalize(UnityObjectToWorldNormal(v.normal));
            wp += wn * (_FurLength * _Shell);
            wp.y -= _Gravity * _Shell * _Shell;
            v.vertex = mul(unity_WorldToObject, float4(wp, 1));
        }

        float hash12(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 g = IN.uv_MainTex * _Density;
            float2 cell = floor(g);
            float h = hash12(cell);                                   // strand length 0..1 of the full fur length
            float2 local = frac(g) - 0.5 + (float2(hash12(cell + 17.0), hash12(cell + 41.0)) - 0.5) * 0.3;
            if (_Shell > 0.001)
            {
                float rel = _Shell / max(h, 0.05);                    // 0 at the root of this strand, 1 at its tip
                clip(1.0 - rel);
                clip((1.0 - rel) * _Thickness * 0.5 - length(local));
            }
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            c.rgb = lerp(c.rgb, c.rgb * _TipColor.rgb * 1.3, _Shell);
            c.rgb *= lerp(1.0 - _Occlusion, 1.0, _Shell);
            o.Albedo = c.rgb;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}
