using ConfigLib;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public sealed class Settings
{
    public bool ExportShapeFiles { get; set; } = false;
}

public sealed class PlayerModelModSystem : ModSystem
{
    public static Settings Settings { get; set; } = new();

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("PlayerModelLib:ExtraSkinnable", typeof(PlayerSkinBehavior));

        new Harmony("PlayerModelLibTranspiler").PatchAll();
        OtherPatches.Patch("PlayerModelLib", api);

        if (api is ICoreClientAPI clientApi)
        {
            ScrollPatches.Init(clientApi);
        }

        if (api.ModLoader.IsModEnabled("configlib"))
        {
            SubscribeToConfigChange(api);
        }
    }

    public override void Dispose()
    {
        new Harmony("PlayerModelLib").UnpatchAll("PlayerModelLibTranspiler");
        OtherPatches.Unpatch("PlayerModelLib");
    }

    private void SubscribeToConfigChange(ICoreAPI api)
    {
        ConfigLibModSystem system = api.ModLoader.GetModSystem<ConfigLibModSystem>();

        system.SettingChanged += (domain, config, setting) =>
        {
            if (domain != "playermodellib") return;

            setting.AssignSettingValue(Settings);
        };

        system.ConfigsLoaded += () =>
        {
            system.GetConfig("playermodellib")?.AssignSettingsValues(Settings);
        };
    }
}