using System;
using System.Linq;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class AnomalyDiscoveryScenarios
{
    [Tool(
        "totalfog/anomaly_discovery",
        Description = "Spawn a previously unknown Fleshmass Heart in hidden explored terrain, inspect its native codex and music state, or reveal it with a nearby player ghoul. Temporary fixture: reload the unchanged save afterward."
    )]
    public static object Discovery(
        string operation = "probe",
        string targetID = "",
        string entityDef = "FleshmassHeart"
    )
    {
        var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a fixture first.");
        var fog = map.GetVisibility();
        if (!fog.Initialized || !ModsConfig.AnomalyActive)
            throw new InvalidOperationException("Need an initialized map and Anomaly.");
        var def = DefDatabase<ThingDef>.GetNamed(entityDef);
        if (
            def.entityCodexEntry == null
            || def.category is not (ThingCategory.Building or ThingCategory.Item)
        )
            throw new ArgumentException(
                "Use an Anomaly building or item with a codex entry.",
                nameof(entityDef)
            );
        Thing target;
        if (operation == "spawn")
        {
            if (Find.EntityCodex.Discovered(def.entityCodexEntry))
                throw new InvalidOperationException(
                    "Use a fixture that has not discovered " + entityDef + "."
                );
            var cell = map.AllCells.First(c =>
                c.x > 4
                && c.z > 4
                && c.x < map.Size.x - 4
                && c.z < map.Size.z - 4
                && !fog.IsShown(Faction.OfPlayer, c)
                && GenAdj
                    .OccupiedRect(c, Rot4.North, def.size)
                    .All(p =>
                        p.InBounds(map)
                        && !fog.IsShown(Faction.OfPlayer, p)
                        && p.Standable(map)
                        && p.GetThingList(map).All(t => t is not (Pawn or Building))
                    )
            );
            foreach (var p in GenAdj.OccupiedRect(cell, Rot4.North, def.size))
                map.fogGrid.Unfog(p);
            target = GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
        }
        else
        {
            target = map.listerThings.AllThings.First(t => t.ThingID == targetID);
            if (operation == "reveal")
            {
                var cell = CellFinder.RandomClosewalkCellNear(target.Position, map, 4);
                map.fogGrid.Unfog(cell);
                var viewer = PawnGenerator.GeneratePawn(
                    DefDatabase<PawnKindDef>.GetNamed("Ghoul"),
                    Faction.OfPlayer
                );
                GenSpawn.Spawn(viewer, cell, map);
                var wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = 1800;
                viewer.jobs.StartJob(wait, JobCondition.InterruptForced);
                viewer.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
            }
            else if (operation != "probe")
                throw new ArgumentException("Use spawn, probe, or reveal.", nameof(operation));
        }
        var entry = def.entityCodexEntry;
        bool suppression = FogSettings.SuppressCombatMusic;
        var music = Find.MusicManagerPlay;
        var transition = new HorrorRelaxTransition();
        // Remove danger as a test prerequisite; use the actual native worker,
        // its ordinary map scan, and the real hidden Heart to satisfy it.
        transition.InitializeWorker(new MusicTransitionDef(), music);
        bool originalMusic,
            suppressedMusic;
        try
        {
            FogSettings.SuppressCombatMusic = false;
            originalMusic = transition.IsTransitionSatisfied();
            FogSettings.SuppressCombatMusic = true;
            suppressedMusic = transition.IsTransitionSatisfied();
        }
        finally
        {
            FogSettings.SuppressCombatMusic = suppression;
        }
        return new
        {
            targetID = target.ThingID,
            x = target.Position.x,
            z = target.Position.z,
            tick = Find.TickManager.TicksGame,
            visible = Visibility.IsVisible(target),
            observed = target.TryGetComp<CompFog>().HideFromPlayer.SeenByPlayer,
            studyEnabled = Find.Anomaly.AnomalyStudyEnabled,
            discoveryResearchCount = entry.discoveredResearchProjects?.Count ?? 0,
            entryDiscovered = Find.EntityCodex.Discovered(entry),
            defDiscovered = Find.EntityCodex.Discovered(def),
            letters = Find
                .LetterStack.LettersListForReading.OfType<ChoiceLetter_EntityDiscovered>()
                .Count(l => l.codexEntry == entry),
            archivedLetters = Find
                .Archive.ArchivablesListForReading.OfType<ChoiceLetter_EntityDiscovered>()
                .Count(l => l.codexEntry == entry),
            originalMusic,
            suppressedMusic,
            currentTransition = music.TriggeredTransition?.def.defName,
        };
    }
}
