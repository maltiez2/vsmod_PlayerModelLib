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

    public ObjectCache<string, Shape>? RescaledShapesCache { get; private set; }
    public ObjectCache<string, Shape>? ReplacedShapesCache { get; private set; }
    public ObjectCache<string, ShapeReplacementUtil.FullShapeCacheEntry>? FullShapeCache { get; set; }

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

        RescaledShapesCache = new(api, "rescaled shapes", 5 * 60 * 1000 + 7 * 1000, threadSafe: false);
        ReplacedShapesCache = new(api, "replaced shapes", 5 * 60 * 1000 + 13 * 1000, threadSafe: false);
        FullShapeCache = new(api, "full replaced shapes", 5 * 60 * 1000 + 13 * 1000, threadSafe: false);
        ShapeReplacementUtil.RescaledShapesCache = RescaledShapesCache;
        ShapeReplacementUtil.ReplacedShapesCache = ReplacedShapesCache;
        ShapeReplacementUtil.FullShapeCache = FullShapeCache;
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
        
        RescaledShapesCache?.Dispose();
        ReplacedShapesCache?.Dispose();
        FullShapeCache?.Dispose();
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