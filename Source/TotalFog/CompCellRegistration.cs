// Rewritten for Total Fog, 2026-10-04.
using Verse;
namespace TotalFog;
public class CompCellRegistration : FogSubcomponent
{
    private MapVisibility fog;
    private Map map;
    private CellRect occupied;
    private IntVec3 position = IntVec3.Invalid;
    private Rot4 rotation = Rot4.Invalid;
    private IntVec2 size;
    private CompVisibility hidden;
    private CompSightModifier affecter;
    private bool registered;
    internal MapVisibility CurrentVisibility => registered && map == parent.Map ? fog : null;
    private bool SameFootprint() => position == parent.Position && size == parent.def.size &&
        (size.x == 1 && size.z == 1 || rotation == parent.Rotation);
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        hidden = mainComponent.HideFromPlayer; affecter = parent.TryGetComp<CompSightModifier>();
        Remove(); position = IntVec3.Invalid; updatePosition();
    }
    public override void ReceiveCompSignal(string signal) => updatePosition();
    public override void CompTick()
    {
        // Every tick already checks the inputs that can move the footprint.
        // A periodic call with the same inputs only repeats this same check.
        if (!registered || map != parent.Map || !SameFootprint()) updatePosition();
    }
    public void updatePosition()
    {
        if (!parent.Spawned || parent.Map == null) return;
        if (registered && map == parent.Map && SameFootprint()) return;
        Remove(); map = parent.Map; fog = map.GetVisibility();
        position = parent.Position; rotation = parent.Rotation; size = parent.def.size;
        occupied = parent.OccupiedRect(); occupied.ClipInsideMap(parent.Map);
        foreach (var cell in occupied)
        {
            if (hidden != null) fog.RegisterCompHideFromPlayerPosition(hidden, cell.x, cell.z);
            if (affecter != null) fog.RegisterCompAffectVisionPosition(affecter, cell.x, cell.z);
        }
        registered = true;
        hidden?.UpdateVisibility(true);
    }
    private void Remove()
    {
        if (!registered || fog == null) return;
        foreach (var cell in occupied)
        {
            if (hidden != null) fog.DeregisterCompHideFromPlayerPosition(hidden, cell.x, cell.z);
            if (affecter != null) fog.DeregisterCompAffectVisionPosition(affecter, cell.x, cell.z);
        }
        registered = false;
    }
    public override void PostDeSpawn(Map previousMap) { Remove(); fog = null; map = null; position = IntVec3.Invalid; }
}
