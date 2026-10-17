Shader "LastSoldier/CrimsonPalette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Water ("Water", Float) = 0
        [MaterialToggle] _Portal ("Recolor portal glow only", Float) = 0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex CrimsonVert
            #pragma fragment CrimsonFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float _Intensity, _Water, _Portal;
            struct CrimsonData
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 world : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            CrimsonData CrimsonVert(appdata_t input)
            {
                v2f sprite = SpriteVert(input);
                CrimsonData output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = sprite.vertex;
                output.color = sprite.color;
                output.texcoord = sprite.texcoord;
                output.world = mul(unity_ObjectToWorld, input.vertex).xy;
                return output;
            }
            fixed4 CrimsonFrag(CrimsonData input) : SV_Target
            {
                fixed4 source = SampleSpriteTexture(input.texcoord);
                float light = dot(source.rgb, float3(.24, .61, .15));
                float3 terrain = lerp(float3(.085,.012,.022), float3(.69,.065,.08), light);
                float ripple = step(.86, frac(input.world.y * .55 + sin(input.world.x * 1.8) * .08 + _Time.y * .15));
                float3 water = float3(.09,.008,.025) + ripple * float3(.04,.01,.008);
                // Portal frames combine neutral stone and green magic in one
                // sprite. Swap only green-dominant pixels, preserving the arch.
                float glow = step(.02, source.g - max(source.r, source.b));
                float3 portal = lerp(source.rgb, float3(source.g, source.r * .45, source.b * .25), glow);
                source.rgb = lerp(lerp(terrain, water, _Water) * _Intensity, portal, _Portal);
                source *= input.color;
                source.rgb *= source.a;
                return source;
            }
            ENDCG
        }
    }
}
