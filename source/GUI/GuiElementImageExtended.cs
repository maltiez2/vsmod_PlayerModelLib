using Cairo;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public class GuiElementImageExtended : GuiElementTextBase
{
    private readonly AssetLocation _imageAsset;
    private readonly float _brightness;
    private readonly float _alpha;
    private readonly int _width;
    private readonly int _height;

    public GuiElementImageExtended(ICoreClientAPI capi, ElementBounds bounds, AssetLocation imageAsset, float brightness, float alpha, int width = 0, int height = 0)
        : base(capi, "", null, bounds)
    {
        this._imageAsset = imageAsset;
        this._brightness = brightness;
        this._alpha = alpha;
        this._width = width;
        this._height = height;
    }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        ctx.Save();
        ImageSurface imageSurfaceFromAsset = GetImageSurfaceFromAsset(api, _imageAsset, mulAlpha: (int)(_alpha * 255f), _width, _height);
        ctx.Rectangle(Bounds.drawX, Bounds.drawY, Bounds.OuterWidth, Bounds.OuterHeight);
        ctx.SetSourceSurface(imageSurfaceFromAsset, (int)Bounds.drawX, (int)Bounds.drawY);
        ctx.FillPreserve();
        if (_brightness < 1)
        {
            ctx.SetSourceRGBA(0.0, 0.0, 0.0, 1f - _brightness);
            ctx.Fill();
        }
        ctx.Restore();
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

        ImageSurface imageSurface = new(Format.Argb32, bitmapExternal.Width, bitmapExternal.Height);
        uint* ptr = (uint*)imageSurface.DataPtr.ToPointer();
        uint* ptr2 = (uint*)bitmapExternal.PixelsPtrAndLock.ToPointer();
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
