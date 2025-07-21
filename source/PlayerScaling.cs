using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public static class PlayerScalingPatches
{
    /*[HarmonyPatch(typeof(EntityPlayerShapeRenderer), "loadModelMatrixForPlayer")]
    [HarmonyPostfix()]
    public static void LoadModelMatrixForPlayer(EntityPlayerShapeRenderer __instance, Entity entity, ref float[] ___ModelMat)
    {
        if (entity is EntityPlayer player)
        {
            URPGORacesModSystem.GetPlayerRace(player).ApplyModelMatrixForPlayerRace(entity, ref ___ModelMat);
        }
    }

    [HarmonyPatch(typeof(EntityShapeRenderer), "loadModelMatrixForGui")]
    [HarmonyPostfix()]
    public static void LoadModelMatrixForGUI(EntityShapeRenderer __instance, Entity entity, ref float[] ___ModelMat)
    {
        if (__instance is EntityPlayerShapeRenderer)
        {
            URPGORacesModSystem.GetPlayerRace(entity as EntityPlayer).ApplyModelMatrixForPlayerRaceInGui(entity, ref ___ModelMat);
        }
    }

    /// <summary>
    /// Called from the race patcher based on the player's race.
    /// Note that this doesn't get called in GUI screens.
    /// </summary>
    public void ApplyModelMatrixForPlayerRace(Entity entity, ref float[] ___ModelMat)
    {
        if (___ModelMat != null && ___ModelMat.Length > 0 && entity is EntityPlayer)
        {
            //Need to undo the last translation to ensure that the player scales from the correct origin.
            Mat4f.Translate(___ModelMat, ___ModelMat, 0.5f, 0f, 0.5f);

            //Scale the model by the race scale.
            Mat4f.Scale(___ModelMat, ___ModelMat, raceScale.X, raceScale.Y, raceScale.Z);

            //Redo the last translation.
            Mat4f.Translate(___ModelMat, ___ModelMat, -0.5f, 0f, -0.5f);
        }
    }

    private bool Event_IsPlayerReady(ref EnumHandling handling)
    {
        EntityPlayer player = capi.World.Player.Entity;
        RaceType currentRace = GetPlayerRace(player);
        player.Properties.EyeHeight = currentRace.raceScale.Y * 1.7f;
        player.HeadBobbingAmplitude = currentRace.raceScale.Y;
        player.Properties.CollisionBoxSize = new Vec2f(0.6f * currentRace.raceScale.X, 1.85f * currentRace.raceScale.Y);
        Traverse.Create(player.Player).Method("updateColSelBoxes").GetValue();
        player.LocalEyePos.Y = currentRace.raceScale.Y * 1.7f;
        return true;
    }

    private void Event_OnEntityLoaded(Entity entity)
    {
        if (entity is EntityPlayer player)
        {
            player.Properties.CollisionBoxSize = new Vec2f(0.6f * GetPlayerRace(player).raceScale.X, 1.85f * GetPlayerRace(player).raceScale.Y);
            Traverse.Create(player.Player).Method("updateColSelBoxes").GetValue();
        }
    }*/
}
