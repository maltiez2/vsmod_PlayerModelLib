// Canvas.cs
using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace PlayerModelLib;

public class GuiElementCanvasEditor : GuiElement
{
    // ── Layout constants (unscaled) ───────────────────────────────────────────
    private const double Pad = 2;
    private const double ColorSwatchSz = 22;
    private const double ColorSwatchGap = 1;
    private const double ColorPickerHeight = 120;

    // ── Layout mode ───────────────────────────────────────────────────────────
    private bool _wideMode = false;

    // ── Data ──────────────────────────────────────────────────────────────────
    private readonly TextureCanvasData _data;

    private int[] _colors;
    private int _selectedColorIndex = 0;
    private int _hoveredColorIndex = -1;

    /// <summary>
    /// Per-pixel swatch index.  Entry i holds the index into <see cref="_colors"/>
    /// that was last painted onto pixel i.  255 = transparent / unset.
    /// </summary>
    private byte[] _pixelSwatchIndex;

    // ── Textures ──────────────────────────────────────────────────────────────
    private LoadedTexture _canvasTexture;
    private LoadedTexture _paletteTexture;

    private bool _canvasDirty = true;
    private bool _paletteDirty = true;

    // ── Geometry (unscaled offsets from Bounds.renderX/Y, recalculated each frame) ──
    // These are stored as unscaled values and converted to render coords on the fly.
    private double _canvasOffsetX, _canvasOffsetY, _canvasW, _canvasH;
    private double _cellW, _cellH;
    private double _paletteOffsetX, _paletteOffsetY, _paletteW, _paletteH;
    private int _swatchCols;
    private int _swatchRows;
    private double _swatchSz;

    // ── Embedded color picker ─────────────────────────────────────────────────
    private GuiElementColorPicker? _colorPicker;
    private bool _suppressPickerCallback;

    // ── Mouse ─────────────────────────────────────────────────────────────────
    private bool _isPainting = false;
    private bool _btnHovered = false;

    // ── Clip bounds ───────────────────────────────────────────────────────────
    /// <summary>
    /// Optional scissor/clip bounds. Set this when the canvas editor is inside a
    /// scrollable area so that it is correctly clipped when scrolled out of view.
    /// </summary>
    public ElementBounds ClipBounds { get; set; }

    // ── Callback ──────────────────────────────────────────────────────────────
    public event Action<TextureCanvasData>? OnChanged;

    // ─────────────────────────────────────────────────────────────────────────

    public GuiElementCanvasEditor(
        ICoreClientAPI capi,
        ElementBounds bounds,
        TextureCanvasData data,
        ElementBounds clipBounds = null)
        : base(capi, bounds)
    {
        _data = data;
        ClipBounds = clipBounds;

        RebuildColorList();
        RebuildPixelSwatchIndex();

        _canvasTexture = new LoadedTexture(capi);
        _paletteTexture = new LoadedTexture(capi);
    }

    // =========================================================================
    //  Public API
    // =========================================================================

    public void SetColors(IEnumerable<int> colors)
    {
        var list = new List<int> { 0 };
        foreach (var c in colors)
            if (c != 0)
                list.Add(c);

        _colors = list.ToArray();
        _data.Colors = _colors;
        _paletteDirty = true;
    }

    public void SetData(TextureCanvasData newData)
    {
        if (newData == null) throw new ArgumentNullException(nameof(newData));

        // Update pixels
        _data.Width = newData.Width;
        _data.Height = newData.Height;
        _data.Pixels = newData.Pixels ?? Array.Empty<int>();

        // Update colors, ensuring transparent slot 0 is always present
        var list = new List<int> { 0 };
        if (newData.Colors != null)
            foreach (var c in newData.Colors)
                if (c != 0)
                    list.Add(c);

        _colors = list.ToArray();
        _data.Colors = _colors;

        // Clamp selected index in case the new palette is smaller
        _selectedColorIndex = Math.Min(_selectedColorIndex, _colors.Length - 1);
        _hoveredColorIndex = -1;

        // Rebuild the pixel→swatch mapping for the new data
        RebuildPixelSwatchIndex();

        // Sync the color picker to the current selection
        if (_colorPicker != null && _selectedColorIndex > 0)
        {
            _suppressPickerCallback = true;
            double[] rgba = ArgbToRgba(_colors[_selectedColorIndex]);
            _colorPicker.SetColor(rgba[0], rgba[1], rgba[2], rgba[3]);
            _suppressPickerCallback = false;
        }

        _canvasDirty = true;
        _paletteDirty = true;
    }

