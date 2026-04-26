using OverhaulLib.Utils;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace PlayerModelLib;

public class EntityTextureSourceBehavior : EntityBehavior, ITexPositionSource
{
    public EntityTextureSourceBehavior(Entity entity) : base(entity)
    {
        Api = entity.Api as ICoreClientAPI ?? throw new InvalidOperationException("EntityTextureSourceBehavior is client side only behavior");

        CustomModelsSystem system = Api.ModLoader.GetModSystem<CustomModelsSystem>() ?? throw new InvalidOperationException("'CustomModelsSystem' not found");

        if (system.TextureSource != null)
        {
            TextureSourcesByPriority.Add((0, system.TextureSource));
        }
        else
        {
            Log.Error(Api, this, $"CustomModelsSystem.TextureSource is null");
        }
    }

    public TextureAtlasPosition? this[string textureCode] => GetTexturePosition(textureCode);
    public Size2i? AtlasSize => Api.EntityTextureAtlas.Size;
    public ICoreClientAPI Api { get; }
    public override string PropertyName() => "EntityTextureSourceBehavior";


    public virtual void AddTextureSource(ITexPositionSource source, float priority)
    {
        for (int entryIndex = 0;  entryIndex < TextureSourcesByPriority.Count; entryIndex++)
        {
            if (TextureSourcesByPriority[entryIndex].priority < priority)
            {
                TextureSourcesByPriority.Insert(entryIndex, (priority, source));
                return;
            }
        }

        TextureSourcesByPriority.Add((priority, source));
    }
    public virtual TextureAtlasPosition GetTexturePosition(string code)
    {
        foreach (( _, ITexPositionSource source) in TextureSourcesByPriority)
        {
            try
            {
                TextureAtlasPosition? position = source[code];
                if (position != null)
                {
                    return position;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"({source.GetType()})" + exception);
            }
        }

        return Api.EntityTextureAtlas.UnknownTexturePosition;
    }



    protected List<(float priority, ITexPositionSource source)> TextureSourcesByPriority { get; } = [];
}
