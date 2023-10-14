Shader "Unlit/BlitShader"
{
    Properties
    {
        _Foreground ("Foreground", 2D) = "white" {}
        _Background ("Background", 2D) = "white" {}
        _Bounds ("Bounds", Vector) = (0, 0, 256, 256)
        _MousePos ("Offset", Vector) = (128, 128, 0, 0)
        _MaxOffset ("Max Offset", Float) = 128
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100
        Blend One Zero
        ZWrite Off

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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            uniform sampler2D _Foreground;
            uniform sampler2D _Background;
            uniform float4 _Bounds;
            uniform float2 _MousePos;
            uniform float _MaxOffset;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 windowCenter = _Bounds.xy + (_Bounds.zw / 2);
                float2 offset = _MousePos - windowCenter;
                //Normalize the offset to the UV
                offset /= _Bounds.zw;

                //Sample the textures
                fixed4 foreground, background;
                foreground = tex2Dlod(_Foreground, float4(i.uv, 0, 0));
                background = tex2Dlod(_Background, float4(i.uv + offset, 0, 0));

                //Layer them with alpha
                fixed4 buffer;
                buffer = background;
                buffer = lerp(buffer, foreground, foreground.a);
                
                //Output
                buffer.a = 1;
                return buffer;
            }
            ENDCG
        }
    }
}
