using HarmonyLib;
using RimWorld;
using RimWorldRealFoW.Utils;
using Verse;
using Verse.Sound;

namespace RimWorldRealFoW;

internal static class HarmonyPatches
{
    //Pawn will not target thing that is hidden by the fog
    [HarmonyPrefix]
    public static bool CanSeePreFix(ref bool __result, Thing seer, Thing target)
    {
        if (seer is not Pawn seerPawn || seer.Faction == null
                                      || !RfowSettings.AISmart || seer.Faction == Faction.OfPlayer
                                      || !seerPawn.RaceProps.Humanlike)
        {
            return true;
        }

        __result = seer.Map.GetMapComponentSeenFog().IsShown(seer.Faction, target.Position);

        return __result;
    }

    //For overlays
    [HarmonyPrefix]
    public static bool DrawOverlayPrefix(Thing t)
    {
        return t.FowIsVisible();
    }

    //For dubs minimap
    [HarmonyPostfix]
    public static void FoggedThingPostfix(ref bool __result, Thing thing)
    {
        if (thing == null)
        {
            return;
        }

        if (__result)
        {
            return;
        }

        __result = !thing.FowIsVisible();
    }

    [HarmonyPostfix]
    public static void FoggedCellPostfix(ref bool __result, IntVec3 c, Map map)
    {
        if (__result)
        {
            return;
        }

        if (!RealFoWModStarter.DubsMintMinimapLoaded)
        {
            return;
        }

        var mapComponentSeenFog = map.GetMapComponentSeenFog();
        if (mapComponentSeenFog == null)
        {
            return;
        }

        if (!mapComponentSeenFog.knownCells[map.cellIndices.CellToIndex(c)])
        {
            __result = true;
        }
    }

    //For Silhouette
    [HarmonyPrefix]
    public static bool ShouldDrawSilhouettePrefix(Thing thing)
    {
        return thing.FowIsVisible();
    }

    //For interaction bubbles
    [HarmonyPrefix]
    public static bool DrawBubblePrefix(Pawn pawn)
    {
        if (!RfowSettings.HideSpeakBubble)
        {
            return true;
        }

        return pawn is not { IsColonist: false, Map: not null } ||
               pawn.Map.GetMapComponentSeenFog().IsShown(Faction.OfPlayer, pawn.Position);
    }

    //For suppressing letter
    [HarmonyPrefix]
    public static bool ReceiveLetterPrefix(ref Letter let)
    {
        if (let?.lookTargets == null)
        {
            return true;
        }

        if (let.def == LetterDefOf.NegativeEvent && RfowSettings.HideEventNegative)
        {
            return false;
        }

        if (let.def == LetterDefOf.NeutralEvent && RfowSettings.HideEventNeutral)
        {
            return false;
        }

        if (let.def == LetterDefOf.PositiveEvent && RfowSettings.HideEventPositive)
        {
            return false;
        }

        if (let.def == LetterDefOf.ThreatBig && RfowSettings.HideThreatBig)
        {
            return false;
        }

        if (let.def == LetterDefOf.ThreatSmall && RfowSettings.HideThreatSmall)
        {
            return false;
        }

        // If DelayAlertsUntilSeen is enabled, defer the letter only if thing is NOT visible
        if (!RfowSettings.DelayAlertsUntilSeen || !let.lookTargets.PrimaryTarget.HasThing)
        {
            return true;
        }

        var thing = let.lookTargets.PrimaryTarget.Thing;
        if (thing?.Map == null)
        {
            return true;
        }

        // Only defer if thing is not visible
        if (!thing.FowIsVisible())
        {
            var pendingAlertManager = thing.Map.GetPendingAlertManager();
            if (pendingAlertManager == null)
            {
                return let.def != LetterDefOf.ThreatSmall || !RfowSettings.HideThreatSmall;
            }

            // Store current game speed before slowdown
            var currentSpeed = Find.TickManager.CurTimeSpeed;
            RealFoWModStarter.LogMessage(
                $"Deferring ThreatBig letter for {thing.Label}, stored speed: {currentSpeed}");
            pendingAlertManager.RegisterPendingLetter(let, thing, currentSpeed);
            return false; // Block the letter for now
        }

        // Thing IS visible, allow letter through normally
        RealFoWModStarter.LogMessage($"ThreatBig letter visible, allowing through: {thing.Label}");
        return true;

        // DelayAlertsUntilSeen disabled or nothing, just block as normal
    }

    [HarmonyPostfix]
    public static void LandingEndedPostfix()
    {
        if (RfowSettings.ClearFogDuringTargeting)
        {
            Find.CurrentMap?.mapDrawer?.RegenerateEverythingNow();
        }
    }

    // Registers sustainers in a dictionary to be later removed when Thing is hidden

    public static class Patch_RegisterSustainer
    {
        [HarmonyPostfix]
        public static void Postfix(Sustainer newSustainer)
        {
            if (newSustainer?.info.Maker.Thing is { } thing)
            {
                FoW_AudioCache.Register(thing, newSustainer);
            }
        }
    }

    public static class Patch_UnregisterSustainer
    {
        [HarmonyPostfix]
        public static void Postfix(Sustainer __instance)
        {
            FoW_AudioCache.Unregister(__instance);
        }
    }

    public static class Patch_PlayOneShot
    {
        [HarmonyPrefix]
        public static bool Prefix(ref SoundInfo info)
        {
            if (!RfowSettings.DoAudioCheck || info.Maker.Map == null || !info.Maker.Cell.InBounds(info.Maker.Map))
            {
                return true; // run the original PlayOneShot
            }

            var audibilityFactor = FoW_AudioCache.GetAudibilityFactor(info.Maker, RfowSettings.AudioSourceRange);
            if (audibilityFactor <= 0f)
            {
                return false; // skip the original call entirely
            }

            info.volumeFactor *= audibilityFactor; // otherwise muffle via SoundInfo.volumeFactor

            return true; // run the original PlayOneShot
        }
    }

    public static class Patch_TrySpawnSustainer
    {
        [HarmonyPrefix]
        public static bool Prefix(SoundInfo info)
        {
            if (!RfowSettings.DoAudioCheck || info.Maker.Thing is not { } thing)
            {
                return true;
            }

            var audibilityFactor = FoW_AudioCache.GetAudibilityFactor(thing, RfowSettings.AudioSourceRange);
            if (audibilityFactor <= 0f)
            {
                return false; // mute entirely
            }

            info.volumeFactor *= audibilityFactor; // muffle looping sound

            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Sustainer __result)
        {
            if (__result is { info.volumeFactor: <= 0f })
            {
                __result.End();
            }
        }
    }

    // Prevent forced slowdown when deferring ThreatBig letters
    public static class Patch_LetterStackReceiveLetter
    {
        [HarmonyPostfix]
        public static void Postfix(Letter let)
        {
            // This postfix runs AFTER LetterStack.ReceiveLetter completes
            // Only called if the letter was NOT deferred (i.e., ReceiveLetterPrefix returned true)

            if (!RfowSettings.DelayAlertsUntilSeen)
            {
                return;
            }

            if (let != null && (let.def != LetterDefOf.ThreatBig || !let.lookTargets.PrimaryTarget.HasThing))
            {
                return;
            }

            var thing = let?.lookTargets.PrimaryTarget.Thing;
            if (thing?.Map != null && thing.FowIsVisible() && PendingAlertManager.IsReplayingLetter)
            {
                RealFoWModStarter.LogMessage(
                    $"LetterStack.ReceiveLetter completed for visible deferred threat letter: {thing.Label}");
            }
        }
    }
}