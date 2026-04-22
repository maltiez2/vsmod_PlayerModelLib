using PlayerModelLib.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
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
        foreach ((string key, AssetLocation? value) in input.Textures ?? [])
        {
            output.Textures[key] = new(value.Domain, value.Path);
        }

        output.TextureSizes = [];
        foreach ((string key, int[]? value) in input.TextureSizes ?? [])
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
        if (shape.Elements == null || shape.Textures == null)
        {
            return;
        }
        
        Dictionary<string, string> replacedCodes = [];
        foreach (ShapeElement shapeElement in shape.Elements)
        {
            WalkShapeElements(shapeElement, element => PrefixFacesTextures(element, prefix, replacedCodes, damageEffect));
        }
        
        Dictionary<string, int[]> textureSizesCopy = shape.TextureSizes.ShallowClone();
        shape.TextureSizes.Clear();

        foreach ((string code, int[] size) in textureSizesCopy)
        {
            if (replacedCodes.ContainsKey(code))
            {
                shape.TextureSizes[replacedCodes[code]] = size;
                replacedCodes.Remove(code);
            }
        }

        foreach ((string from, string to) in replacedCodes)
        {
            if (shape.TextureSizes.ContainsKey(from))
            {
                shape.TextureSizes[to] = shape.TextureSizes[from];
            }
            else
            {
                shape.TextureSizes[to] = [shape.TextureWidth, shape.TextureHeight];
            }
                
            if (shape.Textures.ContainsKey(from))
            {
                shape.Textures[to] = shape.Textures[from];
            }
            shape.Textures.Remove(from);
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
    public static void PrefixFacesTextures(ShapeElement element, string prefix, Dictionary<string, string> replacedCodes, float damageEffect)
    {
        if (element.Name == null || element.FacesResolved == null)
        {
            return;
        }
        
        element.Name = prefix + element.Name;
        if (damageEffect >= 0f)
        {
            element.DamageEffect = damageEffect;
        }

        ShapeElementFace[] facesResolved = element.FacesResolved;
        foreach (ShapeElementFace? shapeElementFace in facesResolved)
        {
            string? textureCode = shapeElementFace?.Texture;
            if (shapeElementFace != null && shapeElementFace.Texture != null && textureCode != null && shapeElementFace.Enabled && !shapeElementFace.Texture.StartsWith(prefix))
            {
                shapeElementFace.Texture = prefix + shapeElementFace.Texture;
                replacedCodes[textureCode] = prefix + textureCode;
            }
        }
    }

    public static Result StepParentShape(Shape parentShape, Shape childShape)
    {
        if (childShape.Elements == null || childShape.Elements.Length == 0)
        {
            return Result.Success();
            //return Result.Warning("Child shape does not contain any elements."); // too many warnings about empty skin parts
        }

        Result result = Result.Success();
        foreach (ShapeElement element in childShape.Elements)
        {
            result += StepParentElement(parentShape, element, null);
        }

        if (!result.IsSuccess)
        {
            return result;
        }

        AddChildTextureSizes(parentShape, childShape);
        StepParentAnimations(parentShape, childShape);

        return result;
    }

    public static Result StepParentElement(Shape parentShape, ShapeElement element, ShapeElement? parentElement)
    {
        Result result = Result.Success();

        result |= ProcessShapeElement(parentShape, element, parentElement);

        if (element.Children != null)
        {
            foreach (ShapeElement childElem in element.Children)
            {
                result |= StepParentElement(parentShape, childElem, element);
            }
        }

        return result;
    }
    public static Result ProcessShapeElement(Shape parentShape, ShapeElement childElement, ShapeElement? parentElement)
    {
        ShapeElement stepparentElem;

        if (childElement.StepParentName != null && childElement.StepParentName != "")
        {
            stepparentElem = parentShape.GetElementByName(childElement.StepParentName, StringComparison.InvariantCultureIgnoreCase);
            if (stepparentElem == null)
            {
                return Result.Error($"Tried to attach '{childElement.Name}' element to '{childElement.StepParentName}', but no such element in parent shape was found.");
            }
        }
        else
        {
            return Result.Error($"Tried to attach '{childElement.Name}' root element, but it had no step parent element specified");
        }

        if (parentElement != null)
        {
            parentElement.Children = parentElement.Children.Remove(childElement);
        }

        if (stepparentElem.Children == null)
        {
            stepparentElem.Children = [childElement];
        }
        else
        {
            stepparentElem.Children = stepparentElem.Children.Append(childElement);
        }

        childElement.ParentElement = stepparentElem;

        childElement.SetJointIdRecursive(stepparentElem.JointId);

        return Result.Success();
    }
    public static void AddChildTextureSizes(Shape parentShape, Shape childShape)
    {
        if (childShape.Textures == null || childShape.TextureSizes == null || parentShape.Textures == null || parentShape.TextureSizes == null)
        {
            return;
        }

        foreach ((string textureCode, int[] size) in childShape.TextureSizes)
        {
            parentShape.TextureSizes[textureCode] = size;
        }

        foreach ((string texutreCode, AssetLocation path) in childShape.Textures)
        {
            if (!parentShape.TextureSizes.ContainsKey(texutreCode))
            {
                parentShape.TextureSizes[texutreCode] = [childShape.TextureWidth, childShape.TextureHeight];
            }
 
            parentShape.Textures[texutreCode] = path;
        }
    }
    public static void StepParentAnimations(Shape parentShape, Shape childShape)
    {
        if (childShape.Animations == null || parentShape.Animations == null)
        {
            return;
        }

        foreach (Animation? childAnimation in childShape.Animations)
        {
            Animation? entityAnim = parentShape.Animations.FirstOrDefault(anim => anim.Code == childAnimation.Code);
            if (entityAnim == null)
            {
                continue;
            }

            foreach (AnimationKeyFrame childKeyFrame in childAnimation.KeyFrames)
            {
                AnimationKeyFrame entityKeyFrame = GetOrCreateKeyFrame(entityAnim, childKeyFrame.Frame);

                foreach ((string elementCode, AnimationKeyFrameElement element) in childKeyFrame.Elements)
                {
                    entityKeyFrame.Elements[elementCode] = element;
                }
            }
        }
    }
    public static AnimationKeyFrame GetOrCreateKeyFrame(Animation parentAnimation, int frame)
    {
        foreach (AnimationKeyFrame parentKeyFrame in parentAnimation.KeyFrames)
        {
            if (parentKeyFrame.Frame == frame)
            {
                return parentKeyFrame;
            }
        }

        for (int parentFrameIndex = 0; parentFrameIndex < parentAnimation.KeyFrames.Length; parentFrameIndex++)
        {
            if (parentAnimation.KeyFrames[parentFrameIndex].Frame > frame)
            {
                AnimationKeyFrame newKeyFrame = new() { Frame = frame, Elements = [] };
                parentAnimation.KeyFrames = parentAnimation.KeyFrames.InsertAt(newKeyFrame, parentFrameIndex);
                return newKeyFrame;
            }
        }

        AnimationKeyFrame startKeyFrame = new() { Frame = frame, Elements = [] };
        parentAnimation.KeyFrames = parentAnimation.KeyFrames.InsertAt(startKeyFrame, 0);
        return startKeyFrame;
    }
}
