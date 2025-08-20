using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PlayerModelLib;

internal static class OtherPatches
{
    public static float CurrentModelGuiScale { get; set; } = 1;
    public static float CurrentModelScale { get; set; } = 1;

    public static void Patch(string harmonyId)
    {
        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(ReloadSkin)))
            );
        new Harmony(harmonyId).Patch(
                typeof(CharacterSystem).GetMethod("Event_PlayerJoin", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(Event_PlayerJoin)))
            );
        new Harmony(harmonyId).Patch(
                typeof(EntityShapeRenderer).GetMethod("loadModelMatrixForGui", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(ApplyModelMatrixForGui)))
            );
    }

    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(CharacterSystem).GetMethod("Event_PlayerJoin", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityShapeRenderer).GetMethod("loadModelMatrixForGui", AccessTools.all), HarmonyPatchType.Postfix, harmonyId);
    }

    private static bool ReloadSkin() => false;

    private static readonly FieldInfo? CharacterSystem_didSelect = typeof(CharacterSystem).GetField("didSelect", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? CharacterSystem_createCharDlg = typeof(CharacterSystem).GetField("createCharDlg", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? CharacterSystem_capi = typeof(CharacterSystem).GetField("capi", BindingFlags.NonPublic | BindingFlags.Instance);

    private static bool Event_PlayerJoin(CharacterSystem __instance, IClientPlayer byPlayer)
    {
        bool didSelect = (bool?)CharacterSystem_didSelect?.GetValue(__instance) ?? false;

        if (didSelect) return true;

        ICoreClientAPI? api = (ICoreClientAPI?)CharacterSystem_capi?.GetValue(__instance);

        if (api == null) return true;

        if (byPlayer.PlayerUID != api.World.Player.PlayerUID) return true;

        byPlayer.Entity.GetBehavior<PlayerSkinBehavior>().OnActuallyInitialize += () =>
        {
            GuiDialogCreateCharacter createCharDlg = new GuiDialogCreateCustomCharacter(api, __instance);
            createCharDlg.PrepAndOpen();
            createCharDlg.OnClosed += () => api.PauseGame(false);
            api.Event.EnqueueMainThreadTask(() => api.PauseGame(true), "pausegame");
            api.Event.PushEvent("begincharacterselection");
            CharacterSystem_createCharDlg.SetValue(__instance, createCharDlg);
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

    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    public class EntityBehaviorContainerPatchCommand
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EntityBehaviorContainer), "addGearToShape", new[]
            {
            typeof(Shape),
            typeof(ItemStack),
            typeof(IAttachableToEntity),
            typeof(string),
            typeof(string),
            typeof(string[]).MakeByRefType(),
            typeof(Dictionary<string, StepParentElementTo>)
        });
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeInstruction[] newInstructions =
            [
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorContainer), "entity")),
                new(OpCodes.Ldloca_S, 3),
                new(OpCodes.Ldloca_S, 5),
                new(OpCodes.Ldarg_3),
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, AccessTools.Method(typeof(EntityBehaviorContainerPatchCommand), nameof(GetModelReplacement))),
            ];
            List<CodeInstruction> codes = [.. instructions];

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Isinst && (Type)codes[i].operand == typeof(Vintagestory.API.Client.ICoreClientAPI))
                {
                    codes.InsertRange(i + 2, newInstructions);

                    return codes;
                }
            }

            return codes;
        }

        public static void GetModelReplacement(ItemStack? stack, Entity entity, ref Shape? defaultShape, ref CompositeShape? compositeShape, IAttachableToEntity yatayata, float damageEffect)
        {
            int itemId = stack?.Item?.Id ?? 0;

            CustomModelsSystem system = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();
            PlayerSkinBehavior? skinBehavior = entity.GetBehavior<PlayerSkinBehavior>();

            string? currentModel = skinBehavior?.CurrentModelCode;

            if (currentModel == null || itemId == 0 || system == null || !system.ModelsLoaded) return;
            
            CustomModelData customModel = system.CustomModels[currentModel];

            if (customModel.WearableShapeReplacers.TryGetValue(itemId, out string? shape))
            {
                defaultShape = LoadShape(entity.Api, shape);

                defaultShape?.SubclassForStepParenting(yatayata.GetTexturePrefixCode(stack), damageEffect);
                defaultShape?.ResolveReferences(entity.World.Logger, currentModel);

                if (compositeShape != null)
                {
                    compositeShape = compositeShape.Clone();
                    compositeShape.Base = shape;
                }

                return;
            }

            if (customModel.WearableCompositeShapeReplacers.TryGetValue(itemId, out CompositeShape? newCompositeShape))
            {
                compositeShape = newCompositeShape.Clone();

                compositeShape.Base = compositeShape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");

                defaultShape = LoadShape(entity.Api, newCompositeShape.Base);

                defaultShape?.SubclassForStepParenting(yatayata.GetTexturePrefixCode(stack), damageEffect);
                defaultShape?.ResolveReferences(entity.World.Logger, currentModel);
            }

            CompositeShape oldCompositeShape = yatayata.GetAttachedShape(stack, "default").Clone();

            string shapePath = oldCompositeShape.Base.ToString();

            if (customModel.WearableShapeReplacersByShape.TryGetValue(shapePath, out shape))
            {
                defaultShape = LoadShape(entity.Api, shape);

                defaultShape?.SubclassForStepParenting(yatayata.GetTexturePrefixCode(stack), damageEffect);
                defaultShape?.ResolveReferences(entity.World.Logger, currentModel);
            }

            if (oldCompositeShape.Overlays != null)
            {
                foreach (CompositeShape? overlay in oldCompositeShape.Overlays)
                {
                    if (overlay == null) continue;

                    ReplaceOverlay(overlay, customModel.WearableShapeReplacersByShape);
                }

                compositeShape = oldCompositeShape;
            }
        }

        private static Shape? LoadShape(ICoreAPI api, string path)
        {
            AssetLocation shapeLocation = new(path);
            shapeLocation = shapeLocation.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape? currentShape = Shape.TryGet(api, shapeLocation);
            return currentShape;
        }

        private static void ReplaceOverlay(CompositeShape shape, Dictionary<string, string> replacements)
        {
            if (replacements.TryGetValue(shape.Base.ToString(), out string? newShape))
            {
                shape.Base = newShape;
            }

            if (shape.Overlays != null)
            {
                foreach (CompositeShape? overlay in shape.Overlays)
                {
                    if (overlay == null) continue;

                    ReplaceOverlay(overlay, replacements);
                }
            }
        }
    }
}