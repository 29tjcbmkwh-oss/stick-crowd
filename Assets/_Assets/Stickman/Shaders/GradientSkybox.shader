// Two-color vertical gradient skybox. Exists because Skybox/Procedural filters its _SkyTint
// through atmosphere scattering — at any thickness the authored hue shifts (the spec's
// #1E7FE0 rendered teal in captures). This renders the two spec colors exactly as authored.
Shader "Skybox/BvORGradient"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.118, 0.498, 0.878, 1)
        _HorizonColor ("Horizon Color", Color) = (0.498, 0.769, 1, 1)
        _Exponent ("Blend Exponent", Range(0.1, 4)) = 1.4
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            float _Exponent;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 0 at/below the horizon, 1 straight up; below-horizon stays horizon color
                float t = pow(saturate(normalize(i.dir).y), _Exponent);
                return lerp(_HorizonColor, _TopColor, t);
            }
            ENDCG
        }
    }
}
