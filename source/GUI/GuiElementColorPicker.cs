using Cairo;
using Vintagestory.API.Config;
using Vintagestory.API.Client;

namespace PlayerModelLib;

public class GuiElementColorPicker : GuiElement
{
    // ─────────────────────────────────────────────────────────────
    //  Constants (unscaled)
    // ─────────────────────────────────────────────────────────────
    private const double SliderHeight = 16;
    private const double SliderPadding = 3;
    private const double PreviewSize = 24;
    private const double BottomPadding = 6;
    private const double InnerPadding = 4;  // left/right inner padding of text input

    // ─────────────────────────────────────────────────────────────
    //  Derived layout (unscaled, relative to Bounds.renderX/Y)
    // ─────────────────────────────────────────────────────────────
    private double _sliderW;

    private double _hueSliderY;
    private double _satSliderY;
    private double _valSliderY;
    private double _alphaSliderY;
    private double _bottomRowY;

    private double _hexInputX;
    private double _hexInputW;

    // ─────────────────────────────────────────────────────────────
    //  Color state
    // ─────────────────────────────────────────────────────────────
    private float _hue;
    private float _saturation;
    private float _value;
    private float _alpha;

    // ─────────────────────────────────────────────────────────────
    //  Drag state
    // ─────────────────────────────────────────────────────────────
    private bool _draggingHue;
    private bool _draggingSat;
    private bool _draggingVal;
    private bool _draggingAlpha;

    // ─────────────────────────────────────────────────────────────
    //  Textures
    // ─────────────────────────────────────────────────────────────
    private LoadedTexture _hueSliderTexture;
    private LoadedTexture _satSliderTexture;
    private LoadedTexture _valSliderTexture;
    private LoadedTexture _alphaSliderTexture;
    private LoadedTexture _previewTexture;

    // ─────────────────────────────────────────────────────────────
    //  Embedded vanilla text input
    // ─────────────────────────────────────────────────────────────
    private GuiElementTextInput _hexInput;
    private bool _suppressHexCallback;

    // ─────────────────────────────────────────────────────────────
    //  Callback
    // ─────────────────────────────────────────────────────────────
    private readonly Action<double[]> _onColorChanged;

    public override bool Focusable => true;

