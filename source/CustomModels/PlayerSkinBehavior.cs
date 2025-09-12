using HarmonyLib;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class PlayerSkinBehavior : EntityBehaviorExtraSkinnable, ITexPositionSource
{
    public PlayerSkinBehavior(Entity entity) : base(entity)
    {
    }

    public string CurrentModelCode { get; protected set; } = "seraph";

    public float CurrentSize { get; protected set; } = 1f;

    public CustomModelData CurrentModel
    {
        get
        {
            if (ModelSystem == null) throw new InvalidOperationException("Calling PlayerSkinBehavior.CurrentModel before it is initialized. Thanks Tyron for initialization outside of constructor.");

            if (ModelSystem.CustomModels.TryGetValue(CurrentModelCode, out CustomModelData? value))
            {
                return value;
            }

            LoggerUtil.Warn(ClientApi, this, $"Custom model '{CurrentModelCode}' does not exists.");

            return ModelSystem.CustomModels[ModelSystem.DefaultModelCode];
        }
    }

    public event Action? OnActuallyInitialize;

    public event Action<string>? OnModelChanged;

    public event Action<Shape>? OnShapeTesselated;


    public Size2i? AtlasSize => ModelSystem?.GetAtlasSize(CurrentModelCode, entity);
    public TextureAtlasPosition? this[string textureCode] => GetAtlasPosition(textureCode, entity);


    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        DefaultSize = entity.Properties.Client.Size;

        ClientApi = entity.Api as ICoreClientAPI;

        ModelSystem = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();

        if (ModelSystem.ModelsLoaded)
        {
            ActuallyInitialize();
        }
        else
        {
            ModelSystem.OnCustomModelsLoaded += ActuallyInitialize;
        }
    }

    public virtual void ActuallyInitialize()
    {
        if (ModelSystem == null) return;

        skintree = entity.WatchedAttributes.GetTreeAttribute("skinConfig");
        if (skintree == null)
        {
            entity.WatchedAttributes["skinConfig"] = skintree = new TreeAttribute();
        }

        string skinModel = GetPlayerModelAttributeValue();
        if (skinModel == null || !ModelSystem.CustomModels.ContainsKey(skinModel))
        {
            entity.WatchedAttributes.SetString("skinModel", ModelSystem.DefaultModelCode);
            CurrentModelCode = ModelSystem.DefaultModelCode;
            AvailableSkinPartsByCode = CurrentModel.SkinParts;
            AvailableSkinParts = CurrentModel.SkinPartsArray;
        }

        entity.WatchedAttributes.RegisterModifiedListener("skinModel", OnSkinModelAttrChanged);
        entity.WatchedAttributes.RegisterModifiedListener("entitySize", OnModelSizeAttrChanged);
        entity.WatchedAttributes.RegisterModifiedListener("skinConfig", OnSkinConfigChanged);
        entity.WatchedAttributes.RegisterModifiedListener("voicetype", OnVoiceConfigChanged);
        entity.WatchedAttributes.RegisterModifiedListener("voicepitch", OnVoiceConfigChanged);

        OnVoiceConfigChanged();

        OnSkinModelChanged();

        if (entity.Api.Side == EnumAppSide.Server && AppliedSkinParts.Count == 0)
        {
            entity.Api.ModLoader.GetModSystem<CharacterSystem>().randomizeSkin(entity, null, false);
        }

        OnActuallyInitialize?.Invoke();
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[]? willDeleteElements)
    {
        if (ModelSystem == null || ClientApi == null || !ModelSystem.ModelsLoaded) return;

        Shape backup = entityShape;

        try
        {
            entityShape = ModelSystem.CustomModels[CurrentModelCode].Shape.Clone();
            shapeIsCloned = true;

            AddMainTextures();

            AddSkinParts(ref entityShape, shapePathForLogging);

            AddSkinPartsTextures(ClientApi, entityShape, shapePathForLogging);

            RemoveHiddenElements(entityShape, ref willDeleteElements);

            OnShapeTesselated?.Invoke(entityShape);
        }
        catch (Exception exception)
        {
            entityShape = backup;
            LoggerUtil.Error(ClientApi, this, $"({CurrentModelCode}) Error when tesselating custom player model:\n{exception}");
        }
    }

    public void SetCurrentModel(string code, float size)
    {
        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = code;
        CurrentSize = size;
        AvailableSkinPartsByCode = CurrentModel.SkinParts;
        AvailableSkinParts = CurrentModel.SkinPartsArray;
        ReplaceEntityShape();
    }

    public override ITexPositionSource? GetTextureSource(ref EnumHandling handling)
    {
        handling = EnumHandling.PreventSubsequent;
        return this;
    }

    public TextureAtlasPosition? GetAtlasPosition(string textureCode, Entity entity)
    {
        if (OverlaysTexturePositions.TryGetValue(textureCode, out TextureAtlasPosition? position))
        {
            return position;
        }

        TextureAtlasPosition? result = ModelSystem?.GetAtlasPosition(CurrentModelCode, textureCode, entity);

        return result ?? ClientApi?.EntityTextureAtlas.UnknownTexturePosition;
    }

    public override void OnEntityLoaded()
    {
        OnSkinModelChanged();
    }

    public override void OnEntitySpawn()
    {
        OnSkinModelChanged();
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        // nothing to do here
    }

    public override string PropertyName() => "skinnableplayercustommodel";

    public void UpdateEntityProperties()
    {
        if (CurrentSize <= 0)
        {
            CurrentSize = 1;
        }

        if (entity is EntityPlayer player)
        {
            float factor = MathF.Sqrt(CurrentSize) * CurrentModel.HeadBobbingScale;
            player.HeadBobbingAmplitude *= factor / PreviousHeadBobbingAmplitudeFactor;
            PreviousHeadBobbingAmplitudeFactor = factor;
        }

        OtherPatches.CurrentModelGuiScale = CurrentModel.GuiModelScale;
        OtherPatches.CurrentModelScale = CurrentModel.ModelSizeFactor;

        entity.Properties.EyeHeight = GameMath.Clamp(CurrentModel.EyeHeight * CurrentSize * CurrentModel.ModelSizeFactor, CurrentModel.MinEyeHeight, CurrentModel.MaxEyeHeight);
        entity.Properties.CollisionBoxSize = new Vec2f(CurrentModel.CollisionBox.X, CurrentModel.CollisionBox.Y);
        entity.Properties.SelectionBoxSize = new Vec2f(CurrentModel.CollisionBox.X, CurrentModel.CollisionBox.Y);
        if (CurrentModel.ScaleColliderWithSizeHorizontally)
        {
            entity.Properties.CollisionBoxSize.X *= CurrentSize * CurrentModel.ModelSizeFactor;
            entity.Properties.SelectionBoxSize.X *= CurrentSize * CurrentModel.ModelSizeFactor;

            entity.Properties.CollisionBoxSize.X = GameMath.Clamp(entity.Properties.CollisionBoxSize.X, CurrentModel.MinCollisionBox.X, CurrentModel.MaxCollisionBox.X);
            entity.Properties.SelectionBoxSize.X = GameMath.Clamp(entity.Properties.SelectionBoxSize.X, CurrentModel.MinCollisionBox.X, CurrentModel.MaxCollisionBox.X);
        }
        if (CurrentModel.ScaleColliderWithSizeVertically)
        {
            entity.Properties.CollisionBoxSize.Y *= CurrentSize * CurrentModel.ModelSizeFactor;
            entity.Properties.SelectionBoxSize.Y *= CurrentSize * CurrentModel.ModelSizeFactor;

            entity.Properties.CollisionBoxSize.Y = GameMath.Clamp(entity.Properties.CollisionBoxSize.Y, CurrentModel.MinCollisionBox.Y, CurrentModel.MaxCollisionBox.Y);
            entity.Properties.SelectionBoxSize.Y = GameMath.Clamp(entity.Properties.SelectionBoxSize.Y, CurrentModel.MinCollisionBox.Y, CurrentModel.MaxCollisionBox.Y);
        }
        if (entity.Api.Side == EnumAppSide.Server) Traverse.Create((entity as EntityPlayer)?.Player).Method("updateColSelBoxes").GetValue();
        entity.Properties.Client.Size = DefaultSize * CurrentSize * CurrentModel.ModelSizeFactor;
        entity.LocalEyePos.Y = GameMath.Clamp(CurrentModel.EyeHeight * CurrentSize * CurrentModel.ModelSizeFactor, CurrentModel.MinEyeHeight, CurrentModel.MaxEyeHeight);

        ChangeTags();

        SetZNear();
    }

    protected CustomModelsSystem? ModelSystem;
    protected ICoreClientAPI? ClientApi;
    protected Dictionary<string, TextureAtlasPosition> OverlaysTexturePositions = [];
    protected Dictionary<string, BlendedOverlayTexture[]> OverlaysByTextures = [];
    protected EntityTagArray PreviousAddedTags = EntityTagArray.Empty;
    protected EntityTagArray PreviousRemovedTags = EntityTagArray.Empty;
    protected float DefaultSize = 1;
    protected float PreviousHeadBobbingAmplitudeFactor = 1;
    protected float PreviousZNearFactor = 1;
    protected float DefaultEyeHeight = 1.7f;
    protected string DefaultModelCode => ModelSystem?.DefaultModelCode ?? "seraph";


    protected void OnSkinConfigChanged()
    {
        if (ModelSystem?.ModelsLoaded != true) return;

        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;

        string modelCode = GetPlayerModelAttributeValue();
        if (modelCode != CurrentModelCode)
        {
            CurrentModelCode = modelCode;
            AvailableSkinPartsByCode = CurrentModel.SkinParts;
            AvailableSkinParts = CurrentModel.SkinPartsArray;
            ReplaceEntityShape();
        }
    }

    protected void OnVoiceConfigChanged()
    {
        if (ModelSystem?.ModelsLoaded != true) return;

        string? voiceType = entity.WatchedAttributes.GetString("voicetype");
        string? voicePitch = entity.WatchedAttributes.GetString("voicepitch");

        VoiceType = voiceType;
        VoicePitch = voicePitch;
        ApplyVoice(VoiceType, VoicePitch, false);
    }

    protected void OnSkinModelAttrChanged()
    {
        string modelCode = GetPlayerModelAttributeValue();
        if (modelCode != CurrentModelCode)
        {
            OnSkinModelChanged();
        }
    }

    protected void OnModelSizeAttrChanged()
    {
        float size = entity.WatchedAttributes.GetFloat("entitySize");
        if (size != CurrentSize)
        {
            OnSkinModelChanged();
        }
    }

    protected void OnSkinModelChanged()
    {
        if (ModelSystem?.ModelsLoaded != true) return;

        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = GetPlayerModelAttributeValue();
        CurrentSize = entity.WatchedAttributes.GetFloat("entitySize");
        if (!ModelSystem.CustomModels.ContainsKey(CurrentModelCode))
        {
            CurrentModelCode = ModelSystem.DefaultModelCode;
            entity.WatchedAttributes.SetString("skinModel", CurrentModelCode);
            return;
        }
        AvailableSkinPartsByCode = CurrentModel.SkinParts;
        AvailableSkinParts = CurrentModel.SkinPartsArray;
        ReplaceEntityShape();
    }

    protected void ReplaceEntityShape()
    {
        if (ModelSystem?.ModelsLoaded != true) return;
        if (!ModelSystem.CustomModels.TryGetValue(CurrentModelCode, out _)) return;

        if (entity.Properties.Client.Renderer is EntityShapeRenderer)
        {
            entity.MarkShapeModified();
        }

        UpdateEntityProperties();

        OnModelChanged?.Invoke(CurrentModelCode);
    }

    protected void ChangeTags()
    {
        if (entity.Api.Side == EnumAppSide.Client) return;

        EntityTagArray currentTags = entity.Tags;



        currentTags &= ~PreviousAddedTags;
        currentTags |= PreviousRemovedTags;



        PreviousAddedTags = CurrentModel.AddTags & ~currentTags;
        PreviousRemovedTags = CurrentModel.RemoveTags & currentTags;

        currentTags &= ~PreviousRemovedTags;
        currentTags |= PreviousAddedTags;

        entity.Tags = currentTags;
        entity.MarkTagsDirty();


    }


    protected virtual void AddMainTextures()
    {
        if (ModelSystem == null) return;

        foreach ((_, CustomModelData? data) in ModelSystem.CustomModels)
        {
            entity.Properties.Client.Textures[data.MainTextureCode] = data.MainTexture;
        }
    }

    protected virtual void AddSkinParts(ref Shape entityShape, string shapePathForLogging)
    {
        foreach (AppliedSkinnablePartVariant? skinPart in AppliedSkinParts)
        {
            AvailableSkinPartsByCode.TryGetValue(skinPart.PartCode, out SkinnablePart? part);

            if (part?.Type == EnumSkinnableType.Shape)
            {
                entityShape = AddSkinPart(skinPart, entityShape, part.DisableElements, shapePathForLogging);
            }
        }
    }

    protected virtual void AddSkinPartsTextures(ICoreClientAPI api, Shape entityShape, string shapePathForLogging)
    {
        foreach (AppliedSkinnablePartVariant? skinPart in AppliedSkinParts)
        {
            AvailableSkinPartsByCode.TryGetValue(skinPart.PartCode, out SkinnablePart? part);

            if (part == null || part.Type != EnumSkinnableType.Texture || part.TextureTarget == null) continue;

            AssetLocation textureLoc;
            if (part.TextureTemplate != null)
            {
                textureLoc = part.TextureTemplate.Clone();
                textureLoc.Path = textureLoc.Path.Replace("{code}", skinPart.Code);
            }
            else
            {
                textureLoc = skinPart.Texture;
            }

            SkinnablePartExtended extendedPart = part as SkinnablePartExtended ?? throw new InvalidOperationException($"Player model lib uses only 'SkinnablePartExtended'");

            if (extendedPart.TargetSkinParts.Length > 0)
            {
                foreach (string targetSkinPart in extendedPart.TargetSkinParts)
                {
                    string code = CustomModelsSystem.PrefixSkinPartTextures(CurrentModelCode, part.TextureTarget, targetSkinPart);

                    if (extendedPart.OverlayTexture)
                    {
                        AddOverlayTexture(code, textureLoc, extendedPart.OverlayMode);
                    }
                    else
                    {
                        if (entityShape.TextureSizes?.TryGetValue(code, out int[]? sizes) == true)
                        {
                            ReplaceTexture(api, entityShape, code, textureLoc, sizes[0], sizes[1], shapePathForLogging);
                        }
                        else
                        {
                            ReplaceTexture(api, entityShape, code, textureLoc, entityShape.TextureWidth, entityShape.TextureHeight, shapePathForLogging);
                        }
                    }
                }
            }
            else
            {
                string mainCode = CustomModelsSystem.PrefixTextureCode(CurrentModelCode, part.TextureTarget);

                if (extendedPart.OverlayTexture)
                {
                    AddOverlayTexture(mainCode, textureLoc, extendedPart.OverlayMode);
                }
                else
                {
                    if (entityShape.TextureSizes?.TryGetValue(mainCode, out int[]? sizes) == true)
                    {
                        ReplaceTexture(api, entityShape, mainCode, textureLoc, sizes[0], sizes[1], shapePathForLogging);
                    }
                    else
                    {
                        ReplaceTexture(api, entityShape, mainCode, textureLoc, entityShape.TextureWidth, entityShape.TextureHeight, shapePathForLogging);
                    }
                }

            }
        }

        foreach (string code in OverlaysByTextures.Keys)
        {
            ApplyOverlayTexture(api, entityShape, code);
        }
        OverlaysByTextures.Clear();
    }

    protected virtual void RemoveHiddenElements(Shape entityShape, ref string[]? willDeleteElements)
    {
        EntityBehaviorTexturedClothing? texturedClothingBehavior = entity.GetBehavior<EntityBehaviorTexturedClothing>();
        if (texturedClothingBehavior == null || texturedClothingBehavior.hideClothing) return;
        InventoryBase? inventory = texturedClothingBehavior.Inventory;
        if (inventory == null) return;

        IEnumerable<string> skinPartsPrefixes = CurrentModel.SkinPartsArray.Select(skinPart => CustomModelsSystem.GetSkinPartTexturePrefix(CurrentModelCode, skinPart.Code));

        foreach (ItemSlot? slot in inventory)
        {
            if (slot.Empty) continue;

            GetWearableElements(slot, out string[]? disableElements, out string[]? keepElements);

            if (disableElements != null)
            {
                RemoveDisabledElements(entityShape, disableElements, skinPartsPrefixes);
            }

            if (keepElements != null && willDeleteElements != null)
            {
                foreach (string element in keepElements)
                {
                    willDeleteElements = willDeleteElements.Remove(element);
                }
            }
        }
    }

    protected virtual void RemoveDisabledElements(Shape entityShape, string[] disableElements, IEnumerable<string> skinPartsPrefixes)
    {
        string basePrefix = CustomModelsSystem.GetTextureCodePrefix(CurrentModelCode);

        foreach (string element in disableElements)
        {
            entityShape.RemoveElementByName(element);
            entityShape.RemoveElementByName(basePrefix + element);
            foreach (string skinPart in skinPartsPrefixes)
            {
                entityShape.RemoveElementByName(skinPart + element);
            }
        }
    }

    protected virtual void GetWearableElements(ItemSlot slot, out string[]? disableElements, out string[]? keepElements)
    {
        ItemStack? stack = slot.Itemstack;
        if (stack == null)
        {
            disableElements = [];
            keepElements = [];
            return;
        }

        disableElements = (stack.Collectible as IAttachableToEntity)?.GetDisableElements(stack);
        keepElements = (stack.Collectible as IAttachableToEntity)?.GetKeepElements(stack);

        disableElements ??= stack.Collectible?.Attributes?["disableElements"]?.AsArray<string>(null);
        disableElements ??= stack.Collectible?.Attributes?["attachableToEntity"]?["disableElements"]?.AsArray<string>(null);
        keepElements ??= stack.Collectible?.Attributes?["keepElements"]?.AsArray<string>(null);
        keepElements ??= stack.Collectible?.Attributes?["attachableToEntity"]?["keepElements"]?.AsArray<string>(null);
    }

    protected Shape AddSkinPart(AppliedSkinnablePartVariant part, Shape entityShape, string[] disableElements, string shapePathForLogging)
    {
        if (ClientApi == null) return entityShape;

        SkinnablePart skinPart = CurrentModel.SkinParts[part.PartCode];

        entityShape.RemoveElements(disableElements);

        AssetLocation shapePath;
        CompositeShape skinPartShape = skinPart.ShapeTemplate;

        if (part.Shape == null && skinPartShape != null)
        {
            shapePath = skinPartShape.Base.CopyWithPath("shapes/" + skinPartShape.Base.Path + ".json");
            shapePath.Path = shapePath.Path.Replace("{code}", part.Code);
        }
        else if (part.Shape != null)
        {
            shapePath = part.Shape.Base.CopyWithPath("shapes/" + part.Shape.Base.Path + ".json");
        }
        else
        {
            return entityShape;
        }

        Shape partShape = Shape.TryGet(ClientApi, shapePath);
        if (partShape == null)
        {
            ClientApi.World.Logger.Warning("Entity skin shape {0} defined in entity config {1} not found or errored, was supposed to be at {2}. Skin part will be invisible.", shapePath, entity.Properties.Code, shapePath);
            return entityShape;
        }

        string prefixCode = CustomModelsSystem.GetSkinPartTexturePrefix(CurrentModelCode, skinPart.Code);
        partShape.SubclassForStepParenting(prefixCode);

        foreach ((string code, int[] size) in partShape.TextureSizes)
        {
            entityShape.TextureSizes[code] = size;
        }

        IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;
        entityShape.StepParentShape(partShape, shapePath.ToShortString(), shapePathForLogging, ClientApi.Logger, (code, path) =>
        {
            if (!textures.ContainsKey(prefixCode + code))
            {
                CompositeTexture compositeTexture = new(path);
                textures[prefixCode + code] = compositeTexture;
                compositeTexture.Bake(ClientApi.Assets);
                ClientApi.EntityTextureAtlas.GetOrInsertTexture(compositeTexture.Baked.TextureFilenames[0], out int textureSubId, out _);
                compositeTexture.Baked.TextureSubId = textureSubId;

            }
        });

        return entityShape;
    }

    protected virtual void ReplaceTexture(ICoreClientAPI api, Shape entityShape, string code, AssetLocation location, int textureWidth, int textureHeight, string shapePathForLogging)
    {
        IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;

        CompositeTexture compositeTexture = new(location);
        textures[code] = compositeTexture;

        compositeTexture.Bake(api.Assets);
        if (!api.EntityTextureAtlas.GetOrInsertTexture(compositeTexture.Baked.TextureFilenames[0], out int textureSubId, out _, null, -1))
        {
            LoggerUtil.Warn(api, this, $"Skin part shape {shapePathForLogging} defined texture {location}, no such texture found.");
        }
        compositeTexture.Baked.TextureSubId = textureSubId;

        entityShape.TextureSizes[code] = [textureWidth, textureHeight];
        textures[code] = compositeTexture;
    }

    protected virtual void AddOverlayTexture(string code, AssetLocation overlayTextureLocation, EnumColorBlendMode overlayMode)
    {
        CompositeTexture overlayTexture = new(overlayTextureLocation);

        if (OverlaysByTextures.TryGetValue(code, out BlendedOverlayTexture[]? overlayTextures))
        {
            OverlaysByTextures[code] = overlayTextures.Append(new BlendedOverlayTexture() { Base = overlayTexture.Base, BlendMode = overlayMode });
            return;
        }

        OverlaysByTextures[code] = [new BlendedOverlayTexture() { Base = overlayTexture.Base, BlendMode = overlayMode }];
    }

    protected virtual void ApplyOverlayTexture(ICoreClientAPI api, Shape entityShape, string code)
    {
        IDictionary<string, CompositeTexture?> textures = entity.Properties.Client.Textures;

        if (!textures.TryGetValue(code, out CompositeTexture? baseTexture)) return;

        CompositeTexture? clonedBaseTexture = baseTexture?.Clone();

        if (clonedBaseTexture == null)
        {
            LoggerUtil.Error(api, this, $"({CurrentModelCode}) Texture '{code}' in null, cant apply overlays to it, will skip.");
            return;
        }

        clonedBaseTexture.BlendedOverlays = OverlaysByTextures[code];

        api.EntityTextureAtlas.GetOrInsertTexture(clonedBaseTexture, out _, out TextureAtlasPosition texturePosition, -1);

        OverlaysTexturePositions[code] = texturePosition;
    }

    protected virtual void SetZNear()
    {
        if (entity.Api is not ICoreClientAPI clientApi) return;
        
        float factor = (float)entity.LocalEyePos.Y / DefaultEyeHeight;

        Debug.WriteLine(factor);

        SetZNear(clientApi, factor);
    }
    
    protected virtual void SetZNear(ICoreClientAPI api, float multiplier)
    {
        ClientMain? client = api.World as ClientMain;
        if (client == null) return;

        PlayerCamera? camera = client.MainCamera;
        if (camera == null) return;

        camera.ZNear /= PreviousZNearFactor;
        PreviousZNearFactor = multiplier;
        camera.ZNear *= PreviousZNearFactor;
    }

    protected virtual string GetPlayerModelAttributeValue() => entity.WatchedAttributes.GetString("skinModel", "seraph") ?? "seraph";
}
