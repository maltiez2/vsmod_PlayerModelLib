using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public enum EnumCreateCharacterTabs
{
    Model,
    Skin,
    Class
}

public sealed class GuiDialogCreateCustomCharacter : GuiDialogCreateCharacter
{
    public override float ZSize
    {
        get { return (float)GuiElement.scaled(280); }
    }
    public override bool PrefersUngrabbedMouse => true;
    public override string? ToggleKeyCombinationCode => null;
    public static bool DialogOpened { get; private set; } = false;

    public static int RenderState { get; private set; } = 0; // Is used to disable model size compensation when rendering model in model tab

    public GuiDialogCreateCustomCharacter(ICoreClientAPI api, CharacterSystem characterSystem) : base(api, characterSystem)
    {
        _characterSystem = characterSystem;
        _customModelsSystem = api.ModLoader.GetModSystem<CustomModelsSystem>();
        _api = api;
    }
    public override void OnGuiOpened()
    {
        DialogOpened = true;

        string? characterClass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
        if (characterClass != null && _characterSystem.characterClassesByCode.ContainsKey(characterClass))
        {
            _characterSystem.setCharacterClass(capi.World.Player.Entity, characterClass, true);
        }
        else
        {
            _characterSystem.setCharacterClass(capi.World.Player.Entity, _characterSystem.characterClasses[0].Code, true);
        }

        try
        {
            ComposeGuis();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }

        EntityShapeRenderer? renderer = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();

        if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Guest || capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Survival)
        {
            _characterInventory?.Open(capi.World.Player);
        }

