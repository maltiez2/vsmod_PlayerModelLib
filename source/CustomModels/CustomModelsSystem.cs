using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;
using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace PlayerModelLib;

public sealed class CustomModelsSystem : ModSystem
{
    public Dictionary<string, CustomModelData> CustomModels { get; } = [];
    public ContainedTextureSource? TextureSource { get; private set; }
    public string DefaultModelCode => CustomModels.Where(entry => entry.Value.Enabled).Select(entry => entry.Key).FirstOrDefault(_defaultModelCode);
    public string DefaultGroupCode => CustomModels.Where(entry => entry.Value.Enabled).Select(entry => entry.Value.Group).FirstOrDefault(_defaultGroupCode);
    public Shape DefaultModel => CustomModels[_defaultModelCode].Shape;
    public CustomModelData DefaultModelData => CustomModels[_defaultModelCode];
    public bool ModelsLoaded { get; private set; } = false;
    public HashSet<string> ExclusiveClasses { get; private set; } = [];
    public Dictionary<string, BaseShapeData> BaseShapesData { get; private set; } = [];
    public bool CanHotLoad => ModelsLoaded;

    public event Action? OnCustomModelsLoaded;
    public event Action? OnCustomModelHotLoaded;
    public event Action<string, IPlayer, PlayerSkinBehavior>? OnCustomModelChanged;

    public override double ExecuteOrder() => 0.21;

    public override void Start(ICoreAPI api)
    {
        RegisterTags(api);
    }
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

