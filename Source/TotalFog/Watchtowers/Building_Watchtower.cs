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
    internal Pawn Rescuer;
    internal float Elevation;
    internal float RescuerElevation;
    internal bool Watching;
    internal bool SightDirty = true;
    private bool collapsing;
    private bool[] sightWindow;

    public IntVec3 Approach => Position + Rotation.FacingCell * 2;
    internal Thing OccupantBody => Occupant?.Dead == true ? Occupant.Corpse : Occupant;
    private Thing RescuerBody => Rescuer?.Dead == true ? Rescuer.Corpse : Rescuer;

    internal bool HasActor(Pawn pawn) => Occupant == pawn || Rescuer == pawn;

    internal float ElevationFor(Thing thing) =>
        thing == OccupantBody ? Elevation
        : thing == RescuerBody ? RescuerElevation
        : 0;

    internal void SetElevation(Pawn pawn, float elevation)
    {
        if (Occupant == pawn)
            Elevation = elevation;
        else if (Rescuer == pawn)
            RescuerElevation = elevation;
    }

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
        && (Rescuer == null || Rescuer == pawn)
        && Approach.InBounds(Map)
        && Approach.Walkable(Map)
        && !this.IsForbidden(pawn)
        && pawn.CanReach(Approach, PathEndMode.OnCell, Danger.Deadly)
        && (Occupant == null || retrieve == OccupantBody && (Occupant.Downed || Occupant.Dead));

    internal void Claim(Pawn pawn)
    {
        if (Rescuer == pawn)
        {
            Rescuer = null;
            RescuerElevation = 0;
        }
        Occupant = pawn;
        Watching = false;
        Map.GetComponent<MapComponent_Watchtowers>().Track(this);
        RefreshSight();
    }

    internal void ClaimRescuer(Pawn pawn)
    {
        Rescuer = pawn;
        RescuerElevation = 0;
        Map.GetComponent<MapComponent_Watchtowers>().Track(this);
    }

    internal void RefreshSight()
    {
        SightDirty = true;
        Occupant?.TryGetComp<CompFog>()?.FieldOfViewWatcher?.UpdateFoV(true);
        Map?.GetVisibility().RaisedTargetChanged(Position);
    }

    internal Vector3 OffsetFor(Thing thing)
    {
        float elevation = ElevationFor(thing);
        if (elevation <= 0)
            return Vector3.zero;
        var ground = Approach.ToVector3Shifted();
        float projection =
            thing is Corpse || thing is Pawn { Downed: true }
                ? PlatformProjection - .18f
                : Core.TowerGeometry.PawnProjection;
        var top = Position.ToVector3Shifted() + new Vector3(0, 0, projection);
        return Vector3.Lerp(ground, top, elevation) - thing.Position.ToVector3Shifted();
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
        if (Rescuer == pawn)
        {
            Map?.GetComponent<MapComponent_Watchtowers>().Untrack(pawn);
            Rescuer = null;
            RescuerElevation = 0;
            return;
        }
        if (Occupant != pawn)
            return;
        Map?.GetComponent<MapComponent_Watchtowers>().Untrack(pawn);
        Occupant = null;
        Elevation = 0;
        Watching = false;
        SightDirty = true;
        pawn.TryGetComp<CompFog>()?.FieldOfViewWatcher?.UpdateFoV(true);
        Map?.GetVisibility().RaisedTargetChanged(Position);
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
        // Native death marks the pawn destroyed while its corpse still owns
        // and deep-saves it. Preserve that reference for a body on the deck.
        Scribe_References.Look(ref Occupant, "totalFogTowerOccupant", saveDestroyedThings: true);
        Scribe_References.Look(ref Rescuer, "totalFogTowerRescuer", saveDestroyedThings: true);
        Scribe_Values.Look(ref Elevation, "totalFogTowerElevation");
        Scribe_Values.Look(ref Watching, "totalFogTowerWatching");
        Scribe_Values.Look(ref RescuerElevation, "totalFogTowerRescuerElevation");
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        var component = map.GetComponent<MapComponent_Watchtowers>();
        component.Register(this);
        if (Occupant != null || Rescuer != null)
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
        if (HasActor(pawn))
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
        if (mode == DestroyMode.Deconstruct && (Occupant != null || Rescuer != null))
            return;
        if (!collapsing && Spawned)
        {
            collapsing = true;
            bool injury = mode is DestroyMode.KillFinalize or DestroyMode.KillFinalizeLeavingsOnly;
            GroundActor(Occupant, injury);
            GroundActor(Rescuer, injury);
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

    private void GroundActor(Pawn pawn, bool injury)
    {
        if (pawn == null)
            return;
        var body = pawn.Dead ? pawn.Corpse : (Thing)pawn;
        float height = ElevationFor(body);
        var cell = FindGroundCell();
        DropCarriedActor(pawn, injury);
        Release(pawn);
        if (body?.Spawned != true)
            return;
        if (body is not Pawn living)
        {
            body.Position = cell;
            return;
        }
        Move(living, cell);
        living.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
        if (injury)
            InjureFall(living, height);
    }

    internal void DropCarriedActor(Pawn carrier, bool injury)
    {
        if (carrier.carryTracker?.CarriedThing is not (Pawn or Corpse))
            return;
        float height = ElevationFor(carrier);
        // Drop through the native carry tracker before job/death cleanup can
        // place the patient at the solid structure's ground cell.
        if (
            !carrier.carryTracker.TryDropCarriedThing(
                FindGroundCell(),
                ThingPlaceMode.Direct,
                out var body
            )
        )
            return;
        if (injury && body is Pawn patient)
            InjureFall(patient, height);
    }

    private void InjureFall(Pawn pawn, float height)
    {
        for (int i = 0; i < 3 && !pawn.Dead; i++)
            pawn.TakeDamage(
                new DamageInfo(DamageDefOf.Blunt, Rand.Range(20f, 35f) * height, instigator: this)
            );
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        var previous = Map;
        GroundActor(Occupant, false);
        GroundActor(Rescuer, false);
        previous.GetComponent<MapComponent_Watchtowers>().Unregister(this);
        base.DeSpawn(mode);
    }
}
