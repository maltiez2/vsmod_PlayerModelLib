using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace PlayerModelLib;

public class GuiElementIndexScroller : GuiElementControl
{
    // ── Configuration ────────────────────────────────────────────────────────

    private int _minIndex = 0;
    private int _maxIndex = 10;
    private int _currentIndex = 0;

    private Action<int> _onIndexChanged;

    /// <summary>Optional bounds used for mouse-wheel hit-testing.</summary>
    private ElementBounds _interactionBounds;

    public float zOffset;

    // ── Textures ─────────────────────────────────────────────────────────────

    private LoadedTexture _handleTexture;
    private LoadedTexture _arrowUpTexture;
    private LoadedTexture _arrowDownTexture;

    // ── Arrow button geometry ─────────────────────────────────────────────────

    /// <summary>Height in pixels of each arrow button at the top/bottom.</summary>
    private int ArrowHeight => (int)scaled(18);

    /// <summary>Bounds of the track area between the two arrows.</summary>
    private double TrackHeight => Bounds.InnerHeight - 2 * ArrowHeight;

    // ── Handle geometry ───────────────────────────────────────────────────────

    /// <summary>Height of the draggable handle in pixels.</summary>
    private float _handleHeight;

    /// <summary>Current Y offset of the handle inside the track (pixels).</summary>
    private float _handlePosition;

    // ── Drag state ────────────────────────────────────────────────────────────

    private bool _dragging;
    private int _dragStartY;
    private float _dragStartHandlePos;

    // ── Properties ────────────────────────────────────────────────────────────

    public override bool Focusable => enabled;

    /// <summary>Current integer index value.</summary>
    public int CurrentIndex => _currentIndex;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="capi">Client API.</param>
    /// <param name="onIndexChanged">Callback fired whenever the index changes.</param>
    /// <param name="bounds">Visual bounds of the whole element.</param>
    /// <param name="maxIndex">Inclusive upper bound (min is always 0).</param>
    /// <param name="interactionBounds">
    ///     Optional region used for mouse-wheel hit-testing.
    ///     Falls back to <paramref name="bounds"/> when null.
    /// </param>
    public GuiElementIndexScroller(
        ICoreClientAPI capi,
        Action<int> onIndexChanged,
        ElementBounds bounds,
        int maxIndex,
        ElementBounds interactionBounds = null)
        : base(capi, bounds)
    {
        _onIndexChanged = onIndexChanged;
        _maxIndex = Math.Max(0, maxIndex);
        _interactionBounds = interactionBounds;

        _handleTexture = new LoadedTexture(capi);
        _arrowUpTexture = new LoadedTexture(capi);
        _arrowDownTexture = new LoadedTexture(capi);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool InsideInteraction(int x, int y)
        => _interactionBounds != null
            ? _interactionBounds.PointInside(x, y)
            : Bounds.PointInside(x, y);

    /// <summary>Number of discrete steps (= maxIndex - minIndex).</summary>
    private int Steps => _maxIndex - _minIndex;

    /// <summary>
    /// Converts a handle position (pixels from top of track) to an index.
    /// </summary>
    private int HandlePosToIndex(float pos)
    {
        if (Steps <= 0) return _minIndex;
        double ratio = pos / Math.Max(1, TrackHeight - _handleHeight);
        return _minIndex + (int)Math.Round(ratio * Steps);
    }

    /// <summary>
    /// Converts an index to a handle position (pixels from top of track).
    /// </summary>
    private float IndexToHandlePos(int index)
    {
        if (Steps <= 0) return 0f;
        double ratio = (double)(index - _minIndex) / Steps;
        return (float)(ratio * (TrackHeight - _handleHeight));
    }

    /// <summary>Recalculates handle height based on track size.</summary>
    private void RecalcHandleHeight()
    {
        // Handle occupies 1/(steps+1) of the track, minimum 10 px.
        double fraction = Steps > 0 ? 1.0 / (Steps + 1) : 1.0;
        _handleHeight = (float)Math.Max(10, TrackHeight * fraction);
    }

    // ── Composition ───────────────────────────────────────────────────────────

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        _interactionBounds?.CalcWorldBounds();

        // Track background
        ctxStatic.SetSourceRGBA(0, 0, 0, 0.2);
        ElementRoundRectangle(ctxStatic, Bounds, false);
        ctxStatic.Fill();
        EmbossRoundRectangleElement(ctxStatic, Bounds, true);

        RecalcHandleHeight();
        _handlePosition = IndexToHandlePos(_currentIndex);

        RecomposeHandle();
        RecomposeArrows();
    }

