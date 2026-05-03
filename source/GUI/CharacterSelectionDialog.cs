using OverhaulLib.Utils;
using System.Diagnostics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlayerModelLib;


public sealed class GuiDialogCreateCustomCharacter : GuiDialogCreateCharacter
{
    public override float ZSize
    {
        get { return (float)GuiElement.scaled(280); }
    }
    public override bool PrefersUngrabbedMouse => true;
    public override string? ToggleKeyCombinationCode => null;
    public static bool DialogOpened { get; set; } = false;
    public static int RenderState { get; set; } = 0; // Is used to disable model size compensation when rendering model in model tab

    public OrderedDictionary<string, ComposeTabDelegate> Tabs { get; } = [];
    public Dictionary<string, bool> TabsEnabled { get; } = [];
    public OrderedDictionary<string, ComposeTabDelegate> ActiveTabs { get; set; } = [];

    public static event Action<GuiDialogCreateCustomCharacter>? OnCreated;

    public GuiDialogCreateCustomCharacter(ICoreClientAPI api, CharacterSystem characterSystem) : base(api, characterSystem)
    {
        CharacterSystem = characterSystem;
        CustomModelsSystem = api.ModLoader.GetModSystem<CustomModelsSystem>();
        Api = api;
        Tabs.Add("model", ComposeModelTab);
        Tabs.Add("skin", ComposeSkinTab);
        Tabs.Add("class", ComposeClassTab);
        TabsEnabled.Add("model", true);
        TabsEnabled.Add("skin", true);
        TabsEnabled.Add("class", true);
        OnCreated?.Invoke(this);
    }
    public override void OnGuiOpened()
    {
        DialogOpened = true;

        string? characterClass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
        if (characterClass != null && CharacterSystem.characterClassesByCode.ContainsKey(characterClass))
        {
            CharacterSystem.setCharacterClass(capi.World.Player.Entity, characterClass, true);
        }
        else
        {
            CharacterSystem.setCharacterClass(capi.World.Player.Entity, CharacterSystem.characterClasses[0].Code, true);
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
            CharacterInventory?.Open(capi.World.Player);
        }

        _ = ChangeModel(0);
    }
    public override void OnGuiClosed()
    {
        DialogOpened = false;

        if (CharacterInventory != null)
        {
            CharacterInventory.Close(capi.World.Player);
            Composers["createcharacter"].GetSlotGrid("leftSlots")?.OnGuiClosed(capi);
            Composers["createcharacter"].GetSlotGrid("rightSlots")?.OnGuiClosed(capi);
        }

        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (skinMod == null) return;

        List<CharacterClass> availableClasses = GetAvailableClasses(system, skinMod.CurrentModelCode);

        CharacterClass characterClass = availableClasses[CurrentClassIndex];

        ClientSelectionDone(CharacterInventory, characterClass.Code, DidSelect);

        system.SynchronizePlayerModel(skinMod.CurrentModelCode);
        system.SynchronizePlayerModelSize(CurrentModelSize);

        EntityBehaviorPlayerInventory? invBhv = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        if (invBhv != null)
        {
            invBhv.hideClothing = false;
        }

        ReTesselate();

        RenderState = 0;

        OnToggleDressOnOff(false);
    }
    public override bool CaptureAllInputs()
    {
        return IsOpened();
    }
    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (focused && InsetSlotBounds?.PointInside(capi.Input.MouseX, capi.Input.MouseY) == true && CurrentTab == 1)
        {
            CharZoom = GameMath.Clamp(CharZoom + args.deltaPrecise / 10f, 0.5f, 1.2f);
            Height = GameMath.Clamp(Height, -HeightLimit * CharZoom, HeightLimit * CharZoom);
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

        if (InsetSlotBounds?.PointInside(capi.Input.MouseX, capi.Input.MouseY) == true && CurrentTab == 1)
        {
            CharZoom = GameMath.Clamp(CharZoom + args.deltaPrecise / 5f, 0.5f, 1f);
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

        RotateCharacter = InsetSlotBounds?.PointInside(args.X, args.Y) ?? false;
    }
    public override void OnMouseUp(MouseEvent args)
    {
        base.OnMouseUp(args);

        RotateCharacter = false;
    }
    public override void OnMouseMove(MouseEvent args)
    {
        base.OnMouseMove(args);

        if (RotateCharacter)
        {
            Yaw -= args.DeltaX / 100f;
            Height += args.DeltaY / 2f;
            Height = GameMath.Clamp(Height, -HeightLimit * CharZoom, HeightLimit * CharZoom);
        }
    }
    public override void OnRenderGUI(float deltaTime)
    {
        if (InsetSlotBounds == null) return;

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

        Mat.Identity();
        Mat.RotateXDeg(-14);
        Vec4f lightRot = Mat.TransformVector(LighPos);
        double pad = GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);

        capi.Render.CurrentActiveShader.Uniform("lightPosition", new Vec3f(lightRot.X, lightRot.Y, lightRot.Z));

        capi.Render.PushScissor(InsetSlotBounds);

        RenderState = CurrentTab + 1;

        switch (CurrentTab)
        {
            case 0:
                capi.Render.RenderEntityToGui(
                    deltaTime,
                    capi.World.Player.Entity,
                    InsetSlotBounds.renderX + pad - GuiElement.scaled(115),
                    InsetSlotBounds.renderY + pad - GuiElement.scaled(-40),
                    (float)GuiElement.scaled(230),
                    Yaw,
                    (float)GuiElement.scaled(205),
                    ColorUtil.WhiteArgb);
                break;
            case 1:
                capi.Render.RenderEntityToGui(
                    deltaTime,
                    capi.World.Player.Entity,
                    InsetSlotBounds.renderX + pad - GuiElement.scaled(195) * CharZoom + GuiElement.scaled(115 * (1 - CharZoom)),
                    InsetSlotBounds.renderY + pad + GuiElement.scaled(10 * (1 - CharZoom)) + GuiElement.scaled(Height),
                    (float)GuiElement.scaled(230),
                    Yaw,
                    (float)GuiElement.scaled(330 * CharZoom),
                    ColorUtil.WhiteArgb);
                break;
            case 2:
                capi.Render.RenderEntityToGui(
                    deltaTime,
                    capi.World.Player.Entity,
                    InsetSlotBounds.renderX + pad - GuiElement.scaled(111),
                    InsetSlotBounds.renderY + pad - GuiElement.scaled(-7),
                    (float)GuiElement.scaled(230),
                    Yaw,
                    (float)GuiElement.scaled(205),
                    ColorUtil.WhiteArgb);
                break;
            default:
                break;
        }

        capi.Render.PopScissor();

        capi.Render.CurrentActiveShader.Uniform("lightPosition", new Vec3f(1, -1, 0).Normalize());

        capi.Render.GlPopMatrix();
    }

