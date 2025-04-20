using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class CustomModelBehavior : EntityBehavior
{
    public CustomModelBehavior(Entity entity) : base(entity)
    {
    }

    public override string PropertyName() => "PlayerModelLib:CustomModel";

    public CustomModelData? ModelData { get; set; } = null;
    public string CurrentModelCode { get; private set; } = "default";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        string modelCode = entity.WatchedAttributes.GetString("playermodellib-custommodel", "default");

        if (modelCode == "default")
        {
            return;
        }

        Dictionary<string, CustomModelData> customModels = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>().CustomModels;

        if (customModels.TryGetValue(modelCode, out CustomModelData? customModelData))
        {
            ModelData = customModelData;
        }
    }

    public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
    {
        entity.AnimManager.LoadAnimator(entity.World.Api, entity, entityShape, entity.AnimManager.Animator?.Animations, true);
    }

    public override void OnGameTick(float dt)
    {
        bool modelChanged = TryResetCustomModel();

        if (modelChanged && !IsCorrectShape())
        {
            ReplaceEntityShape();
        }
    }

    private bool TryResetCustomModel()
    {
        string modelCode = entity.WatchedAttributes.GetString("playermodellib-custommodel", "default");

        if (modelCode == null || modelCode == "") return false;
        if (modelCode == CurrentModelCode) return false;

        if (modelCode == "default")
        {
            ModelData = null;
            CurrentModelCode = modelCode;
            return true;
        }

        Dictionary<string, CustomModelData> customModels = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>().CustomModels;

        if (customModels.TryGetValue(modelCode, out CustomModelData? customModelData))
        {
            ModelData = customModelData;
            CurrentModelCode = modelCode;
            return true;
        }

        return false;
    }

    private void ReplaceEntityShape()
    {
        if (ModelData?.Shape == null || entity.Properties.Client.Renderer is not EntityShapeRenderer renderer) return;

        renderer.OverrideEntityShape = ModelData.Shape;

        renderer.TesselateShape();

        entity.AnimManager.LoadAnimator(entity.World.Api, entity, renderer.OverrideEntityShape, entity.AnimManager.Animator?.Animations, true);
    }

    private bool IsCorrectShape()
    {
        if (ModelData?.Shape == null || entity.Properties.Client.Renderer is not EntityShapeRenderer renderer) return true;

        return renderer.OverrideEntityShape == ModelData.Shape;
    }
}