    private void RecomposeHandle()
    {
        Bounds.CalcWorldBounds();

        int w = (int)Bounds.InnerWidth;
        int h = (int)_handleHeight;
        if (w <= 0 || h <= 0) return;

        ImageSurface surf = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surf);

        RoundRectangle(ctx, 0, 0, w, h, 1);
        ctx.SetSourceRGBA(GuiStyle.DialogHighlightColor);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0, 0, 0, 0.4);
        ctx.Fill();
        EmbossRoundRectangleElement(ctx, 0, 0, w, h, false, 2, 1);

        generateTexture(surf, ref _handleTexture);
        ctx.Dispose();
        surf.Dispose();
    }

    private void RecomposeArrows()
    {
        Bounds.CalcWorldBounds();

        int w = (int)Bounds.InnerWidth;
        int h = ArrowHeight;
        if (w <= 0 || h <= 0) return;

        ComposeArrow(ref _arrowUpTexture, w, h, isUp: true);
        ComposeArrow(ref _arrowDownTexture, w, h, isUp: false);
    }

    private void ComposeArrow(ref LoadedTexture tex, int w, int h, bool isUp)
    {
        ImageSurface surf = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surf);

        // Button background
        RoundRectangle(ctx, 0, 0, w, h, 1);
        ctx.SetSourceRGBA(GuiStyle.DialogDefaultBgColor);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0, 0, 0, 0.3);
        ctx.Fill();
        EmbossRoundRectangleElement(ctx, 0, 0, w, h, false, 1, 1);

        // Arrow triangle
        double cx = w / 2.0;
        double cy = h / 2.0;
        double size = Math.Min(w, h) * 0.3;

        ctx.SetSourceRGBA(1, 1, 1, 0.8);
        ctx.NewPath();

        if (isUp)
        {
            ctx.MoveTo(cx, cy - size);
            ctx.LineTo(cx + size, cy + size * 0.6);
            ctx.LineTo(cx - size, cy + size * 0.6);
        }
        else
        {
            ctx.MoveTo(cx, cy + size);
            ctx.LineTo(cx + size, cy - size * 0.6);
            ctx.LineTo(cx - size, cy - size * 0.6);
        }

        ctx.ClosePath();
        ctx.Fill();

        generateTexture(surf, ref tex);
        ctx.Dispose();
        surf.Dispose();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    public override void RenderInteractiveElements(float deltaTime)
    {
        int x = (int)(Bounds.renderX + Bounds.absPaddingX);
        int y = (int)(Bounds.renderY + Bounds.absPaddingY);
        int w = (int)Bounds.InnerWidth;
        float depth = 200 + zOffset;

        // Up arrow
        api.Render.Render2DTexturePremultipliedAlpha(
            _arrowUpTexture.TextureId,
            x, y, w, ArrowHeight, depth);

        // Handle (offset into track area, below up-arrow)
        api.Render.Render2DTexturePremultipliedAlpha(
            _handleTexture.TextureId,
            x,
            (int)(y + ArrowHeight + _handlePosition),
            w,
            (int)_handleHeight,
            depth);

        // Down arrow
        api.Render.Render2DTexturePremultipliedAlpha(
            _arrowDownTexture.TextureId,
            x,
            (int)(y + ArrowHeight + TrackHeight),
            w, ArrowHeight, depth);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Changes the maximum index and resets the current index if it is now out of range.
    /// </summary>
    public void SetMaxIndex(int maxIndex)
    {
        _maxIndex = Math.Max(0, maxIndex);
        _currentIndex = GameMath.Clamp(_currentIndex, _minIndex, _maxIndex);

        RecalcHandleHeight();
        _handlePosition = IndexToHandlePos(_currentIndex);
        RecomposeHandle();
    }

    /// <summary>Programmatically sets the current index and fires the callback.</summary>
    public void SetIndex(int index, bool triggerCallback = true)
    {
        int clamped = GameMath.Clamp(index, _minIndex, _maxIndex);
        if (clamped == _currentIndex) return;

        _currentIndex = clamped;
        _handlePosition = IndexToHandlePos(_currentIndex);

        if (triggerCallback)
            _onIndexChanged(_currentIndex);
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        if (!InsideInteraction(api.Input.MouseX, api.Input.MouseY)) return;

        // Scroll up => index decreases, scroll down => index increases
        int delta = args.deltaPrecise > 0 ? -1 : 1;
        ApplyDelta(delta);

        args.SetHandled(true);
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (!InsideInteraction(args.X, args.Y)) return;

        int localY = args.Y - (int)Bounds.renderY - (int)Bounds.absPaddingY;

        // Click on up arrow
        if (localY < ArrowHeight)
        {
            ApplyDelta(-1);
            args.Handled = true;
            return;
        }

        // Click on down arrow
        if (localY >= ArrowHeight + TrackHeight)
        {
            ApplyDelta(1);
            args.Handled = true;
            return;
        }

        // Click on track — begin drag
        _dragging = true;
        _dragStartY = args.Y;
        _dragStartHandlePos = _handlePosition;
        args.Handled = true;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        _dragging = false;
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (!_dragging) return;

        float delta = args.Y - _dragStartY;
        float newPos = GameMath.Clamp(
            _dragStartHandlePos + delta,
            0f,
            (float)(TrackHeight - _handleHeight));

        int newIndex = HandlePosToIndex(newPos);

        // Snap handle to the nearest discrete position
        _handlePosition = IndexToHandlePos(newIndex);

        if (newIndex != _currentIndex)
        {
            _currentIndex = newIndex;
            _onIndexChanged(_currentIndex);
        }
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!hasFocus) return;

        if (args.KeyCode == (int)GlKeys.Up)
        {
            ApplyDelta(-1);
            args.Handled = true;
        }
        else if (args.KeyCode == (int)GlKeys.Down)
        {
            ApplyDelta(1);
            args.Handled = true;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ApplyDelta(int delta)
    {
        int newIndex = GameMath.Clamp(_currentIndex + delta, _minIndex, _maxIndex);
        if (newIndex == _currentIndex) return;

        _currentIndex = newIndex;
        _handlePosition = IndexToHandlePos(_currentIndex);
        _onIndexChanged(_currentIndex);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public override void Dispose()
    {
        base.Dispose();
        _handleTexture?.Dispose();
        _arrowUpTexture?.Dispose();
        _arrowDownTexture?.Dispose();
    }
}

// ── Composer helpers ──────────────────────────────────────────────────────────

public static partial class GuiComposerHelpers
{
    /// <summary>
    /// Adds an index scroller that steps through integers 0 … <paramref name="maxIndex"/>.
    /// Each scroll event (wheel, arrow button, or keyboard) moves by exactly 1.
    /// </summary>
    /// <param name="composer">The composer to add the element to.</param>
    /// <param name="onIndexChanged">Callback receiving the new integer index.</param>
    /// <param name="bounds">Visual bounds of the element.</param>
    /// <param name="maxIndex">Inclusive upper bound (lower bound is always 0).</param>
    /// <param name="key">Optional element key for later retrieval.</param>
    /// <param name="interactionBounds">
    ///     Optional region used for mouse-wheel hit-testing.
    ///     Falls back to <paramref name="bounds"/> when null.
    /// </param>
    public static GuiComposer AddIndexScroller(
        this GuiComposer composer,
        Action<int> onIndexChanged,
        ElementBounds bounds,
        int maxIndex,
        string key = null,
        ElementBounds interactionBounds = null)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(
                new GuiElementIndexScroller(
                    composer.Api,
                    onIndexChanged,
                    bounds,
                    maxIndex,
                    interactionBounds),
                key);
        }
        return composer;
    }

    /// <summary>Retrieves an index scroller element by key.</summary>
    public static GuiElementIndexScroller GetIndexScroller(
        this GuiComposer composer, string key)
        => (GuiElementIndexScroller)composer.GetElement(key);
}