    // ─────────────────────────────────────────────────────────────
    //  Constructor
    // ─────────────────────────────────────────────────────────────
    public GuiElementColorPicker(
        ICoreClientAPI capi,
        ElementBounds bounds,
        Action<double[]> onColorChanged,
        double[] initialColorRgba = null)
        : base(capi, bounds)
    {
        _hueSliderTexture = new LoadedTexture(capi);
        _satSliderTexture = new LoadedTexture(capi);
        _valSliderTexture = new LoadedTexture(capi);
        _alphaSliderTexture = new LoadedTexture(capi);
        _previewTexture = new LoadedTexture(capi);

        _onColorChanged = onColorChanged;

        if (initialColorRgba != null && initialColorRgba.Length >= 4)
        {
            RgbToHsv((float)initialColorRgba[0], (float)initialColorRgba[1],
                     (float)initialColorRgba[2],
                     out _hue, out _saturation, out _value);
            _alpha = (float)initialColorRgba[3];
        }
        else
        {
            _hue = 0f; _saturation = 1f; _value = 1f; _alpha = 1f;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Layout  (all values unscaled)
    // ─────────────────────────────────────────────────────────────
    private void CalcLayout()
    {
        // InnerWidth / InnerHeight are already in scaled pixels —
        // divide by GUIScale to get unscaled design units.
        double innerW = Bounds.InnerWidth / RuntimeEnv.GUIScale;

        _sliderW = Math.Max(10, innerW);

        _hueSliderY = 0;
        _satSliderY = _hueSliderY + SliderHeight + SliderPadding;
        _valSliderY = _satSliderY + SliderHeight + SliderPadding;
        _alphaSliderY = _valSliderY + SliderHeight + SliderPadding;

        _bottomRowY = _alphaSliderY + SliderHeight + BottomPadding;

        _hexInputX = PreviewSize + SliderPadding;
        _hexInputW = Math.Max(10, innerW - PreviewSize - SliderPadding);
    }

    // ─────────────────────────────────────────────────────────────
    //  Compose
    // ─────────────────────────────────────────────────────────────
    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        CalcLayout();
        BuildHexInput();

        RecomposeHueSlider();
        RecomposeSatSlider();
        RecomposeValSlider();
        RecomposeAlphaSlider();
        RecomposePreview();
    }

    // Build (or rebuild) the embedded vanilla text input.
    // Its bounds are expressed in the same fixed coordinate space as the
    // composer uses, but parented to Bounds so they move with the picker.
    private void BuildHexInput()
    {
        _hexInput?.Dispose();

        // ElementBounds.Fixed takes unscaled values.
        // Bounds.fixedX/Y are the picker's own fixed position;
        // we offset by the padding + bottom-row position.
        double absX = Bounds.fixedX + (Bounds.fixedPaddingX) + _hexInputX;
        double absY = Bounds.fixedY + (Bounds.fixedPaddingY) + _bottomRowY;

        ElementBounds hexBounds = ElementBounds
            .Fixed(absX, absY, _hexInputW, PreviewSize)
            .WithParent(Bounds.ParentBounds);

        hexBounds.CalcWorldBounds();

        _hexInput = new GuiElementTextInput(
            api,
            hexBounds,
            OnHexTextChanged,
            CairoFont.TextInput()
        );
        _hexInput.SetMaxLength(9);

        // Compose the text input onto the same static surface
        ImageSurface tmpSurface = new ImageSurface(Format.Argb32,
            (int)hexBounds.OuterWidth, (int)hexBounds.OuterHeight);
        Context tmpCtx = new Context(tmpSurface);
        _hexInput.ComposeElements(tmpCtx, tmpSurface);
        tmpCtx.Dispose();
        tmpSurface.Dispose();

        _suppressHexCallback = true;
        _hexInput.SetValue(BuildHexString());
        _suppressHexCallback = false;
    }

    // ─────────────────────────────────────────────────────────────
    //  Slider textures
    // ─────────────────────────────────────────────────────────────
    private void RecomposeHueSlider()
    {
        int w = Math.Max(1, (int)scaled(_sliderW));
        int h = Math.Max(1, (int)scaled(SliderHeight));

        ImageSurface surface = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        using (var grad = new LinearGradient(0, 0, w, 0))
        {
            grad.AddColorStop(0.000, new Color(1, 0, 0, 1));
            grad.AddColorStop(0.167, new Color(1, 1, 0, 1));
            grad.AddColorStop(0.333, new Color(0, 1, 0, 1));
            grad.AddColorStop(0.500, new Color(0, 1, 1, 1));
            grad.AddColorStop(0.667, new Color(0, 0, 1, 1));
            grad.AddColorStop(0.833, new Color(1, 0, 1, 1));
            grad.AddColorStop(1.000, new Color(1, 0, 0, 1));
            ctx.Rectangle(0, 0, w, h);
            ctx.SetSource(grad);
            ctx.Fill();
        }

        DrawIndicator(ctx, w, h, _hue * w);
        ctx.Dispose();
        generateTexture(surface, ref _hueSliderTexture);
        surface.Dispose();
    }

    private void RecomposeSatSlider()
    {
        int w = Math.Max(1, (int)scaled(_sliderW));
        int h = Math.Max(1, (int)scaled(SliderHeight));

        ImageSurface surface = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        HsvToRgb(_hue, 0f, _value, out double gr, out double gg, out double gb);
        HsvToRgb(_hue, 1f, _value, out double hr, out double hg, out double hb);

        using (var grad = new LinearGradient(0, 0, w, 0))
        {
            grad.AddColorStop(0, new Color(gr, gg, gb, 1));
            grad.AddColorStop(1, new Color(hr, hg, hb, 1));
            ctx.Rectangle(0, 0, w, h);
            ctx.SetSource(grad);
            ctx.Fill();
        }

        DrawIndicator(ctx, w, h, _saturation * w);
        ctx.Dispose();
        generateTexture(surface, ref _satSliderTexture);
        surface.Dispose();
    }

    private void RecomposeValSlider()
    {
        int w = Math.Max(1, (int)scaled(_sliderW));
        int h = Math.Max(1, (int)scaled(SliderHeight));

        ImageSurface surface = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        HsvToRgb(_hue, _saturation, 1f, out double vr, out double vg, out double vb);

        using (var grad = new LinearGradient(0, 0, w, 0))
        {
            grad.AddColorStop(0, new Color(0, 0, 0, 1));
            grad.AddColorStop(1, new Color(vr, vg, vb, 1));
            ctx.Rectangle(0, 0, w, h);
            ctx.SetSource(grad);
            ctx.Fill();
        }

        DrawIndicator(ctx, w, h, _value * w);
        ctx.Dispose();
        generateTexture(surface, ref _valSliderTexture);
        surface.Dispose();
    }

    private void RecomposeAlphaSlider()
    {
        int w = Math.Max(1, (int)scaled(_sliderW));
        int h = Math.Max(1, (int)scaled(SliderHeight));

        ImageSurface surface = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        int tile = Math.Max(1, (int)scaled(4));
        for (int row = 0; row * tile < h; row++)
            for (int col = 0; col * tile < w; col++)
            {
                bool light = (row + col) % 2 == 0;
                ctx.SetSourceRGBA(light ? 0.75 : 0.45, light ? 0.75 : 0.45, light ? 0.75 : 0.45, 1);
                ctx.Rectangle(col * tile, row * tile, tile, tile);
                ctx.Fill();
            }

        HsvToRgb(_hue, _saturation, _value, out double r, out double g, out double b);
        using (var grad = new LinearGradient(0, 0, w, 0))
        {
            grad.AddColorStop(0, new Color(r, g, b, 0));
            grad.AddColorStop(1, new Color(r, g, b, 1));
            ctx.Rectangle(0, 0, w, h);
            ctx.SetSource(grad);
            ctx.Fill();
        }

        DrawIndicator(ctx, w, h, _alpha * w);
        ctx.Dispose();
        generateTexture(surface, ref _alphaSliderTexture);
        surface.Dispose();
    }

    /// <summary>
    /// Draws a vertical indicator line at position x inside a slider of pixel
    /// dimensions (w × h).  Both x and w are in scaled pixels.
    /// </summary>
    private static void DrawIndicator(Context ctx, int w, int h, double x)
    {
        // Keep the line fully visible at both extremes
        x = Math.Max(1.5, Math.Min(x, w - 1.5));

        ctx.SetSourceRGBA(0, 0, 0, 0.7);
        ctx.LineWidth = 3;
        ctx.MoveTo(x, 0);
        ctx.LineTo(x, h);
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 1, 1, 1);
        ctx.LineWidth = 1.5;
        ctx.MoveTo(x, 0);
        ctx.LineTo(x, h);
        ctx.Stroke();
    }

