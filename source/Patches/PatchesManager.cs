using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PlayerModelLib;

public static class PatchesManager
{
    public static bool PatchedClientSide { get; private set; } = false;
    public static bool PatchedServerSide { get; private set; } = false;
    public static bool PatchedUniversalSide { get; private set; } = false;

    public const string HarmonyIdPrefix = "PlayerModelLib";
    public const string TranspilerPatchesId = HarmonyIdPrefix + "Transpilers";
    public const string GeneralPatchesId = HarmonyIdPrefix + "General";
    public const string StatsPatchesId = HarmonyIdPrefix + "Stats";
    public const string OffThreadPatchesId = HarmonyIdPrefix + "OffThread";


    public static void Patch(ICoreAPI api)
    {
        OtherPatches.SetApi(api);

        if (!PatchedUniversalSide)
        {
            PatchedUniversalSide = true;
            PatchUniversal(api);
        }

        if (api is ICoreServerAPI && !PatchedServerSide)
        {
            PatchedServerSide = true;
            PatchServer();
        }

        if (api is ICoreClientAPI clientApi && !PatchedClientSide)
        {
            PatchedClientSide = true;
            PatchClient(clientApi);
        }
    }
    public static void Unpatch()
    {
        if (PatchedUniversalSide)
        {
            PatchedUniversalSide = false;
            UnpatchUniversal();
        }

        if (PatchedServerSide)
        {
            PatchedServerSide = false;
            UnpatchServer();
        }

        if (PatchedClientSide)
        {
            PatchedClientSide = false;
            UnpatchClient();
        }
    }



    private static void PatchUniversal(ICoreAPI api)
    {
        new Harmony(TranspilerPatchesId).PatchAll();
        OtherPatches.Patch(GeneralPatchesId, api);
        StatsPatches.Patch(StatsPatchesId, api);
    }
    private static void PatchClient(ICoreClientAPI api)
    {
        // no client patches
    }
    private static void PatchServer()
    {
        // no server patches
    }

    private static void UnpatchUniversal()
    {
        new Harmony(TranspilerPatchesId).UnpatchAll(TranspilerPatchesId);
        OtherPatches.Unpatch(GeneralPatchesId);
        StatsPatches.Unpatch(StatsPatchesId);
    }
    private static void UnpatchClient()
    {
        // no patches to unpatch
    }
    private static void UnpatchServer()
    {
        // no server patches
    }
}
