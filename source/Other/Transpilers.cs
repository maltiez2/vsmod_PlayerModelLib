using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PlayerModelLib;

internal static class TranspilerPatches
{
    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    public class EntityBehaviorContainerPatchCommand
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EntityBehaviorContainer), "addGearToShape",
            [
                typeof(Shape),
                typeof(ItemStack),
                typeof(IAttachableToEntity),
                typeof(string),
                typeof(string),
                typeof(string[]).MakeByRefType(),
                typeof(Dictionary<string, StepParentElementTo>)
            ]);
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
                new(OpCodes.Ldarg_S, 4),
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

        public static void GetModelReplacement(ItemStack? stack, Entity entity, ref Shape? defaultShape, ref CompositeShape? compositeShape, IAttachableToEntity yadayada, float damageEffect, string slotCode)
        {
            int itemId = stack?.Item?.Id ?? 0;

            CustomModelsSystem system = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();
            PlayerSkinBehavior? skinBehavior = entity.GetBehavior<PlayerSkinBehavior>();

            string? currentModel = skinBehavior?.CurrentModelCode;

            if (currentModel == null || itemId == 0 || system == null || !system.ModelsLoaded) return;

            CustomModelData customModel = system.CustomModels[currentModel];

            string? yadaPrefixCode = yadayada.GetTexturePrefixCode(stack);
            //string prefixCode = yadaPrefixCode == null ? slotCode : yadaPrefixCode + "-" + slotCode;
            string prefixCode = yadaPrefixCode == null ? "" : yadaPrefixCode;

            if (customModel.WearableShapeReplacers.TryGetValue(itemId, out string? shape))
            {
                defaultShape = LoadShape(entity.Api, shape);

                defaultShape?.SubclassForStepParenting(prefixCode, damageEffect);
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

                defaultShape?.SubclassForStepParenting(prefixCode, damageEffect);
                defaultShape?.ResolveReferences(entity.World.Logger, currentModel);
            }

            CompositeShape oldCompositeShape = yadayada.GetAttachedShape(stack, "default").Clone();

            string shapePath = oldCompositeShape.Base.ToString();

            if (customModel.WearableShapeReplacersByShape.TryGetValue(shapePath, out shape))
            {
                defaultShape = LoadShape(entity.Api, shape);

                defaultShape?.SubclassForStepParenting(prefixCode, damageEffect);
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