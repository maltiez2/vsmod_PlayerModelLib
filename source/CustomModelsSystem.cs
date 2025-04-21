using Newtonsoft.Json.Linq;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class CustomShapeConfig
{
    public string ShapePath { get; set; } = "";
    public SkinnablePart[] SkinnableParts { get; set; } = Array.Empty<SkinnablePart>();
}

public sealed class CustomModelsSystem : ModSystem
{
    public Dictionary<string, Shape?> CustomModels { get; } = new();
    public Dictionary<string, Dictionary<string, SkinnablePart>> SkinParts { get; set; } = new();
    public Dictionary<string, SkinnablePart[]> SkinPartsArrays { get; set; } = new();
    public string DefaultModelCode { get; private set; } = "seraph";
    public Shape? DefaultModel { get; private set; }

    public override void Start(ICoreAPI api)
    {

    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        DefaultModel = LoadShape(api, "game:entity/humanoid/seraph-faceless");
        DefaultModel?.ResolveReferences(api.Logger, "PlayerModelLib:CustomModel-default");

        Load(api);
        ProcessAnimations(api);
    }

    public void LoadDefault(Entity entity)
    {
        if (_defaultLoaded) return;
        _defaultLoaded = true;

        SkinnablePart[] parts = entity.Properties.Attributes["skinnableParts"].AsObject<SkinnablePart[]>();

        Dictionary<string, SkinnablePart> partsByCode = LoadParts(entity.Api, parts);

        CustomModels.Add(DefaultModelCode, DefaultModel);
        SkinParts.Add(DefaultModelCode, partsByCode);
        SkinPartsArrays.Add(DefaultModelCode, parts);
    }

    private bool _defaultLoaded = false;

    private static Shape? LoadShape(ICoreAPI api, string path)
    {
        AssetLocation shapeLocation = new(path);
        shapeLocation = shapeLocation.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
        Shape? currentShape = Shape.TryGet(api, shapeLocation);
        return currentShape;
    }
    private void LoadDefaultServerSide(ICoreServerAPI api)
    {
        IAsset? playerEntity = api.Assets.TryGet("game:entities/humanoid/player.json");

        if (playerEntity == null)
        {
            // @TODO add error logging
            return;
        }

        JsonObject json;

        try
        {
            json = JsonObject.FromJson(playerEntity.ToText());
        }
        catch (Exception exception)
        {
            // @TODO add error logging
            return;
        }

        SkinnablePart[] parts = json["attributes"]["skinnableParts"].AsObject<SkinnablePart[]>();

        Dictionary<string, SkinnablePart> partsByCode = LoadParts(api, parts);

        CustomModels.Add(DefaultModelCode, DefaultModel);
        SkinParts.Add(DefaultModelCode, partsByCode);
        SkinPartsArrays.Add(DefaultModelCode, parts);
    }
    

    private void Load(ICoreAPI api)
    {
        List<IAsset> modelsConfigs = api.Assets.GetManyInCategory("config", "customplayermodels");

        foreach (Dictionary<string, CustomShapeConfig> customModelConfigs in modelsConfigs.Select(FromAsset))
        {
            foreach ((string code, CustomShapeConfig modelConfig) in customModelConfigs)
            {
                Shape? shape = LoadShape(api, modelConfig.ShapePath);

                if (shape == null) continue; // @TODO add error logging

                Dictionary<string, SkinnablePart> partsByCode = LoadParts(api, modelConfig.SkinnableParts);

                CustomModels.Add(code, shape);
                SkinParts.Add(code, partsByCode);
                SkinPartsArrays.Add(code, modelConfig.SkinnableParts);
            }
        }
    }
    private void ProcessAnimations(ICoreAPI api)
    {
        if (DefaultModel == null) return; // @TODO add error logging
        
        foreach (Shape customShape in CustomModels.Values.OfType<Shape>())
        {
            IEnumerable<string> existingAnimations = customShape.Animations.Select(entry => entry.Code);

            foreach ((uint crc32, Animation animation) in DefaultModel.AnimationsByCrc32)
            {
                if (existingAnimations.Contains(animation.Code)) continue;

                customShape.Animations = customShape.Animations.Append(animation).ToArray();
                customShape.AnimationsByCrc32.Add(crc32, animation);
            }

            customShape.ResolveReferences(api.Logger, "PlayerModelLib:CustomModel-test");
        }
    }

    private Dictionary<string, CustomShapeConfig> FromAsset(IAsset asset)
    {
        Dictionary<string, CustomShapeConfig> result = new();
        string domain = asset.Location.Domain;
        JsonObject json;

        try
        {
            json = JsonObject.FromJson(asset.ToText());
        }
        catch (Exception exception)
        {
            // @TODO add error logging
            return result;
        }

        foreach ((string code, JToken? token) in json.Token as JObject)
        {
            try
            {
                JsonObject configJson = new(token);
                CustomShapeConfig config = configJson.AsObject<CustomShapeConfig>();
                result.Add($"{domain}:{code}", config);
            }
            catch (Exception exception)
            {
                // @TODO add error logging
            }
        }

        return result;
    }
    private Dictionary<string, SkinnablePart> LoadParts(ICoreAPI api, SkinnablePart[] parts)
    {
        Dictionary<string, SkinnablePart> patsByCode = new();

        foreach (SkinnablePart part in parts)
        {
            part.VariantsByCode = new Dictionary<string, SkinnablePartVariant>();

            patsByCode[part.Code] = part;

            if (part.Type == EnumSkinnableType.Texture && api is ICoreClientAPI clientApi)
            {
                ProcessTexturePart(clientApi, part);
            }
            else
            {
                foreach (SkinnablePartVariant variant in part.Variants)
                {
                    part.VariantsByCode[variant.Code] = variant;
                }
            }
        }

        return patsByCode;
    }
    private void ProcessTexturePart(ICoreClientAPI clientApi, SkinnablePart part)
    {
        foreach (SkinnablePartVariant? variant in part.Variants)
        {
            AssetLocation textureLoc;

            if (part.TextureTemplate != null)
            {
                textureLoc = part.TextureTemplate.Clone();
                textureLoc.Path = textureLoc.Path.Replace("{code}", variant.Code);
            }
            else
            {
                textureLoc = variant.Texture;
            }

            IAsset asset = clientApi.Assets.TryGet(textureLoc.Clone().WithPathAppendixOnce(".png").WithPathPrefixOnce("textures/"), true);

            int r = 0, g = 0, b = 0;
            float c = 0;

            BitmapRef bmp = asset.ToBitmap(clientApi);
            for (int i = 0; i < 8; i++)
            {
                Vec2d vec = GameMath.R2Sequence2D(i);
                SKColor col2 = bmp.GetPixelRel((float)vec.X, (float)vec.Y);
                if (col2.Alpha > 0.5)
                {
                    r += col2.Red;
                    g += col2.Green;
                    b += col2.Blue;
                    c++;
                }
            }

            bmp.Dispose();

            c = Math.Max(1, c);
            variant.Color = ColorUtil.ColorFromRgba((int)(b / c), (int)(g / c), (int)(r / c), 255);
            part.VariantsByCode[variant.Code] = variant;
        }
    }
}