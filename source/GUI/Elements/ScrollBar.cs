using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace PlayerModelLib;

public class GuiElemenExtendedtScrollbar : GuiElementControl
{
    public static int DefaultScrollbarWidth = 20;
    public static int DefaultScrollbarPadding = 2;

    protected Action<float> onNewScrollbarValue;

    public bool mouseDownOnScrollbarHandle;
    public int mouseDownStartY;

    protected float visibleHeight;
    protected float totalHeight;

    protected float currentHandlePosition;
    protected float currentHandleHeight = 0;

    /// <summary>
    /// When set, only captures mouse input when hovering inside this bounds.
    /// If null, falls back to using Bounds.
    /// </summary>
    protected ElementBounds interactionBounds;

    /// <summary>
    /// When > 0, the handle height is fixed to this value and won't be
    /// recalculated from visible/total height ratio.
    /// </summary>
    protected float fixedHandleHeight = 0;

    public float zOffset;

    protected LoadedTexture handleTexture;

    public override bool Focusable => enabled;

    /// <summary>
    /// Moving 1 pixel of the scrollbar moves the content by ScrollConversionFactor pixels.
    /// </summary>
    public float ScrollConversionFactor
    {
        get
        {
            float movableArea = (float)(Bounds.InnerHeight - currentHandleHeight);
            if (movableArea <= 0) return 1;

            float innerMovableArea = totalHeight - visibleHeight;
            return innerMovableArea / movableArea;
        }
    }

    /// <summary>
    /// The current Y position of the inner element.
    /// </summary>
    public float CurrentYPosition
    {
        get => currentHandlePosition * ScrollConversionFactor;
        set => currentHandlePosition = value / ScrollConversionFactor;
    }

