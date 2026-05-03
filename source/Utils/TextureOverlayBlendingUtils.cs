using OpenTK.Mathematics;
using System.Runtime.CompilerServices;
using Vintagestory.API.MathTools;

namespace PlayerModelLib;

public static class TextureOverlayBlendingUtils
{
    public static void Blend(EnumTextureOverlayMode mode, int[] basePixels, int[] overlayPixels, Vector2i offset, Vector2i baseSize, Vector2i overlaySize)
    {
        int startX = Math.Max(0, offset.X);
        int startY = Math.Max(0, offset.Y);
        int endX = Math.Min(baseSize.X, offset.X + overlaySize.X);
        int endY = Math.Min(baseSize.Y, offset.Y + overlaySize.Y);

        for (int baseY = startY; baseY < endY; baseY++)
        {
            for (int baseX = startX; baseX < endX; baseX++)
            {
                int baseIndex = baseY * baseSize.X + baseX;
                int overlayX = baseX - offset.X;
                int overlayY = baseY - offset.Y;
                int overlayIndex = overlayY * overlaySize.X + overlayX;

                basePixels[baseIndex] = mode switch
                {
                    EnumTextureOverlayMode.Replace => overlayPixels[overlayIndex],
                    EnumTextureOverlayMode.Normal => NormalBlend(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.AlphaMask => AlphaMask(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.AlphaMaskBlackAndWhite => AlphaMaskBlackAndWhite(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.AlphaMaskBlackAndWhiteInverted => AlphaMaskBlackAndWhiteInverted(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.Darken => ColorBlend.Darken(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.Lighten => ColorBlend.Lighten(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.Multiply => ColorBlend.Multiply(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.Screen => ColorBlend.Screen(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.ColorDodge => ColorBlend.ColorDodge(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.ColorBurn => ColorBlend.ColorBurn(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.Overlay => ColorBlend.Overlay(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.OverlayCutout => ColorBlend.OverlayCutout(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.HueBlend => HueBlend(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.SaturationBlend => SaturationBlend(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    EnumTextureOverlayMode.LuminosityBlend => LuminosityBlend(basePixels[baseIndex], overlayPixels[overlayIndex]),
                    _ => basePixels[baseIndex]
                };
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NormalBlend(int baseColor, int overlayColor)
    {
        int overlayAlpha = (overlayColor >> 24) & 0xFF;

        if (overlayAlpha == 0) return baseColor;

        if (overlayAlpha == 255) return overlayColor;

        int baseAlpha = (baseColor >> 24) & 0xFF;

        int baseR = (baseColor >> 16) & 0xFF;
        int baseG = (baseColor >> 8) & 0xFF;
        int baseB = baseColor & 0xFF;

        int overlayR = (overlayColor >> 16) & 0xFF;
        int overlayG = (overlayColor >> 8) & 0xFF;
        int overlayB = overlayColor & 0xFF;

        int inverseOverlayAlpha = 255 - overlayAlpha;
        int outAlpha = overlayAlpha + (baseAlpha * inverseOverlayAlpha) / 255;

        if (outAlpha == 0) return 0;

        int outR = (overlayR * overlayAlpha + baseR * baseAlpha * inverseOverlayAlpha / 255) / outAlpha;
        int outG = (overlayG * overlayAlpha + baseG * baseAlpha * inverseOverlayAlpha / 255) / outAlpha;
        int outB = (overlayB * overlayAlpha + baseB * baseAlpha * inverseOverlayAlpha / 255) / outAlpha;

        return (outAlpha << 24) | (outR << 16) | (outG << 8) | outB;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlphaMaskBlackAndWhite(int baseColor, int overlayColor)
    {
        int overlayR = (overlayColor >> 16) & 0xFF;
        int overlayG = (overlayColor >> 8) & 0xFF;
        int overlayB = overlayColor & 0xFF;

        int luminance = (overlayR * 2126 + overlayG * 7152 + overlayB * 722) / 10000;

        int baseAlpha = (baseColor >> 24) & 0xFF;
        int newAlpha = (baseAlpha * luminance) / 255;

        return (baseColor & 0x00FFFFFF) | (newAlpha << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlphaMaskBlackAndWhiteInverted(int baseColor, int overlayColor)
    {
        int overlayR = (overlayColor >> 16) & 0xFF;
        int overlayG = (overlayColor >> 8) & 0xFF;
        int overlayB = overlayColor & 0xFF;

        int luminance = (overlayR * 2126 + overlayG * 7152 + overlayB * 722) / 10000;

        int baseAlpha = (baseColor >> 24) & 0xFF;
        int newAlpha = (baseAlpha * (1 - luminance)) / 255;

        return (baseColor & 0x00FFFFFF) | (newAlpha << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlphaMask(int baseColor, int overlayColor)
    {
        int baseAlpha = (baseColor >> 24) & 0xFF;
        int overlayAlpha = (overlayColor >> 24) & 0xFF;
        int newAlpha = (baseAlpha * overlayAlpha) / 255;
        return (baseColor & 0x00FFFFFF) | (newAlpha << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HueBlend(int baseColor, int overlayColor)
    {
        int overlayAlpha = (overlayColor >> 24) & 0xFF;
        if (overlayAlpha == 0) return baseColor;

        int baseR = (baseColor >> 16) & 0xFF;
        int baseG = (baseColor >> 8) & 0xFF;
        int baseB = baseColor & 0xFF;

        int overlayR = (overlayColor >> 16) & 0xFF;
        int overlayG = (overlayColor >> 8) & 0xFF;
        int overlayB = overlayColor & 0xFF;

        RgbToHsl(baseR, baseG, baseB, out float baseH, out float baseS, out float baseL);
        RgbToHsl(overlayR, overlayG, overlayB, out float overlayH, out float overlayS, out float overlayL);
        HslToRgb(overlayH, baseS, baseL, out int outR, out int outG, out int outB);

        return ComposeWithAlpha(baseColor, overlayColor, baseR, baseG, baseB, outR, outG, outB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SaturationBlend(int baseColor, int overlayColor)
    {
        int overlayAlpha = (overlayColor >> 24) & 0xFF;
        if (overlayAlpha == 0) return baseColor;

        int baseR = (baseColor >> 16) & 0xFF;
        int baseG = (baseColor >> 8) & 0xFF;
        int baseB = baseColor & 0xFF;

        int overlayR = (overlayColor >> 16) & 0xFF;
        int overlayG = (overlayColor >> 8) & 0xFF;
        int overlayB = overlayColor & 0xFF;

        RgbToHsl(baseR, baseG, baseB, out float baseH, out float baseS, out float baseL);
        RgbToHsl(overlayR, overlayG, overlayB, out float overlayH, out float overlayS, out float overlayL);
        HslToRgb(baseH, overlayS, baseL, out int outR, out int outG, out int outB);

        return ComposeWithAlpha(baseColor, overlayColor, baseR, baseG, baseB, outR, outG, outB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LuminosityBlend(int baseColor, int overlayColor)
    {
        int overlayAlpha = (overlayColor >> 24) & 0xFF;
        if (overlayAlpha == 0) return baseColor;

        int baseR = (baseColor >> 16) & 0xFF;
        int baseG = (baseColor >> 8) & 0xFF;
        int baseB = baseColor & 0xFF;

        int overlayR = (overlayColor >> 16) & 0xFF;
        int overlayG = (overlayColor >> 8) & 0xFF;
        int overlayB = overlayColor & 0xFF;

        RgbToHsl(baseR, baseG, baseB, out float baseH, out float baseS, out float baseL);
        RgbToHsl(overlayR, overlayG, overlayB, out float overlayH, out float overlayS, out float overlayL);
        HslToRgb(baseH, baseS, overlayL, out int outR, out int outG, out int outB);

        return ComposeWithAlpha(baseColor, overlayColor, baseR, baseG, baseB, outR, outG, outB);
    }





    private static void RgbToHsl(int r, int g, int b, out float h, out float s, out float l)
    {
        float rf = r / 255f;
        float gf = g / 255f;
        float bf = b / 255f;

        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float delta = max - min;

        l = (max + min) / 2f;

        if (delta == 0f)
        {
            h = 0f;
            s = 0f;
            return;
        }

        s = l < 0.5f
            ? delta / (max + min)
            : delta / (2f - max - min);

        if (max == rf)
            h = ((gf - bf) / delta + (gf < bf ? 6f : 0f)) / 6f;
        else if (max == gf)
            h = ((bf - rf) / delta + 2f) / 6f;
        else
            h = ((rf - gf) / delta + 4f) / 6f;
    }

    private static void HslToRgb(float h, float s, float l, out int r, out int g, out int b)
    {
        if (s == 0f)
        {
            int gray = (int)(l * 255f);
            r = g = b = gray;
            return;
        }

        float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
        float p = 2f * l - q;

        r = (int)(HueToRgb(p, q, h + 1f / 3f) * 255f);
        g = (int)(HueToRgb(p, q, h) * 255f);
        b = (int)(HueToRgb(p, q, h - 1f / 3f) * 255f);
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;

        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;

        return p;
    }

    private static int ComposeWithAlpha(int baseColor, int overlayColor, int baseR, int baseG, int baseB, int overlayR, int overlayG, int overlayB)
    {
        int baseAlpha = (baseColor >> 24) & 0xFF;
        int overlayAlpha = (overlayColor >> 24) & 0xFF;

        if (overlayAlpha == 0) return baseColor;

        int inverseOverlayAlpha = 255 - overlayAlpha;
        int outAlpha = overlayAlpha + (baseAlpha * inverseOverlayAlpha) / 255;

        if (outAlpha == 0) return 0;

        int outR = (overlayR * overlayAlpha + baseR * baseAlpha * inverseOverlayAlpha / 255) / outAlpha;
        int outG = (overlayG * overlayAlpha + baseG * baseAlpha * inverseOverlayAlpha / 255) / outAlpha;
        int outB = (overlayB * overlayAlpha + baseB * baseAlpha * inverseOverlayAlpha / 255) / outAlpha;

        outR = Math.Clamp(outR, 0, 255);
        outG = Math.Clamp(outG, 0, 255);
        outB = Math.Clamp(outB, 0, 255);

        return (outAlpha << 24) | (outR << 16) | (outG << 8) | outB;
    }
}