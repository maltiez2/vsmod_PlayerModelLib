using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static class StatsPatches
{
    public const string SprintSpeedStat = "sprintSpeed";
    public const string SneakSpeedStat = "sneakSpeed";
    public const string BackwardSpeedStat = "backwardSpeed";
    public const string SwimSpeedStat = "swimSpeed";
    public const string WarmthBonusStat = "warmthBonus";
    public const string NutritionFactorStat = "nutritionFactor";
    public const string DamageFactorStat = "damageReceivedFactor";
    public const string MaxSaturationFactorStat = "maxSaturationFactor";
   
    public const string TemporalStabilityDropRateStat = "temporalStabilityDropRate";
    public const string TemporalStabilityOffsetStat = "temporalStabilityOffset";
    public const string TemporalStabilityRecoveryRateStat = "temporalStabilityRecoveryRate";
    public const string TemporalStabilityEffectDirectionStat = "temporalStabilityEffectDirection";
    public const string TemporalStabilityCaveDropRateStat = "temporalStabilityCaveDropRate";
    public const string TemporalStabilitySurfaceDropRateStat = "temporalStabilitySurfaceDropRate";
    public const string TemporalStabilityCaveOffsetStat = "temporalStabilityCaveOffset";
    public const string TemporalStabilitySurfaceOffsetStat = "temporalStabilitySurfaceOffset";

    public static Dictionary<EnumFoodCategory, string> NutritionFactorStats { get; } = new()
    {
        { EnumFoodCategory.Fruit, "fruitNutritionFactor" },
        { EnumFoodCategory.Vegetable, "vegetableNutritionFactor" },
        { EnumFoodCategory.Protein, "proteinNutritionFactor" },
        { EnumFoodCategory.Grain, "grainNutritionFactor" },
        { EnumFoodCategory.Dairy, "dairyNutritionFactor" }
    };

    public static Dictionary<EnumDamageType, string> DamageTypeFactorStats { get; } = new()
    {
        { EnumDamageType.Gravity, "gravityDamageFactor" },
        { EnumDamageType.Fire, "fireDamageFactor" },
        { EnumDamageType.BluntAttack, "bluntDamageFactor" },
        { EnumDamageType.SlashingAttack, "slashingDamageFactor" },
        { EnumDamageType.PiercingAttack, "piercingDamageFactor" },
        { EnumDamageType.Suffocation, "suffocationDamageFactor" },
        { EnumDamageType.Heal, "healDamageFactor" },
        { EnumDamageType.Poison, "poisonDamageFactor" },
        { EnumDamageType.Hunger, "hungerDamageFactor" },
        { EnumDamageType.Crushing, "crushingDamageFactor" },
        { EnumDamageType.Frost, "frostDamageFactor" },
        { EnumDamageType.Electricity, "electricityDamageFactor" },
        { EnumDamageType.Heat, "heatDamageFactor" },
        { EnumDamageType.Injury, "injuryDamageFactor" },
        { EnumDamageType.Acid, "acidDamageFactor" }
    };

    public static int SurfaceCaveLightThreshold { get; set; } = 5;

    public static void Patch(string harmonyId, ICoreAPI api)
    {
        new Harmony(harmonyId).Patch(
                typeof(EntityPlayer).GetMethod("GetWalkSpeedMultiplier", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyMovementSpeedStats)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorBodyTemperature).GetMethod("updateWearableConditions", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyWarmthStats)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorHunger).GetMethod("OnEntityReceiveSaturation", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplySaturationStats)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorHealth).GetMethod("OnEntityReceiveDamage", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyDamageStats)))
            );

        new Harmony(harmonyId).Patch(
               typeof(EntityBehaviorHunger).GetProperty("MaxSaturation", AccessTools.all)?.GetMethod,
               postfix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyMaxSaturationStats)))
           );

        new Harmony(harmonyId).Patch(
               typeof(EntityBehaviorTemporalStabilityAffected).GetMethod("OnGameTick", AccessTools.all),
               prefix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyStabilityStats)))
           );
    }
    public static void Unpatch(string harmonyId)
    {
        new Harmony(harmonyId).Unpatch(typeof(EntityPlayer).GetMethod("GetWalkSpeedMultiplier", AccessTools.all), HarmonyPatchType.Postfix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorBodyTemperature).GetMethod("updateWearableConditions", AccessTools.all), HarmonyPatchType.Postfix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorHunger).GetMethod("OnEntityReceiveSaturation", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorHealth).GetMethod("OnEntityReceiveDamage", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorHunger).GetProperty("MaxSaturation", AccessTools.all)?.GetMethod, HarmonyPatchType.Postfix, harmonyId);
    }

    private static readonly FieldInfo? _entityBehaviorBodyTemperature_clothingBonus = typeof(EntityBehaviorBodyTemperature).GetField("clothingBonus", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _entityBehaviorHunger_hungerTree = typeof(EntityBehaviorHunger).GetField("hungerTree", BindingFlags.NonPublic | BindingFlags.Instance);

    private static void ApplyMovementSpeedStats(EntityPlayer __instance, ref double __result)
    {
        if (__instance.Controls.Sneak)
        {
            __result *= Math.Clamp(__instance.Stats.GetBlended(SneakSpeedStat), 0, 1000);
        }
        else if (__instance.Controls.Sprint)
        {
            __result *= Math.Clamp(__instance.Stats.GetBlended(SprintSpeedStat), 0, 1000);
        }
        else if (__instance.Swimming)
        {
            __result *= Math.Clamp(__instance.Stats.GetBlended(SwimSpeedStat), 0, 1000);
        }

        if (__instance.Controls.Backward)
        {
            __result *= Math.Clamp(__instance.Stats.GetBlended(BackwardSpeedStat), 0, 1000);
        }

        /*if (__instance.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative)
        {
            // For Creative mode players, revert the normal walkspeed modifier from the block the entity is currently standing on/in
            int y1 = (int)(__instance.SidedPos.InternalY - 0.05f);
            int y2 = (int)(__instance.SidedPos.InternalY + 0.01f);
            Block belowBlock = __instance.Api.World.BlockAccessor.GetBlockRaw((int)__instance.SidedPos.X, y1, (int)__instance.SidedPos.Z);
            BlockPos insidePos = new(0);
            insidePos.Set((int)__instance.SidedPos.X, y2, (int)__instance.SidedPos.Z);
            Block insideBlock = __instance.Api.World.BlockAccessor.GetBlock(insidePos);
            float mul = belowBlock.WalkSpeedMultiplier * (y1 == y2 ? 1 : insideBlock.WalkSpeedMultiplier);
        }*/
    }
    private static void ApplyWarmthStats(EntityBehaviorBodyTemperature __instance)
    {
        float value = (float?)_entityBehaviorBodyTemperature_clothingBonus?.GetValue(__instance) ?? 0;
        value += Math.Clamp(__instance.entity.Stats.GetBlended(WarmthBonusStat), -100, 100);
        _entityBehaviorBodyTemperature_clothingBonus?.SetValue(__instance, value);
    }
    private static void ApplySaturationStats(EntityBehaviorHunger __instance, ref float saturation, EnumFoodCategory foodCat)
    {
        saturation *= Math.Max(__instance.entity.Stats.GetBlended(NutritionFactorStat), 0);

        if (NutritionFactorStats.TryGetValue(foodCat, out string? stat))
        {
            saturation *= Math.Max(__instance.entity.Stats.GetBlended(stat), 0);
        }
    }
    private static void ApplyDamageStats(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
    {
        damage *= __instance.entity.Stats.GetBlended(DamageFactorStat);

        if (DamageTypeFactorStats.TryGetValue(damageSource.Type, out string? stat))
        {
            damage *= __instance.entity.Stats.GetBlended(stat);
        }
    }
    private static void ApplyMaxSaturationStats(EntityBehaviorHunger __instance, ref float __result)
    {
        ITreeAttribute? hungerTree = (ITreeAttribute?)_entityBehaviorHunger_hungerTree?.GetValue(__instance);
        if (hungerTree == null) return;

        float statValue = Math.Max(__instance.entity.Stats.GetBlended(DamageFactorStat), 0.001f);
        float prevStatValue = hungerTree.GetFloat("maxSaturationStatMultiplier", 1);

        if (statValue == prevStatValue) return;

        hungerTree.SetFloat("maxSaturationStatMultiplier", statValue);

        __result *= statValue / prevStatValue;

        hungerTree.SetFloat("maxsaturation", __result);
        __instance.entity.WatchedAttributes.MarkPathDirty("hunger");
    }
    private static void ApplyStabilityStats(EntityBehaviorTemporalStabilityAffected __instance, float deltaTime)
    {
        __instance.TempStabChangeVelocity *= Math.Sign(__instance.entity.Stats.GetBlended(TemporalStabilityEffectDirectionStat));
        if (__instance.TempStabChangeVelocity > 0)
        {
            __instance.TempStabChangeVelocity *= Math.Max(__instance.entity.Stats.GetBlended(TemporalStabilityDropRateStat), 0);
        }
        else
        {
            __instance.TempStabChangeVelocity *= Math.Max(__instance.entity.Stats.GetBlended(TemporalStabilityRecoveryRateStat), 0);
        }
        __instance.TempStabChangeVelocity += (__instance.entity.Stats.GetBlended(TemporalStabilityOffsetStat) - 1) * deltaTime;

        
        float caveStabilityFactor = Math.Max(__instance.entity.Stats.GetBlended(TemporalStabilityCaveDropRateStat), 0);
        float surfaceStabilityFactor = Math.Max(__instance.entity.Stats.GetBlended(TemporalStabilitySurfaceDropRateStat), 0);
        if (__instance.TempStabChangeVelocity < 0 && (caveStabilityFactor != 1 || surfaceStabilityFactor != 1))
        {
            int lightLevel = __instance.entity.World.BlockAccessor.GetLightLevel(__instance.entity.Pos.AsBlockPos, EnumLightLevelType.OnlySunLight);
            if (lightLevel < SurfaceCaveLightThreshold)
            {
                __instance.TempStabChangeVelocity *= caveStabilityFactor;
            }
            else
            {
                __instance.TempStabChangeVelocity *= surfaceStabilityFactor;
            }
        }

        
        float caveStabilityOffset = __instance.entity.Stats.GetBlended(TemporalStabilityCaveOffsetStat) - 1;
        float surfaceStabilityOffset = __instance.entity.Stats.GetBlended(TemporalStabilitySurfaceOffsetStat) - 1;
        if (caveStabilityOffset != 0 || surfaceStabilityOffset != 0)
        {
            int lightLevel = __instance.entity.World.BlockAccessor.GetLightLevel(__instance.entity.Pos.AsBlockPos, EnumLightLevelType.OnlySunLight);
            if (lightLevel < SurfaceCaveLightThreshold)
            {
                __instance.TempStabChangeVelocity += caveStabilityOffset * deltaTime;
            }
            else
            {
                __instance.TempStabChangeVelocity += surfaceStabilityOffset * deltaTime;
            }
        }
    }
}
