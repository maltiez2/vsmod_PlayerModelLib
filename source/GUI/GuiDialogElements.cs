using Cairo;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public class GuiElementImageExtended : GuiElementTextBase
{
    private readonly AssetLocation imageAsset;
    private readonly float brightness;
    private readonly float alpha;
    private readonly int width;
    private readonly int height;

    public GuiElementImageExtended(ICoreClientAPI capi, ElementBounds bounds, AssetLocation imageAsset, float brightness, float alpha, int width = 0, int height = 0)
        : base(capi, "", null, bounds)
    {
        this.imageAsset = imageAsset;
        this.brightness = brightness;
        this.alpha = alpha;
        this.width = width;
        this.height = height;
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        context.Save();
        ImageSurface imageSurfaceFromAsset = GetImageSurfaceFromAsset(api, imageAsset, mulAlpha: (int)(alpha * 255f), width, height);
        context.Rectangle(Bounds.drawX, Bounds.drawY, Bounds.OuterWidth, Bounds.OuterHeight);
        context.SetSourceSurface(imageSurfaceFromAsset, (int)Bounds.drawX, (int)Bounds.drawY);
        context.FillPreserve();
        if (brightness < 1)
        {
            context.SetSourceRGBA(0.0, 0.0, 0.0, 1f - brightness);
            context.Fill();
        }
        context.Restore();
        imageSurfaceFromAsset.Dispose();
    }

    public unsafe static ImageSurface GetImageSurfaceFromAsset(ICoreClientAPI capi, AssetLocation textureLoc, int mulAlpha = 255, int width = 0, int height = 0)
    {
        byte[] data = capi.Assets.Get(textureLoc.Clone().WithPathPrefixOnce("textures/")).Data;
        BitmapExternal bitmapExternal = capi.Render.BitmapCreateFromPng(data);
        if (mulAlpha != 255)
        {
            bitmapExternal.MulAlpha(mulAlpha);
        }
        if (width > 0 && height > 0)
        {
            SKSamplingOptions options = new(SKFilterMode.Nearest);
            bitmapExternal.bmp = bitmapExternal.bmp.Resize(new SKSizeI(width, height), options);
        }

        ImageSurface imageSurface = new ImageSurface(Format.Argb32, bitmapExternal.Width, bitmapExternal.Height);
        uint* ptr = (uint*)((IntPtr)imageSurface.DataPtr).ToPointer();
        uint* ptr2 = (uint*)((IntPtr)bitmapExternal.PixelsPtrAndLock).ToPointer();
        int num = bitmapExternal.Width * bitmapExternal.Height;
        for (int i = 0; i < num; i++)
        {
            ptr[i] = ptr2[i];
        }

        imageSurface.MarkDirty();
        bitmapExternal.Dispose();
        return imageSurface;
    }
}

public static class GuiElementHelpers
{
    public static GuiComposer AddExtendedImage(this GuiComposer composer, ElementBounds bounds, AssetLocation imageAsset, float brightness = 1f, float alpha = 1f, int width = 0, int height = 0)
    {
        if (!composer.Composed)
        {
            composer.AddStaticElement(new GuiElementImageExtended(composer.Api, bounds, imageAsset, brightness, alpha, width, height));
        }

        return composer;
    }
}
