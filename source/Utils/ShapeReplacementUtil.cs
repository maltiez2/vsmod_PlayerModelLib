using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static partial class ShapeReplacementUtil
{
    public static bool ExportingShape { get; set; } = false;

    public static void GetModelReplacement(ItemStack? stack, Entity entity, ref Shape? defaultShape, ref CompositeShape? compositeShape, IAttachableToEntity yadayada, float damageEffect, string slotCode, ref string[] willDeleteElements)
    {
        if (entity.Api.Side != EnumAppSide.Client) return;

        if (stack == null) return;

        int itemId = stack.Item?.Id ?? 0;

        _system ??= entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();

        PlayerSkinBehavior? skinBehavior = entity.GetBehavior<PlayerSkinBehavior>();

        string? currentModel = skinBehavior?.CurrentModelCode;

        if (skinBehavior == null || currentModel == null || itemId == 0 || _system == null || !_system.ModelsLoaded || currentModel == CustomModelsSystem.SeraphModelCode) return;

        CustomModelData customModel = _system.CustomModels[currentModel];

        string? yadaPrefixCode = yadayada.GetTexturePrefixCode(stack);
        string prefixCode = yadaPrefixCode ?? "";

        GenerateShapes(prefixCode, customModel, itemId, _system, currentModel, stack, entity, ref defaultShape, ref compositeShape, yadayada, damageEffect, ref willDeleteElements);
    }

    public static void ReplaceWearableShape(WearablesTesselatorBehavior tesselatorBehavior, IInventory inventory, ItemSlot slot, ref Shape entityShape, ref string[] willDeleteElements, ref Shape? attachableShape, ref CompositeShape? attachableCompisteShape)
    {
        if (tesselatorBehavior.entity.Api.Side != EnumAppSide.Client) return;

        int itemId = slot.Itemstack?.Item?.Id ?? 0;

        _system ??= tesselatorBehavior.entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();

        PlayerSkinBehavior? skinBehavior = tesselatorBehavior.entity.GetBehavior<PlayerSkinBehavior>();

        string? currentModel = skinBehavior?.CurrentModelCode;

        if (skinBehavior == null || currentModel == null || itemId == 0 || _system == null || !_system.ModelsLoaded || currentModel == CustomModelsSystem.SeraphModelCode) return;

        CustomModelData customModel = _system.CustomModels[currentModel];

        if (slot.Itemstack?.Collectible == null)
        {
            return;
        }

        IAttachableToEntity? attachable = IAttachableToEntity.FromCollectible(slot.Itemstack.Collectible);
        if (attachable == null)
        {
            return;
        }

        GenerateShapes("", customModel, itemId, _system, currentModel, slot.Itemstack, tesselatorBehavior.entity, ref attachableShape, ref attachableCompisteShape, attachable, 1, ref willDeleteElements);
    }

    public static void StaticDispose()
    {
        _system = null;
    }


    private static CustomModelsSystem? _system;

    private static void GenerateShapes(
        string prefix,
        CustomModelData customModel,
        int itemId,
        CustomModelsSystem system,
        string currentModel,
        ItemStack? stack,
        Entity entity,
        ref Shape? defaultShape,
        ref CompositeShape? compositeShape,
        IAttachableToEntity yadayada,
        float damageEffect,
        ref string[] willDeleteElements)
    {
        willDeleteElements = ReplaceWildcardPrefixes(willDeleteElements, prefix);

        if (ReplaceShapeByItem(prefix, entity, ref defaultShape, ref compositeShape, damageEffect, itemId, customModel))
        {
            return;
        }

        if (ReplaceShapeByShape(prefix, stack, entity, ref defaultShape, ref compositeShape, damageEffect, customModel, yadayada))
        {
            return;
        }

        ReplaceOverlays(stack, ref compositeShape, customModel, yadayada);

        CompositeShape? oldCompositeShape = stack == null ? null : yadayada.GetAttachedShape(stack, "default")?.Clone();
        if (oldCompositeShape == null)
        {
            LoggerUtil.Warn(entity.Api, typeof(ShapeReplacementUtil), $"Unable to get attached shape from '{stack?.Collectible?.Code}'");
            return;
        }
        string shapePath = oldCompositeShape.Base.ToString();

        if (system.BaseShapesData.TryGetValue(customModel.BaseShapeCode, out BaseShapeData? baseShapeData))
        {
            defaultShape = ShapeAdjustmentUtil.AdjustClothesShape(entity.Api, oldCompositeShape, baseShapeData, customModel);
            if (defaultShape == null)
            {
                return;
            }

            ShapeLoadingUtil.PrefixTextures(defaultShape, prefix, damageEffect);
            ShapeLoadingUtil.PrefixAnimations(defaultShape, prefix);
            defaultShape.ResolveReferences(entity.World.Logger, currentModel);

            compositeShape = compositeShape?.Clone();
            if (compositeShape != null)
            {
                compositeShape.Overlays = [];
            }

            if (PlayerModelModSystem.Settings.ExportShapeFiles)
            {
                ExportShape(entity.Api, shapePath, shapePath.Replace(':', '-').Replace('/', '-').Replace('\\', '-'), baseShapeData, customModel);
            }
        }
    }

    private static string[] ReplaceWildcardPrefixes(string[] elements, string prefix)
    {
        return elements
            .Where(name => name != null && name.StartsWith('*'))
            .Select(name => prefix + name[1..])
            .Concat(elements)
            .Distinct()
            .ToArray();
    }
    private static void ExportShape(ICoreAPI api, string shapePath, string fileName, BaseShapeData baseShape, CustomModelData modelData)
    {
        try
        {
            ExportingShape = true;
            string fullPath = new AssetLocation(shapePath).WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape? shape = Shape.TryGet(api, fullPath);
            if (shape == null) return;
            shape = ShapeAdjustmentUtil.AdjustClothesShape(api, shape, baseShape, modelData);
            if (shape == null) return;
            AddHashesToTextureCodes(shape.Elements);
            string fullFilePath = Path.Combine(GamePaths.ModConfig, $"clothes-shapes/{modelData.Code.Replace(':', '-')}-{fileName}.json");
            FileInfo? fifo = new(fullFilePath);
            if (fifo.Directory == null) return;
            GamePaths.EnsurePathExists(fifo.Directory.FullName);
            string json = JsonConvert.SerializeObject(shape, Formatting.Indented);

            FixShapeJson(ref json);

            File.WriteAllText(fifo.FullName, json);

            LoggerUtil.Verbose(api, typeof(ShapeReplacementUtil), $"('{modelData.Code}') Exported '{shapePath}' to '{fullFilePath}'");
            LoggerUtil.Dev(api, typeof(ShapeReplacementUtil), $"('{modelData.Code}') Exported '{shapePath}' to '{fullFilePath}'");

            ExportingShape = false;
        }
        catch (Exception exception)
        {
            ExportingShape = false;
            LoggerUtil.Error(api, typeof(ShapeReplacementUtil), $"Error on exporting shape '{fileName}':\n{exception}\n");
        }
    }
    private static void FixShapeJson(ref string json)
    {
        json = "{\n \"editor\": {\"backDropShape\": \"\",\"entityTextureMode\": true}," + json[1..];
        LowercaseJsonKeys(ref json);
        TurnOffAutoUv(ref json);
        FixAnimationsEnums(ref json);
        json = json.Replace("keyFrames", "keyframes").Replace("quantityFrames", "quantityframes").Replace("game:", "");
    }
    private static void FixAnimationsEnums(ref string json)
    {
        json = OnAnimantionEndRegex().Replace(json, match =>
        {
            int value = int.Parse(match.Groups[1].Value);
            string enumName = value switch
            {
                0 => "Repeat",
                1 => "Hold",
                2 => "Stop",
                3 => "EaseOut",
                _ => "Repeat"
            };
            return $"\"onAnimationEnd\": \"{enumName}\"";
        });

        json = OnActivityStoppedRegex().Replace(json, match =>
        {
            int value = int.Parse(match.Groups[1].Value);
            string enumName = value switch
            {
                0 => "PlayTillEnd",
                1 => "Rewind",
                2 => "Stop",
                3 => "EaseOut",
                _ => "PlayTillEnd"
            };
            return $"\"onActivityStopped\": \"{enumName}\"";
        });
    }
    private static void TurnOffAutoUv(ref string json)
    {
        string pattern = @"(\""(north|south|east|west|up|down)\""\s*:\s*\{)([^{}]*\{[^{}]*\}[^{}]*|[^{}])*?\}";

        json = Regex.Replace(json, pattern, match =>
        {
            string faceJson = match.Value;

            if (AutoUvRegex().IsMatch(faceJson))
            {
                return faceJson;
            }

            return AutoUvOffRegex().Replace(faceJson, @", ""autoUv"": false}");
        }, RegexOptions.Singleline);
    }
    private static void LowercaseJsonKeys(ref string json)
    {
        json = LowerCaseJsonKeysRegex().Replace(json, match =>
        {
            string key = match.Groups[1].Value;
            if (string.IsNullOrEmpty(key))
                return match.Value;

            // Lowercase only the first letter
            string lowerKey = char.ToLowerInvariant(key[0]) + key[1..];
            return $"\"{lowerKey}\":";
        });
    }
    private static void AddHashesToTextureCodes(ShapeElement[] elements)
    {
        foreach (ShapeElement element in elements)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (element.Faces != null)
            {
                foreach (ShapeElementFace face in element.Faces.Values)
                {
                    face.Texture = "#" + face.Texture;
                }
            }
#pragma warning restore CS0618 // Type or member is obsolete

            if (element.Children != null)
            {
                AddHashesToTextureCodes(element.Children);
            }
        }
    }
    private static void ReplaceOverlay(CompositeShape shape, Dictionary<string, string> replacements)
    {
        if (replacements.TryGetValue(shape.Base.ToString(), out string? newShape))
        {
            shape.Base = newShape;
        }

        if (shape.Overlays != null)
        {
            foreach (CompositeShape? overlay in shape.Overlays)
            {
                if (overlay == null) continue;

                ReplaceOverlay(overlay, replacements);
            }
        }
    }
    private static bool ReplaceShapeByItem(string prefixCode, Entity entity, ref Shape? defaultShape, ref CompositeShape? compositeShape, float damageEffect, int itemId, CustomModelData customModel)
    {
        if (customModel.WearableShapeReplacers.TryGetValue(itemId, out string? shape))
        {
            defaultShape = ShapeLoadingUtil.LoadShape(entity.Api, shape);
            defaultShape?.SubclassForStepParenting(prefixCode, damageEffect);
            defaultShape?.ResolveReferences(entity.World.Logger, "");

            if (compositeShape != null)
            {
                compositeShape = compositeShape.Clone();
                compositeShape.Base = shape;
            }

            return true;
        }

        if (customModel.WearableCompositeShapeReplacers.TryGetValue(itemId, out CompositeShape? newCompositeShape))
        {
            compositeShape = newCompositeShape.Clone();

            compositeShape.Base = compositeShape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");

            defaultShape = ShapeLoadingUtil.LoadShape(entity.Api, newCompositeShape.Base);

            defaultShape?.SubclassForStepParenting(prefixCode, damageEffect);
            defaultShape?.ResolveReferences(entity.World.Logger, "");

            return true;
        }

        return false;
    }
    private static bool ReplaceShapeByShape(string prefixCode, ItemStack? stack, Entity entity, ref Shape? defaultShape, ref CompositeShape? compositeShape, float damageEffect, CustomModelData customModel, IAttachableToEntity yadayada)
    {
        if (stack == null)
        {
            return false;
        }    
        
        CompositeShape? oldCompositeShape = yadayada.GetAttachedShape(stack, "default")?.Clone();
        if (oldCompositeShape == null)
        {
            LoggerUtil.Warn(entity.Api, typeof(ShapeReplacementUtil), $"Unable to get attached shape from '{stack.Collectible?.Code}'");
            return false;
        }
        string shapePath = oldCompositeShape.Base.ToString();

        if (!customModel.WearableShapeReplacersByShape.TryGetValue(shapePath, out string? shape)) return false;

        defaultShape = ShapeLoadingUtil.LoadShape(entity.Api, shape);
        defaultShape?.SubclassForStepParenting(prefixCode, damageEffect);
        defaultShape?.ResolveReferences(entity.World.Logger, "");

        if (oldCompositeShape.Overlays != null)
        {
            foreach (CompositeShape? overlay in oldCompositeShape.Overlays)
            {
                if (overlay == null) continue;

                ReplaceOverlay(overlay, customModel.WearableShapeReplacersByShape);
            }

            compositeShape = oldCompositeShape;
        }

        return true;
    }
    private static void ReplaceOverlays(ItemStack? stack, ref CompositeShape? compositeShape, CustomModelData customModel, IAttachableToEntity yadayada)
    {
        if (stack == null) return;
        
        CompositeShape? oldCompositeShape = yadayada.GetAttachedShape(stack, "default")?.Clone();
        if (oldCompositeShape == null) return;

        if (oldCompositeShape.Overlays != null)
        {
            foreach (CompositeShape? overlay in oldCompositeShape.Overlays)
            {
                if (overlay == null) continue;

                ReplaceOverlay(overlay, customModel.WearableShapeReplacersByShape);
            }

            compositeShape = oldCompositeShape;
        }
    }

    [GeneratedRegex(@"""onAnimationEnd""\s*:\s*(\d+)")]
    public static partial Regex OnAnimantionEndRegex();
    [GeneratedRegex(@"\""([A-Z][^\""]*)\"":")]
    public static partial Regex LowerCaseJsonKeysRegex();
    [GeneratedRegex(@"""onActivityStopped""\s*:\s*(\d+)")]
    public static partial Regex OnActivityStoppedRegex();
    [GeneratedRegex(@"""autoUv""\s*:")]
    public static partial Regex AutoUvRegex();
    [GeneratedRegex(@"\}\s*$")]
    public static partial Regex AutoUvOffRegex();
}