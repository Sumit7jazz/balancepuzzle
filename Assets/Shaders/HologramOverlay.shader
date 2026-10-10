Shader "BalancePuzzle/HologramOverlay"
{
    // Calibration hologram: unlit, always on top (COM core sits inside the
    // rock, rings pass behind it). No lighting, no depth test, no culling.
    Properties
    {
        _Color ("Glow Color", Color) = (1, 0.93, 0.72, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
