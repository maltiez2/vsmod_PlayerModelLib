using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class CustomShapeConfig
{
    public string ShapePath { get; set; }
}

public class CustomModelData
{
    public CompositeShape? CompositeShape { get; set; }
    public Shape? Shape { get; set; }
}

public class CustomModelsSystem : ModSystem
{
    public Dictionary<string, CustomModelData> CustomModels { get; } = new();
    public Dictionary<string, SkinnablePart> AvailableSkinPartsByCode { get; } = new Dictionary<string, SkinnablePart>();
    public List<SkinnablePart> AvailableSkinParts { get; } = new();

    public CustomModelData? DefaultModel { get; private set; }

    public override void Start(ICoreAPI api)
    {

    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        string testShapePath = "foxmodel:fox";
        Shape? testShape = LoadShape(api, testShapePath);
        Shape? defaultShape = LoadShape(api, "game:entity/humanoid/seraph-faceless");

        CustomModels.Add("test", new() { Shape = testShape });
        DefaultModel = new() { Shape = defaultShape };

        if (DefaultModel.Shape == null) return;
        DefaultModel.Shape.ResolveReferences(api.Logger, "PlayerModelLib:CustomModel-default");

        foreach (Shape customShape in CustomModels.Select(entry => entry.Value.Shape).OfType<Shape>())
        {
            IEnumerable<string> existingAnimations = customShape.Animations.Select(entry => entry.Code);

            foreach ((uint crc32, Animation animation) in DefaultModel.Shape.AnimationsByCrc32)
            {
                if (existingAnimations.Contains(animation.Code)) continue;

                customShape.Animations = customShape.Animations.Append(animation).ToArray();
                customShape.AnimationsByCrc32.Add(crc32, animation);
            }

            customShape.ResolveReferences(api.Logger, "PlayerModelLib:CustomModel-test");
        }
    }

    private Shape? LoadShape(ICoreAPI api, string path)
    {
        AssetLocation shapeLocation = new(path);
        shapeLocation = shapeLocation.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
        Shape? currentShape = Shape.TryGet(api, shapeLocation);
        return currentShape;
    }

    private void LoadSkinnableParts(JsonObject json, ICoreClientAPI api)
    {
        SkinnablePart[] availableSkinParts = json["skinnableParts"].AsObject<SkinnablePart[]>();
        foreach (SkinnablePart part in availableSkinParts)
        {
            part.VariantsByCode = new Dictionary<string, SkinnablePartVariant>();



            AvailableSkinPartsByCode[part.Code] = part;

            if (part.Type == EnumSkinnableType.Texture && api.Side == EnumAppSide.Client)
            {
                LoadedTexture texture = new(api);
                foreach (SkinnablePartVariant? variant in part.Variants)
                {
                    AssetLocation textureLoc;

                    if (part.TextureTemplate != null)
                    {
                        textureLoc = part.TextureTemplate.Clone();
                        textureLoc.Path = textureLoc.Path.Replace("{code}", variant.Code);
                    }
                    else
                    {
                        textureLoc = variant.Texture;
                    }

                    IAsset asset = api.Assets.TryGet(textureLoc.Clone().WithPathAppendixOnce(".png").WithPathPrefixOnce("textures/"), true);

                    int r = 0, g = 0, b = 0;
                    float c = 0;

                    BitmapRef bmp = asset.ToBitmap(api);
                    for (int i = 0; i < 8; i++)
                    {
                        Vec2d vec = GameMath.R2Sequence2D(i);
                        SKColor col2 = bmp.GetPixelRel((float)vec.X, (float)vec.Y);
                        if (col2.Alpha > 0.5)
                        {
                            r += col2.Red;
                            g += col2.Green;
                            b += col2.Blue;
                            c++;
                        }
                    }

                    bmp.Dispose();

                    c = Math.Max(1, c);
                    variant.Color = ColorUtil.ColorFromRgba((int)(b / c), (int)(g / c), (int)(r / c), 255);
                    part.VariantsByCode[variant.Code] = variant;
                }
            }
            else
            {
                foreach (SkinnablePartVariant? variant in part.Variants)
                {
                    part.VariantsByCode[variant.Code] = variant;
                }
            }
        }
    }
}