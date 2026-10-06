// Modified by Andreas Pardeike for Total Fog, 2026-10-04.
using RimWorld;
using Verse;

namespace TotalFog;

public class CompPresentationState : FogSubcomponent
{
    private Map map;

    private MapVisibility mapComp;

    public bool Hidden { get; private set; }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        // The engine exclusively owns drawable registration. Each spawn starts
        // a new presentation lifecycle, reconciled by the visibility adapter.
        Hidden = false;
    }

    public void Hide()
    {
        if (Hidden)
        {
            return;
        }

        Hidden = true;

        // The engine owns tooltip registration. The common tooltip gate checks
        // current faction sight before any thing-specific tooltip is evaluated.

        var selector = Find.Selector;
        if (selector.IsSelected(parent))
        {
            selector.Deselect(parent);
        }

        updateMeshes();
    }

    public void Show()
    {
        if (!Hidden)
        {
            return;
        }

        Hidden = false;

        updateMeshes();
    }

    private void updateMeshes()
    {
        // Dynamic culling owns realtime objects. Their visibility transitions
        // cannot change section geometry, terrain, roofs or power-grid meshes.
        if (parent.def.drawerType is DrawerType.RealtimeOnly or DrawerType.None)
            return;
        if (map != parent.Map)
        {
            map = parent.Map;
            mapComp = map.GetVisibility();
        }

        if (mapComp is not { Initialized: true })
        {
            return;
        }

        var rect = parent.OccupiedRect();
        rect.ClipInsideMap(map);
        foreach (var intVec in rect)
        {
            map.mapDrawer.MapMeshDirty(
                intVec,
                MapMeshFlagDefOf.Things
                    | MapMeshFlagDefOf.Buildings
                    | MapMeshFlagDefOf.GroundGlow
                    | MapMeshFlagDefOf.Terrain
                    | MapMeshFlagDefOf.Roofs
                    | MapMeshFlagDefOf.Snow
                    | MapMeshFlagDefOf.Pollution
                    | MapMeshFlagDefOf.Zone
                    | MapMeshFlagDefOf.PowerGrid
                    | MapMeshFlagDefOf.BuildingsDamage
                    | MapMeshFlagDefOf.Gas
            );
        }
    }
}
