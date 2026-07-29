static const float MAX_LENGTH = 64;
static const half MAX_SAMPLES = 32;

#include "UnityCG.cginc"
#include "UnityUI.cginc"

struct appdata_t
{
    float4 vertex : POSITION;
    float2 texcoord : TEXCOORD0;
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
float _BlurLength;
float _Angle;
float _BlurMode;
float _WeightMode;
float _Samples;
float _SampleSpread;
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

half4 GrabPixelOffset(sampler2D tex, float4 uv, float4 size, float2 pixelOffset)
{
    half4 pixel = tex2Dproj(
        tex,
        UNITY_PROJ_COORD(float4(
            uv.x + size.x * pixelOffset.x,
            uv.y + size.y * pixelOffset.y,
            uv.z,
            uv.w))
    );
    return half4(pixel.rgb, 1);
}

half MotionWeight(half normalizedDistance, half sampleT)
{
    half softness = max(_Softness, 0.0001);

    // 0 = Box, 1 = Gaussian, 2 = Shutter / stronger center & trailing emphasis
    if (_WeightMode < 0.5)
    {
        return 1;
    }
    else if (_WeightMode < 1.5)
    {
        half sigma = max(0.18h, 0.35h / softness);
        return exp(-(normalizedDistance * normalizedDistance) / (2.0h * sigma * sigma));
    }
    else
    {
        half shutter = saturate(1.0h - pow(normalizedDistance, softness));
        return shutter * lerp(1.0h, 1.3h, sampleT);
    }
}

float2 GetMotionDirection()
{
    float angleRad = _Angle * 0.017453292519943295; // degrees to radians
    return float2(cos(angleRad), sin(angleRad));
}

half4 GetMotionBlur(v2f IN, half4 pixel, sampler2D tex, float4 size)
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

    half lengthPx = clamp(_BlurLength, 0, MAX_LENGTH);
    half sampleCount = clamp(round(_Samples), 3, MAX_SAMPLES);
    half spread = max(_SampleSpread, 0.001);
    float2 direction = GetMotionDirection() * spread;

    half4 sum = 0;
    half totalWeight = 0;

    half centerWeight = max(_CenterWeight, 0);
    if (centerWeight > 0)
    {
        sum += GrabPixel(tex, IN.uvgrab) * centerWeight;
        totalWeight += centerWeight;
    }

    if (lengthPx <= 0.0001h)
    {
        half4 result0 = (totalWeight > 0.0001h) ? (sum / totalWeight) : GrabPixel(tex, IN.uvgrab);
        return half4(overlayBlend(result0.rgb, color.rgb), result0.a * visibility);
    }

    half steps = max(sampleCount - 1, 1);

    [loop]
    for (half i = 0; i < MAX_SAMPLES; i++)
    {
        if (i >= sampleCount)
            break;

        half t = (sampleCount <= 1) ? 0 : (i / steps);
        half signedT;

        // 0 = centered, 1 = forward trail, 2 = backward trail
        if (_BlurMode < 0.5)
        {
            signedT = lerp(-1.0h, 1.0h, t);
        }
        else if (_BlurMode < 1.5)
        {
            signedT = t;
        }
        else
        {
            signedT = -t;
        }

        half normalizedDistance = abs(signedT);
        half weight = MotionWeight(normalizedDistance, t);
        float2 offsetPx = direction * (signedT * lengthPx);

        sum += GrabPixelOffset(tex, IN.uvgrab, size, offsetPx) * weight;
        totalWeight += weight;
    }

    half4 result = sum / max(totalWeight, 0.0001h);
    return half4(overlayBlend(result.rgb, color.rgb), result.a * visibility);
}
