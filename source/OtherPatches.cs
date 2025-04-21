using HarmonyLib;
using Vintagestory.API.Common;
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
}