using HarmonyLib;
using OverhaulLib.Utils;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace PlayerModelLib;

internal static class OtherPatches
{
    public static float CurrentModelGuiScale { get; set; } = 1;
    public static float CurrentModelScale { get; set; } = 1;

    public static void Patch(string harmonyId, ICoreAPI api)
    {
        _clientApi = api as ICoreClientAPI;

        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(ReloadSkin)))
            );
        new Harmony(harmonyId).Patch(
                typeof(CharacterSystem).GetMethod("Event_PlayerJoin", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(Event_PlayerJoin)))
            );
        new Harmony(harmonyId).Patch(
                typeof(CharacterSystem).GetMethod("applyTraitAttributes", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(applyTraitAttributes)))
            );
        new Harmony(harmonyId).Patch(
                typeof(CharacterSystem).GetMethod("getClassTraitText", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(getClassTraitText)))
            );
        new Harmony(harmonyId).Patch(
                typeof(CharacterSystem).GetMethod("composeTraitsTab", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(composeTraitsTab)))
            );
        new Harmony(harmonyId).Patch(
                typeof(EntityShapeRenderer).GetMethod("loadModelMatrixForGui", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(ApplyModelMatrixForGui)))
            );
        new Harmony(harmonyId).Patch(
                typeof(ShapeElement).GetMethod("TrimTextureNamesAndResolveFaces", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(ShapeElement_TrimTextureNamesAndResolveFaces)))
            );
        new Harmony(harmonyId).Patch(
                typeof(GuiDialogHairStyling).GetMethod("getCost", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(GuiDialogHairStyling_getCost)))
            );
        new Harmony(harmonyId).Patch(
                typeof(GuiDialogHairStyling).GetMethod("AllowedSkinPartSelection", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(GuiDialogHairStyling_AllowedSkinPartSelection)))
            );
        new Harmony(harmonyId).Patch(
                typeof(TextureAtlasManager).GetMethod("RegenMipMaps", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(TextureAtlasManager_RegenMipMaps)))
            );
        new Harmony(harmonyId).Patch(
                typeof(CharacterSystem).GetMethod("onCharacterSelection", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(onCharacterSelection)))
            );

        new Harmony(harmonyId).Patch(
                typeof(ModSystemGliding).GetMethod("get_HasGlider", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(ModSystemGliding_get_HasGlider)))
            );
    }

    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(CharacterSystem).GetMethod("Event_PlayerJoin", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(CharacterSystem).GetMethod("applyTraitAttributes", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(CharacterSystem).GetMethod("getClassTraitText", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityShapeRenderer).GetMethod("loadModelMatrixForGui", AccessTools.all), HarmonyPatchType.Postfix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(ShapeElement).GetMethod("TrimTextureNamesAndResolveFaces", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(GuiDialogHairStyling).GetMethod("getCost", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(GuiDialogHairStyling).GetMethod("AllowedSkinPartSelection", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(TextureAtlasManager).GetMethod("RegenMipMaps", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(CharacterSystem).GetMethod("onCharacterSelection", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(ModSystemGliding).GetMethod("get_HasGlider", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);

        _clientApi = null;
    }

    public static void SetApi(ICoreAPI api)
    {
        if (api is ICoreClientAPI)
        {
            _clientApi = api as ICoreClientAPI;
        }
    }

#pragma warning disable S3400 // Methods should not return constants // This is required by how harmony path works
    private static bool ReloadSkin() => false;
#pragma warning restore S3400 // Methods should not return constants

    private static ICoreClientAPI? _clientApi;

    private static readonly FieldInfo? _characterSystem_didSelect = typeof(CharacterSystem).GetField("didSelect", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _characterSystem_createCharDlg = typeof(CharacterSystem).GetField("createCharDlg", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _characterSystem_capi = typeof(CharacterSystem).GetField("capi", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _guiDialogHairStyling_currentSkin = typeof(GuiDialogHairStyling).GetField("currentSkin", BindingFlags.NonPublic | BindingFlags.Instance);

    private static bool ModSystemGliding_get_HasGlider(ModSystemGliding __instance, ref bool __result)
    {
        float canGlide = _clientApi?.World.Player.Entity.Stats.GetBlended("canGlide") ?? 1;
        if (canGlide > 1)
        {
            __result = true;
            return false;
        }
        return true;
    }

    private static bool TextureAtlasManager_RegenMipMaps(TextureAtlasManager __instance, int atlasNumber)
    {
        if (__instance.AtlasTextures.Count <= atlasNumber)
        {
            return false;
        }

        return true;
    }

    private static bool Event_PlayerJoin(CharacterSystem __instance, IClientPlayer byPlayer)
    {
        bool didSelect = (bool?)_characterSystem_didSelect?.GetValue(__instance) ?? false;

        if (didSelect) return true;

        ICoreClientAPI? api = (ICoreClientAPI?)_characterSystem_capi?.GetValue(__instance);

        if (api == null) return true;

        if (byPlayer.PlayerUID != api.World.Player.PlayerUID) return true;

        PlayerSkinBehavior? skinBehavior = byPlayer.Entity.GetBehavior<PlayerSkinBehavior>();

        if (skinBehavior == null) return true;

        skinBehavior.OnActuallyInitialize += () =>
        {
            GuiDialogCreateCharacter createCharDlg = new GuiDialogCreateCustomCharacter(api, __instance);
            createCharDlg.PrepAndOpen();
            createCharDlg.OnClosed += () => api.PauseGame(false);
            api.Event.EnqueueMainThreadTask(() => api.PauseGame(true), "pausegame");
            api.Event.PushEvent("begincharacterselection");
            _characterSystem_createCharDlg?.SetValue(__instance, createCharDlg);
        };

        return false;
    }

    private static void ApplyModelMatrixForGui(Entity entity, ref float[] ___ModelMat)
    {
        if (___ModelMat != null && ___ModelMat.Length > 0 && entity is EntityPlayer)
        {
            //Need to undo the last translation to ensure that the player scales from the correct origin.
            Mat4f.Translate(___ModelMat, ___ModelMat, 0.5f, 0f, 0.5f);

            bool resize = GuiDialogCreateCustomCharacter.RenderState != (int)EnumCreateCharacterTabs.Model + 1;

            float size = MathF.Sqrt((resize ? entity.Properties.Client.Size : CurrentModelScale) / CurrentModelGuiScale);

            Mat4f.Scale(___ModelMat, ___ModelMat, 1 / size, 1 / size, 1 / size);

            //Redo the last translation.
            Mat4f.Translate(___ModelMat, ___ModelMat, -0.5f, 0f, -0.5f);
        }
    }

    private static bool applyTraitAttributes(CharacterSystem __instance, EntityPlayer eplr)
    {
        string classCode = eplr.WatchedAttributes.GetString("characterClass");
        CharacterClass? characterClass = __instance.characterClasses?.Find(c => c.Code == classCode)
            ?? throw new ArgumentException($"Character class with code '{classCode}' not found when trying to apply class traits for player '{eplr.Player?.PlayerName ?? eplr.GetName()}'.");

        // Reset 
        foreach ((_, EntityFloatStats stats) in eplr.Stats)
        {
            foreach ((string stat, _) in stats.ValuesByKey)
            {
                if (stat == "trait")
                {
                    stats.Remove(stat);
                    break;
                }
            }
        }

        CustomModelsSystem modelSystem = eplr.Api.ModLoader.GetModSystem<CustomModelsSystem>();
        PlayerSkinBehavior? skinBehavior = eplr.GetBehavior<PlayerSkinBehavior>();
        string? modelCode = skinBehavior?.CurrentModelCode;
        string[] extraModelTraits = modelCode == null ? [] : modelSystem.CustomModels[modelCode].ExtraTraits;
        string[] extraTraits = eplr.WatchedAttributes.GetStringArray("extraTraits") ?? [];
        IEnumerable<string> allTraits = extraTraits == null ? characterClass.Traits : characterClass.Traits.Concat(extraModelTraits).Concat(extraTraits).Distinct();

        // Aggregate stats values
        Dictionary<string, double> statValues = [];
        foreach (string traitCode in allTraits)
        {
            if (!__instance.TraitsByCode.TryGetValue(traitCode, out Trait? trait)) continue;

            foreach ((string attributeCode, double attributeValue) in trait.Attributes)
            {
                if (statValues.ContainsKey(attributeCode))
                {
                    statValues[attributeCode] += attributeValue;
                }
                else
                {
                    statValues[attributeCode] = attributeValue;
                }
            }
        }

        // Apply aggregated values
        foreach ((string stat, double value) in statValues)
        {
            eplr.Stats.Set(stat, "trait", (float)value, true);
        }

        eplr.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();

        return false;
    }

    private static bool composeTraitsTab(CharacterSystem __instance, GuiComposer compo)
    {
        if (_clientApi == null) return false;
        
        _composer = compo;

        string text = "";
        getClassTraitText(__instance, ref text);

        ElementBounds charTextBounds;

        if (_clientApi.ModLoader.IsModEnabled("overhaullib"))
        {
            charTextBounds = ElementBounds.Fixed(-18, 14, 417, 370);
        }
        else
        {
            charTextBounds = ElementBounds.Fixed(-18, 14, 375, 316);
        }

        ElementBounds bgBounds = charTextBounds.ForkBoundingParent(6, 6, 6, 6);
        ElementBounds clipBounds = charTextBounds.FlatCopy().FixedGrow(6, 11).WithFixedOffset(0, -6);
        ElementBounds scrollbarBounds = charTextBounds.CopyOffsetedSibling(charTextBounds.fixedWidth + 7, -6, 0, 12).WithFixedWidth(20);

        compo
            .BeginChildElements(bgBounds)
                .BeginClip(clipBounds)
                    .AddRichtext(text, CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), charTextBounds, "traitsDesc")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
            .EndChildElements()
        ;

        compo.GetScrollbar("scrollbar").SetHeights(
            316,
            632
        );
        compo.GetScrollbar("scrollbar").SetScrollbarPosition(0);

        return false;
    }

    private static GuiComposer? _composer;

    private static void OnNewScrollbarValue(float value)
    {
        GuiElementRichtext? richtextElem = _composer?.GetRichtext("traitsDesc");

        if (richtextElem != null)
        {
            richtextElem.Bounds.fixedY = 0 - value;
            richtextElem.Bounds.CalcWorldBounds();
        }
    }

#pragma warning disable CS0618 // Type or member is obsolete - It is not in this case
#pragma warning disable S3241 // Methods should not return values that are never used - harmony prefix need to return bool in this case
    private static bool getClassTraitText(CharacterSystem __instance, ref string __result)
    {
        string? classCode = _clientApi?.World?.Player?.Entity?.WatchedAttributes?.GetString("characterClass");
        CharacterClass? characterClass = __instance.characterClasses.Find(c => c.Code == classCode);

        if (characterClass == null)
        {
            Log.Error(_clientApi, typeof(OtherPatches), $"Character class with code '{classCode}' not found when trying to set character class for player '{_clientApi?.World?.Player?.PlayerName}'.");
            __result = "";
            return false;
        }

        StringBuilder fullDescription = new();
        StringBuilder attributes = new();

        string[] extraTraits = _clientApi?.World?.Player?.Entity.WatchedAttributes.GetStringArray("extraTraits") ?? [];
        IEnumerable<string> allTraits = characterClass.Traits.Concat(extraTraits);
        IOrderedEnumerable<Trait> characterTraits = allTraits
            .Where(__instance.TraitsByCode.ContainsKey)
            .Select(code => __instance.TraitsByCode[code])
            .OrderBy(trait => (int)trait.Type);

        foreach (Trait? trait in characterTraits)
        {
            attributes.Clear();
            foreach ((string attribute, double attributeValue) in trait.Attributes)
            {
                if (attributes.Length > 0) attributes.Append(", ");
                attributes.Append(GuiDialogCreateCustomCharacter.GetAttributeDescription(attribute, attributeValue));
            }

            if (attributes.Length > 0)
            {
                fullDescription.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), attributes));
            }
            else
            {
                string? traitDescription = Lang.GetIfExists("traitdesc-" + trait.Code);
                if (traitDescription != null)
                {
                    fullDescription.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), traitDescription));
                }
                else
                {
                    fullDescription.AppendLine(Lang.Get("trait-" + trait.Code));
                }
            }
        }

        if (!allTraits.Any())
        {
            fullDescription.AppendLine(Lang.Get("No positive or negative traits"));
        }

        __result = fullDescription.ToString();

        return false;
    }
    private static bool ShapeElement_TrimTextureNamesAndResolveFaces(ShapeElement __instance)

    {
        if (!ShapeReplacementUtil.ExportingShape) return true;


        if (__instance.Faces != null)
        {
            foreach (KeyValuePair<string, ShapeElementFace> val in __instance.Faces)
            {
                ShapeElementFace f = val.Value;
                if (!f.Enabled) continue;
                BlockFacing facing = BlockFacing.FromFirstLetter(val.Key);
                if (__instance.FacesResolved == null)
                {
                    continue;
                }
                __instance.FacesResolved[facing.Index] = f;
                f.Texture = f.Texture[1..].DeDuplicate();
            }

        }

        if (__instance.Children != null)
        {
            foreach (ShapeElement child in __instance.Children) ShapeElement_TrimTextureNamesAndResolveFaces(child);
        }

        __instance.Name = __instance.Name.DeDuplicate();
        __instance.StepParentName = __instance.StepParentName.DeDuplicate();
        AttachmentPoint[]? AttachmentPoints = __instance.AttachmentPoints;
        if (AttachmentPoints != null)
        {
            for (int i = 0; i < AttachmentPoints.Length; i++) AttachmentPoints[i].DeDuplicate();
        }

        return false;
    }
#pragma warning restore S3241 // Methods should not return values that are never used
#pragma warning restore CS0618 // Type or member is obsolete

    private static bool GuiDialogHairStyling_getCost(GuiDialogHairStyling __instance, ref int __result)
    {
        int cost = 0;
        PlayerSkinBehavior? skinMod = _clientApi?.World.Player.Entity.GetBehavior<PlayerSkinBehavior>();
        var availableSkinParts = skinMod?.AvailableSkinParts.Get();
        if (skinMod == null || availableSkinParts == null)
        {
            return false;
        }
        
        Dictionary<string, string> currentSkin = (Dictionary<string, string>?)_guiDialogHairStyling_currentSkin?.GetValue(__instance) ?? [];

        foreach (string code in availableSkinParts.Select(skinpart => skinpart.Code))
        {
            AppliedSkinnablePartVariant? appliedVar = skinMod.AppliedSkinParts.Get().FirstOrDefault(sp => sp.PartCode == code);

            if (appliedVar == null)
            {
                continue;
            }

            if (!currentSkin.ContainsKey(code))
            {
                continue;
            }

            if (!__instance.hairStylingCost.ContainsKey(code))
            {
                continue;
            }

            if (currentSkin[code] != appliedVar.Code)
            {
                cost += __instance.hairStylingCost[code];
            }
        }
        
        __result = cost;

        return false;
    }

    private static bool GuiDialogHairStyling_AllowedSkinPartSelection(ref bool __result)
    {
        __result = false;
        return false;
    }

    private static bool onCharacterSelection(CharacterSystem __instance, IServerPlayer fromPlayer, CharacterSelectionPacket p)
    {
        bool didSelectBefore = fromPlayer.GetModData<bool>("createCharacter", false);
        bool allowSelect = !didSelectBefore || fromPlayer.Entity.WatchedAttributes.GetBool("allowcharselonce") || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

        if (!allowSelect)
        {
            fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
            fromPlayer.BroadcastPlayerData(true);
            return false;
        }

        if (p.DidSelect)
        {
            fromPlayer.SetModData<bool>("createCharacter", true);

            __instance.setCharacterClass(fromPlayer.Entity, p.CharacterClass, !didSelectBefore || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative);

            var bh = fromPlayer.Entity.GetBehavior<PlayerSkinBehavior>();
            bh?.ApplyVoice(p.VoiceType, p.VoicePitch, false);

            foreach (var skinpart in p.SkinParts)
            {
                bh?.SelectSkinPart(skinpart.Key, skinpart.Value, false);
            }

            var date = DateTime.UtcNow;
            fromPlayer.ServerData.LastCharacterSelectionDate = date.ToShortDateString() + " " + date.ToShortTimeString();

            // allow players that just joined to immediately re select the class
            var allowOneFreeClassChange = fromPlayer.Entity.Api.World.Config.GetBool("allowOneFreeClassChange");
            if (!didSelectBefore && allowOneFreeClassChange)
            {
                fromPlayer.ServerData.LastCharacterSelectionDate = null;
            }
            else
            {
                fromPlayer.Entity.WatchedAttributes.RemoveAttribute("allowcharselonce");
            }
        }
        fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
        fromPlayer.BroadcastPlayerData(true);
        return false;
    }
}