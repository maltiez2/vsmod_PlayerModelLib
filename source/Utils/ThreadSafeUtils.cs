using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace PlayerModelLib;

public static class ThreadSafeUtils
{
    public static void InsertTextureIntoAtlas(CompositeTexture compositeTexture, ICoreClientAPI api, Entity entity, ITextureAtlasAPI? targetAtlas = null, CustomPlayerShapeRenderer? renderer = null, Action<int, TextureAtlasPosition>? onInsert = null)
    {
        renderer ??= entity.Properties.Client.Renderer as CustomPlayerShapeRenderer;
        renderer?.TexturesAwaitingToBeAddedToAtlas.Increment();

        if (Environment.CurrentManagedThreadId != RuntimeEnv.MainThreadId)
        {
            api.Event.EnqueueMainThreadTask(() => InsertTextureIntoAtlasTask(compositeTexture, api, renderer, targetAtlas, onInsert), "ThreadSafeUtils.InsertTextureIntoAtlas");
        }
        else
        {
            InsertTextureIntoAtlasTask(compositeTexture, api, renderer, targetAtlas, onInsert);
        }
    }

    private static void InsertTextureIntoAtlasTask(CompositeTexture compositeTexture, ICoreClientAPI api, CustomPlayerShapeRenderer? renderer, ITextureAtlasAPI? targetAtlas = null, Action<int, TextureAtlasPosition>? onInsert = null)
    {
        try
        {
            if (compositeTexture.Baked == null)
            {
                compositeTexture.Bake(api.Assets);
            }

            if ((targetAtlas ?? api.EntityTextureAtlas).GetOrInsertTexture(compositeTexture, out int textureSubId, out TextureAtlasPosition? position) && compositeTexture.Baked != null)
            {
                compositeTexture.Baked.TextureSubId = textureSubId;
                onInsert?.Invoke(textureSubId, position);
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"InsertTextureIntoAtlasTask error:\n{exception}");
        }
        finally
        {
            renderer?.TexturesAwaitingToBeAddedToAtlas.Decrement();
        }
    }
}