    // =========================================================================
    //  Init helpers
    // =========================================================================

    private void RebuildColorList()
    {
        var list = new List<int> { 0 };
        if (_data.Colors != null)
            foreach (var c in _data.Colors)
                if (c != 0)
                    list.Add(c);

        _colors = list.ToArray();
        _data.Colors = _colors;
    }

    private void RebuildPixelSwatchIndex()
    {
        int len = _data.Pixels?.Length ?? 0;
        _pixelSwatchIndex = new byte[len];

        for (int i = 0; i < len; i++)
        {
            int argb = _data.Pixels[i];
            byte si = 0;

            for (int c = 0; c < _colors.Length; c++)
            {
                if (_colors[c] == argb)
                {
                    si = (byte)Math.Min(c, 254);
                    break;
                }
            }

            _pixelSwatchIndex[i] = si;
        }
    }

    // =========================================================================
    //  Layout — stores unscaled offsets, NOT absolute render coords.
    //  Render coords are computed each frame as: Bounds.renderX + scaled(offsetX)
    // =========================================================================

    private void CalcLayout()
    {
        Bounds.CalcWorldBounds();
        if (_wideMode) CalcLayoutWide();
        else CalcLayoutSplit();
    }

    private void CalcLayoutSplit()
    {
        double totalW = Bounds.InnerWidth;
        double totalH = Bounds.InnerHeight;
        double pickerH = scaled(ColorPickerHeight);
        double pad = scaled(Pad);

        double halfW = totalW / 2.0;
        double cellSize = halfW / _data.Width;
        double canvasH = cellSize * _data.Height;

        double maxCanvasH = totalH - pickerH - pad * 2;
        if (canvasH > maxCanvasH)
        {
            cellSize = maxCanvasH / _data.Height;
            canvasH = cellSize * _data.Height;
        }

        // Store as scaled pixel offsets from Bounds.renderX/Y
        _canvasOffsetX = 0;
        _canvasOffsetY = 0;
        _canvasW = cellSize * _data.Width;
        _canvasH = canvasH;
        _cellW = cellSize;
        _cellH = cellSize;

        _paletteOffsetX = halfW;
        _paletteOffsetY = 0;
        _paletteW = halfW;
        _paletteH = canvasH;

        double swatchGap = scaled(ColorSwatchGap);
        double nominalSz = scaled(ColorSwatchSz);
        _swatchCols = Math.Max(1,
            (int)((_paletteW - scaled(Pad) * 2 + swatchGap) / (nominalSz + swatchGap)));
        double totalGaps = (_swatchCols - 1) * swatchGap;
        _swatchSz = (_paletteW - scaled(Pad) * 2 - totalGaps) / _swatchCols;
    }

    private void CalcLayoutWide()
    {
        double totalW = Bounds.InnerWidth;
        double totalH = Bounds.InnerHeight;
        double pad = scaled(Pad);

        double swatchGap = scaled(ColorSwatchGap);
        double nominalSz = scaled(ColorSwatchSz);
        int cols = Math.Max(1,
            (int)((totalW - pad * 2 + swatchGap) / (nominalSz + swatchGap)));
        int totalSlots = _colors.Length + 1;
        int rows = (totalSlots + cols - 1) / cols;
        double swatchAreaH = pad * 2 + rows * nominalSz + Math.Max(0, rows - 1) * swatchGap;

        double maxCanvasH = totalH - swatchAreaH - pad;
        double cellSize = Math.Min(totalW / _data.Width,
                                     maxCanvasH / _data.Height);
        cellSize = Math.Max(cellSize, 1);

        _canvasW = cellSize * _data.Width;
        _canvasH = cellSize * _data.Height;
        _cellW = cellSize;
        _cellH = cellSize;

        _canvasOffsetX = 0;
        _canvasOffsetY = 0;

        _paletteOffsetX = 0;
        _paletteOffsetY = _canvasH + pad;
        _paletteW = totalW;
        _paletteH = swatchAreaH;

        double totalGaps = (cols - 1) * swatchGap;
        _swatchSz = (totalW - pad * 2 - totalGaps) / cols;
        _swatchCols = cols;
    }

