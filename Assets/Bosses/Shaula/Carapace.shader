Shader "LastSoldier/ShaulaCarapace"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Rage ("Crimson phase", Range(0,1)) = 0
        _Flash ("Hit flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Rage, _Flash;
            output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(output i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                // Discard transparent matte from generated art without modifying its pixels.
                clip(c.a-.45);
                float light=max(c.r,max(c.g,c.b));
                float shell=step(.008,light)*(1-smoothstep(.26,.5,light));
                fixed3 red=fixed3(.16+light*.95,.025+light*.16,.035+light*.22);
                c.rgb=lerp(c.rgb,red,shell*_Rage);
                c.rgb=lerp(c.rgb,fixed3(1,.97,.82),_Flash*.75);
                return fixed4(c.rgb*i.color.rgb,i.color.a);
            }
            ENDCG
        }
    }
}
