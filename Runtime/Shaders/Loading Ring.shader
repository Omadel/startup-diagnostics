// Ring that turns clockwise, its head opaque and its tail fading out over a turn, drawn in the disc inscribed in the quad:
// the loading indicator of the launch screen, without texture nor script to turn it. Its pass has no light mode, for the
// built-in render pipeline and the Universal Render Pipeline alike.
Shader "Hidden/Startup Diagnostics/Loading Ring"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Thickness ("Thickness, in fraction of the radius", Range(0.01, 1)) = 0.2
        _TurnsPerSecond ("Turns per second", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Thickness;
            float _TurnsPerSecond;

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 offset : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.offset = input.uv * 2 - 1;
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                // Coverage of the ring, smoothed over a pixel on both edges.
                float radius = length(input.offset);
                float pixel = fwidth(radius);
                float ring = saturate((1 - radius) / pixel + 0.5) * saturate((radius - (1 - _Thickness)) / pixel + 0.5);
                // Fraction of a turn behind the head, clockwise from the top: 1 at the head, 0 a turn behind it.
                float turn = frac(atan2(input.offset.x, input.offset.y) / UNITY_TWO_PI - _Time.y * _TurnsPerSecond);
                return fixed4(_Color.rgb, _Color.a * ring * turn);
            }
            ENDCG
        }
    }
}