    // ─────────────────────────────────────────────────────────────
    //  Preview texture
    // ─────────────────────────────────────────────────────────────
    private void RecomposePreview()
    {
        int size = Math.Max(1, (int)scaled(PreviewSize));

        ImageSurface surface = new ImageSurface(Format.Argb32, size, size);
        Context ctx = genContext(surface);

        int tile = Math.Max(1, (int)scaled(5));
        for (int row = 0; row * tile < size; row++)
            for (int col = 0; col * tile < size; col++)
            {
                bool light = (row + col) % 2 == 0;
                ctx.SetSourceRGBA(light ? 0.75 : 0.45, light ? 0.75 : 0.45, light ? 0.75 : 0.45, 1);
                ctx.Rectangle(col * tile, row * tile, tile, tile);
                ctx.Fill();
            }

        HsvToRgb(_hue, _saturation, _value, out double r, out double g, out double b);
        ctx.SetSourceRGBA(r, g, b, _alpha);
        ctx.Rectangle(0, 0, size, size);
        ctx.Fill();

        ctx.SetSourceRGBA(0, 0, 0, 0.45);
        ctx.LineWidth = 1;
        ctx.Rectangle(0.5, 0.5, size - 1, size - 1);
        ctx.Stroke();

        ctx.Dispose();
        generateTexture(surface, ref _previewTexture);
        surface.Dispose();
    }

