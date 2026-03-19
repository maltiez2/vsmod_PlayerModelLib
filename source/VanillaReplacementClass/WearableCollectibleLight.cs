using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace PlayerModelLib;

public delegate void OnWearableCollectibleLightAppliedDelegate(WearableCollectibleLightBehavior wearableLightBehavior, IInventory inventory, ItemSlot slot, ref byte[] britestLight, ref byte[] slotLight);
public delegate void AfterWearableCollectibleLightAppliedDelegate(WearableCollectibleLightBehavior wearableLightBehavior);

public class WearableCollectibleLightBehavior : EntityBehavior
{
    public WearableCollectibleLightBehavior(Entity entity) : base(entity)
    {
        PlayerEntity = entity as EntityPlayer ?? throw new InvalidOperationException("WearableCollectibleLightBehavior should be attached only to 'EntityPlayer'");
    }

    public static event OnWearableCollectibleLightAppliedDelegate? OnWearableCollectibleLightApplied;
    public static event AfterWearableCollectibleLightAppliedDelegate? AfterWearableCollectibleLightApplied;

    public readonly EntityPlayer PlayerEntity;
    public readonly HashSet<string> InventoriesToProcess = [
        GlobalConstants.characterInvClassName,
        GlobalConstants.backpackInvClassName
    ];
    public readonly Dictionary<string, List<int>> InventoriesSlotsToProcess = new()
    {
         [GlobalConstants.backpackInvClassName] = [0, 1, 2, 3]
    };

    public override string PropertyName() => "";

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        ResetEntityLight();

        byte[] brightestLight = [0, 0, 0];
        foreach (string inventoryId in InventoriesToProcess)
        {
            IInventory? inventory = PlayerEntity.Player?.InventoryManager?.GetOwnInventory(inventoryId);
            if (inventory == null)
            {
                LoggerUtil.Error(entity.Api, this, $"Unable to get inventory with id '{inventoryId}' for player '{PlayerEntity.Player?.PlayerName}'");
                continue;
            }

            foreach (ItemSlot? slot in inventory)
            {
                if (slot?.Itemstack?.Collectible == null)
                {
                    continue;
                }

                if (InventoriesSlotsToProcess.ContainsKey(inventoryId) && !InventoriesSlotsToProcess[inventoryId].Contains(inventory.GetSlotId(slot)))
                {
                    continue;
                }
                
                byte[] slotLight = slot.Itemstack.Collectible.GetLightHsv(entity.World.BlockAccessor, null, slot.Itemstack) ?? [0, 0, 0];
                if (slotLight.Length != 3)
                {
                    continue;
                }

                OnWearableCollectibleLightApplied?.Invoke(this, inventory, slot, ref brightestLight, ref slotLight);

                if (slotLight[2] > brightestLight[2])
                {
                    brightestLight = slotLight;
                }
            }
        }

        if (entity.LightHsv == null || entity.LightHsv.Length != 3)
        {
            entity.LightHsv = brightestLight;
        }
        else if (entity.LightHsv[2] < brightestLight[2])
        {
            entity.LightHsv = brightestLight;
        }

        AfterWearableCollectibleLightApplied?.Invoke(this);
    }

    public virtual void ResetEntityLight()
    {
        entity.LightHsv = [0, 0, 0];
    }
}
