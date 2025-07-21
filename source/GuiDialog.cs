using CommandLine;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class GuiDialogCreateCustomCharacter : GuiDialogCreateCharacter
{
    public override float ZSize
    {
        get { return (float)GuiElement.scaled(280); }
    }
    public override bool PrefersUngrabbedMouse => true;
    public override string? ToggleKeyCombinationCode => null;

    public GuiDialogCreateCustomCharacter(ICoreClientAPI capi, CharacterSystem characterSystem) : base(capi, characterSystem)
    {
        _characterSystem = characterSystem;
        _customModelsSystem = capi.ModLoader.GetModSystem<CustomModelsSystem>();
    }
    public override void OnGuiOpened()
    {
        Debug.WriteLine("GuiDialogCreateCustomCharacter opened");

        string characterClass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
        if (characterClass != null)
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
        catch
        {

        }


        EntityShapeRenderer? renderer = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        renderer?.TesselateShape();

        if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Guest || capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Survival)
        {
            if (_characterInventory != null) _characterInventory.Open(capi.World.Player);
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

        CharacterClass characterClass = _characterSystem.characterClasses[_currentClassIndex];

        if (_clientSelectionDone != null)
        {
            _clientSelectionDone.Invoke(_characterSystem, new object[] { _characterInventory, characterClass.Code, _didSelect }); // thanks Tyron for making methods internal!
        }

        capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>().hideClothing = false;
        reTesselate();
    }
    public override bool CaptureAllInputs()
    {
        return IsOpened();
    }
    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
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

        if (_insetSlotBounds.PointInside(capi.Input.MouseX, capi.Input.MouseY) && _curTab == 0)
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

        _rotateCharacter = _insetSlotBounds.PointInside(args.X, args.Y);
    }
    public override void OnMouseUp(MouseEvent args)
    {
        base.OnMouseUp(args);

        _rotateCharacter = false;
    }
    public override void OnMouseMove(MouseEvent args)
    {
        base.OnMouseMove(args);

        if (_rotateCharacter) _yaw -= args.DeltaX / 100f;
    }
    public override void OnRenderGUI(float deltaTime)
    {
        foreach (KeyValuePair<string, GuiComposer> item in (IEnumerable<KeyValuePair<string, GuiComposer>>)Composers)
        {
            item.Value.Render(deltaTime);
            MouseOverCursor = item.Value.MouseOverCursor;
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

        if (_curTab == 0)
        {
            capi.Render.RenderEntityToGui(
                deltaTime,
                capi.World.Player.Entity,
                _insetSlotBounds.renderX + pad - GuiElement.scaled(195) * _charZoom + GuiElement.scaled(115 * (1 - _charZoom)),
                _insetSlotBounds.renderY + pad + GuiElement.scaled(10 * (1 - _charZoom)),
                (float)GuiElement.scaled(230),
                _yaw,
                (float)GuiElement.scaled(330 * _charZoom),
                ColorUtil.WhiteArgb);
        }
        else
        {
            capi.Render.RenderEntityToGui(
                deltaTime,
                capi.World.Player.Entity,
                _insetSlotBounds.renderX + pad - GuiElement.scaled(110),
                _insetSlotBounds.renderY + pad - GuiElement.scaled(15),
                (float)GuiElement.scaled(230),
                _yaw,
                (float)GuiElement.scaled(205),
                ColorUtil.WhiteArgb);
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
    private int _curTab = 0;
    private readonly int _rows = 7;
    private float _charZoom = 1f;
    private bool _charNaked = true;
    private readonly int _dlgHeight = 433 + 80;
    private float _yaw = -GameMath.PIHALF + 0.3f;
    private bool _rotateCharacter;
    private readonly Vec4f _lighPos = new Vec4f(-1, -1, 0, 0).NormalizeXYZ();
    private readonly Matrixf _mat = new();
    private readonly MethodInfo? _clientSelectionDone = typeof(CharacterSystem).GetMethod("ClientSelectionDone", BindingFlags.NonPublic | BindingFlags.Instance);

    private void ComposeGuis()
    {
        double pad = GuiElementItemSlotGridBase.unscaledSlotPadding;
        double slotsize = GuiElementPassiveItemSlot.unscaledSlotSize;

        _characterInventory = capi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        ElementBounds tabBounds = ElementBounds.Fixed(0, -25, 450, 25);

        double ypos = 20 + pad;

        ElementBounds bgBounds = ElementBounds.FixedSize(717, _dlgHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);

        ElementBounds dialogBounds = ElementBounds.FixedSize(757, _dlgHeight + 40).WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);


        GuiTab[] tabs = new GuiTab[] {
            new() { Name = Lang.Get("tab-skinandvoice"), DataInt = 0 },
            new() { Name = Lang.Get("tab-charclass"), DataInt = 1 },
          //  new GuiTab() { Name = "Outfit", DataInt = 2 }
        };

        GuiComposer createCharacterComposer;
        Composers["createcharacter"] = createCharacterComposer =
            capi.Gui
            .CreateCompo("createcharacter", dialogBounds)
            .AddShadedDialogBG(bgBounds, true)
            .AddDialogTitleBar(_curTab == 0 ? Lang.Get("Customize Skin") : (_curTab == 1 ? Lang.Get("Select character class") : Lang.Get("Select your outfit")), OnTitleBarClose)
            .AddHorizontalTabs(tabs, tabBounds, onTabClicked, CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), "tabs")
            .BeginChildElements(bgBounds)
        ;

        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        //capi.World.Player.Entity.hideClothing = false;
        EntityBehaviorPlayerInventory bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        bh.hideClothing = false;

        if (_curTab == 0)
        {
            PlayerSkinBehavior skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();

            string[] modelValues = system.CustomModels.Keys.ToArray();
            string[] modelNames = system.CustomModels.Keys.Select(key => new AssetLocation(key)).Select(key => Lang.Get($"{key.Domain}:playermodel-{key.Path}")).ToArray();
            int modelIndex = 0;

            for (int index = 0; index < modelValues.Length; index++)
            {
                if (modelValues[index] == skinMod.CurrentModelCode)
                {
                    modelIndex = index;
                    break;
                }
            }

            createCharacterComposer.AddDropDown(modelValues, modelNames, modelIndex, (variantcode, selected) => onToggleModel(variantcode), ElementBounds.Fixed(490, -16).WithFixedSize(180, 25), "dropdown-modelselection");

            createCharacterComposer.AddRichtext("Model:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(440, -13).WithFixedSize(210, 22));

            //capi.World.Player.Entity.hideClothing = charNaked;
            bh.hideClothing = _charNaked;

            EntityShapeRenderer? essr = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
            essr.TesselateShape();

            CairoFont smallfont = CairoFont.WhiteSmallText();
            Cairo.TextExtents textExt = smallfont.GetTextExtents(Lang.Get("Show dressed"));
            int colorIconSize = 22;

            ElementBounds leftColBounds = ElementBounds.Fixed(0, ypos, 204, _dlgHeight - 59).FixedGrow(2 * pad, 2 * pad);

            _insetSlotBounds = ElementBounds.Fixed(0, ypos + 2, 265, leftColBounds.fixedHeight - 2 * pad - 10).FixedRightOf(leftColBounds, 10);
            ElementBounds rightColBounds = ElementBounds.Fixed(0, ypos, 54, _dlgHeight - 59).FixedGrow(2 * pad, 2 * pad).FixedRightOf(_insetSlotBounds, 10);
            ElementBounds toggleButtonBounds = ElementBounds.Fixed(
                    (int)_insetSlotBounds.fixedX + _insetSlotBounds.fixedWidth / 2 - textExt.Width / RuntimeEnv.GUIScale / 2 - 12,
                    0,
                    textExt.Width / RuntimeEnv.GUIScale + 1,
                    textExt.Height / RuntimeEnv.GUIScale
                )
                .FixedUnder(_insetSlotBounds, 4).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(12, 6)
            ;

            ElementBounds bounds = null;
            ElementBounds prevbounds = null;

            double leftX = 0;

            foreach (SkinnablePart? skinpart in skinMod.AvailableSkinParts)
            {
                bounds = ElementBounds.Fixed(leftX, (prevbounds == null || prevbounds.fixedY == 0) ? -10 : prevbounds.fixedY + 8, colorIconSize, colorIconSize);

                string code = skinpart.Code;

                AppliedSkinnablePartVariant appliedVar = skinMod.AppliedSkinParts.FirstOrDefault(sp => sp.PartCode == code);

                if (skinpart.Type == EnumSkinnableType.Texture && !skinpart.UseDropDown)
                {
                    int selectedIndex = 0;
                    int[] colors = new int[skinpart.Variants.Length];

                    for (int i = 0; i < skinpart.Variants.Length; i++)
                    {
                        colors[i] = skinpart.Variants[i].Color;

                        if (appliedVar?.Code == skinpart.Variants[i].Code) selectedIndex = i;
                    }

                    createCharacterComposer.AddRichtext(Lang.Get("skinpart-" + code), CairoFont.WhiteSmallText(), bounds = bounds.BelowCopy(0, 10).WithFixedSize(210, 22));
                    createCharacterComposer.AddColorListPicker(colors, (index) => onToggleSkinPartColor(code, index), bounds = bounds.BelowCopy(0, 0).WithFixedSize(colorIconSize, colorIconSize), 180, "picker-" + code);

                    for (int i = 0; i < colors.Length; i++)
                    {
                        GuiElementColorListPicker picker = createCharacterComposer.GetColorListPicker("picker-" + code + "-" + i);
                        picker.ShowToolTip = true;
                        picker.TooltipText = Lang.Get("color-" + skinpart.Variants[i].Code);

                        //Console.WriteLine("\"" + Lang.Get("color-" + skinpart.Variants[i].Code) + "\": \""+ skinpart.Variants[i].Code + "\"");
                    }

                    createCharacterComposer.ColorListPickerSetValue("picker-" + code, selectedIndex);
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

                        //Console.WriteLine("\"" + names[i] + "\": \"" + skinpart.Variants[i].Code + "\",");

                        if (appliedVar?.Code == values[i]) selectedIndex = i;
                    }


                    createCharacterComposer.AddRichtext(Lang.Get("skinpart-" + code), CairoFont.WhiteSmallText(), bounds = bounds.BelowCopy(0, 10).WithFixedSize(210, 22));

                    string tooltip = Lang.GetIfExists("skinpartdesc-" + code);
                    if (tooltip != null)
                    {
                        createCharacterComposer.AddHoverText(tooltip, CairoFont.WhiteSmallText(), 300, bounds = bounds.FlatCopy());
                    }

                    createCharacterComposer.AddDropDown(values, names, selectedIndex, (variantcode, selected) => onToggleSkinPartColor(code, variantcode), bounds = bounds.BelowCopy(0, 0).WithFixedSize(200, 25), "dropdown-" + code);
                }

                prevbounds = bounds.FlatCopy();

                if (skinpart.Colbreak)
                {
                    leftX = _insetSlotBounds.fixedX + _insetSlotBounds.fixedWidth + 22;
                    prevbounds.fixedY = 0;
                }
            }

            createCharacterComposer.AddInset(_insetSlotBounds, 2);
            createCharacterComposer.AddToggleButton(Lang.Get("Show dressed"), smallfont, OnToggleDressOnOff, toggleButtonBounds, "showdressedtoggle");
            createCharacterComposer.AddButton(Lang.Get("Randomize"), () => { return OnRandomizeSkin(new Dictionary<string, string>()); }, ElementBounds.Fixed(0, _dlgHeight - 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(8, 6), CairoFont.WhiteSmallText(), EnumButtonStyle.Small);
            createCharacterComposer.AddIf(capi.Settings.String.Exists("lastSkinSelection"))
                .AddButton(Lang.Get("Last selection"), () => { return OnRandomizeSkin(_characterSystem.getPreviousSelection()); }, ElementBounds.Fixed(130, _dlgHeight - 25).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(8, 6), CairoFont.WhiteSmallText(), EnumButtonStyle.Small)
                .EndIf();

            

            

            createCharacterComposer.AddSmallButton(Lang.Get("Confirm Skin"), OnNext, ElementBounds.Fixed(0, _dlgHeight - 25).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal);
            

            createCharacterComposer.GetToggleButton("showdressedtoggle").SetValue(!_charNaked);
        }

        if (_curTab == 1)
        {
            EntityShapeRenderer? essr = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
            essr.TesselateShape();

            ypos -= 10;

            ElementBounds leftColBounds = ElementBounds.Fixed(0, ypos, 0, _dlgHeight - 47).FixedGrow(2 * pad, 2 * pad);
            _insetSlotBounds = ElementBounds.Fixed(0, ypos + 25, 190, leftColBounds.fixedHeight - 2 * pad + 10).FixedRightOf(leftColBounds, 10);

            ElementBounds rightSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, ypos, 1, _rows).FixedGrow(2 * pad, 2 * pad).FixedRightOf(_insetSlotBounds, 10);
            ElementBounds prevButtonBounds = ElementBounds.Fixed(0, ypos + 25, 35, slotsize - 4).WithFixedPadding(2).FixedRightOf(_insetSlotBounds, 20);
            ElementBounds centerTextBounds = ElementBounds.Fixed(0, ypos + 25, 200, slotsize - 4 - 8).FixedRightOf(prevButtonBounds, 20);

            ElementBounds charclasssInset = centerTextBounds.ForkBoundingParent(4, 4, 4, 4);

            ElementBounds nextButtonBounds = ElementBounds.Fixed(0, ypos + 25, 35, slotsize - 4).WithFixedPadding(2).FixedRightOf(charclasssInset, 20);

            CairoFont font = CairoFont.WhiteMediumText();
            centerTextBounds.fixedY += (centerTextBounds.fixedHeight - font.GetFontExtents().Height / RuntimeEnv.GUIScale) / 2;

            ElementBounds charTextBounds = ElementBounds.Fixed(0, 0, 480, 100).FixedUnder(prevButtonBounds, 20).FixedRightOf(_insetSlotBounds, 20);

            createCharacterComposer
                .AddInset(_insetSlotBounds, 2)

                .AddIconButton("left", (on) => changeClass(-1), prevButtonBounds.FlatCopy())
                .AddInset(charclasssInset, 2)
                .AddDynamicText("Commoner", font.Clone().WithOrientation(EnumTextOrientation.Center), centerTextBounds, "className")
                .AddIconButton("right", (on) => changeClass(1), nextButtonBounds.FlatCopy())

                .AddRichtext("", CairoFont.WhiteDetailText(), charTextBounds, "characterDesc")
                .AddSmallButton(Lang.Get("Confirm Class"), OnConfirm, ElementBounds.Fixed(0, _dlgHeight - 30).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(12, 6), EnumButtonStyle.Normal)
            ;

            changeClass(0);
        }

        GuiElementHorizontalTabs tabElem = createCharacterComposer.GetHorizontalTabs("tabs");
        tabElem.unscaledTabSpacing = 20;
        tabElem.unscaledTabPadding = 10;
        tabElem.activeElement = _curTab;

        createCharacterComposer.Compose();
    }
    private bool OnRandomizeSkin(Dictionary<string, string> preselection)
    {
        EntityPlayer entity = capi.World.Player.Entity;

        //essr.doReloadShapeAndSkin = false;
        EntityBehaviorPlayerInventory bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        bh.doReloadShapeAndSkin = false;

        _characterSystem.randomizeSkin(entity, preselection);
        EntityBehaviorExtraSkinnable skinMod = entity.GetBehavior<EntityBehaviorExtraSkinnable>();

        foreach (AppliedSkinnablePartVariant? appliedPart in skinMod.AppliedSkinParts)
        {
            string partcode = appliedPart.PartCode;

            SkinnablePart? skinPart = skinMod.AvailableSkinParts.FirstOrDefault(part => part.Code == partcode);
            int index = skinPart.Variants.IndexOf(part => part.Code == appliedPart.Code);

            if (skinPart.Type == EnumSkinnableType.Texture && !skinPart.UseDropDown)
            {
                Composers["createcharacter"].ColorListPickerSetValue("picker-" + partcode, index);
            }
            else
            {
                Composers["createcharacter"].GetDropDown("dropdown-" + partcode).SetSelectedIndex(index);
            }
        }

        //essr.doReloadShapeAndSkin = true;
        bh.doReloadShapeAndSkin = true;
        reTesselate();

        return true;
    }
    private void OnToggleDressOnOff(bool on)
    {
        _charNaked = !on;
        EntityBehaviorPlayerInventory bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        bh.hideClothing = _charNaked;
        //capi.World.Player.Entity.hideClothing = charNaked;
        reTesselate();
    }
    private void onToggleSkinPartColor(string partCode, string variantCode)
    {
        EntityBehaviorExtraSkinnable skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
        skinMod.selectSkinPart(partCode, variantCode);
    }
    private void onToggleSkinPartColor(string partCode, int index)
    {
        EntityBehaviorExtraSkinnable skinMod = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

        string variantCode = skinMod.AvailableSkinPartsByCode[partCode].Variants[index].Code;

        skinMod.selectSkinPart(partCode, variantCode);
    }
    private void onToggleModel(string modelCode)
    {
        EntityBehaviorPlayerInventory bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorPlayerInventory>();
        bh.doReloadShapeAndSkin = true;

        PlayerSkinBehavior skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = capi.ModLoader.GetModSystem<CustomModelsSystem>();

        system.SynchronizePlayerModelAndSize(modelCode, skinMod.CurrentSize);
        skinMod.SetCurrentModel(modelCode, skinMod.CurrentSize);

        ComposeGuis();

        OnRandomizeSkin(new Dictionary<string, string>());
    }
    private bool OnNext()
    {
        _curTab = 1;
        ComposeGuis();
        return true;
    }
    private void onTabClicked(int tabid)
    {
        _curTab = tabid;
        ComposeGuis();
    }
    private bool OnConfirm()
    {
        _didSelect = true;
        TryClose();
        return true;
    }
    private void OnTitleBarClose()
    {
        TryClose();
    }
    private void SendInvPacket(object packet)
    {
        capi.Network.SendPacketClient(packet);
    }
    private void changeClass(int dir)
    {
        PlayerSkinBehavior skinMod = capi.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();

        List<CharacterClass> availableClasses = _characterSystem.characterClasses.Where(element => _customModelsSystem.CustomModels[skinMod.CurrentModelCode].AvailableClasses.Contains(element.Code)).ToList();
        if (availableClasses.Count == 0)
        {
            availableClasses = _characterSystem.characterClasses;
        }

        availableClasses = availableClasses.Where(element => !_customModelsSystem.CustomModels[skinMod.CurrentModelCode].SkipClasses.Contains(element.Code)).ToList();

        _currentClassIndex = GameMath.Mod(_currentClassIndex + dir, availableClasses.Count);

        CharacterClass chclass = availableClasses[_currentClassIndex];
        Composers["createcharacter"].GetDynamicText("className").SetNewText(Lang.Get("characterclass-" + chclass.Code));

        StringBuilder fulldesc = new();
        StringBuilder attributes = new();

        fulldesc.AppendLine(Lang.Get("characterdesc-" + chclass.Code));
        fulldesc.AppendLine();
        fulldesc.AppendLine(Lang.Get("traits-title"));

        
        IOrderedEnumerable<Trait> chartraitsExtra = _customModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits.Select(code => _characterSystem.TraitsByCode[code]).OrderBy(trait => (int)trait.Type);
        IOrderedEnumerable<Trait> chartraits = chclass.Traits.Where(code => !_customModelsSystem.CustomModels[skinMod.CurrentModelCode].ExtraTraits.Contains(code)).Select(code => _characterSystem.TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

        foreach (Trait? trait in chartraitsExtra)
        {
            attributes.Clear();
            foreach (KeyValuePair<string, double> val in trait.Attributes)
            {
                if (attributes.Length > 0) attributes.Append(", ");
                attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", val.Key, val.Value)));
            }

            if (attributes.Length > 0)
            {
                fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), attributes));
            }
            else
            {
                string desc = Lang.GetIfExists("traitdesc-" + trait.Code);
                if (desc != null)
                {
                    fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), desc));
                }
                else
                {
                    fulldesc.AppendLine(Lang.Get("trait-" + trait.Code));
                }
            }
        }

        if (chartraitsExtra.Count() != 0)
        {
            fulldesc.AppendLine("");
        }

        foreach (Trait? trait in chartraits)
        {
            attributes.Clear();
            foreach (KeyValuePair<string, double> val in trait.Attributes)
            {
                if (attributes.Length > 0) attributes.Append(", ");
                attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", val.Key, val.Value)));
            }

            if (attributes.Length > 0)
            {
                fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), attributes));
            }
            else
            {
                string desc = Lang.GetIfExists("traitdesc-" + trait.Code);
                if (desc != null)
                {
                    fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), desc));
                }
                else
                {
                    fulldesc.AppendLine(Lang.Get("trait-" + trait.Code));
                }


            }
        }

        if (chclass.Traits.Length == 0)
        {
            fulldesc.AppendLine(Lang.Get("No positive or negative traits"));
        }

        Composers["createcharacter"].GetRichtext("characterDesc").SetNewText(fulldesc.ToString(), CairoFont.WhiteDetailText());

        _characterSystem.setCharacterClass(capi.World.Player.Entity, chclass.Code, true);

        reTesselate();
    }
    private void reTesselate()
    {
        EntityShapeRenderer? essr = capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer;
        essr.TesselateShape();
    }
}
