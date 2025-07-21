using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;

namespace PlayerModelLib;

public static class OtherPatches
{
    public static void Patch(string harmonyId)
    {
        /*new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorTexturedClothing).GetMethod("GetTextureSource", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(GetTextureSource)))
            );*/
        /*new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorExtraSkinnable).GetMethod("addSkinPart", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(addSkinPart)))
            );*/
        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(OtherPatches), nameof(reloadSkin)))
            );
    }

    public static void Unpatch(string harmonyId)
    {
        //new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorTexturedClothing).GetMethod("GetTextureSource", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        //new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorExtraSkinnable).GetMethod("addSkinPart", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorTexturedClothing).GetMethod("reloadSkin", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
    }

    private static readonly FieldInfo? EntityBehaviorTexturedClothing_skinTextureSubId = typeof(EntityBehaviorTexturedClothing).GetField("skinTextureSubId", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? EntityBehaviorTexturedClothing_textureSpaceAllocated = typeof(EntityBehaviorTexturedClothing).GetField("textureSpaceAllocated", BindingFlags.NonPublic | BindingFlags.Instance);

    /*public static void GetTextureSource(EntityBehaviorTexturedClothing __instance)
    {
        ICoreClientAPI? api = __instance.entity.Api as ICoreClientAPI;

        bool textureSpaceAllocated = (bool?)EntityBehaviorTexturedClothing_textureSpaceAllocated?.GetValue(__instance) ?? false;

        if (textureSpaceAllocated) return;
        
        PlayerSkinBehavior? extraSkinnableBehavior = __instance.entity.GetBehavior<PlayerSkinBehavior>();

        if (extraSkinnableBehavior == null) return;

        string currentModel = extraSkinnableBehavior.CurrentModelCode;

        CustomModelsSystem system = api.ModLoader.GetModSystem<CustomModelsSystem>();

        if (!extraSkinnableBehavior.LoadedOnce)
        {
            extraSkinnableBehavior.LoadedOnce = true;

            TextureAtlasPosition origTexPos = api.EntityTextureAtlas.Positions[__instance.entity.Properties.Client.FirstTexture.Baked.TextureSubId];
            string skinBaseTextureKey = __instance.entity.Properties.Attributes?["skinBaseTextureKey"].AsString("seraph");
            if (skinBaseTextureKey != null) origTexPos = api.EntityTextureAtlas.Positions[__instance.entity.Properties.Client.Textures[skinBaseTextureKey].Baked.TextureSubId];

            int width = (int)((origTexPos.x2 - origTexPos.x1) * __instance.AtlasSize.Width);
            int height = (int)((origTexPos.y2 - origTexPos.y1) * __instance.AtlasSize.Height);

            api.EntityTextureAtlas.AllocateTextureSpace(width, height, out int skinTextureSubId, out var skinTexPos);

            (__instance.entity.Properties.Client.Renderer as EntityShapeRenderer).skinTexPos = skinTexPos;

            EntityBehaviorTexturedClothing_textureSpaceAllocated?.SetValue(__instance, true);
            EntityBehaviorTexturedClothing_skinTextureSubId?.SetValue(__instance, skinTextureSubId);

            extraSkinnableBehavior.DefaultModelTextureSpace = skinTextureSubId;
            extraSkinnableBehavior.DefaultModelTexturePosition = skinTexPos;
        }

        if (currentModel == system.DefaultModelCode && extraSkinnableBehavior.DefaultModelTextureSpace == 0)
        {
            TextureAtlasPosition origTexPos = api.EntityTextureAtlas.Positions[__instance.entity.Properties.Client.FirstTexture.Baked.TextureSubId];
            string skinBaseTextureKey = __instance.entity.Properties.Attributes?["skinBaseTextureKey"].AsString("seraph");
            if (skinBaseTextureKey != null) origTexPos = api.EntityTextureAtlas.Positions[__instance.entity.Properties.Client.Textures[skinBaseTextureKey].Baked.TextureSubId];

            int width = (int)((origTexPos.x2 - origTexPos.x1) * __instance.AtlasSize.Width);
            int height = (int)((origTexPos.y2 - origTexPos.y1) * __instance.AtlasSize.Height);

            api.EntityTextureAtlas.AllocateTextureSpace(width, height, out int skinTextureSubId, out var skinTexPos);
            
            (__instance.entity.Properties.Client.Renderer as EntityShapeRenderer).skinTexPos = skinTexPos;

            EntityBehaviorTexturedClothing_textureSpaceAllocated?.SetValue(__instance, true);
            EntityBehaviorTexturedClothing_skinTextureSubId?.SetValue(__instance, skinTextureSubId);

            extraSkinnableBehavior.DefaultModelTextureSpace = skinTextureSubId;
            extraSkinnableBehavior.DefaultModelTexturePosition = skinTexPos;

            return;
        }
        else if (currentModel == system.DefaultModelCode)
        {
            (__instance.entity.Properties.Client.Renderer as EntityShapeRenderer).skinTexPos = extraSkinnableBehavior.DefaultModelTexturePosition;
            
            EntityBehaviorTexturedClothing_textureSpaceAllocated?.SetValue(__instance, true);
            EntityBehaviorTexturedClothing_skinTextureSubId?.SetValue(__instance, extraSkinnableBehavior.DefaultModelTextureSpace);
            Debug.WriteLine($"({(__instance.entity as EntityPlayer).Player?.PlayerName ?? __instance.entity.EntityId.ToString()}) set for {currentModel}: {(int)((extraSkinnableBehavior.DefaultModelTexturePosition.x2 - extraSkinnableBehavior.DefaultModelTexturePosition.x1) * __instance.AtlasSize.Width)} {(int)((extraSkinnableBehavior.DefaultModelTexturePosition.y2 - extraSkinnableBehavior.DefaultModelTexturePosition.y1) * __instance.AtlasSize.Height)} at {extraSkinnableBehavior.DefaultModelTexturePosition.x1 * __instance.AtlasSize.Width} {extraSkinnableBehavior.DefaultModelTexturePosition.y1 * __instance.AtlasSize.Height}");
            return;
        }


        if (extraSkinnableBehavior.TextureSpacesByModelCodes.TryGetValue(currentModel, out int spaceId))
        {
            TextureAtlasPosition texPos = extraSkinnableBehavior.SkinTexturePositionsByModelCodes[currentModel];
            EntityBehaviorTexturedClothing_skinTextureSubId?.SetValue(__instance, spaceId);
            (__instance.entity.Properties.Client.Renderer as EntityShapeRenderer).skinTexPos = texPos;
            Debug.WriteLine($"({(__instance.entity as EntityPlayer).Player?.PlayerName ?? __instance.entity.EntityId.ToString()}) set for {currentModel}: {(int)((texPos.x2 - texPos.x1) * __instance.AtlasSize.Width)} {(int)((texPos.y2 - texPos.y1) * __instance.AtlasSize.Height)} at {texPos.x1 * __instance.AtlasSize.Width} {texPos.y1 * __instance.AtlasSize.Height}");
        }
        else
        {
            if (!system.MainTextures.ContainsKey(currentModel)) return;
            
            CompositeTexture tex = system.MainTextures[currentModel];

            TextureAtlasPosition texPos = system.MainTexturesPositions[currentModel];

            int width = (int)((texPos.x2 - texPos.x1) * __instance.AtlasSize.Width);
            int height = (int)((texPos.y2 - texPos.y1) * __instance.AtlasSize.Height);

            api.EntityTextureAtlas.AllocateTextureSpace(width, height, out int skinTextureSubId, out TextureAtlasPosition? skinTexPos);

            Debug.WriteLine($"AllocateTextureSpace: {currentModel} - {(__instance.entity as EntityPlayer).Player?.PlayerName ?? __instance.entity.EntityId.ToString()} - {skinTextureSubId}");

            (__instance.entity.Properties.Client.Renderer as EntityShapeRenderer).skinTexPos = skinTexPos;

            EntityBehaviorTexturedClothing_textureSpaceAllocated?.SetValue(__instance, true);
            EntityBehaviorTexturedClothing_skinTextureSubId?.SetValue(__instance, skinTextureSubId);

            extraSkinnableBehavior.TextureSpacesByModelCodes[currentModel] = skinTextureSubId;
            extraSkinnableBehavior.SkinTexturePositionsByModelCodes[currentModel] = skinTexPos;
            Debug.WriteLine($"({(__instance.entity as EntityPlayer).Player?.PlayerName ?? __instance.entity.EntityId.ToString()}) create for {currentModel}: {width} {height} at {texPos.x1 * __instance.AtlasSize.Width} {texPos.y1 * __instance.AtlasSize.Height}");
        }
    }*/

    public static bool reloadSkin() => false;

    /*public static bool addSkinPart(EntityBehaviorExtraSkinnable __instance, ref Shape __result, AppliedSkinnablePartVariant part, Shape entityShape, string[] disableElements, string shapePathForLogging)
    {
        var skinpart = __instance.AvailableSkinPartsByCode[part.PartCode];
        if (skinpart.Type == EnumSkinnableType.Voice)
        {
            return true;
        }

        PlayerSkinBehavior? extraSkinnableBehavior = __instance.entity.GetBehavior<PlayerSkinBehavior>();
        CustomModelsSystem system = __instance.entity.World.Api.ModLoader.GetModSystem<CustomModelsSystem>();

        if (extraSkinnableBehavior == null) return true;

        string currentModel = extraSkinnableBehavior.CurrentModel;

        if (system.DefaultModelCode == currentModel) return true;

        entityShape.RemoveElements(disableElements);

        var api = __instance.entity.World.Api;
        ICoreClientAPI capi = __instance.entity.World.Api as ICoreClientAPI;
        AssetLocation shapePath;
        CompositeShape tmpl = skinpart.ShapeTemplate;

        if (part.Shape == null && tmpl != null)
        {
            shapePath = tmpl.Base.CopyWithPath("shapes/" + tmpl.Base.Path + ".json");
            shapePath.Path = shapePath.Path.Replace("{code}", part.Code);
        }
        else
        {
            shapePath = part.Shape.Base.CopyWithPath("shapes/" + part.Shape.Base.Path + ".json");
        }

        Shape partShape = Shape.TryGet(api, shapePath);
        if (partShape == null)
        {
            //api.World.Logger.Warning("Entity skin shape {0} defined in entity config {1} not found or errored, was supposed to be at {2}. Skin part will be invisible.", shapePath, entity.Properties.Code, shapePath);
            return false;
        }

        string prefixcode = "skinpart" + "-" + currentModel.Replace(':', '-');
        partShape.SubclassForStepParenting(prefixcode + "-");

        var textures = __instance.entity.Properties.Client.Textures;
        entityShape.StepParentShape(partShape, shapePath.ToShortString(), shapePathForLogging, api.Logger, (texcode, loc) =>
        {
            if (capi == null) return;
            if (!textures.ContainsKey(prefixcode+ "-" + texcode) && skinpart.TextureRenderTo == null)
            {
                var cmpt = textures[prefixcode + "-" + texcode] = new CompositeTexture(loc);
                cmpt.Bake(api.Assets);
                capi.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _);
                cmpt.Baked.TextureSubId = textureSubid;
            }
        });

        __result = entityShape;

        return false;
    }*/

    /*[HarmonyPatch(typeof(EntityBehaviorTexturedClothing), "reloadSkin")]
    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    public class EntityBehaviorTexturedClothingPatchCommand
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeInstruction[] newInstructions = new CodeInstruction[]
            {
                new(OpCodes.Pop),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorTexturedClothing), "entity")),
                new(OpCodes.Call, AccessTools.Method(typeof(EntityBehaviorTexturedClothingPatchCommand), nameof(GetSkinBaseTextureKey))),
            };

            List<CodeInstruction> codes = new(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Dup)
                {
                    codes.InsertRange(i + 2, newInstructions);

                    return codes;
                }
            }

            return codes;
        }

        private static string GetSkinBaseTextureKey(Entity entity)
        {
            return entity.GetBehavior<PlayerSkinBehavior>().MainTextureCode;
        }
    }*/


    [HarmonyPatchCategory("PlayerModelLibTranspiler")]
    public class EntityBehaviorContainerPatchCommand
    {
        [HarmonyTargetMethod]
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(EntityBehaviorContainer), "addGearToShape", new[]
            {
            typeof(Shape),
            typeof(ItemStack),
            typeof(IAttachableToEntity),
            typeof(string),
            typeof(string),
            typeof(string[]).MakeByRefType(),
            typeof(Dictionary<string, StepParentElementTo>)
        });
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeInstruction[] newInstructions = new CodeInstruction[]
            {
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorContainer), "entity")),
                new(OpCodes.Ldloca_S, 3),
                new(OpCodes.Ldarg_3),
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, AccessTools.Method(typeof(EntityBehaviorContainerPatchCommand), nameof(GetModelReplacement))),
            };
            List<CodeInstruction> codes = new(instructions);
            MethodInfo targetMethod = AccessTools.Method(typeof(IWearableShapeSupplier), "GetShape");

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

        public static void GetModelReplacement(ItemStack? stack, Entity entity, ref Shape defaultShape, IAttachableToEntity yatayata, float damageEffect)
        {
            string currentModel = entity.WatchedAttributes.GetString("skinModel");
            int itemId = stack?.Item?.Id ?? 0;

            CustomModelsSystem system = entity.Api.ModLoader.GetModSystem<CustomModelsSystem>();

            if (currentModel != null && itemId != 0 && system != null)
            {
                if (system.CustomModels[currentModel].WearableShapeReplacers.TryGetValue(itemId, out string? shape))
                {
                    defaultShape = LoadShape(entity.Api, shape);

                    defaultShape?.SubclassForStepParenting(yatayata.GetTexturePrefixCode(stack), damageEffect);
                    defaultShape?.ResolveReferences(entity.World.Logger, currentModel);

                    return;
                }

                string shapePath = yatayata.GetAttachedShape(stack, "default").Base.ToString();

                if (system.CustomModels[currentModel].WearableShapeReplacersByShape.TryGetValue(shapePath, out shape))
                {
                    defaultShape = LoadShape(entity.Api, shape);

                    defaultShape?.SubclassForStepParenting(yatayata.GetTexturePrefixCode(stack), damageEffect);
                    defaultShape?.ResolveReferences(entity.World.Logger, currentModel);

                    return;
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
    }
}