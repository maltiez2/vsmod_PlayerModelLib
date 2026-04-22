using PlayerModelLib.Utils;
using System.Collections.Immutable;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public delegate void BeforeWearableTesselatedDelegate(WearablesTesselatorBehavior tesselatorBehavior, IInventory inventory, ref ItemSlot slot, ref Shape entityShape, ref string[] willDeleteElements, ref bool skipSlot);
public delegate void BeforeWearableShapeAttachedDelegate(WearablesTesselatorBehavior tesselatorBehavior, IInventory inventory, ItemSlot slot, ref Shape entityShape, ref string[] willDeleteElements, ref Shape attachableShape, ref CompositeShape? attachableCompisteShape);
public delegate void OnTryGetTexturePositionDelegate(WearablesTesselatorBehavior tesselatorBehavior, string code, ref TextureAtlasPosition position);

public class WearablesTesselatorBehavior : EntityBehavior, ITexPositionSource
{
    public WearablesTesselatorBehavior(Entity entity) : base(entity)
    {
        PlayerEntity = entity as EntityPlayer ?? throw new InvalidOperationException("WearablesTesselator should be attached only to 'EntityPlayer'");
    }

    public static HashSet<string> InventoriesToProcess { get; protected set; } = [
        GlobalConstants.characterInvClassName,
        GlobalConstants.backpackInvClassName
    ];

    public static Dictionary<string, HashSet<int>> SlotsToProcess { get; protected set; } = new()
    {
        [GlobalConstants.backpackInvClassName] = [0, 1, 2, 3]
    };

    public override string PropertyName() => "";

    public readonly EntityPlayer PlayerEntity;

    public static event BeforeWearableTesselatedDelegate? BeforeWearableTesselated;
    public static event BeforeWearableShapeAttachedDelegate? BeforeWearableShapeAttached;
    public static event OnTryGetTexturePositionDelegate? OnTryGetTexturePosition;
    public readonly ThreadSafeDictionary<string, TextureAtlasPosition> WearableTextures = new([]);

    public bool TesselateItems
    {
        get => TesselateItemsValue.Value;
        set => TesselateItemsValue.Value = value;
    }

    public event OnTryGetTexturePositionDelegate? OnTryGetTexturePositionByInstance;

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        if (!shapeIsCloned)
        {
            entityShape = ShapeLoadingUtil.CloneShape(entityShape);
            shapeIsCloned = true;
        }

        /*if (entityShape.Textures != null && entity.Api is ICoreClientAPI clientApi)
        {
            foreach ((string code, AssetLocation? texturePath) in entityShape.Textures)
            {
                CompositeTexture texture = new(texturePath);

                AddTextureToAtlas(clientApi, code, texture);
            }
        }*/

        if (!TesselateItems)
        {
            return;
        }

        if (PlayerEntity.Player?.InventoryManager == null)
        {
            return;
        }

