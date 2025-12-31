using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PlayerModelLib;

internal static class TranspilerPatches
{
    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    public static class PatchEntityBehaviorContainerCommand
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
                new(OpCodes.Ldarg_S, (byte)6),
                new(OpCodes.Call, AccessTools.Method(typeof(ShapeReplacementUtil), nameof(ShapeReplacementUtil.GetModelReplacement))),
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
    }

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

    [HarmonyPatch(typeof(Entity), "FromBytes", [typeof(BinaryReader), typeof(bool)])]
    public static class PatchEntityFromBytesRemoveMaxSaturationPatch
    {
        static readonly MethodInfo SetFloatMethod = AccessTools.Method(typeof(ITreeAttribute), nameof(ITreeAttribute.SetFloat));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

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
}