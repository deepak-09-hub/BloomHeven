static const float MAX_RADIUS = 64;
static const half MAX_KERNEL_SAMPLES = 64;

#include "UnityCG.cginc"
#include "UnityUI.cginc"

struct appdata_t
{
    float4 vertex : POSITION;
    float2 texcoord: TEXCOORD0;
    float4 color : COLOR;
};

struct v2f
{
    float4 vertex : POSITION;
    float4 uvgrab : TEXCOORD0;
    float4 worldpos : TEXCOORD1;
    float2 uvmain : TEXCOORD2;
    float4 color : COLOR;
};

sampler2D _MainTex;
float4 _MainTex_ST;

v2f vert(appdata_t v)
{
    v2f OUT;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    OUT.worldpos = v.vertex;
    OUT.vertex = UnityObjectToClipPos(v.vertex);

#if UNITY_UV_STARTS_AT_TOP
    float scale = -1.0;
#else
    float scale = 1.0;
#endif

    OUT.uvgrab.xy = (float2(OUT.vertex.x, OUT.vertex.y * scale) + OUT.vertex.w) * 0.5;
    OUT.uvgrab.zw = OUT.vertex.zw;
    OUT.uvmain = TRANSFORM_TEX(v.texcoord, _MainTex);
    OUT.color = v.color;
    return OUT;
}

float4 _OverlayColor;
float _Radius;
float _BlurAlgorithm;
float _SampleStep;
float _Softness;
float _CenterWeight;
float4 _ClipRect;

half4 layerBlend(half4 back, half4 front)
{
    half a0 = front.a;
    half a1 = back.a;
    half a01 = (1 - a0) * a1 + a0;

    return half4(
        ((1 - a0) * a1 * back.r + a0 * front.r) / max(a01, 1e-5),
        ((1 - a0) * a1 * back.g + a0 * front.g) / max(a01, 1e-5),
        ((1 - a0) * a1 * back.b + a0 * front.b) / max(a01, 1e-5),
        a01);
}

#define BLEND_OVERLAY(a, b) b <= 0.5 ? (2*b)*a : (1 - (1-2*(b-0.5)) * (1-a))
half3 overlayBlend(half3 back, half3 front)
{
    return half3(
        BLEND_OVERLAY(back.r, front.r),
        BLEND_OVERLAY(back.g, front.g),
        BLEND_OVERLAY(back.b, front.b)
    );
}

half4 GrabPixel(sampler2D tex, float4 uv)
{
    half4 pixel = tex2Dproj(tex, UNITY_PROJ_COORD(uv));
    return half4(pixel.rgb, 1);
}

half4 GrabPixelXY(sampler2D tex, float4 uv, float4 size, half kernelx, half kernely)
{
    half4 pixel = tex2Dproj(
        tex,
        UNITY_PROJ_COORD(float4(
            uv.x + size.x * kernelx,
            uv.y + size.y * kernely,
            uv.z,
            uv.w))
    );
    return half4(pixel.rgb, 1);
}

half ComputeKernelWeight(half normalizedDistance)
{
    half softness = max(_Softness, 0.0001);

    // 0 = Box, 1 = Gaussian, 2 = SoftGaussian, 3 = Tent
    if (_BlurAlgorithm < 0.5)
    {
        return 1;
    }
    else if (_BlurAlgorithm < 1.5)
    {
        // Standard Gaussian-ish falloff
        half sigma = max(0.18h, 0.35h / softness);
        return exp(-(normalizedDistance * normalizedDistance) / (2.0h * sigma * sigma));
    }
    else if (_BlurAlgorithm < 2.5)
    {
        // Softer / creamier falloff with a wider shoulder
        half sigma = max(0.22h, 0.55h / softness);
        return exp(-(normalizedDistance * normalizedDistance) / (2.0h * sigma * sigma));
    }
    else
    {
        // Tent / linear falloff
        return saturate(1.0h - pow(normalizedDistance, softness));
    }
}

half4 GetBlurInDir(v2f IN, half4 pixel, sampler2D tex, float4 size, half dirx, half diry)
{
#ifdef UNITY_COLORSPACE_GAMMA
    float4 color = _OverlayColor;
#else
    float4 color = float4(LinearToGammaSpace(_OverlayColor.rgb), _OverlayColor.a);
#endif

#if IS_BLUR_ALPHA_MASKED
    float visibility = color.a * pixel.a;
#else
    float visibility = color.a;
#endif

    visibility *= UnityGet2DClipping(IN.worldpos.xy, _ClipRect);

    half radius = clamp(_Radius, 0, MAX_RADIUS);
    half stepSize = max(_SampleStep, 0.5);

    half4 sum = 0;
    half totalWeight = 0;

    half centerWeight = max(_CenterWeight, 0.001);
    sum += GrabPixel(tex, IN.uvgrab) * centerWeight;
    totalWeight += centerWeight;

    [loop]
    for (half range = stepSize; range <= radius && range <= MAX_KERNEL_SAMPLES; range += stepSize)
    {
        half normalizedDistance = (radius > 0.0001h) ? (range / radius) : 0;
        half weight = ComputeKernelWeight(normalizedDistance);

        sum += GrabPixelXY(tex, IN.uvgrab, size, range * dirx, range * diry) * weight;
        sum += GrabPixelXY(tex, IN.uvgrab, size, -range * dirx, -range * diry) * weight;
        totalWeight += weight * 2;
    }

    half4 result = sum / max(totalWeight, 0.0001h);
    return half4(overlayBlend(result.rgb, color.rgb), result.a * visibility);
}
