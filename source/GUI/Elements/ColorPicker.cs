// ColorPicker.cs
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
    private const double SliderPadding = 4;
    private const double PreviewSize = 24;
    private const double BottomPadding = 6;

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
    //  Hex input — rendered as a texture, not a live element,
    //  so it moves correctly with scroll and clips properly.
    // ─────────────────────────────────────────────────────────────
    private LoadedTexture _hexBgTexture;   // inset background baked once
    private LoadedTexture _hexTextTexture; // text recomposed on value change
    private string _hexValue = "";
    private bool _hexFocused = false;
    private int _hexCursorPos = 0;
    private bool _suppressHexCallback;

    // ─────────────────────────────────────────────────────────────
    //  Clip bounds
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Optional scissor/clip bounds. Set this when the picker is inside a scrollable
    /// area so that it is correctly clipped when scrolled out of view.
    /// </summary>
    public ElementBounds ClipBounds { get; set; }

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
        double[] initialColorRgba = null,
        ElementBounds clipBounds = null)
        : base(capi, bounds)
    {
        _hueSliderTexture = new LoadedTexture(capi);
        _satSliderTexture = new LoadedTexture(capi);
        _valSliderTexture = new LoadedTexture(capi);
        _alphaSliderTexture = new LoadedTexture(capi);
        _previewTexture = new LoadedTexture(capi);
        _hexBgTexture = new LoadedTexture(capi);
        _hexTextTexture = new LoadedTexture(capi);

        _onColorChanged = onColorChanged;
        ClipBounds = clipBounds;

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

        // Bake the hex input background (inset box) into its own texture
        // so it renders dynamically and moves with scroll.
        ComposeHexBackground();
        ComposeHexText();

        RecomposeHueSlider();
        RecomposeSatSlider();
        RecomposeValSlider();
        RecomposeAlphaSlider();
        RecomposePreview();
    }

    // ─────────────────────────────────────────────────────────────
    //  Hex input background texture (inset box, baked once)
    // ─────────────────────────────────────────────────────────────
    private void ComposeHexBackground()
    {
        int w = Math.Max(1, (int)scaled(_hexInputW));
        int h = Math.Max(1, (int)scaled(PreviewSize));

        ImageSurface surface = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        // Dark inset background
        ctx.SetSourceRGBA(0, 0, 0, 0.2);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();

        // Inset emboss border
        EmbossRoundRectangleElement(ctx, 0, 0, w, h, true, 1, 1);

        generateTexture(surface, ref _hexBgTexture);
        ctx.Dispose();
        surface.Dispose();
    }

    // ─────────────────────────────────────────────────────────────
    //  Hex input text texture (recomposed on value change)
    // ─────────────────────────────────────────────────────────────
    private void ComposeHexText()
    {
        int w = Math.Max(1, (int)scaled(_hexInputW));
        int h = Math.Max(1, (int)scaled(PreviewSize));

        ImageSurface surface = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        var font = CairoFont.TextInput();
        font.SetupContext(ctx);

        // Text color
        ctx.SetSourceRGBA(1, 1, 1, _hexFocused ? 1.0 : 0.8);

        double textY = (h - font.GetFontExtents().Height) / 2.0 + font.GetFontExtents().Ascent;
        ctx.MoveTo(scaled(3), textY);
        ctx.ShowText(_hexValue);

        // Simple cursor when focused
        if (_hexFocused)
        {
            string beforeCursor = _hexValue.Substring(0, Math.Min(_hexCursorPos, _hexValue.Length));
            double cursorX = scaled(3) + font.GetTextExtents(beforeCursor).XAdvance;
            ctx.SetSourceRGBA(1, 1, 1, 0.9);
            ctx.LineWidth = 1.5;
            ctx.MoveTo(cursorX, scaled(3));
            ctx.LineTo(cursorX, h - scaled(3));
            ctx.Stroke();
        }

        generateTexture(surface, ref _hexTextTexture);
        ctx.Dispose();
        surface.Dispose();
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

    private static void DrawIndicator(Context ctx, int w, int h, double x)
    {
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
        bool hasClip = ClipBounds != null;
        if (hasClip)
            api.Render.PushScissor(ClipBounds, true);

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

        // Preview square
        Render2DTexture(_previewTexture.TextureId,
            bx, by + scaled(_bottomRowY),
            scaled(PreviewSize), scaled(PreviewSize));

        // Hex input — background then text, both dynamic
        double hexRenderX = bx + scaled(_hexInputX);
        double hexRenderY = by + scaled(_bottomRowY);
        double hexRenderW = scaled(_hexInputW);
        double hexRenderH = scaled(PreviewSize);

        Render2DTexture(_hexBgTexture.TextureId,
            hexRenderX, hexRenderY, hexRenderW, hexRenderH);

        Render2DTexture(_hexTextTexture.TextureId,
            hexRenderX, hexRenderY, hexRenderW, hexRenderH);

        if (hasClip)
            api.Render.PopScissor();
    }

    // ─────────────────────────────────────────────────────────────
    //  Hex input hit bounds (live, from renderX/Y)
    // ─────────────────────────────────────────────────────────────
    private bool IsInsideHexInput(int mx, int my)
    {
        double hexRenderX = Bounds.renderX + scaled(_hexInputX);
        double hexRenderY = Bounds.renderY + scaled(_bottomRowY);
        double hexRenderW = scaled(_hexInputW);
        double hexRenderH = scaled(PreviewSize);
        return mx >= hexRenderX && mx < hexRenderX + hexRenderW &&
               my >= hexRenderY && my < hexRenderY + hexRenderH;
    }

    // ─────────────────────────────────────────────────────────────
    //  Focus
    // ─────────────────────────────────────────────────────────────
    public override void OnFocusGained()
    {
        base.OnFocusGained();
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        if (_hexFocused)
        {
            _hexFocused = false;
            ComposeHexText();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Mouse
    // ─────────────────────────────────────────────────────────────
    public override void OnMouseDown(ICoreClientAPI api, MouseEvent mouse)
    {
        if (IsInsideHexInput(mouse.X, mouse.Y))
        {
            _hexFocused = true;
            _hexCursorPos = _hexValue.Length;
            ComposeHexText();
            mouse.Handled = true;
            return;
        }

        if (_hexFocused)
        {
            _hexFocused = false;
            ComposeHexText();
        }

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
    }

    // ─────────────────────────────────────────────────────────────
    //  Keyboard — only active when hex field is focused
    // ─────────────────────────────────────────────────────────────
    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!_hexFocused) return;

        switch (args.KeyCode)
        {
            case (int)GlKeys.BackSpace:
                if (_hexCursorPos > 0 && _hexValue.Length > 0)
                {
                    _hexValue = _hexValue.Remove(_hexCursorPos - 1, 1);
                    _hexCursorPos--;
                    TryApplyHexInput(_hexValue);
                    ComposeHexText();
                }
                args.Handled = true;
                break;

            case (int)GlKeys.Delete:
                if (_hexCursorPos < _hexValue.Length)
                {
                    _hexValue = _hexValue.Remove(_hexCursorPos, 1);
                    TryApplyHexInput(_hexValue);
                    ComposeHexText();
                }
                args.Handled = true;
                break;

            case (int)GlKeys.Left:
                if (_hexCursorPos > 0) _hexCursorPos--;
                ComposeHexText();
                args.Handled = true;
                break;

            case (int)GlKeys.Right:
                if (_hexCursorPos < _hexValue.Length) _hexCursorPos++;
                ComposeHexText();
                args.Handled = true;
                break;

            case (int)GlKeys.Home:
                _hexCursorPos = 0;
                ComposeHexText();
                args.Handled = true;
                break;

            case (int)GlKeys.End:
                _hexCursorPos = _hexValue.Length;
                ComposeHexText();
                args.Handled = true;
                break;

            case (int)GlKeys.Escape:
            //case (int)GlKeys.Return:
            case (int)GlKeys.KeypadEnter:
                _hexFocused = false;
                ComposeHexText();
                args.Handled = true;
                break;
        }
    }

    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
    {
        if (!_hexFocused) return;

        char c = (char)args.KeyChar;
        // Allow # prefix and hex characters only, max 9 chars (#AARRGGBB)
        if (_hexValue.Length < 9 && (c == '#' || IsHexChar(c)))
        {
            _hexValue = _hexValue.Insert(_hexCursorPos, c.ToString());
            _hexCursorPos++;
            TryApplyHexInput(_hexValue);
            ComposeHexText();
        }
        args.Handled = true;
    }

    private static bool IsHexChar(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    // ─────────────────────────────────────────────────────────────
    //  Hex apply
    // ─────────────────────────────────────────────────────────────
    private void TryApplyHexInput(string raw)
    {
        if (_suppressHexCallback) return;
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
    //  Hit-test helpers
    // ─────────────────────────────────────────────────────────────
    private bool HitSlider(double mx, double my, double sliderUnscaledY)
    {
        double sy = scaled(sliderUnscaledY);
        double sh = scaled(SliderHeight);
        double sw = scaled(_sliderW);
        return mx >= 0 && mx <= sw && my >= sy && my < sy + sh;
    }

    // ─────────────────────────────────────────────────────────────
    //  Slider value updaters
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

        if (updateHexField)
        {
            _suppressHexCallback = true;
            _hexValue = BuildHexString();
            _hexCursorPos = Math.Min(_hexCursorPos, _hexValue.Length);
            _suppressHexCallback = false;
            ComposeHexText();
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
        _hexBgTexture?.Dispose();
        _hexTextTexture?.Dispose();

        _hueSliderTexture = _satSliderTexture = _valSliderTexture =
            _alphaSliderTexture = _previewTexture =
            _hexBgTexture = _hexTextTexture = null;

        base.Dispose();
    }
}

// ─────────────────────────────────────────────────────────────────
//  Composer extension
// ─────────────────────────────────────────────────────────────────
public static class GuiComposerColorPickerExtension
{
    /// <summary>
    /// Adds a color picker to the current GUI instance.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="onColorChanged">Callback fired when the color changes.</param>
    /// <param name="bounds">The bounds of the color picker.</param>
    /// <param name="initialColor">Optional initial RGBA color.</param>
    /// <param name="clipBounds">
    /// Optional scissor/clip bounds. Set this when the picker is inside a scrollable
    /// area so that it is correctly clipped when scrolled out of view.
    /// </param>
    /// <param name="key">The name of this element.</param>
    public static GuiComposer AddColorPicker(
        this GuiComposer composer,
        Action<double[]> onColorChanged,
        ElementBounds bounds,
        double[] initialColor = null,
        ElementBounds clipBounds = null,
        string key = null)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(
                new GuiElementColorPicker(composer.Api, bounds, onColorChanged, initialColor, clipBounds),
                key);
        }
        return composer;
    }

    public static GuiElementColorPicker GetColorPicker(this GuiComposer composer, string key)
        => composer.GetElement(key) as GuiElementColorPicker;
}