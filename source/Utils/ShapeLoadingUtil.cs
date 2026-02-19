using Vintagestory.API.Common;
using Vintagestory.ServerMods;

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

    public static void PrefixTextures(Shape shape, string prefix, float damageEffect = 0f)
    {
        HashSet<string> replacedCodes = [];
        foreach (ShapeElement shapeElement in shape.Elements)
        {
            WalkShapeElements(shapeElement, element => PrefixFacesTextures(element, prefix, replacedCodes, damageEffect));
        }

        if (shape.Textures != null)
        {
            Dictionary<string, int[]> textureSizesCopy = shape.TextureSizes.ShallowClone();
            shape.TextureSizes.Clear();

            foreach ((string code, int[] size) in textureSizesCopy)
            {
                shape.TextureSizes[prefix + code] = size;
                replacedCodes.Remove(code);
            }

            foreach (string item in replacedCodes)
            {
                shape.TextureSizes[prefix + item] = [shape.TextureWidth, shape.TextureHeight];
            }
        }
    }

    public static void PrefixAnimations(Shape shape, string prefix)
    {
        if (shape.Animations == null)
        {
            return;
        }

        foreach (Animation animation in shape.Animations)
        {
            foreach (AnimationKeyFrame animationKeyFrame in animation.KeyFrames)
            {
                Dictionary<string, AnimationKeyFrameElement> dictionary = new();
                foreach ((string code, AnimationKeyFrameElement element) in animationKeyFrame.Elements)
                {
                    dictionary[prefix + code] = element;
                }

                animationKeyFrame.Elements = dictionary;
            }
        }
    }

    public static void PrefixFacesTextures(ShapeElement element, string prefix, HashSet<string> replacedCodes, float damageEffect)
    {
        element.Name = prefix + element.Name;
        if (damageEffect >= 0f)
        {
            element.DamageEffect = damageEffect;
        }

        ShapeElementFace[] facesResolved = element.FacesResolved;
        foreach (ShapeElementFace shapeElementFace in facesResolved)
        {
            if (shapeElementFace != null && shapeElementFace.Enabled && !shapeElementFace.Texture.StartsWith(prefix))
            {
                replacedCodes.Add(shapeElementFace.Texture);
                shapeElementFace.Texture = prefix + shapeElementFace.Texture;
            }
        }
    }
}
