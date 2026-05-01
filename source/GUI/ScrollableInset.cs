using Cairo;
using Vintagestory.API.Client;

namespace PlayerModelLib;

public class GuiElementScrollableInset : GuiElement
{
    public GuiElementScrollableInset(ICoreClientAPI capi, ElementBounds bounds, int depth, float brightness)
        : base(capi, bounds)
    {
        Depth = depth;
        Brightness = brightness;
    }

    public override bool Focusable => false;


    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        GenerateTexture();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (InsetTexture != null && InsetTexture.TextureId != 0)
        {
            api?.Render.Render2DTexture(
                InsetTexture.TextureId,
                Bounds
            );
        }
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        // ignore, to let other elements work as intened
    }

    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        // ignore, to let other elements work as intened
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        // ignore, to let other elements work as intened
    }

    public override bool IsPositionInside(int posX, int posY)
    {
        // ignore, to let other elements work as intened
        return false;
    }

    public override void Dispose()
    {
        base.Dispose();
        InsetTexture?.Dispose();
        InsetTexture = null;
    }



    protected readonly int Depth;
    protected readonly float Brightness;
    protected LoadedTexture? InsetTexture;

    protected virtual void EmbossRoundRectangleLocal(Context ctx, double x, double y, double width, double height, double radius, int depth, float intensity = 0.4f, float lightDarkBalance = 1f, bool inverse = false, float alphaOffset = 0)
    {
        double degrees = Math.PI / 180.0;

        int linewidth = 0;
        ctx.Antialias = Antialias.Best;

        int light = 255;
        int dark = 0;

        if (inverse)
        {
            light = 0;
            dark = 255;
            lightDarkBalance = 2 - lightDarkBalance;
        }

        int depthCounter = depth;
        while (depthCounter-- > 0)
        {
            ctx.NewPath();

            ctx.Arc(x + radius, y + height - radius, radius, 135 * degrees, 180 * degrees);
            ctx.Arc(x + radius, y + radius, radius, 180 * degrees, 270 * degrees);
            ctx.Arc(x + width - radius, y + radius, radius, -90 * degrees, -45 * degrees);

            float factor = intensity * (depth - linewidth) / depth;

            double alpha = Math.Min(1, lightDarkBalance * factor) - alphaOffset;
            ctx.SetSourceRGBA(light, light, light, alpha);

            ctx.LineWidth = 1;
            ctx.Stroke();

            ctx.NewPath();
            ctx.Arc(x + width - radius, y + radius, radius, -45 * degrees, 0 * degrees);
            ctx.Arc(x + width - radius, y + height - radius, radius, 0 * degrees, 90 * degrees);
            ctx.Arc(x + radius, y + height - radius, radius, 90 * degrees, 135 * degrees);

            alpha = Math.Min(1, (2 - lightDarkBalance) * factor) - alphaOffset;
            ctx.SetSourceRGBA(dark, dark, dark, alpha);
            ctx.LineWidth = 1;
            ctx.Stroke();

            linewidth++;

            x += 1f;
            y += 1f;
            width -= 2;
            height -= 2;
        }
    }

    protected virtual void GenerateTexture()
    {
        InsetTexture?.Dispose();

        double outerWidth = Bounds.OuterWidth;
        double outerHeight = Bounds.OuterHeight;

        int width = (int)Math.Ceiling(outerWidth);
        int height = (int)Math.Ceiling(outerHeight);

        if (width <= 0) width = 1;
        if (height <= 0) height = 1;

        ImageSurface surface = new(Format.Argb32, width, height);
        Context ctx = new(surface);

        if (Brightness < 1)
        {
            ctx.SetSourceRGBA(0, 0, 0, 1 - Brightness);
            ctx.Rectangle(0, 0, outerWidth, outerHeight);
            ctx.Fill();
        }

        EmbossRoundRectangleLocal(ctx, 0, 0, outerWidth, outerHeight, radius: 2, depth: Depth, inverse: true, intensity: 0.6f);

        InsetTexture = new LoadedTexture(api);
        generateTexture(surface, ref InsetTexture);

        ctx.Dispose();
        surface.Dispose();
    }
}

public static class GuiElementScrollableInsetHelper
{
    /// <summary>
    /// Adds a scrollable inset to the current GUI. Unlike AddInset, this inset
    /// moves correctly when its parent bounds are scrolled.
    /// </summary>
    public static GuiComposer AddScrollableInset(this GuiComposer composer, ElementBounds bounds, int depth = 4, float brightness = 0.85f)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(new GuiElementScrollableInset(composer.Api, bounds, depth, brightness));
        }
        return composer;
    }
}