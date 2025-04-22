using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static class OtherPatches
{
    public static void Patch(string harmonyId)
    {
        /*new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorExtraSkinnable).GetMethod("Initialize", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(EntityBehaviorExtraSkinnable_Initialize)))
            );*/
    }

    public static void Unpatch(string harmonyId)
    {
        //new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorExtraSkinnable).GetMethod("Initialize", AccessTools.all), HarmonyPatchType.Postfix, harmonyId);
    }

    [HarmonyPatch(typeof(EntityBehaviorTexturedClothing), "reloadSkin")]
    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    public class GuiDialogPatchCommand
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeInstruction[] newInstructions = new CodeInstruction[]
            {
                new(OpCodes.Pop),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorTexturedClothing), "entity")),
                new(OpCodes.Call, AccessTools.Method(typeof(GuiDialogPatchCommand), nameof(GetSkinBaseTextureKey))),
            };

            List<CodeInstruction> codes = new(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Dup)
                {
                    codes.InsertRange(i + 2, newInstructions);

                    return codes;
                }
            }

            return codes;
        }

        private static string GetSkinBaseTextureKey(Entity entity)
        {
            return entity.GetBehavior<ExtraSkinnableBehavior>().MainTextureCode;
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
            CodeInstruction[] newInstructions = new CodeInstruction[]
            {
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorContainer), "entity")),
                new(OpCodes.Ldloca_S, 3),
                new(OpCodes.Ldarg_3),
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, AccessTools.Method(typeof(EntityBehaviorContainerPatchCommand), nameof(GetModelReplacement))),
            };
            List<CodeInstruction> codes = new(instructions);
            MethodInfo targetMethod = AccessTools.Method(typeof(IWearableShapeSupplier), "GetShape");

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

        public static void GetModelReplacement(ItemStack? stack, Entity entity, ref Shape defaultShape, IAttachableToEntity yatayata, float damageEffect)
        {
            string currentModel = entity.WatchedAttributes.GetString("skinModel");
            int itemId = stack?.Item?.Id ?? 0;

            CustomModelsSystem system = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();

            if (currentModel != null && itemId != 0 && system != null)
            {
                if (system.WearableShapeReplacers.TryGetValue(currentModel, out Dictionary<int, string>? replacements))
                {
                    if (replacements.TryGetValue(itemId, out string? shape))
                    {
                        defaultShape = LoadShape(entity.Api, shape);

                        defaultShape?.SubclassForStepParenting(yatayata.GetTexturePrefixCode(stack), damageEffect);
                        defaultShape?.ResolveReferences(entity.World.Logger, currentModel);
                    }
                }
            }
        }

        private static Shape? LoadShape(ICoreAPI api, string path)
        {
            AssetLocation shapeLocation = new(path);
            shapeLocation = shapeLocation.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape? currentShape = Shape.TryGet(api, shapeLocation);
            return currentShape;
        }
    }
}