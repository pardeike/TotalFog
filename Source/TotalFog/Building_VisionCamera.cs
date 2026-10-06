using System.Text;
using RimWorld;
using Verse;

namespace TotalFog;

[StaticConstructorOnStartup]
public class Building_VisionCamera : Building
{
    private MapVisibility mapComp;

    private CompPowerTrader powerComp;

    public bool IsPowered()
    {
        return powerComp.PowerOn;
    }

    public override string GetInspectString()
    {
        var inspect = new StringBuilder();
        inspect.Append(base.GetInspectString());
        inspect.AppendInNewLine(mapComp.workingCameraConsole ? "Revealing".Translate() : "NoCameraConsole".Translate());

        return inspect.ToString();
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        powerComp = GetComp<CompPowerTrader>();
        mapComp = map.GetVisibility();
        mapComp.RegisterSurveillanceCamera(this);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        base.DeSpawn(mode);
        mapComp.DeregisterSurveillanceCamera(this);
    }
}