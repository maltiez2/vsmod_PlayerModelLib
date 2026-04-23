using ConfigLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using OverhaulLib.Utils;

namespace PlayerModelLib;

public sealed class Settings
{
    public bool ExportShapeFiles { get; set; } = false;
    public string DefaultModelCode { get; set; } = "seraph";
    public bool DisableModelClassesAndTraits { get; set; } = false;
    public bool MultiThreadPayerShapeGeneration { get; set; } = true;
    public bool LogOffThreadTesselationErrors { get; set; } = true;
}

public sealed class LatePlayerModelModSystem : ModSystem
{
    public override double ExecuteOrder() => 1;

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.RegisterEntityRendererClass("PlayerShape", typeof(CustomPlayerShapeRenderer));
    }
}

public sealed class PlayerModelModSystem : ModSystem
{
    public static Settings Settings { get; set; } = new();
    public static event Action<ICoreAPI, Settings>? OnSettingsLoaded;

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("PlayerModelLib:ExtraSkinnable", typeof(PlayerSkinBehavior));
        api.RegisterEntityBehaviorClass("PlayerModelLib:CustomPlayerInventory", typeof(CustomPlayerInventory));
        api.RegisterEntityBehaviorClass("PlayerModelLib:WearablesTesselator", typeof(WearablesTesselatorBehavior));
        api.RegisterEntityBehaviorClass("PlayerModelLib:WearableCollectibleLight", typeof(WearableCollectibleLightBehavior));

        PatchesManager.Patch(api);

        if (api.ModLoader.IsModEnabled("configlib"))
        {
            SubscribeToConfigChange(api);
        }

        OnSettingsLoaded?.Invoke(api, Settings);
    }

    public override void Dispose()
    {
        PatchesManager.Unpatch();

        ShapeReplacementUtil.StaticDispose();
    }

    private static void SubscribeToConfigChange(ICoreAPI api)
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
            OnSettingsLoaded?.Invoke(api, Settings);
        };
    }
}