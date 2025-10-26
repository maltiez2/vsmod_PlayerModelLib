using HarmonyLib;
using Newtonsoft.Json;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace PlayerModelLib;

internal static class TranspilerPatches
{
    public static bool ExportingShape { get; set; } = false;

    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    public static class EntityBehaviorContainerPatchCommand
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EntityBehaviorContainer), "addGearToShape",
            [
                typeof(Shape),
                typeof(ItemStack),
                typeof(IAttachableToEntity),
                typeof(string),
                typeof(string),
                typeof(string[]).MakeByRefType(),
                typeof(Dictionary<string, StepParentElementTo>)
            ]);
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeInstruction[] newInstructions =
            [
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorContainer), "entity")),
                new(OpCodes.Ldloca_S, 3),
                new(OpCodes.Ldloca_S, 5),
                new(OpCodes.Ldarg_3),
                new(OpCodes.Ldloc_1),
                new(OpCodes.Ldarg_S, 4),
                new(OpCodes.Call, AccessTools.Method(typeof(EntityBehaviorContainerPatchCommand), nameof(GetModelReplacement))),
            ];
            List<CodeInstruction> codes = [.. instructions];

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Isinst && (Type)codes[i].operand == typeof(Vintagestory.API.Client.ICoreClientAPI))
                {
                    codes.InsertRange(i + 2, newInstructions);

                    return codes;
                }
            }

            return codes;
        }

        public static void GetModelReplacement(ItemStack? stack, Entity entity, ref Shape? defaultShape, ref CompositeShape? compositeShape, IAttachableToEntity yadayada, float damageEffect, string slotCode)
        {
            int itemId = stack?.Item?.Id ?? 0;

            CustomModelsSystem system = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();
            PlayerSkinBehavior? skinBehavior = entity.GetBehavior<PlayerSkinBehavior>();

            string? currentModel = skinBehavior?.CurrentModelCode;

            if (currentModel == null || itemId == 0 || system == null || !system.ModelsLoaded) return;

            CustomModelData customModel = system.CustomModels[currentModel];

            string? yadaPrefixCode = yadayada.GetTexturePrefixCode(stack);
            string prefixCode = yadaPrefixCode == null ? "" : yadaPrefixCode;

            if (ReplaceShapeByItem(prefixCode, entity, ref defaultShape, ref compositeShape, damageEffect, itemId, customModel))
            {
                return;
            }

            if (ReplaceShapeByShape(prefixCode, stack, entity, ref defaultShape, ref compositeShape, damageEffect, customModel, yadayada))
            {
                return;
            }

            ReplaceOverlays(stack, ref compositeShape, customModel, yadayada);

            CompositeShape oldCompositeShape = yadayada.GetAttachedShape(stack, "default").Clone();
            string shapePath = oldCompositeShape.Base.ToString();


            if (system.BaseShapesData.TryGetValue(customModel.BaseShapeCode, out BaseShapeData? baseShapeData))
            {
                defaultShape = ShapeAdjustmentUtil.AdjustClothesShape(entity.Api, shapePath, baseShapeData, customModel);
                defaultShape?.SubclassForStepParenting(prefixCode, damageEffect);
                defaultShape?.ResolveReferences(entity.World.Logger, currentModel);

                if (PlayerModelModSystem.Settings.ExportShapeFiles)
                {
                    ExportShape(entity.Api, shapePath, shapePath.Replace(':', '-').Replace('/', '-').Replace('\\', '-'), baseShapeData, customModel);
                }
            }
        }

        private static Shape? LoadShape(ICoreAPI api, string path)
        {
            AssetLocation shapeLocation = new(path);
            shapeLocation = shapeLocation.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape? currentShape = Shape.TryGet(api, shapeLocation);
            return currentShape;
        }

        private static void ExportShape(ICoreAPI api, string shapePath, string fileName, BaseShapeData baseShape, CustomModelData modelData)
        {
            try
            {
                ExportingShape = true;
                Shape? shape = LoadShape(api, shapePath);
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

                LoggerUtil.Verbose(api, typeof(TranspilerPatches), $"('{modelData.Code}') Exported '{shapePath}' to '{fullFilePath}'");
                LoggerUtil.Dev(api, typeof(TranspilerPatches), $"('{modelData.Code}') Exported '{shapePath}' to '{fullFilePath}'");

                ExportingShape = false;
            }
            catch (Exception exception)
            {
                ExportingShape = false;
                LoggerUtil.Error(api, typeof(TranspilerPatches), $"Error on exporting shape '{fileName}':\n{exception}\n");
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
            json = Regex.Replace(json, @"""onAnimationEnd""\s*:\s*(\d+)", match =>
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

            json = Regex.Replace(json, @"""onActivityStopped""\s*:\s*(\d+)", match =>
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

                if (Regex.IsMatch(faceJson, @"""autoUv""\s*:"))
                    return faceJson;

                return Regex.Replace(faceJson, @"\}\s*$", @", ""autoUv"": false}");
            }, RegexOptions.Singleline);
        }
        private static void LowercaseJsonKeys(ref string json)
        {
            json = Regex.Replace(json, @"\""([A-Z][^\""]*)\"":", match =>
            {
                string key = match.Groups[1].Value;
                if (string.IsNullOrEmpty(key))
                    return match.Value;

                // Lowercase only the first letter
                string lowerKey = char.ToLowerInvariant(key[0]) + key.Substring(1);
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
                defaultShape = LoadShape(entity.Api, shape);

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

                defaultShape = LoadShape(entity.Api, newCompositeShape.Base);

                defaultShape?.SubclassForStepParenting(prefixCode, damageEffect);
                defaultShape?.ResolveReferences(entity.World.Logger, "");

                return true;
            }

            return false;
        }

        private static bool ReplaceShapeByShape(string prefixCode, ItemStack? stack, Entity entity, ref Shape? defaultShape, ref CompositeShape? compositeShape, float damageEffect, CustomModelData customModel, IAttachableToEntity yadayada)
        {
            CompositeShape oldCompositeShape = yadayada.GetAttachedShape(stack, "default").Clone();

            string shapePath = oldCompositeShape.Base.ToString();

            if (!customModel.WearableShapeReplacersByShape.TryGetValue(shapePath, out string? shape)) return false;

            defaultShape = LoadShape(entity.Api, shape);

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
            CompositeShape oldCompositeShape = yadayada.GetAttachedShape(stack, "default").Clone();

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
    }
}