    public static string GetAttributeDescription(string attribute, double value)
    {
        string defaultKey = string.Format(GlobalConstants.DefaultCultureInfo, AttributeLangPrefix + "-{0}-{1}", attribute, value);
        string newKey = $"{AttributeLangPrefix}-{attribute}";

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

    public const string AttributeLangPrefix = "charattribute";
    public bool DidSelect { get; set; } = false;
    public IInventory? CharacterInventory { get; set; }
    public ElementBounds? InsetSlotBounds { get; set; }
    public readonly CharacterSystem CharacterSystem;
    public readonly CustomModelsSystem CustomModelsSystem;
    public int CurrentClassIndex { get; set; } = 0;
    public int CurrentTab { get; set; } = 0;
    public float CharZoom { get; set; } = 1f;
    public bool HideClothing { get; set; } = true;
    public const int DlgHeight = 433 + 80 + 33;
    public float Yaw { get; set; } = -GameMath.PIHALF + 0.3f;
    public float Height { get; set; } = 0;
    public const float HeightLimit = 150;
    public bool RotateCharacter { get; set; }
    public readonly Vec4f LighPos = new Vec4f(-1, -1, 0, 0).NormalizeXYZ();
    public readonly Matrixf Mat = new();
    public float CurrentModelSize { get; set; } = 1f;
    public GuiComposer? Composer { get; set; }
    public float ClipHeight { get; set; } = 377;
    public readonly ICoreClientAPI Api;
    public const string PreviousSelectionFile = "playermodellib-previous-selections.json";
    public const double DialogWidth = 757;

    public ElementBounds? _skinTabLeftColumnBounds { get; set; }
    public ElementBounds? _skinTabRightColumnBounds { get; set; }

    public new void ComposeGuis()
    {
        ActiveTabs.Clear();
        foreach ((string key, ComposeTabDelegate? tab) in Tabs)
        {
            if (TabsEnabled[key])
            {
                ActiveTabs.Add(key, tab);
            }
        }


        double padding = GuiElementItemSlotGridBase.unscaledSlotPadding;
        double slotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
        double yPosition = 20 + padding;

        CharacterInventory = capi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ElementBounds tabBounds = ElementBounds.Fixed(0, -25, 450, 25);

        ElementBounds backgroundBounds = ElementBounds.FixedSize(717, DlgHeight)
            .WithFixedPadding(GuiStyle.ElementToDialogPadding);

        ElementBounds dialogBounds = ElementBounds.FixedSize(DialogWidth, DlgHeight + 40)
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);

        int tabsCount = 0;
        GuiTab[] tabs = ActiveTabs.Select(entry => new GuiTab() { Name = Lang.Get($"playermodellib:tab-{entry.Key}"), DataInt = tabsCount++ }).ToArray();

        string tabTitle = Lang.Get($"playermodellib:tab-{ActiveTabs.GetAt(CurrentTab).Key}");

        GuiComposer composer = capi.Gui
            .CreateCompo("createcharacter", dialogBounds)
            .AddShadedDialogBG(backgroundBounds, true)
            .AddDialogTitleBar(tabTitle, OnTitleBarClose)
            .AddHorizontalTabs(tabs, tabBounds, onTabClicked, CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), "tabs")
            .BeginChildElements(backgroundBounds);

        Composer = composer;

        Composers["createcharacter"] = composer;

        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();
        EntityBehaviorPlayerInventory inventoryBehavior = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>() ?? throw new Exception();
        inventoryBehavior.hideClothing = false;

        ActiveTabs.GetAt(CurrentTab).Value.Invoke(this, composer, yPosition, padding, slotSize, backgroundBounds, dialogBounds);

        GuiElementHorizontalTabs tabElem = composer.GetHorizontalTabs("tabs");
        tabElem.unscaledTabSpacing = 20;
        tabElem.unscaledTabPadding = 10;
        tabElem.activeElement = CurrentTab;

