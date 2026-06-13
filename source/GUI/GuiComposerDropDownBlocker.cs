using System.Reflection;
using Vintagestory.API.Client;

namespace PlayerModelLib;

public static class GuiComposerDropDownBlocker
{
    private static FieldInfo _interactiveElementsField;

    private static FieldInfo InteractiveElementsField
    {
        get
        {
            if (_interactiveElementsField == null)
            {
                _interactiveElementsField = typeof(GuiComposer).GetField(
                    "interactiveElements",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
            }
            return _interactiveElementsField;
        }
    }

    private static Dictionary<string, GuiElement> GetElements(GuiComposer composer)
    {
        return InteractiveElementsField?.GetValue(composer) as Dictionary<string, GuiElement>;
    }

    public static GuiElementScrollableDropDown FindBlockingDropDown(GuiComposer composer, int x, int y)
    {
        Dictionary<string, GuiElement> elements = GetElements(composer);
        if (elements == null) return null;

        foreach (GuiElement element in elements.Values)
        {
            if (element is GuiElementScrollableDropDown dropdown
                && dropdown.listMenu.IsOpened
                && dropdown.listMenu.IsPositionInside(x, y))
            {
                return dropdown;
            }
        }

        return null;
    }

    public static bool IsBlockedByOpenDropDown(GuiComposer composer, int x, int y)
    {
        return FindBlockingDropDown(composer, x, y) != null;
    }

    /// <summary>
    /// Handles MouseDown for the composer manually, stopping iteration
    /// if a dropdown list consumes the event.
    /// </summary>
    public static bool HandleMouseDown(GuiComposer composer, ICoreClientAPI api, MouseEvent args)
    {
        Dictionary<string, GuiElement> elements = GetElements(composer);
        if (elements == null) return false;

        // First pass: check if any open dropdown list owns this position.
        // If so, deliver ONLY to that dropdown and stop.
        foreach (GuiElement element in elements.Values)
        {
            if (element is GuiElementScrollableDropDown dropdown && dropdown.listMenu.IsOpened)
            {
                if (dropdown.listMenu.IsPositionInside(args.X, args.Y))
                {
                    dropdown.OnMouseDown(api, args);
                    args.Handled = true;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Handles MouseUp for the composer manually, stopping iteration
    /// if a dropdown list consumes the event.
    /// </summary>
    public static bool HandleMouseUp(GuiComposer composer, ICoreClientAPI api, MouseEvent args)
    {
        Dictionary<string, GuiElement> elements = GetElements(composer);
        if (elements == null) return false;

        foreach (GuiElement element in elements.Values)
        {
            if (element is GuiElementScrollableDropDown dropdown && dropdown.listMenu.IsOpened)
            {
                if (dropdown.listMenu.IsPositionInside(args.X, args.Y))
                {
                    dropdown.OnMouseUp(api, args);
                    args.Handled = true;
                    return true;
                }
            }
        }

        return false;
    }
}