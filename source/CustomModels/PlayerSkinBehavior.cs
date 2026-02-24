using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class PlayerSkinBehavior : EntityBehavior, ITexPositionSource
{
    public delegate void OnWearableItemProcessingDelegate(Entity player, PlayerSkinBehavior behavior, ref ItemSlot slot, ref string[]? disableElements, ref string[]? keepElements);

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

    public bool Initialized { get; protected set; }

    public readonly ThreadSafeDictionary<string, SkinnablePart> AvailableSkinPartsByCode = new([]);
    public readonly ThreadSafeList<SkinnablePart> AvailableSkinParts = new([]);
    public readonly ThreadSafeList<AppliedSkinnablePartVariant> AppliedSkinParts = new([]);
    public readonly ThreadSafeDictionary<string, TextureAtlasPosition> OverlayTextures = new([]);

    public string VoiceType { get; set; } = "altoflute";
    public string VoicePitch { get; set; } = "medium";
    public string MainTextureCode { get; set; } = "";

    public event Action? OnActuallyInitialize;

    public event Action<string>? OnModelChanged;

    public event Action<Shape>? OnShapeTesselated;

    public static event OnWearableItemProcessingDelegate? OnWearableItemProcessing;


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

        if (entity.Api.Side == EnumAppSide.Server)
        {
            RemoveNotExistingTraits();
        }

        SkinRandomizerConstraints = entity.Api.Assets.Get("config/seraphrandomizer.json").ToObject<SeraphRandomizerConstraints>();
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        WearablesTesselator = entity.GetBehavior<WearablesTesselatorBehavior>();
        if (WearablesTesselator != null)
        {
            WearablesTesselator.OnTryGetTexturePositionByInstance += ReplaceOverlay;
        }
    }

    public virtual void ActuallyInitialize()
    {
        if (Initialized) return;

        if (ModelSystem == null) return;

        SkinTree = entity.WatchedAttributes.GetTreeAttribute("skinConfig");
        if (SkinTree == null)
        {
            entity.WatchedAttributes["skinConfig"] = SkinTree = new TreeAttribute();
        }

        string skinModel = GetPlayerModelAttributeValue();

        if (entity.Api.Side == EnumAppSide.Client)
        {
            Debug.WriteLine($"Client: {skinModel}");
        }

        if (!ModelSystem.CustomModels.ContainsKey(skinModel) && entity.Api.ModLoader.IsModEnabled("customplayermodel"))
        {
            LoggerUtil.Notify(entity.Api, this, $"(player: {(entity as EntityPlayer)?.GetName()}) Custom model with code '{skinModel}' was not found. Probably was not yet received from player. Will reset model to default until the model is received.");

            TempModelCode = skinModel;
            TempSkinConfig = SkinTree.Clone();

            CurrentModelCode = ModelSystem.DefaultModelCode;
            AvailableSkinPartsByCode.Set(CurrentModel.SkinParts);
            AvailableSkinParts.Set(CurrentModel.SkinPartsArray);
            SetModelAttribute(CurrentModelCode);
            OnVoiceConfigChanged();
            OnSkinModelChanged();

            if (entity.Api.Side == EnumAppSide.Server && !GetAppliedSkinParts().Any())
            {
                RandomizeSkin(entity, [], false);
            }

            ModelSystem.OnCustomModelHotLoaded += (code) =>
            {
                if (!Initialized && TempModelCode == code)
                {
                    LoggerUtil.Notify(entity.Api, this, $"(player: {(entity as EntityPlayer)?.GetName()}) A custom model '{code}' was hot loaded, trying to restore original model.");
                    ActuallyInitialize();
                }
            };
            return;
        }

        if (entity.Api.Side == EnumAppSide.Server && TempModelCode != null)
        {
            if (!ModelSystem.CustomModels.ContainsKey(TempModelCode))
            {
                return;
            }

            CurrentModelCode = TempModelCode;
            SetModelAttribute(TempModelCode);
            skinModel = TempModelCode;
            TempModelCode = null;
        }

        if (entity.Api.Side == EnumAppSide.Server && TempSkinConfig != null)
        {
            entity.WatchedAttributes.SetAttribute("skinConfig", TempSkinConfig);
            entity.WatchedAttributes.MarkPathDirty("skinConfig");
            TempSkinConfig = null;
        }

        if (skinModel == null || !ModelSystem.CustomModels.ContainsKey(skinModel))
        {
            SetModelAttribute(ModelSystem.DefaultModelCode);
            CurrentModelCode = ModelSystem.DefaultModelCode;
            AvailableSkinPartsByCode.Set(CurrentModel.SkinParts);
            AvailableSkinParts.Set(CurrentModel.SkinPartsArray);
        }
        else
        {
            CurrentModelCode = skinModel;
        }

        entity.WatchedAttributes.RegisterModifiedListener("skinModel", OnSkinModelAttrChanged);
        entity.WatchedAttributes.RegisterModifiedListener("entitySize", OnModelSizeAttrChanged);
        entity.WatchedAttributes.RegisterModifiedListener("skinConfig", OnSkinConfigChanged);
        entity.WatchedAttributes.RegisterModifiedListener("voicetype", OnVoiceConfigChanged);
        entity.WatchedAttributes.RegisterModifiedListener("voicepitch", OnVoiceConfigChanged);

        OnVoiceConfigChanged();

        OnSkinModelChanged();

        if (entity.Api.Side == EnumAppSide.Server && !GetAppliedSkinParts().Any())
        {
            RandomizeSkin(entity, [], false);
        }

        Initialized = true;

        OnActuallyInitialize?.Invoke();
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[]? willDeleteElements)
    {
        if (ModelSystem == null || ClientApi == null || !ModelSystem.ModelsLoaded) return;

        Shape? newShape = Tesselate(shapePathForLogging, ref willDeleteElements);
        if (newShape != null)
        {
            entityShape = newShape;
            shapeIsCloned = true;
            return;
        }

        OnShapeTesselated?.Invoke(entityShape);
    }

    public void SetCurrentModel(string code, float size)
    {
        SkinTree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = code;
        CurrentSize = size;
        AvailableSkinPartsByCode.Set(CurrentModel.SkinParts);
        AvailableSkinParts.Set(CurrentModel.SkinPartsArray);
        ReplaceEntityShape();
        ApplyTraitAttributes(CurrentModelCode);
    }

    public void ApplyTraitAttributesWithModelTraits(string modelCode) => ApplyTraitAttributes(modelCode);

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

        EntityBehaviorBreathe? breathBehavior = entity.GetBehavior<EntityBehaviorBreathe>();
        if (breathBehavior != null)
        {
            breathBehavior.MaxOxygen /= PreviousMaxOxygen;
            PreviousMaxOxygen = CurrentModel.MaxOxygenFactor;
            breathBehavior.MaxOxygen *= PreviousMaxOxygen;
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

        EntityBehaviorPlayerPhysics? physicsBehavior = entity.GetBehavior<EntityBehaviorPlayerPhysics>();
        if (physicsBehavior != null)
        {
            physicsBehavior.StepHeight /= PreviousStepHeight;
            PreviousStepHeight = CurrentModel.StepHeight / DefaultStepHeight;
            physicsBehavior.StepHeight *= PreviousStepHeight;

            Cuboidf sneakTestCollisionbox = new Cuboidf(new(-entity.Properties.CollisionBoxSize.X / 2, 0, -entity.Properties.CollisionBoxSize.X / 2), new(entity.Properties.CollisionBoxSize.X / 2, entity.Properties.CollisionBoxSize.Y, entity.Properties.CollisionBoxSize.X / 2)).OmniNotDownGrowBy(-entity.Properties.CollisionBoxSize.X / 10f);
            sneakTestCollisionbox.Y2 /= 2;
            EntityBehaviorControlledPhysics_sneakTestCollisionbox?.SetValue(physicsBehavior, sneakTestCollisionbox);
        }

        ChangeTags();

        SetZNear();
    }

    public IReadOnlyList<AppliedSkinnablePartVariant> GetAppliedSkinParts()
    {
        ITreeAttribute? appliedTree = SkinTree?.GetTreeAttribute("appliedParts");
        if (appliedTree == null)
        {
            AppliedSkinParts.Set([]);
            return [];
        }

        List<AppliedSkinnablePartVariant> appliedSkinParts = [];
        foreach (SkinnablePart part in AvailableSkinParts.Get())
        {
            string code = appliedTree.GetString(part.Code);
            if (code != null && part.VariantsByCode.TryGetValue(code, out SkinnablePartVariant? variant))
            {
                appliedSkinParts.Add(variant.AppliedCopy(part.Code));
            }
        }

        AppliedSkinParts.Set(appliedSkinParts);

        return appliedSkinParts;
    }

    public virtual void SelectSkinPart(string partCode, string variantCode, bool retesselateShape = true, bool playVoice = true)
    {
        AvailableSkinPartsByCode.TryGetValue(partCode, out SkinnablePart? part);

        if (SkinTree == null)
        {
            return;
        }

        ITreeAttribute? appliedTree = SkinTree.GetTreeAttribute("appliedParts");
        if (appliedTree == null)
        {
            appliedTree = new TreeAttribute();
            SkinTree["appliedParts"] = appliedTree;
        }
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

        EntityShapeRenderer? renderer = entity.Properties.Client.Renderer as EntityShapeRenderer;
        if (retesselateShape)
        {
            renderer?.TesselateShape();
        }
    }

    public virtual void ApplyVoice(string voiceType, string voicePitch, bool testTalk)
    {
        if (!AvailableSkinPartsByCode.TryGetValue("voicetype", out SkinnablePart? availVoices) || !AvailableSkinPartsByCode.TryGetValue("voicepitch", out SkinnablePart? availPitches))
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
    }

    public bool RandomizeSkin(Entity entity, Dictionary<string, string> preSelection, bool playVoice = true)
    {
        bool mustached = entity.Api.World.Rand.NextDouble() < 0.3;

        Dictionary<string, RandomizerConstraint> currentConstraints = new();

        foreach (SkinnablePart skinpart in AvailableSkinParts.Get())
        {
            SkinnablePartVariant[] variants = skinpart.Variants.Where(v => v.Category == "standard").ToArray();

            int index = entity.Api.World.Rand.Next(variants.Length);

            if (preSelection.TryGetValue(skinpart.Code, out string? variantCode))
            {
                index = variants.IndexOf(val => val.Code == variantCode);
            }
            else
            {
                if (currentConstraints.TryGetValue(skinpart.Code, out RandomizerConstraint? partConstraints))
                {
                    variantCode = partConstraints.SelectRandom(entity.Api.World.Rand, variants);
                    index = variants.IndexOf(val => val.Code == variantCode);
                }

                if ((skinpart.Code == "mustache" || skinpart.Code == "beard") && !mustached)
                {
                    index = 0;
                    variantCode = "none";
                }
            }

            if (variantCode == null) variantCode = variants[index].Code;

            SelectSkinPart(skinpart.Code, variantCode, true, playVoice);

            if (SkinRandomizerConstraints == null)
            {
                SkinRandomizerConstraints = entity.Api.Assets.Get("config/seraphrandomizer.json").ToObject<SeraphRandomizerConstraints>();
            }

            if (SkinRandomizerConstraints.Constraints.TryGetValue(skinpart.Code, out Dictionary<string, Dictionary<string, RandomizerConstraint>>? partConstraintsGroup) &&
                partConstraintsGroup.TryGetValue(variantCode, out Dictionary<string, RandomizerConstraint>? constraints))
            {
                foreach (KeyValuePair<string, RandomizerConstraint> val in constraints)
                {
                    currentConstraints[val.Key] = val.Value;
                }
            }

            if (skinpart.Code == "voicetype" && variantCode == "high") mustached = false;
        }

        return true;
    }


    protected static readonly FieldInfo? EntityBehaviorControlledPhysics_sneakTestCollisionbox = typeof(EntityBehaviorControlledPhysics).GetField("sneakTestCollisionbox", BindingFlags.NonPublic | BindingFlags.Instance);
    protected CustomModelsSystem? ModelSystem;
    protected ICoreClientAPI? ClientApi;
    protected readonly ThreadSafeDictionary<string, TextureAtlasPosition?> OverlaysTexturePositions = new([]);
    protected readonly ThreadSafeDictionary<string, BlendedOverlayTexture[]> OverlaysByTextures = new([]);
    protected TagSetFast PreviousAddedTags = TagSetFast.Empty;
    protected TagSetFast PreviousRemovedTags = TagSetFast.Empty;
    protected const float DefaultStepHeight = 0.6f;
    protected float DefaultSize = 1;
    protected float PreviousHeadBobbingAmplitudeFactor = 1;
    protected float PreviousZNearFactor = 1;
    protected float PreviousStepHeight = 1;
    protected float PreviousMaxOxygen = 1;
    protected float DefaultEyeHeight = 1.7f;
    protected ITreeAttribute? TempSkinConfig;
    protected string? TempModelCode;
    protected ITreeAttribute? SkinTree;
    protected SeraphRandomizerConstraints? SkinRandomizerConstraints;
    protected WearablesTesselatorBehavior? WearablesTesselator;


    protected void SetModelAttribute(string code)
    {
        if (entity.Api.Side == EnumAppSide.Server)
        {
            entity.WatchedAttributes.SetString("skinModel", code);
            entity.WatchedAttributes.MarkPathDirty("skinModel");
        }
    }

    protected Shape? Tesselate(string shapePathForLogging, ref string[]? willDeleteElements)
    {
        if (ModelSystem == null || ClientApi == null || !ModelSystem.ModelsLoaded) return null;

        try
        {
            Shape entityShape = ShapeLoadingUtil.CloneShape(ModelSystem.CustomModels[CurrentModelCode].Shape);

            AddMainTextures();
            AddSkinParts(ref entityShape, shapePathForLogging, ref willDeleteElements);
            AddSkinPartsTextures(ClientApi, entityShape, shapePathForLogging);
            RemoveHiddenElements(entityShape, ref willDeleteElements);

            entityShape.CollectAndResolveReferences(entity.Api.Logger, shapePathForLogging);

            return entityShape;
        }
        catch (Exception exception)
        {
            LoggerUtil.Error(ClientApi, this, $"({CurrentModelCode}) Error when tesselating custom player model:\n{exception}");
        }

        return null;
    }

    protected void OnSkinConfigChanged()
    {
        if (GuiDialogCreateCustomCharacter.DialogOpened) return;

        if (ModelSystem?.ModelsLoaded != true) return;

        SkinTree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;

        string modelCode = GetPlayerModelAttributeValue();
        if (modelCode != CurrentModelCode)
        {
            CurrentModelCode = modelCode;
            AvailableSkinPartsByCode.Set(CurrentModel.SkinParts);
            AvailableSkinParts.Set(CurrentModel.SkinPartsArray);
            ReplaceEntityShape();
        }
    }

    protected void OnVoiceConfigChanged()
    {
        if (GuiDialogCreateCustomCharacter.DialogOpened) return;

        if (ModelSystem?.ModelsLoaded != true) return;

        string? voiceType = entity.WatchedAttributes.GetString("voicetype");
        string? voicePitch = entity.WatchedAttributes.GetString("voicepitch");

        VoiceType = voiceType;
        VoicePitch = voicePitch;
        ApplyVoice(VoiceType, VoicePitch, false);
    }

    protected void OnSkinModelAttrChanged()
    {
        if (GuiDialogCreateCustomCharacter.DialogOpened) return;

        string modelCode = GetPlayerModelAttributeValue();
        if (modelCode != CurrentModelCode)
        {
            OnSkinModelChanged();
        }
    }

    protected void OnModelSizeAttrChanged()
    {
        if (GuiDialogCreateCustomCharacter.DialogOpened) return;

        float size = entity.WatchedAttributes.GetFloat("entitySize");
        if (size != CurrentSize)
        {
            OnSkinModelChanged();
        }
    }

    protected void OnSkinModelChanged()
    {
        if (ModelSystem?.ModelsLoaded != true) return;

        SkinTree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
        CurrentModelCode = GetPlayerModelAttributeValue();
        CurrentSize = entity.WatchedAttributes.GetFloat("entitySize");
        if (!ModelSystem.CustomModels.ContainsKey(CurrentModelCode))
        {
            CurrentModelCode = ModelSystem.DefaultModelCode;
            SetModelAttribute(CurrentModelCode);
            return;
        }
        AvailableSkinPartsByCode.Set(CurrentModel.SkinParts);
        AvailableSkinParts.Set(CurrentModel.SkinPartsArray);
        ReplaceEntityShape();
        ApplyTraitAttributes(CurrentModelCode);

        if (entity is EntityPlayer player)
        {
            ModelSystem.CustomModelChanged(CurrentModelCode, player.Player, this);
        }
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

        TagSetFast currentTags = entity.Tags;

        currentTags &= ~PreviousAddedTags;
        currentTags |= PreviousRemovedTags;

        PreviousAddedTags = CurrentModel.AddTags & ~currentTags;
        PreviousRemovedTags = CurrentModel.RemoveTags & currentTags;

        currentTags &= ~PreviousRemovedTags;
        currentTags |= PreviousAddedTags;

        entity.Tags = currentTags;
        entity.MarkTagsDirty();
    }

    protected virtual void RemoveNotExistingTraits()
    {
        CharacterSystem? characterSystem = entity.Api.ModLoader.GetModSystem<CharacterSystem>();
        EntityPlayer? player = entity as EntityPlayer;

        if (characterSystem == null || player == null) return;

        IEnumerable<string> extraTraits = player.WatchedAttributes.GetStringArray("extraTraits", []);
        IEnumerable<string> removedTraits = extraTraits.Where(trait => !characterSystem.TraitsByCode.ContainsKey(trait));
        IEnumerable<string> newTraits = extraTraits.Where(characterSystem.TraitsByCode.ContainsKey);

        if (!removedTraits.Any()) return;

        string removedTraitsMessage = removedTraits.Aggregate((a, b) => $"{a}, {b}");
        LoggerUtil.Warn(player.Api, this, $"Removed traits that no longer exist from player '{player.Player?.PlayerName ?? player.GetName()}': {removedTraitsMessage}");

        player.WatchedAttributes.SetStringArray("extraTraits", newTraits.ToArray());
    }

    protected virtual void AddMainTextures()
    {
        if (ModelSystem == null) return;

        foreach ((_, CustomModelData? data) in ModelSystem.CustomModels)
        {
            foreach (string code in data.MainTextureCodes)
            {
                entity.Properties.Client.Textures[code] = data.MainTextures[code];
            }
        }
    }

    protected virtual void AddSkinParts(ref Shape entityShape, string shapePathForLogging, ref string[]? willDeleteElements)
    {
        foreach (AppliedSkinnablePartVariant? skinPart in GetAppliedSkinParts())
        {
            AvailableSkinPartsByCode.TryGetValue(skinPart.PartCode, out SkinnablePart? part);

            if (part?.Type == EnumSkinnableType.Shape)
            {
                string[]? disabledElements = null;
                (part as SkinnablePartExtended)?.DisableElementsByVariantCode.TryGetValue(skinPart.Code, out disabledElements);
                disabledElements = disabledElements?.Concat(part.DisableElements ?? []).ToArray() ?? [];

                if (disabledElements.Length > 0)
                {
                    willDeleteElements ??= [];
                    willDeleteElements = willDeleteElements.Concat(disabledElements).ToArray();
                }

                entityShape = AddSkinPart(skinPart, entityShape, disabledElements, shapePathForLogging);
            }
        }
    }

    protected virtual void CollectDisabledElements(ref string[]? willDeleteElements)
    {
        foreach (AppliedSkinnablePartVariant? skinPart in GetAppliedSkinParts())
        {
            AvailableSkinPartsByCode.TryGetValue(skinPart.PartCode, out SkinnablePart? part);

            if (part?.Type == EnumSkinnableType.Shape)
            {
                string[]? disabledElements = null;
                (part as SkinnablePartExtended)?.DisableElementsByVariantCode.TryGetValue(skinPart.Code, out disabledElements);
                disabledElements = disabledElements?.Concat(part.DisableElements ?? []).ToArray() ?? [];

                if (disabledElements.Length > 0)
                {
                    willDeleteElements ??= [];
                    willDeleteElements = willDeleteElements.Concat(disabledElements).ToArray();
                }
            }
        }
    }

    protected virtual void AddSkinPartsTextures(ICoreClientAPI api, Shape entityShape, string shapePathForLogging)
    {
        OverlaysByTextures.Set([]);

        foreach (AppliedSkinnablePartVariant? skinPart in GetAppliedSkinParts())
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

        foreach (string code in OverlaysByTextures.Get().Keys)
        {
            ApplyOverlayTexture(api, entityShape, code);
        }
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
        disableElements = [];
        keepElements = [];

        OnWearableItemProcessing?.Invoke(entity, this, ref slot, ref disableElements, ref keepElements);

        ItemStack? stack = slot.Itemstack;
        if (stack == null)
        {
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

        Shape? partShape = ShapeLoadingUtil.LoadShape(ClientApi, shapePath);
        if (partShape == null)
        {
            ClientApi.World.Logger.Warning("Entity skin shape {0} defined in entity config {1} not found or errored, was supposed to be at {2}. Skin part will be invisible.", shapePath, entity.Properties.Code, shapePath);
            return entityShape;
        }

        string prefixCode = CustomModelsSystem.GetSkinPartTexturePrefix(CurrentModelCode, skinPart.Code);

        ShapeLoadingUtil.PrefixTextures(partShape, prefixCode);

        foreach ((string code, int[] size) in partShape.TextureSizes)
        {
            entityShape.TextureSizes[code] = size;
        }
        foreach ((string code, AssetLocation texturePath) in partShape.Textures)
        {
            entityShape.Textures[prefixCode + code] = texturePath;
        }
        foreach ((string textureCode, AssetLocation? texturePath) in partShape.Textures)
        {
            CompositeTexture compositeTexture = new(texturePath);

            ThreadSafeUtils.InsertTextureIntoAtlas(compositeTexture, ClientApi, entity, onInsert: (textureSubId, position) =>
            {
                WearablesTesselator?.WearableTextures.SetValue(textureCode, position);
            });
        }
        ShapeLoadingUtil.StepParentShape(entityShape, partShape);

        return entityShape;
    }

    protected virtual void ReplaceTexture(ICoreClientAPI api, Shape entityShape, string code, AssetLocation location, int textureWidth, int textureHeight, string shapePathForLogging)
    {
        IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;

        CompositeTexture compositeTexture = new(location);
        textures[code] = compositeTexture;

        compositeTexture.Bake(api.Assets);
        entityShape.TextureSizes[code] = [textureWidth, textureHeight];
        textures[code] = compositeTexture;

        ThreadSafeUtils.InsertTextureIntoAtlas(compositeTexture, api, entity, onInsert: (textureSubId, position) =>
        {
            WearablesTesselator?.WearableTextures.SetValue(code, position);
        });
    }

    protected virtual void AddOverlayTexture(string code, AssetLocation overlayTextureLocation, EnumColorBlendMode overlayMode)
    {
        CompositeTexture overlayTexture = new(overlayTextureLocation);

        if (OverlaysByTextures.TryGetValue(code, out BlendedOverlayTexture[]? overlayTextures))
        {
            OverlaysByTextures.SetValue(code, overlayTextures.Append(new BlendedOverlayTexture() { Base = overlayTexture.Base, BlendMode = overlayMode }));
            return;
        }

        OverlaysByTextures.SetValue(code, [new BlendedOverlayTexture() { Base = overlayTexture.Base, BlendMode = overlayMode }]);
    }

    protected virtual void ApplyOverlayTexture(ICoreClientAPI api, Shape entityShape, string code)
    {
        IDictionary<string, CompositeTexture?> textures = entity.Properties.Client.Textures;

        if (!textures.TryGetValue(code, out CompositeTexture? baseTexture))
        {
            return;
        }

        CompositeTexture? clonedBaseTexture = baseTexture?.Clone();

        if (clonedBaseTexture == null)
        {
            LoggerUtil.Error(api, this, $"({CurrentModelCode}) Texture '{code}' in null, cant apply overlays to it, will skip.");
            return;
        }

        clonedBaseTexture.BlendedOverlays = OverlaysByTextures.GetValue(code);

        ThreadSafeUtils.InsertTextureIntoAtlas(clonedBaseTexture, api, entity, onInsert: (textureSubId, position) =>
        {
            OverlayTextures.SetValue(code, position);
        });
    }

    protected virtual void SetZNear()
    {
        if (entity.Api is not ICoreClientAPI clientApi) return;

        float factor = (float)entity.LocalEyePos.Y / DefaultEyeHeight;

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

    protected void ApplyTraitAttributes(string modelCode)
    {
        EntityPlayer? eplr = entity as EntityPlayer;
        CharacterSystem? __instance = eplr?.Api.ModLoader.GetModSystem<CharacterSystem>();
        CustomModelsSystem? modelSystem = eplr?.Api.ModLoader.GetModSystem<CustomModelsSystem>();

        string? classCode = eplr?.WatchedAttributes.GetString("characterClass");
        if (eplr == null || __instance == null || classCode == null || classCode == "") return;
        CharacterClass? characterClass = __instance.characterClasses?.Find(c => c.Code == classCode);

        if (characterClass == null)
        {
            LoggerUtil.Error(entity.Api, this, $"Character class with code '{classCode}' not found when trying to apply class traits for player '{eplr.Player?.PlayerName ?? eplr.GetName()}'.");
            return;
        }

        // Reset 
        foreach ((_, EntityFloatStats stats) in eplr.Stats)
        {
            foreach ((string stat, _) in stats.ValuesByKey)
            {
                if (stat == "trait")
                {
                    stats.Remove(stat);
                    break;
                }
            }
        }

        string[] extraModelTraits = modelSystem?.CustomModels[modelCode].ExtraTraits ?? [];
        string[] extraTraits = eplr.WatchedAttributes.GetStringArray("extraTraits") ?? [];
        IEnumerable<string> allTraits = extraTraits == null ? characterClass.Traits : characterClass.Traits.Concat(extraModelTraits).Concat(extraTraits).Distinct();

        // Aggregate stats values
        Dictionary<string, double> statValues = [];
        foreach (string traitCode in allTraits)
        {
            if (!__instance.TraitsByCode.TryGetValue(traitCode, out Trait? trait)) continue;

            foreach ((string attributeCode, double attributeValue) in trait.Attributes)
            {
                if (statValues.ContainsKey(attributeCode))
                {
                    statValues[attributeCode] += attributeValue;
                }
                else
                {
                    statValues[attributeCode] = attributeValue;
                }
            }
        }

        // Apply aggregated values
        foreach ((string stat, double value) in statValues)
        {
            eplr.Stats.Set(stat, "trait", (float)value, true);
        }

        eplr.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
    }

    protected virtual void ReplaceOverlay(WearablesTesselatorBehavior tesselatorBehavior, string code, ref TextureAtlasPosition position)
    {
        if (OverlayTextures.TryGetValue(code, out TextureAtlasPosition? overlayPosition))
        {
            position = overlayPosition;
        }
    }
}
