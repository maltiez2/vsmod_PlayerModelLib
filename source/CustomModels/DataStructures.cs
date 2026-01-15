using HarmonyLib;
using OpenTK.Mathematics;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class SkinnablePartExtended : SkinnablePart
{
    public string[] TargetSkinParts { get; set; } = [];
    public bool OverlayTexture { get; set; } = false;
    public EnumColorBlendMode OverlayMode { get; set; } = EnumColorBlendMode.Normal;
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string[]> DisableElementsByVariantCode { get; set; } = [];
}

public class CustomModelConfig
{
    public bool Enabled { set; get; } = true;
    public string? Name { get; set; }
    public string Domain { get; set; } = "game";
    public string Group { get; set; } = "";
    public string Icon { get; set; } = "";
    public string GroupIcon { get; set; } = "";
    public string ShapePath { get; set; } = "";
    public string BaseShapeCode { get; set; } = "";
    public SkinnablePartExtended[] SkinnableParts { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacers { get; set; } = [];
    public Dictionary<string, CompositeShape> WearableCompositeModelReplacers { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacersByShape { get; set; } = [];
    public string[] AvailableClasses { get; set; } = [];
    public string[] SkipClasses { get; set; } = [];
    public string[] ExtraTraits { get; set; } = [];
    public string[] ExclusiveClasses { get; set; } = [];
    public float[] CollisionBox { get; set; } = [];
    public float EyeHeight { get; set; } = 1.7f;
    public float[] SizeRange { get; set; } = [0.8f, 1.2f];
    public bool ScaleColliderWithSizeHorizontally { get; set; } = true;
    public bool ScaleColliderWithSizeVertically { get; set; } = true;
    public float[] MaxCollisionBox { get; set; } = [float.MaxValue, float.MaxValue];
    public float[] MinCollisionBox { get; set; } = [0, 0];
    public float MaxEyeHeight { get; set; } = float.MaxValue;
    public float MinEyeHeight { get; set; } = 0;
    public string[] AddTags { get; set; } = [];
    public string[] RemoveTags { get; set; } = [];
    public float ModelSizeFactor { get; set; } = 1;
    public float HeadBobbingScale { get; set; } = 1;
    public float GuiModelScale { get; set; } = 1;
    public float WalkEyeHeightMultiplier { get; set; } = 1;
    public float SprintEyeHeightMultiplier { get; set; } = 1;
    public float SneakEyeHeightMultiplier { get; set; } = 0.8f;
    public float StepHeight { get; set; } = 0.6f;
    public float MaxOxygenFactor { get; set; } = 1;
}

public class CustomModelData
{
    public bool Enabled { set; get; } = true;
    public string Code { get; set; }
    public string? Name { get; set; }
    public string Group { get; set; } = "";
    public AssetLocation? Icon { get; set; } = null;
    public AssetLocation? GroupIcon { get; set; } = null;
    public Shape Shape { get; set; }
    public string BaseShapeCode { get; set; } = "";
    public Dictionary<string, (Vector3d origin, Vector3d size)> ElementSizes { get; set; } = [];
    public Dictionary<string, SkinnablePart> SkinParts { get; set; } = [];
    public SkinnablePart[] SkinPartsArray { get; set; } = [];
    public string[] MainTextureCodes { get; set; } = [];
    public Dictionary<string, CompositeTexture?> MainTextures { get; set; } = [];
    public Dictionary<string, Vector2i> MainTextureSizes { get; set; } = [];
    public Dictionary<int, string> WearableShapeReplacers { get; set; } = [];
    public Dictionary<int, CompositeShape> WearableCompositeShapeReplacers { get; set; } = [];
    public Dictionary<string, string> WearableShapeReplacersByShape { get; set; } = [];
    public HashSet<string> AvailableClasses { get; set; } = [];
    public HashSet<string> SkipClasses { get; set; } = [];
    public HashSet<string> ExclusiveClasses { get; set; } = [];
    public string[] ExtraTraits { get; set; } = [];
    public Vector2 CollisionBox { get; set; }
    public float EyeHeight { get; set; }
    public Vector2 SizeRange { get; set; }
    public bool ScaleColliderWithSizeHorizontally { get; set; } = true;
    public bool ScaleColliderWithSizeVertically { get; set; } = true;
    public Vector2 MaxCollisionBox { get; set; }
    public Vector2 MinCollisionBox { get; set; }
    public float MaxEyeHeight { get; set; } = float.MaxValue;
    public float MinEyeHeight { get; set; } = 0;
    public EntityTagArray AddTags { get; set; } = EntityTagArray.Empty;
    public EntityTagArray RemoveTags { get; set; } = EntityTagArray.Empty;
    public float ModelSizeFactor { get; set; } = 1;
    public float HeadBobbingScale { get; set; } = 1;
    public float GuiModelScale { get; set; } = 1;
    public float WalkEyeHeightMultiplier { get; set; } = 1;
    public float SprintEyeHeightMultiplier { get; set; } = 1;
    public float SneakEyeHeightMultiplier { get; set; } = 0.8f;
    public float StepHeight { get; set; } = 0.6f;
    public float MaxOxygenFactor { get; set; } = 1;


    public CustomModelData(string code, Shape shape)
    {
        Code = code;
        Shape = shape;
    }
}

public class BaseShapeDataJson
{
    public string Domain { get; set; } = "";
    public AssetLocation ShapePath { get; set; } = new();
    public string[] KeyElements { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacers { get; set; } = [];
    public Dictionary<string, CompositeShape> WearableCompositeModelReplacers { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacersByShape { get; set; } = [];
}


public class BaseShapeData
{
    public string Code { get; set; } = "";
    public Dictionary<string, (Vector3d origin, Vector3d size)> ElementSizes { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacers { get; set; } = [];
    public Dictionary<string, CompositeShape> WearableCompositeModelReplacers { get; set; } = [];
    public Dictionary<string, string> WearableModelReplacersByShape { get; set; } = [];
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ChangePlayerModelPacket
{
    public string ModelCode { get; set; } = "";
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ChangePlayerModelSizePacket
{
    public float EntitySize { get; set; } = 1;
}