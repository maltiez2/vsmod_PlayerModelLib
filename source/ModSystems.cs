using HarmonyLib;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public sealed class PlayerModelModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("PlayerModelLib:ExtraSkinnable", typeof(ExtraSkinnableBehavior));

        new Harmony("PlayerModelLibTranspiler").PatchAll();
        OtherPatches.Patch("PlayerModelLib");
    }

    public override void Dispose()
    {
        new Harmony("PlayerModelLib").UnpatchAll();
        OtherPatches.Unpatch("PlayerModelLib");
    }
}