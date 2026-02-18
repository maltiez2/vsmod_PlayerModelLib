using HarmonyLib;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static class OffThreadRenderingPatches
{
    public static void Patch(string harmonyId, ICoreAPI api)
    {
        _clientApi = api as ICoreClientAPI;

        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorContainer).GetMethod("addTexture", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OffThreadRenderingPatches), nameof(EntityBehaviorContainer_addTexture_121)))
            );
    }

    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorContainer).GetMethod("addTexture", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        _clientApi = null;
    }

    public static void SetApi(ICoreAPI api)
    {
        if (api is ICoreClientAPI)
        {
            _clientApi = api as ICoreClientAPI;
        }
    }

    private static ICoreClientAPI? _clientApi;

    private static bool EntityBehaviorContainer_addTexture(EntityBehaviorContainer __instance, ICoreClientAPI capi, string texcode, AssetLocation tloc, IDictionary<string, CompositeTexture> textures, string texturePrefixCode, ITextureAtlasAPI targetAtlas)
    {
        if (_clientApi == null) return true;

        CompositeTexture compositeTexture = new(tloc);
        if (textures != null)
        {
            textures[texturePrefixCode + texcode] = compositeTexture;
        }
        compositeTexture.Bake(capi.Assets);

        PlayerSkinBehavior? skinBehavior = __instance.entity.GetBehavior<PlayerSkinBehavior>();

        if (Environment.CurrentManagedThreadId != RuntimeEnv.MainThreadId)
        {
            skinBehavior?.TexturesAwaitingToBeAddedToAtlas.Increment();
            _clientApi.Event.EnqueueMainThreadTask(() => InsertReplacedTextureIntoAtlas(compositeTexture, _clientApi, skinBehavior, targetAtlas), "PlayerSkinBehavior.AddSkinPart");
        }
        else
        {
            InsertReplacedTextureIntoAtlas(compositeTexture, _clientApi, skinBehavior);
        }

        return false;
    }

    private static bool EntityBehaviorContainer_addTexture_121(EntityBehaviorContainer __instance, ICoreClientAPI capi, string texcode, AssetLocation tloc, IDictionary<string, CompositeTexture> textures, string texturePrefixCode)
    {
        if (_clientApi == null) return true;

        CompositeTexture compositeTexture = new(tloc);
        if (textures != null)
        {
            string textureCode = $"{texturePrefixCode}{texcode}";

            textures[textureCode] = compositeTexture;
        }

        PlayerSkinBehavior? skinBehavior = __instance.entity.GetBehavior<PlayerSkinBehavior>();

        if (Environment.CurrentManagedThreadId != RuntimeEnv.MainThreadId)
        {
            skinBehavior?.TexturesAwaitingToBeAddedToAtlas.Increment();
            _clientApi.Event.EnqueueMainThreadTask(() => InsertReplacedTextureIntoAtlas(compositeTexture, _clientApi, skinBehavior), "PlayerSkinBehavior.AddSkinPart");
        }
        else
        {
            InsertReplacedTextureIntoAtlas(compositeTexture, _clientApi, skinBehavior);
        }

        return false;
    }

    private static void InsertReplacedTextureIntoAtlas(CompositeTexture compositeTexture, ICoreClientAPI api, PlayerSkinBehavior? skinBehavior, ITextureAtlasAPI? targetAtlas = null)
    {
        compositeTexture.Bake(_clientApi.Assets);
        if (!(targetAtlas ?? api.EntityTextureAtlas).GetOrInsertTexture(compositeTexture.Baked.TextureFilenames[0], out int textureSubId, out _))
        {
            return;
        }
        compositeTexture.Baked.TextureSubId = textureSubId;
        skinBehavior?.TexturesAwaitingToBeAddedToAtlas.Decrement();
    }
}