    // ─────────────────────────────────────────────────────────────
    //  Render
    // ─────────────────────────────────────────────────────────────
    public override void RenderInteractiveElements(float deltaTime)
    {
        double bx = Bounds.renderX;
        double by = Bounds.renderY;

        Render2DTexture(_hueSliderTexture.TextureId,
            bx, by + scaled(_hueSliderY),
            scaled(_sliderW), scaled(SliderHeight));

        Render2DTexture(_satSliderTexture.TextureId,
            bx, by + scaled(_satSliderY),
            scaled(_sliderW), scaled(SliderHeight));

        Render2DTexture(_valSliderTexture.TextureId,
            bx, by + scaled(_valSliderY),
            scaled(_sliderW), scaled(SliderHeight));

        Render2DTexture(_alphaSliderTexture.TextureId,
            bx, by + scaled(_alphaSliderY),
            scaled(_sliderW), scaled(SliderHeight));

        // Preview square — bottom-left of bottom row
        Render2DTexture(_previewTexture.TextureId,
            bx, by + scaled(_bottomRowY),
            scaled(PreviewSize), scaled(PreviewSize));

        // Vanilla text input — bottom-right of bottom row
        _hexInput?.RenderInteractiveElements(deltaTime);
    }

    // ─────────────────────────────────────────────────────────────
    //  Mouse
    // ─────────────────────────────────────────────────────────────
    public override void OnMouseDown(ICoreClientAPI api, MouseEvent mouse)
    {
        // Forward to text input if the click lands inside it
        if (_hexInput != null)
        {
            ElementBounds hb = _hexInput.Bounds;
            if (mouse.X >= hb.renderX && mouse.X < hb.renderX + hb.OuterWidth &&
                mouse.Y >= hb.renderY && mouse.Y < hb.renderY + hb.OuterHeight)
            {
                _hexInput.OnMouseDownOnElement(api, mouse);
                mouse.Handled = true;
                return;
            }

            if (_hexInput.HasFocus)
                _hexInput.OnFocusLost();
        }

        // Relative mouse position inside this element (scaled pixels)
        double mx = mouse.X - Bounds.renderX;
        double my = mouse.Y - Bounds.renderY;

        if (HitSlider(mx, my, _hueSliderY)) { _draggingHue = true; UpdateHueFromMouse(mx); mouse.Handled = true; return; }
        if (HitSlider(mx, my, _satSliderY)) { _draggingSat = true; UpdateSatFromMouse(mx); mouse.Handled = true; return; }
        if (HitSlider(mx, my, _valSliderY)) { _draggingVal = true; UpdateValFromMouse(mx); mouse.Handled = true; return; }
        if (HitSlider(mx, my, _alphaSliderY)) { _draggingAlpha = true; UpdateAlphaFromMouse(mx); mouse.Handled = true; }
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (!_draggingHue && !_draggingSat && !_draggingVal && !_draggingAlpha) return;

        double mx = args.X - Bounds.renderX;

        if (_draggingHue) UpdateHueFromMouse(mx);
        if (_draggingSat) UpdateSatFromMouse(mx);
        if (_draggingVal) UpdateValFromMouse(mx);
        if (_draggingAlpha) UpdateAlphaFromMouse(mx);

        args.Handled = true;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        _draggingHue = _draggingSat = _draggingVal = _draggingAlpha = false;
        _hexInput?.OnMouseUp(api, args);
    }

