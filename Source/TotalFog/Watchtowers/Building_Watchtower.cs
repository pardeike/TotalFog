using System.Collections.Generic;
using RimWorld;
using TotalFog.Utils;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.Watchtowers;

[DefOf]
public static class WatchtowerDefOf
{
    public static ThingDef TotalFog_Watchtower;
    public static JobDef TotalFog_WatchtowerWatch;
    public static JobDef TotalFog_WatchtowerDescend;
    public static JobDef TotalFog_WatchtowerRetrieve;
}

/// <summary>One solid edifice and one native pawn. Elevation never changes map-space coordinates.</summary>
public sealed class Building_Watchtower : WatchtowerGraphic
{
    internal Pawn Occupant;
    internal float Elevation;
    internal bool Watching;
    internal bool SightDirty = true;
    private bool collapsing;
    private bool[] sightWindow;

    public IntVec3 Approach => Position + Rotation.FacingCell * 2;
    internal Thing OccupantBody => Occupant?.Dead == true ? Occupant.Corpse : Occupant;
    internal bool ProvidesSight =>
        Watching
        && Occupant?.Spawned == true
        && !Occupant.Downed
        && Occupant.Awake()
        && !Occupant.InMentalState;

    internal bool CanEnter(Pawn pawn, Thing retrieve = null) =>
        Spawned
        && pawn?.Spawned == true
        && pawn.RaceProps.Humanlike
        && !pawn.Downed
        && Approach.InBounds(Map)
        && Approach.Walkable(Map)
        && !this.IsForbidden(pawn)
        && pawn.CanReach(Approach, PathEndMode.OnCell, Danger.Deadly)
        && (Occupant == null || retrieve == OccupantBody && (Occupant.Downed || Occupant.Dead));

    internal void Claim(Pawn pawn)
    {
        Occupant = pawn;
        Watching = false;
        Map.GetComponent<MapComponent_Watchtowers>().Track(this);
        RefreshSight();
    }

    internal void RefreshSight()
    {
        SightDirty = true;
        Occupant?.TryGetComp<CompFog>()?.FieldOfViewWatcher?.UpdateFoV(true);
    }

    internal Vector3 OffsetFor(Thing thing)
    {
        if (thing != OccupantBody || Elevation <= 0)
            return Vector3.zero;
        var ground = Approach.ToVector3Shifted();
        float projection =
            thing is Corpse || Occupant?.Downed == true
                ? PlatformProjection - .18f
                : Core.TowerGeometry.PawnProjection;
        var top = Position.ToVector3Shifted() + new Vector3(0, 0, projection);
        return Vector3.Lerp(ground, top, Elevation) - thing.Position.ToVector3Shifted();
    }

    internal bool[] SightWindow(CellRect rect)
    {
        var component = Map.GetComponent<MapComponent_Watchtowers>();
        sightWindow ??= new bool[Map.cellIndices.NumGridCells];
        Core.TowerGeometry.CopySightWindow(
            component.ElevatedBlockers,
            sightWindow,
            Map.Size.x,
            rect.minX,
            rect.minZ,
            rect.maxX,
            rect.maxZ,
            Position.x,
            Position.z
        );
        return sightWindow;
    }

    internal void Release(Pawn pawn)
    {
        if (Occupant != pawn)
            return;
        Map?.GetComponent<MapComponent_Watchtowers>().Untrack(this);
        Occupant = null;
        Elevation = 0;
        Watching = false;
        SightDirty = true;
        pawn.TryGetComp<CompFog>()?.FieldOfViewWatcher?.UpdateFoV(true);
    }

    internal static void Move(Pawn pawn, IntVec3 cell)
    {
        pawn.pather.StopDead();
        pawn.Position = cell;
        pawn.Notify_Teleported(endCurrentJob: false);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref Occupant, "totalFogTowerOccupant");
        Scribe_Values.Look(ref Elevation, "totalFogTowerElevation");
        Scribe_Values.Look(ref Watching, "totalFogTowerWatching");
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        var component = map.GetComponent<MapComponent_Watchtowers>();
        component.Register(this);
        if (Occupant != null)
            component.Track(this);
    }

    public override string GetInspectString() =>
        base.GetInspectString()
        + (
            Occupant == null
                ? ""
                : "\n" + "TotalFog_TowerOccupied".Translate(Occupant.LabelShortCap)
        );

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
    {
        foreach (var option in base.GetFloatMenuOptions(pawn))
            yield return option;
        if (Occupant == pawn)
        {
            yield return new FloatMenuOption(
                "TotalFog_TowerDescend".Translate(),
                () =>
                    pawn.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerDescend, this)
                    )
            );
            yield break;
        }
        Thing body = OccupantBody;
        bool retrieve = body != null && (Occupant.Downed || Occupant.Dead);
        var label = retrieve ? "TotalFog_TowerRetrieve" : "TotalFog_TowerWatch";
        if (!CanEnter(pawn, retrieve ? body : null))
        {
            yield return new FloatMenuOption(
                label.Translate() + " (" + "TotalFog_TowerUnavailable".Translate() + ")",
                null
            );
            yield break;
        }
        yield return new FloatMenuOption(
            label.Translate(),
            () =>
            {
                var next = JobMaker.MakeJob(
                    retrieve
                        ? WatchtowerDefOf.TotalFog_WatchtowerRetrieve
                        : WatchtowerDefOf.TotalFog_WatchtowerWatch,
                    this
                );
                if (retrieve)
                {
                    next.targetC = body;
                    if (body is Pawn patient)
                        next.targetB = RestUtility.FindBedFor(
                            patient,
                            pawn,
                            checkSocialProperness: false
                        );
                }
                pawn.jobs.TryTakeOrderedJob(next);
            }
        );
    }

    // Normal deconstruction is refused while occupied. Harmful destruction ejects
    // the native pawn/corpse exactly once, before the edifice grid is removed.
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        if (mode == DestroyMode.Deconstruct && Occupant != null)
            return;
        if (!collapsing && Spawned && OccupantBody?.Spawned == true)
        {
            collapsing = true;
            var pawn = Occupant;
            var body = OccupantBody;
            var height = Elevation;
            var cell = FindGroundCell();
            Release(pawn);
            if (body is Pawn living)
            {
                Move(living, cell);
                living.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                if (mode is DestroyMode.KillFinalize or DestroyMode.KillFinalizeLeavingsOnly)
                    for (int i = 0; i < 3 && !living.Dead; i++)
                        living.TakeDamage(
                            new DamageInfo(
                                DamageDefOf.Blunt,
                                Rand.Range(20f, 35f) * height,
                                instigator: this
                            )
                        );
            }
            else
                body.Position = cell;
        }
        base.Destroy(mode);
    }

    private IntVec3 FindGroundCell()
    {
        if (Approach.InBounds(Map) && Approach.Walkable(Map))
            return Approach;
        foreach (var cell in GenRadial.RadialCellsAround(Position, 8, true))
            if (cell.InBounds(Map) && cell.Walkable(Map))
                return cell;
        // Once this edifice is gone its center is valid; never lose the native pawn.
        return Position;
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        var previous = Map;
        if (OccupantBody?.Spawned == true)
        {
            var pawn = Occupant;
            var body = OccupantBody;
            var cell = FindGroundCell();
            Release(pawn);
            if (body is Pawn living)
                Move(living, cell);
            else
                body.Position = cell;
        }
        previous.GetComponent<MapComponent_Watchtowers>().Unregister(this);
        base.DeSpawn(mode);
    }
}
