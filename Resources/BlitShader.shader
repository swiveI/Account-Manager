Shader "Unlit/BlitShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MipLevel ("Mip Level", float) = 0
        _Left ("Left", float) = 0
        _Right ("Right", float) = 1
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

            sampler2D _MainTex;
            uniform float _MipLevel;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = 0;
                float Alpha = 1 / _MipLevel;

                bool left = i.uv.x < _Left;
                bool right = i.uv.x > _Right;
                bool middle = !left && !right;
                float distanceToEdge; // 0 to 1

                if (left)
                {
                    distanceToEdge = _Left - i.uv.x;
                }
                else if (right)
                {
                    distanceToEdge = i.uv.x - _Right;
                }
                else
                {
                    distanceToEdge = 0;
                }



                for (int mip = 0; mip < _MipLevel; mip++)
                {
                    col += tex2Dlod(_MainTex, float4(i.uv, 0, mip * i.uv.x)) * Alpha;
                }
                col.a = 1;
                return col;
            }
            ENDCG
        }
    }
}