        try
        {
            composer.Compose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public delegate void ComposeTabDelegate(GuiDialogCreateCustomCharacter dialog, GuiComposer composer, double yPosition, double padding, double slotSize, ElementBounds backgroundBounds, ElementBounds dialogBounds);


    public void ComposeModelTab(GuiDialogCreateCustomCharacter dialog, GuiComposer composer, double yPosition, double padding, double slotSize, ElementBounds backgroundBounds, ElementBounds dialogBounds)
    {
        float _clipHeightModel;

        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        PlayerSkinBehavior? skinBehavior = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        if (skinBehavior == null) return;

        _ = GetCurrentModelAndGroup(system, skinBehavior, out int modelIndex, out int groupIndex);
        GetCustomGroups(system, out string[] groupValues, out string[] groupNames);
        GetCustomModels(system, groupValues[groupIndex], out string[] modelValues, out string[] modelNames, out AssetLocation?[] modelIcons, out AssetLocation? groupIcon);

        yPosition -= 30;

        double insetTopY = yPosition + 30;
        double insetHeight = DlgHeight - 26 - insetTopY + 33;

        ElementBounds leftColBounds = ElementBounds.Fixed(0, yPosition, 0, DlgHeight - 47).FixedGrow(padding, padding);
        InsetSlotBounds = ElementBounds.Fixed(0, insetTopY, 190, insetHeight).FixedRightOf(leftColBounds, -10);

        ElementBounds groupTextBounds = ElementBounds.Fixed(0, insetTopY, 525, slotSize - 4 - 8).FixedRightOf(InsetSlotBounds, 10);
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
        double totalIconHeight = iconInsetSize + 5 + nameHeight; // icon inset + spacing + name

        // Arrow buttons - same height as icons + name
        double buttonHeight = iconInsetSize - 4;

        // Size slider


        // Scrollable description area
        int visibleHeight = (int)Math.Max(120, DlgHeight - (modelSelectorY + totalIconHeight + 20 + 20 + 20) + 7);
        ElementBounds descriptionTextBounds = ElementBounds.Fixed(0, 0, 500, visibleHeight)
            .FixedUnder(groupDropBoxInset, 160)
            .FixedRightOf(InsetSlotBounds, 10); // Changed from 10 to 5

        ElementBounds bgBounds = descriptionTextBounds.ForkBoundingParent(6, 6, 6, 6);
        ElementBounds clipBounds = descriptionTextBounds.FlatCopy().FixedGrow(6, 11).WithFixedOffset(0, -6);
        ElementBounds scrollbarBounds = descriptionTextBounds.CopyOffsetedSibling(descriptionTextBounds.fixedWidth + 7, -6, 0, 12).WithFixedWidth(20);

        ElementBounds sizeTextBounds = ElementBounds.Fixed(0, modelSelectorY + totalIconHeight + 20, 100, 20).FixedUnder(scrollbarBounds, 5).FixedRightOf(InsetSlotBounds, 10);
        ElementBounds sizeSliderBounds = ElementBounds.Fixed(0, modelSelectorY + totalIconHeight + 20, 290, 20).FixedUnder(scrollbarBounds, 5).FixedRightOf(sizeTextBounds, 0);

        composer.AddInset(InsetSlotBounds, 2);
        composer.AddInset(groupDropBoxInset, 2);

        // Group dropdown
        GuiElementScrollableDropDown groupDropDown = new(
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
                -1 :
                GameMath.Mod(modelIndex - 1 + 0, modelValues.Length);

        ElementBounds groupIconInsetBounds = ElementBounds.Fixed(
            0,
            modelSelectorY,
            iconInsetSize - 1,
            groupIconInsetSize
        ).FixedRightOf(InsetSlotBounds, 10);
        composer.AddInset(groupIconInsetBounds, 2);
        ElementBounds groupIconBounds = ElementBounds.Fixed(
                0,
                modelSelectorY + iconInsetPadding,
                iconSize - 25,
                176 - 40 + 6
            ).FixedRightOf(InsetSlotBounds, 10 + iconInsetPadding + 0);

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
                0 :
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
                -1 :
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
        CurrentModelSize = skinBehavior.CurrentSize;
        CurrentModelSize = GameMath.Clamp(CurrentModelSize, minSize, maxSize);

        composer.AddRichtext(Lang.Get("playermodellib:model-size-slider"), CairoFont.WhiteSmallText(), sizeTextBounds);
        composer.AddSlider(value => { CurrentModelSize = value / 100f; OnModelSizeChanged(); return true; }, sizeSliderBounds, "modelSizeSlider");
        composer.GetSlider("modelSizeSlider").SetValues((int)(CurrentModelSize * 100), (int)(minSize * 100), (int)(maxSize * 100), 1, unit: "%");

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
        Composer?.GetScrollbar("scrollbarModel")?.SetHeights(
            _clipHeightModel,
            _clipHeightModel * 2
        );
        Composer?.GetScrollbar("scrollbarModel")?.SetScrollbarPosition(0);

        composer.AddSmallButton(Lang.Get("Confirm model"), OnNextImpl, ElementBounds.Fixed(11, DlgHeight - 24).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal);

        string modelDescription = CreateModelDescription(system, skinBehavior.CurrentModelCode);
        composer.GetRichtext("modelDescription").SetNewText(modelDescription, CairoFont.WhiteDetailText());

        OnToggleDressOnOff(false);
    }
    public void ComposeSkinTab(GuiDialogCreateCustomCharacter dialog, GuiComposer composer, double yPosition, double padding, double slotSize, ElementBounds backgroundBounds, ElementBounds dialogBounds)
    {
        PlayerSkinBehavior? skinBehavior = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        if (skinBehavior == null) return;
        EntityShapeRenderer? renderer = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();

        EntityBehaviorPlayerInventory inventoryBehavior = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>() ?? throw new Exception();
        inventoryBehavior.hideClothing = HideClothing;

        CairoFont smallfont = CairoFont.WhiteSmallText();
        Cairo.TextExtents textExt = smallfont.GetTextExtents(Lang.Get("Show dressed"));

        int colorIconSize = 22;
        double horizontalOffset = -10;
        double verticalOffset = -5;
        double columnsWidth = 236;
        double previewWidth = 256;
        double columnsHeight = DlgHeight - 54 + 4;
        double buttonsBarWidth = columnsWidth * 2 + previewWidth + padding * 2;
        double buttonsBarHeight = 36;
        double hideClothingHeight = 30;
        double previewInsetHeight = columnsHeight - hideClothingHeight - padding;
        double scrollBarWidth = 12;
        double colorPickerheight = 110;
        double canvasHeight = 256;
        double skinPartWidht = columnsWidth - scrollBarWidth;
        double dropDownheight = 24 + padding * 2;

        // top level bounds
        ElementBounds leftColumnBounds = ElementBounds.Fixed(horizontalOffset, yPosition, columnsWidth, columnsHeight);
        ElementBounds previewAreaBounds = leftColumnBounds.RightCopy(padding, 0, 0, 0).WithFixedWidth(previewWidth);
        ElementBounds rightColumnBounds = ElementBounds.Fixed(0, yPosition, columnsWidth, columnsHeight).FixedRightOf(previewAreaBounds, padding);
        ElementBounds bottomButtonsBarBounds = ElementBounds.Fixed(horizontalOffset, padding, buttonsBarWidth, buttonsBarHeight).FixedUnder(leftColumnBounds);
        // preview area
        ElementBounds insetBounds = ElementBounds.Fixed(0, 0, previewWidth, previewInsetHeight).WithParent(previewAreaBounds);
        ElementBounds hideClothingButtonBounds = ElementBounds.Fixed(0, padding, previewWidth, hideClothingHeight).WithParent(previewAreaBounds).FixedUnder(insetBounds);
        // buttons bar
        ElementBounds randomizeButtonBounds = ElementBounds.Fixed(0, 0).WithFixedOffset(padding, padding).WithParent(bottomButtonsBarBounds).WithFixedPadding(8, 6);
        ElementBounds lastSelectionButtonBounds = ElementBounds.Fixed(0, 0).WithFixedOffset(padding, padding).WithParent(bottomButtonsBarBounds).WithFixedPadding(8, 6).RightOf(randomizeButtonBounds, padding);
        ElementBounds confirmButtonBounds = ElementBounds.Fixed(0, 0).WithFixedOffset(-padding, padding).WithParent(bottomButtonsBarBounds).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6);
        // skin parts
        ElementBounds dropDownSkinPartBounds = ElementBounds.Fixed(0, 0).WithFixedSize(skinPartWidht, dropDownheight);
        ElementBounds swatchesSkinPartBounds = ElementBounds.Fixed(0, 0).WithFixedSize(colorIconSize, colorIconSize);
        ElementBounds colorPickerSkinPartBounds = ElementBounds.Fixed(0, 0).WithFixedSize(skinPartWidht, colorPickerheight);
        ElementBounds canvasSkinPartBounds = ElementBounds.Fixed(0, 0).WithFixedSize(skinPartWidht, canvasHeight);
        ElementBounds skinPartTitleBounds = ElementBounds.Fixed(0, 0).WithFixedSize(skinPartWidht, 22);
        // left column
        ElementBounds leftColumnScrollBarBounds = leftColumnBounds.FlatCopy().WithFixedPosition(0, 0).FixedGrow(-2, -2).WithFixedWidth(scrollBarWidth).WithParent(leftColumnBounds);
        ElementBounds leftColumnClipBounds = leftColumnBounds.FlatCopy().WithFixedPosition(0, 0).FixedGrow(-scrollBarWidth, -2).WithParent(leftColumnBounds).RightOf(leftColumnScrollBarBounds);
        ElementBounds leftColumnScrollableBounds = leftColumnClipBounds.FlatCopy().WithFixedPosition(0, 0).WithParent(leftColumnBounds).RightOf(leftColumnScrollBarBounds);
        _skinTabLeftColumnBounds = leftColumnScrollableBounds;
        // right column
        ElementBounds rightColumnClipBounds = rightColumnBounds.FlatCopy().WithFixedPosition(0, 0).FixedGrow(-scrollBarWidth, -2).WithParent(rightColumnBounds);
        ElementBounds rightColumnScrollableBounds = rightColumnClipBounds.FlatCopy().WithFixedPosition(0, 0).WithParent(rightColumnBounds);
        ElementBounds rightColumnScrollBarBounds = rightColumnBounds.FlatCopy().WithFixedPosition(0, 0).FixedGrow(-2, -2).WithFixedWidth(scrollBarWidth).WithParent(rightColumnBounds).RightOf(rightColumnClipBounds);
        _skinTabRightColumnBounds = rightColumnScrollableBounds;


        composer.AddInset(leftColumnBounds, 4, 0.9f);
        composer.AddInset(previewAreaBounds, 0, 1);
        composer.AddInset(rightColumnBounds, 4, 0.9f);
        composer.AddInset(bottomButtonsBarBounds, 0, 1);
        composer.AddInset(insetBounds);
        composer.AddInset(hideClothingButtonBounds, 0, 1);

        composer.AddToggleButton(Lang.Get("playermodellib:gui-button-hide-clothing"), smallfont, OnToggleDressOnOff, hideClothingButtonBounds, "showdressedtoggle");
        composer.AddButton(Lang.Get("Randomize"), () => OnRandomizeSkin([]), randomizeButtonBounds, CairoFont.WhiteSmallText(), EnumButtonStyle.Small);
        composer.AddButton(Lang.Get("Last selection"), () => OnRandomizeSkin(GetPreviousSelection()), lastSelectionButtonBounds, CairoFont.WhiteSmallText(), EnumButtonStyle.Small);
        composer.AddButton(Lang.Get("Confirm Skin"), OnNextImpl, confirmButtonBounds, CairoFont.WhiteSmallText(), EnumButtonStyle.Small);

        InsetSlotBounds = insetBounds;

        composer.AddExtendedVerticalScrollbar(OnNewScrollbarValueSkinLeft, leftColumnScrollBarBounds, "skinparts-left-scrollbar", leftColumnBounds);
        composer.BeginClip(leftColumnClipBounds);

        IEnumerable<SkinnablePartExtended> skinParts = skinBehavior.AvailableSkinParts.Get().OfType<SkinnablePartExtended>();
        ElementBounds previousSkinPartBounds = ElementBounds.Fixed(0, 0).WithFixedSize(0, 0).WithParent(leftColumnScrollableBounds);

        bool leftColumn = true;
        bool columnBroken = false;
        double leftColumnContentHeight = 0;
        double rightColumnContentHeight = 0;
        foreach (SkinnablePartExtended skinPart in skinParts)
        {
            ElementBounds clipBounds = leftColumn ? leftColumnClipBounds : rightColumnClipBounds;
            ElementBounds skinPartParentBounds = leftColumn ? leftColumnScrollableBounds : rightColumnScrollableBounds;

            if (skinPart.Type == EnumSkinnableType.Texture && skinPart.SolidColor)
            {
                previousSkinPartBounds = ComposeColorPickerSkinPart(skinPart, composer, previousSkinPartBounds, skinPartTitleBounds, colorPickerSkinPartBounds, swatchesSkinPartBounds, skinPartParentBounds, skinBehavior, padding, clipBounds);
            }
            else if (skinPart.Type == EnumSkinnableType.Texture && skinPart.Canvas)
            {
                previousSkinPartBounds = ComposeCanvasSkinPart(skinPart, composer, previousSkinPartBounds, skinPartTitleBounds, canvasSkinPartBounds, skinPartParentBounds, skinBehavior, padding);
            }
            else if (skinPart.Type == EnumSkinnableType.Texture && !skinPart.UseDropDown)
            {
                previousSkinPartBounds = ComposeSwatchSkinPart(skinPart, composer, previousSkinPartBounds, skinPartTitleBounds, swatchesSkinPartBounds, skinPartParentBounds, skinBehavior, padding, clipBounds);
            }
            else
            {
                previousSkinPartBounds = ComposeDropDownSkinPart(skinPart, composer, previousSkinPartBounds, skinPartTitleBounds, dropDownSkinPartBounds, skinPartParentBounds, skinBehavior, padding, clipBounds);
            }

            composer.AddScrollableInset(previousSkinPartBounds, 0, 1);

            if (leftColumn)
            {
                leftColumnContentHeight += previousSkinPartBounds.fixedHeight;
            }
            else
            {
                rightColumnContentHeight += previousSkinPartBounds.fixedHeight;
            }

            if (skinPart.Colbreak && !columnBroken)
            {
                composer.EndClip();
                composer.AddExtendedVerticalScrollbar(OnNewScrollbarValueSkinRight, rightColumnScrollBarBounds, "skinparts-right-scrollbar", rightColumnBounds);
                composer.BeginClip(rightColumnClipBounds);

                previousSkinPartBounds = ElementBounds.Fixed(0, 0).WithFixedSize(0, 0).WithParent(rightColumnScrollableBounds);
                leftColumn = false;
                columnBroken = true;
            }
        }

        composer.EndClip();

        composer.GetExtendedScrollbar("skinparts-left-scrollbar").SetHeights((float)columnsHeight, (float)Math.Max(leftColumnContentHeight + columnsHeight, columnsHeight));
        composer.GetExtendedScrollbar("skinparts-right-scrollbar").SetHeights((float)columnsHeight, (float)Math.Max(rightColumnContentHeight + columnsHeight, columnsHeight));
        composer.GetExtendedScrollbar("skinparts-left-scrollbar").SetScrollbarPosition(0);
        composer.GetExtendedScrollbar("skinparts-right-scrollbar").SetScrollbarPosition(0);
        composer.GetExtendedScrollbar("skinparts-left-scrollbar").SetFixedHandleHeight(100);
        composer.GetExtendedScrollbar("skinparts-right-scrollbar").SetFixedHandleHeight(100);

        composer.GetToggleButton("showdressedtoggle").SetValue(false);
        OnToggleDressOnOff(false);
    }
    public void ComposeClassTab(GuiDialogCreateCustomCharacter dialog, GuiComposer composer, double yPosition, double padding, double slotSize, ElementBounds backgroundBounds, ElementBounds dialogBounds)
    {
        EntityShapeRenderer? renderer = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();

        yPosition -= 25;

        ElementBounds leftColBounds = ElementBounds.Fixed(0, yPosition, 0, DlgHeight - 23).FixedGrow(padding, padding);
        ElementBounds prevButtonBounds = ElementBounds.Fixed(0, yPosition + 23, 35, slotSize - 4).WithFixedPadding(2).FixedRightOf(leftColBounds, -10);
        ElementBounds centerTextBounds = ElementBounds.Fixed(0, yPosition + 25, 432, slotSize - 4 - 8).FixedRightOf(prevButtonBounds, 10);
        ElementBounds charclasssInset = centerTextBounds.ForkBoundingParent(4, 4, 4, 4);
        ElementBounds nextButtonBounds = ElementBounds.Fixed(0, yPosition + 23, 35, slotSize - 4).WithFixedPadding(2).FixedRightOf(charclasssInset, 9);

        CairoFont font = CairoFont.WhiteMediumText();
        centerTextBounds.fixedY += (centerTextBounds.fixedHeight - font.GetFontExtents().Height / RuntimeEnv.GUIScale) / 2;

        int visibleHeight = (int)Math.Max(120, DlgHeight - (yPosition + 25) - 62);
        ElementBounds charTextBounds = ElementBounds.Fixed(0, 0, 498, visibleHeight)
            .FixedUnder(prevButtonBounds, 15)
            .FixedRightOf(leftColBounds, -10);

        ElementBounds bgBounds = charTextBounds.ForkBoundingParent(6, 6, 6, 6);
        ElementBounds clipBounds = charTextBounds.FlatCopy().FixedGrow(6, 11).WithFixedOffset(0, -6);
        ElementBounds scrollbarBounds = charTextBounds.CopyOffsetedSibling(charTextBounds.fixedWidth + 7, -6, 0, 12).WithFixedWidth(20);


        InsetSlotBounds = ElementBounds.Fixed(0, yPosition + 25, 193, leftColBounds.fixedHeight - 2 * padding - 30).FixedRightOf(nextButtonBounds, 11);

        composer
            .AddInset(InsetSlotBounds, 2)
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

            .AddSmallButton(Lang.Get("Confirm Class"), OnNextImpl,
                ElementBounds.Fixed(11, DlgHeight - 24).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6),
                EnumButtonStyle.Normal)
        ;

        ClipHeight = (float)clipBounds.fixedHeight;
        composer.GetScrollbar("scrollbar").SetHeights(
            ClipHeight,
            ClipHeight
        );
        composer.GetScrollbar("scrollbar").SetScrollbarPosition(0);

        ChangeClass(0);

        OnToggleDressOnOff(false);
    }


