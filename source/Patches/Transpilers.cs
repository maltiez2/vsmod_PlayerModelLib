using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PlayerModelLib;

internal static class TranspilerPatches
{
    [HarmonyPatchCategory("PlayerModelLibTranspiler")] // addGearToShape
    public static class PatchEntityBehaviorContainerModelReplacement
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EntityBehaviorContainer), "addGearToShape",
            [
                typeof(ICoreAPI),
                typeof(Entity),
                typeof(ITextureAtlasAPI),
                typeof(Shape),
                typeof(ItemStack),
                typeof(IAttachableToEntity),
                typeof(string),
                typeof(string),
                typeof(string[]).MakeByRefType(),
                typeof(IDictionary<string, CompositeTexture>),
                typeof(Dictionary<string, StepParentElementTo>)
            ]);
        }

        static MethodInfo _applyStepParentOverridesMI = AccessTools.Method(typeof(EntityBehaviorContainer), "applyStepParentOverrides");

        static MethodInfo _getModelReplacementMI = AccessTools.Method(typeof(PatchEntityBehaviorContainerModelReplacement), nameof(GetModelReplacement));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(_applyStepParentOverridesMI))
                {
                    // Insert BEFORE first applyStepParentOverrides call
                    codes.InsertRange(i,
                    [
                        // stack
                        new CodeInstruction(OpCodes.Ldarg_S, 4),   // ItemStack stack
                        new CodeInstruction(OpCodes.Ldarg_1),      // Entity optionalTargetEntity

                        new CodeInstruction(OpCodes.Ldloca_S, 3),  // ref Shape shape
                        new CodeInstruction(OpCodes.Ldloca_S, 5),  // ref CompositeShape compositeShape

                        new CodeInstruction(OpCodes.Ldarg_S, 5),   // IAttachableToEntity iatta
                        new CodeInstruction(OpCodes.Ldloc_1),      // float damageEffect
                        new CodeInstruction(OpCodes.Ldarg_S, 6),   // string slotCode
                        new CodeInstruction(OpCodes.Ldarg_S, 8),   // ref string[] willDeleteElements

                        new CodeInstruction(OpCodes.Call, _getModelReplacementMI)
                    ]);

                    break; // only first occurrence
                }
            }

            return Transpiler2(codes);
        }

        private static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = [.. instructions];

            for (int i = 2; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldloc_0 && codes[i - 1].opcode == OpCodes.Blt && codes[i - 2].opcode == OpCodes.Conv_I4)
                {
                    List<CodeInstruction> copyCodes = codes[i..(i + 3)];
                    copyCodes[2] = copyCodes[2].Clone();
                    copyCodes[2].opcode = OpCodes.Brtrue_S;
                    codes.InsertRange(i + 3, copyCodes);
                    codes.InsertRange(i - 2,
                    [
                        new CodeInstruction(OpCodes.Ldloc_S, 7),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, _entityField),
                        new CodeInstruction(OpCodes.Call, _insertMethod)
                    ]);

                    return codes;
                }
            }

            return codes;
        }

        private static void GetModelReplacement(ItemStack? stack, Entity entity, ref Shape? defaultShape, ref CompositeShape? compositeShape, IAttachableToEntity yadayada, float damageEffect, string slotCode, ref string[] willDeleteElements)
        {
            if (entity?.Api?.Side != EnumAppSide.Client)
            {
                return;
            }
            ShapeReplacementUtil.GetModelReplacement(stack, entity, ref defaultShape, ref compositeShape, yadayada, damageEffect, slotCode, ref willDeleteElements);
        }

        private static void InsertTexturesIntoAtlas(Dictionary<string, CompositeTexture> textures, Entity entity)
        {
            if (entity?.Api is not ICoreClientAPI clientApi || textures is null)
            {
                return;
            }

            foreach ((string code, CompositeTexture compositeTexture) in textures)
            {
                textures[code] = compositeTexture.Clone();
                ThreadSafeUtils.InsertTextureIntoAtlas(textures[code], clientApi, entity);
            }
        }

        private static readonly MethodInfo _insertMethod = AccessTools.Method(
            typeof(PatchEntityBehaviorContainerModelReplacement),
            nameof(InsertTexturesIntoAtlas)
        );

        private static readonly FieldInfo _entityField = AccessTools.Field(typeof(EntityBehavior), "entity");
    }


    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    [HarmonyPatch(typeof(EntityPlayer), "updateEyeHeight")]
    public static class PatchEntityPlayerUpdateEyeHeight
    {
        public static float GetSneakEyeMultiplier(EntityPlayer player)
        {
            PlayerSkinBehavior? skinBehavior = player.GetBehavior<PlayerSkinBehavior>();

            if (skinBehavior == null) return 0.8f;

            return skinBehavior.CurrentModel.SneakEyeHeightMultiplier;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new(instructions);
            MethodInfo getMultMethod = AccessTools.Method(typeof(PatchEntityPlayerUpdateEyeHeight), nameof(GetSneakEyeMultiplier));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R8 && (double)codes[i].operand == 0.800000011920929)
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldarg_0);
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, getMultMethod));
                    i++;
                }
            }

            return codes;
        }
    }

    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    [HarmonyPatch(typeof(EntityPlayer), "updateEyeHeight")]
    public static class PatchEntityPlayerUpdateEyeHeightInsertBeforeFloorSitting
    {
        public static void ApplyEyeHightModifiers(EntityPlayer player, ref double newEyeheight, ref double newModelHeight)
        {
            float modifier = GetEyeHightModifier(player);
            newEyeheight *= modifier;
            newModelHeight *= modifier;
        }

        public static float GetEyeHightModifier(EntityPlayer player)
        {
            PlayerSkinBehavior? skinBehavior = player.GetBehavior<PlayerSkinBehavior>();

            if (skinBehavior == null) return 1f;

            bool moving = (player.Controls.TriesToMove && player.SidedPos.Motion.LengthSq() > 0.00001) && !player.Controls.NoClip && !player.Controls.DetachedMode;
            bool walking = moving && player.OnGround;

            if (walking && !player.Controls.Backward && !player.Controls.Sneak && !player.Controls.IsClimbing && !player.Controls.IsFlying)
            {
                if (player.Controls.Sprint)
                {
                    return skinBehavior.CurrentModel.SprintEyeHeightMultiplier;
                }
                else
                {
                    return skinBehavior.CurrentModel.WalkEyeHeightMultiplier;
                }
            }

            return 1;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new(instructions);
            MethodInfo callMethod = AccessTools.Method(typeof(PatchEntityPlayerUpdateEyeHeightInsertBeforeFloorSitting), nameof(ApplyEyeHightModifiers));

            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (codes[i].opcode == OpCodes.Ldloc_2 &&
                    codes[i + 1].opcode == OpCodes.Callvirt &&
                    codes[i + 1].operand is MethodInfo mi &&
                    mi.Name == "get_FloorSitting")
                {
                    // Insert before i
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),        // this (EntityPlayer)
                        new CodeInstruction(OpCodes.Ldloca_S, 4),    // ref newEyeheight
                        new CodeInstruction(OpCodes.Ldloca_S, 5),    // ref newModelHeight
                        new CodeInstruction(OpCodes.Call, callMethod)
                    });
                    break;
                }
            }

            return codes;
        }
    }

    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    [HarmonyPatch(typeof(Entity), "FromBytes", [typeof(BinaryReader), typeof(bool)])]
    public static class PatchEntityFromBytesRemoveMaxSaturation
    {
        static readonly MethodInfo SetFloatMethod = AccessTools.Method(typeof(ITreeAttribute), nameof(ITreeAttribute.SetFloat));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt &&
                    codes[i].operand is MethodInfo mi &&
                    mi == SetFloatMethod)
                {
                    codes.RemoveRange(i - 3, 4);
                    i -= 4;
                }
            }

            return codes;
        }
    }

    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "updateWearableConditions")]
    public static class Patch_UpdateWearableConditions
    {
        private static void ApplyWarmthStats(ref float clothingBonus, EntityAgent agent)
        {
            float value = clothingBonus;
            value += Math.Clamp(agent.Stats.GetBlended(StatsPatches.WarmthBonusStat), -100, 100);
            clothingBonus = value;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new(instructions);

            FieldInfo clothingBonusField = AccessTools.Field(
                typeof(EntityBehaviorBodyTemperature),
                "clothingBonus");

            FieldInfo entityField = AccessTools.Field(
                typeof(EntityBehavior),
                "entity");

            MethodInfo hookMethod = AccessTools.Method(
                typeof(Patch_UpdateWearableConditions),
                nameof(ApplyWarmthStats));

            for (int i = code.Count - 1; i >= 0; i--)
            {
                if (code[i].opcode == OpCodes.Ret)
                {
                    List<CodeInstruction> insert =
                    [
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldflda, clothingBonusField),

                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, entityField),
                        new CodeInstruction(OpCodes.Isinst, typeof(EntityAgent)),

                        new CodeInstruction(OpCodes.Call, hookMethod)
                    ];

                    code.InsertRange(i, insert);
                    break;
                }
            }

            return code;
        }
    }
}