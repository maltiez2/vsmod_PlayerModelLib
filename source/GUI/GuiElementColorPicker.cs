using Cairo;
using PlayerModelLib;
using Vintagestory.API.Client;

namespace PlayerModelLib;

public class GuiElementColorPicker : GuiElement
{
    // Layout constants (unscaled)
    private const double Padding = 6;
    private const double SliderWidth = 20;
    private const double SliderSpacing = 6;
    private const double PreviewSize = 40;
    private const double HexInputWidth = 120;
    private const double HexInputHeight = 24;
    private const double PickerSize = 200;

    // Current color state
    private float _hue;        // 0..1
    private float _saturation; // 0..1
    private float _lightness;  // 0..1 (value/brightness in HSV)
    private float _alpha;      // 0..1

    // Interaction state
    private bool _draggingPicker;
    private bool _draggingHue;
    private bool _draggingAlpha;
    private bool _editingHex;
    private string _hexInputText = "";
    private string _hexInputDisplay = "";

    // Textures
    private LoadedTexture _pickerTexture;
    private LoadedTexture _hueSliderTexture;
    private LoadedTexture _alphaSliderTexture;
    private LoadedTexture _previewTexture;
    private LoadedTexture _hexBgTexture;

    // Callback
    private readonly Action<double[]> _onColorChanged; // rgba 0..1

    // Sub-region positions (relative to Bounds.drawX/drawY, unscaled)
    private double PickerX => 0;
    private double PickerY => 0;
    private double HueSliderX => PickerSize + SliderSpacing;
    private double HueSliderY => 0;
    private double AlphaSliderX => HueSliderX + SliderWidth + SliderSpacing;
    private double AlphaSliderY => 0;
    private double PreviewX => 0;
    private double PreviewY => PickerSize + Padding;
    private double HexInputX => PreviewSize + Padding;
    private double HexInputY => PickerSize + Padding + (PreviewSize - HexInputHeight) / 2.0;

    public override bool Focusable => true;


    public GuiElementColorPicker(
        ICoreClientAPI capi,
        ElementBounds bounds,
        Action<double[]> onColorChanged,
        double[] initialColorRgba = null)
        : base(capi, bounds)
    {
        _pickerTexture = new LoadedTexture(capi);
        _hueSliderTexture = new LoadedTexture(capi);
        _alphaSliderTexture = new LoadedTexture(capi);
        _previewTexture = new LoadedTexture(capi);
        _hexBgTexture = new LoadedTexture(capi);

        _onColorChanged = onColorChanged;

        if (initialColorRgba != null && initialColorRgba.Length >= 4)
        {
            RgbToHsv(
                (float)initialColorRgba[0],
                (float)initialColorRgba[1],
                (float)initialColorRgba[2],
                out _hue, out _saturation, out _lightness);
            _alpha = (float)initialColorRgba[3];
        }
        else
        {
            _hue = 0f;
            _saturation = 1f;
            _lightness = 1f;
            _alpha = 1f;
        }

        UpdateHexText();
    }