        _ = ChangeModel(0);
    }
    public override void OnGuiClosed()
    {
        DialogOpened = false;

        if (_characterInventory != null)
        {
            _characterInventory.Close(capi.World.Player);
            Composers["createcharacter"].GetSlotGrid("leftSlots")?.OnGuiClosed(capi);
            Composers["createcharacter"].GetSlotGrid("rightSlots")?.OnGuiClosed(capi);
        }

        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (skinMod == null) return;

        List<CharacterClass> availableClasses = GetAvailableClasses(system, skinMod.CurrentModelCode);

        CharacterClass characterClass = availableClasses[_currentClassIndex];

        if (_clientSelectionDone != null)
        {
            _clientSelectionDone.Invoke(_characterSystem, [_characterInventory, characterClass.Code, _didSelect]);
        }

        system.SynchronizePlayerModel(skinMod.CurrentModelCode);
        system.SynchronizePlayerModelSize(_currentModelSize);

        EntityBehaviorPlayerInventory? invBhv = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        if (invBhv != null)
        {
            invBhv.hideClothing = false;
        }

        ReTesselate();

        RenderState = 0;
    }
    public override bool CaptureAllInputs()
    {
        return IsOpened();
    }
    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (focused && _insetSlotBounds?.PointInside(capi.Input.MouseX, capi.Input.MouseY) == true && _currentTab == EnumCreateCharacterTabs.Skin)
        {
            _charZoom = GameMath.Clamp(_charZoom + args.deltaPrecise / 10f, 0.5f, 1.2f);
            _height = GameMath.Clamp(_height, -_heightLimit * _charZoom, _heightLimit * _charZoom);
            args.SetHandled();
            return;
        }

        GuiComposer[] array = Composers.ToArray();
        GuiComposer[] array2 = array;
        for (int i = 0; i < array2.Length; i++)
        {
            array2[i].OnMouseWheel(args);
            if (args.IsHandled)
            {
                return;
            }
        }

        if (!focused)
        {
            return;
        }

        array2 = array;
        for (int i = 0; i < array2.Length; i++)
        {
            if (array2[i].Bounds.PointInside(capi.Input.MouseX, capi.Input.MouseY))
            {
                args.SetHandled();
            }
        }

        if (_insetSlotBounds?.PointInside(capi.Input.MouseX, capi.Input.MouseY) == true && _currentTab == EnumCreateCharacterTabs.Skin)
        {
            _charZoom = GameMath.Clamp(_charZoom + args.deltaPrecise / 5f, 0.5f, 1f);
        }
    }
    public override void OnMouseDown(MouseEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        GuiComposer[] array = Composers.ToArray();
        GuiComposer[] array2 = array;
        for (int i = 0; i < array2.Length; i++)
        {
            array2[i].OnMouseDown(args);
            if (args.Handled)
            {
                return;
            }
        }

        if (!IsOpened())
        {
            return;
        }

        array2 = array;
        for (int i = 0; i < array2.Length; i++)
        {
            if (array2[i].Bounds.PointInside(args.X, args.Y))
            {
                args.Handled = true;
                break;
            }
        }

        _rotateCharacter = _insetSlotBounds?.PointInside(args.X, args.Y) ?? false;
    }
    public override void OnMouseUp(MouseEvent args)
    {
        base.OnMouseUp(args);

        _rotateCharacter = false;
    }
    public override void OnMouseMove(MouseEvent args)
    {
        base.OnMouseMove(args);

        if (_rotateCharacter)
        {
            _yaw -= args.DeltaX / 100f;
            _height += args.DeltaY / 2f;
            _height = GameMath.Clamp(_height, -_heightLimit * _charZoom, _heightLimit * _charZoom);
        }
    }
    public override void OnRenderGUI(float deltaTime)
    {
        if (_insetSlotBounds == null) return;

        foreach ((_, GuiComposer composer) in Composers)
        {
            composer.Render(deltaTime);
            MouseOverCursor = composer.MouseOverCursor;
        }

        if (capi.IsGamePaused)
        {
            capi.World.Player.Entity.talkUtil.OnGameTick(deltaTime);
        }

        capi.Render.GlPushMatrix();

        if (focused) { capi.Render.GlTranslate(0, 0, 150); }

        capi.Render.GlRotate(-14, 1, 0, 0);

        _mat.Identity();
        _mat.RotateXDeg(-14);
        Vec4f lightRot = _mat.TransformVector(_lighPos);
        double pad = GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);

        capi.Render.CurrentActiveShader.Uniform("lightPosition", new Vec3f(lightRot.X, lightRot.Y, lightRot.Z));

        capi.Render.PushScissor(_insetSlotBounds);

        RenderState = (int)_currentTab + 1;

        switch (_currentTab)
        {
            case EnumCreateCharacterTabs.Model:
                capi.Render.RenderEntityToGui(
                    deltaTime,
                    capi.World.Player.Entity,
                    _insetSlotBounds.renderX + pad - GuiElement.scaled(115),
                    _insetSlotBounds.renderY + pad - GuiElement.scaled(-40),
                    (float)GuiElement.scaled(230),
                    _yaw,
                    (float)GuiElement.scaled(205),
                    ColorUtil.WhiteArgb);
                break;
            case EnumCreateCharacterTabs.Skin:
                capi.Render.RenderEntityToGui(
                    deltaTime,
                    capi.World.Player.Entity,
                    _insetSlotBounds.renderX + pad - GuiElement.scaled(195) * _charZoom + GuiElement.scaled(115 * (1 - _charZoom)),
                    _insetSlotBounds.renderY + pad + GuiElement.scaled(10 * (1 - _charZoom)) + GuiElement.scaled(_height),
                    (float)GuiElement.scaled(230),
                    _yaw,
                    (float)GuiElement.scaled(330 * _charZoom),
                    ColorUtil.WhiteArgb);
                break;
            case EnumCreateCharacterTabs.Class:
                capi.Render.RenderEntityToGui(
                    deltaTime,
                    capi.World.Player.Entity,
                    _insetSlotBounds.renderX + pad - GuiElement.scaled(111),
                    _insetSlotBounds.renderY + pad - GuiElement.scaled(-7),
                    (float)GuiElement.scaled(230),
                    _yaw,
                    (float)GuiElement.scaled(205),
                    ColorUtil.WhiteArgb);
                break;
        }

        capi.Render.PopScissor();

        capi.Render.CurrentActiveShader.Uniform("lightPosition", new Vec3f(1, -1, 0).Normalize());

        capi.Render.GlPopMatrix();
    }

    public static string GetAttributeDescription(string attribute, double value)
    {
        string defaultKey = string.Format(GlobalConstants.DefaultCultureInfo, _attributeLangPrefix + "-{0}-{1}", attribute, value);
        string newKey = $"{_attributeLangPrefix}-{attribute}";

        if (Lang.HasTranslation(defaultKey))
        {
            return Lang.Get(defaultKey);
        }
        else if (Lang.HasTranslation(newKey))
        {
            return string.Format(GlobalConstants.DefaultCultureInfo, Lang.Get(newKey), value);
        }
        else
        {
            return Lang.Get(defaultKey);
        }
    }

    private const string _attributeLangPrefix = "charattribute";
    private bool _didSelect = false;
    private IInventory? _characterInventory;
    private ElementBounds? _insetSlotBounds;
    private readonly CharacterSystem _characterSystem;
    private readonly CustomModelsSystem _customModelsSystem;
    private int _currentClassIndex = 0;
    private EnumCreateCharacterTabs _currentTab = 0;
    private float _charZoom = 1f;
    private bool _charNaked = true;
    private readonly int _dlgHeight = 433 + 80 + 33;
    private float _yaw = -GameMath.PIHALF + 0.3f;
    private float _height = 0;
    private const float _heightLimit = 150;
    private bool _rotateCharacter;
    private readonly Vec4f _lighPos = new Vec4f(-1, -1, 0, 0).NormalizeXYZ();
    private readonly Matrixf _mat = new();
    private readonly MethodInfo? _clientSelectionDone = typeof(CharacterSystem).GetMethod("ClientSelectionDone", BindingFlags.NonPublic | BindingFlags.Instance);
    private float _currentModelSize = 1f;
    private GuiComposer? _composer;
    private float _clipHeight = 377;
    private float _clipHeightModel = 200;
    internal static bool _applyScrollPatch = false;
    private ICoreClientAPI _api;
    private const string _previousSelectionFile = "playermodellib-previous-selections.json";

    private new void ComposeGuis()
    {
        _applyScrollPatch = true;

        double padding = GuiElementItemSlotGridBase.unscaledSlotPadding;
        double slotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
        double yPosition = 20 + padding;

        _characterInventory = capi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ElementBounds tabBounds = ElementBounds.Fixed(0, -25, 450, 25);

        ElementBounds backgroundBounds = ElementBounds.FixedSize(717, _dlgHeight)
            .WithFixedPadding(GuiStyle.ElementToDialogPadding);

        ElementBounds dialogBounds = ElementBounds.FixedSize(757, _dlgHeight + 40)
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);

        GuiTab[] tabs = [
            new() { Name = Lang.Get("tab-model"), DataInt = 0 },
            new() { Name = Lang.Get("tab-skinandvoice"), DataInt = 1 },
            new() { Name = Lang.Get("tab-charclass"), DataInt = 2 },
        ];

        string tabTitle = _currentTab switch
        {
            EnumCreateCharacterTabs.Model => Lang.Get("Select model"),
            EnumCreateCharacterTabs.Skin => Lang.Get("Customize Skin"),
            EnumCreateCharacterTabs.Class => Lang.Get("Select character class"),
            _ => ""
        };

        GuiComposer composer = capi.Gui
            .CreateCompo("createcharacter", dialogBounds)
            .AddShadedDialogBG(backgroundBounds, true)
            .AddDialogTitleBar(tabTitle, OnTitleBarClose)
            .AddHorizontalTabs(tabs, tabBounds, onTabClicked, CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), "tabs")
            .BeginChildElements(backgroundBounds);

        _composer = composer;

        Composers["createcharacter"] = composer;

        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();
        EntityBehaviorPlayerInventory inventoryBehavior = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>() ?? throw new Exception();
        inventoryBehavior.hideClothing = false;

        switch (_currentTab)
        {
            case EnumCreateCharacterTabs.Model:
                ComposeModelTab(composer, system, yPosition, padding, slotSize);
                break;
            case EnumCreateCharacterTabs.Skin:
                ComposeSkinTab(composer, inventoryBehavior, yPosition, padding, backgroundBounds);
                break;
            case EnumCreateCharacterTabs.Class:
                ComposeClassTab(composer, yPosition, padding, slotSize);
                break;
        }

        GuiElementHorizontalTabs tabElem = composer.GetHorizontalTabs("tabs");
        tabElem.unscaledTabSpacing = 20;
        tabElem.unscaledTabPadding = 10;
        tabElem.activeElement = (int)_currentTab;

        try
        {
            composer.Compose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _applyScrollPatch = false;
            return;
        }

        ScrollPatches.Composed();
    }

    private void ComposeModelTab(GuiComposer composer, CustomModelsSystem system, double yPosition, double padding, double slotSize)
    {
        PlayerSkinBehavior? skinBehavior = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        if (skinBehavior == null) return;

        _ = GetCurrentModelAndGroup(system, skinBehavior, out int modelIndex, out int groupIndex);
        GetCustomGroups(system, out string[] groupValues, out string[] groupNames);
        GetCustomModels(system, groupValues[groupIndex], out string[] modelValues, out string[] modelNames, out AssetLocation?[] modelIcons, out AssetLocation? groupIcon);

        yPosition -= 30;

        double insetTopY = yPosition + 30;
        double insetHeight = _dlgHeight - 26 - insetTopY + 33;

        ElementBounds leftColBounds = ElementBounds.Fixed(0, yPosition, 0, _dlgHeight - 47).FixedGrow(padding, padding);
        _insetSlotBounds = ElementBounds.Fixed(0, insetTopY, 190, insetHeight).FixedRightOf(leftColBounds, -10);

        ElementBounds groupTextBounds = ElementBounds.Fixed(0, insetTopY, 525, slotSize - 4 - 8).FixedRightOf(_insetSlotBounds, 10);
        ElementBounds groupDropBoxInset = groupTextBounds.ForkBoundingParent(4, 4, 4, 4);

        CairoFont font = CairoFont.WhiteMediumText();
        groupTextBounds.fixedY += (groupTextBounds.fixedHeight - font.GetFontExtents().Height / RuntimeEnv.GUIScale) / 2;

        CairoFont dropDownFont = CairoFont.WhiteSmallishText();

        // Model selector with arrows and icons - positioned below group dropdown with reduced spacing
        double modelSelectorY = yPosition + 30 + slotSize + 5;

        double iconSize = 128;
        double iconInsetPadding = 1; // Padding inside inset for the icon
        double iconInsetSize = iconSize + (iconInsetPadding * 2) - 25; // Inset size includes padding
        double groupIconInsetSize = 176 + (iconInsetPadding * 2) - 38 + 3;
        double nameHeight = 30;
        double iconSpacing = 10;
        double totalIconHeight = iconInsetSize + 5 + nameHeight; // icon inset + spacing + name

        double iconContainerWidth = (iconInsetSize * 3) + (iconSpacing * 2);

        // Arrow buttons - same height as icons + name
        double buttonHeight = iconInsetSize - 4;

        // Size slider


        // Scrollable description area
        int visibleHeight = (int)Math.Max(120, _dlgHeight - (modelSelectorY + totalIconHeight + 20 + 20 + 20) + 7);
        ElementBounds descriptionTextBounds = ElementBounds.Fixed(0, 0, 500, visibleHeight)
            .FixedUnder(groupDropBoxInset, 160)
            .FixedRightOf(_insetSlotBounds, 10); // Changed from 10 to 5

        ElementBounds bgBounds = descriptionTextBounds.ForkBoundingParent(6, 6, 6, 6);
        ElementBounds clipBounds = descriptionTextBounds.FlatCopy().FixedGrow(6, 11).WithFixedOffset(0, -6);
        ElementBounds scrollbarBounds = descriptionTextBounds.CopyOffsetedSibling(descriptionTextBounds.fixedWidth + 7, -6, 0, 12).WithFixedWidth(20);

        ElementBounds sizeTextBounds = ElementBounds.Fixed(0, modelSelectorY + totalIconHeight + 20, 100, 20).FixedUnder(scrollbarBounds, 5).FixedRightOf(_insetSlotBounds, 10);
        ElementBounds sizeSliderBounds = ElementBounds.Fixed(0, modelSelectorY + totalIconHeight + 20, 290, 20).FixedUnder(scrollbarBounds, 5).FixedRightOf(sizeTextBounds, 0);

        composer.AddInset(_insetSlotBounds, 2);
        composer.AddInset(groupDropBoxInset, 2);

        // Group dropdown
        GuiElementDropDown groupDropDown = new(
            composer.Api,
            groupValues,
            groupNames,
            groupIndex,
            (variantCode, selected) => onToggleModelGroup(variantCode, composer),
            groupTextBounds,
            dropDownFont.Clone().WithOrientation(EnumTextOrientation.Left),
            multiSelect: false);

        composer.AddInteractiveElement(groupDropDown, "groupDropdown");

        // Three model icons in insets (always display 3 slots)
        AssetLocation emptyTexture = new("playermodellib", "textures/icons/empty.png");

        //double baseXOffset = 5 + 35 + 10; // Changed from 10 to 5 (after inset + left button + spacing)

        int displayIndex = modelValues.Length == 1 ?
                (0 == 1 ? 0 : -1) : // Single model: only show in center
                GameMath.Mod(modelIndex - 1 + 0, modelValues.Length);

        ElementBounds groupIconInsetBounds = ElementBounds.Fixed(
            0,
            modelSelectorY,
            iconInsetSize - 1,
            groupIconInsetSize
        ).FixedRightOf(_insetSlotBounds, 10);
        composer.AddInset(groupIconInsetBounds, 2);
        ElementBounds groupIconBounds = ElementBounds.Fixed(
                0,
                modelSelectorY + iconInsetPadding,
                iconSize - 25,
                176 - 40 + 6
            ).FixedRightOf(_insetSlotBounds, 10 + iconInsetPadding + 0);

        // Add icon (empty texture if no model to display)
        AssetLocation iconToDisplay = groupIcon ?? emptyTexture;
        float iconAlpha = (groupIcon != null) ? 1 : 0;
        float iconBrightness = 1;
        composer.AddExtendedImage(groupIconBounds, iconToDisplay, iconBrightness, iconAlpha, (int)GuiElement.scaled(groupIconBounds.fixedWidth), (int)GuiElement.scaled(groupIconBounds.fixedHeight));


        // *********** LEFT ICON **************

        ElementBounds leftIconInsetBounds = ElementBounds.Fixed(
            0,
            modelSelectorY,
            iconInsetSize,
            iconInsetSize
        ).FixedRightOf(groupIconBounds, 18);
        composer.AddInset(leftIconInsetBounds, 2);
        ElementBounds leftIconBounds = ElementBounds.Fixed(
                0,
                modelSelectorY + iconInsetPadding,
                iconSize - 25,
                iconSize - 25
            ).FixedRightOf(groupIconBounds, 18 + iconInsetPadding + 0);

        // Add icon (empty texture if no model to display)
        iconToDisplay = (displayIndex >= 0 && displayIndex < modelIcons.Length && modelIcons[displayIndex] != null)
            ? modelIcons[displayIndex] ?? emptyTexture
            : emptyTexture;
        iconAlpha = (displayIndex >= 0 && displayIndex < modelIcons.Length && modelIcons[displayIndex] != null) ? 1 : 0;
        iconBrightness = (displayIndex >= 0 && displayIndex < modelIcons.Length && modelIcons[displayIndex] != null) ? 0.5f : 1;
        composer.AddExtendedImage(leftIconBounds, iconToDisplay, iconBrightness, iconAlpha, (int)GuiElement.scaled(leftIconBounds.fixedWidth), (int)GuiElement.scaled(leftIconBounds.fixedHeight));


        // *********** LEFT BUTTON **************

        ElementBounds prevButtonBounds = ElementBounds.Fixed(0, modelSelectorY, 35, buttonHeight).WithFixedPadding(2).FixedRightOf(leftIconInsetBounds, 5);

        // *********** MIDDLE ICON **************

        displayIndex = modelValues.Length == 1 ?
                (1 == 1 ? 0 : -1) : // Single model: only show in center
                GameMath.Mod(modelIndex - 1 + 1, modelValues.Length);

        ElementBounds middleIconInsetBounds = ElementBounds.Fixed(
            0,
            modelSelectorY,
            iconInsetSize,
            iconInsetSize
        ).FixedRightOf(prevButtonBounds, 8);
        composer.AddInset(middleIconInsetBounds, 2);
        ElementBounds middleIconBounds = ElementBounds.Fixed(
                0,
                modelSelectorY + iconInsetPadding,
                iconSize - 25,
                iconSize - 25
            ).FixedRightOf(prevButtonBounds, 8 + iconInsetPadding);

        // Add icon (empty texture if no model to display)
        iconToDisplay = (displayIndex >= 0 && displayIndex < modelIcons.Length && modelIcons[displayIndex] != null)
            ? modelIcons[displayIndex] ?? emptyTexture
            : emptyTexture;
        iconAlpha = (displayIndex >= 0 && displayIndex < modelIcons.Length && modelIcons[displayIndex] != null) ? 1 : 0;
        composer.AddExtendedImage(middleIconBounds, iconToDisplay, 1, iconAlpha, (int)GuiElement.scaled(middleIconBounds.fixedWidth), (int)GuiElement.scaled(middleIconBounds.fixedHeight));


        // *********** RIGHT BUTTON **************

        ElementBounds nextButtonBounds = ElementBounds.Fixed(0, modelSelectorY, 35, buttonHeight).WithFixedPadding(2).FixedRightOf(middleIconInsetBounds, 5); // Adjusted

        // *********** RIGHT ICON **************

        displayIndex = modelValues.Length == 1 ?
                (2 == 1 ? 0 : -1) : // Single model: only show in center
                GameMath.Mod(modelIndex - 1 + 2, modelValues.Length);

        ElementBounds rightIconInsetBounds = ElementBounds.Fixed(
            0,
            modelSelectorY,
            iconInsetSize,
            iconInsetSize
        ).FixedRightOf(nextButtonBounds, 8);
        composer.AddInset(rightIconInsetBounds, 2);
        ElementBounds rightIconBounds = ElementBounds.Fixed(
                0,
                modelSelectorY + iconInsetPadding,
                iconSize - 25,
                iconSize - 25
            ).FixedRightOf(nextButtonBounds, 8 + iconInsetPadding + 1);

        // Add icon (empty texture if no model to display)
        iconToDisplay = (displayIndex >= 0 && displayIndex < modelIcons.Length && modelIcons[displayIndex] != null)
            ? modelIcons[displayIndex] ?? emptyTexture
            : emptyTexture;
        iconAlpha = (displayIndex >= 0 && displayIndex < modelIcons.Length && modelIcons[displayIndex] != null) ? 1 : 0;
        iconBrightness = (displayIndex >= 0 && displayIndex < modelIcons.Length && modelIcons[displayIndex] != null) ? 0.5f : 1;
        composer.AddExtendedImage(rightIconBounds, iconToDisplay, iconBrightness, iconAlpha, (int)GuiElement.scaled(rightIconBounds.fixedWidth), (int)GuiElement.scaled(rightIconBounds.fixedHeight));

        // Current model name - spanning all three icons
        ElementBounds nameInsetBounds = ElementBounds.Fixed(
            0,
            modelSelectorY + iconInsetSize + 5,
            411,
            nameHeight
        ).FixedRightOf(groupIconBounds, 18);
        nameInsetBounds.fixedOffsetY += 3;

        ElementBounds nameTextBounds = nameInsetBounds.FlatCopy().FixedGrow(-4, -4);
        nameTextBounds.fixedY += 2;

        composer.AddInset(nameInsetBounds, 2);

        string currentModelName = (modelIndex >= 0 && modelIndex < modelNames.Length)
            ? modelNames[modelIndex]
            : "";

        composer.AddDynamicText(currentModelName,
            CairoFont.WhiteSmallishText().WithOrientation(EnumTextOrientation.Center),
            nameTextBounds,
            "currentModelName");

        // Model selector with arrows
        composer.AddIconButton("left", (on) => ChangeModel(-1, composer), prevButtonBounds.FlatCopy());
        composer.AddIconButton("right", (on) => ChangeModel(1, composer), nextButtonBounds.FlatCopy());

        // Size slider
        float minSize = skinBehavior.CurrentModel.SizeRange.X;
        float maxSize = skinBehavior.CurrentModel.SizeRange.Y;
        _currentModelSize = skinBehavior.CurrentSize;
        _currentModelSize = GameMath.Clamp(_currentModelSize, minSize, maxSize);

        composer.AddRichtext(Lang.Get("playermodellib:model-size-slider"), CairoFont.WhiteSmallText(), sizeTextBounds);
        composer.AddSlider(value => { _currentModelSize = value / 100f; OnModelSizeChanged(); return true; }, sizeSliderBounds, "modelSizeSlider");
        composer.GetSlider("modelSizeSlider").SetValues((int)(_currentModelSize * 100), (int)(minSize * 100), (int)(maxSize * 100), 1, unit: "%");

        // Scrollable description
        composer
            .BeginChildElements(bgBounds)
                .AddInset(bgBounds.FlatCopy(), 3)
                .BeginClip(clipBounds)
                    .AddRichtext("", CairoFont.WhiteDetailText(), descriptionTextBounds, "modelDescription")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValueModel, scrollbarBounds, "scrollbarModel")
            .EndChildElements();

        _clipHeightModel = 500;
        _composer?.GetScrollbar("scrollbarModel")?.SetHeights(
            _clipHeightModel,
            _clipHeightModel * 2
        );
        _composer?.GetScrollbar("scrollbarModel")?.SetScrollbarPosition(0);

        composer.AddSmallButton(Lang.Get("Confirm model"), OnNextImpl, ElementBounds.Fixed(11, _dlgHeight - 24).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal);

        string modelDescription = CreateModelDescription(system, skinBehavior.CurrentModelCode);
        composer.GetRichtext("modelDescription").SetNewText(modelDescription, CairoFont.WhiteDetailText());

        ReTesselate();
    }
    private void ComposeSkinTab(GuiComposer composer, EntityBehaviorPlayerInventory inventoryBehavior, double yPosition, double padding, ElementBounds backgroundBounds)
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();

        if (skinMod == null) return;

        inventoryBehavior.hideClothing = _charNaked;

        EntityShapeRenderer? essr = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        essr?.TesselateShape();

        CairoFont smallfont = CairoFont.WhiteSmallText();
        Cairo.TextExtents textExt = smallfont.GetTextExtents(Lang.Get("Show dressed"));
        int colorIconSize = 22;

        ElementBounds leftColBounds = ElementBounds.Fixed(0, yPosition, 204, _dlgHeight - 59).FixedGrow(2 * padding, 2 * padding);

        _insetSlotBounds = ElementBounds.Fixed(0, yPosition + 2, 265, leftColBounds.fixedHeight - 2 * padding - 10).FixedRightOf(leftColBounds, 10);
        ElementBounds toggleButtonBounds = ElementBounds.Fixed(
                (int)_insetSlotBounds.fixedX + _insetSlotBounds.fixedWidth / 2 - textExt.Width / RuntimeEnv.GUIScale / 2 - 12,
                0,
                textExt.Width / RuntimeEnv.GUIScale + 1,
                textExt.Height / RuntimeEnv.GUIScale
            )
            .FixedUnder(_insetSlotBounds, 4).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(12, 6);



        ElementBounds? bounds = null;
        ElementBounds? prevbounds = null;

        double leftX = 0;

        ScrollPatches.PreLoop(composer, skinMod.AvailableSkinParts, backgroundBounds, leftColBounds, _insetSlotBounds, toggleButtonBounds, ref leftX);

        foreach (SkinnablePart? skinpart in skinMod.AvailableSkinParts)
        {
            bounds = ElementBounds.Fixed(leftX, (prevbounds == null || prevbounds.fixedY == 0) ? -10 : prevbounds.fixedY + 8, colorIconSize, colorIconSize);

            ScrollPatches.NewBounds(bounds);

            string code = skinpart.Code;

            AppliedSkinnablePartVariant? appliedVar = skinMod.AppliedSkinParts.FirstOrDefault(sp => sp.PartCode == code);

            SkinnablePartVariant[] variants = skinpart.Variants.Where(variant => variant.Category == "standard" || variant.Category == null || variant.Category == "").ToArray();

            if (skinpart.Type == EnumSkinnableType.Texture && !skinpart.UseDropDown)
            {
                int selectedIndex = 0;
                int[] colors = new int[variants.Length];

                for (int i = 0; i < variants.Length; i++)
                {
                    colors[i] = variants[i].Color;

                    if (appliedVar?.Code == variants[i].Code) selectedIndex = i;
                }


                bounds = bounds.BelowCopy(0, 10).WithFixedSize(210, 22);
                composer.AddRichtext(Lang.Get("skinpart-" + code), CairoFont.WhiteSmallText(), bounds);

                bounds = bounds.BelowCopy(0, 0).WithFixedSize(colorIconSize, colorIconSize);
                composer.AddColorListPicker(colors, (index) => onToggleSkinPartColor(code, index), bounds, 180, "picker-" + code);

                for (int i = 0; i < colors.Length; i++)
                {
                    GuiElementColorListPicker picker = composer.GetColorListPicker("picker-" + code + "-" + i);
                    picker.ShowToolTip = true;
                    if (Lang.HasTranslation("skinpart-" + code + "-" + variants[i].Code))
                    {
                        picker.TooltipText = Lang.Get("skinpart-" + code + "-" + variants[i].Code);
                    }
                    else
                    {
                        picker.TooltipText = Lang.Get("color-" + variants[i].Code);
                    }
                }

                composer.ColorListPickerSetValue("picker-" + code, selectedIndex);
            }
            else
            {
                int selectedIndex = 0;

                string[] names = new string[variants.Length];
                string[] values = new string[variants.Length];

                for (int i = 0; i < variants.Length; i++)
                {
                    names[i] = Lang.Get("skinpart-" + code + "-" + variants[i].Code);
                    values[i] = variants[i].Code;

                    if (appliedVar?.Code == values[i]) selectedIndex = i;
                }

                bounds = bounds.BelowCopy(0, 10).WithFixedSize(210, 22);
                composer.AddRichtext(Lang.Get("skinpart-" + code), CairoFont.WhiteSmallText(), bounds);

                string tooltip = Lang.GetIfExists("skinpartdesc-" + code);
                if (tooltip != null)
                {
                    bounds = bounds.FlatCopy();
                    composer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, bounds);
                }

                bounds = bounds.BelowCopy(0, 0).WithFixedSize(200, 25);
                composer.AddDropDown(values, names, selectedIndex, (variantcode, selected) => onToggleSkinPartColor(code, variantcode), bounds, "dropdown-" + code);
            }

            prevbounds = bounds.FlatCopy();

            if (skinpart.Colbreak)
            {
                ScrollPatches.ColBreak(composer, backgroundBounds, bounds, ref leftX);

                leftX = _insetSlotBounds.fixedX + _insetSlotBounds.fixedWidth + 22;
                prevbounds.fixedY = 0;
            }
        }

        ScrollPatches.PostLoop(composer, toggleButtonBounds);

        composer.AddInset(_insetSlotBounds, 2);
        composer.AddToggleButton(Lang.Get("Show dressed"), smallfont, OnToggleDressOnOff, toggleButtonBounds, "showdressedtoggle");
        composer.AddButton(Lang.Get("Randomize"), () => { return OnRandomizeSkin(new Dictionary<string, string>()); }, ElementBounds.Fixed(0, _dlgHeight - 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(8, 6), CairoFont.WhiteSmallText(), EnumButtonStyle.Small);
        composer.AddIf(capi.Settings.String.Exists("lastSkinSelection"))
            .AddButton(Lang.Get("Last selection"), () => { return OnRandomizeSkin(GetPreviousSelection()); }, ElementBounds.Fixed(130, _dlgHeight - 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(8, 6), CairoFont.WhiteSmallText(), EnumButtonStyle.Small)
            .EndIf();

        composer.AddSmallButton(Lang.Get("Confirm Skin"), OnNextImpl, ElementBounds.Fixed(11, _dlgHeight - 24).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal);

        composer.GetToggleButton("showdressedtoggle").SetValue(!_charNaked);
    }
    private void ComposeClassTab(GuiComposer composer, double yPosition, double padding, double slotSize)
    {
        EntityShapeRenderer? renderer = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();

        yPosition -= 25;

        ElementBounds leftColBounds = ElementBounds.Fixed(0, yPosition, 0, _dlgHeight - 23).FixedGrow(padding, padding);
        ElementBounds prevButtonBounds = ElementBounds.Fixed(0, yPosition + 23, 35, slotSize - 4).WithFixedPadding(2).FixedRightOf(leftColBounds, -10);
        ElementBounds centerTextBounds = ElementBounds.Fixed(0, yPosition + 25, 432, slotSize - 4 - 8).FixedRightOf(prevButtonBounds, 10);
        ElementBounds charclasssInset = centerTextBounds.ForkBoundingParent(4, 4, 4, 4);
        ElementBounds nextButtonBounds = ElementBounds.Fixed(0, yPosition + 23, 35, slotSize - 4).WithFixedPadding(2).FixedRightOf(charclasssInset, 9);

        CairoFont font = CairoFont.WhiteMediumText();
        centerTextBounds.fixedY += (centerTextBounds.fixedHeight - font.GetFontExtents().Height / RuntimeEnv.GUIScale) / 2;

        int visibleHeight = (int)Math.Max(120, _dlgHeight - (yPosition + 25) - 62);
        ElementBounds charTextBounds = ElementBounds.Fixed(0, 0, 498, visibleHeight)
            .FixedUnder(prevButtonBounds, 15)
            .FixedRightOf(leftColBounds, -10);

        ElementBounds bgBounds = charTextBounds.ForkBoundingParent(6, 6, 6, 6);
        ElementBounds clipBounds = charTextBounds.FlatCopy().FixedGrow(6, 11).WithFixedOffset(0, -6);
        ElementBounds scrollbarBounds = charTextBounds.CopyOffsetedSibling(charTextBounds.fixedWidth + 7, -6, 0, 12).WithFixedWidth(20);


        _insetSlotBounds = ElementBounds.Fixed(0, yPosition + 25, 193, leftColBounds.fixedHeight - 2 * padding - 30).FixedRightOf(nextButtonBounds, 11);

        composer
            .AddInset(_insetSlotBounds, 2)
            .AddIconButton("left", (on) => ChangeClass(-1), prevButtonBounds.FlatCopy())
            .AddInset(charclasssInset, 2)
            .AddDynamicText("Commoner", font.Clone().WithOrientation(EnumTextOrientation.Center), centerTextBounds, "className")
            .AddIconButton("right", (on) => ChangeClass(1), nextButtonBounds.FlatCopy())

            .BeginChildElements(bgBounds)
                .AddInset(bgBounds.FlatCopy(), 3)
                .BeginClip(clipBounds)
                    .AddRichtext("", CairoFont.WhiteDetailText(), charTextBounds, "characterDesc")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
            .EndChildElements()

            .AddSmallButton(Lang.Get("Confirm Class"), OnConfirm,
                ElementBounds.Fixed(11, _dlgHeight - 24).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6),
                EnumButtonStyle.Normal)
        ;

        _clipHeight = (float)clipBounds.fixedHeight;
        composer.GetScrollbar("scrollbar").SetHeights(
            _clipHeight,
            _clipHeight
        );
        composer.GetScrollbar("scrollbar").SetScrollbarPosition(0);

        ChangeClass(0);
    }


    private void onToggleModelGroup(string groupCode, GuiComposer? composer = null)
    {
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();
        GetCustomModels(system, groupCode, out string[] modelValues, out _, out _, out _);

        if (modelValues.Length > 0)
        {
            onToggleModel(modelValues[0], composer);
        }
    }

    private Dictionary<string, string> GetPreviousSelection()
    {
        string currentModel = GetCurrentModel();

        Dictionary<string, Dictionary<string, string>> allSelections = _api.LoadModConfig<Dictionary<string, Dictionary<string, string>>>(_previousSelectionFile) ?? [];

        if (allSelections.TryGetValue(currentModel, out Dictionary<string, string>? result))
        {
            return result;
        }

        return [];
    }
    private void SavePreviousSelection(Dictionary<string, string> selection)
    {
        string currentModel = GetCurrentModel();

        Dictionary<string, Dictionary<string, string>> allSelections = _api.LoadModConfig<Dictionary<string, Dictionary<string, string>>>(_previousSelectionFile) ?? [];

        allSelections[currentModel] = selection;

        _api.StoreModConfig(allSelections, _previousSelectionFile);
    }

    private string GetCurrentModel()
    {
        PlayerSkinBehavior? skinBehavior = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (skinBehavior == null) return system.DefaultModelCode;

        _ = GetCurrentModelAndGroup(system, skinBehavior, out int modelIndex, out int groupIndex);
        GetCustomGroups(system, out string[] groupValues, out _);
        GetCustomModels(system, groupValues[groupIndex], out string[] modelValues, out _, out _, out _);

        return modelValues[modelIndex];
    }

    private bool ChangeModel(int dir, GuiComposer? composer = null)
    {
        PlayerSkinBehavior? skinBehavior = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (skinBehavior == null) return false;

        _ = GetCurrentModelAndGroup(system, skinBehavior, out int modelIndex, out int groupIndex);
        GetCustomGroups(system, out string[] groupValues, out _);
        GetCustomModels(system, groupValues[groupIndex], out string[] modelValues, out _, out _, out _);

        modelIndex = GameMath.Mod(modelIndex + dir, modelValues.Length);

        onToggleModel(modelValues[modelIndex], composer);

        return true;
    }
    private void OnNewScrollbarValueModel(float value)
    {
        GuiElementRichtext? richtextElem = _composer?.GetRichtext("modelDescription");

        if (richtextElem != null)
        {
            richtextElem.Bounds.fixedY = 0 - value;
            richtextElem.Bounds.CalcWorldBounds();
        }
    }
    private void OnNewScrollbarValue(float value)
    {
        GuiElementRichtext? richtextElem = _composer?.GetRichtext("characterDesc");

        if (richtextElem != null)
        {
            richtextElem.Bounds.fixedY = 0 - value;// Math.Min(value / 100f * richtextElem.TotalHeight, Math.Max(richtextElem.TotalHeight - 377, 0));
            richtextElem.Bounds.CalcWorldBounds();
        }
    }

    private bool OnRandomizeSkin(Dictionary<string, string> preselection)
    {
        EntityPlayer entity = capi.World.Player.Entity;

        EntityBehaviorPlayerInventory? bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        if (bh == null) return false;
        bh.doReloadShapeAndSkin = false;

        _characterSystem.randomizeSkin(entity, preselection);
        EntityBehaviorExtraSkinnable? skinMod = entity.GetBehavior<EntityBehaviorExtraSkinnable>();

        if (skinMod == null) return false;

        foreach (AppliedSkinnablePartVariant? appliedPart in skinMod.AppliedSkinParts)
        {
            string partcode = appliedPart.PartCode;

            SkinnablePart? skinPart = skinMod.AvailableSkinParts.FirstOrDefault(part => part.Code == partcode);
            if (skinPart == null) continue;

            SkinnablePartVariant[] variants = skinPart.Variants.Where(variant => variant.Category == "standard" || variant.Category == null || variant.Category == "").ToArray();

            int index = variants.IndexOf(part => part.Code == appliedPart.Code);

            if (skinPart.Type == EnumSkinnableType.Texture && !skinPart.UseDropDown)
            {
                Composers["createcharacter"].ColorListPickerSetValue("picker-" + partcode, index);
            }
            else
            {
                Composers["createcharacter"].GetDropDown("dropdown-" + partcode)?.SetSelectedIndex(index);
            }
        }

        bh.doReloadShapeAndSkin = true;
        ReTesselate();

        return true;
    }
    private void OnToggleDressOnOff(bool on)
    {
        _charNaked = !on;
        EntityBehaviorPlayerInventory? bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        if (bh != null) bh.hideClothing = _charNaked;
        ReTesselate();
    }
    private void onToggleSkinPartColor(string partCode, string variantCode)
    {
        EntityBehaviorExtraSkinnable? skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
        skinMod?.selectSkinPart(partCode, variantCode);
    }
    private void onToggleSkinPartColor(string partCode, int index)
    {
        EntityBehaviorExtraSkinnable? skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

        if (skinMod == null) return;

        string variantCode = skinMod.AvailableSkinPartsByCode[partCode].Variants[index].Code;

        skinMod.selectSkinPart(partCode, variantCode);
    }
    private void onToggleModel(string modelCode, GuiComposer? composer = null)
    {
        EntityBehaviorPlayerInventory? bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (bh == null || skinMod == null) return;

        skinMod.SetCurrentModel(modelCode, _currentModelSize);
        bh.doReloadShapeAndSkin = true;

        List<CharacterClass> availableClasses = GetAvailableClasses(system, skinMod.CurrentModelCode);
        _characterSystem.setCharacterClass(capi.World.Player.Entity, availableClasses[0].Code, true);

        try
        {
            float minSize = system.CustomModels[modelCode].SizeRange.X;
            float maxSize = system.CustomModels[modelCode].SizeRange.Y;
            _currentModelSize = GameMath.Clamp(_currentModelSize, minSize, maxSize);
            composer?.GetSlider("modelSizeSlider").SetValues((int)(_currentModelSize * 100), (int)(minSize * 100), (int)(maxSize * 100), 1, unit: "%");
            OnModelSizeChanged();
        }
        catch
        {
            Debug.WriteLine("Failed to reset model size");
        }

        ComposeGuis();

        OnRandomizeSkin(GetPreviousSelection());
    }
    private bool OnNextImpl()
    {
        _currentTab = (EnumCreateCharacterTabs)GameMath.Clamp((int)_currentTab + 1, 0, (int)EnumCreateCharacterTabs.Class);
        ComposeGuis();
        return true;
    }
    private void onTabClicked(int tabid)
    {
        _currentTab = (EnumCreateCharacterTabs)tabid;
        ComposeGuis();
    }
    private bool OnConfirm()
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        if (skinMod != null)
        {
            Dictionary<string, string> selection = skinMod.AppliedSkinParts.ToDictionary(part => part.PartCode, part => part.Code);

            SavePreviousSelection(selection);
        }

        _didSelect = true;
        TryClose();
        return true;
    }
    private new void OnTitleBarClose()
    {
        TryClose();
    }
    private void ChangeClass(int dir)
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (skinMod == null) return;

        List<CharacterClass> availableClasses = GetAvailableClasses(system, skinMod.CurrentModelCode);

        _currentClassIndex = GameMath.Mod(_currentClassIndex + dir, availableClasses.Count);

        CharacterClass chclass = availableClasses[_currentClassIndex];
        Composers["createcharacter"]?.GetDynamicText("className").SetNewText(Lang.Get("characterclass-" + chclass.Code));

        StringBuilder fulldesc = new();

        fulldesc.AppendLine();
        fulldesc.AppendLine(Lang.Get("characterdesc-" + chclass.Code));
        fulldesc.AppendLine();
        fulldesc.AppendLine(Lang.Get("traits-title"));


        IOrderedEnumerable<Trait> chartraitsExtra = _customModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits
            .Where(_characterSystem.TraitsByCode.ContainsKey)
            .Select(code => _characterSystem.TraitsByCode[code])
            .OrderBy(trait => (int)trait.Type);
        IOrderedEnumerable<Trait> chartraits = chclass.Traits
            .Where(code => !_customModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits.Contains(code))
            .Where(_characterSystem.TraitsByCode.ContainsKey)
            .Select(code => _characterSystem.TraitsByCode[code])
            .OrderBy(trait => (int)trait.Type);

        AppendTraits(fulldesc, chartraits);

        if (chclass.Traits.Length == 0)
        {
            fulldesc.AppendLine(Lang.Get("No positive or negative traits"));
        }

        if (chartraitsExtra.Any())
        {
            fulldesc.AppendLine();
            fulldesc.AppendLine(Lang.Get("model-traits-title"));

            AppendTraits(fulldesc, chartraitsExtra);
        }

        fulldesc.AppendLine();

        Composers["createcharacter"].GetRichtext("characterDesc").SetNewText(fulldesc.ToString(), CairoFont.WhiteDetailText());

        _composer?.GetScrollbar("scrollbar")?.SetHeights(
            _clipHeight,
            (float)(Math.Max(_clipHeight, _composer.GetRichtext("characterDesc")?.TotalHeight ?? _clipHeight))
        );
        _composer?.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);

        _characterSystem.setCharacterClass(capi.World.Player.Entity, chclass.Code, true);

        ReTesselate();
    }
    private void ReTesselate()
    {
        EntityShapeRenderer? renderer = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();
    }
    private void OnModelSizeChanged()
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();

        skinMod?.SetCurrentModel(skinMod.CurrentModelCode, _currentModelSize);
    }
    private string CreateModelDescription(CustomModelsSystem system, string model)
    {
        CustomModelData modelConfig = system.CustomModels[model];

        StringBuilder fullDescription = new();

        string[] modelSplit = model.Split(':');

        if (modelSplit.Length != 2)
        {
            modelSplit = ["game", model];
        }

        string descEntry = $"{modelSplit[0]}:modeldesc-{modelSplit[1]}";
        if (Lang.HasTranslation(descEntry))
        {
            fullDescription.AppendLine(Lang.Get(descEntry));
        }

        IEnumerable<string> missingTraits = modelConfig.ExtraTraits.Where(code => !_characterSystem.TraitsByCode.ContainsKey(code));
        if (missingTraits.Any())
        {
            string missingTraitsList = missingTraits.Aggregate((a, b) => $"{a}, {b}");
            LoggerUtil.Error(_api, this, $"Custom model '{model}' has traits that does not exist: {missingTraitsList}.\nIt can be caused by either bug in the mod that adds that model, or it can be caused by some other mods breaking vanilla character system.");
            IEnumerable<string> existingTraits = _characterSystem.TraitsByCode.Keys;
            if (existingTraits.Any())
            {
                string existingTraitsList = existingTraits.Aggregate((a, b) => $"{a},\n{b}");
                LoggerUtil.Error(_api, this, $"All existing traits:\n{existingTraitsList}\n");
            }

            fullDescription.AppendLine();
            fullDescription.AppendLine($"<font color=\"#ff8484\">##########################################################</font>");
            fullDescription.AppendLine($"<font color=\"#ff8484\">Error occurred while loading custom model traits. Check client-main.log file.</font>");
            fullDescription.AppendLine($"<font color=\"#ff8484\">##########################################################</font>");
        }

        IOrderedEnumerable<Trait> traits = modelConfig.ExtraTraits
            .Where(_characterSystem.TraitsByCode.ContainsKey)
            .Select(code => _characterSystem.TraitsByCode[code])
            .OrderBy(trait => (int)trait.Type);

        if (traits.Any())
        {
            fullDescription.AppendLine("");
            fullDescription.AppendLine(Lang.Get("traits-title"));
        }

        AppendTraits(fullDescription, traits);

        fullDescription.AppendLine("");

        List<CharacterClass> availableClasses = GetAvailableClasses(system, model);

        fullDescription.Append(Lang.Get("playermodellib:available-classes"));
        bool addedOne = false;

        foreach (CharacterClass characterClass in availableClasses)
        {
            if (addedOne) fullDescription.Append(", ");
            addedOne = true;
            fullDescription.Append(Lang.Get("characterclass-" + characterClass.Code));
        }

        return fullDescription.ToString();
    }
    private void AppendTraits(StringBuilder fullDescription, IEnumerable<Trait> traits)
    {
        StringBuilder attributes = new();

        foreach (Trait trait in traits)
        {
            attributes.Clear();
            foreach ((string attribute, double value) in trait.Attributes)
            {
                if (attributes.Length > 0) attributes.Append(", ");

                attributes.Append(GetAttributeDescription(attribute, value));
            }

            if (attributes.Length > 0)
            {
                fullDescription.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), attributes));
            }
            else
            {
                string? description = Lang.GetIfExists("traitdesc-" + trait.Code);
                if (description != null)
                {
                    fullDescription.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), description));
                }
                else
                {
                    fullDescription.AppendLine(Lang.Get("trait-" + trait.Code));
                }
            }
        }
    }
    private List<CharacterClass> GetAvailableClasses(CustomModelsSystem system, string model)
    {
        HashSet<string> availableClassesForModel = system.CustomModels[model].AvailableClasses;
        HashSet<string> skippedClassesForModel = system.CustomModels[model].SkipClasses;
        HashSet<string> exclusiveClassesForModel = system.CustomModels[model].ExclusiveClasses;

        IEnumerable<CharacterClass> availableClasses = _characterSystem.characterClasses.Where(element => availableClassesForModel.Contains(element.Code));
        if (!availableClasses.Any())
        {
            availableClasses = _characterSystem.characterClasses;
        }

        availableClasses = availableClasses.Where(element => !system.ExclusiveClasses.Contains(element.Code) || exclusiveClassesForModel.Contains(element.Code));

        availableClasses = availableClasses.Where(element => !skippedClassesForModel.Contains(element.Code)).Where(element => element.Enabled);

        return availableClasses.ToList();
    }
    private void GetCustomGroups(CustomModelsSystem system, out string[] groupValues, out string[] groupNames)
    {
        string[] modelValues = GetAvailableModels(system);

        HashSet<string> groups = [];
        foreach (string modelValue in modelValues)
        {
            groups.Add(system.CustomModels[modelValue].Group);
        }
        groupValues = groups.ToArray();
        groupNames = groups.Select(key => new AssetLocation(key)).Select(GetCustomGroupLangEntry).ToArray();

        if (groupValues.Length == 0)
        {
            groupValues = [system.DefaultGroupCode];
            groupNames = [GetCustomGroupLangEntry(system.DefaultGroupCode)];
        }
    }
    private void GetCustomModels(CustomModelsSystem system, string group, out string[] modelValues, out string[] modelNames, out AssetLocation?[] modelIcons, out AssetLocation? groupIcon)
    {
        modelValues = GetAvailableModels(system).Where(code => system.CustomModels[code].Group == group).ToArray();
        modelNames = modelValues.Select(key => GetCustomModelLangEntry(new AssetLocation(key), system.CustomModels[key].Name)).ToArray();
        modelIcons = modelValues.Select(code => system.CustomModels[code].Icon).ToArray();
        groupIcon = system.CustomModels
            .Where(entry => entry.Value.Group == group)
            .Select(entry => entry.Value.GroupIcon)
            .FirstOrDefault(icon => icon != null);

        if (modelValues.Length == 0)
        {
            modelValues = [system.DefaultModelCode];
            modelNames = [GetCustomModelLangEntry(system.DefaultModelCode, null)];
        }
    }
    private string[] GetAvailableModels(CustomModelsSystem system)
    {
        string[] extraCustomModels = capi?.World?.Player?.Entity?.WatchedAttributes?.GetStringArray("extraCustomModels", []) ?? [];
        return system.CustomModels.Where(entry => entry.Value.Enabled || extraCustomModels.Contains(entry.Value.Code)).Select(entry => entry.Key).ToArray();
    }
    private string GetCustomModelLangEntry(AssetLocation code, string? name) => name ?? Lang.Get($"{code.Domain}:playermodel-{code.Path}");
    private string GetCustomGroupLangEntry(AssetLocation code) => Lang.GetIfExists($"game:playermodelgroup-{code.Path}") ?? Lang.Get($"{code.Domain}:playermodel-{code.Path}");
    private bool GetCurrentModelAndGroup(CustomModelsSystem system, PlayerSkinBehavior skinBehavior, out int modelIndex, out int groupIndex)
    {
        GetCustomGroups(system, out string[] groupValues, out string[] groupNames);

        for (groupIndex = 0; groupIndex < groupValues.Length; groupIndex++)
        {
            GetCustomModels(system, groupValues[groupIndex], out string[] modelValues, out _, out _, out _);

            for (modelIndex = 0; modelIndex < modelValues.Length; modelIndex++)
            {
                if (modelValues[modelIndex] == skinBehavior.CurrentModelCode)
                {
                    return true;
                }
            }
        }

        groupIndex = 0;
        modelIndex = 0;
        return false;
    }
}