    // Live render coords — always computed from current Bounds.renderX/Y
    private double CanvasRenderX => Bounds.renderX + _canvasOffsetX;
    private double CanvasRenderY => Bounds.renderY + _canvasOffsetY;
    private double PaletteRenderX => Bounds.renderX + _paletteOffsetX;
    private double PaletteRenderY => Bounds.renderY + _paletteOffsetY;

    private ElementBounds BuildPickerBounds()
    {
        double pad = scaled(Pad);
        double scale = RuntimeEnv.GUIScale;
        double pickerOffsetY = (_canvasOffsetY + _canvasH + pad) / scale;
        return ElementBounds
            .Fixed(0, pickerOffsetY, Bounds.InnerWidth / scale, ColorPickerHeight)
            .WithParent(Bounds);
    }

    // =========================================================================
    //  Compose
    // =========================================================================

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        CalcLayout();

        _colorPicker?.Dispose();
        _colorPicker = null;

        if (!_wideMode)
        {
            double[] initialColor = ArgbToRgba(_colors[_selectedColorIndex == 0 ? 1 : _selectedColorIndex]);
            _colorPicker = new GuiElementColorPicker(
                api,
                BuildPickerBounds(),
                OnPickerColorChanged,
                initialColor,
                ClipBounds);  // propagate clip bounds into the embedded picker
            _colorPicker.ComposeElements(ctxStatic, surface);
        }

