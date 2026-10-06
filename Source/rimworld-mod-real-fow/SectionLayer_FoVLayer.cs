using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Unity.Collections;
using UnityEngine;
using Verse;

namespace RimWorldRealFoW;

public class SectionLayerFoVLayer : SectionLayer
{
    public static bool PrefEnableFade = true;

    public static int PrefFadeSpeedMult = 20;

    public static byte PrefFogAlpha = 86;

    private readonly FieldInfo fogGridFieldInfo = AccessTools.Field(typeof(FogGrid), "fogGrid");

    private readonly bool[] vertsNotShown = new bool[9];

    private readonly bool[] vertsSeen = new bool[9];

    private bool activeFogTransitions;

    private int[] alphaChangeTick = [];

    private short[] factionShownGrid;

    private Color32[] meshColors = [];

    private MapComponentSeenFog pawnFog;

    private byte[] targetAlphas = [];

    static SectionLayerFoVLayer()
    {
    }

    public SectionLayerFoVLayer(Section section) : base(section)
    {
        relevantChangeTypes = FoWDef.RealFogOfWar | MapMeshFlagDefOf.FogOfWar;
    }

    public override bool Visible
    {
        get
        {
            if (!DebugViewSettings.drawFog)
            {
                return false;
            }

            if (RfowSettings.OnlyOutsideColony && Map.IsPlayerHome)
            {
                return false;
            }

            return !ModLister.OdysseyInstalled || !RfowSettings.ClearFogDuringTargeting ||
                   !WorldComponent_GravshipController.CutsceneInProgress &&
                   Find.GravshipController?.LandingAreaConfirmationInProgress != true;
        }
    }

    private static void makeBaseGeometry(Section section, LayerSubMesh sm, AltitudeLayer altitudeLayer)
    {
        var cellRect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
        cellRect.ClipInsideMap(section.map);
        var y = altitudeLayer.AltitudeFor();
        sm.verts.Capacity = cellRect.Area * 9;
        for (var i = cellRect.minX; i <= cellRect.maxX; i++)
        {
            for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
            {
                sm.verts.Add(new Vector3(i, y, j));
                sm.verts.Add(new Vector3(i, y, j + 0.5f));
                sm.verts.Add(new Vector3(i, y, j + 1));
                sm.verts.Add(new Vector3(i + 0.5f, y, j + 1));
                sm.verts.Add(new Vector3(i + 1, y, j + 1));
                sm.verts.Add(new Vector3(i + 1, y, j + 0.5f));
                sm.verts.Add(new Vector3(i + 1, y, j));
                sm.verts.Add(new Vector3(i + 0.5f, y, j));
                sm.verts.Add(new Vector3(i + 0.5f, y, j + 0.5f));
            }
        }

        var num = cellRect.Area * 8 * 3;
        sm.tris.Capacity = num;
        var num2 = 0;
        while (sm.tris.Count < num)
        {
            sm.tris.Add(num2 + 7);
            sm.tris.Add(num2);
            sm.tris.Add(num2 + 1);
            sm.tris.Add(num2 + 1);
            sm.tris.Add(num2 + 2);
            sm.tris.Add(num2 + 3);
            sm.tris.Add(num2 + 3);
            sm.tris.Add(num2 + 4);
            sm.tris.Add(num2 + 5);
            sm.tris.Add(num2 + 5);
            sm.tris.Add(num2 + 6);
            sm.tris.Add(num2 + 7);
            sm.tris.Add(num2 + 7);
            sm.tris.Add(num2 + 1);
            sm.tris.Add(num2 + 8);
            sm.tris.Add(num2 + 1);
            sm.tris.Add(num2 + 3);
            sm.tris.Add(num2 + 8);
            sm.tris.Add(num2 + 3);
            sm.tris.Add(num2 + 5);
            sm.tris.Add(num2 + 8);
            sm.tris.Add(num2 + 5);
            sm.tris.Add(num2 + 7);
            sm.tris.Add(num2 + 8);
            num2 += 9;
        }

        sm.FinalizeMesh(MeshParts.Verts | MeshParts.Tris);
    }

