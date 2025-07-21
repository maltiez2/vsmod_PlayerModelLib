using HarmonyLib;
using System.Diagnostics;
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

        ModelSystem.OnCustomModelsLoaded += ActuallyInitialize;
    }

    public virtual void ActuallyInitialize()
    {
        if (ModelSystem == null) return;

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

        entity.WatchedAttributes.RegisterModifiedListener("skinModel", OnSkinModelChanged);
        entity.WatchedAttributes.RegisterModifiedListener("skinConfig", OnSkinConfigChanged);
        entity.WatchedAttributes.RegisterModifiedListener("voicetype", OnVoiceConfigChanged);
        entity.WatchedAttributes.RegisterModifiedListener("voicepitch", OnVoiceConfigChanged);

        if (entity.Api.Side == EnumAppSide.Server && AppliedSkinParts.Count == 0)
        {
            entity.Api.ModLoader.GetModSystem<CharacterSystem>().randomizeSkin(entity, null, false);
        }
        OnVoiceConfigChanged();

        ReplaceEntityShape();
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        if (ModelSystem == null || ClientApi == null) return;

        entityShape = ModelSystem.CustomModels[CurrentModelCode].Shape.Clone();
        shapeIsCloned = true;

        AddMainTextures();

        AddSkinParts(ref entityShape, shapePathForLogging);

        AddSkinPartsTextures(ClientApi, entityShape, shapePathForLogging);

        //(entity.Properties.Client.Renderer as EntityShapeRenderer).skinTexPos = CurrentModel.MainTexturePosition;
    }

    public void SetCurrentModel(string code)
    {
        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = code;
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
        return ModelSystem?.GetAtlasPosition(CurrentModelCode, textureCode, entity);
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

    protected CustomModelsSystem? ModelSystem;
    protected ICoreClientAPI? ClientApi;

    protected void OnSkinConfigChanged()
    {
        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = entity.WatchedAttributes.GetString("skinModel");
        AvailableSkinPartsByCode = CurrentModel.SkinParts;
        AvailableSkinParts = CurrentModel.SkinPartsArray;
        ReplaceEntityShape();
        entity.MarkShapeModified();
    }

    protected void OnVoiceConfigChanged()
    {
        VoiceType = entity.WatchedAttributes.GetString("voicetype");
        VoicePitch = entity.WatchedAttributes.GetString("voicepitch");
        ApplyVoice(VoiceType, VoicePitch, false);
    }

    protected void OnSkinModelChanged()
    {
        if (ModelSystem == null) return;

        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = entity.WatchedAttributes.GetString("skinModel");
        if (!ModelSystem.CustomModels.ContainsKey(CurrentModelCode))
        {
            CurrentModelCode = ModelSystem.DefaultModelCode;
            entity.WatchedAttributes.SetString("skinModel", CurrentModelCode);
        }
        AvailableSkinPartsByCode = CurrentModel.SkinParts;
        AvailableSkinParts = CurrentModel.SkinPartsArray;
        ReplaceEntityShape();
        entity.MarkShapeModified();
    }

    protected void ReplaceEntityShape()
    {
        if (ModelSystem == null || !ModelSystem.CustomModels.TryGetValue(CurrentModelCode, out CustomModelData? customModel)) return;
        if (entity.Properties.Client.Renderer is not EntityShapeRenderer renderer || entity is not EntityPlayer player) return;

        renderer.TesselateShape();

        player.Properties.EyeHeight = customModel.EyeHeight;
        player.Properties.CollisionBoxSize = new Vec2f(customModel.CollisionBox.X, customModel.CollisionBox.Y);
        player.Properties.SelectionBoxSize = new Vec2f(customModel.CollisionBox.X, customModel.CollisionBox.Y);
        Traverse.Create(player.Player).Method("updateColSelBoxes").GetValue();
        player.LocalEyePos.Y = player.Properties.EyeHeight;
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

            if ((part as SkinnablePartExtended)?.TargetSkinParts.Length > 0)
            {
                foreach (string targetSkinPart in (part as SkinnablePartExtended).TargetSkinParts)
                {
                    string code = CustomModelsSystem.PrefixSkinPartTextures(CurrentModelCode, part.TextureTarget, targetSkinPart);

                    if (entityShape.TextureSizes.TryGetValue(code, out int[]? sizes))
                    {
                        LoadTexture(api, entityShape, code, textureLoc, sizes[0], sizes[1], shapePathForLogging);
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
                    LoadTexture(api, entityShape, mainCode, textureLoc, sizes[0], sizes[1], shapePathForLogging);
                }
                else
                {
                    LoggerUtil.Error(api, this, $"Skin part has no textureSize: {mainCode} in {shapePathForLogging}");
                }
            }
        }
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

    /*protected void Essr_OnReloadSkin(LoadedTexture atlas, TextureAtlasPosition skinTexPos, int textureSubId)
    {
        ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;

        foreach (AppliedSkinnablePartVariant? val in AppliedSkinParts)
        {
            SkinnablePart part = AvailableSkinPartsByCode[val.PartCode];

            if (part.Type != EnumSkinnableType.Texture) continue;
            if (part.TextureTarget != null && part.TextureTarget != mainTextureCode) continue;

            LoadedTexture texture = new(capi);

            capi.Render.GetOrLoadTexture(val.Texture.Clone().WithPathAppendixOnce(".png"), ref texture);

            int posx = part.TextureRenderTo?.X ?? 0;
            int posy = part.TextureRenderTo?.Y ?? 0;

            Debug.WriteLine($"({(entity as EntityPlayer).Player.PlayerName}) redner into: {skinTexPos.x1 * capi.EntityTextureAtlas.Size.Width + posx} {skinTexPos.y1 * capi.EntityTextureAtlas.Size.Height + posy}, with dimensions {texture.Width} {texture.Height}");
            Debug.WriteLine($"({(entity as EntityPlayer).Player.PlayerName}) textureSubId: {textureSubId}, skinTexPos.atlasTextureId: {skinTexPos.atlasTextureId}");

            capi.EntityTextureAtlas.RenderTextureIntoAtlas(
                skinTexPos.atlasTextureId,
                texture,
                0,
                0,
                texture.Width,
                texture.Height,
                skinTexPos.x1 * capi.EntityTextureAtlas.Size.Width + posx,
                skinTexPos.y1 * capi.EntityTextureAtlas.Size.Height + posy,
                part.Code == "baseskin" ? -1 : 0.005f
            );
        }

        IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;

        string prefixcode = "skinpart";
        if (CurrentModel != ModelSystem.DefaultModelCode)
        {
            prefixcode += "-" + CurrentModel.Replace(':', '-');
        }

        foreach (string modelCode in ModelSystem.MainTextureCodes.Keys)
        {
            string mtexturecode = ModelSystem.MainTextureCodes[modelCode];

            if (textures.ContainsKey(mtexturecode))
            {
                textures[mtexturecode].Baked.TextureSubId = textureSubId;
                textures[prefixcode + "-" + mtexturecode] = textures[mtexturecode];
            }
        }
    }*/

    protected virtual void LoadTexture(ICoreClientAPI api, Shape entityShape, string code, AssetLocation location, int textureWidth, int textureHeight, string shapePathForLogging)
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
}
