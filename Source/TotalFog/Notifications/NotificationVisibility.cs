using RimWorld;
using TotalFog.Presentation;
using Verse;

namespace TotalFog.Notifications;

internal static class NotificationVisibility
{
    internal static bool HasVisibleTarget(LookTargets targets)
    {
        if (targets?.targets == null || targets.targets.Count == 0)
            return true;
        foreach (var target in targets.targets)
        {
            if (!target.IsValid)
                continue;
            if (target.HasThing)
            {
                // Colony health and prisoner events are known to the colony,
                // even while an individual is outside another observer's sight.
                if (
                    !target.Thing.Destroyed
                    && target.Thing is Pawn pawn
                    && (
                        pawn.Faction == Faction.OfPlayer
                        || pawn.IsPrisonerOfColony && pawn.HostFaction == Faction.OfPlayer
                    )
                )
                    return true;
                if (
                    !target.Thing.Destroyed
                    && ThingVisibility.IsVisible(target.Thing, allowMemory: false)
                )
                    return true;
            }
            else if (
                target.Map == null
                || ThingVisibility.Unrestricted(target.Map)
                || target.Cell.InBounds(target.Map)
                    && target.Map.GetVisibility().IsShown(Faction.OfPlayer, target.Cell)
            )
                return true;
        }
        return false;
    }

    internal static bool HasLiveTarget(LookTargets targets)
    {
        if (targets?.targets == null)
            return false;
        foreach (var target in targets.targets)
            if (
                target.IsValid
                && (
                    !target.HasThing
                    || !target.Thing.Destroyed && target.Thing is not Pawn { Dead: true }
                )
            )
                return true;
        return false;
    }

    internal static Map MapFor(LookTargets targets)
    {
        if (targets?.targets == null)
            return null;
        foreach (var target in targets.targets)
            if ((target.HasThing ? target.Thing.MapHeld : target.Map) is { } map)
                return map;
        return null;
    }

    internal static bool Suppressed(LetterDef def) =>
        def == LetterDefOf.NegativeEvent && FogSettings.HideEventNegative
        || def == LetterDefOf.NeutralEvent && FogSettings.HideEventNeutral
        || def == LetterDefOf.PositiveEvent && FogSettings.HideEventPositive
        || def == LetterDefOf.ThreatBig && FogSettings.HideThreatBig
        || def == LetterDefOf.ThreatSmall && FogSettings.HideThreatSmall;

    internal static bool Suppressed(MessageTypeDef def) =>
        def == MessageTypeDefOf.NegativeEvent && FogSettings.HideEventNegative
        || def == MessageTypeDefOf.NeutralEvent && FogSettings.HideEventNeutral
        || def == MessageTypeDefOf.PositiveEvent && FogSettings.HideEventPositive
        || def == MessageTypeDefOf.ThreatBig && FogSettings.HideThreatBig
        || def == MessageTypeDefOf.ThreatSmall && FogSettings.HideThreatSmall;
}
