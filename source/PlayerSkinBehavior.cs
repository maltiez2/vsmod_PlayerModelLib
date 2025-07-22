using HarmonyLib;
using System.Diagnostics;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class PlayerSkinBehavior : EntityBehaviorExtraSkinnable, ITexPositionSource
{
    /*public Dictionary<string, SkinnablePart> AvailableSkinPartsByCode { get; set; } = new Dictionary<string, SkinnablePart>();
    public SkinnablePart[] AvailableSkinParts { get; set; }
    public string VoiceType = "altoflute";
    public string VoicePitch = "medium";
    public string mainTextureCode;
    public List<AppliedSkinnablePartVariant> appliedTemp = new List<AppliedSkinnablePartVariant>();
    protected ITreeAttribute skintree;*/

    public PlayerSkinBehavior(Entity entity) : base(entity)
    {
    }

    public string CurrentModelCode { get; protected set; } = "seraph";

    public float CurrentSize { get; protected set; } = 1f;

    public CustomModelData CurrentModel
    {
        get
        {
            if (ModelSystem == null) throw new InvalidOperationException("Calling PlayerSkinBehavior.CurrentModel before it is initialized. Thanks Tyron for initialisation outside of constructor.");

            if (ModelSystem.CustomModels.TryGetValue(CurrentModelCode, out CustomModelData? value))
            {
                return value;
            }

            LoggerUtil.Warn(ClientApi, this, $"Custom model '{CurrentModelCode}' does not exists.");

            return ModelSystem.CustomModels[ModelSystem.DefaultModelCode];
        }
    }


    public Size2i? AtlasSize => ModelSystem?.GetAtlasSize(CurrentModelCode, entity);
    public TextureAtlasPosition? this[string textureCode] => GetAtlasPosition(textureCode, entity);


    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
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

        Debug.WriteLine($"ActuallyInitialize - side: {entity.Api.Side}");

        skintree = entity.WatchedAttributes.GetTreeAttribute("skinConfig");
        if (skintree == null)
        {
            entity.WatchedAttributes["skinConfig"] = skintree = new TreeAttribute();
        }

        string? skinModel = entity.WatchedAttributes.GetString("skinModel");
        if (skinModel == null || !ModelSystem.CustomModels.ContainsKey(skinModel))
        {
            entity.WatchedAttributes.SetString("skinModel", ModelSystem.DefaultModelCode);
            CurrentModelCode = ModelSystem.DefaultModelCode;
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
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        if (ModelSystem == null || ClientApi == null || !ModelSystem.ModelsLoaded) return;

        entityShape = ModelSystem.CustomModels[CurrentModelCode].Shape.Clone();
        shapeIsCloned = true;

        AddMainTextures();

        AddSkinParts(ref entityShape, shapePathForLogging);

        AddSkinPartsTextures(ClientApi, entityShape, shapePathForLogging);
    }

    public void SetCurrentModel(string code, float size)
    {
        Debug.WriteLine($"SetCurrentModel - side: {entity.Api.Side}");

        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = code;
        CurrentSize = size;
        AvailableSkinPartsByCode = CurrentModel.SkinParts;
        AvailableSkinParts = CurrentModel.SkinPartsArray;
        ReplaceEntityShape();
        entity.MarkShapeModified();
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
        Debug.WriteLine($"OnEntityLoaded - {(entity as EntityPlayer).Player?.PlayerName ?? entity.EntityId.ToString()}");
    }

    public override void OnEntitySpawn()
    {
        OnSkinModelChanged();
        Debug.WriteLine($"OnEntitySpawn - {(entity as EntityPlayer).Player?.PlayerName ?? entity.EntityId.ToString()}");
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        Debug.WriteLine($"OnEntityDespawn - {(entity as EntityPlayer).Player?.PlayerName ?? entity.EntityId.ToString()}");
    }

    public override string PropertyName() => "skinnableplayercustommodel";
    
    public void UpdateEntityProperties()
    {
        Debug.WriteLine($"UpdateEntityProperties - side: {entity.Api.Side}");

        if (CurrentSize <= 0)
        {
            CurrentSize = 1;
        }

        entity.Properties.EyeHeight = GameMath.Clamp(CurrentModel.EyeHeight * CurrentSize, CurrentModel.MinEyeHeight, CurrentModel.MaxEyeHeight);
        entity.Properties.CollisionBoxSize = new Vec2f(CurrentModel.CollisionBox.X, CurrentModel.CollisionBox.Y);
        entity.Properties.SelectionBoxSize = new Vec2f(CurrentModel.CollisionBox.X, CurrentModel.CollisionBox.Y);
        if (CurrentModel.ScaleColliderWithSizeHorizontally)
        {
            entity.Properties.CollisionBoxSize.X *= CurrentSize;
            entity.Properties.SelectionBoxSize.X *= CurrentSize;

            entity.Properties.CollisionBoxSize.X = GameMath.Clamp(entity.Properties.CollisionBoxSize.X, CurrentModel.MinCollisionBox.X, CurrentModel.MaxCollisionBox.X);
            entity.Properties.SelectionBoxSize.X = GameMath.Clamp(entity.Properties.SelectionBoxSize.X, CurrentModel.MinCollisionBox.X, CurrentModel.MaxCollisionBox.X);
        }
        if (CurrentModel.ScaleColliderWithSizeVertically)
        {
            entity.Properties.CollisionBoxSize.Y *= CurrentSize;
            entity.Properties.SelectionBoxSize.Y *= CurrentSize;

            entity.Properties.CollisionBoxSize.Y = GameMath.Clamp(entity.Properties.CollisionBoxSize.Y, CurrentModel.MinCollisionBox.Y, CurrentModel.MaxCollisionBox.Y);
            entity.Properties.SelectionBoxSize.Y = GameMath.Clamp(entity.Properties.SelectionBoxSize.Y, CurrentModel.MinCollisionBox.Y, CurrentModel.MaxCollisionBox.Y);
        }
        if (entity.Api.Side == EnumAppSide.Server) Traverse.Create((entity as EntityPlayer)?.Player).Method("updateColSelBoxes").GetValue();
        entity.Properties.Client.Size = CurrentSize;
        entity.LocalEyePos.Y = GameMath.Clamp(CurrentModel.EyeHeight * CurrentSize, CurrentModel.MinEyeHeight, CurrentModel.MaxEyeHeight);
    }



    protected CustomModelsSystem? ModelSystem;
    protected ICoreClientAPI? ClientApi;
    protected Dictionary<string, int> OverlaysTextureSpaces = [];
    protected Dictionary<string, TextureAtlasPosition> OverlaysTexturePositions = [];
    protected Dictionary<string, BlendedOverlayTexture[]> OverlaysByTextures = [];
    protected int SkinTreeHash = 0;
    

    protected void OnSkinConfigChanged()
    {
        if (ModelSystem?.ModelsLoaded != true) return;

        Debug.WriteLine($"Try OnSkinConfigChanged - side: {entity.Api.Side}");

        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;

        int skinTreeHash = skintree?.GetHashCode() ?? 0;
        if (SkinTreeHash == skinTreeHash) return;
        SkinTreeHash = skinTreeHash;

        Debug.WriteLine($"OnSkinConfigChanged - side: {entity.Api.Side}");

        string modelCode = entity.WatchedAttributes.GetString("skinModel");
        if (modelCode != CurrentModelCode)
        {
            CurrentModelCode = modelCode;
            AvailableSkinPartsByCode = CurrentModel.SkinParts;
            AvailableSkinParts = CurrentModel.SkinPartsArray;
            ReplaceEntityShape();
        }
        
        entity.MarkShapeModified();
    }

    protected void OnVoiceConfigChanged()
    {
        if (ModelSystem?.ModelsLoaded != true) return;

        string voiceType = entity.WatchedAttributes.GetString("voicetype");
        string voicePitch = entity.WatchedAttributes.GetString("voicepitch");

        if (voiceType != VoiceType || voicePitch != VoicePitch)
        {
            VoiceType = voiceType;
            VoicePitch = voicePitch;
            ApplyVoice(VoiceType, VoicePitch, false);
        }
    }

    protected void OnSkinModelAttrChanged()
    {
        string modelCode = entity.WatchedAttributes.GetString("skinModel");
        if (modelCode != CurrentModelCode)
        {
            OnSkinModelChanged();
        }
    }

    protected void OnModelSizeAttrChanged()
    {
        float size = entity.WatchedAttributes.GetFloat("entitySize");
        if (size !=  CurrentSize)
        {
            OnSkinModelChanged();
        }
    }

    protected void OnSkinModelChanged()
    {
        if (ModelSystem?.ModelsLoaded != true) return;

        Debug.WriteLine($"OnSkinModelChanged - side: {entity.Api.Side}");

        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = entity.WatchedAttributes.GetString("skinModel");
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
        entity.MarkShapeModified();
    }

    protected void ReplaceEntityShape()
    {
        Debug.WriteLine($"Try replaceEntityShape - side: {entity.Api.Side}");

        if (ModelSystem?.ModelsLoaded != true) return;
        if (!ModelSystem.CustomModels.TryGetValue(CurrentModelCode, out _)) return;
        
        if (entity.Properties.Client.Renderer is EntityShapeRenderer renderer)
        {
            entity.MarkShapeModified();
            //renderer.TesselateShape();
        }

        Debug.WriteLine($"ReplaceEntityShape - side: {entity.Api.Side}");

        UpdateEntityProperties();
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

                    if (entityShape.TextureSizes.TryGetValue(code, out int[]? sizes))
                    {
                        if (extendedPart.OverlayTexture)
                        {
                            AddOverlayTexture(code, textureLoc, extendedPart.OverlayMode);
                        }
                        else
                        {
                            ReplaceTexture(api, entityShape, code, textureLoc, sizes[0], sizes[1], shapePathForLogging);
                        }

                    }
                    else
                    {
                        LoggerUtil.Error(api, this, $"Skin part has no textureSize: {code} in {shapePathForLogging}");
                    }
                }
            }
            else
            {
                string mainCode = CustomModelsSystem.PrefixTextureCode(CurrentModelCode, part.TextureTarget);
                if (entityShape.TextureSizes.TryGetValue(mainCode, out int[]? sizes))
                {
                    if (extendedPart.OverlayTexture)
                    {
                        AddOverlayTexture(mainCode, textureLoc, extendedPart.OverlayMode);
                    }
                    else
                    {
                        ReplaceTexture(api, entityShape, mainCode, textureLoc, sizes[0], sizes[1], shapePathForLogging);
                    }
                }
                else
                {
                    LoggerUtil.Error(api, this, $"Skin part has no textureSize: {mainCode} in {shapePathForLogging}");
                }
            }
        }

        foreach (string code in OverlaysByTextures.Keys)
        {
            ApplyOverlayTexture(api, entityShape, code);
        }
        OverlaysByTextures.Clear();
    }

    protected virtual void RemoveHiddenElements(Shape entityShape, string[] willDeleteElements)
    {
        EntityBehaviorTexturedClothing? texturedClothingBehavior = entity.GetBehavior<EntityBehaviorTexturedClothing>();
        if (texturedClothingBehavior == null || texturedClothingBehavior.hideClothing) return;
        InventoryBase? inventory = texturedClothingBehavior.Inventory;
        if (inventory == null) return;

        foreach (ItemSlot? slot in inventory)
        {
            if (slot.Empty) continue;

            ItemStack stack = slot.Itemstack;
            JsonObject collectibleAttributes = stack.Collectible.Attributes;

            entityShape.RemoveElements(collectibleAttributes?["disableElements"]?.AsArray<string>(null));
            string[]? keepElements = collectibleAttributes?["keepElements"]?.AsArray<string>(null);
            if (keepElements != null && willDeleteElements != null)
            {
                foreach (string element in keepElements)
                {
                    willDeleteElements = willDeleteElements.Remove(element);
                }
            }
        }
    }

    protected Shape AddSkinPart(AppliedSkinnablePartVariant part, Shape entityShape, string[] disableElements, string shapePathForLogging)
    {
        SkinnablePart skinPart = CurrentModel.SkinParts[part.PartCode];

        entityShape.RemoveElements(disableElements);

        AssetLocation shapePath;
        CompositeShape tmpl = skinPart.ShapeTemplate;

        if (part.Shape == null && tmpl != null)
        {
            shapePath = tmpl.Base.CopyWithPath("shapes/" + tmpl.Base.Path + ".json");
            shapePath.Path = shapePath.Path.Replace("{code}", part.Code);
        }
        else
        {
            shapePath = part.Shape.Base.CopyWithPath("shapes/" + part.Shape.Base.Path + ".json");
        }

        Shape partShape = Shape.TryGet(ClientApi, shapePath);
        if (partShape == null)
        {
            ClientApi.World.Logger.Warning("Entity skin shape {0} defined in entity config {1} not found or errored, was supposed to be at {2}. Skin part will be invisible.", shapePath, entity.Properties.Code, shapePath);
            return null;
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
                Debug.WriteLine($"*** Added texture: {prefixCode + code}");
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
        IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;

        CompositeTexture baseTexture = textures[code].Clone();

        baseTexture.BlendedOverlays = OverlaysByTextures[code];

        api.EntityTextureAtlas.GetOrInsertTexture(baseTexture, out int textureSubId, out TextureAtlasPosition texturePosition, -1);

        OverlaysTexturePositions[code] = texturePosition;

        //AllocateTextureSpace(api, entityShape, code);

        //RenderOverlayTexture(api, entityShape, code, textureSubId, texturePosition);
    }

    protected virtual void RenderOverlayTexture(ICoreClientAPI api, Shape entityShape, string code, int overlayTextureId, TextureAtlasPosition overlayTexturePosition)
    {
        float width = (overlayTexturePosition.x2 - overlayTexturePosition.x1) * api.EntityTextureAtlas.Size.Width;
        float height = (overlayTexturePosition.y2 - overlayTexturePosition.y1) * api.EntityTextureAtlas.Size.Height;

        LoadedTexture texture = new(null)
        {
            TextureId = overlayTextureId,
            Width = (int)width,
            Height = (int)height
        };

        api.EntityTextureAtlas.RenderTextureIntoAtlas(
            OverlaysTexturePositions[code].atlasTextureId,
            texture,
            overlayTexturePosition.x1 * api.EntityTextureAtlas.Size.Width,
            overlayTexturePosition.y1 * api.EntityTextureAtlas.Size.Height,
            width,
            height,
            OverlaysTexturePositions[code].x1 * api.EntityTextureAtlas.Size.Width,
            OverlaysTexturePositions[code].y1 * api.EntityTextureAtlas.Size.Height,
            -1
        );
    }

    protected virtual void AllocateTextureSpace(ICoreClientAPI api, Shape entityShape, string code)
    {
        if (OverlaysTextureSpaces.TryGetValue(code, out int space))
        {
            //api.EntityTextureAtlas.FreeTextureSpace(space);
        }

        int width = entityShape.TextureSizes[code][0] * 2;
        int height = entityShape.TextureSizes[code][1] * 2;

        api.EntityTextureAtlas.AllocateTextureSpace(width, height, out int skinTextureSubId, out TextureAtlasPosition spaceTexturePosition);

        OverlaysTextureSpaces[code] = skinTextureSubId;
        OverlaysTexturePositions[code] = spaceTexturePosition;
    }
}