    // ─────────────────────────────────────────────────────────────
    //  Compose
    // ─────────────────────────────────────────────────────────────

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        RecomposePicker();
        RecomposeHueSlider();
        RecomposeAlphaSlider();
        RecomposePreview();
        RecomposeHexBg();
    }

    private void RecomposePicker()
    {
        int w = (int)scaled(PickerSize);
        int h = (int)scaled(PickerSize);

        ImageSurface surface = new(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        // Draw saturation/value gradient for current hue
        // Left→Right: saturation 0→1
        // Top→Bottom: value 1→0

        HsvToRgb(_hue, 1f, 1f, out double hr, out double hg, out double hb);

        // White → hue color (horizontal)
        using (LinearGradient hGrad = new(0, 0, w, 0))
        {
            hGrad.AddColorStop(0, new Color(1, 1, 1, 1));
            hGrad.AddColorStop(1, new Color(hr, hg, hb, 1));
            ctx.Rectangle(0, 0, w, h);
            ctx.SetSource(hGrad);
            ctx.Fill();
        }

        // Transparent → black (vertical overlay)
        using (LinearGradient vGrad = new(0, 0, 0, h))
        {
            vGrad.AddColorStop(0, new Color(0, 0, 0, 0));
            vGrad.AddColorStop(1, new Color(0, 0, 0, 1));
            ctx.Rectangle(0, 0, w, h);
            ctx.SetSource(vGrad);
            ctx.Fill();
        }

        // Draw crosshair cursor
        double cx = _saturation * w;
        double cy = (1.0 - _lightness) * h;
        ctx.SetSourceRGBA(1, 1, 1, 1);
        ctx.LineWidth = 1.5;
        ctx.Arc(cx, cy, scaled(5), 0, 2 * Math.PI);
        ctx.Stroke();
        ctx.SetSourceRGBA(0, 0, 0, 0.6);
        ctx.Arc(cx, cy, scaled(5) + 1, 0, 2 * Math.PI);
        ctx.Stroke();

        ctx.Dispose();
        generateTexture(surface, ref _pickerTexture);
        surface.Dispose();
    }

    private void RecomposeHueSlider()
    {
        int w = (int)scaled(SliderWidth);
        int h = (int)scaled(PickerSize);

        ImageSurface surface = new(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        // Rainbow gradient top→bottom
        using (LinearGradient grad = new(0, 0, 0, h))
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

        // Hue indicator line
        double iy = _hue * h;
        ctx.SetSourceRGBA(1, 1, 1, 1);
        ctx.LineWidth = 2;
        ctx.MoveTo(0, iy);
        ctx.LineTo(w, iy);
        ctx.Stroke();
        ctx.SetSourceRGBA(0, 0, 0, 0.5);
        ctx.LineWidth = 1;
        ctx.MoveTo(0, iy);
        ctx.LineTo(w, iy);
        ctx.Stroke();

        ctx.Dispose();
        generateTexture(surface, ref _hueSliderTexture);
        surface.Dispose();
    }

    private void RecomposeAlphaSlider()
    {
        int w = (int)scaled(SliderWidth);
        int h = (int)scaled(PickerSize);

        ImageSurface surface = new(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        // Checkerboard background
        int tileSize = (int)scaled(6);
        for (int row = 0; row * tileSize < h; row++)
        {
            for (int col = 0; col * tileSize < w; col++)
            {
                bool light = (row + col) % 2 == 0;
                ctx.SetSourceRGBA(light ? 0.8 : 0.5, light ? 0.8 : 0.5, light ? 0.8 : 0.5, 1);
                ctx.Rectangle(col * tileSize, row * tileSize, tileSize, tileSize);
                ctx.Fill();
            }
        }

        // Get current RGB
        HsvToRgb(_hue, _saturation, _lightness, out double r, out double g, out double b);

        // Alpha gradient top=opaque → bottom=transparent
        using (LinearGradient grad = new(0, 0, 0, h))
        {
            grad.AddColorStop(0, new Color(r, g, b, 1));
            grad.AddColorStop(1, new Color(r, g, b, 0));
            ctx.Rectangle(0, 0, w, h);
            ctx.SetSource(grad);
            ctx.Fill();
        }

        // Alpha indicator line
        double iy = (1.0 - _alpha) * h;
        ctx.SetSourceRGBA(1, 1, 1, 1);
        ctx.LineWidth = 2;
        ctx.MoveTo(0, iy);
        ctx.LineTo(w, iy);
        ctx.Stroke();
        ctx.SetSourceRGBA(0, 0, 0, 0.5);
        ctx.LineWidth = 1;
        ctx.MoveTo(0, iy);
        ctx.LineTo(w, iy);
        ctx.Stroke();

        ctx.Dispose();
        generateTexture(surface, ref _alphaSliderTexture);
        surface.Dispose();
    }

    private void RecomposePreview()
    {
        int size = (int)scaled(PreviewSize);

        ImageSurface surface = new(Format.Argb32, size, size);
        Context ctx = genContext(surface);

        // Checkerboard
        int tileSize = (int)scaled(6);
        for (int row = 0; row * tileSize < size; row++)
        {
            for (int col = 0; col * tileSize < size; col++)
            {
                bool light = (row + col) % 2 == 0;
                ctx.SetSourceRGBA(light ? 0.8 : 0.5, light ? 0.8 : 0.5, light ? 0.8 : 0.5, 1);
                ctx.Rectangle(col * tileSize, row * tileSize, tileSize, tileSize);
                ctx.Fill();
            }
        }

        HsvToRgb(_hue, _saturation, _lightness, out double r, out double g, out double b);
        ctx.SetSourceRGBA(r, g, b, _alpha);
        ctx.Rectangle(0, 0, size, size);
        ctx.Fill();

        // Border
        ctx.SetSourceRGBA(0, 0, 0, 0.5);
        ctx.LineWidth = 1;
        ctx.Rectangle(0.5, 0.5, size - 1, size - 1);
        ctx.Stroke();

        ctx.Dispose();
        generateTexture(surface, ref _previewTexture);
        surface.Dispose();
    }

    private void RecomposeHexBg()
    {
        int w = (int)scaled(HexInputWidth);
        int h = (int)scaled(HexInputHeight);

        ImageSurface surface = new(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        // Background
        ctx.SetSourceRGBA(0.1, 0.1, 0.1, 0.85);
        RoundRectangle(ctx, 0, 0, w, h, scaled(3));
        ctx.Fill();

        // Border — highlight if focused
        ctx.SetSourceRGBA(_editingHex ? 0.6 : 0.3, _editingHex ? 0.8 : 0.3, _editingHex ? 1.0 : 0.3, 1);
        ctx.LineWidth = 1;
        RoundRectangle(ctx, 0.5, 0.5, w - 1, h - 1, scaled(3));
        ctx.Stroke();

        // Hex text
        ctx.SetSourceRGBA(1, 1, 1, 1);
        ctx.SelectFontFace("monospace", FontSlant.Normal, FontWeight.Normal);
        ctx.SetFontSize(scaled(11));

        string display = _editingHex ? _hexInputDisplay : _hexInputText;
        TextExtents te = ctx.TextExtents(display);
        ctx.MoveTo(scaled(4), (h + te.Height) / 2.0);
        ctx.ShowText(display);

        // Cursor when editing
        if (_editingHex)
        {
            double cursorX = scaled(4) + te.Width + 1;
            ctx.SetSourceRGBA(1, 1, 1, 0.9);
            ctx.LineWidth = 1.5;
            ctx.MoveTo(cursorX, scaled(4));
            ctx.LineTo(cursorX, h - scaled(4));
            ctx.Stroke();
        }

        ctx.Dispose();
        generateTexture(surface, ref _hexBgTexture);
        surface.Dispose();
    }

    // ─────────────────────────────────────────────────────────────
    //  Render
    // ─────────────────────────────────────────────────────────────

    public override void RenderInteractiveElements(float deltaTime)
    {
        double bx = Bounds.renderX;
        double by = Bounds.renderY;

        double ps = scaled(PickerSize);
        double sw = scaled(SliderWidth);
        double ss = scaled(SliderSpacing);

        // Picker area
        Render2DTexture(_pickerTexture.TextureId,
            bx + scaled(PickerX),
            by + scaled(PickerY),
            ps, ps);

        // Hue slider
        Render2DTexture(_hueSliderTexture.TextureId,
            bx + scaled(HueSliderX),
            by + scaled(HueSliderY),
            sw, ps);

        // Alpha slider
        Render2DTexture(_alphaSliderTexture.TextureId,
            bx + scaled(AlphaSliderX),
            by + scaled(AlphaSliderY),
            sw, ps);

        // Preview square
        Render2DTexture(_previewTexture.TextureId,
            bx + scaled(PreviewX),
            by + scaled(PreviewY),
            scaled(PreviewSize), scaled(PreviewSize));

        // Hex input
        Render2DTexture(_hexBgTexture.TextureId,
            bx + scaled(HexInputX),
            by + scaled(HexInputY),
            scaled(HexInputWidth), scaled(HexInputHeight));
    }

    // ─────────────────────────────────────────────────────────────
    //  Mouse interaction
    // ─────────────────────────────────────────────────────────────

    public override void OnMouseDown(ICoreClientAPI api, MouseEvent mouse)
    {
        double bx = Bounds.renderX;
        double by = Bounds.renderY;
        double mx = mouse.X - bx;
        double my = mouse.Y - by;

        double ps = scaled(PickerSize);
        double sw = scaled(SliderWidth);
        double ss = scaled(SliderSpacing);

        // Picker area
        if (mx >= scaled(PickerX) && mx < scaled(PickerX) + ps &&
            my >= scaled(PickerY) && my < scaled(PickerY) + ps)
        {
            _draggingPicker = true;
            UpdatePickerFromMouse(mx, my);
            mouse.Handled = true;
            return;
        }

        // Hue slider
        if (mx >= scaled(HueSliderX) && mx < scaled(HueSliderX) + sw &&
            my >= scaled(HueSliderY) && my < scaled(HueSliderY) + ps)
        {
            _draggingHue = true;
            UpdateHueFromMouse(my);
            mouse.Handled = true;
            return;
        }

        // Alpha slider
        if (mx >= scaled(AlphaSliderX) && mx < scaled(AlphaSliderX) + sw &&
            my >= scaled(AlphaSliderY) && my < scaled(AlphaSliderY) + ps)
        {
            _draggingAlpha = true;
            UpdateAlphaFromMouse(my);
            mouse.Handled = true;
            return;
        }

        // Hex input box
        if (mx >= scaled(HexInputX) && mx < scaled(HexInputX) + scaled(HexInputWidth) &&
            my >= scaled(HexInputY) && my < scaled(HexInputY) + scaled(HexInputHeight))
        {
            _editingHex = true;
            _hexInputDisplay = _hexInputText;
            RecomposeHexBg();
            mouse.Handled = true;
            return;
        }

        // Clicked elsewhere — stop hex editing
        if (_editingHex)
        {
            _editingHex = false;
            RecomposeHexBg();
        }
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (!_draggingPicker && !_draggingHue && !_draggingAlpha) return;

        double bx = Bounds.renderX;
        double by = Bounds.renderY;
        double mx = args.X - bx;
        double my = args.Y - by;

        if (_draggingPicker) UpdatePickerFromMouse(mx, my);
        if (_draggingHue) UpdateHueFromMouse(my);
        if (_draggingAlpha) UpdateAlphaFromMouse(my);

        args.Handled = true;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        _draggingPicker = false;
        _draggingHue = false;
        _draggingAlpha = false;
    }

    private void UpdatePickerFromMouse(double mx, double my)
    {
        double ps = scaled(PickerSize);
        double ox = mx - scaled(PickerX);
        double oy = my - scaled(PickerY);

        _saturation = (float)Math.Max(0, Math.Min(1, ox / ps));
        _lightness = (float)Math.Max(0, Math.Min(1, 1.0 - oy / ps));

        OnColorUpdated();
    }

    private void UpdateHueFromMouse(double my)
    {
        double ps = scaled(PickerSize);
        double oy = my - scaled(HueSliderY);
        _hue = (float)Math.Max(0, Math.Min(1, oy / ps));

        OnColorUpdated();
    }

    private void UpdateAlphaFromMouse(double my)
    {
        double ps = scaled(PickerSize);
        double oy = my - scaled(AlphaSliderY);
        _alpha = (float)Math.Max(0, Math.Min(1, 1.0 - oy / ps));

        OnColorUpdated();
    }

    // ─────────────────────────────────────────────────────────────
    //  Keyboard (hex input)
    // ─────────────────────────────────────────────────────────────

    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
        if (!_editingHex) return;

        char c = args.KeyChar;

        if (args.KeyCode == (int)GlKeys.BackSpace)
        {
            if (_hexInputDisplay.Length > 0)
                _hexInputDisplay = _hexInputDisplay.Substring(0, _hexInputDisplay.Length - 1);
            RecomposeHexBg();
            args.Handled = true;
            return;
        }

        if (args.KeyCode == (int)GlKeys.Enter || args.KeyCode == (int)GlKeys.KeypadEnter)
        {
            TryApplyHexInput();
            _editingHex = false;
            RecomposeHexBg();
            args.Handled = true;
            return;
        }

        if (args.KeyCode == (int)GlKeys.Escape)
        {
            _editingHex = false;
            _hexInputDisplay = _hexInputText;
            RecomposeHexBg();
            args.Handled = true;
            return;
        }

        // Allow hex chars + optional leading #
        if (_hexInputDisplay.Length < 9 &&
            (IsHexChar(c) || (c == '#' && _hexInputDisplay.Length == 0)))
        {
            _hexInputDisplay += char.ToUpper(c);
            RecomposeHexBg();
            args.Handled = true;
        }
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!_editingHex) return;

        if (args.KeyCode == (int)GlKeys.BackSpace)
        {
            if (_hexInputDisplay.Length > 0)
                _hexInputDisplay = _hexInputDisplay.Substring(0, _hexInputDisplay.Length - 1);
            RecomposeHexBg();
            args.Handled = true;
        }
    }

    private bool IsHexChar(char c)
    {
        return (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F');
    }

    private void TryApplyHexInput()
    {
        string input = _hexInputDisplay.TrimStart('#');

        // Accept AARRGGBB (8) or RRGGBB (6)
        if (input.Length == 8)
        {
            try
            {
                byte a = Convert.ToByte(input.Substring(0, 2), 16);
                byte r = Convert.ToByte(input.Substring(2, 2), 16);
                byte g = Convert.ToByte(input.Substring(4, 2), 16);
                byte b = Convert.ToByte(input.Substring(6, 2), 16);

                _alpha = a / 255f;
                RgbToHsv(r / 255f, g / 255f, b / 255f,
                            out _hue, out _saturation, out _lightness);
                OnColorUpdated();
            }
            catch { /* invalid — ignore */ }
        }
        else if (input.Length == 6)
        {
            try
            {
                byte r = Convert.ToByte(input.Substring(0, 2), 16);
                byte g = Convert.ToByte(input.Substring(2, 2), 16);
                byte b = Convert.ToByte(input.Substring(4, 2), 16);

                RgbToHsv(r / 255f, g / 255f, b / 255f,
                            out _hue, out _saturation, out _lightness);
                OnColorUpdated();
            }
            catch { /* invalid — ignore */ }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Color update pipeline
    // ─────────────────────────────────────────────────────────────

    private void OnColorUpdated()
    {
        UpdateHexText();
        RecomposePicker();
        RecomposeHueSlider();
        RecomposeAlphaSlider();
        RecomposePreview();
        RecomposeHexBg();

        HsvToRgb(_hue, _saturation, _lightness, out double r, out double g, out double b);
        _onColorChanged?.Invoke(new double[] { r, g, b, _alpha });
    }

    private void UpdateHexText()
    {
        HsvToRgb(_hue, _saturation, _lightness, out double r, out double g, out double b);
        byte rb = (byte)Math.Round(r * 255);
        byte gb = (byte)Math.Round(g * 255);
        byte bb = (byte)Math.Round(b * 255);
        byte ab = (byte)Math.Round(_alpha * 255);
        _hexInputText = $"#{ab:X2}{rb:X2}{gb:X2}{bb:X2}";
    }

    // ─────────────────────────────────────────────────────────────
    //  Color space helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>Converts HSV to RGB. All values 0..1.</summary>
    private static void HsvToRgb(float h, float s, float v,
                                    out double r, out double g, out double b)
    {
        if (s <= 0f)
        {
            r = g = b = v;
            return;
        }

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

    /// <summary>Converts RGB to HSV. All values 0..1.</summary>
    private static void RgbToHsv(float r, float g, float b,
                                    out float h, out float s, out float v)
    {
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        v = max;
        s = max < 1e-6f ? 0f : delta / max;

        if (delta < 1e-6f)
        {
            h = 0f;
            return;
        }

        if (max == r)
            h = (g - b) / delta;
        else if (max == g)
            h = 2f + (b - r) / delta;
        else
            h = 4f + (r - g) / delta;

        h /= 6f;
        if (h < 0f) h += 1f;
    }

    // ─────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Programmatically set the color. Fires the callback.
    /// </summary>
    public void SetColor(double r, double g, double b, double a = 1.0)
    {
        RgbToHsv((float)r, (float)g, (float)b,
                    out _hue, out _saturation, out _lightness);
        _alpha = (float)a;
        OnColorUpdated();
    }

    /// <summary>
    /// Returns current color as RGBA double array (0..1 each).
    /// </summary>
    public double[] GetColor()
    {
        HsvToRgb(_hue, _saturation, _lightness, out double r, out double g, out double b);
        return new double[] { r, g, b, _alpha };
    }

    // ─────────────────────────────────────────────────────────────
    //  Dispose
    // ─────────────────────────────────────────────────────────────

    public override void Dispose()
    {
        _pickerTexture?.Dispose();
        _hueSliderTexture?.Dispose();
        _alphaSliderTexture?.Dispose();
        _previewTexture?.Dispose();
        _hexBgTexture?.Dispose();

        _pickerTexture = null;
        _hueSliderTexture = null;
        _alphaSliderTexture = null;
        _previewTexture = null;
        _hexBgTexture = null;

        base.Dispose();
    }
}

public static class GuiComposerColorPickerExtension
{
    /// <summary>
    /// Adds a color picker element.
    /// </summary>
    /// <param name="composer">The composer.</param>
    /// <param name="onColorChanged">Callback receiving RGBA (0..1 each) when color changes.</param>
    /// <param name="bounds">Element bounds. Should be at least ~270×260 unscaled.</param>
    /// <param name="initialColor">Optional initial RGBA color (0..1 each).</param>
    /// <param name="key">Optional element key.</param>
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

    /// <summary>
    /// Retrieves a color picker element by key.
    /// </summary>
    public static GuiElementColorPicker GetColorPicker(this GuiComposer composer, string key)
        => composer.GetElement(key) as GuiElementColorPicker;
}