    public override void Regenerate()
    {
        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        if (Map.IsPlayerHome && RfowSettings.OnlyOutsideColony)
        {
            return;
        }

        pawnFog ??= Map.GetMapComponentSeenFog();

        if (pawnFog is not { Initialized: true })
        {
            return;
        }

        var subMesh = GetSubMesh(MatBases.FogOfWar);
        bool noVertex;
        if (subMesh.mesh.vertexCount == 0)
        {
            noVertex = true;
            subMesh.mesh.MarkDynamic();
            makeBaseGeometry(section, subMesh, AltitudeLayer.FogOfWar);
            targetAlphas = new byte[subMesh.mesh.vertexCount];
            alphaChangeTick = new int[subMesh.mesh.vertexCount];
            meshColors = new Color32[subMesh.mesh.vertexCount];
        }
        else
        {
            noVertex = false;
        }

        var num = 0;
        var fogGrid = (NativeBitArray)fogGridFieldInfo.GetValue(Map.fogGrid);
        factionShownGrid ??= pawnFog.GetFactionShownCells(Faction.OfPlayer);

        var array = factionShownGrid;
        var playerVisibilityChangeTick = pawnFog.playerVisibilityChangeTick;
        var knownCells = pawnFog.knownCells;
        var x = Map.Size.x;
        var cellRect = section.CellRect;
        var num2 = Map.Size.z - 1;
        var num3 = x - 1;
        var changeColor = false;
        for (var i = cellRect.minX; i <= cellRect.maxX; i++)
        {
            for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
            {
                var num4 = (j * x) + i;
                var num5 = playerVisibilityChangeTick[num4];
                if (!fogGrid.IsSet(num4))
                {
                    if (array[num4] == 0)
                    {
                        for (var k = 0; k < 9; k++)
                        {
                            vertsNotShown[k] = true;
                            vertsSeen[k] = knownCells[num4];
                        }

                        if (knownCells[num4])
                        {
                            var num6 = ((j + 1) * x) + i;
                            var num7 = ((j - 1) * x) + i;
                            var num8 = (j * x) + i + 1;
                            var num9 = (j * x) + (i - 1);
                            var num10 = ((j - 1) * x) + (i - 1);
                            var num11 = ((j + 1) * x) + (i - 1);
                            var num12 = ((j + 1) * x) + i + 1;
                            var num13 = ((j - 1) * x) + i + 1;
                            if (j < num2 && !knownCells[num6])
                            {
                                vertsSeen[2] = false;
                                vertsSeen[3] = false;
                                vertsSeen[4] = false;
                            }

                            if (j > 0 && !knownCells[num7])
                            {
                                vertsSeen[6] = false;
                                vertsSeen[7] = false;
                                vertsSeen[0] = false;
                            }

                            if (i < num3 && !knownCells[num8])
                            {
                                vertsSeen[4] = false;
                                vertsSeen[5] = false;
                                vertsSeen[6] = false;
                            }

                            if (i > 0 && !knownCells[num9])
                            {
                                vertsSeen[0] = false;
                                vertsSeen[1] = false;
                                vertsSeen[2] = false;
                            }

                            if (j > 0 && i > 0 && !knownCells[num10])
                            {
                                vertsSeen[0] = false;
                            }

                            if (j < num2 && i > 0 && !knownCells[num11])
                            {
                                vertsSeen[2] = false;
                            }

                            if (j < num2 && i < num3 && !knownCells[num12])
                            {
                                vertsSeen[4] = false;
                            }

                            if (j > 0 && i < num3 && !knownCells[num13])
                            {
                                vertsSeen[6] = false;
                            }
                        }
                    }
                    else
                    {
                        for (var l = 0; l < 9; l++)
                        {
                            vertsNotShown[l] = false;
                            vertsSeen[l] = false;
                        }

                        var num6 = ((j + 1) * x) + i;
                        var num7 = ((j - 1) * x) + i;
                        var num8 = (j * x) + i + 1;
                        var num9 = (j * x) + (i - 1);
                        var num10 = ((j - 1) * x) + (i - 1);
                        var num11 = ((j + 1) * x) + (i - 1);
                        var num12 = ((j + 1) * x) + i + 1;
                        var num13 = ((j - 1) * x) + i + 1;
                        if (j < num2 && array[num6] == 0)
                        {
                            vertsNotShown[2] = true;
                            vertsSeen[2] = knownCells[num6];
                            vertsNotShown[3] = true;
                            vertsSeen[3] = knownCells[num6];
                            vertsNotShown[4] = true;
                            vertsSeen[4] = knownCells[num6];
                        }

                        if (j > 0 && array[num7] == 0)
                        {
                            vertsNotShown[6] = true;
                            vertsSeen[6] = knownCells[num7];
                            vertsNotShown[7] = true;
                            vertsSeen[7] = knownCells[num7];
                            vertsNotShown[0] = true;
                            vertsSeen[0] = knownCells[num7];
                        }

                        if (i < num3 && array[num8] == 0)
                        {
                            vertsNotShown[4] = true;
                            vertsSeen[4] = knownCells[num8];
                            vertsNotShown[5] = true;
                            vertsSeen[5] = knownCells[num8];
                            vertsNotShown[6] = true;
                            vertsSeen[6] = knownCells[num8];
                        }

                        if (i > 0 && array[num9] == 0)
                        {
                            vertsNotShown[0] = true;
                            vertsSeen[0] = knownCells[num9];
                            vertsNotShown[1] = true;
                            vertsSeen[1] = knownCells[num9];
                            vertsNotShown[2] = true;
                            vertsSeen[2] = knownCells[num9];
                        }

                        if (j > 0 && i > 0 && array[num10] == 0)
                        {
                            vertsNotShown[0] = true;
                            vertsSeen[0] = knownCells[num10];
                        }

                        if (j < num2 && i > 0 && array[num11] == 0)
                        {
                            vertsNotShown[2] = true;
                            vertsSeen[2] = knownCells[num11];
                        }

                        if (j < num2 && i < num3 && array[num12] == 0)
                        {
                            vertsNotShown[4] = true;
                            vertsSeen[4] = knownCells[num12];
                        }

                        if (j > 0 && i < num3 && array[num13] == 0)
                        {
                            vertsNotShown[6] = true;
                            vertsSeen[6] = knownCells[num13];
                        }
                    }
                }
                else
                {
                    for (var m = 0; m < 9; m++)
                    {
                        vertsNotShown[m] = true;
                        vertsSeen[m] = false;
                    }
                }

                for (var n = 0; n < 9; n++)
                {
                    byte b;
                    if (vertsNotShown[n])
                    {
                        b = vertsSeen[n] ? PrefFogAlpha : byte.MaxValue;

                        changeColor = true;
                    }
                    else
                    {
                        b = 0;
                    }

                    if (!PrefEnableFade || noVertex)
                    {
                        if (noVertex || meshColors[num].a != b)
                        {
                            meshColors[num] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, b);
                        }

                        if (PrefEnableFade)
                        {
                            activeFogTransitions = true;
                            targetAlphas[num] = b;
                            alphaChangeTick[num] = num5;
                        }
                    }
                    else
                    {
                        if (targetAlphas[num] != b)
                        {
                            activeFogTransitions = true;
                            targetAlphas[num] = b;
                            alphaChangeTick[num] = num5;
                        }
                    }

                    num++;
                }
            }
        }

