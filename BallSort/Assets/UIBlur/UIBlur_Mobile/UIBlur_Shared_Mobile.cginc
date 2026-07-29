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
float _Quality;
float _SampleSpacing;
float _Softness;
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

half4 GrabPixelOffset(sampler2D tex, float4 uv, float4 size, half offsetX, half offsetY)
{
    half4 pixel = tex2Dproj(
        tex,
        UNITY_PROJ_COORD(float4(
            uv.x + size.x * offsetX,
            uv.y + size.y * offsetY,
            uv.z,
            uv.w))
    );
    return half4(pixel.rgb, 1);
}

half4 GetMobileBlurInDir(v2f IN, half4 pixel, sampler2D tex, float4 size, half dirx, half diry)
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

    half radius = clamp(_Radius, 0.0h, 12.0h);
    half spacing = max(_SampleSpacing, 0.5h);
    half soft = max(_Softness, 0.5h);

    half o1 = radius * 0.5h * spacing;
    half o2 = radius * 1.0h * spacing;
    half o3 = radius * 1.5h * spacing;

    half4 sum = 0;
    half total = 0;

    // fast 3-tap
    if (_Quality < 0.5)
    {
        half w0 = 0.5h * soft;
        half w1 = 0.25h;

        sum += GrabPixel(tex, IN.uvgrab) * w0;
        sum += GrabPixelOffset(tex, IN.uvgrab, size,  o1 * dirx,  o1 * diry) * w1;
        sum += GrabPixelOffset(tex, IN.uvgrab, size, -o1 * dirx, -o1 * diry) * w1;
        total = w0 + w1 + w1;
    }
    // balanced 5-tap
    else if (_Quality < 1.5)
    {
        half w0 = 0.30h * soft;
        half w1 = 0.24h;
        half w2 = 0.11h / soft;

        sum += GrabPixel(tex, IN.uvgrab) * w0;
        sum += GrabPixelOffset(tex, IN.uvgrab, size,  o1 * dirx,  o1 * diry) * w1;
        sum += GrabPixelOffset(tex, IN.uvgrab, size, -o1 * dirx, -o1 * diry) * w1;
        sum += GrabPixelOffset(tex, IN.uvgrab, size,  o2 * dirx,  o2 * diry) * w2;
        sum += GrabPixelOffset(tex, IN.uvgrab, size, -o2 * dirx, -o2 * diry) * w2;
        total = w0 + w1 + w1 + w2 + w2;
    }
    // soft 7-tap
    else
    {
        half w0 = 0.22h * soft;
        half w1 = 0.19h;
        half w2 = 0.10h;
        half w3 = 0.05h / soft;

        sum += GrabPixel(tex, IN.uvgrab) * w0;
        sum += GrabPixelOffset(tex, IN.uvgrab, size,  o1 * dirx,  o1 * diry) * w1;
        sum += GrabPixelOffset(tex, IN.uvgrab, size, -o1 * dirx, -o1 * diry) * w1;
        sum += GrabPixelOffset(tex, IN.uvgrab, size,  o2 * dirx,  o2 * diry) * w2;
        sum += GrabPixelOffset(tex, IN.uvgrab, size, -o2 * dirx, -o2 * diry) * w2;
        sum += GrabPixelOffset(tex, IN.uvgrab, size,  o3 * dirx,  o3 * diry) * w3;
        sum += GrabPixelOffset(tex, IN.uvgrab, size, -o3 * dirx, -o3 * diry) * w3;
        total = w0 + w1 + w1 + w2 + w2 + w3 + w3;
    }

    half4 result = sum / max(total, 0.0001h);
    return half4(overlayBlend(result.rgb, color.rgb), result.a * visibility);
}
