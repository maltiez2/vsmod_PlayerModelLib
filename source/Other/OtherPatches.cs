﻿using HarmonyLib;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
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
                typeof(EntityShapeRenderer).GetMethod("loadModelMatrixForGui", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(ApplyModelMatrixForGui)))
            );
        new Harmony(harmonyId).Patch(
                typeof(ShapeElement).GetMethod("TrimTextureNamesAndResolveFaces", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(ShapeElement_TrimTextureNamesAndResolveFaces)))
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

        _clientApi = null;
    }

    private static bool ReloadSkin() => false;

    private static ICoreClientAPI? _clientApi;

    private static readonly FieldInfo? _characterSystem_didSelect = typeof(CharacterSystem).GetField("didSelect", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _characterSystem_createCharDlg = typeof(CharacterSystem).GetField("createCharDlg", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _characterSystem_capi = typeof(CharacterSystem).GetField("capi", BindingFlags.NonPublic | BindingFlags.Instance);

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

    private static bool getClassTraitText(CharacterSystem __instance, ref string __result)
    {
        string? classCode = _clientApi?.World?.Player?.Entity?.WatchedAttributes?.GetString("characterClass");
        CharacterClass? characterClass = __instance.characterClasses.Find(c => c.Code == classCode);

        if (characterClass == null)
        {
            LoggerUtil.Error(_clientApi, typeof(OtherPatches), $"Character class with code '{classCode}' not found when trying to set character class for player '{_clientApi?.World?.Player?.PlayerName}'.");
            __result = "";
            return false;
        }

        StringBuilder fullDescription = new();
        StringBuilder attributes = new();

        string[] extraTraits = _clientApi?.World?.Player?.Entity.WatchedAttributes.GetStringArray("extraTraits") ?? [];
        IEnumerable<string> allTraits = characterClass.Traits.Concat(extraTraits);
        IOrderedEnumerable<Trait> characterTraits = allTraits.Select(code => __instance.TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

        foreach (Trait? trait in characterTraits)
        {
            attributes.Clear();
            foreach ((string attribute, double attributeValue) in trait.Attributes)
            {
                if (attributes.Length > 0) attributes.Append(", ");
                attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", attribute, attributeValue)));
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

#pragma warning disable CS0618 // Type or member is obsolete - It is not in this case
#pragma warning disable S3241 // Methods should not return values that are never used - harmony prefix need to return bool in this case
    private static bool ShapeElement_TrimTextureNamesAndResolveFaces(ShapeElement __instance)

    {
        if (!TranspilerPatches.ExportingShape) return true;


        if (__instance.Faces != null)
        {
            foreach (KeyValuePair<string, ShapeElementFace> val in __instance.Faces)
            {
                ShapeElementFace f = val.Value;
                if (!f.Enabled) continue;
                BlockFacing facing = BlockFacing.FromFirstLetter(val.Key);
                __instance.FacesResolved[facing.Index] = f;
                f.Texture = f.Texture.Substring(1).DeDuplicate();
            }

        }

        if (__instance.Children != null)
        {
            foreach (ShapeElement child in __instance.Children) ShapeElement_TrimTextureNamesAndResolveFaces(child);
        }

        __instance.Name = __instance.Name.DeDuplicate();
        __instance.StepParentName = __instance.StepParentName.DeDuplicate();
        AttachmentPoint[] AttachmentPoints = __instance.AttachmentPoints;
        if (AttachmentPoints != null)
        {
            for (int i = 0; i < AttachmentPoints.Length; i++) AttachmentPoints[i].DeDuplicate();
        }

        return false;
    }
#pragma warning restore S3241 // Methods should not return values that are never used
#pragma warning restore CS0618 // Type or member is obsolete
}