        _canvasDirty = true;
        _paletteDirty = true;
    }

    // =========================================================================
    //  Picker callback
    // =========================================================================

    private void OnPickerColorChanged(double[] rgba)
    {
        if (_suppressPickerCallback) return;
        if (_selectedColorIndex == 0) return;

        int argb = RgbaToArgb(rgba);
        if (_colors[_selectedColorIndex] == argb) return;

        _colors[_selectedColorIndex] = argb;
        _data.Colors = _colors;

        RecolorPixelsForSwatch(_selectedColorIndex);

        _paletteDirty = true;
        _canvasDirty = true;

        OnChanged?.Invoke(_data);
    }

    private void RecolorPixelsForSwatch(int swatchIndex)
    {
        int newArgb = _colors[swatchIndex];
        for (int i = 0; i < _pixelSwatchIndex.Length; i++)
        {
            if (_pixelSwatchIndex[i] == swatchIndex)
                _data.Pixels[i] = newArgb;
        }
    }

    // =========================================================================
    //  Render
    // =========================================================================

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (_canvasDirty) RecomposeCanvas();
        if (_paletteDirty) RecomposePalette();

        bool hasClip = ClipBounds != null;
        if (hasClip)
            api.Render.PushScissor(ClipBounds, true);

        // Use live render coords so everything moves with scroll
        Render2DTexture(_canvasTexture.TextureId,
            CanvasRenderX, CanvasRenderY, _canvasW, _canvasH);

        Render2DTexture(_paletteTexture.TextureId,
            PaletteRenderX, PaletteRenderY, _paletteW, _paletteH);

        if (hasClip)
            api.Render.PopScissor();

        // Color picker manages its own scissor via its own ClipBounds
        _colorPicker?.RenderInteractiveElements(deltaTime);
    }

    // =========================================================================
    //  Canvas texture
    // =========================================================================

    private void RecomposeCanvas()
    {
        _canvasDirty = false;

        int texW = Math.Max(1, (int)_canvasW);
        int texH = Math.Max(1, (int)_canvasH);

        using var surface = new ImageSurface(Format.Argb32, texW, texH);
        using var ctx = genContext(surface);

        DrawCheckerboard(ctx, 0, 0, texW, texH, _data.Width, _data.Height);

        for (int py = 0; py < _data.Height; py++)
            for (int px = 0; px < _data.Width; px++)
            {
                int i = py * _data.Width + px;
                if (i >= _data.Pixels.Length) continue;

                int argb = _data.Pixels[i];
                if (((argb >> 24) & 0xFF) == 0) continue;

                UnpackArgb(argb, out double a, out double r, out double g, out double b);
                ctx.SetSourceRGBA(r, g, b, a);
                ctx.Rectangle(px * _cellW, py * _cellH, _cellW, _cellH);
                ctx.Fill();
            }

        ctx.SetSourceRGBA(0, 0, 0, 0.22);
        ctx.LineWidth = 1;
        for (int px = 0; px <= _data.Width; px++)
        {
            ctx.MoveTo(px * _cellW, 0);
            ctx.LineTo(px * _cellW, texH);
            ctx.Stroke();
        }
        for (int py = 0; py <= _data.Height; py++)
        {
            ctx.MoveTo(0, py * _cellH);
            ctx.LineTo(texW, py * _cellH);
            ctx.Stroke();
        }

        ctx.SetSourceRGBA(0.15, 0.15, 0.15, 1);
        ctx.LineWidth = 2;
        ctx.Rectangle(0, 0, texW, texH);
        ctx.Stroke();

        generateTexture(surface, ref _canvasTexture);
    }

    // =========================================================================
    //  Palette texture
    // =========================================================================

    private void RecomposePalette()
    {
        _paletteDirty = false;

        int texW = Math.Max(1, (int)_paletteW);
        int texH = Math.Max(1, (int)_paletteH);

        using var surface = new ImageSurface(Format.Argb32, texW, texH);
        using var ctx = genContext(surface);

        double pad = scaled(Pad);
        double swatchGap = scaled(ColorSwatchGap);
        double nominalSz = scaled(ColorSwatchSz);

        _swatchCols = Math.Max(1,
            (int)((texW - pad * 2 + swatchGap) / (nominalSz + swatchGap)));
        double totalGaps = (_swatchCols - 1) * swatchGap;
        _swatchSz = (texW - pad * 2 - totalGaps) / _swatchCols;

        DrawToggleButton(ctx, pad, pad, _swatchSz);

        for (int i = 0; i < _colors.Length; i++)
        {
            int slot = i + 1;
            GetSlotXY(slot, _swatchCols, pad, _swatchSz, swatchGap,
                out double sx, out double sy);

            bool locked = (i == 0);
            DrawColorSwatch(ctx, sx, sy, _swatchSz, _colors[i],
                i == _selectedColorIndex,
                i == _hoveredColorIndex,
                locked);
        }

        _swatchRows = ((_colors.Length + 1) + _swatchCols - 1) / _swatchCols;

        generateTexture(surface, ref _paletteTexture);
    }

    // =========================================================================
    //  Slot layout helper
    // =========================================================================

    private void GetSlotXY(int slot, int cols, double pad, double sz, double gap,
        out double x, out double y)
    {
        int row = slot / cols;
        int col = slot % cols;

        if (!_wideMode && row % 2 == 1)
            col = (cols - 1) - col;

        x = pad + col * (sz + gap);
        y = pad + row * (sz + gap);
    }

    // =========================================================================
    //  Drawing helpers
    // =========================================================================

    private void DrawToggleButton(Context ctx, double x, double y, double size)
    {
        double bgAlpha = _btnHovered ? 0.55 : 0.30;
        ctx.SetSourceRGBA(0.12, 0.12, 0.12, bgAlpha);
        ctx.Rectangle(x, y, size, size);
        ctx.Fill();

        if (_wideMode)
            ctx.SetSourceRGBA(1, 0.85, 0, 1);
        else
            ctx.SetSourceRGBA(0.7, 0.7, 0.7, 0.8);
        ctx.LineWidth = 1.5;
        ctx.Rectangle(x, y, size, size);
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 1, 1, 0.9);
        double m = size * 0.18;
        double iw = size - m * 2;
        double ih = size - m * 2;

        if (_wideMode)
        {
            double barH = ih * 0.38;
            ctx.Rectangle(x + m, y + m, iw, barH);
            ctx.Fill();
            double rowY = y + m + barH + ih * 0.10;
            double halfIw = iw / 2.0 - 1;
            double remH = ih - barH - ih * 0.10;
            ctx.Rectangle(x + m, rowY, halfIw, remH);
            ctx.Fill();
            ctx.Rectangle(x + m + halfIw + 2, rowY, halfIw, remH);
            ctx.Fill();
        }
        else
        {
            double halfIw = iw / 2.0 - 1;
            ctx.Rectangle(x + m, y + m, halfIw, ih);
            ctx.Fill();
            ctx.Rectangle(x + m + halfIw + 2, y + m, halfIw, ih);
            ctx.Fill();
        }
    }

    private static void DrawCheckerboard(
        Context ctx, double x, double y, double w, double h,
        int gridCols, int gridRows)
    {
        double cellW = w / gridCols;
        double cellH = h / gridRows;
        for (int row = 0; row < gridRows; row++)
            for (int col = 0; col < gridCols; col++)
            {
                double v = (row + col) % 2 == 0 ? 0.76 : 0.54;
                ctx.SetSourceRGBA(v, v, v, 1);
                ctx.Rectangle(x + col * cellW, y + row * cellH, cellW, cellH);
                ctx.Fill();
            }
    }

    private void DrawColorSwatch(
        Context ctx,
        double x, double y, double size,
        int argb, bool selected, bool hovered, bool locked = false)
    {
        double half = size / 2.0;
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 2; col++)
            {
                double v = (row + col) % 2 == 0 ? 0.76 : 0.54;
                ctx.SetSourceRGBA(v, v, v, 1);
                ctx.Rectangle(x + col * half, y + row * half, half, half);
                ctx.Fill();
            }

        UnpackArgb(argb, out double a, out double r, out double g, out double b);
        ctx.SetSourceRGBA(r, g, b, a);
        ctx.Rectangle(x, y, size, size);
        ctx.Fill();

        if (selected)
        {
            ctx.SetSourceRGBA(1, 0.9, 0, 1);
            ctx.LineWidth = 2.5;
        }
        else if (hovered && !locked)
        {
            ctx.SetSourceRGBA(1, 1, 1, 0.85);
            ctx.LineWidth = 1.5;
        }
        else
        {
            ctx.SetSourceRGBA(0, 0, 0, 0.55);
            ctx.LineWidth = 1;
        }
        ctx.Rectangle(x, y, size, size);
        ctx.Stroke();

        if (locked)
            DrawLockIcon(ctx, x, y, size);
    }

    private static void DrawLockIcon(Context ctx, double x, double y, double size)
    {
        double m = size * 0.18;
        double bw = size * 0.58;
        double bh = size * 0.42;
        double bx = x + (size - bw) / 2.0;
        double by = y + size - m - bh;
        double br = bw * 0.15;

        double slw = Math.Max(1.5, size * 0.11);
        double shOuterR = bw * 0.48 / 2.0 + slw / 2.0;
        double shCx = x + size / 2.0;
        double shCy = by;

        ctx.SetSourceRGBA(1, 1, 1, 0.85);
        ctx.LineWidth = slw;
        ctx.LineCap = LineCap.Butt;
        ctx.Arc(shCx, shCy, shOuterR - slw / 2.0, Math.PI, 0);
        ctx.Stroke();

        ctx.SetSourceRGBA(1, 1, 1, 0.85);
        RoundRect(ctx, bx, by, bw, bh, br);
        ctx.Fill();

        double dotR = bw * 0.14;
        ctx.SetSourceRGBA(0.15, 0.15, 0.15, 0.90);
        ctx.Arc(x + size / 2.0, by + bh * 0.44, dotR, 0, Math.PI * 2);
        ctx.Fill();
    }

    private static void RoundRect(Context ctx,
        double x, double y, double w, double h, double r)
    {
        ctx.MoveTo(x + r, y);
        ctx.LineTo(x + w - r, y);
        ctx.Arc(x + w - r, y + r, r, -Math.PI / 2, 0);
        ctx.LineTo(x + w, y + h - r);
        ctx.Arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
        ctx.LineTo(x + r, y + h);
        ctx.Arc(x + r, y + h - r, r, Math.PI / 2, Math.PI);
        ctx.LineTo(x, y + r);
        ctx.Arc(x + r, y + r, r, Math.PI, 3 * Math.PI / 2);
        ctx.ClosePath();
    }

    // =========================================================================
    //  Hit testing — uses live render coords
    // =========================================================================

    private int HitTestPaletteSlot(int mx, int my)
    {
        double pad = scaled(Pad);
        double swatchGap = scaled(ColorSwatchGap);

        double rx = mx - PaletteRenderX - pad;
        double ry = my - PaletteRenderY - pad;
        if (rx < 0 || ry < 0) return -1;

        int row = (int)(ry / (_swatchSz + swatchGap));
        int col = (int)(rx / (_swatchSz + swatchGap));

        double localX = rx - col * (_swatchSz + swatchGap);
        double localY = ry - row * (_swatchSz + swatchGap);
        if (localX > _swatchSz || localY > _swatchSz) return -1;
        if (col >= _swatchCols) return -1;

        int slot;
        if (!_wideMode && row % 2 == 1)
            slot = row * _swatchCols + (_swatchCols - 1 - col);
        else
            slot = row * _swatchCols + col;

        int totalSlots = _colors.Length + 1;
        if (slot < 0 || slot >= totalSlots) return -1;
        return slot;
    }

    private bool TryGetCanvasCell(int mx, int my, out int px, out int py)
    {
        px = py = -1;
        double rx = mx - CanvasRenderX;
        double ry = my - CanvasRenderY;
        if (rx < 0 || ry < 0 || rx >= _canvasW || ry >= _canvasH) return false;
        px = (int)(rx / _cellW);
        py = (int)(ry / _cellH);
        return px >= 0 && px < _data.Width && py >= 0 && py < _data.Height;
    }

    // =========================================================================
    //  Mouse events
    // =========================================================================

    public override void OnMouseDown(ICoreClientAPI api, MouseEvent mouse)
    {
        if (!IsPositionInside(mouse.X, mouse.Y)) return;

        if (_colorPicker != null &&
            IsInsideBounds(_colorPicker.Bounds, mouse.X, mouse.Y))
        {
            _colorPicker.OnMouseDown(api, mouse);
            if (mouse.Handled) return;
        }

        int slot = HitTestPaletteSlot(mouse.X, mouse.Y);
        if (slot == 0)
        {
            ToggleLayout(api);
            mouse.Handled = true;
            return;
        }
        if (slot > 0)
        {
            SelectSwatch(slot - 1);
            mouse.Handled = true;
            return;
        }

        if (TryGetCanvasCell(mouse.X, mouse.Y, out int px, out int py))
        {
            _isPainting = true;
            PaintPixel(px, py);
            mouse.Handled = true;
        }
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent mouse)
    {
        _colorPicker?.OnMouseMove(api, mouse);

        int slot = HitTestPaletteSlot(mouse.X, mouse.Y);

        bool prevBtnHover = _btnHovered;
        _btnHovered = (slot == 0);
        if (_btnHovered != prevBtnHover) _paletteDirty = true;

        int prev = _hoveredColorIndex;
        _hoveredColorIndex = (slot > 0) ? slot - 1 : -1;
        if (_hoveredColorIndex != prev) _paletteDirty = true;

        if (_isPainting && TryGetCanvasCell(mouse.X, mouse.Y, out int px, out int py))
            PaintPixel(px, py);
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        _isPainting = false;
        _colorPicker?.OnMouseUp(api, args);
    }

    // =========================================================================
    //  Keyboard
    // =========================================================================

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
        => _colorPicker?.OnKeyDown(api, args);

    public override void OnKeyPress(ICoreClientAPI api, KeyEvent args)
        => _colorPicker?.OnKeyPress(api, args);

    // =========================================================================
    //  Layout toggle
    // =========================================================================

    private void ToggleLayout(ICoreClientAPI api)
    {
        _wideMode = !_wideMode;
        int w = Math.Max(1, (int)Bounds.OuterWidth);
        int h = Math.Max(1, (int)Bounds.OuterHeight);
        using var surface = new ImageSurface(Format.Argb32, w, h);
        using var ctx = genContext(surface);
        ComposeElements(ctx, surface);
    }

    // =========================================================================
    //  Swatch selection
    // =========================================================================

    private void SelectSwatch(int index)
    {
        if (index < 0 || index >= _colors.Length) return;
        _selectedColorIndex = index;
        _paletteDirty = true;

        if (index == 0) return;

        if (_colorPicker != null)
        {
            _suppressPickerCallback = true;
            double[] rgba = ArgbToRgba(_colors[index]);
            _colorPicker.SetColor(rgba[0], rgba[1], rgba[2], rgba[3]);
            _suppressPickerCallback = false;
        }
    }

    // =========================================================================
    //  Painting
    // =========================================================================

    private void PaintPixel(int px, int py)
    {
        int idx = py * _data.Width + px;
        if ((uint)idx >= (uint)_data.Pixels.Length) return;

        int newColor = _colors[_selectedColorIndex];
        byte newSwatch = (byte)Math.Min(_selectedColorIndex, 254);

        if (_data.Pixels[idx] == newColor &&
            _pixelSwatchIndex[idx] == newSwatch) return;

        _data.Pixels[idx] = newColor;
        _pixelSwatchIndex[idx] = newSwatch;

        _canvasDirty = true;
        OnChanged?.Invoke(_data);
    }

    // =========================================================================
    //  Utilities
    // =========================================================================

    private static void UnpackArgb(int argb,
        out double a, out double r, out double g, out double b)
    {
        a = ((argb >> 24) & 0xFF) / 255.0;
        r = ((argb >> 16) & 0xFF) / 255.0;
        g = ((argb >> 8) & 0xFF) / 255.0;
        b = (argb & 0xFF) / 255.0;
    }

    private static double[] ArgbToRgba(int argb)
    {
        UnpackArgb(argb, out double a, out double r, out double g, out double b);
        return new double[] { r, g, b, a };
    }

    private static int RgbaToArgb(double[] rgba)
    {
        byte a = (byte)Math.Round(rgba[3] * 255);
        byte r = (byte)Math.Round(rgba[0] * 255);
        byte g = (byte)Math.Round(rgba[1] * 255);
        byte b = (byte)Math.Round(rgba[2] * 255);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    private static bool IsInsideBounds(ElementBounds b, double x, double y)
        => x >= b.renderX && x < b.renderX + b.OuterWidth &&
           y >= b.renderY && y < b.renderY + b.OuterHeight;

    // =========================================================================
    //  Misc overrides
    // =========================================================================

    public override bool Focusable => true;

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        _colorPicker?.OnFocusLost();
    }

    public override void Dispose()
    {
        _canvasTexture.Dispose();
        _paletteTexture.Dispose();
        _colorPicker?.Dispose();
        _colorPicker = null;
        base.Dispose();
    }
}

public static class GuiComposerExtensions
{
    /// <summary>
    /// Adds a canvas editor to the current GUI instance.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="data">The canvas data to edit.</param>
    /// <param name="bounds">The bounds of the canvas editor.</param>
    /// <param name="onChanged">Optional callback fired when the canvas changes.</param>
    /// <param name="clipBounds">
    /// Optional scissor/clip bounds. Set this when the canvas editor is inside a
    /// scrollable area so that it is correctly clipped when scrolled out of view.
    /// </param>
    /// <param name="key">The name of this element.</param>
    public static GuiComposer AddCanvasEditor(
        this GuiComposer composer,
        TextureCanvasData data,
        ElementBounds bounds,
        Action<TextureCanvasData>? onChanged = null,
        ElementBounds clipBounds = null,
        string key = "canvasEditor")
    {
        var element = new GuiElementCanvasEditor(composer.Api, bounds, data, clipBounds);
        if (onChanged != null)
            element.OnChanged += onChanged;
        composer.AddInteractiveElement(element, key);
        return composer;
    }

    public static GuiElementCanvasEditor? GetCanvasEditor(
        this GuiComposer composer,
        string key = "canvasEditor")
        => composer.GetElement(key) as GuiElementCanvasEditor;
}