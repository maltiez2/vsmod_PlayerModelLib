using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace PlayerModelLib;

/// <summary>
/// A color list picker that correctly clips and scrolls within a scrollable area.
/// </summary>
public class GuiElementScrollableColorListPicker : GuiElementElementListPickerBase<int>
{
    /// <summary>
    /// Optional scissor/clip bounds. Set this when the picker is inside a scrollable
    /// area so that it is correctly clipped when scrolled out of view.
    /// </summary>
    public ElementBounds ClipBounds { get; set; }

    protected LoadedTexture colorTexture;

    // Store the color so we can re-bake the texture
    private readonly int color;

    public GuiElementScrollableColorListPicker(ICoreClientAPI capi, int color, ElementBounds bounds,
        ElementBounds clipBounds = null)
        : base(capi, color, bounds)
    {
        this.color = color;
        this.ClipBounds = clipBounds;
        colorTexture = new LoadedTexture(capi);
    }

    /// <summary>
    /// Called by base class during ComposeElements - draws to the static surface.
    /// We draw nothing here because we handle rendering dynamically ourselves.
    /// </summary>
    public override void DrawElement(int elem, Context ctx, ImageSurface surface)
    {
        // Intentionally empty - color swatch is rendered dynamically in
        // RenderInteractiveElements via colorTexture so it moves with scroll.
    }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();

        // Bake color swatch into its own texture for dynamic rendering
        ComposeColorTexture();

        // Let base class compose the active/selected highlight ring texture
        base.ComposeElements(ctx, surface);
    }

    private void ComposeColorTexture()
    {
        int width = (int)Bounds.OuterWidth;
        int height = (int)Bounds.OuterHeight;

        if (width <= 0) width = 1;
        if (height <= 0) height = 1;

        ImageSurface surface = new ImageSurface(Format.Argb32, width, height);
        Context ctx = genContext(surface);

        double[] dcolor = ColorUtil.ToRGBADoubles(color);
        ctx.SetSourceRGBA(dcolor[0], dcolor[1], dcolor[2], 1);
        RoundRectangle(ctx, 0, 0, Bounds.InnerWidth, Bounds.InnerHeight, 1);
        ctx.Fill();

        generateTexture(surface, ref colorTexture);

        ctx.Dispose();
        surface.Dispose();
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        bool hasClip = ClipBounds != null;

        if (hasClip)
            api.Render.PushScissor(ClipBounds, true);

        // Render color swatch dynamically so it moves correctly with scroll
        api.Render.Render2DTexturePremultipliedAlpha(
            colorTexture.TextureId,
            (int)Bounds.renderX,
            (int)Bounds.renderY,
            (int)Bounds.OuterWidth,
            (int)Bounds.OuterHeight
        );

        // Let base class render the selected highlight ring on top
        base.RenderInteractiveElements(deltaTime);

        if (hasClip)
            api.Render.PopScissor();
    }

    public override void Dispose()
    {
        base.Dispose();
        colorTexture?.Dispose();
        colorTexture = null;
    }
}


public static class GuiElementScrollableColorListPickerHelper
{
    /// <summary>
    /// Returns a previously added scrollable color list picker element.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static GuiElementScrollableColorListPicker GetScrollableColorListPicker(this GuiComposer composer,
        string key)
    {
        return (GuiElementScrollableColorListPicker)composer.GetElement(key);
    }

    /// <summary>
    /// Selects one of the colors from a scrollable color list picker by index.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="key"></param>
    /// <param name="selectedIndex"></param>
    public static void ScrollableColorListPickerSetValue(this GuiComposer composer, string key, int selectedIndex)
    {
        int i = 0;
        GuiElementScrollableColorListPicker btn;
        while ((btn = composer.GetScrollableColorListPicker(key + "-" + i)) != null)
        {
            btn.SetValue(i == selectedIndex);
            i++;
        }
    }

    /// <summary>
    /// Adds a range of clickable color swatches that correctly clip and scroll
    /// within a scrollable area.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="colors">The colors to display.</param>
    /// <param name="onToggle">The event fired when a color is selected.</param>
    /// <param name="startBounds">The starting bounds for the first color swatch.</param>
    /// <param name="maxLineWidth">The maximum line width before wrapping.</param>
    /// <param name="clipBounds">
    /// Optional scissor/clip bounds. Set this when the picker is inside a scrollable
    /// area so that it is correctly clipped when scrolled out of view.
    /// </param>
    /// <param name="key">The name of this color list picker.</param>
    /// <returns></returns>
    public static GuiComposer AddScrollableColorListPicker(this GuiComposer composer, int[] colors,
        Action<int> onToggle, ElementBounds startBounds, int maxLineWidth,
        ElementBounds clipBounds = null, string key = null)
    {
        if (composer.Composed) return composer;

        if (key == null) key = "scrollablecolorlistpicker";

        int quantityButtons = colors.Length;
        double lineWidth = 0;

        for (int i = 0; i < colors.Length; i++)
        {
            int index = i;

            if (lineWidth > maxLineWidth)
            {
                startBounds.fixedX -= lineWidth;
                startBounds.fixedY += startBounds.fixedHeight + 5;
                lineWidth = 0;
            }

            var elem = new GuiElementScrollableColorListPicker(
                composer.Api, colors[i], startBounds.FlatCopy(), clipBounds);

            composer.AddInteractiveElement(elem, key + "-" + i);

            // Mirror the toggle logic from the base AddElementListPicker:
            // selecting one swatch deselects all others; clicking an already-selected
            // swatch keeps it selected.
            (composer[key + "-" + i] as GuiElementElementListPickerBase<int>).handler = (on) =>
            {
                if (on)
                {
                    onToggle(index);
                    for (int j = 0; j < quantityButtons; j++)
                    {
                        if (j == index) continue;
                        (composer[key + "-" + j] as GuiElementElementListPickerBase<int>).SetValue(false);
                    }
                }
                else
                {
                    // Prevent deselecting - keep it on
                    (composer[key + "-" + index] as GuiElementElementListPickerBase<int>).SetValue(true);
                }
            };

            startBounds.fixedX += startBounds.fixedWidth + 5;
            lineWidth += startBounds.fixedWidth + 5;
        }

        return composer;
    }
}