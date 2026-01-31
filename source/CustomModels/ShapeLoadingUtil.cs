using System.Diagnostics;
using Vintagestory.API.Common;

namespace PlayerModelLib;


public static class ShapeLoadingUtil
{
    public static ObjectCache<string, Shape>? ShapesCache { get; set; }

    public static Shape? LoadShape(ICoreAPI api, AssetLocation path)
    {
        string fullPath = path.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
        if (api.Side == EnumAppSide.Client && ShapesCache?.Get(fullPath, out Shape? result) == true)
        {
            return CloneShape(result);
        }

        Shape? currentShape = Shape.TryGet(api, fullPath);

        if (api.Side == EnumAppSide.Client && currentShape != null)
        {
            ShapesCache?.Add(fullPath, CloneShape(currentShape));
        }

        return currentShape;
    }

    public static Shape CloneShape(Shape input)
    {
        Shape output = input.Clone();

        output.Textures = [];
        foreach ((string key, AssetLocation? value) in input.Textures)
        {
            output.Textures[key] = new(value.Domain, value.Path);
        }

        output.TextureSizes = [];
        foreach ((string key, int[]? value) in input.TextureSizes)
        {
            output.TextureSizes[key] = (int[]?)value.Clone();
        }

        foreach (ShapeElement? element in input.Elements ?? [])
        {
            if (element != null)
            {
                WalkShapeElements(element, CloneFaces);
            }
        }

        return output;
    }

    public static void CloneFaces(ShapeElement element)
    {
        if (element.FacesResolved == null)
        {
            return;
        }
        
        List<ShapeElementFace?> newFaces = [];
        foreach (ShapeElementFace? face in element.FacesResolved)
        {
            if (face == null)
            {
                newFaces.Add(null);
                continue;
            }
            ShapeElementFace newFace = new()
            {
                Texture = face.Texture,
                Uv = face.Uv,
                ReflectiveMode = face.ReflectiveMode,
                WindMode = face.WindMode,
                WindData = face.WindData,
                Rotation = face.Rotation,
                Glow = face.Glow,
                Enabled = face.Enabled
            };
            newFaces.Add(newFace);
        }
        element.FacesResolved = newFaces.ToArray();
    }

    public static void WalkShapeElements(ShapeElement element, Action<ShapeElement> action)
    {
        action.Invoke(element);
        if (element.Children != null)
        {
            foreach (ShapeElement? child in element.Children)
            {
                if (child != null)
                {
                    WalkShapeElements(child, action);
                }
            }
        }
    }
}
