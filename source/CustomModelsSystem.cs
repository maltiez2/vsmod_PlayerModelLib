using Newtonsoft.Json.Linq;
using ProtoBuf;
using SkiaSharp;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class CustomShapeConfig
{
    public string ShapePath { get; set; } = "";
    public string MainTextureCode { get; set; } = "seraph";
    public SkinnablePart[] SkinnableParts { get; set; } = Array.Empty<SkinnablePart>();
    public Dictionary<string, string> WearableModelReplacers { get; set; } = new();
    public Dictionary<string, string> WearableModelReplacersByShape { get; set; } = new();
    public string[] AvailableClasses { get; set; } = Array.Empty<string>();
    public string[] SkipClasses { get; set; } = Array.Empty<string>();
    public string[] ExtraTraits { get; set; } = Array.Empty<string>();
    public string Domain { get; set; } = "game";
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ChangePlayerModelPacket
{
    public string ModelCode { get; set; } = "";
}

public sealed class CustomModelsSystem : ModSystem
{
    public Dictionary<string, Shape?> CustomModels { get; } = new();
    public Dictionary<string, Dictionary<string, SkinnablePart>> SkinParts { get; set; } = new();
    public Dictionary<string, SkinnablePart[]> SkinPartsArrays { get; set; } = new();
    public Dictionary<string, string> MainTextureCodes { get; set; } = new();
    public Dictionary<string, CompositeTexture> MainTextures { get; set; } = new();
    public Dictionary<string, int[]> MainTextureSizes { get; set; } = new();
    public Dictionary<string, Dictionary<int, string>> WearableShapeReplacers { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> WearableShapeReplacersByShape { get; set; } = new();
    public Dictionary<string, HashSet<string>> AvailableClasses { get; set; } = new();
    public Dictionary<string, HashSet<string>> SkipClasses { get; set; } = new();
    public Dictionary<string, string[]> ExtraTraits { get; set; } = new();
    public ContainedTextureSource? TextureSource { get; private set; }
    public string DefaultModelCode { get; private set; } = "seraph";
    public Shape? DefaultModel { get; private set; }


    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;
        _clientChannel = api.Network.RegisterChannel("PlayerModelLib:CustomModelsSystem")
            .RegisterMessageType<ChangePlayerModelPacket>();

        TextureSource = new(api, api.EntityTextureAtlas, _textures, "PlayerModelLib:CustomModelsSystem");
    }
    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Network.RegisterChannel("PlayerModelLib:CustomModelsSystem")
            .RegisterMessageType<ChangePlayerModelPacket>()
            .SetMessageHandler<ChangePlayerModelPacket>(HandleChangePlayerModelPacket);
    }
    public override void AssetsLoaded(ICoreAPI api)
    {
        _clientApi = api as ICoreClientAPI;

        DefaultModel = LoadShape(api, "game:entity/humanoid/seraph-faceless");
        DefaultModel?.ResolveReferences(api.Logger, "PlayerModelLib:CustomModel-default");

        Load(api);
        ProcessMainTextures();
        ProcessAnimations(api);
    }
    public override void AssetsFinalize(ICoreAPI api)
    {
        CollectTextures();
        LoadModelReplacements(api);
    }
    public TextureAtlasPosition? GetAtlasPosition(string modelCode, string textureCode, Entity entity)
    {
        if (modelCode == DefaultModelCode)
        {
            int textureIndex = entity.WatchedAttributes.GetInt("textureIndex");
            return _clientApi?.Tesselator.GetTextureSource(entity, null, textureIndex)[textureCode];
        }

        string fullCode = $"{modelCode}-{textureCode}";

        if (textureCode == MainTextureCodes[modelCode])
        {
            fullCode = textureCode;
        }

        //Debug.WriteLine(fullCode);

        if (!_textures.ContainsKey(fullCode))
        {
            int textureIndex = entity.WatchedAttributes.GetInt("textureIndex");
            try
            {
                return _clientApi?.Tesselator.GetTextureSource(entity, null, textureIndex)?[textureCode];
            }
            catch (Exception exception)
            {
                return null;
            }
        }

        return TextureSource?[fullCode];
    }
    public Size2i? GetAtlasSize(string modelCode, Entity entity)
    {
        if (modelCode == DefaultModelCode)
        {
            int textureIndex = entity.WatchedAttributes.GetInt("textureIndex");
            return _clientApi?.Tesselator.GetTextureSource(entity, null, textureIndex).AtlasSize;
        }

        return TextureSource?.AtlasSize;
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
        MainTextureCodes.Add(DefaultModelCode, entity.Properties.Attributes["mainTextureCode"].AsString("seraph"));
    }
    public void SynchronizePlayerModel(string code)
    {
        _clientChannel?.SendPacket(new ChangePlayerModelPacket()
        {
            ModelCode = code
        });
    }

    private bool _defaultLoaded = false;
    private IClientNetworkChannel? _clientChannel;
    private readonly Dictionary<string, AssetLocation> _textures = new();
    private ICoreClientAPI? _clientApi;
    private readonly Dictionary<string, string> _oldMainTextureCodes = new();
    private readonly Dictionary<string, Dictionary<string, string>> _wearableModelReplacers = new();

    private static Shape? LoadShape(ICoreAPI api, string path)
    {
        AssetLocation shapeLocation = new(path);
        shapeLocation = shapeLocation.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
        Shape? currentShape = Shape.TryGet(api, shapeLocation);
        return currentShape;
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

                _wearableModelReplacers.Add(code, modelConfig.WearableModelReplacers);

                Dictionary<string, SkinnablePart> partsByCode = LoadParts(api, modelConfig.SkinnableParts);

                CustomModels.Add(code, shape);
                SkinParts.Add(code, partsByCode);
                SkinPartsArrays.Add(code, modelConfig.SkinnableParts);
                MainTextureCodes.Add(code, $"{modelConfig.Domain}-{code}-{modelConfig.MainTextureCode}");
                _oldMainTextureCodes.Add(code, modelConfig.MainTextureCode);
                AvailableClasses.Add(code, modelConfig.AvailableClasses.ToHashSet());
                ExtraTraits.Add(code, modelConfig.ExtraTraits);
                SkipClasses.Add(code, modelConfig.SkipClasses.ToHashSet());
                WearableShapeReplacersByShape.Add(code, modelConfig.WearableModelReplacersByShape);
            }
        }
    }
    private void LoadModelReplacements(ICoreAPI api)
    {
        foreach ((string modelCode, Dictionary<string, string> paths) in _wearableModelReplacers)
        {
            WearableShapeReplacers.Add(modelCode, new());

            foreach ((string itemCodeWildcard, string path) in paths)
            {
                foreach (Item item in api.World.Items)
                {
                    if (WildcardUtil.Match(itemCodeWildcard, item.Code?.ToString() ?? ""))
                    {
                        WearableShapeReplacers[modelCode].TryAdd(item.Id, path);
                    }
                }
            }
        }

        List<IAsset> modelsConfigs = api.Assets.GetMany("config/model-replacements-bycode");
        foreach (IAsset asset in modelsConfigs)
        {
            Dictionary<string, Dictionary<string, string>> replacements = ReplacementsFromAsset(asset);

            foreach ((string modelCode, Dictionary<string, string> paths) in replacements)
            {
                if (!WearableShapeReplacers.ContainsKey(modelCode))
                {
                    WearableShapeReplacers.Add(modelCode, new());
                }

                foreach ((string itemCodeWildcard, string path) in paths)
                {
                    foreach (Item item in api.World.Items)
                    {
                        if (WildcardUtil.Match(itemCodeWildcard, item.Code?.ToString() ?? ""))
                        {
                            WearableShapeReplacers[modelCode][item.Id] = path;
                        }
                    }
                }
            }
        }

        List<IAsset> modelsConfigs2 = api.Assets.GetMany("config/model-replacements-byshape");
        foreach (IAsset asset in modelsConfigs2)
        {
            Dictionary<string, Dictionary<string, string>> replacements = ReplacementsFromAsset(asset);

            foreach ((string modelCode, Dictionary<string, string> paths) in replacements)
            {
                if (!WearableShapeReplacersByShape.ContainsKey(modelCode))
                {
                    WearableShapeReplacersByShape.Add(modelCode, new());
                }

                foreach ((string fromPath, string toPath) in paths)
                {
                    WearableShapeReplacersByShape[modelCode][fromPath] = toPath;
                }
            }
        }
    }
    private void ProcessAnimations(ICoreAPI api)
    {
        if (DefaultModel == null) return; // @TODO add error logging

        foreach (Shape customShape in CustomModels.Values.OfType<Shape>())
        {
            if (customShape == DefaultModel) continue;

            HashSet<string> existingAnimations = customShape.Animations.Select(GetAnimationCode).ToHashSet();

            foreach ((uint crc32, Animation animation) in DefaultModel.AnimationsByCrc32)
            {
                string code = GetAnimationCode(animation);

                if (existingAnimations.Contains(code)) continue;

                existingAnimations.Add(code);
                customShape.Animations = customShape.Animations.Append(animation.Clone()).ToArray();
                //customShape.AnimationsByCrc32.Add(crc32, customShape.Animations.Last());
            }

            customShape.ResolveReferences(api.Logger, "PlayerModelLib:CustomModel-test");
        }
    }
    private string GetAnimationCode(Animation anim)
    {
        if (anim.Code == null || anim.Code.Length == 0)
        {
            return anim.Name.ToLowerInvariant().Replace(" ", "");
        }
        return anim.Code.ToLowerInvariant().Replace(" ", "");
    }
    private void HandleChangePlayerModelPacket(IPlayer player, ChangePlayerModelPacket packet)
    {
        player.Entity.WatchedAttributes.SetString("skinModel", packet.ModelCode);
        player.Entity.WatchedAttributes.SetStringArray("extraTraits", ExtraTraits[packet.ModelCode]);
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
                config.Domain = domain;
                result.Add($"{domain}:{code}", config);
            }
            catch (Exception exception)
            {
                // @TODO add error logging
            }
        }

        return result;
    }
    private Dictionary<string, Dictionary<string, string>> ReplacementsFromAsset(IAsset asset)
    {
        try
        {
            return JsonObject.FromJson(asset.ToText()).AsObject<Dictionary<string, Dictionary<string, string>>>();
        }
        catch (Exception exception)
        {
            // @TODO add error logging
            return new();
        }
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
    private void ProcessMainTextures()
    {
        foreach ((string modelCode, Shape? shape) in CustomModels)
        {
            if (shape == null || modelCode == DefaultModelCode) continue;

            string oldTextureCode = _oldMainTextureCodes[modelCode];
            string newTextureCode = MainTextureCodes[modelCode];

            if (shape.Textures.ContainsKey(oldTextureCode))
            {
                AssetLocation texturePath = shape.Textures[oldTextureCode];
                shape.Textures.Remove(oldTextureCode);
                shape.Textures[newTextureCode] = texturePath;
            }

            if (shape.TextureSizes.ContainsKey(oldTextureCode))
            {
                int[] size = shape.TextureSizes[oldTextureCode];
                shape.TextureSizes.Remove(oldTextureCode);
                shape.TextureSizes[newTextureCode] = size;
                MainTextureSizes[newTextureCode] = size;
            }

            foreach (ShapeElement element in shape.Elements)
            {
                ProcessShapeElement(element, oldTextureCode, newTextureCode);
            }
        }
    }
    private void ProcessShapeElement(ShapeElement element, string oldTextureCode, string newTextureCode)
    {
        if (element.Children != null)
        {
            foreach (ShapeElement child in element.Children)
            {
                ProcessShapeElement(child, oldTextureCode, newTextureCode);
            }
        }

        if (element.FacesResolved == null) return;

        foreach (ShapeElementFace face in element.FacesResolved)
        {
            if (face?.Texture == oldTextureCode)
            {
                face.Texture = newTextureCode;
            }
        }
    }
    private void CollectTextures()
    {
        if (_clientApi == null) return;

        foreach ((string modelCode, Shape? model) in CustomModels)
        {
            if (model == null) continue;
            if (modelCode == DefaultModelCode) continue;

            foreach ((string textureCode, AssetLocation texturePath) in model.Textures)
            {
                if (textureCode == MainTextureCodes[modelCode])
                {
                    if (_textures.ContainsKey(textureCode)) continue;

                    _textures.Add(textureCode, texturePath);
                    _ = TextureSource?[textureCode];

                    IAsset? textureAsset2 = _clientApi?.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (textureAsset2 == null) continue;

                    Debug.WriteLine($"Loading texture {modelCode}-{textureCode} with path {texturePath}");

                    _clientApi?.EntityTextureAtlas.GetOrInsertTexture(texturePath, out _, out _, () => textureAsset2.ToBitmap(_clientApi));

                    MainTextures[modelCode] = new CompositeTexture()
                    {
                        Base = texturePath
                    };

                    MainTextures[modelCode].Bake(_clientApi.Assets);

                    continue;
                }

                if (_textures.ContainsKey($"{modelCode}-{textureCode}")) continue;

                _textures.Add($"{modelCode}-{textureCode}", texturePath);
                _ = TextureSource?[$"{modelCode}-{textureCode}"];

                IAsset? textureAsset = _clientApi?.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (textureAsset == null) continue;

                Debug.WriteLine($"Loading texture {modelCode}-{textureCode} with path {texturePath}");

                _clientApi?.EntityTextureAtlas.GetOrInsertTexture(texturePath, out _, out _, () => textureAsset.ToBitmap(_clientApi));
            }
        }
    }
}