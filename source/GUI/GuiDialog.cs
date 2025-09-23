using System.Diagnostics;
using System.Reflection;
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

    public static int RenderState { get; private set; } = 0; // Is used to disable model size compensation when rendering model in model tab

    public GuiDialogCreateCustomCharacter(ICoreClientAPI api, CharacterSystem characterSystem) : base(api, characterSystem)
    {
        _characterSystem = characterSystem;
        _customModelsSystem = api.ModLoader.GetModSystem<CustomModelsSystem>();
    }
    public override void OnGuiOpened()
    {
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
    }
    public override void OnGuiClosed()
    {
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
                    _insetSlotBounds.renderX + pad - GuiElement.scaled(110),
                    _insetSlotBounds.renderY + pad - GuiElement.scaled(15),
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
                    _insetSlotBounds.renderX + pad - GuiElement.scaled(110),
                    _insetSlotBounds.renderY + pad - GuiElement.scaled(15),
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


    private bool _didSelect = false;
    private IInventory? _characterInventory;
    private ElementBounds? _insetSlotBounds;
    private readonly CharacterSystem _characterSystem;
    private readonly CustomModelsSystem _customModelsSystem;
    private int _currentClassIndex = 0;
    private EnumCreateCharacterTabs _currentTab = 0;
    private float _charZoom = 1f;
    private bool _charNaked = true;
    private readonly int _dlgHeight = 433 + 80;
    private float _yaw = -GameMath.PIHALF + 0.3f;
    private float _height = 0;
    private const float _heightLimit = 150;
    private bool _rotateCharacter;
    private readonly Vec4f _lighPos = new Vec4f(-1, -1, 0, 0).NormalizeXYZ();
    private readonly Matrixf _mat = new();
    private readonly MethodInfo? _clientSelectionDone = typeof(CharacterSystem).GetMethod("ClientSelectionDone", BindingFlags.NonPublic | BindingFlags.Instance);
    private float _currentModelSize = 1f;

    private new void ComposeGuis()
    {
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

        composer.Compose();

        ScrollPatches.Composed();
    }

    private void ComposeModelTab(GuiComposer composer, CustomModelsSystem system, double yPosition, double padding, double slotSize)
    {
        PlayerSkinBehavior? skinBehavior = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        if (skinBehavior == null) return;

        GetCustomModels(system, out string[] modelValues, out string[] modelNames);

        int modelIndex = 0;
        bool modelFound = false;

        for (int index = 0; index < modelValues.Length; index++)
        {
            if (modelValues[index] == skinBehavior.CurrentModelCode)
            {
                modelIndex = index;
                modelFound = true;
                break;
            }
        }

        if (!modelFound)
        {
            onToggleModel(modelValues[modelIndex]);
        }

        yPosition -= 10;

        ElementBounds leftColBounds = ElementBounds.Fixed(0, yPosition, 0, _dlgHeight - 47).FixedGrow(2 * padding, 2 * padding);
        _insetSlotBounds = ElementBounds.Fixed(0, yPosition + 25, 190, leftColBounds.fixedHeight - 2 * padding + 10).FixedRightOf(leftColBounds, 10);

        ElementBounds centerTextBounds = ElementBounds.Fixed(0, yPosition + 25, 480, slotSize - 4 - 8).FixedRightOf(_insetSlotBounds, 20);
        ElementBounds dropBoxInset = centerTextBounds.ForkBoundingParent(4, 4, 4, 4);

        CairoFont font = CairoFont.WhiteMediumText();
        centerTextBounds.fixedY += (centerTextBounds.fixedHeight - font.GetFontExtents().Height / RuntimeEnv.GUIScale) / 2;

        CairoFont dropDownFont = CairoFont.WhiteSmallishText();

        ElementBounds sizeTextBounds = ElementBounds.Fixed(0, 0, 120, 20).FixedUnder(centerTextBounds, 60).FixedRightOf(_insetSlotBounds, 20);
        ElementBounds sizeSliderBounds = ElementBounds.Fixed(0, 0, 369, 20).FixedUnder(centerTextBounds, 60).FixedRightOf(sizeTextBounds, 0);
        ElementBounds descriptionTextBounds = ElementBounds.Fixed(0, 0, 480, 100).FixedUnder(sizeSliderBounds, 20).FixedRightOf(_insetSlotBounds, 20);

        composer.AddInset(_insetSlotBounds, 2);
        composer.AddInset(dropBoxInset, 2);

        GuiElementDropDown dropDown = new(
            composer.Api,
            modelValues,
            modelNames,
            modelIndex,
            (variantCode, selected) => onToggleModel(variantCode),
            centerTextBounds,
            dropDownFont.Clone().WithOrientation(EnumTextOrientation.Left),
            multiSelect: false);

        composer.AddInteractiveElement(dropDown, null);

        float minSize = skinBehavior.CurrentModel.SizeRange.X;
        float maxSize = skinBehavior.CurrentModel.SizeRange.Y;
        _currentModelSize = skinBehavior.CurrentSize;
        _currentModelSize = GameMath.Clamp(_currentModelSize, minSize, maxSize);

        composer.AddRichtext(Lang.Get("playermodellib:model-size-slider"), CairoFont.WhiteSmallText(), sizeTextBounds);
        composer.AddSlider(value => { _currentModelSize = value / 100f; OnModelSizeChanged(); return true; }, sizeSliderBounds, "modelSizeSlider");
        composer.GetSlider("modelSizeSlider").SetValues((int)(_currentModelSize * 100), (int)(minSize * 100), (int)(maxSize * 100), 1, unit: "%");

        composer.AddRichtext("", CairoFont.WhiteDetailText(), descriptionTextBounds, "modelDescription");

        composer.AddSmallButton(Lang.Get("Confirm model"), OnNextImpl, ElementBounds.Fixed(0, _dlgHeight - 30).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal);

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

            if (skinpart.Type == EnumSkinnableType.Texture && !skinpart.UseDropDown)
            {
                int selectedIndex = 0;
                int[] colors = new int[skinpart.Variants.Length];

                for (int i = 0; i < skinpart.Variants.Length; i++)
                {
                    colors[i] = skinpart.Variants[i].Color;

                    if (appliedVar?.Code == skinpart.Variants[i].Code) selectedIndex = i;
                }


                bounds = bounds.BelowCopy(0, 10).WithFixedSize(210, 22);
                composer.AddRichtext(Lang.Get("skinpart-" + code), CairoFont.WhiteSmallText(), bounds);

                bounds = bounds.BelowCopy(0, 0).WithFixedSize(colorIconSize, colorIconSize);
                composer.AddColorListPicker(colors, (index) => onToggleSkinPartColor(code, index), bounds, 180, "picker-" + code);

                for (int i = 0; i < colors.Length; i++)
                {
                    GuiElementColorListPicker picker = composer.GetColorListPicker("picker-" + code + "-" + i);
                    picker.ShowToolTip = true;
                    if (Lang.HasTranslation("skinpart-" + code + "-" + skinpart.Variants[i].Code))
                    {
                        picker.TooltipText = Lang.Get("skinpart-" + code + "-" + skinpart.Variants[i].Code);
                    }
                    else
                    {
                        picker.TooltipText = Lang.Get("color-" + skinpart.Variants[i].Code);
                    }
                }

                composer.ColorListPickerSetValue("picker-" + code, selectedIndex);
            }
            else
            {
                int selectedIndex = 0;

                string[] names = new string[skinpart.Variants.Length];
                string[] values = new string[skinpart.Variants.Length];

                for (int i = 0; i < skinpart.Variants.Length; i++)
                {
                    names[i] = Lang.Get("skinpart-" + code + "-" + skinpart.Variants[i].Code);
                    values[i] = skinpart.Variants[i].Code;

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
            .AddButton(Lang.Get("Last selection"), () => { return OnRandomizeSkin(_characterSystem.getPreviousSelection()); }, ElementBounds.Fixed(130, _dlgHeight - 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(8, 6), CairoFont.WhiteSmallText(), EnumButtonStyle.Small)
            .EndIf();

        composer.AddSmallButton(Lang.Get("Confirm Skin"), OnNextImpl, ElementBounds.Fixed(0, _dlgHeight - 25).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal);

        composer.GetToggleButton("showdressedtoggle").SetValue(!_charNaked);
    }
    private void ComposeClassTab(GuiComposer composer, double yPosition, double padding, double slotSize)
    {
        EntityShapeRenderer? renderer = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();

        yPosition -= 10;

        ElementBounds leftColBounds = ElementBounds.Fixed(0, yPosition, 0, _dlgHeight - 47).FixedGrow(2 * padding, 2 * padding);
        _insetSlotBounds = ElementBounds.Fixed(0, yPosition + 25, 190, leftColBounds.fixedHeight - 2 * padding + 10).FixedRightOf(leftColBounds, 10);

        ElementBounds prevButtonBounds = ElementBounds.Fixed(0, yPosition + 25, 35, slotSize - 4).WithFixedPadding(2).FixedRightOf(_insetSlotBounds, 20);
        ElementBounds centerTextBounds = ElementBounds.Fixed(0, yPosition + 25, 200, slotSize - 4 - 8).FixedRightOf(prevButtonBounds, 20);

        ElementBounds charclasssInset = centerTextBounds.ForkBoundingParent(4, 4, 4, 4);

        ElementBounds nextButtonBounds = ElementBounds.Fixed(0, yPosition + 25, 35, slotSize - 4).WithFixedPadding(2).FixedRightOf(charclasssInset, 20);

        CairoFont font = CairoFont.WhiteMediumText();
        centerTextBounds.fixedY += (centerTextBounds.fixedHeight - font.GetFontExtents().Height / RuntimeEnv.GUIScale) / 2;

        ElementBounds charTextBounds = ElementBounds.Fixed(0, 0, 480, 100).FixedUnder(prevButtonBounds, 20).FixedRightOf(_insetSlotBounds, 20);

        composer
            .AddInset(_insetSlotBounds, 2)

            .AddIconButton("left", (on) => ChangeClass(-1), prevButtonBounds.FlatCopy())
            .AddInset(charclasssInset, 2)
            .AddDynamicText("Commoner", font.Clone().WithOrientation(EnumTextOrientation.Center), centerTextBounds, "className")
            .AddIconButton("right", (on) => ChangeClass(1), nextButtonBounds.FlatCopy())
            .AddRichtext("", CairoFont.WhiteDetailText(), charTextBounds, "characterDesc")
            .AddSmallButton(Lang.Get("Confirm Class"), OnConfirm, ElementBounds.Fixed(0, _dlgHeight - 30).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal)
        ;

        ChangeClass(0);
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
            int index = skinPart.Variants.IndexOf(part => part.Code == appliedPart.Code);

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
    private void onToggleModel(string modelCode)
    {
        EntityBehaviorPlayerInventory? bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        PlayerSkinBehavior? skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        if (bh == null || skinMod == null) return;

        skinMod.SetCurrentModel(modelCode, _currentModelSize);
        bh.doReloadShapeAndSkin = true;

        List<CharacterClass> availableClasses = GetAvailableClasses(system, skinMod.CurrentModelCode);
        _characterSystem.setCharacterClass(capi.World.Player.Entity, availableClasses[0].Code, true);

        ComposeGuis();

        OnRandomizeSkin(new Dictionary<string, string>());
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

        fulldesc.AppendLine(Lang.Get("characterdesc-" + chclass.Code));
        fulldesc.AppendLine();
        fulldesc.AppendLine(Lang.Get("traits-title"));


        IOrderedEnumerable<Trait> chartraitsExtra = _customModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits.Select(code => _characterSystem.TraitsByCode[code]).OrderBy(trait => (int)trait.Type);
        IOrderedEnumerable<Trait> chartraits = chclass.Traits.Where(code => !_customModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits.Contains(code)).Select(code => _characterSystem.TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

        AppendTraits(fulldesc, chartraitsExtra);

        if (chartraitsExtra.Any())
        {
            fulldesc.AppendLine("");
        }

        AppendTraits(fulldesc, chartraits);

        if (chclass.Traits.Length == 0)
        {
            fulldesc.AppendLine(Lang.Get("No positive or negative traits"));
        }

        Composers["createcharacter"].GetRichtext("characterDesc").SetNewText(fulldesc.ToString(), CairoFont.WhiteDetailText());

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

        fullDescription.AppendLine(Lang.Get($"{modelSplit[0]}:modeldesc-{modelSplit[1]}"));

        IOrderedEnumerable<Trait> traits = modelConfig.ExtraTraits.Select(code => _characterSystem.TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

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

                attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", attribute, value)));
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
    private void GetCustomModels(CustomModelsSystem system, out string[] modelValues, out string[] modelNames)
    {
        modelValues = system.CustomModels.Where(entry => entry.Value.Enabled).Select(entry => entry.Key).ToArray();
        modelNames = modelValues.Select(key => new AssetLocation(key)).Select(GetCustomModelLangEntry).ToArray();

        if (modelValues.Length == 0)
        {
            modelValues = [system.DefaultModelCode];
            modelNames = [GetCustomModelLangEntry(system.DefaultModelCode)];
        }
    }
    private string GetCustomModelLangEntry(AssetLocation code) => Lang.Get($"{code.Domain}:playermodel-{code.Path}");
}
