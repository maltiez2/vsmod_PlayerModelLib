using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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

    // will be used in 1.22
    private static bool EntityBehaviorContainer_addTexture(EntityBehaviorContainer __instance, ICoreClientAPI capi, string texcode, AssetLocation tloc, IDictionary<string, CompositeTexture> textures, string texturePrefixCode, ITextureAtlasAPI targetAtlas)
    {
        if (capi == null) return true;

        CompositeTexture compositeTexture = new(tloc);
        if (textures != null)
        {
            textures[texturePrefixCode + texcode] = compositeTexture;
        }
        compositeTexture.Bake(capi.Assets);

        ThreadSafeUtils.InsertTextureIntoAtlas(compositeTexture, capi, __instance.entity);

        return false;
    }

    private static bool EntityBehaviorContainer_addTexture_121(EntityBehaviorContainer __instance, ICoreClientAPI capi, string texcode, AssetLocation tloc, IDictionary<string, CompositeTexture> textures, string texturePrefixCode)
    {
        if (capi == null) return true;

        CompositeTexture compositeTexture = new(tloc);
        if (textures != null)
        {
            string textureCode = $"{texturePrefixCode}{texcode}";

            textures[textureCode] = compositeTexture;
        }
        compositeTexture.Bake(capi.Assets);

        ThreadSafeUtils.InsertTextureIntoAtlas(compositeTexture, capi, __instance.entity);

        return false;
    }
}
