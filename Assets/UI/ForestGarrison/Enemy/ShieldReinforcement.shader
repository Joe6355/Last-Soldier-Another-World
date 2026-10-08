Shader "LastSoldier/ShieldReinforcement"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShieldColor ("Reinforced Shield", Color) = (1,0.22,0.18,1)
        [PerRendererData] _Reinforced ("Reinforced", Float) = 0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment ReinforcementFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _ShieldColor;
            fixed _Reinforced;

            fixed4 ReinforcementFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord);
                // These brown hues belong only to the wooden shield in all eight existing frames.
                // Keep the bone, metal and black outline pixels unchanged.
                fixed wood = step(c.g * 1.35, c.r) * step(c.b * 1.5, c.g) * step(0.05, c.r);
                c.rgb = lerp(c.rgb, c.r * 1.2 * _ShieldColor.rgb, wood * saturate(_Reinforced));
                c *= IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
