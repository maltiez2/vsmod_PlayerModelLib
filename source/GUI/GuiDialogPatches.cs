using HarmonyLib;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static class GuiDialogPatches
{
    public static object NewGuiClassConstructor { get; set; } = AccessTools.Constructor(typeof(GuiDialogCreateCustomCharacter), new Type[] { typeof(ICoreClientAPI), typeof(CharacterSystem) });

    [HarmonyPatch(typeof(CharacterSystem), "Event_PlayerJoin")]
    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
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
                    codes[i].operand = NewGuiClassConstructor;

                    return codes;
                }
            }

            return codes;
        }
    }

    [HarmonyPatch(typeof(CharacterSystem), "onCharSelCmd")]
    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
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
                    codes[i].operand = NewGuiClassConstructor;

                    return codes;
                }
            }

            return codes;
        }
    }
}
