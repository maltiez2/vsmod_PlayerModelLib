using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
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
    public const string HealingFactorStat = "healingReceivedFactor";
    public const string MaxSaturationFactorStat = "maxSaturationFactor";
    public const string BuoyancyFactorStat = "buoyancyFactor";
    public const string CanSwimStat = "canSwim";
    public const string TemporalStabilityDropRateStat = "temporalStabilityDropRate";
    public const string TemporalStabilityOffsetStat = "temporalStabilityOffset";
    public const string TemporalStabilityRecoveryRateStat = "temporalStabilityRecoveryRate";
    public const string TemporalStabilityEffectDirectionStat = "temporalStabilityEffectDirection";
    public const string TemporalStabilityCaveDropRateStat = "temporalStabilityCaveDropRate";
    public const string TemporalStabilitySurfaceDropRateStat = "temporalStabilitySurfaceDropRate";
    public const string TemporalStabilityCaveOffsetStat = "temporalStabilityCaveOffset";
    public const string TemporalStabilitySurfaceOffsetStat = "temporalStabilitySurfaceOffset";
    public const string NightWalkSpeed = "nightWalkSpeed";
    public const string DayWalkSpeed = "dayWalkSpeed";
    public const string NightDamageFactor = "nightDamageFactor";
    public const string DayDamageFactor = "dayDamageFactor";
    public const string NightHealingFactor = "nightHealingFactor";
    public const string DayHealingFactor = "dayHealingFactor";
    public const string DarknessWalkSpeed = "darknessWalkSpeed";
    public const string LightWalkSpeed = "lightWalkSpeed";
    public const string DarknessDamageFactor = "darknessDamageFactor";
    public const string LightDamageFactor = "lightDamageFactor";
    public const string DarknessHealingFactor = "darknessHealingFactor";
    public const string LightHealingFactor = "lightHealingFactor";
    public const string SaturationLossStat = "saturationLossFactor";
    public const string BreathTypeStat = "breathType";
    public const string DarknessCanBreathStat = "canBreathInLight";
    public const string LightCanBreathStat = "canBreathInDarkness";
    public const string CaveCanBreathStat = "canBreathInCaves";
    public const string SurfaceCanBreathStat = "canBreathOnSurface";

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

    public static int SurfaceCaveLightThreshold { get; set; } = 8;
    public static int DarknessLightThreshold { get; set; } = 3;
    public static int LightLightThreshold { get; set; } = 8;
    public static Vintagestory.API.Common.DayTimeFrame DayFrame { get; set; } = new(6, 18);

    public static void Patch(string harmonyId, ICoreAPI api)
    {
        if (_applied) return;

        new Harmony(harmonyId).Patch(
                typeof(EntityPlayer).GetMethod("GetWalkSpeedMultiplier", AccessTools.all),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyMovementSpeedStats)))
            );

        new Harmony(harmonyId).Patch(
                typeof(PModulePlayerInLiquid).GetMethod("HandleSwimming", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(HandleSwimming)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorHunger).GetMethod("OnEntityReceiveSaturation", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplySaturationStats)))
            );

        new Harmony(harmonyId).Patch(
                typeof(EntityBehaviorHealth).GetMethod("OnEntityReceiveDamage", AccessTools.all),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyDamageStats)))
            );

        new Harmony(harmonyId).Patch(
               typeof(EntityBehaviorHunger).GetProperty("MaxSaturation", AccessTools.all)?.GetMethod,
               postfix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyMaxSaturationStats)))
           );

        new Harmony(harmonyId).Patch(
               typeof(EntityBehaviorTemporalStabilityAffected).GetMethod("OnGameTick", AccessTools.all),
               prefix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyStabilityStats)))
           );

        new Harmony(harmonyId).Patch(
               typeof(EntityBehaviorHunger).GetMethod("ReduceSaturation", AccessTools.all),
               prefix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplySaturationLossStats)))
           );

        new Harmony(harmonyId).Patch(
               typeof(EntityBehaviorBreathe).GetMethod("Check", AccessTools.all),
               postfix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyBreathCheckStats)))
           );

        new Harmony(harmonyId).Patch(
               typeof(PModuleGravity).GetMethod("DoApply", AccessTools.all),
               postfix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyGravityStats)))
           );

        new Harmony(harmonyId).Patch(
               typeof(EntityBehaviorPlayerPhysics).GetMethod("SetPlayerControls", AccessTools.all),
               prefix: new HarmonyMethod(AccessTools.Method(typeof(StatsPatches), nameof(ApplyControlsStats)))
           );

        _applied = true;
    }
    public static void Unpatch(string harmonyId)
    {
        if (!_applied) return;

        new Harmony(harmonyId).Unpatch(typeof(EntityPlayer).GetMethod("GetWalkSpeedMultiplier", AccessTools.all), HarmonyPatchType.Postfix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(PModulePlayerInLiquid).GetMethod("HandleSwimming", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorHunger).GetMethod("OnEntityReceiveSaturation", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorHealth).GetMethod("OnEntityReceiveDamage", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorHunger).GetProperty("MaxSaturation", AccessTools.all)?.GetMethod, HarmonyPatchType.Postfix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorTemporalStabilityAffected).GetMethod("OnGameTick", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorHunger).GetMethod("ReduceSaturation", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorBreathe).GetMethod("Check", AccessTools.all), HarmonyPatchType.Postfix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(PModuleGravity).GetMethod("DoApply", AccessTools.all), HarmonyPatchType.Postfix, harmonyId);
        new Harmony(harmonyId).Unpatch(typeof(EntityBehaviorPlayerPhysics).GetMethod("SetPlayerControls", AccessTools.all), HarmonyPatchType.Prefix, harmonyId);

        _applied = false;
    }

    private static readonly FieldInfo? _entityBehaviorHunger_hungerTree = typeof(EntityBehaviorHunger).GetField("hungerTree", BindingFlags.NonPublic | BindingFlags.Instance);
    private static bool _applied = false;
    private const int _standardHoursPerDay = 24;
    private static bool _handleSwimming = true;

    private static bool HandleSwimming(PModulePlayerInLiquid __instance, float dt, Entity entity, EntityPos pos, EntityControls controls)
    {
        if (!_handleSwimming) return true;

        float factor = Math.Clamp((entity as EntityPlayer).Stats.GetBlended(SwimSpeedStat), 0, 10);

        Vintagestory.API.MathTools.Vec3d prev = pos.Motion.Clone();
        _handleSwimming = false;
        __instance.HandleSwimming(dt, entity, pos, controls);
        _handleSwimming = true;
        pos.Motion.Set(prev + (pos.Motion - prev) * factor);

        return false;
    }
    private static void ApplyMovementSpeedStats(EntityPlayer __instance, ref double __result)
    {
        float baseWalkSpeedStat = __instance.Stats.GetBlended("walkspeed");
        __result /= baseWalkSpeedStat;

        float resultingMultiplier = baseWalkSpeedStat;

        if (IsCurrentlyDay(__instance.Api.World))
        {
            float dayStat = __instance.Stats.GetBlended(DayWalkSpeed) - 1;
            resultingMultiplier += dayStat;
        }
        else
        {
            float nightStat = __instance.Stats.GetBlended(NightWalkSpeed) - 1;
            resultingMultiplier += nightStat;
        }

        float darknessWalkSpeed = __instance.Stats.GetBlended(DarknessWalkSpeed) - 1;
        float lightWalkSpeed = __instance.Stats.GetBlended(LightDamageFactor) - 1;
        if (darknessWalkSpeed != 0 || lightWalkSpeed != 0)
        {
            int lightLevel = GetLightLevel(__instance, EnumLightLevelType.MaxTimeOfDayLight);
            if (lightLevel < DarknessLightThreshold)
            {
                resultingMultiplier += darknessWalkSpeed;
            }
            else if (lightLevel > LightLightThreshold)
            {
                resultingMultiplier += lightWalkSpeed;
            }
        }

        __result *= resultingMultiplier;


        if (__instance.Controls.Sneak)
        {
            __result *= Math.Clamp(__instance.Stats.GetBlended(SneakSpeedStat), 0, 1000);
        }
        else if (__instance.Controls.Sprint)
        {
            __result *= Math.Clamp(__instance.Stats.GetBlended(SprintSpeedStat), 0, 1000);
        }
        if (__instance.Controls.Backward)
        {
            __result *= Math.Max(__instance.Stats.GetBlended(BackwardSpeedStat), 0);
        }
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
        float resultingMultiplier = 1;

        float generalStat = __instance.entity.Stats.GetBlended(damageSource.Type == EnumDamageType.Heal ? HealingFactorStat : DamageFactorStat) - 1;
        resultingMultiplier += generalStat;

        if (DamageTypeFactorStats.TryGetValue(damageSource.Type, out string? stat))
        {
            float specificStat = __instance.entity.Stats.GetBlended(stat) - 1;
            resultingMultiplier += specificStat;
        }

        if (IsCurrentlyDay(__instance.entity.Api.World))
        {
            float dayStat = __instance.entity.Stats.GetBlended(damageSource.Type == EnumDamageType.Heal ? DayHealingFactor : DayDamageFactor) - 1;
            resultingMultiplier += dayStat;
        }
        else
        {
            float nightStat = __instance.entity.Stats.GetBlended(damageSource.Type == EnumDamageType.Heal ? NightHealingFactor : NightDamageFactor) - 1;
            resultingMultiplier += nightStat;
        }

        float darknessDamageFactor = __instance.entity.Stats.GetBlended(damageSource.Type == EnumDamageType.Heal ? DarknessHealingFactor : DarknessDamageFactor) - 1;
        float lightDamageFactor = __instance.entity.Stats.GetBlended(damageSource.Type == EnumDamageType.Heal ? LightHealingFactor : LightDamageFactor) - 1;
        if (darknessDamageFactor != 0 || lightDamageFactor != 0)
        {
            int lightLevel = GetLightLevel(__instance.entity, EnumLightLevelType.MaxTimeOfDayLight);
            if (lightLevel < DarknessLightThreshold)
            {
                resultingMultiplier += darknessDamageFactor;
            }
            else if (lightLevel > LightLightThreshold)
            {
                resultingMultiplier += lightDamageFactor;
            }
        }

        damage *= MathF.Max(resultingMultiplier, 0);
    }
    private static void ApplyMaxSaturationStats(EntityBehaviorHunger __instance, ref float __result)
    {
        ITreeAttribute? hungerTree = (ITreeAttribute?)_entityBehaviorHunger_hungerTree?.GetValue(__instance);
        if (hungerTree == null) return;

        float statValue = Math.Max(__instance.entity.Stats.GetBlended(MaxSaturationFactorStat), 0.001f);
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

        float caveStabilityFactor = Math.Max(__instance.entity.Stats.GetBlended(TemporalStabilityCaveDropRateStat), 0);
        float surfaceStabilityFactor = Math.Max(__instance.entity.Stats.GetBlended(TemporalStabilitySurfaceDropRateStat), 0);
        if (__instance.TempStabChangeVelocity < 0 && (caveStabilityFactor != 1 || surfaceStabilityFactor != 1))
        {
            int lightLevel = GetLightLevel(__instance.entity, EnumLightLevelType.OnlySunLight);
            if (lightLevel < SurfaceCaveLightThreshold)
            {
                __instance.TempStabChangeVelocity *= caveStabilityFactor;
            }
            else
            {
                __instance.TempStabChangeVelocity *= surfaceStabilityFactor;
            }
        }

        __instance.TempStabChangeVelocity += CorrectStabilityOffset(__instance.entity.Stats.GetBlended(TemporalStabilityOffsetStat) - 1) * deltaTime;

        float caveStabilityOffset = CorrectStabilityOffset(__instance.entity.Stats.GetBlended(TemporalStabilityCaveOffsetStat) - 1);
        float surfaceStabilityOffset = CorrectStabilityOffset(__instance.entity.Stats.GetBlended(TemporalStabilitySurfaceOffsetStat) - 1);
        if (caveStabilityOffset != 0 || surfaceStabilityOffset != 0)
        {
            int lightLevel = GetLightLevel(__instance.entity, EnumLightLevelType.OnlySunLight);
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
    private static void ApplySaturationLossStats(EntityBehaviorHunger __instance, ref float satLossMultiplier)
    {
        float statMultiplier = __instance.entity.Stats.GetBlended(SaturationLossStat);
        satLossMultiplier *= Math.Max(statMultiplier, 0);
        if (statMultiplier <= 0)
        {
            __instance.Saturation = __instance.MaxSaturation;
        }
    }
    private static void ApplyBreathCheckStats(EntityBehaviorBreathe __instance)
    {
        if (IsInCreative(__instance.entity)) return;

        float type = __instance.entity.Stats.GetBlended(BreathTypeStat) - 1;
        if (type < 0 - float.Epsilon)
        {
            __instance.HasAir = !__instance.HasAir;
        }
        else if (type > 0 + float.Epsilon)
        {
            __instance.HasAir = true;
        }
        else
        {
            // preserve vanilla behavior
        }

        float caveCanBreath = __instance.entity.Stats.GetBlended(CaveCanBreathStat) - 1;
        float surfaceCanBreath = __instance.entity.Stats.GetBlended(SurfaceCanBreathStat) - 1;
        if (caveCanBreath != 0 || surfaceCanBreath != 0)
        {
            int lightLevel = GetLightLevel(__instance.entity, EnumLightLevelType.OnlySunLight);
            if (lightLevel < SurfaceCaveLightThreshold)
            {
                if (caveCanBreath < 0 - float.Epsilon)
                {
                    __instance.HasAir = false;
                }
                else if (caveCanBreath > 0 + float.Epsilon)
                {
                    __instance.HasAir = true;
                }
            }
            else
            {
                if (surfaceCanBreath < 0 - float.Epsilon)
                {
                    __instance.HasAir = false;
                }
                else if (surfaceCanBreath > 0 + float.Epsilon)
                {
                    __instance.HasAir = true;
                }
            }
        }

        float darknessCanBreath = __instance.entity.Stats.GetBlended(DarknessCanBreathStat) - 1;
        float lightCanBreath = __instance.entity.Stats.GetBlended(LightCanBreathStat) - 1;
        if (darknessCanBreath != 0 || lightCanBreath != 0)
        {
            int lightLevel = GetLightLevel(__instance.entity, EnumLightLevelType.MaxTimeOfDayLight);
            if (lightLevel < DarknessLightThreshold)
            {
                if (darknessCanBreath < 0 - float.Epsilon)
                {
                    __instance.HasAir = false;
                }
                else if (darknessCanBreath > 0 + float.Epsilon)
                {
                    __instance.HasAir = true;
                }
            }
            else if (lightLevel > LightLightThreshold)
            {
                if (lightCanBreath < 0 - float.Epsilon)
                {
                    __instance.HasAir = false;
                }
                else if (lightCanBreath > 0 + float.Epsilon)
                {
                    __instance.HasAir = true;
                }
            }
        }
    }
    private static void ApplyGravityStats(float dt, Entity entity, EntityPos pos, EntityControls controls)
    {
        if (pos.Y < -100) return;

        double trueGravity = GlobalConstants.GravityPerSecond * dt;
        double gravity = GlobalConstants.GravityPerSecond * (entity.FeetInLiquid ? 0.33f : 1f) * dt;
        float buoyancyFactor = entity.Stats.GetBlended(BuoyancyFactorStat) - 1;

        if (entity.Swimming)
        {
            pos.Motion.Y += gravity * buoyancyFactor;
        }
        else if (entity.Swimming && controls.TriesToMove && entity.Alive)
        {
            pos.Motion.Y += trueGravity * buoyancyFactor;
        }
    }
    private static void ApplyControlsStats(EntityBehaviorPlayerPhysics __instance)
    {
        float canSwim = __instance.entity.Stats.GetBlended(CanSwimStat) - 1;
        if (canSwim < 0 - float.Epsilon)
        {
            __instance.Entity.Swimming = false;
        }
    }

    private static bool IsCurrentlyDay(IWorldAccessor world)
    {
        // introduce a bit of randomness so that (e.g.) hens do not all wake up simultaneously at 06:00, which looks artificial
        // essentially works in fractions of a day, instead of hours, but for convinience scaled to use 24 hours per day scale
        double hourOfDay = world.Calendar.HourOfDay / world.Calendar.HoursPerDay * _standardHoursPerDay;

        return DayFrame.Matches(hourOfDay);
    }
    private static int GetLightLevel(Entity entity, EnumLightLevelType lightType)
    {
        return entity.World.BlockAccessor.GetLightLevel(entity.Pos.AsBlockPos, lightType);
    }
    private static bool IsInCreative(Entity entity)
    {
        if (entity is EntityPlayer player)
        {
            EnumGameMode mode = entity.World?.PlayerByUid(player?.PlayerUID ?? "")?.WorldData?.CurrentGameMode ?? EnumGameMode.Survival;
            if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator)
            {
                return true;
            }
        }

        return false;
    }
    private static float CorrectStabilityOffset(float value)
    {
        if (value < 0)
        {
            return value * 800;
        }
        else
        {
            return value * 200;
        }
    }
}
