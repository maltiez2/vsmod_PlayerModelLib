using OpenTK.Mathematics;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

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

        AdjustClothesShape(api, result, baseShape, modelData);

        return result;
    }

    public static Shape? AdjustClothesShape(ICoreAPI api, Shape shapeToChange, BaseShapeData baseShape, CustomModelData modelData)
    {
        foreach (ShapeElement? element in shapeToChange.Elements)
        {
            if (element == null) continue;
            string code = element.StepParentName ?? "";
            if (baseShape.ElementSizes.ContainsKey(code) && modelData.ElementSizes.ContainsKey(code) && baseShape.ElementSizes[code] != modelData.ElementSizes[code])
            {
                Vector3d scaleVector = GetScaleVector(baseShape.ElementSizes[code].size, modelData.ElementSizes[code].size);
                RescaleShapeElement(element, scaleVector);
            }
        }

        return shapeToChange;
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

        AdjustScaleToRotation(element, ref scale);

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
            foreach (ShapeElement? child in element.Children)
            {
                RescaleShapeElementRecursive(child, scale);
            }
        }
    }

    private static void AdjustScaleToRotation(ShapeElement element, ref Vector3d scale)
    {
        if (Math.Abs(element.RotationX) > 0)
        {
            (double uniform, double main, double opposite) = GetFactorFromAngle(element.RotationX);
            double yzScale = (scale.Y + scale.Z) / 2;
            scale = new(scale.X, yzScale * uniform + scale.Y * main + scale.Z * opposite, yzScale * uniform + scale.Z * main + scale.Y * opposite);
        }

        if (Math.Abs(element.RotationY) > 0)
        {
            (double uniform, double main, double opposite) = GetFactorFromAngle(element.RotationY);
            double xzScale = (scale.X + scale.Z) / 2;
            scale = new(xzScale * uniform + scale.X * main + scale.Z * opposite, scale.Y, xzScale * uniform + scale.Z * main + scale.X * opposite);
        }

        if (Math.Abs(element.RotationZ) > 0)
        {
            (double uniform, double main, double opposite) = GetFactorFromAngle(element.RotationZ);
            double xyScale = (scale.X + scale.Y) / 2;
            scale = new(xyScale * uniform + scale.X * main + scale.Y * opposite, xyScale * uniform + scale.Y * main + scale.X * opposite, scale.Z);
        }
    }

    private static (double uniform, double main, double opposite) GetFactorFromAngle(double angleDeg)
    {
        const double max = 90;
        const double doubleMax = max * 2;
        const double halfMax = max / 2;
        double angle = Math.Abs(angleDeg);

        while (angle > doubleMax)
        {
            angle -= doubleMax;
        }

        if (angle > max)
        {
            angle = doubleMax - angle;
        }

        if (angle <= halfMax)
        {
            double uniform = GameMath.Clamp(angle / halfMax, 0, 1);
            return (uniform, 1 - uniform, 0);
        }
        else
        {
            double uniform = GameMath.Clamp((max - angle) / halfMax, 0, 1);
            return (uniform, 0, 1 - uniform);
        }
    }
}
