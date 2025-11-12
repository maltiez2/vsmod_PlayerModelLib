using ConfigLib;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public sealed class Settings
{
    public bool ExportShapeFiles { get; set; } = false;
    public string DefaultModelCode { get; set; } = "seraph";
}

public sealed class PlayerModelModSystem : ModSystem
{
    public static Settings Settings { get; set; } = new();
    public static event Action<ICoreAPI, Settings>? OnSettingsLoaded;

    public ObjectCache<string, Shape>? RescaledShapesCache { get; private set; }
    public ObjectCache<string, Shape>? ReplacedShapesCache { get; private set; }

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("PlayerModelLib:ExtraSkinnable", typeof(PlayerSkinBehavior));

        new Harmony("PlayerModelLibTranspiler").PatchAll();
        OtherPatches.Patch("PlayerModelLib", api);
        StatsPatches.Patch("PlayerModelLib", api);

        if (api is ICoreClientAPI clientApi)
        {
            ScrollPatches.Init(clientApi);
        }

        if (api.ModLoader.IsModEnabled("configlib"))
        {
            SubscribeToConfigChange(api);
        }

        OnSettingsLoaded?.Invoke(api, Settings);

        RescaledShapesCache = new(api, "rescaled shapes", 1000, 5 * 60 * 1000 + 7 * 1000, threadSafe: false);
        ReplacedShapesCache = new(api, "replaced shapes", 1000, 5 * 60 * 1000 + 13 * 1000, threadSafe: false);
        ShapeReplacementUtil.RescaledShapesCache = RescaledShapesCache;
        ShapeReplacementUtil.ReplacedShapesCache = ReplacedShapesCache;
    }

    public override void Dispose()
    {
        new Harmony("PlayerModelLib").UnpatchAll("PlayerModelLibTranspiler");
        OtherPatches.Unpatch("PlayerModelLib");
        StatsPatches.Unpatch("PlayerModelLib");
        RescaledShapesCache?.Dispose();
        ReplacedShapesCache?.Dispose();
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
            OnSettingsLoaded?.Invoke(api, Settings);
        };
    }
}