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
}
