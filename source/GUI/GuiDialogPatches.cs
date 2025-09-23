using HarmonyLib;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static class GuiDialogPatches
{
    public static object NewGuiClassConstructor { get; set; } = AccessTools.Constructor(typeof(GuiDialogCreateCustomCharacter), [typeof(ICoreClientAPI), typeof(CharacterSystem)]);

    [HarmonyPatch(typeof(CharacterSystem), "Event_PlayerJoin")]
    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "harmony specific")]
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "harmony specific>")]
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
