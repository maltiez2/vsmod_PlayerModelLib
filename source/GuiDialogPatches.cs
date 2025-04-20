using HarmonyLib;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static class GuiDialogPatches
{
    public static void Patch(string harmonyId, ICoreAPI api)
    {

    }

    public static void Unpatch(string harmonyId, ICoreAPI api)
    {

    }

    [HarmonyPatch(typeof(CharacterSystem), "Event_PlayerJoin")]
    [HarmonyPatchCategory("playermodellib")]
    public class GuiDialogPatchPlayerJoin
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj)
                {
                    codes[i].operand = AccessTools.Constructor(typeof(GuiDialogCreateCustomCharacter), new Type[] { typeof(ICoreClientAPI), typeof(CharacterSystem) });

                    return codes;
                }
            }

            return codes;
        }
    }

    [HarmonyPatch(typeof(CharacterSystem), "onCharSelCmd")]
    [HarmonyPatchCategory("playermodellib")]
    public class GuiDialogPatchCommand
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj)
                {
                    codes[i].operand = AccessTools.Constructor(typeof(GuiDialogCreateCustomCharacter), new Type[] { typeof(ICoreClientAPI), typeof(CharacterSystem) });

                    return codes;
                }
            }

            return codes;
        }
    }
}
