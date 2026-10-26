// 뽑기 결과 카드 뒤 배경: 중심에서 퍼지는 동심원 파장 + 비틀린 소용돌이 줄무늬를 섞어 3색 팔레트로 흐르듯 순환한다.
// UI/Default와 같은 스텐실·클립 사각형·정점 색 알파를 지원해서 화면 전체 Image/RawImage에 바로 쓸 수 있다.
// 텍스처 없이 sin/atan2 정도만 써서 WebGL 2.0에서 돈다.
// 시간: 결과 화면은 timeScale이 0일 수 있어 스크립트가 _AnimTime(멈추지 않는 시간)을 넘긴다. 0이면 _Time.y를 쓴다.
Shader "ScrapFishing/UI/PsyWaves"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorA ("Color A", Color) = (0.16,0.2,0.31,1)
        _ColorB ("Color B", Color) = (0.35,0.4,0.5,1)
        _ColorC ("Color C", Color) = (0.6,0.64,0.71,1)
        _Speed ("Speed", Float) = 0.6
        _CycleSpeed ("Palette Cycle Speed", Float) = 1
        _Frequency ("Ring Frequency", Float) = 36
        _Swirl ("Swirl Twist", Float) = 6
        _Intensity ("Intensity", Range(0, 1)) = 1
        _CenterDarken ("Center Darken", Range(0, 1)) = 0.35
        _PixelSize ("Pixel Cells Across (0 = off)", Float) = 0
        _WhiteFade ("White Fade (0 = white)", Range(0, 1)) = 1
        _Center ("Center (UV)", Vector) = (0.5,0.5,0,0)
        _Aspect ("Width / Height", Float) = 0.5625
        _AnimTime ("Animation Time", Float) = 0
        _Opaque ("Ignore Vertex Alpha", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            float _Speed;
            float _CycleSpeed;
            float _Frequency;
            float _Swirl;
            float _Intensity;
            float _CenterDarken;
            float _PixelSize;
            float _WhiteFade;
            float4 _Center;
            float _Aspect;
            float _AnimTime;
            float _Opaque;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 3색을 고리 모양으로 이어서(A→B→C→A) 부드럽게 고른다.
            fixed3 Palette(float x)
            {
                x = frac(x) * 3.0;
                fixed3 ab = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.0, 1.0, x));
                fixed3 bc = lerp(_ColorB.rgb, _ColorC.rgb, smoothstep(1.0, 2.0, x));
                fixed3 ca = lerp(_ColorC.rgb, _ColorA.rgb, smoothstep(2.0, 3.0, x));
                return x < 1.0 ? ab : (x < 2.0 ? bc : ca);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                // 픽셀아트 느낌: 가로 _PixelSize칸(세로는 화면 비율대로)으로 UV를 내려 계단 모양을 만든다. 0이면 끈다.
                if (_PixelSize > 1.0)
                {
                    float2 cells = float2(_PixelSize, _PixelSize / max(_Aspect, 0.01));
                    uv = (floor(uv * cells) + 0.5) / cells;
                }

                float t = (_AnimTime > 0.0 ? _AnimTime : _Time.y) * _Speed;
                float2 p = uv - _Center.xy;
                p.x *= _Aspect;

                // 4) 약한 노이즈 일렁임: 서로 다른 sin을 합쳐 거리와 각도를 조금씩 흔든다.
                float wobble = sin(p.x * 11.0 + t * 1.7) * 0.012 + sin(p.y * 13.0 - t * 1.3) * 0.012 + sin((p.x + p.y) * 7.0 + t * 0.9) * 0.008;
                float r = length(p) + wobble;
                float a = atan2(p.y, p.x);

                // 1) 동심원 파장(바깥으로 퍼짐) 2) 각도+거리로 비틀린 소용돌이 줄무늬.
                float rings = sin(r * _Frequency - t * 3.0);
                float swirl = sin(a * 5.0 + r * _Swirl * 4.0 - t * 2.0);
                float v = (rings * 0.55 + swirl * 0.45) * 0.5 + 0.5;

                // 3) 섞은 값으로 3색 팔레트를 흐르듯 순환.
                fixed3 col = Palette(v * 0.85 + r * 0.6 + t * 0.12 * _CycleSpeed);
                fixed3 calm = (_ColorA.rgb + _ColorB.rgb + _ColorC.rgb) / 3.0;
                col = lerp(calm, col, _Intensity);

                // 카드 뒤(중심 가까이)는 어둡게 해서 카드 테두리·이름이 묻히지 않게.
                float darken = (1.0 - smoothstep(0.08, 0.45, length(p))) * _CenterDarken;
                col *= 1.0 - darken;

                // 흰 화면에서 파장으로.
                col = lerp(fixed3(1.0, 1.0, 1.0), col, _WhiteFade);

                fixed4 color = fixed4(col, lerp(IN.color.a, 1.0, _Opaque) * _Color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
