using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldRealFoW;

[StaticConstructorOnStartup]
public class Building_CameraConsole : Building
{
    private readonly Graphic[] workingGraphics = new Graphic[13];
    private CompBreakdownable breakdownableComp;

    private Graphic censorGraphic;

    private int deltaMonitor = 100;

    private int lastTick;

    private MapComponentSeenFog mapComp;

    private CompPowerTrader powerComp;


    public bool Manned => Find.TickManager.TicksGame < lastTick + deltaMonitor;

    public bool WorkingNow => FlickUtility.WantsToBeOn(this) && (powerComp == null || powerComp.PowerOn) &&
                              breakdownableComp is not { BrokenDown: true };

    public override string GetInspectString()
    {
        var inspect = new StringBuilder();
        inspect.Append(base.GetInspectString());
        if (mapComp != null)
        {
            inspect.AppendInNewLine("CameraCount".Translate() + ": " + mapComp.SurveillanceCameraCount());
        }

        return inspect.ToString();
    }

    public static bool NeedWatcher()
    {
        //Turret need the console to work so just keep it like this
        return true;
    }

    private void drawOverLay()
    {
        if (!Manned || Rotation != Rot4.North)
        {
            return;
        }

        var cameraCount = Mathf.Min(mapComp.SurveillanceCameraCount(), 12);
        workingGraphics[cameraCount] ??= GraphicDatabase.Get(
            def.graphicData.graphicClass,
            $"{def.graphicData.texPath}_FX{cameraCount}",
            ShaderDatabase.MoteGlow,
            def.graphicData.drawSize,
            DrawColor,
            DrawColorTwo
        );

        workingGraphics[cameraCount].Draw(DrawPos + new Vector3(0f, 1f, 0f), Rotation, this);
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        base.DrawAt(drawLoc, flip);
        drawOverLay();
    }

    public void Used(int delta)
    {
        lastTick = Find.TickManager.TicksGame;
        if (deltaMonitor < delta)
        {
            deltaMonitor = delta;
        }
    }


    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        powerComp = GetComp<CompPowerTrader>();
        breakdownableComp = GetComp<CompBreakdownable>();
        mapComp = map.GetMapComponentSeenFog();
        mapComp.RegisterCameraConsole(this);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        base.DeSpawn(mode);
        mapComp.DeregisterCameraConsole(this);
    }
}