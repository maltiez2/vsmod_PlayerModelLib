using ConfigLib;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public sealed class Settings
{
    public bool ExportShapeFiles { get; set; } = false;
    public string DefaultModelCode { get; set; } = "seraph";
    public bool DisableModelClassesAndTraits { get; set; } = false;
}

public sealed class PlayerModelModSystem : ModSystem
{
    public static Settings Settings { get; set; } = new();
    public static event Action<ICoreAPI, Settings>? OnSettingsLoaded;

    public static ObjectCache<string, Shape>? ShapesCache { get; set; }

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntityBehaviorClass("PlayerModelLib:ExtraSkinnable", typeof(PlayerSkinBehavior));

        OtherPatches.SetApi(api);

        if (!_patched)
        {
            new Harmony("PlayerModelLibTranspiler").PatchAll();
            OtherPatches.Patch("PlayerModelLib", api);
            StatsPatches.Patch("PlayerModelLib", api);
            _patched = true;
        }

        if (api is ICoreClientAPI clientApi)
        {
            ScrollPatches.Init(clientApi);
        }

        if (api.ModLoader.IsModEnabled("configlib"))
        {
            SubscribeToConfigChange(api);
        }

        OnSettingsLoaded?.Invoke(api, Settings);

        ShapesCache = new(api, "[PML] shapes", TimeSpan.FromMinutes(10), threadSafe: false);
        ShapeLoadingUtil.ShapesCache = ShapesCache;
    }

    public override void Dispose()
    {
        if (_patched)
        {
            new Harmony("PlayerModelLib").UnpatchAll("PlayerModelLibTranspiler");
            OtherPatches.Unpatch("PlayerModelLib");
            StatsPatches.Unpatch("PlayerModelLib");
            _patched = false;
        }
        
        ShapesCache?.Dispose();
        ShapeReplacementUtil.StaticDispose();
    }

    private static bool _patched = false;

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