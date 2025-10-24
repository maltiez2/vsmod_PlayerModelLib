using OpenTK.Mathematics;
using Vintagestory.API.Common;

namespace PlayerModelLib;

public static class ShapeAdjustmentUtil
{
    public static Shape? AdjustClothesShape(ICoreAPI api, AssetLocation shapePath, BaseShapeData baseShape, CustomModelData modelData)
    {
        if (modelData.WearableShapeReplacersByShape.ContainsKey(shapePath))
        {
            shapePath = modelData.WearableShapeReplacersByShape[shapePath];
        }
        
        Shape? result = LoadShape(api, shapePath)?.Clone();
        if (result == null)
        {
            return null;
        }

        foreach (ShapeElement? element in result.Elements)
        {
            if (element == null) continue;
            string code = element.StepParentName ?? "";
            if (baseShape.ElementSizes.ContainsKey(code) && modelData.ElementSizes.ContainsKey(code) && baseShape.ElementSizes[code] != modelData.ElementSizes[code])
            {
                RescaleShapeElement(element, GetScaleVector(baseShape.ElementSizes[code].size, modelData.ElementSizes[code].size));
            }
        }

        return result;
    }

    public static Shape? AdjustClothesShape(ICoreAPI api, Shape shapeToChange, BaseShapeData baseShape, CustomModelData modelData)
    {
        Shape? result = shapeToChange;
        if (result == null)
        {
            return null;
        }

        foreach (ShapeElement? element in result.Elements)
        {
            if (element == null) continue;
            string code = element.StepParentName ?? "";
            if (baseShape.ElementSizes.ContainsKey(code) && modelData.ElementSizes.ContainsKey(code) && baseShape.ElementSizes[code] != modelData.ElementSizes[code])
            {
                RescaleShapeElement(element, GetScaleVector(baseShape.ElementSizes[code].size, modelData.ElementSizes[code].size));
            }
        }

        return result;
    }

    public static Shape? LoadShape(ICoreAPI api, AssetLocation path)
    {
        path = path.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
        Shape? currentShape = Shape.TryGet(api, path);
        return currentShape;
    }

    private static Vector3d GetScaleVector(Vector3d baseSize, Vector3d customSize)
    {
        Vector3d scale = new(customSize.X / baseSize.X, customSize.Y / baseSize.Y, customSize.Z / baseSize.Z);
        return scale;
    }

    public static void RescaleShapeElement(ShapeElement element, Vector3d scale)
    {
        RescaleShapeElementRecursive(element, scale);
    }

    private static void RescaleShapeElementRecursive(ShapeElement element, Vector3d scale)
    {
        if (element == null) return;

        if (element.RotationOrigin != null && element.RotationOrigin.Length >= 3)
        {
            element.RotationOrigin[0] *= scale.X;
            element.RotationOrigin[1] *= scale.Y;
            element.RotationOrigin[2] *= scale.Z;
        }

        if (element.From != null && element.From.Length >= 3)
        {
            element.From[0] *= scale.X;
            element.From[1] *= scale.Y;
            element.From[2] *= scale.Z;
        }

        if (element.To != null && element.To.Length >= 3)
        {
            element.To[0] *= scale.X;
            element.To[1] *= scale.Y;
            element.To[2] *= scale.Z;
        }

        if (element.Children != null)
        {
            foreach (var child in element.Children)
            {
                RescaleShapeElementRecursive(child, scale);
            }
        }
    }
}