    /// <summary>
    /// Creates a new Scrollbar.
    /// </summary>
    /// <param name="capi">The client API.</param>
    /// <param name="onNewScrollbarValue">The event that fires when the scrollbar is changed.</param>
    /// <param name="bounds">The bounds of the scrollbar.</param>
    /// <param name="interactionBounds">
    /// Optional bounds used for mouse interaction hit-testing.
    /// If null, <paramref name="bounds"/> is used instead.
    /// </param>
    /// <param name="fixedHandleHeight">
    /// When greater than 0 the handle will always be rendered at exactly this
    /// pixel height instead of being derived from the visible/total ratio.
    /// </param>
    public GuiElemenExtendedtScrollbar(
    ICoreClientAPI capi,
    Action<float> onNewScrollbarValue,
    ElementBounds bounds,
    ElementBounds interactionBounds = null,
    float fixedHandleHeight = 0)
    : base(capi, bounds)
    {
        handleTexture = new LoadedTexture(capi);
        this.onNewScrollbarValue = onNewScrollbarValue;
        this.interactionBounds = interactionBounds;
        this.fixedHandleHeight = fixedHandleHeight;

        // If a fixed size was given, apply it immediately so ComposeElements
        // already has a non-zero currentHandleHeight when it calls RecomposeHandle.
        if (fixedHandleHeight > 0)
            currentHandleHeight = fixedHandleHeight;

        currentHandlePosition = 0;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="x"/>/<paramref name="y"/> lie inside
    /// the interaction bounds (or the element bounds if no interaction bounds
    /// were supplied).
    /// </summary>
    private bool IsInsideInteractionBounds(int x, int y)
    {
        if (interactionBounds != null)
            return interactionBounds.PointInside(x, y);

        return Bounds.PointInside(x, y);
    }

    // ── Composition ─────────────────────────────────────────────────────────

    public override void ComposeElements(Context ctxStatic, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        interactionBounds?.CalcWorldBounds();

        ctxStatic.SetSourceRGBA(0, 0, 0, 0.2);
        ElementRoundRectangle(ctxStatic, Bounds, false);
        ctxStatic.Fill();

        EmbossRoundRectangleElement(ctxStatic, Bounds, true);

        RecomposeHandle();
    }

    public virtual void RecomposeHandle()
    {
        Bounds.CalcWorldBounds();

        int w = (int)Bounds.InnerWidth;
        int h = (int)currentHandleHeight;
        if (w <= 0 || h <= 0) return;   // nothing to draw yet

        ImageSurface surface = new ImageSurface(Format.Argb32, w, h);
        Context ctx = genContext(surface);

        RoundRectangle(ctx, 0, 0, w, h, 1);
        ctx.SetSourceRGBA(GuiStyle.DialogHighlightColor);
        ctx.FillPreserve();
        ctx.SetSourceRGBA(0, 0, 0, 0.4);
        ctx.Fill();

        EmbossRoundRectangleElement(ctx, 0, 0, w, h, false, 2, 1);

        generateTexture(surface, ref handleTexture);

        ctx.Dispose();
        surface.Dispose();
    }

    // ── Rendering ───────────────────────────────────────────────────────────

    public override void RenderInteractiveElements(float deltaTime)
    {
        api.Render.Render2DTexturePremultipliedAlpha(
            handleTexture.TextureId,
            (int)(Bounds.renderX + Bounds.absPaddingX),
            (int)(Bounds.renderY + Bounds.absPaddingY + currentHandlePosition),
            (int)Bounds.InnerWidth,
            (int)currentHandleHeight,
            200 + zOffset
        );
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Sets both the visible and total heights, then recalculates the handle.
    /// </summary>
    public void SetHeights(float visibleHeight, float totalHeight)
    {
        this.visibleHeight = visibleHeight;
        SetNewTotalHeight(totalHeight);
    }

    /// <summary>
    /// Updates the total height and recalculates the handle size/position.
    /// </summary>
    public void SetNewTotalHeight(float totalHeight)
    {
        this.totalHeight = totalHeight;

        if (fixedHandleHeight > 0)
        {
            // Clamp against the actual track height which is only valid after
            // CalcWorldBounds has run, so do it here as well.
            currentHandleHeight = Math.Min(fixedHandleHeight, (float)Bounds.InnerHeight);
        }
        else
        {
            float ratio = GameMath.Clamp(visibleHeight / totalHeight, 0f, 1f);
            currentHandleHeight = Math.Max(10f, ratio * (float)Bounds.InnerHeight);
        }

        currentHandlePosition = GameMath.Clamp(
            currentHandlePosition, 0f,
            (float)(Bounds.InnerHeight - currentHandleHeight));

        TriggerChanged();
        RecomposeHandle();
    }

    public void SetScrollbarPosition(int pos)
    {
        currentHandlePosition = Math.Max(0f, pos);
        onNewScrollbarValue(0);
    }

    /// <summary>Triggers the change callback with the current scroll value.</summary>
    public void TriggerChanged()
    {
        onNewScrollbarValue(CurrentYPosition);
    }

    /// <summary>Scrolls to the very bottom of the content.</summary>
    public void ScrollToBottom()
    {
        if (totalHeight < visibleHeight)
        {
            currentHandlePosition = 0;
            onNewScrollbarValue(0);
        }
        else
        {
            currentHandlePosition = (float)(Bounds.InnerHeight - currentHandleHeight);
            onNewScrollbarValue(totalHeight - visibleHeight);
        }
    }

    public void EnsureVisible(double posX, double posY)
    {
        double startY = CurrentYPosition;
        double endY = CurrentYPosition + visibleHeight;

        if (posY < startY)
        {
            float diff = (float)(startY - posY) / ScrollConversionFactor;
            currentHandlePosition = Math.Max(0f, currentHandlePosition - diff);
            TriggerChanged();
            return;
        }

        if (posY > endY)
        {
            float diff = (float)(posY - endY) / ScrollConversionFactor;
            currentHandlePosition = (float)Math.Min(
                Bounds.InnerHeight - currentHandleHeight,
                currentHandlePosition + diff);
            TriggerChanged();
        }
    }

    // ── Input ───────────────────────────────────────────────────────────────

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        // Only react when the pointer is inside the interaction area.
        if (!IsInsideInteractionBounds(api.Input.MouseX, api.Input.MouseY)) return;
        if (Bounds.InnerHeight <= currentHandleHeight + 0.001f) return;

        float y = currentHandlePosition
                  - (float)scaled(102) * args.deltaPrecise / ScrollConversionFactor;

        double movable = Bounds.InnerHeight - currentHandleHeight;
        currentHandlePosition = (float)GameMath.Clamp(y, 0, movable);
        TriggerChanged();

        args.SetHandled(true);
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (Bounds.InnerHeight <= currentHandleHeight + 0.001f) return;

        // Only start dragging when the click is inside the interaction bounds.
        if (!IsInsideInteractionBounds(args.X, args.Y)) return;

        mouseDownOnScrollbarHandle = true;
        mouseDownStartY = GameMath.Max(0, args.Y - (int)Bounds.renderY, 0);

        if (mouseDownStartY > currentHandleHeight)
            mouseDownStartY = (int)currentHandleHeight / 2;

        UpdateHandlePositionAbs(args.Y - (int)Bounds.renderY - mouseDownStartY);
        args.Handled = true;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        mouseDownOnScrollbarHandle = false;
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        if (mouseDownOnScrollbarHandle)
        {
            UpdateHandlePositionAbs(args.Y - (int)Bounds.renderY - mouseDownStartY);
        }
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!hasFocus) return;

        if (args.KeyCode == (int)GlKeys.Down || args.KeyCode == (int)GlKeys.Up)
        {
            float direction = args.KeyCode == (int)GlKeys.Down ? -0.5f : 0.5f;
            float y = currentHandlePosition
                      - (float)scaled(102) * direction / ScrollConversionFactor;

            double movable = Bounds.InnerHeight - currentHandleHeight;
            currentHandlePosition = (float)GameMath.Clamp(y, 0, movable);
            TriggerChanged();
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void UpdateHandlePositionAbs(float y)
    {
        double movable = Bounds.InnerHeight - currentHandleHeight;
        currentHandlePosition = (float)GameMath.Clamp(y, 0, movable);
        TriggerChanged();
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    public override void Dispose()
    {
        base.Dispose();
        handleTexture.Dispose();
    }

    /// <summary>
    /// Changes the fixed handle height after creation and recomposes the handle texture.
    /// Pass 0 to revert to proportional sizing (requires SetHeights to have been called).
    /// </summary>
    public void SetFixedHandleHeight(float height)
    {
        fixedHandleHeight = height;

        if (fixedHandleHeight > 0)
        {
            currentHandleHeight = Math.Min(fixedHandleHeight, (float)Bounds.InnerHeight);
        }
        else
        {
            // Fall back to proportional sizing using whatever heights were set last.
            float ratio = GameMath.Clamp(visibleHeight / totalHeight, 0f, 1f);
            currentHandleHeight = Math.Max(10f, ratio * (float)Bounds.InnerHeight);
        }

        // Keep handle inside the track.
        currentHandlePosition = GameMath.Clamp(
            currentHandlePosition, 0f,
            (float)(Bounds.InnerHeight - currentHandleHeight));

        TriggerChanged();
        RecomposeHandle();
    }
}

// ── Composer helpers ─────────────────────────────────────────────────────────

public static partial class GuiComposerHelpers
{
    /// <summary>
    /// Adds a vertical scrollbar to the GUI.
    /// </summary>
    /// <param name="composer"></param>
    /// <param name="onNewScrollbarValue">Callback invoked whenever the scroll position changes.</param>
    /// <param name="bounds">Visual bounds of the scrollbar track.</param>
    /// <param name="key">Optional element key.</param>
    /// <param name="interactionBounds">
    /// Optional bounds used for mouse hit-testing.  When supplied, mouse
    /// events are only captured while the cursor is inside this region.
    /// </param>
    /// <param name="fixedHandleHeight">
    /// When greater than 0 the handle is rendered at exactly this height
    /// (in pixels) instead of being sized proportionally.
    /// </param>
    public static GuiComposer AddExtendedVerticalScrollbar(
        this GuiComposer composer,
        Action<float> onNewScrollbarValue,
        ElementBounds bounds,
        string key = null,
        ElementBounds interactionBounds = null,
        float fixedHandleHeight = 0)
    {
        if (!composer.Composed)
        {
            composer.AddInteractiveElement(
                new GuiElemenExtendedtScrollbar(
                    composer.Api,
                    onNewScrollbarValue,
                    bounds,
                    interactionBounds,
                    fixedHandleHeight),
                key);
        }
        return composer;
    }

    /// <summary>Gets a scrollbar element by key.</summary>
    public static GuiElemenExtendedtScrollbar GetExtendedScrollbar(this GuiComposer composer, string key)
        => (GuiElemenExtendedtScrollbar)composer.GetElement(key);
}