        RegisterServerChatCommands(api);
    }
    public override void AssetsLoaded(ICoreAPI api)
    {
        _api = api;
        _clientApi = api as ICoreClientAPI;
    }
    public override void AssetsFinalize(ICoreAPI api)
    {
        LoadBaseShapes(api);
        LoadDefault();
        Load(api);
        CollectExclusiveClasses();

        if (api.Side == EnumAppSide.Client)
        {
            ProcessMainTextures();
            ProcessAnimations(api);
            ProcessAttachmentPoints();
            CollectTextures();
            LoadModelReplacements(api);
        }

        ModelsLoaded = true;
        OnCustomModelsLoaded?.Invoke();
    }
    public TextureAtlasPosition? GetAtlasPosition(string modelCode, string textureCode, Entity entity)
    {
        string fullCode = PrefixTextureCode(modelCode, textureCode);

        if (_textures.ContainsKey(fullCode))
        {
            try
            {
                return TextureSource?[fullCode];
            }
            catch (Exception)
            {
                try
                {
                    return TextureSource?[textureCode];
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        int textureIndex = entity.WatchedAttributes.GetInt("textureIndex");
        try
        {
            ITexPositionSource? source = _clientApi?.Tesselator.GetTextureSource(entity, null, textureIndex);

            return source?[textureCode];
        }
        catch (Exception)
        {
            return null;
        }
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

    public void HotLoadCustomModel(string code, CustomModelConfig modelConfig)
    {
        if (!CanHotLoad)
        {
            throw new InvalidOperationException("[Player Model lib] To early for HotLoadCustomModel");
        }

        if (_api == null) return;

        if (CustomModels.ContainsKey(code))
        {
            return;
        }

        LoadCustomModel(_api, code, modelConfig);
        CollectExclusiveClasses();

        if (_api.Side == EnumAppSide.Client)
        {
            CustomModelData modelData = CustomModels[code];

            ProcessCustomModelMainTextures(code, modelData);
            ProcessCustomModelAnimations(modelData.Shape, code, _api);
            CollectDefaultAddAttachmentPoints(out Dictionary<string, (AttachmentPoint[] points, ShapeElement element, string parent)> attachmentPointsByElement);
            AddAttachmentPointsToCustomModel(modelData.Shape, attachmentPointsByElement, code);
            CollectTexturesForCustomModel(code, modelData);

            if (_wearableModelReplacers.TryGetValue(code, out Dictionary<string, string>? paths))
            {
                LoadCustomModelWearableModelReplacers(_api, code, paths);
            }

            if (_wearableCompositeModelReplacers.TryGetValue(code, out Dictionary<string, CompositeShape>? otherPaths))
            {
                LoadCustomModelWearableCompositeModelReplacers(_api, code, otherPaths);
            }
        }

        OnCustomModelHotLoaded?.Invoke();
    }

    public void CustomModelChanged(string code, IPlayer player, PlayerSkinBehavior behavior)
    {
        OnCustomModelChanged?.Invoke(code, player, behavior);
    }

    public static string PrefixTextureCode(string modelCode, string textureCode) => GetTextureCodePrefix(modelCode) + textureCode;
    public static string PrefixSkinPartTextures(string modelCode, string textureCode, string skinCode) => GetSkinPartTexturePrefix(modelCode, skinCode) + textureCode;

    public static string GetTextureCodePrefix(string modelCode) => $"{modelCode.Replace(':', '-')}-base-";
    public static string GetSkinPartTexturePrefix(string modelCode, string skinCode) => $"{modelCode.Replace(':', '-')}-{skinCode}-";


    private const string _defaultModelPath = "game:entity/humanoid/seraph-faceless";
    private const string _defaultMainTextureCode = "seraph";
    private const string _playerEntityCode = "game:player";
    private const string _defaultModelCode = "seraph";
    private const string _defaultGroupCode = "temporal";
    private const string _extraCustomModelsAttribute = "extraCustomModels";
    private const string _modelReplacementsByCodePath = "config/model-replacements-bycode";
    private const string _modelReplacementsByShapePath = "config/model-replacements-byshape";
    private const string _compositeModelReplacementsByCodePath = "config/composite-model-replacements-bycode";

    private bool _defaultLoaded = false;
    private IClientNetworkChannel? _clientChannel;
    private readonly Dictionary<string, AssetLocation> _textures = [];
    private ICoreClientAPI? _clientApi;
    private ICoreAPI? _api;
    private readonly Dictionary<string, string[]> _oldMainTextureCodes = [];
    private readonly Dictionary<string, Dictionary<string, string>> _wearableModelReplacers = [];
    private readonly Dictionary<string, Dictionary<string, CompositeShape>> _wearableCompositeModelReplacers = [];

    private void LoadDefault()
    {
        if (_api == null) return;
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
        if (!defaultConfigList.TryGetValue($"playermodellib:{_defaultModelCode}", out CustomModelConfig? defaultConfig))
        {
            defaultConfig = new();
        }

        EntityProperties playerProperties = _api.World.GetEntityType(_playerEntityCode) ?? throw new ArgumentException("[Player Model lib] Unable to get player entity properties.");

        SkinnablePartExtended[] parts = [.. playerProperties.Attributes["skinnableParts"].AsObject<SkinnablePartExtended[]>().Where(part => part.Enabled)];

        FixDefaultSkinParts(parts);

        Dictionary<string, SkinnablePart> partsByCode = LoadParts(_api, parts, _defaultModelCode);

        FixColBreak(parts, _defaultModelCode);

        _ = _api.ModLoader.GetModSystem<ModSystemSkinnableAdditions>().AppendAdditions(parts);

        CheckTexturePartTextureCodes(partsByCode.Values, defaultShape, _defaultModelCode);



        CustomModelData defaultModelData = new(_defaultModelCode, defaultShape)
        {
            SkinParts = partsByCode,
            SkinPartsArray = parts,
            MainTextureCodes = [PrefixTextureCode(_defaultModelCode, _defaultMainTextureCode)],
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
            AddTags = _api.TagRegistry.EntityTagsToTagArray(defaultConfig.AddTags),
            RemoveTags = _api.TagRegistry.EntityTagsToTagArray(defaultConfig.RemoveTags),
            ModelSizeFactor = defaultConfig.ModelSizeFactor,
            HeadBobbingScale = defaultConfig.HeadBobbingScale,
            GuiModelScale = defaultConfig.GuiModelScale,
            Enabled = defaultConfig.Enabled,
            Group = _defaultGroupCode,
            Icon = new("playermodellib:textures/icons/seraph.png"),
            GroupIcon = new("playermodellib:textures/icons/temporal.png"),
            WalkEyeHeightMultiplier = defaultConfig.WalkEyeHeightMultiplier,
            SprintEyeHeightMultiplier = defaultConfig.SprintEyeHeightMultiplier,
            SneakEyeHeightMultiplier = defaultConfig.SneakEyeHeightMultiplier,
            StepHeight = defaultConfig.StepHeight,
            MaxOxygenFactor = defaultConfig.MaxOxygenFactor
        };

        CustomModels.Add(_defaultModelCode, defaultModelData);
        _oldMainTextureCodes.Add(_defaultModelCode, [_defaultMainTextureCode]);
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
                try
                {
                    LoadCustomModel(api, code, modelConfig);
                }
                catch (Exception exception)
                {
                    LoggerUtil.Error(api, this, $"Error on loading model '{code}': {exception}");
                }
            }
        }
    }
    private void LoadCustomModel(ICoreAPI api, string code, CustomModelConfig modelConfig)
    {
        Shape? shape = LoadShape(api, modelConfig.ShapePath);

        if (shape == null)
        {
            LoggerUtil.Error(_api, this, $"({code}) Unable to load shape '{modelConfig.ShapePath}'");
            return;
        }

        if (shape.Textures == null)
        {
            LoggerUtil.Error(_api, this, $"({code}) Shape '{modelConfig.ShapePath}' does not have textures list defined");
            return;
        }

        if (!shape.Textures.Any())
        {
            LoggerUtil.Error(_api, this, $"({code}) Shape '{modelConfig.ShapePath}' does not have any textures specified, will skip the model.");
            return;
        }

        IEnumerable<string> mainTextures = shape.Textures.Select(entry => entry.Key);

        foreach ((string textureCode, AssetLocation? texturePath) in shape.Textures)
        {
            if (texturePath == null) continue;

            if (!texturePath.HasDomain())
            {
                texturePath.Domain = new AssetLocation(modelConfig.ShapePath).Domain;
            }

            AssetLocation path = texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png");

            if (!api.Assets.Exists(path) && api.Side == EnumAppSide.Client)
            {
                LoggerUtil.Error(_api, this, $"({code}) Shape '{modelConfig.ShapePath}' has texture with code '{textureCode}' and path '{texturePath}'. This texture was not found in assets, will skip loading this model.");
                return;
            }
        }

        modelConfig.SkinnableParts = [.. modelConfig.SkinnableParts.Where(part => part.Enabled)];

        Dictionary<string, SkinnablePart> partsByCode = LoadParts(api, modelConfig.SkinnableParts, code);

        FixColBreak(modelConfig.SkinnableParts, code);

        CheckTexturePartTextureCodes(partsByCode.Values, shape, code);

        CustomModelData modelData = new(code, shape)
        {
            Enabled = modelConfig.Enabled,
            Name = modelConfig.Name,
            SkinParts = partsByCode,
            SkinPartsArray = modelConfig.SkinnableParts,
            MainTextureCodes = mainTextures.Select(textureCode => PrefixTextureCode(code, textureCode)).ToArray(),
            AvailableClasses = [.. modelConfig.AvailableClasses],
            SkipClasses = [.. modelConfig.SkipClasses],
            ExclusiveClasses = [.. modelConfig.ExclusiveClasses],
            ExtraTraits = modelConfig.ExtraTraits,
            WearableShapeReplacersByShape = modelConfig.WearableModelReplacersByShape,
            CollisionBox = modelConfig.CollisionBox.Length == 0 ? CustomModels[_defaultModelCode].CollisionBox : new Vector2(modelConfig.CollisionBox[0], modelConfig.CollisionBox[1]),
            EyeHeight = modelConfig.EyeHeight,
            SizeRange = new(modelConfig.SizeRange[0], modelConfig.SizeRange[1]),
            MaxCollisionBox = new Vector2(modelConfig.MaxCollisionBox[0], modelConfig.MaxCollisionBox[1]),
            MinCollisionBox = new Vector2(modelConfig.MinCollisionBox[0], modelConfig.MinCollisionBox[1]),
            ScaleColliderWithSizeHorizontally = modelConfig.ScaleColliderWithSizeHorizontally,
            ScaleColliderWithSizeVertically = modelConfig.ScaleColliderWithSizeVertically,
            MaxEyeHeight = modelConfig.MaxEyeHeight,
            MinEyeHeight = modelConfig.MinEyeHeight,
            AddTags = api.TagRegistry.EntityTagsToTagArray(modelConfig.AddTags),
            RemoveTags = api.TagRegistry.EntityTagsToTagArray(modelConfig.RemoveTags),
            ModelSizeFactor = modelConfig.ModelSizeFactor,
            HeadBobbingScale = modelConfig.HeadBobbingScale,
            GuiModelScale = modelConfig.GuiModelScale,
            BaseShapeCode = modelConfig.BaseShapeCode,
            WalkEyeHeightMultiplier = modelConfig.WalkEyeHeightMultiplier,
            SprintEyeHeightMultiplier = modelConfig.SprintEyeHeightMultiplier,
            SneakEyeHeightMultiplier = modelConfig.SneakEyeHeightMultiplier,
            StepHeight = modelConfig.StepHeight,
            MaxOxygenFactor = modelConfig.MaxOxygenFactor
        };

        AssetLocation icon = new AssetLocation(modelConfig.Icon).WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png");
        if (api.Assets.Exists(icon))
        {
            modelData.Icon = icon;
        }
        else
        {
            LoggerUtil.Warn(_api, this, $"({code}) Model icon by path '{icon}' does not exists");
            modelData.Icon = null;
        }

        AssetLocation groupIcon = new AssetLocation(modelConfig.GroupIcon).WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png");
        if (api.Assets.Exists(groupIcon))
        {
            modelData.GroupIcon = groupIcon;
        }
        else
        {
            LoggerUtil.Verbose(_api, this, $"({code}) Group icon by path '{icon}' does not exists");
            modelData.GroupIcon = null;
        }

        if (modelConfig.Group != "")
        {
            modelData.Group = modelConfig.Group;
        }
        else
        {
            modelData.Group = code;
        }

        if (BaseShapesData.TryGetValue(modelData.BaseShapeCode, out BaseShapeData? baseShapeData))
        {
            CollectBaseShapeElements(shape.Elements, baseShapeData.ElementSizes.Keys.ToArray(), modelData.ElementSizes);
        }

        _wearableModelReplacers.Add(code, modelConfig.WearableModelReplacers);
        _wearableCompositeModelReplacers.Add(code, modelConfig.WearableCompositeModelReplacers);

        CustomModels.Add(code, modelData);
        _oldMainTextureCodes.Add(code, mainTextures.ToArray());
    }
    private void LoadModelReplacements(ICoreAPI api)
    {
        foreach ((string modelCode, Dictionary<string, string> paths) in _wearableModelReplacers)
        {
            LoadCustomModelWearableModelReplacers(api, modelCode, paths);
        }

        foreach ((string modelCode, Dictionary<string, CompositeShape> paths) in _wearableCompositeModelReplacers)
        {
            LoadCustomModelWearableCompositeModelReplacers(api, modelCode, paths);
        }

        List<IAsset> modelsConfigs = api.Assets.GetMany(_compositeModelReplacementsByCodePath);
        foreach (IAsset asset in modelsConfigs)
        {
            Dictionary<string, Dictionary<string, CompositeShape>> replacements = CompositeReplacementsFromAsset(asset);

            foreach ((string modelCodeExpression, Dictionary<string, CompositeShape> paths) in replacements)
            {
                string[] modelCodes = modelCodeExpression.Split('|');
                foreach (string modelCode in modelCodes)
                {
                    if (!CustomModels.ContainsKey(modelCode))
                    {
                        LoggerUtil.Error(_api, this, $"Error while loading wearable composite model replacements by code: custom model with code '{modelCode}' does not exists.");
                        continue;
                    }

                    foreach ((string itemCodeWildcard, CompositeShape path) in paths)
                    {
                        foreach (Item item in api.World.Items)
                        {
                            if (!WildcardUtil.Match(itemCodeWildcard, item.Code?.ToString() ?? "")) continue;

                            ReplaceVariants(path, item);

                            CustomModels[modelCode].WearableCompositeShapeReplacers[item.Id] = path;
                        }
                    }
                }
            }
        }

        modelsConfigs = api.Assets.GetMany(_modelReplacementsByCodePath);
        foreach (IAsset asset in modelsConfigs)
        {
            Dictionary<string, Dictionary<string, string>> replacements = ReplacementsFromAsset(asset);

            foreach ((string modelCodeExpression, Dictionary<string, string> paths) in replacements)
            {
                string[] modelCodes = modelCodeExpression.Split('|');
                foreach (string modelCode in modelCodes)
                {
                    if (!CustomModels.ContainsKey(modelCode))
                    {
                        LoggerUtil.Error(_api, this, $"Error while loading wearable model replacements by code: custom model with code '{modelCode}' does not exists.");
                        continue;
                    }

                    foreach ((string itemCodeWildcard, string path) in paths)
                    {
                        foreach (Item item in api.World.Items)
                        {
                            if (!WildcardUtil.Match(itemCodeWildcard, item.Code?.ToString() ?? "")) continue;

                            string processedPath = path;

                            foreach ((string variantCode, string variantValue) in item.Variant)
                            {
                                processedPath = processedPath.Replace($"{{{variantCode}}}", variantValue);
                            }

                            if (api.Assets.Exists(GetShapeLocation(processedPath)))
                            {
                                CustomModels[modelCode].WearableShapeReplacers.TryAdd(item.Id, processedPath);
                            }
                            else
                            {
                                LoggerUtil.Error(_api, this, $"Shape '{processedPath}' that replaces shape for item '{item.Code}' for model '{modelCode}' was not found, skipping.");
                            }
                        }
                    }
                }
            }
        }

        modelsConfigs = api.Assets.GetMany(_modelReplacementsByShapePath);
        foreach (IAsset asset in modelsConfigs)
        {
            Dictionary<string, Dictionary<string, string>> replacements = ReplacementsFromAsset(asset);

            foreach ((string modelCodeExpression, Dictionary<string, string> paths) in replacements)
            {
                string[] modelCodes = modelCodeExpression.Split('|');
                foreach (string modelCode in modelCodes)
                {
                    if (!CustomModels.ContainsKey(modelCode))
                    {
                        LoggerUtil.Error(_api, this, $"Error while loading wearable model replacements by shape: custom model with code '{modelCode}' does not exists.");
                        continue;
                    }

                    foreach ((string fromPath, string toPath) in paths)
                    {
                        if (api.Assets.Exists(GetShapeLocation(toPath)))
                        {
                            CustomModels[modelCode].WearableShapeReplacersByShape[fromPath] = toPath;
                        }
                        else
                        {
                            LoggerUtil.Error(_api, this, $"Shape '{toPath}' that replaces shape '{fromPath}' for model '{modelCode}' was not found, skipping.");
                        }
                    }
                }
            }
        }
    }
    private void LoadCustomModelWearableModelReplacers(ICoreAPI api, string modelCode, Dictionary<string, string> paths)
    {
        if (!CustomModels.ContainsKey(modelCode))
        {
            LoggerUtil.Error(_api, this, $"Error while loading wearable model replacements by shape: custom model with code '{modelCode}' does not exists.");
            return;
        }

        CustomModels[modelCode].WearableShapeReplacers = [];

        foreach ((string itemCodeWildcard, string path) in paths)
        {
            foreach (Item item in api.World.Items)
            {
                if (!WildcardUtil.Match(itemCodeWildcard, item.Code?.ToString() ?? "")) continue;

                string processedPath = path;

                foreach ((string variantCode, string variantValue) in item.Variant)
                {
                    processedPath = processedPath.Replace($"{variantCode}", variantValue);
                }

                if (api.Assets.Exists(GetShapeLocation(processedPath)))
                {
                    CustomModels[modelCode].WearableShapeReplacers.TryAdd(item.Id, processedPath);
                }
                else
                {
                    LoggerUtil.Error(_api, this, $"Shape '{processedPath}' that replaces shape for item '{item.Code}' for model '{modelCode}' was not found, skipping.");
                }
            }
        }
    }
    private void LoadCustomModelWearableCompositeModelReplacers(ICoreAPI api, string modelCode, Dictionary<string, CompositeShape> paths)
    {
        if (!CustomModels.ContainsKey(modelCode))
        {
            LoggerUtil.Error(_api, this, $"Error while loading wearable composite model replacements by shape: custom model with code '{modelCode}' does not exists.");
            return;
        }

        CustomModels[modelCode].WearableShapeReplacers = [];

        foreach ((string itemCodeWildcard, CompositeShape path) in paths)
        {
            foreach (Item item in api.World.Items)
            {
                if (!WildcardUtil.Match(itemCodeWildcard, item.Code?.ToString() ?? "")) continue;

                CustomModels[modelCode].WearableCompositeShapeReplacers[item.Id] = path;
            }
        }
    }
    private void LoadBaseShapes(ICoreAPI api)
    {
        List<IAsset> modelsConfigs = api.Assets.GetManyInCategory("config", "baseshapes");

        foreach (Dictionary<string, BaseShapeDataJson> baseShapesDataJsons in modelsConfigs.Select(BaseShapesFromAsset))
        {
            foreach ((string code, BaseShapeDataJson baseShapeDataJson) in baseShapesDataJsons)
            {
                try
                {
                    LoadBaseShape(api, code, baseShapeDataJson);
                }
                catch (Exception exception)
                {
                    LoggerUtil.Error(api, this, $"Error on loading base shape '{code}': {exception}");
                }
            }
        }
    }
    private void RegisterTags(ICoreAPI api)
    {
        List<IAsset> modelsConfigs = api.Assets.GetManyInCategory("config", "customplayermodels");

        foreach (Dictionary<string, CustomModelConfig> customModelConfigs in modelsConfigs.Select(FromAsset))
        {
            foreach ((_, CustomModelConfig modelConfig) in customModelConfigs)
            {
                api.TagRegistry.RegisterEntityTags(modelConfig.AddTags);
            }
        }
    }

    private AssetLocation GetShapeLocation(string path) => new AssetLocation(path).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
    private void ProcessAnimations(ICoreAPI api)
    {
        foreach ((Shape customShape, string code) in CustomModels.Where(entry => entry.Key != _defaultModelCode).Select(entry => (entry.Value.Shape, entry.Value.Code)))
        {
            ProcessCustomModelAnimations(customShape, code, api);
        }
    }
    private void ProcessCustomModelAnimations(Shape customShape, string modelCode, ICoreAPI api)
    {
        if (customShape.Animations == null)
        {
            customShape.Animations = [];
        }

        HashSet<string> existingAnimations = [.. customShape.Animations.Select(GetAnimationCode)];

        foreach ((uint crc32, Animation animation) in CustomModels[_defaultModelCode].Shape.AnimationsByCrc32)
        {
            string code = GetAnimationCode(animation);

            if (existingAnimations.Contains(code)) continue;

            existingAnimations.Add(code);
            customShape.Animations = [.. customShape.Animations, animation.Clone()];
        }

        HashSet<string> removedElements = [];
        CollectExistingShapeElementNames(customShape, out HashSet<string> existingElements);
        foreach (Animation? animation in customShape.Animations)
        {
            if (animation == null) continue;

            RemoveNotExistingShapeElements(animation, existingElements, out HashSet<string> removed);

            removedElements.AddRange(removed);
        }

        if (removedElements.Count > 0)
        {
            string removedList = removedElements.Aggregate((f, s) => $"{f}, {s}");
            LoggerUtil.Verbose(_api, this, $"For model '{modelCode}' removed {removedElements.Count} not existing elements from animations: {removedList}");
        }

        customShape.ResolveReferences(api.Logger, "PlayerModelLib:CustomModel-ProcessAnimations");
    }
    private void ProcessAttachmentPoints()
    {
        CollectDefaultAddAttachmentPoints(out Dictionary<string, (AttachmentPoint[] points, ShapeElement element, string parent)> attachmentPointsByElement);

        foreach ((Shape customShape, string code) in CustomModels.Where(entry => entry.Key != _defaultModelCode).Select(entry => (entry.Value.Shape, entry.Value.Code)))
        {
            AddAttachmentPointsToCustomModel(customShape, attachmentPointsByElement, code);
        }
    }
    private void RemoveNotExistingShapeElements(Animation animation, HashSet<string> existing, out HashSet<string> removed)
    {
        removed = [];

        foreach (AnimationKeyFrame? frame in animation.KeyFrames)
        {
            if (frame == null || frame.Elements == null) continue;

            IEnumerable<string> extraElements = frame.Elements.Select(entry => entry.Key).Where(key => !existing.Contains(key));

            removed.AddRange(extraElements);

            foreach (string element in extraElements)
            {
                frame.Elements.Remove(element);
            }
        }
    }
    private void CollectExistingShapeElementNames(Shape customShape, out HashSet<string> names)
    {
        names = [];
        if (customShape.Elements != null)
        {
            foreach (ShapeElement? element in customShape.Elements)
            {
                if (element != null)
                {
                    CollectExistingShapeElementNamesRecursive(element, ref names);
                }
            }
        }
    }
    private void CollectExistingShapeElementNamesRecursive(ShapeElement element, ref HashSet<string> names)
    {
        if (element.Name != null)
        {
            names.Add(element.Name);
        }

        if (element.Children != null)
        {
            foreach (ShapeElement? child in element.Children)
            {
                if (child != null)
                {
                    CollectExistingShapeElementNamesRecursive(child, ref names);
                }
            }
        }
    }
    private void CollectDefaultAddAttachmentPoints(out Dictionary<string, (AttachmentPoint[] points, ShapeElement element, string parent)> attachmentPointsByElement)
    {
        attachmentPointsByElement = [];
        foreach (ShapeElement element in CustomModels[_defaultModelCode].Shape.Elements)
        {
            CollectAttachmentPoints(element, element, attachmentPointsByElement);
        }
    }
    private void AddAttachmentPointsToCustomModel(Shape customShape, Dictionary<string, (AttachmentPoint[] points, ShapeElement element, string parent)> attachmentPointsByElement, string modelCode)
    {
        foreach (ShapeElement element in customShape.Elements)
        {
            AddAttachmentPoints(element, attachmentPointsByElement, modelCode);
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
                    break;

                default:
                    break;
            }
        }
    }
    private void FixColBreak(SkinnablePartExtended[] parts, string modelCodeForLogging)
    {
        if (parts.Count(skinPart => skinPart.Colbreak) == 1) return;

        LoggerUtil.Warn(_api, this, $"Model '{modelCodeForLogging}' has no 'calBreak: true' specified, or has specified it more than once. Will automatically reassign 'calBreak' values.");

        int middleIndex = (parts.Length - 1) / 2;
        for (int index = 0; index < parts.Length; index++)
        {
            parts[index].Colbreak = (index == middleIndex);
        }
    }
    private void CollectAttachmentPoints(ShapeElement element, ShapeElement parent, Dictionary<string, (AttachmentPoint[] points, ShapeElement element, string parent)> attachmentPointsByElement)
    {
        if (element.AttachmentPoints != null && element.AttachmentPoints.Length > 0)
        {
            attachmentPointsByElement[element.Name] = (element.AttachmentPoints, element, parent.Name);
        }

        if (element.Children != null)
        {
            foreach (ShapeElement child in element.Children)
            {
                CollectAttachmentPoints(child, element, attachmentPointsByElement);
            }
        }
    }
    private void AddAttachmentPoints(ShapeElement element, Dictionary<string, (AttachmentPoint[] points, ShapeElement element, string parent)> attachmentPointsByElement, string modelCode)
    {
        foreach ((string elementName, (AttachmentPoint[] pointsData, ShapeElement elementData, string parentName)) in attachmentPointsByElement)
        {
            if (element.Name == elementName)
            {
                if (element.AttachmentPoints != null && element.AttachmentPoints.Length > 0)
                {
                    IEnumerable<string> existing = element.AttachmentPoints.Select(point => point.Code);

                    IEnumerable<string> newPoints = pointsData.Where(point => !existing.Contains(point.Code)).Select(point => point.Code);
                    if (newPoints.Any())
                    {
                        string pointsList = newPoints.Aggregate((f, s) => $"{f}, {s}");
                        LoggerUtil.Verbose(_api, this, $"Adding attachment points to model '{modelCode}' into element '{elementName}': {pointsList}");
                    }

                    element.AttachmentPoints = [.. element.AttachmentPoints, .. pointsData.Where(point => !existing.Contains(point.Code))];
                }
                else
                {
                    IEnumerable<string> newPoints = pointsData.Select(point => point.Code);
                    if (newPoints.Any())
                    {
                        string pointsList = newPoints.Aggregate((f, s) => $"{f}, {s}");
                        LoggerUtil.Verbose(_api, this, $"Adding attachment points to model '{modelCode}' into element '{elementName}': {pointsList}");
                    }

                    element.AttachmentPoints = pointsData;
                }
            }

            if (element.Name == parentName && (element.Children == null || !element.Children.Select(child => child?.Name ?? "").Contains(elementName)))
            {
                if (element.Children == null)
                {
                    element.Children = [];
                }

                IEnumerable<string> newPoints = pointsData.Select(point => point.Code);
                if (newPoints.Any())
                {
                    string pointsList = newPoints.Aggregate((f, s) => $"{f}, {s}");
                    LoggerUtil.Verbose(_api, this, $"Adding missing shape element '{elementName}' to model '{modelCode}' into element '{parentName}' for adding attachments point");
                    LoggerUtil.Verbose(_api, this, $"Adding attachment points to model '{modelCode}' into element '{elementName}': {pointsList}");
                }

                ShapeElement newShapeElement = elementData.Clone();
                newShapeElement.Children = [];
                newShapeElement.ParentElement = element;

                element.Children = element.Children.Append(newShapeElement);
            }
        }

        if (element.Children != null)
        {
            foreach (ShapeElement child in element.Children)
            {
                AddAttachmentPoints(child, attachmentPointsByElement, modelCode);
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
            string text = asset.ToText();
            json = JsonObject.FromJson(text).Token as JObject;

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
            LoggerUtil.Error(_api, this, $"Failed to get model replacements from '{asset.Location}'. Exception: {exception}");
            return [];
        }
    }
    private Dictionary<string, Dictionary<string, CompositeShape>> CompositeReplacementsFromAsset(IAsset asset)
    {
        try
        {
            return JsonObject.FromJson(asset.ToText()).AsObject<Dictionary<string, Dictionary<string, CompositeShape>>>();
        }
        catch (Exception exception)
        {
            LoggerUtil.Error(_api, this, $"Failed to get composite model replacements from '{asset.Location}'. Exception: {exception}");
            return [];
        }
    }
    private Dictionary<string, BaseShapeDataJson> BaseShapesFromAsset(IAsset asset)
    {
        Dictionary<string, BaseShapeDataJson> result = [];
        string domain = asset.Location.Domain;
        JObject? json;

        try
        {
            string text = asset.ToText();
            json = JsonObject.FromJson(text).Token as JObject;

            if (json == null)
            {
                LoggerUtil.Error(_api, this, $"Error when trying to load base shape config '{asset.Location}'.");
                return result;
            }
        }
        catch (Exception exception)
        {
            LoggerUtil.Error(_api, this, $"Exception when trying to load base shape config '{asset.Location}':\n{exception}");
            return result;
        }

        foreach ((string code, JToken? token) in json)
        {
            try
            {
                JsonObject configJson = new(token);
                BaseShapeDataJson config = configJson.AsObject<BaseShapeDataJson>();
                config.Domain = domain;
                result.Add($"{domain}:{code}", config);
            }
            catch (Exception exception)
            {
                LoggerUtil.Error(_api, this, $"Exception when trying to load base shape config '{asset.Location}' for base shape '{code}':\n{exception}");
            }
        }

        return result;
    }
    private void ReplaceVariants(CompositeShape shape, Item item)
    {
        string processedPath = shape.Base;

        foreach ((string variantCode, string variantValue) in item.Variant)
        {
            processedPath = processedPath.Replace($"{{{variantCode}}}", variantValue);
        }

        shape.Base = processedPath;

        if (shape.Overlays != null)
        {
            foreach (CompositeShape? overlay in shape.Overlays)
            {
                if (overlay != null)
                {
                    ReplaceVariants(overlay, item);
                }
            }
        }
    }
    private Dictionary<string, SkinnablePart> LoadParts(ICoreAPI api, SkinnablePartExtended[] parts, string model)
    {
        Dictionary<string, SkinnablePart> patsByCode = [];

        foreach (SkinnablePartExtended part in parts)
        {
            if (part.Code == null)
            {
                LoggerUtil.Error(_api, this, $"Skin part for model '{model}' does not have code specified, skipping.");
                continue;
            }

            part.VariantsByCode = [];

            patsByCode[part.Code] = part;

            if (part.Type == EnumSkinnableType.Texture && api is ICoreClientAPI clientApi)
            {
                IEnumerable<string> additionalTargets = parts
                    .Where(element => element.Type == EnumSkinnableType.Shape)
                    .Where(element => element.TargetSkinParts.Contains(part.Code))
                    .Select(element => element.Code);

                if (additionalTargets.Any() && part.TargetSkinParts.Length == 0)
                {
                    additionalTargets = additionalTargets.Append("base");
                }

                part.TargetSkinParts = part.TargetSkinParts
                    .Concat(additionalTargets)
                    .Distinct()
                    .ToArray();

                ProcessTexturePart(clientApi, part, model);
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
    private void LoadBaseShape(ICoreAPI api, string code, BaseShapeDataJson json)
    {
        Shape? shape = LoadShape(api, json.ShapePath);

        if (shape == null)
        {
            LoggerUtil.Error(api, this, $"Error while loading base shape '{code}': shape '{json.ShapePath}' does not exists.");
            return;
        }

        BaseShapeData result = new()
        {
            Code = code,
            WearableModelReplacers = json.WearableModelReplacers,
            WearableCompositeModelReplacers = json.WearableCompositeModelReplacers,
            WearableModelReplacersByShape = json.WearableModelReplacersByShape
        };

        CollectBaseShapeElements(shape.Elements, json.KeyElements, result.ElementSizes);

        if (BaseShapesData.ContainsKey(code))
        {
            LoggerUtil.Error(api, this, $"Error while loading base shape '{code}': such base shape is already loaded.");
            return;
        }

        BaseShapesData.Add(code, result);
    }

    private void RegisterServerChatCommands(ICoreServerAPI api)
    {
        IChatCommandApi? chatCommandApi = api?.ChatCommands;
        CommandArgumentParsers? chatCommandParser = api?.ChatCommands.Parsers;

        chatCommandApi?
            .GetOrCreate("player")
            .BeginSub("enablePlayerModel")
                .WithAlias("epm")
                .RequiresPrivilege(Privilege.grantrevoke)
                .WithDesc("Allow selection of specified player model even if it is disabled")
                .WithArgs(chatCommandParser?.Word("code"))
                .HandleWith((args) => CmdPlayer.Each(args, HandleEnablePlayerModel))
            .EndSub()
            .BeginSub("disablePlayerModel")
                .WithAlias("dpm")
                .RequiresPrivilege(Privilege.grantrevoke)
                .WithDesc("Removes player model from list of allowed models.")
                .WithArgs(chatCommandParser?.Word("code"))
                .HandleWith((args) => CmdPlayer.Each(args, HandleDisablePlayerModel))
            .EndSub();
    }
    private TextCommandResult HandleEnablePlayerModel(PlayerUidName targetPlayer, TextCommandCallingArgs args)
    {
        ICoreServerAPI api = _api as ICoreServerAPI ?? throw new ArgumentException("'enablePlayerModel' should be run on server side");
        ServerMain server = api.World as ServerMain ?? throw new ArgumentException("Hm... It seems 'api.World' is not 'ServerMain'");

        IWorldPlayerData? playerData = server.GetWorldPlayerData(targetPlayer.Uid);

        if (playerData == null)
        {
            return TextCommandResult.Error(Lang.Get("Only works for players that have connected to your server at least once"));
        }

        string[] extraCustomModels = playerData.EntityPlayer?.WatchedAttributes?.GetStringArray(_extraCustomModelsAttribute, []) ?? [];
        string playerModelCode = (string?)args[1] ?? "";

        if (!CustomModels.ContainsKey(playerModelCode))
        {
            string existingModels = CustomModels.Keys.Aggregate((a, b) => $"{a}, {b}");
            return TextCommandResult.Error($"Model '{playerModelCode}' does not exists. Existing models: {existingModels}");
        }

        if (extraCustomModels.Contains(playerModelCode))
        {
            return TextCommandResult.Error($"Player '{targetPlayer.Name}' can already select '{playerModelCode}'");
        }

        extraCustomModels = extraCustomModels.Append(playerModelCode).Distinct().ToArray();

        playerData.EntityPlayer?.WatchedAttributes?.SetStringArray(_extraCustomModelsAttribute, extraCustomModels);
        playerData.EntityPlayer?.WatchedAttributes?.MarkPathDirty(_extraCustomModelsAttribute);

        return TextCommandResult.Success($"Player '{targetPlayer.Name}' now has access to '{playerModelCode}' player model");
    }
    private TextCommandResult HandleDisablePlayerModel(PlayerUidName targetPlayer, TextCommandCallingArgs args)
    {
        ICoreServerAPI api = _api as ICoreServerAPI ?? throw new ArgumentException("'enablePlayerModel' should be run on server side");
        ServerMain server = api.World as ServerMain ?? throw new ArgumentException("Hm... It seems 'api.World' is not 'ServerMain'");

        IWorldPlayerData? playerData = server.GetWorldPlayerData(targetPlayer.Uid);

        if (playerData == null)
        {
            return TextCommandResult.Error("Only works for players that have connected to your server at least once");
        }

        string[] extraCustomModels = playerData.EntityPlayer?.WatchedAttributes?.GetStringArray(_extraCustomModelsAttribute, []) ?? [];
        string playerModelCode = (string?)args[1] ?? "";

        if (!extraCustomModels.Contains(playerModelCode))
        {
            return TextCommandResult.Error($"Player '{targetPlayer.Name}' already cannot select '{playerModelCode}'");
        }

        extraCustomModels = extraCustomModels.Remove(playerModelCode).Distinct().ToArray();

        playerData.EntityPlayer?.WatchedAttributes?.SetStringArray(_extraCustomModelsAttribute, extraCustomModels);
        playerData.EntityPlayer?.WatchedAttributes?.MarkPathDirty(_extraCustomModelsAttribute);

        return TextCommandResult.Success($"Player '{targetPlayer.Name}' now does not have access to '{playerModelCode}' player model");
    }

    private void CheckTexturePartTextureCodes(IEnumerable<SkinnablePart> allParts, Shape modelShape, string modelCode)
    {
        return; // Cannot do such check, because have to load shape skin parts to be able to check

        foreach (SkinnablePart part in allParts.Where(part => part.Type == EnumSkinnableType.Texture))
        {
            string? target = part.TextureTarget;
            if (target == null)
            {
                LoggerUtil.Warn(_api, this, $"({modelCode}) Texture skin part '{part.Code}' has no 'TextureTarget' specified");
                continue;
            }

            if (modelShape.Textures != null && !modelShape.Textures.ContainsKey(target))
            {
                string existingCodes = "";
                if (modelShape.Textures.Count > 0)
                {
                    existingCodes = modelShape.Textures.Keys.Aggregate((f, s) => $"{f}, {s}");
                }
                LoggerUtil.Warn(_api, this, $"({modelCode}) Texture skin part '{part.Code}' has texture target '{target}' but model shape does not contain textures with such code, existing textures: {existingCodes}");
            }
        }
    }
    private void ProcessTexturePart(ICoreClientAPI clientApi, SkinnablePart part, string model)
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

            IAsset? asset = clientApi.Assets.TryGet(textureLoc.Clone().WithPathAppendixOnce(".png").WithPathPrefixOnce("textures/"), true);

            if (asset == null)
            {
                LoggerUtil.Error(clientApi, this, $"(model: {model}) Texture '{textureLoc}' not found for skin part '{part.Code}' and variant '{variant.Code}'.");

                throw new ArgumentException($"[Player Model lib] (model: {model}) Texture '{textureLoc}' not found for skin part '{part.Code}' and variant '{variant.Code}'.");
            }

            if (variant.Color != 0)
            {
                int b = variant.Color % 1000;
                int g = variant.Color / 1000 % 1000;
                int r = variant.Color / 1000000 % 1000;

#pragma warning disable S2234 // Thanks Tyron for consistency and ease of use of vanilla API!
                variant.Color = ColorUtil.ColorFromRgba(b, g, r, 255);
#pragma warning restore S2234
            }
            else
            {
                int r = 0;
                int g = 0;
                int b = 0;
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
            }

            part.VariantsByCode[variant.Code] = variant;
        }
    }
    private void ProcessMainTextures()
    {
        foreach ((string modelCode, CustomModelData data) in CustomModels)
        {
            ProcessCustomModelMainTextures(modelCode, data);
        }
    }
    private void ProcessCustomModelMainTextures(string modelCode, CustomModelData data)
    {
        string[] oldTextureCodes = _oldMainTextureCodes[modelCode];
        string[] newTextureCodes = data.MainTextureCodes;

        for (int textureIndex = 0; textureIndex < newTextureCodes.Length; textureIndex++)
        {
            string oldTextureCode = oldTextureCodes[textureIndex];
            string newTextureCode = newTextureCodes[textureIndex];

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
        foreach ((string modelCode, CustomModelData data) in CustomModels)
        {
            CollectTexturesForCustomModel(modelCode, data);
        }
    }
    private void CollectTexturesForCustomModel(string modelCode, CustomModelData data)
    {
        if (_clientApi == null) return;

        foreach ((string textureCode, AssetLocation texturePath) in data.Shape.Textures)
        {
            if (data.MainTextureCodes.Contains(textureCode))
            {
                {
                    string newCode = PrefixTextureCode(modelCode, textureCode);

                    if (_textures.ContainsKey(newCode)) continue;

                    _textures.Add(newCode, texturePath);
                    _ = TextureSource?[newCode];

                    IAsset? textureAsset2 = _clientApi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (textureAsset2 == null) continue;

                    //_clientApi.EntityTextureAtlas.GetOrInsertTexture(texturePath, out _, out TextureAtlasPosition texPos, () => textureAsset2.ToBitmap(_clientApi));

                    CompositeTexture mainTexture = new()
                    {
                        Base = texturePath
                    };

                    mainTexture.Bake(_clientApi.Assets);

                    data.MainTextures[newCode] = mainTexture;
                }

                {
                    string newCode = textureCode;

                    if (_textures.ContainsKey(newCode)) continue;

                    _textures.Add(newCode, texturePath);
                    _ = TextureSource?[newCode];

                    IAsset? textureAsset2 = _clientApi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (textureAsset2 == null) continue;

                    //_clientApi.EntityTextureAtlas.GetOrInsertTexture(texturePath, out _, out TextureAtlasPosition texPos, () => textureAsset2.ToBitmap(_clientApi));

                    CompositeTexture mainTexture = new()
                    {
                        Base = texturePath
                    };

                    mainTexture.Bake(_clientApi.Assets);

                    data.MainTextures[newCode] = mainTexture;
                }

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
    private void CollectExclusiveClasses()
    {
        foreach (HashSet<string> classes in CustomModels.Select(entry => entry.Value.ExclusiveClasses))
        {
            ExclusiveClasses.AddRange(classes);
        }
    }
    private void CollectBaseShapeElements(ShapeElement[] elements, string[] elementsToCollect, Dictionary<string, (Vector3d, Vector3d)> collectedElements)
    {
        foreach (ShapeElement? element in elements)
        {
            if (element == null) continue;

            string code = element.Name ?? "";
            if (elementsToCollect.Contains(code) && !collectedElements.ContainsKey(code) && element.From != null && element.To != null)
            {
                Vector3d size = new(
                    MathF.Abs((float)element.To[0] - (float)element.From[0]),
                    MathF.Abs((float)element.To[1] - (float)element.From[1]),
                    MathF.Abs((float)element.To[2] - (float)element.From[2]));

                Vector3d origin = new(
                    MathF.Abs((float)element.From[0]),
                    MathF.Abs((float)element.From[1]),
                    MathF.Abs((float)element.From[2]));

                collectedElements.TryAdd(code, (origin, size));
            }

            if (element.Children != null)
            {
                CollectBaseShapeElements(element.Children, elementsToCollect, collectedElements);
            }
        }
    }
}