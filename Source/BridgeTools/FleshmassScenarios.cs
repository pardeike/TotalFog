using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class FleshmassScenarios
{
    [Tool(
        "totalfog/fleshmass_fixture",
        Description = "Stage only the native Fleshmass Heart and its mass/bundle/spitter geometry from a public 1.6 Mortal-Heart save in the isolated profile. This does not load the full colony or establish its reproduction. Reload the unchanged fixture afterward."
    )]
    public static object Import(
        string sourceSaveName = "Mortal-Heart11",
        int offsetX = -47,
        int offsetZ = 34
    )
    {
        if (
            !sourceSaveName.StartsWith("Mortal-Heart", StringComparison.Ordinal)
            || sourceSaveName != Path.GetFileNameWithoutExtension(sourceSaveName)
        )
            throw new ArgumentException("Use a Mortal-Heart save in the isolated profile.");
        var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a fixture first.");
        if (!Find.TickManager.Paused || !map.GetVisibility().Initialized)
            throw new InvalidOperationException("Use a paused initialized map.");
        var document = XDocument.Load(GenFilePaths.FilePathForSavedGame(sourceSaveName));
        if (
            document.Root?.Element("meta")?.Element("gameVersion")?.Value.StartsWith("1.6.") != true
        )
            throw new InvalidOperationException("Only public 1.6 input is accepted.");
        var inputMaps = document.Root.Element("game").Element("maps").Elements("li").ToArray();
        if (inputMaps.Length != 1)
            throw new InvalidOperationException("Use a single-map source save.");
        var nodes = inputMaps[0]
            .Element("things")
            .Elements("thing")
            .Where(n =>
                n.Element("def")?.Value
                    is "FleshmassHeart"
                        or "Fleshmass_Active"
                        or "NerveBundle"
                        or "FleshmassSpitter"
            )
            .ToArray();
        if (
            nodes.Length > 5000
            || nodes.Count(n => n.Element("def").Value == "FleshmassHeart") != 1
        )
            throw new InvalidOperationException("Need one Heart and at most 5,000 native objects.");
        var entries = nodes
            .Select(n =>
            {
                var values = n.Element("pos").Value.Trim('(', ')').Split(',');
                var cell = new IntVec3(
                    int.Parse(values[0], CultureInfo.InvariantCulture) + offsetX,
                    0,
                    int.Parse(values[2], CultureInfo.InvariantCulture) + offsetZ
                );
                var def = DefDatabase<ThingDef>.GetNamed(n.Element("def").Value);
                var rect = GenAdj.OccupiedRect(cell, Rot4.North, def.size);
                if (
                    !rect.InBounds(map)
                    || rect.Any(c =>
                        c.GetThingList(map).Any(t => t.Faction?.IsPlayer == true || t is Pawn)
                    )
                )
                    throw new InvalidOperationException(
                        "The shifted geometry overlaps a pawn/player object or the map edge."
                    );
                return new
                {
                    node = n,
                    cell,
                    def,
                };
            })
            .ToArray();
        var heartEntry = entries.Single(e => e.def.defName == "FleshmassHeart");
        var heart = (Building_FleshmassHeart)ThingMaker.MakeThing(heartEntry.def);
        heart.SetFaction(Faction.OfEntities);
        GenSpawn.Spawn(heart, heartEntry.cell, map);
        heart.Comp.threatPoints = float.Parse(
            heartEntry.node.Element("threatPoints").Value,
            CultureInfo.InvariantCulture
        );
        foreach (
            var entry in entries
                .Where(e => e != heartEntry)
                .OrderBy(e => e.def.defName != "Fleshmass_Active")
        )
        {
            var thing = ThingMaker.MakeThing(entry.def);
            thing.SetFaction(Faction.OfEntities);
            GenSpawn.Spawn(thing, entry.cell, map, WipeMode.FullRefund);
            if (thing.TryGetComp<CompFleshmass>() is { } mass)
                mass.source = heart;
            if (thing.TryGetComp<CompFleshmassHeartChild>() is { } child)
                child.heart = heart;
        }
        // Rebuild native connectivity from the linked mass. These fixture-only
        // inputs keep an initially spawned Heart from acting like a new incident.
        heart.Comp.PostSpawnSetup(true);
        AccessTools
            .Field(typeof(CompGrowsFleshmassTendrils), "growthPoints")
            .SetValue(heart.Comp, 0);
        AccessTools.Field(typeof(CompFleshmassHeart), "fleshbeastPoints").SetValue(heart.Comp, 0f);
        return new
        {
            sourceSaveName,
            offsetX,
            offsetZ,
            targetID = heart.ThingID,
            heartPosition = heart.Position.ToString(),
            counts = map
                .listerThings.AllThings.Where(t =>
                    t.Faction == Faction.OfEntities
                    && t.def.defName
                        is "FleshmassHeart"
                            or "Fleshmass_Active"
                            or "NerveBundle"
                            or "FleshmassSpitter"
                )
                .GroupBy(t => t.def.defName)
                .ToDictionary(g => g.Key, g => g.Count()),
            contiguousCells = heart.Comp.ContiguousFleshmass.Count(),
            growthPoints = heart.Comp.GrowthPoints,
            visible = Visibility.IsVisible(heart),
            tick = Find.TickManager.TicksGame,
        };
    }
}
