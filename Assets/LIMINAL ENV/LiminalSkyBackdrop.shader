Shader "Liminal/SkyBackdrop"
{
    // skyPS.png is a flat sky study: blue gradient with a single cloud sitting just above
    // the bottom edge. A latitude-longitude panorama would push that far below the horizon,
    // so this maps the image bottom to the horizon and lets _VerticalPower lift the cloud
    // into view, with _Rotation placing it behind the door.
    Properties
    {
        _MainTex ("Sky", 2D) = "white" {}
        _Rotation ("Rotation", Float) = 0
        _VerticalPower ("Vertical Power", Float) = 1
        _Tint ("Tint", Color) = (1,1,1,1)
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

            sampler2D _MainTex;
            float _Rotation;
            float _VerticalPower;
            float4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
            };

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
                float3 dir = normalize(i.dir);
                float u = atan2(dir.z, dir.x) * 0.15915494 + 0.5 + _Rotation / 360.0;
                float elevation = asin(clamp(dir.y, -1.0, 1.0)) * 0.63661977;
                float v = pow(saturate(elevation), max(_VerticalPower, 0.01));
                fixed4 col = tex2D(_MainTex, float2(frac(u), v));
                return col * _Tint;
            }
            ENDCG
        }
    }
    Fallback Off
}
