using HarmonyLib;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

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
        new Harmony(harmonyId).Unpatch(typeof(OffThreadRenderingPatches).GetMethod("addTexture", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
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

        _clientApi.Event.EnqueueMainThreadTask(() => InsertReplacedTextureIntoAtlas(compositeTexture, _clientApi, targetAtlas), "PlayerSkinBehavior.AddSkinPart");

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

        _clientApi.Event.EnqueueMainThreadTask(() => InsertReplacedTextureIntoAtlas(compositeTexture, _clientApi), "PlayerSkinBehavior.AddSkinPart");

        return false;


        /*var cmpt = new CompositeTexture(tloc);
        textures[texturePrefixCode + texcode] = cmpt;
        cmpt.Bake(Api.Assets);
        capi.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _);
        cmpt.Baked.TextureSubId = textureSubid;*/
    }

    private static void InsertReplacedTextureIntoAtlas(CompositeTexture compositeTexture, ICoreClientAPI api, ITextureAtlasAPI? targetAtlas = null)
    {
        compositeTexture.Bake(_clientApi.Assets);
        if (!(targetAtlas ?? api.EntityTextureAtlas).GetOrInsertTexture(compositeTexture.Baked.TextureFilenames[0], out int textureSubId, out _))
        {
            return;
        }
        compositeTexture.Baked.TextureSubId = textureSubId;
    }
}
