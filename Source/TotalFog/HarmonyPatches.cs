using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using TotalFog.Utils;
using Verse;
using Verse.Sound;

namespace TotalFog;

internal static class HarmonyPatches
{
    public static IEnumerable<CodeInstruction> ManhunterArrivalTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var body = instructions.ToList();
        var native = AccessTools.Method(typeof(TimeSlower), nameof(TimeSlower.SignalForceNormalSpeedShort));
        int index = body.FindIndex(code => code.Calls(native));
        if (index < 0 || body.Skip(index + 1).Any(code => code.Calls(native)))
        {
            Log.Warning("Total Fog: silent manhunter arrival slowdown needs one native call; retaining native code.");
            return body;
        }
        var call = body[index];
        body.InsertRange(index, new[] { new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(call), new CodeInstruction(OpCodes.Ldarg_1) });
        call.opcode = OpCodes.Call;
        call.operand = AccessTools.Method(typeof(Notifications.SilentRaidPolicy), nameof(Notifications.SilentRaidPolicy.ManhunterArrivalSlowdown));
        return body;
    }

    //Pawn will not target thing that is hidden by the fog
    [HarmonyPrefix]
    public static bool CanSeePreFix(ref bool __result, Thing seer, Thing target)
    {
        if (target == null || seer?.Map == null || target.MapHeld != seer.Map ||
            Visibility.AllowsTarget(seer, target.Position))
        {
            return true;
        }

        __result = false;
        return false;
    }

    //For overlays
    [HarmonyPrefix]
    public static bool DrawOverlayPrefix(Thing t)
    {
        return t.IsFogVisible();
    }

    //For Silhouette
    [HarmonyPrefix]
    public static bool ShouldDrawSilhouettePrefix(Thing thing)
    {
        return thing.IsFogVisible();
    }

    public static class Patch_PlayOneShot
    {
        [HarmonyPrefix]
        public static bool Prefix(ref SoundInfo info)
        {
            if (!(FogSettings.DoAudioCheck || FogSettings.MuteHiddenSounds) || info.Maker.Map == null || !info.Maker.Cell.InBounds(info.Maker.Map))
            {
                return true; // run the original PlayOneShot
            }

            var audibilityFactor = SoundAudibility.GetAudibilityFactor(info.Maker, FogSettings.AudioSourceRange);
            if (audibilityFactor <= 0f)
            {
                return false; // skip the original call entirely
            }

            info.volumeFactor *= audibilityFactor; // otherwise muffle via SoundInfo.volumeFactor

            return true; // run the original PlayOneShot
        }
    }

}
