using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;
using ProtoBuf;
using SkiaSharp;
using System;
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

public class SkinnablePartExtended : SkinnablePart
{
    public string[] TargetSkinParts { get; set; } = [];
    public bool OverlayTexture { get; set; } = false;
    public EnumColorBlendMode OverlayMode { get; set; } = EnumColorBlendMode.Normal;
}

public class CustomModelConfig
{
    public bool Enabled { get; set; } = true;
    public string Domain { get; set; } = "game";
    public string ShapePath { get; set; } = "";
    public string MainTextureCode { get; set; } = "seraph";
    public SkinnablePartExtended[] SkinnableParts { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacers { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacersByShape { get; set; } = [];
    public string[] AvailableClasses { get; set; } = [];
    public string[] SkipClasses { get; set; } = [];
    public string[] ExtraTraits { get; set; } = [];
    public string[] ExclusiveClasses { get; set; } = [];
    public float[] CollisionBox { get; set; } = [];
    public float EyeHeight { get; set; } = 1.7f;
    public float[] SizeRange { get; set; } = [0.8f, 1.2f];
    public bool ScaleColliderWithSizeHorizontally { get; set; } = true;
    public bool ScaleColliderWithSizeVertically { get; set; } = true;
    public float[] MaxCollisionBox { get; set; } = [float.MaxValue, float.MaxValue];
    public float[] MinCollisionBox { get; set; } = [0, 0];
    public float MaxEyeHeight { get; set; } = float.MaxValue;
    public float MinEyeHeight { get; set; } = 0;
    public string[] AddTags { get; set; } = [];
    public string[] RemoveTags { get; set; } = [];
    public float ModelSizeFactor { get; set; } = 1;
}

public class CustomModelData
{
    public bool Enabled { get; set; } = true;
    public string Code { get; set; }
    public Shape Shape { get; set; }
    public Dictionary<string, SkinnablePart> SkinParts { get; set; } = [];
    public SkinnablePart[] SkinPartsArray { get; set; } = [];
    public string MainTextureCode { get; set; } = "";
    public CompositeTexture? MainTexture { get; set; }
    public Vector2i MainTextureSize { get; set; }
    public TextureAtlasPosition? MainTexturePosition { get; set; }
    public Dictionary<int, string> WearableShapeReplacers { get; set; } = [];
    public Dictionary<string, string> WearableShapeReplacersByShape { get; set; } = [];
    public HashSet<string> AvailableClasses { get; set; } = [];
    public HashSet<string> SkipClasses { get; set; } = [];
    public HashSet<string> ExclusiveClasses { get; set; } = [];
    public string[] ExtraTraits { get; set; } = [];
    public Vector2 CollisionBox { get; set; }
    public float EyeHeight { get; set; }
    public Vector2 SizeRange { get; set; }
    public bool ScaleColliderWithSizeHorizontally { get; set; } = true;
    public bool ScaleColliderWithSizeVertically { get; set; } = true;
    public Vector2 MaxCollisionBox { get; set; }
    public Vector2 MinCollisionBox { get; set; }
    public float MaxEyeHeight { get; set; } = float.MaxValue;
    public float MinEyeHeight { get; set; } = 0;
    //public EntityTagArray AddTags { get; set; } = EntityTagArray.Empty;
    //public EntityTagArray RemoveTags { get; set; } = EntityTagArray.Empty;
    public float ModelSizeFactor { get; set; } = 1;


    public CustomModelData(string code, Shape shape)
    {
        Code = code;
        Shape = shape;
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ChangePlayerModelPacket
{
    public string ModelCode { get; set; } = "";
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ChangePlayerModelSizePacket
{
    public float EntitySize { get; set; } = 1;
}

public sealed class CustomModelsSystem : ModSystem
{
    public Dictionary<string, CustomModelData> CustomModels { get; } = [];
    public ContainedTextureSource? TextureSource { get; private set; }
    public string DefaultModelCode => CustomModels.Where(entry => entry.Value.Enabled).Select(entry => entry.Key).FirstOrDefault(_defaultModelCode);
    public Shape DefaultModel => CustomModels[DefaultModelCode].Shape;
    public CustomModelData DefaultModelData => CustomModels[DefaultModelCode];
    public bool ModelsLoaded { get; private set; } = false;
    public HashSet<string> ExclusiveClasses { get; private set; } = [];

    public event Action? OnCustomModelsLoaded;

    public override double ExecuteOrder() => 0.21;


    public override void StartClientSide(ICoreClientAPI api)
    {
        _api = api;
        _clientApi = api;
        _clientChannel = api.Network.RegisterChannel("PlayerModelLib:CustomModelsSystem")
            .RegisterMessageType<ChangePlayerModelPacket>()
            .RegisterMessageType<ChangePlayerModelSizePacket>();

        TextureSource = new(api, api.EntityTextureAtlas, _textures, "PlayerModelLib:CustomModelsSystem");
    }
    public override void StartServerSide(ICoreServerAPI api)
    {
        _api = api;
        api.Network.RegisterChannel("PlayerModelLib:CustomModelsSystem")
            .RegisterMessageType<ChangePlayerModelPacket>()
            .RegisterMessageType<ChangePlayerModelSizePacket>()
            .SetMessageHandler<ChangePlayerModelPacket>(HandleChangePlayerModelPacket)
            .SetMessageHandler<ChangePlayerModelSizePacket>(HandleChangePlayerModelSizePacket);
    }
    public override void AssetsLoaded(ICoreAPI api)
    {
        _api = api;
        _clientApi = api as ICoreClientAPI;
    }
    public override void AssetsFinalize(ICoreAPI api)
    {
        LoadDefault();
        Load(api);
        CollectExclusiveClasses();

        if (api.Side == EnumAppSide.Client)
        {
            ProcessMainTextures();
            ProcessAnimations(api);
            CollectTextures();
            LoadModelReplacements(api);
        }

        ModelsLoaded = true;
        OnCustomModelsLoaded?.Invoke();
    }
    public TextureAtlasPosition? GetAtlasPosition(string modelCode, string textureCode, Entity entity)
    {
        string fullCode = PrefixTextureCode(modelCode, textureCode);

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
        return TextureSource?.AtlasSize;
    }

    public void SynchronizePlayerModel(string code)
    {
        _clientChannel?.SendPacket(new ChangePlayerModelPacket()
        {
            ModelCode = code,
        });
    }
    public void SynchronizePlayerModelSize(float size)
    {
        _clientChannel?.SendPacket(new ChangePlayerModelSizePacket()
        {
            EntitySize = size,
        });
    }

    public static string PrefixTextureCode(string modelCode, string textureCode) => GetTextureCodePrefix(modelCode) + textureCode;
    public static string PrefixSkinPartTextures(string modelCode, string textureCode, string skinCode) => GetSkinPartTexturePrefix(modelCode, skinCode) + textureCode;

    public static string GetTextureCodePrefix(string modelCode) => $"{modelCode.Replace(':', '-')}-base-";
    public static string GetSkinPartTexturePrefix(string modelCode, string skinCode) => $"{modelCode.Replace(':', '-')}-{skinCode}-";


    private const string _defaultModelPath = "game:entity/humanoid/seraph-faceless";
    private const string _defaultMainTextureCode = "seraph";
    private const string _playerEntityCode = "game:player";
    private const string _defaultModelCode = "seraph";

    private const string _modelReplacementsByCodePath = "config/model-replacements-bycode";
    private const string _modelReplacementsByShapePath = "config/model-replacements-byshape";

    private bool _defaultLoaded = false;
    private IClientNetworkChannel? _clientChannel;
    private readonly Dictionary<string, AssetLocation> _textures = [];
    private ICoreClientAPI? _clientApi;
    private ICoreAPI _api;
    private readonly Dictionary<string, string> _oldMainTextureCodes = [];
    private readonly Dictionary<string, Dictionary<string, string>> _wearableModelReplacers = [];

    private void LoadDefault()
    {
        if (_defaultLoaded) return;
        _defaultLoaded = true;

        Shape defaultShape;
        if (_clientApi != null)
        {
            defaultShape = LoadShape(_clientApi, _defaultModelPath) ?? throw new ArgumentException("[Player Model lib] Unable to load default player shape.");
            defaultShape.ResolveReferences(_clientApi.Logger, "PlayerModelLib:CustomModel-default");
        }
        else
        {
            defaultShape = new();
        }

        IAsset defaultConfigAsset = _api.Assets.Get("playermodellib:config/default-model-config.json");
        Dictionary<string, CustomModelConfig> defaultConfigList = FromAsset(defaultConfigAsset);
        if (!defaultConfigList.TryGetValue($"playermodellib:{_defaultModelCode}", out CustomModelConfig defaultConfig))
        {
            defaultConfig = new();
        }

        EntityProperties playerProperties = _api.World.GetEntityType(_playerEntityCode) ?? throw new ArgumentException("[Player Model lib] Unable to get player entity properties.");

        SkinnablePartExtended[] parts = playerProperties.Attributes["skinnableParts"].AsObject<SkinnablePartExtended[]>();

        FixDefaultSkinParts(parts);

        Dictionary<string, SkinnablePart> partsByCode = LoadParts(_api, parts);

        CustomModelData defaultModelData = new(_defaultModelCode, defaultShape)
        {
            SkinParts = partsByCode,
            SkinPartsArray = parts,
            MainTextureCode = PrefixTextureCode(_defaultModelCode, _defaultMainTextureCode),
            CollisionBox = new(playerProperties.CollisionBoxSize.X, playerProperties.CollisionBoxSize.Y),
            EyeHeight = (float)playerProperties.EyeHeight,

            AvailableClasses = [.. defaultConfig.AvailableClasses],
            SkipClasses = [.. defaultConfig.SkipClasses],
            ExclusiveClasses = [.. defaultConfig.ExclusiveClasses],
            ExtraTraits = defaultConfig.ExtraTraits,
            WearableShapeReplacersByShape = defaultConfig.WearableModelReplacersByShape,
            SizeRange = new(defaultConfig.SizeRange[0], defaultConfig.SizeRange[1]),
            MaxCollisionBox = new Vector2(defaultConfig.MaxCollisionBox[0], defaultConfig.MaxCollisionBox[1]),
            MinCollisionBox = new Vector2(defaultConfig.MinCollisionBox[0], defaultConfig.MinCollisionBox[1]),
            ScaleColliderWithSizeHorizontally = defaultConfig.ScaleColliderWithSizeHorizontally,
            ScaleColliderWithSizeVertically = defaultConfig.ScaleColliderWithSizeVertically,
            MaxEyeHeight = defaultConfig.MaxEyeHeight,
            MinEyeHeight = defaultConfig.MinEyeHeight,
            //AddTags = _api.TagRegistry.EntityTagsToTagArray(defaultConfig.AddTags),
            //RemoveTags = _api.TagRegistry.EntityTagsToTagArray(defaultConfig.RemoveTags),
            ModelSizeFactor = defaultConfig.ModelSizeFactor,
            Enabled = defaultConfig.Enabled,
        };

        CustomModels.Add(_defaultModelCode, defaultModelData);

        _oldMainTextureCodes.Add(DefaultModelCode, _defaultMainTextureCode);

        if (_clientApi != null) ProcessAttachmentPoints();
    }
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

        foreach (Dictionary<string, CustomModelConfig> customModelConfigs in modelsConfigs.Select(FromAsset))
        {
            foreach ((string code, CustomModelConfig modelConfig) in customModelConfigs)
            {
                Shape? shape = LoadShape(api, modelConfig.ShapePath);

                if (shape == null)
                {
                    _api?.Logger.Error($"[Player Model lib] Unable to load shape '{modelConfig.ShapePath}' for model '{code}'");
                    continue;
                }

                _wearableModelReplacers.Add(code, modelConfig.WearableModelReplacers);

                Dictionary<string, SkinnablePart> partsByCode = LoadParts(api, modelConfig.SkinnableParts);

                CustomModelData modelData = new(code, shape)
                {
                    SkinParts = partsByCode,
                    SkinPartsArray = modelConfig.SkinnableParts,
                    MainTextureCode = PrefixTextureCode(code, modelConfig.MainTextureCode),
                    AvailableClasses = [.. modelConfig.AvailableClasses],
                    SkipClasses = [.. modelConfig.SkipClasses],
                    ExclusiveClasses = [.. modelConfig.ExclusiveClasses],
                    ExtraTraits = modelConfig.ExtraTraits,
                    WearableShapeReplacersByShape = modelConfig.WearableModelReplacersByShape,
                    CollisionBox = modelConfig.CollisionBox.Length == 0 ? DefaultModelData.CollisionBox : new Vector2(modelConfig.CollisionBox[0], modelConfig.CollisionBox[1]),
                    EyeHeight = modelConfig.EyeHeight,
                    SizeRange = new(modelConfig.SizeRange[0], modelConfig.SizeRange[1]),
                    MaxCollisionBox = new Vector2(modelConfig.MaxCollisionBox[0], modelConfig.MaxCollisionBox[1]),
                    MinCollisionBox = new Vector2(modelConfig.MinCollisionBox[0], modelConfig.MinCollisionBox[1]),
                    ScaleColliderWithSizeHorizontally = modelConfig.ScaleColliderWithSizeHorizontally,
                    ScaleColliderWithSizeVertically = modelConfig.ScaleColliderWithSizeVertically,
                    MaxEyeHeight = modelConfig.MaxEyeHeight,
                    MinEyeHeight = modelConfig.MinEyeHeight,
                    //AddTags = api.TagRegistry.EntityTagsToTagArray(modelConfig.AddTags),
                    //RemoveTags = api.TagRegistry.EntityTagsToTagArray(modelConfig.RemoveTags),
                    ModelSizeFactor = modelConfig.ModelSizeFactor,
                    Enabled = modelConfig.Enabled,
                };

                CustomModels.Add(code, modelData);

                _oldMainTextureCodes.Add(code, modelConfig.MainTextureCode);
            }
        }
    }
    private void LoadModelReplacements(ICoreAPI api)
    {
        foreach ((string modelCode, Dictionary<string, string> paths) in _wearableModelReplacers)
        {
            CustomModels[modelCode].WearableShapeReplacers = [];

            foreach ((string itemCodeWildcard, string path) in paths)
            {
                foreach (Item item in api.World.Items)
                {
                    if (WildcardUtil.Match(itemCodeWildcard, item.Code?.ToString() ?? ""))
                    {
                        CustomModels[modelCode].WearableShapeReplacers.TryAdd(item.Id, path);
                    }
                }
            }
        }

        List<IAsset> modelsConfigs = api.Assets.GetMany(_modelReplacementsByCodePath);
        foreach (IAsset asset in modelsConfigs)
        {
            Dictionary<string, Dictionary<string, string>> replacements = ReplacementsFromAsset(asset);

            foreach ((string modelCode, Dictionary<string, string> paths) in replacements)
            {
                foreach ((string itemCodeWildcard, string path) in paths)
                {
                    foreach (Item item in api.World.Items)
                    {
                        if (WildcardUtil.Match(itemCodeWildcard, item.Code?.ToString() ?? ""))
                        {
                            CustomModels[modelCode].WearableShapeReplacers[item.Id] = path;
                        }
                    }
                }
            }
        }

        modelsConfigs = api.Assets.GetMany(_modelReplacementsByShapePath);
        foreach (IAsset asset in modelsConfigs)
        {
            Dictionary<string, Dictionary<string, string>> replacements = ReplacementsFromAsset(asset);

            foreach ((string modelCode, Dictionary<string, string> paths) in replacements)
            {
                foreach ((string fromPath, string toPath) in paths)
                {
                    CustomModels[modelCode].WearableShapeReplacersByShape[fromPath] = toPath;
                }
            }
        }
    }
    private void ProcessAnimations(ICoreAPI api)
    {
        foreach (Shape customShape in CustomModels.Where(entry => entry.Key != _defaultModelCode).Select(entry => entry.Value.Shape))
        {
            HashSet<string> existingAnimations = [.. customShape.Animations.Select(GetAnimationCode)];

            foreach ((uint crc32, Animation animation) in DefaultModel.AnimationsByCrc32)
            {
                string code = GetAnimationCode(animation);

                if (existingAnimations.Contains(code)) continue;

                existingAnimations.Add(code);
                customShape.Animations = [.. customShape.Animations, animation.Clone()];
            }

            customShape.ResolveReferences(api.Logger, "PlayerModelLib:CustomModel-test");
        }
    }
    private void ProcessAttachmentPoints()
    {
        Dictionary<string, AttachmentPoint[]> attachmentPointsByElement = [];

        foreach (ShapeElement element in DefaultModel.Elements)
        {
            CollectAttachmentPoints(element, attachmentPointsByElement);
        }

        foreach (Shape customShape in CustomModels.Where(entry => entry.Key != _defaultModelCode).Select(entry => entry.Value.Shape))
        {
            foreach (ShapeElement element in customShape.Elements)
            {
                AddAttachmentPoints(element, attachmentPointsByElement);
            }
        }
    }
    private void FixDefaultSkinParts(SkinnablePartExtended[] parts)
    {
        foreach (SkinnablePartExtended part in parts)
        {
            switch (part.Code)
            {
                case "underwear":
                case "baseskin":
                    part.TextureTarget = "seraph";
                    part.OverlayTexture = true;
                    part.OverlayMode = EnumColorBlendMode.Normal;
                    break;

                case "haircolor":
                    part.TargetSkinParts = ["beard", "mustache", "hairextra", "hairbase"];
                    break;

                case "facialexpression":
                    foreach (SkinnablePartVariant variant in part.Variants)
                    {
                        string code = variant.Code;
                        variant.Shape.Base = $"playermodellib:seraphfaces/{code}";
                    }
                    break;

                case "eyecolor":
                    part.TextureTarget = "playermodellib-iris";
                    part.TargetSkinParts = ["facialexpression"];
                    /*foreach (SkinnablePartVariant variant in part.Variants)
                    {
                        string code = variant.Code;
                        variant.Texture = $"playermodellib:eyes/{code}";
                    }*/
                    break;

                default:
                    break;
            }
        }
    }
    private static void CollectAttachmentPoints(ShapeElement element, Dictionary<string, AttachmentPoint[]> attachmentPointsByElement)
    {
        if (element.AttachmentPoints != null && element.AttachmentPoints.Length > 0)
        {
            attachmentPointsByElement[element.Name] = element.AttachmentPoints;
        }

        if (element.Children != null)
        {
            foreach (ShapeElement child in element.Children)
            {
                CollectAttachmentPoints(child, attachmentPointsByElement);
            }
        }
    }
    private static void AddAttachmentPoints(ShapeElement element, Dictionary<string, AttachmentPoint[]> attachmentPointsByElement)
    {
        if (attachmentPointsByElement.TryGetValue(element.Name, out AttachmentPoint[] points))
        {
            if (element.AttachmentPoints != null && element.AttachmentPoints.Length > 0)
            {
                IEnumerable<string> existing = element.AttachmentPoints.Select(point => point.Code);

                element.AttachmentPoints = element.AttachmentPoints.Concat(points.Where(point => !existing.Contains(point.Code))).ToArray();
            }
            else
            {
                element.AttachmentPoints = points;
            }
        }

        if (element.Children != null)
        {
            foreach (ShapeElement child in element.Children)
            {
                AddAttachmentPoints(child, attachmentPointsByElement);
            }
        }
    }
    private string GetAnimationCode(Animation animation)
    {
        if (animation.Code == null || animation.Code.Length == 0)
        {
            return animation.Name.ToLowerInvariant().Replace(" ", "");
        }
        return animation.Code.ToLowerInvariant().Replace(" ", "");
    }
    private void HandleChangePlayerModelPacket(IPlayer player, ChangePlayerModelPacket packet)
    {
        player.Entity.WatchedAttributes.SetString("skinModel", packet.ModelCode);
        player.Entity.WatchedAttributes.SetStringArray("extraTraits", CustomModels[packet.ModelCode].ExtraTraits);

        player.Entity.GetBehavior<PlayerSkinBehavior>()?.UpdateEntityProperties();
    }
    private void HandleChangePlayerModelSizePacket(IPlayer player, ChangePlayerModelSizePacket packet)
    {
        player.Entity.WatchedAttributes.SetFloat("entitySize", packet.EntitySize);

        player.Entity.GetBehavior<PlayerSkinBehavior>()?.UpdateEntityProperties();
    }
    private Dictionary<string, CustomModelConfig> FromAsset(IAsset asset)
    {
        Dictionary<string, CustomModelConfig> result = [];
        string domain = asset.Location.Domain;
        JObject? json;

        try
        {
            json = JsonObject.FromJson(asset.ToText()).Token as JObject;

            if (json == null)
            {
                LoggerUtil.Error(_api, this, $"Error when trying to load model config '{asset.Location}'.");
                return result;
            }
        }
        catch (Exception exception)
        {
            LoggerUtil.Error(_api, this, $"Exception when trying to load model config '{asset.Location}':\n{exception}");
            return result;
        }

        foreach ((string code, JToken? token) in json)
        {
            try
            {
                JsonObject configJson = new(token);
                CustomModelConfig config = configJson.AsObject<CustomModelConfig>();
                config.Domain = domain;
                result.Add($"{domain}:{code}", config);
            }
            catch (Exception exception)
            {
                LoggerUtil.Error(_api, this, $"Exception when trying to load model config '{asset.Location}' for model '{code}':\n{exception}");
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
        Dictionary<string, SkinnablePart> patsByCode = [];

        foreach (SkinnablePart part in parts)
        {
            part.VariantsByCode = [];

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
        foreach ((string modelCode, CustomModelData data) in CustomModels)
        {
            string oldTextureCode = _oldMainTextureCodes[modelCode];
            string newTextureCode = data.MainTextureCode;

            if (data.Shape.Textures.ContainsKey(oldTextureCode))
            {
                AssetLocation texturePath = data.Shape.Textures[oldTextureCode];
                data.Shape.Textures.Remove(oldTextureCode);
                data.Shape.Textures[newTextureCode] = texturePath;
            }

            if (data.Shape.TextureSizes.ContainsKey(oldTextureCode))
            {
                int[] size = data.Shape.TextureSizes[oldTextureCode];
                data.Shape.TextureSizes.Remove(oldTextureCode);
                data.Shape.TextureSizes[newTextureCode] = size;
                data.MainTextureSize = new(size[0], size[1]);
            }

            foreach (ShapeElement element in data.Shape.Elements)
            {
                ProcessShapeElement(element, oldTextureCode, newTextureCode);
            }
        }
    }
    private static void ProcessShapeElement(ShapeElement element, string oldTextureCode, string newTextureCode)
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

        foreach ((string modelCode, CustomModelData data) in CustomModels)
        {
            foreach ((string textureCode, AssetLocation texturePath) in data.Shape.Textures)
            {
                if (textureCode == data.MainTextureCode)
                {
                    if (_textures.ContainsKey(textureCode)) continue;

                    _textures.Add(textureCode, texturePath);
                    _ = TextureSource?[textureCode];

                    IAsset? textureAsset2 = _clientApi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (textureAsset2 == null) continue;

                    _clientApi.EntityTextureAtlas.GetOrInsertTexture(texturePath, out _, out TextureAtlasPosition texPos, () => textureAsset2.ToBitmap(_clientApi));

                    Debug.WriteLine($"CollectTextures - {modelCode} - {textureCode} - {(texPos.x2 - texPos.x1) * 4096}- {(texPos.y2 - texPos.y1) * 4096}");

                    data.MainTexturePosition = texPos;

                    data.MainTexture = new CompositeTexture()
                    {
                        Base = texturePath
                    };

                    data.MainTexture.Bake(_clientApi.Assets);

                    continue;
                }

                string newTextureCode = PrefixTextureCode(modelCode, textureCode);

                if (_textures.ContainsKey(newTextureCode)) continue;

                _textures.Add(newTextureCode, texturePath);
                _ = TextureSource?[newTextureCode];

                IAsset? textureAsset = _clientApi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (textureAsset == null) continue;

                _clientApi.EntityTextureAtlas.GetOrInsertTexture(texturePath, out _, out _, () => textureAsset.ToBitmap(_clientApi));
            }
        }
    }
    private void CollectExclusiveClasses()
    {
        foreach (HashSet<string> classes in CustomModels.Select(entry => entry.Value.ExclusiveClasses))
        {
            ExclusiveClasses.AddRange(classes);
        }
    }
}