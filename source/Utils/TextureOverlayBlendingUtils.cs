using System.Runtime.CompilerServices;
using Vintagestory.API.MathTools;

namespace PlayerModelLib;

public static class TextureOverlayBlendingUtils
{
    public static void Blend(EnumTextureOverlayMode mode, int[] basePixels, int[] overlayPixels, string? color = null)
    {
        int length = basePixels.Length;
        switch (mode)
        {
            case EnumTextureOverlayMode.Color:
                int rgbaColor = ColorUtil.Hex2Int(color);
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = rgbaColor;
                }
                break;

            case EnumTextureOverlayMode.Replace:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = overlayPixels[pixelIndex];
                }
                break;

            case EnumTextureOverlayMode.Normal:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = NormalBlend(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.AlphaMask:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = AlphaMask(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.AlphaMaskBlackAndWhite:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = AlphaMaskBlackAndWhite(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.AlphaMaskBlackAndWhiteInverted:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = AlphaMaskBlackAndWhiteInverted(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.Darken:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = ColorBlend.Darken(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.Lighten:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = ColorBlend.Lighten(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.Multiply:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = ColorBlend.Multiply(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.Screen:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = ColorBlend.Screen(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.ColorDodge:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = ColorBlend.ColorDodge(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.ColorBurn:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = ColorBlend.ColorBurn(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.Overlay:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = ColorBlend.Overlay(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;

            case EnumTextureOverlayMode.OverlayCutout:
                for (int pixelIndex = 0; pixelIndex < length; pixelIndex++)
                {
                    basePixels[pixelIndex] = ColorBlend.OverlayCutout(basePixels[pixelIndex], overlayPixels[pixelIndex]);
                }
                break;
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
