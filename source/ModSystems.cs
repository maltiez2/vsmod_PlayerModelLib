using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public sealed class PlayerModelModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("PlayerModelLib:ExtraSkinnable", typeof(PlayerSkinBehavior));

        new Harmony("PlayerModelLibTranspiler").PatchAll();
        OtherPatches.Patch("PlayerModelLib");

        if (api is ICoreClientAPI clientApi)
        {
            ScrollPatches.Init(clientApi);
        }
    }

    public override void Dispose()
    {
        new Harmony("PlayerModelLib").UnpatchAll();
        OtherPatches.Unpatch("PlayerModelLib");
    }
}