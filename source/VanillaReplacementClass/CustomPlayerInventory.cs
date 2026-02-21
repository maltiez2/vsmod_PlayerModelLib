using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class CustomPlayerInventory : EntityBehaviorPlayerInventory
{
    public CustomPlayerInventory(Entity entity) : base(entity)
    {
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        // Do nothing
    }

    public override ITexPositionSource? GetTextureSource(ref EnumHandling handling)
    {
        return null;
    }
}
