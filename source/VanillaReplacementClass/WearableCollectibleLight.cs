using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace PlayerModelLib;

public delegate void OnWearableCollectibleLightAppliedDelegate(WearablesTesselatorBehavior tesselatorBehavior, ref IInventory inventory, ref ItemSlot slot);

public class WearableCollectibleLightBehavior : EntityBehavior
{
    public WearableCollectibleLightBehavior(Entity entity) : base(entity)
    {
    }

    public static event OnWearableCollectibleLightAppliedDelegate? OnWearableCollectibleLightApplied;

    public override string PropertyName() => "";

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        /*
            var brightestSlot = Inventory.MaxBy(slot => slot.Empty ? 0 : slot.Itemstack.Collectible.LightHsv[2]);
            if (!brightestSlot.Empty)
            {
                entity.LightHsv = brightestSlot.Itemstack.Collectible.GetLightHsv(entity.World.BlockAccessor, null, brightestSlot.Itemstack);
            }
            else
            {
                entity.LightHsv = null;
            }
         */
    }
}
