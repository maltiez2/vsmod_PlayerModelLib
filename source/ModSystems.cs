using HarmonyLib;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public sealed class PlayerModelModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("PlayerModelLib:CustomModel", typeof(CustomModelBehavior));

        new Harmony("PlayerModelLib").PatchAll();
    }

    public override void Dispose()
    {
        new Harmony("PlayerModelLib").UnpatchAll();
    }
}