using ImGuiNET;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class ExtraSkinnableBehavior : EntityBehaviorExtraSkinnable, ITexPositionSource
{
    /*public Dictionary<string, SkinnablePart> AvailableSkinPartsByCode { get; set; } = new Dictionary<string, SkinnablePart>();
    public SkinnablePart[] AvailableSkinParts { get; set; }
    public string VoiceType = "altoflute";
    public string VoicePitch = "medium";
    public string mainTextureCode;
    public List<AppliedSkinnablePartVariant> appliedTemp = new List<AppliedSkinnablePartVariant>();
    protected ITreeAttribute skintree;*/

    public ExtraSkinnableBehavior(Entity entity) : base(entity)
    {
    }

    public Dictionary<string, Dictionary<string, SkinnablePart>> AvailableSkinPartsByModelCodes { get; set; } = new();
    public Dictionary<string, SkinnablePart[]> AvailableSkinPartsArraysByModelCodes { get; set; } = new();
    public Dictionary<string, Shape?> AvailableModels { get; set; } = new();
    public string CurrentModel { get; protected set; } = "seraph";

    public Size2i? AtlasSize => ModelSystem?.GetAtlasSize(CurrentModel, entity);
    public TextureAtlasPosition? this[string textureCode] => ModelSystem?.GetAtlasPosition(CurrentModel, textureCode, entity);
     

    /*public IReadOnlyList<AppliedSkinnablePartVariant> AppliedSkinParts
    {
        get
        {
            appliedTemp.Clear();

            ITreeAttribute appliedTree = skintree.GetTreeAttribute("appliedParts");
            if (appliedTree == null) return appliedTemp;

            foreach (SkinnablePart part in AvailableSkinParts)
            {

                string code = appliedTree.GetString(part.Code);
                if (code != null && part.VariantsByCode.TryGetValue(code, out SkinnablePartVariant variant))
                {
                    appliedTemp.Add(variant.AppliedCopy(part.Code));
                }
            }

            return appliedTemp;
        }
    }*/


    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        ModelSystem = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();

        ModelSystem.LoadDefault(entity);

        AvailableSkinPartsByModelCodes = ModelSystem.SkinParts;
        AvailableSkinPartsArraysByModelCodes = ModelSystem.SkinPartsArrays;
        AvailableModels = ModelSystem.CustomModels;

        skintree = entity.WatchedAttributes.GetTreeAttribute("skinConfig");
        if (skintree == null)
        {
            entity.WatchedAttributes["skinConfig"] = skintree = new TreeAttribute();
        }

        string? skinModel = entity.WatchedAttributes.GetString("skinModel");
        if (skinModel == null)
        {
            entity.WatchedAttributes.SetString("skinModel", ModelSystem.DefaultModelCode);
            CurrentModel = ModelSystem.DefaultModelCode;
        }

        AvailableSkinPartsByCode = AvailableSkinPartsByModelCodes[CurrentModel];
        AvailableSkinParts = AvailableSkinPartsArraysByModelCodes[CurrentModel];

        mainTextureCode = properties.Attributes["mainTextureCode"].AsString("seraph");

        entity.WatchedAttributes.RegisterModifiedListener("skinModel", onSkinModelChanged);
        entity.WatchedAttributes.RegisterModifiedListener("skinConfig", onSkinConfigChanged);
        entity.WatchedAttributes.RegisterModifiedListener("voicetype", onVoiceConfigChanged);
        entity.WatchedAttributes.RegisterModifiedListener("voicepitch", onVoiceConfigChanged);

        if (entity.Api.Side == EnumAppSide.Server && AppliedSkinParts.Count == 0)
        {
            entity.Api.ModLoader.GetModSystem<CharacterSystem>().randomizeSkin(entity, null, false);
        }
        onVoiceConfigChanged();

        

        //AddMainTextures();

        ReplaceEntityShape();
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        if (DefaultSkinTexPos == null && entity.Properties.Client.Renderer is EntityShapeRenderer renderer)
        {
            DefaultSkinTexPos = renderer.skinTexPos;
        }

        AddMainTextures();

        entity.AnimManager.LoadAnimator(entity.World.Api, entity, entityShape, entity.AnimManager.Animator?.Animations, true);

        base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
    }

    public void SetCurrentModel(string code)
    {
        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModel = code;
        AvailableSkinPartsByCode = AvailableSkinPartsByModelCodes[CurrentModel];
        AvailableSkinParts = AvailableSkinPartsArraysByModelCodes[CurrentModel];
        mainTextureCode = ModelSystem?.MainTextureCodes[CurrentModel] ?? "seraph";
        ReplaceEntityShape();
        entity.MarkShapeModified();
    }

    public override ITexPositionSource? GetTextureSource(ref EnumHandling handling)
    {
        if (CurrentModel == ModelSystem?.DefaultModelCode) return null;

        handling = EnumHandling.PreventSubsequent;
        return this;
    }

    /*public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        // Make a copy so we don't mess up the original
        if (!shapeIsCloned)
        {
            Shape newShape = entityShape.Clone();
            entityShape = newShape;
            shapeIsCloned = true;
        }

        //var AppliedSkinParts = this.AppliedSkinParts;    // AppliedSkinParts.get_ is costly and we call this every frame! So we at least hold it locally and call it only once not twice. It clears and rebuilds the list each time from Dictionaries... // wtf? this is not called every fame

        foreach (AppliedSkinnablePartVariant? skinpart in AppliedSkinParts)
        {
            AvailableSkinPartsByCode.TryGetValue(skinpart.PartCode, out SkinnablePart part);

            if (part?.Type == EnumSkinnableType.Shape)
            {
                entityShape = addSkinPart(skinpart, entityShape, part.DisableElements, shapePathForLogging);
            }
        }

        foreach (AppliedSkinnablePartVariant? val in AppliedSkinParts)
        {
            AvailableSkinPartsByCode.TryGetValue(val.PartCode, out SkinnablePart part);

            if (part != null && part.Type == EnumSkinnableType.Texture && part.TextureTarget != null && part.TextureTarget != mainTextureCode)
            {
                AssetLocation textureLoc;
                if (part.TextureTemplate != null)
                {
                    textureLoc = part.TextureTemplate.Clone();
                    textureLoc.Path = textureLoc.Path.Replace("{code}", val.Code);
                }
                else
                {
                    textureLoc = val.Texture;
                }

                string code = "skinpart-" + part.TextureTarget;
                entityShape.TextureSizes.TryGetValue(code, out int[] sizes);
                if (sizes != null)
                {
                    loadTexture(entityShape, code, textureLoc, sizes[0], sizes[1], shapePathForLogging);
                }
                else
                {
                    entity.Api.Logger.Error("Skinpart has no textureSize: " + code + " in: " + shapePathForLogging);
                }
            }
        }

        EntityBehaviorTexturedClothing ebhtc = entity.GetBehavior<EntityBehaviorTexturedClothing>();
        InventoryBase inv = ebhtc.Inventory;
        if (inv != null)
        {
            foreach (ItemSlot? slot in inv)
            {
                if (slot.Empty) continue;

                if (ebhtc.hideClothing)
                {
                    continue;
                }

                ItemStack stack = slot.Itemstack;
                JsonObject attrObj = stack.Collectible.Attributes;

                entityShape.RemoveElements(attrObj?["disableElements"]?.AsArray<string>(null));
                string[]? keepEles = attrObj?["keepElements"]?.AsArray<string>(null);
                if (keepEles != null && willDeleteElements != null)
                {
                    foreach (string val in keepEles) willDeleteElements = willDeleteElements.Remove(val);
                }
            }
        }
    }*/

    public override void OnEntityLoaded()
    {
        init();
    }

    public override void OnEntitySpawn()
    {
        init();
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        EntityBehaviorTexturedClothing ebhtc = entity.GetBehavior<EntityBehaviorTexturedClothing>();
        if (ebhtc != null)
        {
            ebhtc.OnReloadSkin -= Essr_OnReloadSkin;
        }
    }

    /*public void selectSkinPart(string partCode, string variantCode, bool retesselateShape = true, bool playVoice = true)
    {
        AvailableSkinPartsByCode.TryGetValue(partCode, out var part);


        ITreeAttribute appliedTree = skintree.GetTreeAttribute("appliedParts");
        if (appliedTree == null) skintree["appliedParts"] = appliedTree = new TreeAttribute();
        appliedTree[partCode] = new StringAttribute(variantCode);


        if (part?.Type == EnumSkinnableType.Voice)
        {
            entity.WatchedAttributes.SetString(partCode, variantCode);

            if (partCode == "voicetype")
            {
                VoiceType = variantCode;
            }
            if (partCode == "voicepitch")
            {
                VoicePitch = variantCode;
            }

            ApplyVoice(VoiceType, VoicePitch, playVoice);
            return;
        }

        var essr = entity.Properties.Client.Renderer as EntityShapeRenderer;
        if (retesselateShape) essr?.TesselateShape();
        return;
    }*/

    public override string PropertyName() => "skinnableplayercustommodel";

    /*public void ApplyVoice(string voiceType, string voicePitch, bool testTalk)
    {
        if (!AvailableSkinPartsByCode.TryGetValue("voicetype", out var availVoices) || !AvailableSkinPartsByCode.TryGetValue("voicepitch", out var availPitches))
        {
            return;
        }

        VoiceType = voiceType;
        VoicePitch = voicePitch;

        if (entity is EntityPlayer plr && plr.talkUtil != null && voiceType != null)
        {

            if (!availVoices.VariantsByCode.ContainsKey(voiceType))
            {
                voiceType = availVoices.Variants[0].Code;
            }

            plr.talkUtil.soundName = availVoices.VariantsByCode[voiceType].Sound;

            float pitchMod = 1;
            switch (VoicePitch)
            {
                case "verylow": pitchMod = 0.6f; break;
                case "low": pitchMod = 0.8f; break;
                case "medium": pitchMod = 1f; break;
                case "high": pitchMod = 1.2f; break;
                case "veryhigh": pitchMod = 1.4f; break;
            }

            plr.talkUtil.pitchModifier = pitchMod;
            plr.talkUtil.chordDelayMul = 1.1f;

            if (testTalk)
            {
                plr.talkUtil.Talk(EnumTalkType.Idle);
            }
        }
    }*/

    

    protected CustomModelsSystem? ModelSystem;
    protected bool DidInit = false;
    protected TextureAtlasPosition? DefaultSkinTexPos;

    protected void init()
    {
        if (entity.World.Side != EnumAppSide.Client) return;

        if (!DidInit)
        {
            EntityShapeRenderer? essr = entity.Properties.Client.Renderer as EntityShapeRenderer;
            if (essr == null) throw new InvalidOperationException("The extra skinnable entity behavior requires the entity to use the Shape renderer.");

            EntityBehaviorTexturedClothing ebhtc = entity.GetBehavior<EntityBehaviorTexturedClothing>();
            if (ebhtc == null) throw new InvalidOperationException("The extra skinnable entity behavior requires the entity to have the TextureClothing entitybehavior.");

            ebhtc.OnReloadSkin += Essr_OnReloadSkin;
            DidInit = true;
        }
    }

    protected void onSkinConfigChanged()
    {
        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModel = entity.WatchedAttributes.GetString("skinModel");
        AvailableSkinPartsByCode = AvailableSkinPartsByModelCodes[CurrentModel];
        AvailableSkinParts = AvailableSkinPartsArraysByModelCodes[CurrentModel];
        mainTextureCode = ModelSystem?.MainTextureCodes[CurrentModel] ?? "seraph";
        ReplaceEntityShape();
        entity.MarkShapeModified();
    }

    protected void onVoiceConfigChanged()
    {
        VoiceType = entity.WatchedAttributes.GetString("voicetype");
        VoicePitch = entity.WatchedAttributes.GetString("voicepitch");
        ApplyVoice(VoiceType, VoicePitch, false);
    }

    protected void onSkinModelChanged()
    {
        skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModel = entity.WatchedAttributes.GetString("skinModel");
        AvailableSkinPartsByCode = AvailableSkinPartsByModelCodes[CurrentModel];
        AvailableSkinParts = AvailableSkinPartsArraysByModelCodes[CurrentModel];
        mainTextureCode = ModelSystem?.MainTextureCodes[CurrentModel] ?? "seraph";
        ReplaceEntityShape();
        entity.MarkShapeModified();
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

        textures[mainTextureCode].Baked.TextureSubId = textureSubId;
        textures["skinpart-" + mainTextureCode] = textures[mainTextureCode];
    }*/

    /*protected Shape addSkinPart(AppliedSkinnablePartVariant part, Shape entityShape, string[] disableElements, string shapePathForLogging)
    {
        SkinnablePart skinpart = AvailableSkinPartsByCode[part.PartCode];
        if (skinpart.Type == EnumSkinnableType.Voice)
        {
            entity.WatchedAttributes.SetString("voicetype", part.Code);
            return entityShape;
        }

        entityShape.RemoveElements(disableElements);

        ICoreAPI api = entity.World.Api;
        ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;
        AssetLocation shapePath;
        CompositeShape tmpl = skinpart.ShapeTemplate;

        if (part.Shape == null && tmpl != null)
        {
            shapePath = tmpl.Base.CopyWithPath("shapes/" + tmpl.Base.Path + ".json");
            shapePath.Path = shapePath.Path.Replace("{code}", part.Code);
        }
        else
        {
            shapePath = part.Shape.Base.CopyWithPath("shapes/" + part.Shape.Base.Path + ".json");
        }

        Shape partShape = Shape.TryGet(api, shapePath);
        if (partShape == null)
        {
            api.World.Logger.Warning("Entity skin shape {0} defined in entity config {1} not found or errored, was supposed to be at {2}. Skin part will be invisible.", shapePath, entity.Properties.Code, shapePath);
            return null;
        }

        string prefixcode = "skinpart";
        partShape.SubclassForStepParenting(prefixcode + "-");

        IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;
        entityShape.StepParentShape(partShape, shapePath.ToShortString(), shapePathForLogging, api.Logger, (texcode, loc) =>
        {
            if (capi == null) return;
            if (!textures.ContainsKey("skinpart-" + texcode) && skinpart.TextureRenderTo == null)
            {
                CompositeTexture cmpt = textures[prefixcode + "-" + texcode] = new CompositeTexture(loc);
                cmpt.Bake(api.Assets);
                capi.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _);
                cmpt.Baked.TextureSubId = textureSubid;
            }
        });
        return entityShape;
    }

    protected void loadTexture(Shape entityShape, string code, AssetLocation location, int textureWidth, int textureHeight, string shapePathForLogging)
    {
        if (entity.World.Side == EnumAppSide.Server) return;

        IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;
        ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;

        CompositeTexture cmpt = textures[code] = new CompositeTexture(location);
        cmpt.Bake(capi.Assets);
        if (!capi.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _, null, -1))
        {
            capi.Logger.Warning("Skin part shape {0} defined texture {1}, no such texture found.", shapePathForLogging, location);
        }
        cmpt.Baked.TextureSubId = textureSubid;

        entityShape.TextureSizes[code] = new int[] { textureWidth, textureHeight };
        textures[code] = cmpt;
    }*/

    protected void ReplaceEntityShape()
    {
        if (AvailableModels[CurrentModel] == null || entity.Properties.Client.Renderer is not EntityShapeRenderer renderer) return;

        renderer.OverrideEntityShape = AvailableModels[CurrentModel];
        renderer.skinTexPos = CurrentModel != ModelSystem.DefaultModelCode ? ModelSystem.GetAtlasPosition(CurrentModel, mainTextureCode, entity) : DefaultSkinTexPos;
        renderer.TesselateShape();

        entity.AnimManager.LoadAnimator(entity.World.Api, entity, renderer.OverrideEntityShape, entity.AnimManager.Animator?.Animations, true);

        if (entity.Properties.Attributes?["skinBaseTextureKey"].Token is JValue value)
        {
            value.Value = ModelSystem.MainTextureCodes[CurrentModel];
        }
    }

    protected void AddMainTextures()
    {
        foreach ((string modelCode, string textureCode) in ModelSystem.MainTextureCodes)
        {
            if (!ModelSystem.MainTextures.ContainsKey(modelCode)) return;
            
            CompositeTexture texture = ModelSystem.MainTextures[modelCode];

            entity.Properties.Client.Textures[textureCode] = texture;
        }
    }

    protected void Essr_OnReloadSkin(LoadedTexture atlas, TextureAtlasPosition skinTexPos, int textureSubId)
    {
        ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;

        foreach (var val in AppliedSkinParts)
        {
            SkinnablePart part = AvailableSkinPartsByCode[val.PartCode];

            if (part.Type != EnumSkinnableType.Texture) continue;
            if (part.TextureTarget != null && part.TextureTarget != mainTextureCode) continue;

            LoadedTexture texture = new LoadedTexture(capi);

            capi.Render.GetOrLoadTexture(val.Texture.Clone().WithPathAppendixOnce(".png"), ref texture);


            int posx = part.TextureRenderTo?.X ?? 0;
            int posy = part.TextureRenderTo?.Y ?? 0;

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

        var textures = entity.Properties.Client.Textures;

        textures[mainTextureCode].Baked.TextureSubId = textureSubId;
        textures["skinpart-" + mainTextureCode] = textures[mainTextureCode];
    }
}