    public ElementBounds ComposeDebugSkinPart(SkinnablePartExtended skinPart, GuiComposer composer, ElementBounds previous, ElementBounds skinPartTitleBounds, ElementBounds dropDownSkinPartBounds, ElementBounds parentBounds, PlayerSkinBehavior skinBehavior, double padding, ElementBounds clipBounds)
    {
        string partCode = skinPart.Code;
        ElementBounds partBounds = ElementBounds.Fixed(0, 0)
            .WithFixedSize(skinPartTitleBounds.fixedWidth, skinPartTitleBounds.fixedHeight + padding + dropDownSkinPartBounds.fixedHeight + padding)
            .WithParent(parentBounds)
            .FixedUnder(previous);

        skinPartTitleBounds = skinPartTitleBounds.FlatCopy().WithParent(partBounds);
        dropDownSkinPartBounds = dropDownSkinPartBounds.FlatCopy().WithParent(partBounds).FixedUnder(skinPartTitleBounds, padding);

        composer.AddRichtext(Lang.Get("skinpart-" + partCode), CairoFont.WhiteSmallText(), skinPartTitleBounds);
        string tooltip = Lang.GetIfExists("skinpartdesc-" + partCode);
        if (tooltip != null)
        {
            ElementBounds hoverTextBounds = skinPartTitleBounds.FlatCopy();
            composer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, hoverTextBounds);
        }
        composer.AddScrollableInset(dropDownSkinPartBounds);

