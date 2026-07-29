Shader "Custom/UIMotionBlur"
{
    Properties
    {
        [Toggle(IS_BLUR_ALPHA_MASKED)] _IsAlphaMasked("Image Alpha Masks Blur", Float) = 1
        [Toggle(IS_SPRITE_VISIBLE)] _IsSpriteVisible("Show Image", Float) = 1

        _BlurLength("Motion Length", Range(0, 64)) = 10
        _Angle("Direction Angle", Range(0, 360)) = 0
        _OverlayColor("Blurred Overlay/Opacity", Color) = (0.5, 0.5, 0.5, 1)

        [Header(Motion Blur Options)]
        [Enum(Centered,0,ForwardTrail,1,BackwardTrail,2)] _BlurMode("Blur Mode", Float) = 0
        [Enum(Box,0,Gaussian,1,Shutter,2)] _WeightMode("Weight Mode", Float) = 1
        _Samples("Samples", Range(3, 32)) = 9
        _SampleSpread("Sample Spread", Range(0.25, 3)) = 1
        _Softness("Softness / Falloff", Range(0.25, 4)) = 1
        _CenterWeight("Center Weight", Range(0, 4)) = 1

        // see Stencil in UI/Default
        [HideInInspector][PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector]_ColorMask ("Color Mask", Float) = 15
        [HideInInspector]_UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    Category
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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

        SubShader
        {
            GrabPass
            {
                Tags
                {
                    "LightMode" = "Always"
                    "Queue" = "Background"
                }
            }

            Pass
            {
                Name "UIMotionBlur"
                Tags{ "LightMode" = "Always" }

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma fragmentoption ARB_precision_hint_fastest
                #pragma multi_compile __ IS_BLUR_ALPHA_MASKED
                #pragma multi_compile __ IS_SPRITE_VISIBLE
                #pragma multi_compile __ UNITY_UI_ALPHACLIP

                #include "UIMotionBlur_Shared.cginc"

                sampler2D _GrabTexture;
                float4 _GrabTexture_TexelSize;

                half4 frag(v2f IN) : COLOR
                {
                    half4 pixel_raw = tex2D(_MainTex, IN.uvmain);

                #if IS_SPRITE_VISIBLE
                    return layerBlend(
                        GetMotionBlur(IN, pixel_raw, _GrabTexture, _GrabTexture_TexelSize),
                        pixel_raw * IN.color
                    );
                #else
                    return GetMotionBlur(IN, pixel_raw, _GrabTexture, _GrabTexture_TexelSize);
                #endif
                }
                ENDCG
            }
        }
    }
    Fallback "UI/Default"
}
