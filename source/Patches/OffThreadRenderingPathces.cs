using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static class OffThreadRenderingPatches
{
    public static void Patch(string harmonyId, ICoreAPI api)
    {
        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorContainer).GetMethod("addTexture", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OffThreadRenderingPatches), nameof(EntityBehaviorContainer_addTexture)))
            );
    }

    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorContainer).GetMethod("addTexture", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
    }

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
}