        return partBounds;
    }

    public ElementBounds ComposeDropDownSkinPart(SkinnablePartExtended skinPart, GuiComposer composer, ElementBounds previous, ElementBounds skinPartTitleBounds, ElementBounds dropDownSkinPartBounds, ElementBounds parentBounds, PlayerSkinBehavior skinBehavior, double padding, ElementBounds clipBounds)
    {
        string partCode = skinPart.Code;
        AppliedSkinnablePartVariant? appliedVariant = skinBehavior.AppliedSkinParts.Get().FirstOrDefault(variant => variant.PartCode == partCode);
        SkinnablePartVariant[] variants = skinPart.Variants.Where(variant => variant.Category == "standard" || variant.Category == null || variant.Category == "").ToArray();

        int selectedIndex = 0;

        string[] names = new string[variants.Length];
        string[] values = new string[variants.Length];

        for (int i = 0; i < variants.Length; i++)
        {
            names[i] = Lang.Get("skinpart-" + partCode + "-" + variants[i].Code);
            values[i] = variants[i].Code;

            if (appliedVariant?.Code == values[i]) selectedIndex = i;
        }

        ElementBounds partBounds = ElementBounds.Fixed(0, 0)
            .WithFixedSize(skinPartTitleBounds.fixedWidth, skinPartTitleBounds.fixedHeight + padding + dropDownSkinPartBounds.fixedHeight + padding)
            .WithParent(parentBounds)
            .FixedUnder(previous);

        skinPartTitleBounds = skinPartTitleBounds.FlatCopy().WithFixedOffset(padding, padding).FixedGrow(-padding * 2).WithParent(partBounds);
        dropDownSkinPartBounds = dropDownSkinPartBounds.FlatCopy().WithFixedOffset(padding, padding).FixedGrow(-padding * 2).WithParent(partBounds).FixedUnder(skinPartTitleBounds, padding);

        composer.AddRichtext(Lang.Get("skinpart-" + partCode), CairoFont.WhiteSmallText(), skinPartTitleBounds);
        string tooltip = Lang.GetIfExists("skinpartdesc-" + partCode);
        if (tooltip != null)
        {
            ElementBounds hoverTextBounds = skinPartTitleBounds.FlatCopy();
            composer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, hoverTextBounds);
        }

        composer.AddScrollableDropDown(values, names, selectedIndex, (variantcode, selected) => onToggleSkinPartColor(partCode, variantcode), dropDownSkinPartBounds, clipBounds, "dropdown-" + partCode);

        return partBounds;
    }
    public ElementBounds ComposeSwatchSkinPart(SkinnablePartExtended skinPart, GuiComposer composer, ElementBounds previous, ElementBounds skinPartTitleBounds, ElementBounds swatchesSkinPartBounds, ElementBounds parentBounds, PlayerSkinBehavior skinBehavior, double padding, ElementBounds clipBounds)
    {
        string partCode = skinPart.Code;
        AppliedSkinnablePartVariant? appliedVariant = skinBehavior.AppliedSkinParts.Get().FirstOrDefault(variant => variant.PartCode == partCode);
        SkinnablePartVariant[] variants = skinPart.Variants.Where(variant => variant.Category == "standard" || variant.Category == null || variant.Category == "").ToArray();
        int selectedIndex = 0;
        int[] colors = new int[variants.Length];
        for (int i = 0; i < variants.Length; i++)
        {
            colors[i] = variants[i].Color;

            if (appliedVariant?.Code == variants[i].Code) selectedIndex = i;
        }
        int rowsNumber = (int)Math.Ceiling((float)colors.Length / 7);


        ElementBounds partBounds = ElementBounds.Fixed(0, 0)
            .WithFixedSize(skinPartTitleBounds.fixedWidth, skinPartTitleBounds.fixedHeight + padding + (swatchesSkinPartBounds.fixedHeight + padding) * rowsNumber + padding)
            .WithParent(parentBounds)
            .FixedUnder(previous, padding);
        skinPartTitleBounds = skinPartTitleBounds.FlatCopy().WithFixedOffset(padding, padding).FixedGrow(-padding * 2).WithParent(partBounds);
        swatchesSkinPartBounds = swatchesSkinPartBounds.FlatCopy().WithFixedOffset(padding * 2, padding).WithParent(partBounds).FixedUnder(skinPartTitleBounds, padding);

        composer.AddRichtext(Lang.Get("skinpart-" + partCode), CairoFont.WhiteSmallText(), skinPartTitleBounds);
        string tooltip = Lang.GetIfExists("skinpartdesc-" + partCode);
        if (tooltip != null)
        {
            ElementBounds hoverTextBounds = skinPartTitleBounds.FlatCopy();
            composer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, hoverTextBounds);
        }

        swatchesSkinPartBounds = swatchesSkinPartBounds.FlatCopy().WithParent(partBounds);
        composer.AddScrollableColorListPicker(colors, (index) => onToggleSkinPartColor(partCode, index), swatchesSkinPartBounds, 190, clipBounds, "picker-" + partCode);

        for (int i = 0; i < colors.Length; i++)
        {
            GuiElementScrollableColorListPicker picker = composer.GetScrollableColorListPicker("picker-" + partCode + "-" + i);
            picker.ShowToolTip = true;
            if (Lang.HasTranslation("skinpart-" + partCode + "-" + variants[i].Code))
            {
                picker.TooltipText = Lang.Get("skinpart-" + partCode + "-" + variants[i].Code);
            }
            else
            {
                picker.TooltipText = Lang.Get("color-" + variants[i].Code);
            }
        }

        composer.ScrollableColorListPickerSetValue("picker-" + partCode, selectedIndex);


        return partBounds;
    }
    public ElementBounds ComposeColorPickerSkinPart(SkinnablePartExtended skinPart, GuiComposer composer, ElementBounds previous, ElementBounds skinPartTitleBounds, ElementBounds colorPickerSkinPartBounds, ElementBounds swatchesSkinPartBounds, ElementBounds parentBounds, PlayerSkinBehavior skinBehavior, double padding, ElementBounds clipBounds)
    {
        string partCode = skinPart.Code;
        AppliedSkinnablePartVariant? appliedVariant = skinBehavior.AppliedSkinParts.Get().FirstOrDefault(variant => variant.PartCode == partCode);
        SkinnablePartVariant[] variants = skinPart.Variants.Where(variant => variant.Category == "standard" || variant.Category == null || variant.Category == "").ToArray();
        int selectedIndex = 0;
        int[] colors = new int[variants.Length];
        for (int i = 0; i < variants.Length; i++)
        {
            colors[i] = variants[i].Color;

            if (appliedVariant?.Code == variants[i].Code) selectedIndex = i;
        }
        int rowsNumber = (int)Math.Ceiling((float)colors.Length / 7);

        ElementBounds partBounds = ElementBounds.Fixed(0, 0)
            .WithFixedSize(skinPartTitleBounds.fixedWidth, skinPartTitleBounds.fixedHeight + padding + colorPickerSkinPartBounds.fixedHeight + padding + (swatchesSkinPartBounds.fixedHeight + padding) * rowsNumber + padding)
            .WithParent(parentBounds)
            .FixedUnder(previous);
        skinPartTitleBounds = skinPartTitleBounds.FlatCopy().WithFixedOffset(padding, padding).FixedGrow(-padding * 2).WithParent(partBounds);
        colorPickerSkinPartBounds = colorPickerSkinPartBounds.FlatCopy().WithFixedOffset(padding, padding).FixedGrow(-padding * 2).WithParent(partBounds).FixedUnder(skinPartTitleBounds, padding);
        swatchesSkinPartBounds = swatchesSkinPartBounds.FlatCopy().WithFixedOffset(padding * 2, padding).WithParent(partBounds).FixedUnder(colorPickerSkinPartBounds, padding);

        composer.AddRichtext(Lang.Get("skinpart-" + partCode), CairoFont.WhiteSmallText(), skinPartTitleBounds);
        string tooltip = Lang.GetIfExists("skinpartdesc-" + partCode);
        if (tooltip != null)
        {
            ElementBounds hoverTextBounds = skinPartTitleBounds.FlatCopy();
            composer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, hoverTextBounds);
        }

        composer.AddColorPicker(
            rgba => onToggleSkinPartActuallyColor(partCode, rgba),
            colorPickerSkinPartBounds,
            [1.0, 1.0, 1.0, 1.0],
            key: "colorpicker-" + partCode
            );

        swatchesSkinPartBounds = swatchesSkinPartBounds.FlatCopy().WithParent(partBounds);
        composer.AddScrollableColorListPicker(colors, (index) => onTogglePickerPartColor(partCode, index), swatchesSkinPartBounds, 190, clipBounds, "picker-" + partCode);

        for (int i = 0; i < colors.Length; i++)
        {
            GuiElementScrollableColorListPicker picker = composer.GetScrollableColorListPicker("picker-" + partCode + "-" + i);
            picker.ShowToolTip = true;
            if (Lang.HasTranslation("skinpart-" + partCode + "-" + variants[i].Code))
            {
                picker.TooltipText = Lang.Get("skinpart-" + partCode + "-" + variants[i].Code);
            }
            else
            {
                picker.TooltipText = Lang.Get("color-" + variants[i].Code);
            }
        }

        composer.ScrollableColorListPickerSetValue("picker-" + partCode, selectedIndex);

        return partBounds;
    }
    public ElementBounds ComposeCanvasSkinPart(SkinnablePartExtended skinPart, GuiComposer composer, ElementBounds previous, ElementBounds skinPartTitleBounds, ElementBounds canvasSkinPartBounds, ElementBounds parentBounds, PlayerSkinBehavior skinBehavior, double padding)
    {
        string partCode = skinPart.Code;
        AppliedSkinnablePartVariant? appliedVariant = skinBehavior.AppliedSkinParts.Get().FirstOrDefault(variant => variant.PartCode == partCode);

        ElementBounds partBounds = ElementBounds.Fixed(0, 0)
            .WithFixedSize(skinPartTitleBounds.fixedWidth, skinPartTitleBounds.fixedHeight + padding + canvasSkinPartBounds.fixedHeight + padding)
            .WithParent(parentBounds)
            .FixedUnder(previous);
        skinPartTitleBounds = skinPartTitleBounds.FlatCopy().WithFixedOffset(padding, padding).FixedGrow(-padding * 2).WithParent(partBounds);
        canvasSkinPartBounds = canvasSkinPartBounds.FlatCopy().WithFixedOffset(padding, padding).FixedGrow(-padding * 2).WithParent(partBounds).FixedUnder(skinPartTitleBounds, padding);

        composer.AddRichtext(Lang.Get("skinpart-" + partCode), CairoFont.WhiteSmallText(), skinPartTitleBounds);
        string tooltip = Lang.GetIfExists("skinpartdesc-" + partCode);
        if (tooltip != null)
        {
            ElementBounds hoverTextBounds = skinPartTitleBounds.FlatCopy();
            composer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, hoverTextBounds);
        }


        int colorsNumber = Math.Clamp(skinPart.ColorsNumber, 1, 14);

        TextureCanvasData canvasData = new()
        {
            Width = skinPart.Size[0],
            Height = skinPart.Size[1],
            Colors = new int[colorsNumber],
            Pixels = new int[skinPart.Size[0] * skinPart.Size[1]]
        };
        for (int i = 0; i < colorsNumber; i++)
        {
            canvasData.Colors[i] = -1;
        }

        if (appliedVariant?.Code != null)
        {
            canvasData = TextureCanvasData.Deserialize(appliedVariant.Code);
        }

        composer.AddCanvasEditor(canvasData, canvasSkinPartBounds, data => onToggleSkinPartCanvas(partCode, data), key: "canvas-" + partCode);

        return partBounds;
    }

    public void onToggleModelGroup(string groupCode, GuiComposer? composer = null)
    {
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();
        GetCustomModels(system, groupCode, out string[] modelValues, out _, out _, out _);

        if (modelValues.Length > 0)
        {
            onToggleModel(modelValues[0], composer);
        }
    }

    public Dictionary<string, string> GetPreviousSelection()
    {
        string currentModel = GetCurrentModel();

        Dictionary<string, Dictionary<string, string>> allSelections = Api.LoadModConfig<Dictionary<string, Dictionary<string, string>>>(PreviousSelectionFile) ?? [];

        if (allSelections.TryGetValue(currentModel, out Dictionary<string, string>? result))
        {
            return result;
        }

        return [];
    }
    public void SavePreviousSelection(Dictionary<string, string> selection)
    {
        string currentModel = GetCurrentModel();

        Dictionary<string, Dictionary<string, string>> allSelections = Api.LoadModConfig<Dictionary<string, Dictionary<string, string>>>(PreviousSelectionFile) ?? [];

        allSelections[currentModel] = selection;

        Api.StoreModConfig(allSelections, PreviousSelectionFile);
    }

    public string GetCurrentModel()
    {
        PlayerSkinBehavior? skinBehavior = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (skinBehavior == null) return system.DefaultModelCode;

        _ = GetCurrentModelAndGroup(system, skinBehavior, out int modelIndex, out int groupIndex);
        GetCustomGroups(system, out string[] groupValues, out _);
        GetCustomModels(system, groupValues[groupIndex], out string[] modelValues, out _, out _, out _);

        return modelValues[modelIndex];
    }

    public bool ChangeModel(int dir, GuiComposer? composer = null)
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
    public void OnNewScrollbarValueModel(float value)
    {
        GuiElementRichtext? richtextElem = Composer?.GetRichtext("modelDescription");

        if (richtextElem != null)
        {
            richtextElem.Bounds.fixedY = 0 - value;
            richtextElem.Bounds.CalcWorldBounds();
        }
    }
    public void OnNewScrollbarValue(float value)
    {
        GuiElementRichtext? richtextElem = Composer?.GetRichtext("characterDesc");

        if (richtextElem != null)
        {
            richtextElem.Bounds.fixedY = 0 - value;
            richtextElem.Bounds.CalcWorldBounds();
        }
    }

    public void OnNewScrollbarValueSkinLeft(float value)
    {
        if (_skinTabLeftColumnBounds != null)
        {
            _skinTabLeftColumnBounds.fixedY = -value;
            _skinTabLeftColumnBounds.CalcWorldBounds();
        }
    }
    public void OnNewScrollbarValueSkinRight(float value)
    {
        if (_skinTabRightColumnBounds != null)
        {
            _skinTabRightColumnBounds.fixedY = -value;
            _skinTabRightColumnBounds.CalcWorldBounds();
        }
    }

    public static double[] HexArgbToDoubleArray(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return [0, 0, 0, 0];

        if (hex.StartsWith('#'))
        {
            hex = hex.Substring(1);
        }

        if (hex.Length != 8) return [0, 0, 0, 0];

        byte a = Convert.ToByte(hex.Substring(0, 2), 16);
        byte r = Convert.ToByte(hex.Substring(2, 2), 16);
        byte g = Convert.ToByte(hex.Substring(4, 2), 16);
        byte b = Convert.ToByte(hex.Substring(6, 2), 16);

        return
        [
            a / 255.0,
            r / 255.0,
            g / 255.0,
            b / 255.0
        ];
    }
    public bool OnRandomizeSkin(Dictionary<string, string> preselection)
    {
        EntityPlayer entity = capi.World.Player.Entity;

        EntityBehaviorPlayerInventory? bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        if (bh == null) return false;
        bh.doReloadShapeAndSkin = false;

        PlayerSkinBehavior? skinMod = entity.GetBehavior<PlayerSkinBehavior>();
        skinMod?.RandomizeSkin(entity, preselection);

        if (skinMod == null) return false;

        foreach (AppliedSkinnablePartVariant? appliedPart in skinMod.AppliedSkinParts.Get())
        {
            string partcode = appliedPart.PartCode;

            SkinnablePart? skinPart = skinMod.AvailableSkinParts.Get().FirstOrDefault(part => part.Code == partcode);
            if (skinPart == null) continue;

            SkinnablePartVariant[] variants = skinPart.Variants.Where(variant => variant.Category == "standard" || variant.Category == null || variant.Category == "").ToArray();

            int index = variants.IndexOf(part => part.Code == appliedPart.Code);

            if (skinPart.Type == EnumSkinnableType.Texture && skinPart is SkinnablePartExtended ext && ext.SolidColor)
            {
                double[] color = HexArgbToDoubleArray(appliedPart.Code);
                Composers["createcharacter"].GetColorPicker("colorpicker-" + partcode)?.SetColor(color[1], color[2], color[3], color[0]);
                Composers["createcharacter"].ScrollableColorListPickerSetValue("picker-" + partcode, index);
            }
            else if (skinPart.Type == EnumSkinnableType.Texture && skinPart is SkinnablePartExtended ext2 && ext2.Canvas)
            {
                TextureCanvasData newData = TextureCanvasData.Deserialize(appliedPart.Code);
                newData.Width = ext2.Size[0];
                newData.Height = ext2.Size[1];
                int colorsNumber = ext2.ColorsNumber + 1;
                if (newData.Colors.Length < colorsNumber)
                {
                    for (int i = newData.Colors.Length; i <= colorsNumber; i++)
                    {
                        newData.Colors = newData.Colors.Append(-1);
                    }
                }
                else if (newData.Colors.Length > colorsNumber)
                {
                    newData.Colors = newData.Colors[0..colorsNumber];
                }
                Composers["createcharacter"].GetCanvasEditor("canvas-" + partcode)?.SetData(newData);
            }
            else if (skinPart.Type == EnumSkinnableType.Texture && !skinPart.UseDropDown)
            {
                Composers["createcharacter"].ScrollableColorListPickerSetValue("picker-" + partcode, index);
            }
            else
            {
                Composers["createcharacter"].GetScrollableDropDown("dropdown-" + partcode)?.SetSelectedIndex(index);
            }
        }

        bh.doReloadShapeAndSkin = true;
        ReTesselate();

        return true;
    }
    public void OnToggleDressOnOff(bool on)
    {
        HideClothing = on;
        WearablesTesselatorBehavior? tesselator = capi.World.Player.Entity.GetBehavior<WearablesTesselatorBehavior>();
        if (tesselator != null)
        {
            tesselator.TesselateItems = !HideClothing;
        }
        ReTesselate();
    }
    public void onToggleSkinPartColor(string partCode, string variantCode)
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        skinMod?.SelectSkinPart(partCode, variantCode);
    }
    public void onToggleSkinPartColor(string partCode, int index)
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();

        if (skinMod == null) return;

        string variantCode = skinMod.AvailableSkinPartsByCode.GetValue(partCode)?.Variants[index].Code ?? "";

        skinMod.SelectSkinPart(partCode, variantCode);
    }
    public void onTogglePickerPartColor(string partCode, int index)
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();

        if (skinMod == null) return;

        string colorValue = skinMod.AvailableSkinPartsByCode.GetValue(partCode)?.Variants[index].Code ?? "#00000000";
        double[] color = HexArgbToDoubleArray(colorValue);
        Composers["createcharacter"].GetColorPicker("colorpicker-" + partCode)?.SetColor(color[1], color[2], color[3], color[0]);
    }
    public void onToggleSkinPartActuallyColor(string partCode, double[] color)
    {
        string colorHex = RgbaToArgbHex(color);
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        skinMod?.SelectSkinPart(partCode, colorHex);
    }
    public void onToggleSkinPartCanvas(string partCode, TextureCanvasData data)
    {
        string serializedCanvas = data.Serialize();
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        skinMod?.SelectSkinPart(partCode, serializedCanvas);
    }
    public static string RgbaToArgbHex(double[] rgba)
    {
        if (rgba == null || rgba.Length != 4) throw new ArgumentException("Input must be a double[4] array (RGBA).");

        byte r = (byte)(Math.Clamp(rgba[0], 0.0, 1.0) * 255);
        byte g = (byte)(Math.Clamp(rgba[1], 0.0, 1.0) * 255);
        byte b = (byte)(Math.Clamp(rgba[2], 0.0, 1.0) * 255);
        byte a = (byte)(Math.Clamp(rgba[3], 0.0, 1.0) * 255);

        return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
    }
    public void onToggleModel(string modelCode, GuiComposer? composer = null)
    {
        EntityBehaviorPlayerInventory? bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (bh == null || skinMod == null) return;

        skinMod.SetCurrentModel(modelCode, CurrentModelSize);
        bh.doReloadShapeAndSkin = true;

        List<CharacterClass> availableClasses = GetAvailableClasses(system, skinMod.CurrentModelCode);
        CharacterSystem.setCharacterClass(capi.World.Player.Entity, availableClasses[0].Code, true);

        try
        {
            float minSize = system.CustomModels[modelCode].SizeRange.X;
            float maxSize = system.CustomModels[modelCode].SizeRange.Y;
            CurrentModelSize = GameMath.Clamp(CurrentModelSize, minSize, maxSize);
            composer?.GetSlider("modelSizeSlider").SetValues((int)(CurrentModelSize * 100), (int)(minSize * 100), (int)(maxSize * 100), 1, unit: "%");
            OnModelSizeChanged();
        }
        catch
        {
            Debug.WriteLine("Failed to reset model size");
        }

        ComposeGuis();

        OnRandomizeSkin(GetPreviousSelection());
    }
    public bool OnNextImpl()
    {
        if (CurrentTab == TabsEnabled.Count(entry => entry.Value) - 1)
        {
            return OnConfirm();
        }

        CurrentTab = GameMath.Clamp(CurrentTab + 1, 0, TabsEnabled.Count(entry => entry.Value));
        ComposeGuis();
        return true;
    }
    public void onTabClicked(int tabid)
    {
        CurrentTab = tabid;
        ComposeGuis();
    }
    public bool OnConfirm()
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        if (skinMod != null)
        {
            Dictionary<string, string> selection = skinMod.AppliedSkinParts.Get().ToDictionary(part => part.PartCode, part => part.Code);

            SavePreviousSelection(selection);
        }

        DidSelect = true;
        TryClose();
        return true;
    }
    public new void OnTitleBarClose()
    {
        TryClose();
    }
    public void ChangeClass(int dir)
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (skinMod == null) return;

        List<CharacterClass> availableClasses = GetAvailableClasses(system, skinMod.CurrentModelCode);

        CurrentClassIndex = GameMath.Mod(CurrentClassIndex + dir, availableClasses.Count);

        CharacterClass chclass = availableClasses[CurrentClassIndex];
        Composers["createcharacter"]?.GetDynamicText("className").SetNewText(Lang.Get("characterclass-" + chclass.Code));

        StringBuilder fulldesc = new();

        fulldesc.AppendLine();
        fulldesc.AppendLine(Lang.Get("characterdesc-" + chclass.Code));
        fulldesc.AppendLine();
        fulldesc.AppendLine(Lang.Get("traits-title"));


        IOrderedEnumerable<Trait> chartraitsExtra = CustomModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits
            .Where(CharacterSystem.TraitsByCode.ContainsKey)
            .Select(code => CharacterSystem.TraitsByCode[code])
            .OrderBy(trait => (int)trait.Type);
        IOrderedEnumerable<Trait> chartraits = chclass.Traits
            .Where(code => !CustomModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits.Contains(code))
            .Where(CharacterSystem.TraitsByCode.ContainsKey)
            .Select(code => CharacterSystem.TraitsByCode[code])
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

        Composer?.GetScrollbar("scrollbar")?.SetHeights(
            ClipHeight,
            (float)(Math.Max(ClipHeight, Composer.GetRichtext("characterDesc")?.TotalHeight ?? ClipHeight))
        );
        Composer?.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);

        CharacterSystem.setCharacterClass(capi.World.Player.Entity, chclass.Code, true);

        ReTesselate();
    }
    public void ReTesselate()
    {
        EntityShapeRenderer? renderer = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();
    }
    public void OnModelSizeChanged()
    {
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();

        skinMod?.SetCurrentModel(skinMod.CurrentModelCode, CurrentModelSize);
    }
    public string CreateModelDescription(CustomModelsSystem system, string model)
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

        IEnumerable<string> missingTraits = modelConfig.ExtraTraits.Where(code => !CharacterSystem.TraitsByCode.ContainsKey(code));
        if (missingTraits.Any())
        {
            string missingTraitsList = missingTraits.Aggregate((a, b) => $"{a}, {b}");
            Log.Error(Api, this, $"Custom model '{model}' has traits that does not exist: {missingTraitsList}.\nIt can be caused by either bug in the mod that adds that model, or it can be caused by some other mods breaking vanilla character system.");
            IEnumerable<string> existingTraits = CharacterSystem.TraitsByCode.Keys;
            if (existingTraits.Any())
            {
                string existingTraitsList = existingTraits.Aggregate((a, b) => $"{a},\n{b}");
                Log.Error(Api, this, $"All existing traits:\n{existingTraitsList}\n");
            }

            fullDescription.AppendLine();
            fullDescription.AppendLine($"<font color=\"#ff8484\">##########################################################</font>");
            fullDescription.AppendLine($"<font color=\"#ff8484\">Error occurred while loading custom model traits. Check client-main.log file.</font>");
            fullDescription.AppendLine($"<font color=\"#ff8484\">##########################################################</font>");
        }

        IOrderedEnumerable<Trait> traits = modelConfig.ExtraTraits
            .Where(CharacterSystem.TraitsByCode.ContainsKey)
            .Select(code => CharacterSystem.TraitsByCode[code])
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
    public static void AppendTraits(StringBuilder fullDescription, IEnumerable<Trait> traits)
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
    public List<CharacterClass> GetAvailableClasses(CustomModelsSystem system, string model)
    {
        HashSet<string> availableClassesForModel = system.CustomModels[model].AvailableClasses;
        HashSet<string> skippedClassesForModel = system.CustomModels[model].SkipClasses;
        HashSet<string> exclusiveClassesForModel = system.CustomModels[model].ExclusiveClasses;

        IEnumerable<CharacterClass> availableClasses = CharacterSystem.characterClasses.Where(element => availableClassesForModel.Contains(element.Code));
        if (!availableClasses.Any())
        {
            availableClasses = CharacterSystem.characterClasses;
        }

        availableClasses = availableClasses.Where(element => !system.ExclusiveClasses.Contains(element.Code) || exclusiveClassesForModel.Contains(element.Code));

        availableClasses = availableClasses.Where(element => !skippedClassesForModel.Contains(element.Code)).Where(element => element.Enabled);

        return availableClasses.ToList();
    }
    public void GetCustomGroups(CustomModelsSystem system, out string[] groupValues, out string[] groupNames)
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
    public void GetCustomModels(CustomModelsSystem system, string group, out string[] modelValues, out string[] modelNames, out AssetLocation?[] modelIcons, out AssetLocation? groupIcon)
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
    public string[] GetAvailableModels(CustomModelsSystem system)
    {
        string[] extraCustomModels = capi?.World?.Player?.Entity?.WatchedAttributes?.GetStringArray("extraCustomModels", []) ?? [];
        return system.CustomModels.Where(entry => entry.Value.Enabled || extraCustomModels.Contains(entry.Value.Code)).Select(entry => entry.Key).ToArray();
    }
    public static string GetCustomModelLangEntry(AssetLocation code, string? name) => name ?? Lang.Get($"{code.Domain}:playermodel-{code.Path}");
    public string GetCustomGroupLangEntry(AssetLocation code) => Lang.GetIfExists($"game:playermodelgroup-{code.Path}") ?? Lang.Get($"{code.Domain}:playermodel-{code.Path}");
    public bool GetCurrentModelAndGroup(CustomModelsSystem system, PlayerSkinBehavior skinBehavior, out int modelIndex, out int groupIndex)
    {
        GetCustomGroups(system, out string[] groupValues, out _);

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
    public void ClientSelectionDone(IInventory? characterInv, string characterClass, bool didSelect)
    {
        List<ClothStack> clothesPacket = [];
        if (characterInv != null)
        {
            for (int i = 0; i < characterInv.Count; i++)
            {
                ItemSlot slot = characterInv[i];
                if (slot.Itemstack == null) continue;

                clothesPacket.Add(new ClothStack()
                {
                    Code = slot.Itemstack.Collectible.Code.ToShortString(),
                    SlotNum = i,
                    Class = slot.Itemstack.Class
                });
            }
        }

        Dictionary<string, string> skinParts = [];
        PlayerSkinBehavior? bh = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();

        List<AppliedSkinnablePartVariant> applied = bh?.AppliedSkinParts.Get().ToList() ?? [];
        foreach (AppliedSkinnablePartVariant? val in applied)
        {
            skinParts[val.PartCode] = val.Code;
        }
        bh?.AppliedSkinParts.Set(applied);

        capi.Network.GetChannel("charselection").SendPacket(new CharacterSelectionPacket()
        {
            Clothes = clothesPacket.ToArray(),
            DidSelect = didSelect,
            SkinParts = skinParts,
            CharacterClass = characterClass,
            VoicePitch = bh?.VoicePitch,
            VoiceType = bh?.VoiceType
        });

        capi.Network.SendPlayerNowReady();

        capi.Event.PushEvent("finishcharacterselection");
    }
}
