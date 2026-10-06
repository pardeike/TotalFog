// Small engine boundary for independently testing the production component.
// Live BridgeTools tests cover real engine registration, rendering, and save/load.
using System.Collections.Generic;

namespace Verse
{
    public class TimeSlower { public int Signals; public void SignalForceNormalSpeedShort() => Signals++; }
    public class MapComponent { public Map map; }
    public class ModSettings { }
    public interface IExposable { }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, ResolvingCrossRefs, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public enum DrawerType { None, MapMeshOnly, RealtimeOnly, MapMeshAndRealTime }
    public enum ThingCategory { Item, Pawn, Building, Projectile, Mote }
    public class ThingDef { public DrawerType drawerType = DrawerType.RealtimeOnly; public bool hasTooltip; public ThingCategory category; public IntVec2 size = new(1, 1); }
    public class Faction { public static readonly Faction OfPlayer = new(); }
    public class Thing
    {
        public ThingDef def = new();
        public Map Map = new();
        public Map MapHeld => Map;
        public IntVec3 PositionHeld;
        public IntVec3 Position => PositionHeld;
        public IntVec3 InteractionCell => PositionHeld;
        public int OverlayCalls;
        public void DrawGUIOverlay() => OverlayCalls++;
        public Faction Faction;
        public bool Spawned = true;
        public bool Destroyed;
        public object Component;
        public int SizeX { get => def.size.x; set => def.size = new(value, def.size.z); }
        public int SizeZ { get => def.size.z; set => def.size = new(def.size.x, value); }
        public Rot4 Rotation = new(0);
        public RimWorld.CompSelectProxy Proxy;
        public int ComponentQueries;
        public T TryGetComp<T>() where T : class
        {
            ComponentQueries++;
            return Component as T ?? Proxy as T;
        }
        public CellRect OccupiedRect()
        {
            var cells = new CellRect();
            int width = Rotation.IsHorizontal ? SizeZ : SizeX, height = Rotation.IsHorizontal ? SizeX : SizeZ;
            for (int x = 0; x < width; x++) for (int z = 0; z < height; z++)
                cells.Add(new IntVec3(PositionHeld.x + x, PositionHeld.z + z));
            return cells;
        }
    }
    public class ThingWithComps : Thing { }
    public class Building : ThingWithComps { }
    public class RaceProperties { public bool Humanlike = true; }
    public class Pawn : ThingWithComps { public bool Dead; public bool IsPrisonerOfColony; public RaceProperties RaceProps = new(); }
    public class LetterDef { }
    public class LookTargets { public List<TargetInfo> targets = new(); }
    public struct TargetInfo
    {
        public bool IsValid;
        public Thing Thing;
        public Map Map;
        public IntVec3 Cell;
        public bool HasThing => Thing != null;
    }
    public struct DrawPosition { public IntVec3 Cell; public IntVec3 ToIntVec3() => Cell; }
    public class FleckManager { public Map parent; }
    public struct FleckStatic { public UnityEngine.Vector3 DrawPos; }
    public class Map
    {
        public IntVec3 Size = new(2, 2);
        public bool Bypass;
        public bool IsPlayerHome;
        public TotalFog.MapVisibility Fog;
        public List<MapComponent> components = new();
        public int ComponentLookups;
        public Map() { Fog = new(this); components.Add(Fog); }
        public T GetComponent<T>() where T : MapComponent
        {
            ComponentLookups++;
            foreach (var component in components) if (component is T match) return match;
            return null;
        }
        public FogGrid fogGrid = new();
        public CellIndices cellIndices = new();
        public DynamicDrawManager dynamicDrawManager = new();
        public Tooltips tooltipGiverList = new();
        public MapDrawer mapDrawer = new();
        public List<Thing> Things = new();
    }
    public class FogGrid { public bool Fogged; public bool IsFogged(IntVec3 cell) => Fogged; }
    public class CellIndices { public int CellToIndex(IntVec3 cell) => cell.z * 2 + cell.x; }
    public class DynamicDrawManager
    {
        public struct ThingCullDetails { public bool shouldDraw, shouldDrawShadow; public IntVec3 cell; }
        public List<ThingWithComps> things = new();
        public void RegisterDrawable(ThingWithComps thing) => things.Add(thing);
        public void DeRegisterDrawable(ThingWithComps thing) => things.Remove(thing);
    }
    public class Tooltips { public void Notify_ThingSpawned(ThingWithComps thing) { } public void Notify_ThingDespawned(ThingWithComps thing) { } }
    public class MapDrawer
    {
        public int DirtyCalls;
        public void MapMeshDirty(IntVec3 cell, int flags) => DirtyCalls++;
        public void RegenerateEverythingNow() => DirtyCalls++;
    }
    public record struct IntVec3(int x, int z)
    {
        public static readonly IntVec3 Invalid = new(-1, -1);
        public bool InBounds(Map map) => (uint)x < 2 && (uint)z < 2;
        public List<Thing> GetThingList(Map map) => map.Things;
        public static IntVec3 operator +(IntVec3 a, IntVec3 b) => new(a.x + b.x, a.z + b.z);
        public static IntVec3 operator -(IntVec3 a, IntVec3 b) => new(a.x - b.x, a.z - b.z);
        public bool AdjacentToCardinal(IntVec3 other) => System.Math.Abs(x - other.x) + System.Math.Abs(z - other.z) == 1;
        public bool InHorDistOf(IntVec3 other, float range)
            => (x - other.x) * (x - other.x) + (z - other.z) * (z - other.z) <= range * range;
    }
    public class VerbProperties { public bool requireLineOfSight = true; }
    public class Verb { public Thing caster; public VerbProperties verbProps = new(); public LocalTargetInfo CurrentTarget; }
    public readonly struct LocalTargetInfo
    {
        public readonly Thing Thing;
        private readonly IntVec3 cell;
        public LocalTargetInfo(Thing thing) { Thing = thing; cell = IntVec3.Invalid; }
        public LocalTargetInfo(IntVec3 targetCell) { Thing = null; cell = targetCell; }
        public bool HasThing => Thing != null;
        public IntVec3 Cell => Thing?.Position ?? cell;
    }
    public struct ShootLine { }
    public static class GenTypes { public static IEnumerable<System.Type> AllTypes = System.Array.Empty<System.Type>(); }
    public record struct IntVec2(int x, int z);
    public record struct Rot4(int Value)
    {
        public static readonly Rot4 Invalid = new(-1);
        public bool IsHorizontal => Value == 1 || Value == 3;
    }
    public class CellRect : List<IntVec3>
    {
        public int Area => Count;
        public static CellRect SingleCell(IntVec3 cell) => new() { cell };
        public CellRect MovedBy(int x, int z)
        {
            var moved = new CellRect();
            foreach (var cell in this) moved.Add(new IntVec3(cell.x + x, cell.z + z));
            return moved;
        }
        public void ClipInsideMap(Map map) => RemoveAll(c => !c.InBounds(map));
    }
    public class Selector
    {
        public HashSet<Thing> Selected = new();
        public bool IsSelected(Thing thing) => Selected.Contains(thing);
        public void Deselect(Thing thing) => Selected.Remove(thing);
    }
    public class TickManager { public int TicksGame; }
    public static class Find
    {
        public static Selector Selector = new(); public static TickManager TickManager = new(); public static Map CurrentMap;
        public static RimWorld.Planet.World World;
        public static WorldComponent_GravshipController GravshipController => World?.GetComponent<WorldComponent_GravshipController>();
    }
    public static class ModsConfig
    {
        public static bool OdysseyActive;
        public static readonly HashSet<string> Active = new(System.StringComparer.OrdinalIgnoreCase);
        public static bool IsActive(string id) => Active.Contains(id);
    }
    public static class Log { public static void Warning(string message) { } }
    public class WorldComponent_GravshipController
    {
        public readonly RimWorld.Planet.World World;
        public WorldComponent_GravshipController(RimWorld.Planet.World world) => World = world;
        public static bool CutsceneInProgress;
        public bool LandingAreaConfirmationInProgress;
        public void LandingEnded() { }
    }
    public static class UI { public static IntVec3 Cell; public static IntVec3 MouseCell() => Cell; }
    public enum ProgramState { Playing, MapInitializing }
    public static class Current { public static ProgramState ProgramState; }
    public static class Scribe_Values { public static void Look(ref bool value, string key) { } }
    public static class ThingMaker { public static Thing MakeThing(ThingDef def) => new TotalFog.Mote_HearingCue(); }
    public static class GenSpawn { public static void Spawn(Thing thing, IntVec3 position, Map map) { } }
}
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Verse.IntVec3 ToIntVec3() => new((int)System.Math.Floor(x), (int)System.Math.Floor(z));
    }
    public static class Mathf { public static int RoundToInt(float value) => (int)System.Math.Round(value); }
}
namespace Unity.Collections
{
    public readonly struct NativeArray<T>
    {
        private readonly T[] values;
        public NativeArray(T[] values) => this.values = values;
        public int Length => values.Length;
        public T this[int index] { get => values[index]; set => values[index] = value; }
    }
}
namespace RimWorld
{
    public class Building_Turret : Verse.Building { }
    public class IncidentDef { }
    public static class IncidentDefOf { public static readonly IncidentDef ManhunterPack = new(); }
    public class IncidentParms { public bool silent; public bool sendLetter = true; public float points; }
    public class IncidentWorker { public IncidentDef def; }
    public class IncidentWorker_RaidEnemy : IncidentWorker { }
    public static class LetterDefOf
    {
        public static readonly Verse.LetterDef NegativeEvent = new(), NeutralEvent = new(),
            PositiveEvent = new(), ThreatBig = new(), ThreatSmall = new();
    }
    public class PawnFlyer : Verse.Thing
    {
        public Verse.Pawn FlyingPawn;
        public Verse.DrawPosition RenderedPosition;
        public int DrawPosReads;
        public bool ThrowOnDrawPos;
        public Verse.DrawPosition DrawPos
        {
            get
            {
                DrawPosReads++;
                if (ThrowOnDrawPos) throw new System.InvalidOperationException("DrawPos mutates the native grid");
                return RenderedPosition;
            }
        }
    }
    public class CompSelectProxy { public Verse.Thing thingToSelect; }
    public class CompMannable { public Verse.Pawn ManningPawn; }
    public static class BeautyUtility { public static List<Verse.IntVec3> beautyRelevantCells = new(); }
    public static class MapMeshFlagDefOf
    {
        public const int Things = 1, Buildings = 2, GroundGlow = 4, Terrain = 8, Roofs = 16,
            Snow = 32, Pollution = 64, Zone = 128, PowerGrid = 256, BuildingsDamage = 512, Gas = 1024;
    }
}
namespace TotalFog
{
    public class CompFog
    {
        public CompVisibility HideFromPlayer;
        public CompPresentationState Hiddenable;
        public CompCellRegistration ComponentsPositionTracker;
        public CompSightSource FieldOfViewWatcher;
    }
    public class CompSightSource
    {
        public float Range;
        public Verse.IntVec3 LastSource;
        public int Queries;
        public float CalcPawnSightRange(Verse.IntVec3 cell, bool forTargeting, bool shouldMove)
        { LastSource = cell; Queries++; return Range; }
    }
    public class CompSightModifier { }
    public class MapVisibility : Verse.MapComponent
    {
        public readonly Verse.Map Owner;
        public MapVisibility(Verse.Map owner = null) { Owner = owner; map = owner; }
        public bool Initialized; public bool[] knownCells = new bool[4], InSight = new bool[4];
        public bool[] viewBlockerCells = new bool[4];
        public int VisibilityQueries;
        public readonly Dictionary<Verse.Faction, bool[]> FactionSight = new();
        public bool IsShown(Verse.Faction faction, Verse.IntVec3 cell)
        {
            VisibilityQueries++;
            return (faction != null && FactionSight.TryGetValue(faction, out var sight) ? sight : InSight)[cell.z * 2 + cell.x];
        }
        public readonly Dictionary<(int, int), HashSet<CompVisibility>> Registered = new();
        public int RegistrationWrites, DeregistrationWrites;
        public void RegisterCompHideFromPlayerPosition(CompVisibility comp, int x, int z)
        {
            RegistrationWrites++;
            if (!Registered.TryGetValue((x, z), out var list)) Registered.Add((x, z), list = new());
            list.Add(comp);
        }
        public void DeregisterCompHideFromPlayerPosition(CompVisibility comp, int x, int z)
        {
            DeregistrationWrites++;
            if (Registered.TryGetValue((x, z), out var list) && list.Remove(comp) && list.Count == 0) Registered.Remove((x, z));
        }
        public void RegisterCompAffectVisionPosition(CompSightModifier comp, int x, int z) { }
        public void DeregisterCompAffectVisionPosition(CompSightModifier comp, int x, int z) { }
    }
    public class FogSettings : Verse.ModSettings
    {
        public static bool DoAudioCheck = true;
        public static bool MuteHiddenSounds;
        public static int AudioSourceRange = 30;
        public static bool OnlyOutsideColony;
        public static bool ClearFogDuringTargeting = true;
        public static bool AISmart;
        public static bool SilentRaids;
        public static bool HideEventNegative, HideEventNeutral, HideEventPositive, HideThreatBig, HideThreatSmall;
    }
    public class Mote_HearingCue : Verse.Thing { public void Initialize(UnityEngine.Vector3 position, float size, float velocity) { } }
    public static class FogDefOf { public static readonly Verse.ThingDef Mote_SoundWave = new(); }
    public static class SoundAudibility
    {
        public static float Factor = 1;
        public static float GetAudibilityFactor(Verse.Sound.SoundTarget maker, int range) => Factor;
    }
}
namespace RimWorld.Planet
{
    public class World
    {
        public readonly List<object> components = new();
        public int ComponentLookups;
        public T GetComponent<T>() where T : class
        {
            ComponentLookups++;
            foreach (var component in components) if (component is T match) return match;
            return null;
        }
    }
}
namespace HarmonyLib
{
    public class Harmony
    {
        public static readonly List<System.Reflection.MethodInfo> Patched = new();
        public void Patch(System.Reflection.MethodInfo original, HarmonyMethod postfix) => Patched.Add(original);
    }
    public class HarmonyMethod { public int priority; public HarmonyMethod(System.Type type, string name) { } }
    public static class Priority { public const int Last = 0; }
    public static class AccessTools
    {
        public static readonly Dictionary<string, System.Type> Types = new();
        public static System.Type TypeByName(string name) => Types.TryGetValue(name, out var type) ? type : null;
        public static System.Reflection.MethodInfo Method(System.Type type, string name) => type.GetMethod(name);
        public static System.Reflection.MethodInfo DeclaredMethod(System.Type type, string name, System.Type[] parameters)
            => type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly,
                null, parameters, null);
    }
}
namespace Verse.Sound
{
    public struct SoundTarget { }
    public struct SoundInfo { public float volumeFactor; public SoundTarget Maker; }
    public class SampleSustainer { public SoundInfo Info; }
}

namespace TotalFog
{
    public class DeferredNotifications : Verse.MapComponent { }
    public class DeferredNotification : Verse.IExposable { }
    public class Building_VisionConsole : Verse.Building { }
    public class Building_VisionCamera : Verse.Building { }
    public class JobDriver_MonitorVision : Verse.AI.JobDriver { }
}
namespace Verse.AI { public class JobDriver { } }