        foreach (string inventoryId in InventoriesToProcess)
        {
            IInventory? inventory = PlayerEntity.Player?.InventoryManager?.GetOwnInventory(inventoryId);
            if (inventory == null)
            {
                LoggerUtil.Error(entity.Api, this, $"Unable to get inventory with id '{inventoryId}' for player '{PlayerEntity.Player?.PlayerName}'");
                continue;
            }

            ProcessInventory(inventoryId, inventory, ref entityShape, ref willDeleteElements);
        }
    }

    public override ITexPositionSource GetTextureSource(ref EnumHandling handling)
    {
        handling = EnumHandling.Handled;
        return this;
    }


    protected const string DefaultSlotCode = "default";
    protected readonly ThreadSafeBool TesselateItemsValue = new(true);

    Size2i ITexPositionSource.AtlasSize => (entity.Api as ICoreClientAPI)?.EntityTextureAtlas.Size ?? new();
    TextureAtlasPosition ITexPositionSource.this[string textureCode]
    {
        get
        {
            if (WearableTextures.TryGetValue(textureCode, out TextureAtlasPosition? position))
            {
                OnTryGetTexturePosition?.Invoke(this, textureCode, ref position);
                OnTryGetTexturePositionByInstance?.Invoke(this, textureCode, ref position);

                return position;
            }

            return (entity.Api as ICoreClientAPI)?.EntityTextureAtlas[textureCode] ?? new();
        }
    }

    protected virtual void ProcessInventory(string inventoryId, IInventory inventory, ref Shape entityShape, ref string[] willDeleteElements)
    {
        ImmutableArray<ItemSlot> slots = [.. inventory];

        SlotsToProcess.TryGetValue(inventoryId, out HashSet<int>? slotsToProcess);

        foreach (ItemSlot slot in slots)
        {
            if (slotsToProcess != null && !slotsToProcess.Contains(inventory.GetSlotId(slot)))
            {
                continue;
            }

            ItemSlot slotToProcess = slot; // as separate variable to pass as a ref

            bool skipSlot = false;
            BeforeWearableTesselated?.Invoke(this, inventory, ref slotToProcess, ref entityShape, ref willDeleteElements, ref skipSlot);
            if (skipSlot)
            {
                continue;
            }

            if (slot.Empty)
            {
                continue;
            }

            ProcessSlot(slot, inventory, ref entityShape, ref willDeleteElements);
        }
    }
    protected virtual void ProcessSlot(ItemSlot slot, IInventory inventory, ref Shape entityShape, ref string[] willDeleteElements)
    {
        ItemStack? stack = slot.Itemstack;
        if (stack == null)
        {
            return;
        }

        IAttachableToEntity? attachable = IAttachableToEntity.FromCollectible(stack.Collectible);
        if (attachable == null)
        {
            return;
        }

        string prefix = GetPrefix(stack, attachable);
        Shape? attachableShape = GenerateAttachableShape(stack, attachable, prefix, DefaultSlotCode, out CompositeShape? compositeGearShape);
        if (attachableShape == null)
        {
            return;
        }

        string[] elementsToRemove = attachable.GetDisableElements(stack) ?? [];
        string[] elementsToKeep = attachable.GetKeepElements(stack) ?? [];
        willDeleteElements = willDeleteElements.Except(elementsToKeep).Concat(elementsToRemove).Distinct().ToArray();

        if (compositeGearShape != null && compositeGearShape.Overlays != null)
        {
            Result shapeOverlayResult = AttachShapeOverlays(attachableShape, compositeGearShape);
            shapeOverlayResult.LogErrorsAndWarnings(entity.Api, this);
            if (!shapeOverlayResult.IsSuccess)
            {
                LoggerUtil.Error(entity.Api, this, $"Failed to attach attachable shape overlays for collectible '{stack.Collectible.Code}'.");
                return;
            }
        }

        BeforeWearableShapeAttached?.Invoke(this, inventory, slot, ref entityShape, ref willDeleteElements, ref attachableShape, ref compositeGearShape);

        if (stack.Item.Textures != null)
        {
            foreach ((string textureCode, CompositeTexture texture) in stack.Item.Textures)
            {
                attachableShape.Textures[textureCode] = texture.Base;
            }
        }

        

        float damageEffectValue = GetDamageEffectValue(stack);
        attachableShape.ResolveReferences(entity.Api.Logger, $"WearablesTesselator.ProcessSlot for '{stack.Collectible.Code}'");
        ShapeLoadingUtil.PrefixTextures(attachableShape, prefix, damageEffectValue);
        ShapeLoadingUtil.PrefixAnimations(attachableShape, prefix);


        Result stepParentResult = ShapeLoadingUtil.StepParentShape(entityShape, attachableShape);
        stepParentResult.LogErrorsAndWarnings(entity.Api, this);
        if (!stepParentResult.IsSuccess)
        {
            LoggerUtil.Error(entity.Api, this, $"Failed to attach shape for collectible '{stack.Collectible.Code}'.");
            return;
        }

        if (entity.Api is not ICoreClientAPI clientApi)
        {
            return;
        }


        Dictionary<string, CompositeTexture> attachableTextures = entity.Properties?.Client?.Textures?.ToDictionary() ?? [];

        if (stack.Item.Textures != null)
        {
            foreach ((string textureCode, CompositeTexture texture) in stack.Item.Textures)
            {
                attachableTextures[prefix + textureCode] = texture.Clone();
            }
        }

        attachable.CollectTextures(stack, attachableShape, prefix, attachableTextures);

        foreach ((string textureCode, CompositeTexture texture) in attachableTextures)
        {
            AddTextureToAtlas(clientApi, textureCode, texture);
        }

        foreach ((string textureCode, AssetLocation? texturePath) in attachableShape.Textures)
        {
            if (texturePath == null || textureCode == "seraph")
            {
                continue;
            }

            CompositeTexture texture = new(texturePath);

            AddTextureToAtlas(clientApi, textureCode, texture);
        }
    }

    protected virtual void AddTextureToAtlas(ICoreClientAPI api, string textureCode, CompositeTexture texture)
    {
        CompositeTexture compositeTexture = new(texture.Base);

        compositeTexture.Bake(api.Assets);

        ThreadSafeUtils.InsertTextureIntoAtlas(compositeTexture, api, entity, onInsert: (textureSubId, position) =>
        {
            WearableTextures.SetValue(textureCode, position);
        });
    }
    protected virtual float GetDamageEffectValue(ItemStack stack)
    {
        float damageEffect = 0;
        if (stack.ItemAttributes?["visibleDamageEffect"].AsBool() == true)
        {
            damageEffect = Math.Max(0, 1 - (float)stack.Collectible.GetRemainingDurability(stack) / stack.Collectible.GetMaxDurability(stack) * 1.1f);
        }
        return damageEffect;
    }
    protected virtual string GetPrefix(ItemStack stack, IAttachableToEntity attachable)
    {
        string prefix = attachable.GetTexturePrefixCode(stack) ?? "";
        return prefix;
    }
    protected virtual Shape? GenerateAttachableShape(ItemStack stack, IAttachableToEntity attachable, string prefix, string slotCode, out CompositeShape? compositeGearShape)
    {
        if (stack.Collectible is IWearableShapeSupplier wearableShapeSupplier)
        {
            Shape wearableShape = wearableShapeSupplier.GetShape(stack, PlayerEntity, prefix);
            if (wearableShape != null)
            {
                compositeGearShape = null;
                return wearableShape;
            }
        }

        compositeGearShape = attachable.GetAttachedShape(stack, slotCode);
        if (compositeGearShape == null || compositeGearShape.Base == null)
        {
            return null;
        }

        AssetLocation shapePath = compositeGearShape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
        Shape? gearShape = ShapeLoadingUtil.LoadShape(entity.Api, shapePath);
        if (gearShape == null)
        {
            LoggerUtil.Warn(entity.Api, this, $"Cant load attachable wearable shape by path '{shapePath}' for item '{stack.Collectible.Code}'");
            return null;
        }

        return gearShape;
    }
    protected virtual Result AttachShapeOverlays(Shape attachableShape, CompositeShape compositeGearShape)
    {
        Result result = Result.Success();
        foreach (CompositeShape? overlay in compositeGearShape.Overlays)
        {
            if (overlay == null)
            {
                continue;
            }

            if (overlay.Base != null)
            {
                result &= AttachShapeOverlay(attachableShape, overlay.Base);
            }
        }
        return result;
    }
    protected virtual Result AttachShapeOverlay(Shape attachableShape, AssetLocation overlayShapePath)
    {
        Shape? overlayShape = ShapeLoadingUtil.LoadShape(entity.Api, overlayShapePath);
        if (overlayShape == null)
        {
            return Result.Error($"Shape '{overlayShapePath}' was not found when attaching shape overlay.");
        }

        return ShapeLoadingUtil.StepParentShape(attachableShape, overlayShape);
    }
}