    // ─────────────────────────────────────────────────────────────
    //  Keyboard — forward to embedded text input
    // ─────────────────────────────────────────────────────────────
    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (_hexInput == null || !_hexInput.HasFocus) return;
        _hexInput.OnKeyDown(api, args);
    }

    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
        if (_hexInput == null || !_hexInput.HasFocus) return;
        _hexInput.OnKeyPress(api, args);
    }

    // ─────────────────────────────────────────────────────────────
    //  Hex input callback
    // ─────────────────────────────────────────────────────────────
    private void OnHexTextChanged(string text)
    {
        if (_suppressHexCallback) return;
        TryApplyHexInput(text);
    }

    private void TryApplyHexInput(string raw)
    {
        string input = raw.TrimStart('#');
        try
        {
            if (input.Length == 8)
            {
                byte a = Convert.ToByte(input.Substring(0, 2), 16);
                byte r = Convert.ToByte(input.Substring(2, 2), 16);
                byte g = Convert.ToByte(input.Substring(4, 2), 16);
                byte b = Convert.ToByte(input.Substring(6, 2), 16);
                _alpha = a / 255f;
                RgbToHsv(r / 255f, g / 255f, b / 255f, out _hue, out _saturation, out _value);
                OnColorUpdated(updateHexField: false);
            }
            else if (input.Length == 6)
            {
                byte r = Convert.ToByte(input.Substring(0, 2), 16);
                byte g = Convert.ToByte(input.Substring(2, 2), 16);
                byte b = Convert.ToByte(input.Substring(4, 2), 16);
                RgbToHsv(r / 255f, g / 255f, b / 255f, out _hue, out _saturation, out _value);
                OnColorUpdated(updateHexField: false);
            }
        }
        catch { /* malformed — ignore */ }
    }

    // ─────────────────────────────────────────────────────────────
    //  Hit-test helpers  (mx/my are in scaled pixels)
    // ─────────────────────────────────────────────────────────────
    private bool HitSlider(double mx, double my, double sliderUnscaledY)
    {
        double sy = scaled(sliderUnscaledY);
        double sh = scaled(SliderHeight);
        double sw = scaled(_sliderW);
        return mx >= 0 && mx <= sw && my >= sy && my < sy + sh;
    }

    // ─────────────────────────────────────────────────────────────
    //  Slider value updaters  (mx is in scaled pixels)
    // ─────────────────────────────────────────────────────────────
    private void UpdateHueFromMouse(double mx)
    {
        _hue = Clamp01(mx / scaled(_sliderW));
        OnColorUpdated();
    }

    private void UpdateSatFromMouse(double mx)
    {
        _saturation = Clamp01(mx / scaled(_sliderW));
        OnColorUpdated();
    }

    private void UpdateValFromMouse(double mx)
    {
        _value = Clamp01(mx / scaled(_sliderW));
        OnColorUpdated();
    }

    private void UpdateAlphaFromMouse(double mx)
    {
        _alpha = Clamp01(mx / scaled(_sliderW));
        OnColorUpdated();
    }

    // ─────────────────────────────────────────────────────────────
    //  Color update pipeline
    // ─────────────────────────────────────────────────────────────
    private void OnColorUpdated(bool updateHexField = true)
    {
        RecomposeHueSlider();
        RecomposeSatSlider();
        RecomposeValSlider();
        RecomposeAlphaSlider();
        RecomposePreview();

        if (updateHexField && _hexInput != null)
        {
            _suppressHexCallback = true;
            _hexInput.SetValue(BuildHexString());
            _suppressHexCallback = false;
        }

        HsvToRgb(_hue, _saturation, _value, out double r, out double g, out double b);
        _onColorChanged?.Invoke(new double[] { r, g, b, _alpha });
    }

    private string BuildHexString()
    {
        HsvToRgb(_hue, _saturation, _value, out double r, out double g, out double b);
        byte rb = (byte)Math.Round(r * 255);
        byte gb = (byte)Math.Round(g * 255);
        byte bb = (byte)Math.Round(b * 255);
        byte ab = (byte)Math.Round(_alpha * 255);
        return $"#{ab:X2}{rb:X2}{gb:X2}{bb:X2}";
    }

    // ─────────────────────────────────────────────────────────────
    //  Color space helpers
    // ─────────────────────────────────────────────────────────────
    private static void HsvToRgb(float h, float s, float v,
                                  out double r, out double g, out double b)
    {
        if (s <= 0f) { r = g = b = v; return; }
        float hh = (h * 360f) / 60f;
        int i = (int)hh;
        float ff = hh - i;
        float p = v * (1f - s);
        float q = v * (1f - s * ff);
        float t = v * (1f - s * (1f - ff));
        switch (i % 6)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
    }

    private static void RgbToHsv(float r, float g, float b,
                                  out float h, out float s, out float v)
    {
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;
        v = max;
        s = max < 1e-6f ? 0f : delta / max;
        if (delta < 1e-6f) { h = 0f; return; }
        if (max == r) h = (g - b) / delta;
        else if (max == g) h = 2f + (b - r) / delta;
        else h = 4f + (r - g) / delta;
        h /= 6f;
        if (h < 0f) h += 1f;
    }

    private static float Clamp01(double v)
        => (float)Math.Max(0.0, Math.Min(1.0, v));

    // ─────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────
    public void SetColor(double r, double g, double b, double a = 1.0)
    {
        RgbToHsv((float)r, (float)g, (float)b, out _hue, out _saturation, out _value);
        _alpha = (float)a;
        OnColorUpdated();
    }

    public double[] GetColor()
    {
        HsvToRgb(_hue, _saturation, _value, out double r, out double g, out double b);
        return new double[] { r, g, b, _alpha };
    }

    // ─────────────────────────────────────────────────────────────
    //  Dispose
    // ─────────────────────────────────────────────────────────────
    public override void Dispose()
    {
        _hueSliderTexture?.Dispose();
        _satSliderTexture?.Dispose();
        _valSliderTexture?.Dispose();
        _alphaSliderTexture?.Dispose();
        _previewTexture?.Dispose();
        _hexInput?.Dispose();

        _hueSliderTexture = _satSliderTexture = _valSliderTexture =
            _alphaSliderTexture = _previewTexture = null;
        _hexInput = null;

        base.Dispose();
    }
}

// ─────────────────────────────────────────────────────────────────
//  Composer extension
// ─────────────────────────────────────────────────────────────────
public static class GuiComposerColorPickerExtension
{
    /// <summary>
    /// Adds a color picker with four horizontal sliders (H, S, V, A),
    /// a preview square, and a vanilla-style hex text input.
    /// Minimum recommended unscaled bounds:
    ///   width  ≥ 160
    ///   height ≥ (4 * SliderHeight) + (3 * SliderPadding) + BottomPadding + PreviewSize
    ///          = 80 + 15 + 6 + 36 = 137
    /// </summary>
    public static GuiComposer AddColorPicker(
        this GuiComposer composer,
        Action<double[]> onColorChanged,
        ElementBounds bounds,
        double[] initialColor = null,
        string key = null)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(
                new GuiElementColorPicker(composer.Api, bounds, onColorChanged, initialColor),
                key);
        }
        return composer;
    }

    public static GuiElementColorPicker GetColorPicker(this GuiComposer composer, string key)
        => composer.GetElement(key) as GuiElementColorPicker;
}