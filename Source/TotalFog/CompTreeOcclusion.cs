// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using RimWorld;
using TotalFog.Utils;
using Verse;
namespace TotalFog;
public class CompTreeOcclusion : FogSubcomponent
{
    public static readonly CompProperties_TreeOcclusion CompDef = new();
    private Plant plant;
    private Map map;
    private IntVec3 position;
    private bool blocking;
    private int nextCheck;
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        plant = parent as Plant; map = parent.Map; position = parent.Position; blocking = false; Refresh();
    }
    public override void ReceiveCompSignal(string signal) => Refresh();
    public override void CompTick()
    {
        int tick = Find.TickManager.TicksGame;
        if (tick < nextCheck) return;
        nextCheck = tick + 60; Refresh();
    }
    private void Refresh()
    {
        if (plant?.Spawned == true && (map != parent.Map || position != parent.Position))
        {
            Remove(); map = parent.Map; position = parent.Position;
        }
        bool value = plant?.Spawned == true && FogThingUtility.PlantBlocksView(plant) && FogSettings.TreesBlockSight && plant.Growth >= .5f;
        if (value == blocking) return;
        blocking = value;
        var fog = map.GetVisibility();
        if (value) fog.RegisterTreeViewBlocker(this, position.x, position.z);
        else fog.DeregisterTreeViewBlocker(this, position.x, position.z);
        fog.SetTreeBlocker(position, value);
    }
    public override void PostDeSpawn(Map previousMap)
        => Remove();
    private void Remove()
    {
        if (!blocking) return;
        var fog = map.GetVisibility();
        fog.DeregisterTreeViewBlocker(this, position.x, position.z); fog.SetTreeBlocker(position, false);
        blocking = false;
    }
}
