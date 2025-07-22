using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace PlayerModelLib;

internal static class OtherPatches
{
    public static void Patch(string harmonyId)
    {
        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(reloadSkin)))
            );
    }

    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
    }

    public static bool reloadSkin() => false;


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
            int itemId = stack?.Item?.Id ?? 0;

            CustomModelsSystem system = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();
            PlayerSkinBehavior? skinBehavior = entity.GetBehavior<PlayerSkinBehavior>();

            string? currentModel = skinBehavior?.CurrentModelCode;

            if (currentModel != null && itemId != 0 && system != null && system.ModelsLoaded)
            {
                if (system.CustomModels[currentModel].WearableShapeReplacers.TryGetValue(itemId, out string? shape))
                {
                    defaultShape = LoadShape(entity.Api, shape);

                    defaultShape?.SubclassForStepParenting(yatayata.GetTexturePrefixCode(stack), damageEffect);
                    defaultShape?.ResolveReferences(entity.World.Logger, currentModel);

                    return;
                }

                string shapePath = yatayata.GetAttachedShape(stack, "default").Base.ToString();

                if (system.CustomModels[currentModel].WearableShapeReplacersByShape.TryGetValue(shapePath, out shape))
                {
                    defaultShape = LoadShape(entity.Api, shape);

                    defaultShape?.SubclassForStepParenting(yatayata.GetTexturePrefixCode(stack), damageEffect);
                    defaultShape?.ResolveReferences(entity.World.Logger, currentModel);

                    return;
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