        if (PrefEnableFade && !noVertex)
        {
            return;
        }

        if (changeColor)
        {
            subMesh.disabled = false;
            subMesh.mesh.colors32 = meshColors;
        }
        else
        {
            subMesh.disabled = true;
        }
    }

    public override void DrawLayer()
    {
        if (PrefEnableFade && Visible && activeFogTransitions && (!RfowSettings.OnlyOutsideColony || !Map.IsPlayerHome))
        {
            var ticksGame = Find.TickManager.TicksGame;
            var num = Math.Max((int)Find.TickManager.CurTimeSpeed, 1);
            var disableSubmesh = false;
            var setColor = false;
            var array = meshColors;
            for (var i = 0; i < targetAlphas.Length; i++)
            {
                var b = targetAlphas[i];
                var b2 = array[i].a;
                if (b2 > b)
                {
                    disableSubmesh = true;
                    if (ticksGame != alphaChangeTick[i])
                    {
                        b2 = (byte)Math.Max(b, b2 - (PrefFadeSpeedMult / num * (ticksGame - alphaChangeTick[i])));
                        array[i] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, b2);
                        alphaChangeTick[i] = ticksGame;
                    }
                }
                else
                {
                    if (b2 < b)
                    {
                        disableSubmesh = true;
                        if (ticksGame != alphaChangeTick[i])
                        {
                            b2 = (byte)Math.Min(b,
                                b2 + (PrefFadeSpeedMult / num * (ticksGame - alphaChangeTick[i])));
                            array[i] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, b2);
                            alphaChangeTick[i] = ticksGame;
                        }
                    }
                }

                if (b2 > 0)
                {
                    setColor = true;
                }
            }

            if (disableSubmesh)
            {
                var subMesh = GetSubMesh(MatBases.FogOfWar);
                if (setColor)
                {
                    subMesh.disabled = false;
                    subMesh.mesh.colors32 = array;
                }
                else
                {
                    subMesh.disabled = true;
                }
            }
            else
            {
                activeFogTransitions = false;
            }
        }

        base.DrawLayer();
    }
}