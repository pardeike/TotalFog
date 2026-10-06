using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW;

public class MapComponentSeenFog : MapComponent
{
    //New addition

    //Camera console
    private readonly List<Building_CameraConsole> cameraConsoles = [];
    public readonly List<CompAffectVision>[] compAffectVisionGrid;
    private readonly List<CompHideFromPlayer>[] compHideFromPlayerGrid;
    private readonly byte[] compHideFromPlayerGridCount;

    private readonly short[][] factionsFullShow;
    public readonly List<CompFieldOfViewWatcher> fowWatchers;
    private readonly IntVec3[] idxToCellCache;
    private readonly int mapCellLength;
    private readonly MapDrawer mapDrawer;
    public readonly int mapSizeX;
    private readonly int mapSizeZ;

    private readonly Designation[] mineDesignationGrid;
    public readonly int[] playerVisibilityChangeTick;

    //Camera building
    private readonly List<Building_SurveillanceCamera> surveillanceCameras = [];

    // Same as viewBlockerCells, but trees only
    public readonly bool[] treeBlockerCells;
    private readonly List<CompTreeViewBlocker>[] treeViewBlockerGrid;
    public readonly bool[] viewBlockerCells;
    private int currentGameTick;

    private short[][] factionsShownCells;

    public bool[] knownCells;

    //public bool[] knownFilthCells; // entry containing only cells with filth.
    private int maxFactionLoadId;
    private Section[] sections;
    private int sectionsSizeX;

    private int sectionsSizeY;

    public MapComponentSeenFog(Map map) : base(map)
    {
        mapCellLength = map.cellIndices.NumGridCells;
        mapSizeX = map.Size.x;
        mapSizeZ = map.Size.z;


        mapDrawer = map.mapDrawer;

        fowWatchers = new List<CompFieldOfViewWatcher>(1000);

        maxFactionLoadId = 0;

        foreach (var faction in Find.World.factionManager.AllFactionsListForReading)
        {
            maxFactionLoadId = Math.Max(maxFactionLoadId, faction.loadID);
        }

        factionsShownCells = new short[maxFactionLoadId + 1][];
        factionsFullShow = new short[maxFactionLoadId + 1][];

        knownCells = new bool[mapCellLength];
        viewBlockerCells = new bool[mapCellLength];
        treeBlockerCells = new bool[mapCellLength];
        playerVisibilityChangeTick = new int[mapCellLength];
        mineDesignationGrid = new Designation[mapCellLength];
        idxToCellCache = new IntVec3[mapCellLength];
        compHideFromPlayerGrid = new List<CompHideFromPlayer>[mapCellLength];
        compHideFromPlayerGridCount = new byte[mapCellLength];
        compAffectVisionGrid = new List<CompAffectVision>[mapCellLength];
        treeViewBlockerGrid = new List<CompTreeViewBlocker>[mapCellLength];
        for (var i = 0; i < mapCellLength; i++)
        {
            idxToCellCache[i] = CellIndicesUtility.IndexToCell(i, mapSizeX);

            compHideFromPlayerGrid[i] = new List<CompHideFromPlayer>(16);
            compHideFromPlayerGridCount[i] = 0;
            compAffectVisionGrid[i] = new List<CompAffectVision>(16);
            treeViewBlockerGrid[i] = new List<CompTreeViewBlocker>(4);

            playerVisibilityChangeTick[i] = 0;
        }

        foreach (var filth in map.listerThings.ThingsInGroup(ThingRequestGroup.Filth))
        {
            _ = map.cellIndices.CellToIndex(filth.Position);
        }
    }

    public bool Initialized { get; private set; }


    public bool workingCameraConsole
    {
        get
        {
            return
                !RfowSettings.NeedWatcher ||
                cameraConsoles.Any(c => c.WorkingNow && c.Manned);
        }
    }

    public override void MapComponentTick()
    {
        currentGameTick = Find.TickManager.TicksGame;
        if (Initialized)
        {
            return;
        }

        Initialized = true;
        init();
    }

    public void RegisterCameraConsole(Building_CameraConsole console)
    {
        cameraConsoles.Add(console);
    }

    public void DeregisterCameraConsole(Building_CameraConsole console)
    {
        cameraConsoles.Remove(console);
    }

    public void RegisterSurveillanceCamera(Building_SurveillanceCamera camera)
    {
        surveillanceCameras.Add(camera);
    }

    public void DeregisterSurveillanceCamera(Building_SurveillanceCamera camera)
    {
        surveillanceCameras.Remove(camera);
    }

    public int SurveillanceCameraCount()
    {
        return surveillanceCameras.Count(c => c.IsPowered());
    }

    private short[] getFactionFullShow(Faction faction)
    {
        if (factionsFullShow[faction.loadID] != null)
        {
            return factionsFullShow[faction.loadID];
        }

        factionsFullShow[faction.loadID] = new short[mapCellLength];
        for (var i = 0; i < factionsFullShow[faction.loadID].Length; i++)
        {
            factionsFullShow[faction.loadID][i] = 1;
        }

        return factionsFullShow[faction.loadID];
    }

    public short[] GetFactionShownCells(Faction faction)
    {
        short[] result;
        if (faction == null)
        {
            result = null;
        }
        else
        {
            if (maxFactionLoadId < faction.loadID)
            {
                maxFactionLoadId = faction.loadID + 1;
                Array.Resize(ref factionsShownCells, maxFactionLoadId + 1);
            }

            factionsShownCells[faction.loadID] ??= new short[mapCellLength];

            result = map.Biome.defName == "OuterSpaceBiome"
                ? getFactionFullShow(faction)
                : factionsShownCells[faction.loadID];
        }

        return result;
    }

    public bool IsShown(Faction faction, IntVec3 cell)
    {
        return IsShown(faction, cell.x, cell.z);
    }

    public bool IsShown(Faction faction, int x, int z)
    {
        if (map.IsPlayerHome && RfowSettings.OnlyOutsideColony)
        {
            return true;
        }

        return GetFactionShownCells(faction)[(z * mapSizeX) + x] != 0;
    }

    public void RegisterCompHideFromPlayerPosition(CompHideFromPlayer comp, int x, int z)
    {
        if (x < 0 || z < 0 || x >= mapSizeX || z >= mapSizeZ)
        {
            return;
        }

        var idx = (z * mapSizeX) + x;
        compHideFromPlayerGrid[idx].Add(comp);
        compHideFromPlayerGridCount[idx]++;
    }

    public void DeregisterCompHideFromPlayerPosition(CompHideFromPlayer comp, int x, int z)
    {
        if (x < 0 || z < 0 || x >= mapSizeX || z >= mapSizeZ)
        {
            return;
        }

        var idx = (z * mapSizeX) + x;
        compHideFromPlayerGrid[idx].Remove(comp);
        compHideFromPlayerGridCount[idx]--;
    }

    public void RegisterCompAffectVisionPosition(CompAffectVision comp, int x, int z)
    {
        if (x >= 0 && z >= 0 && x < mapSizeX && z < mapSizeZ)
        {
            compAffectVisionGrid[(z * mapSizeX) + x].Add(comp);
        }
    }

    public void DeregisterCompAffectVisionPosition(CompAffectVision comp, int x, int z)
    {
        if (x >= 0 && z >= 0 && x < mapSizeX && z < mapSizeZ)
        {
            compAffectVisionGrid[(z * mapSizeX) + x].Remove(comp);
        }
    }

    public void RegisterTreeViewBlocker(CompTreeViewBlocker comp, int x, int z)
    {
        if (x >= 0 && z >= 0 && x < mapSizeX && z < mapSizeZ)
        {
            treeViewBlockerGrid[(z * mapSizeX) + x].Add(comp);
        }
    }

    public void DeregisterTreeViewBlocker(CompTreeViewBlocker comp, int x, int z)
    {
        if (x >= 0 && z >= 0 && x < mapSizeX && z < mapSizeZ)
        {
            treeViewBlockerGrid[(z * mapSizeX) + x].Remove(comp);
        }
    }

    // True if this cell is blocked by a tree
    public bool IsTreeViewBlocker(int idx)
    {
        return idx >= 0 && idx < treeViewBlockerGrid.Length && treeViewBlockerGrid[idx].Count != 0;
    }

    public void RegisterMineDesignation(Designation des)
    {
        var cell = des.target.Cell;
        mineDesignationGrid[(cell.z * mapSizeX) + cell.x] = des;
    }

    public void DeregisterMineDesignation(Designation des)
    {
        var cell = des.target.Cell;
        mineDesignationGrid[(cell.z * mapSizeX) + cell.x] = null;
    }

    private void init()
    {
        var array = (Section[,])Traverse.Create(mapDrawer).Field("sections").GetValue();
        sectionsSizeX = array.GetLength(0);
        sectionsSizeY = array.GetLength(1);
        sections = new Section[sectionsSizeX * sectionsSizeY];
        for (var i = 0; i < sectionsSizeY; i++)
        {
            for (var j = 0; j < sectionsSizeX; j++)
            {
                sections[(i * sectionsSizeX) + j] = array[j, i];
            }
        }

        var allDesignations = map.designationManager.AllDesignations;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var k = 0; k < allDesignations.Count; k++)
        {
            var designation = allDesignations[k];
            if (designation.def == DesignationDefOf.Mine && !designation.target.HasThing)
            {
                RegisterMineDesignation(designation);
            }
        }

        if (map.IsPlayerHome && map.mapPawns.ColonistsSpawnedCount == 0)
        {
            var playerStartSpot = MapGenerator.PlayerStartSpot;

            var radius = RfowSettings.BaseViewRange;
            ShadowCaster.computeFieldOfViewWithShadowCasting(playerStartSpot.x, playerStartSpot.z, radius,
                viewBlockerCells, map.Size.x, map.Size.z, false, null, null, null, knownCells, 0, 0, mapSizeX, null, 0,
                0, 0, 0, 0);
            for (var l = 0; l < mapCellLength; l++)
            {
                if (!knownCells[l])
                {
                    continue;
                }

                var c = CellIndicesUtility.IndexToCell(l, mapSizeX);
                foreach (var this2 in map.thingGrid.ThingsListAtFast(c))
                {
                    var compMainComponent = (CompMainComponent)this2.TryGetCompLocal(CompMainComponent.CompDef);
                    if (compMainComponent is { HideFromPlayer: not null })
                    {
                        compMainComponent.HideFromPlayer.ForceSeen();
                    }
                }
            }
        }

        foreach (var thing in map.listerThings.AllThings)
        {
            if (!thing.Spawned)
            {
                continue;
            }

            var compMainComponent2 = (CompMainComponent)thing.TryGetCompLocal(CompMainComponent.CompDef);
            if (compMainComponent2 == null)
            {
                continue;
            }

            compMainComponent2.ComponentsPositionTracker?.updatePosition();

            compMainComponent2.FieldOfViewWatcher?.UpdateFoV();

            compMainComponent2.HideFromPlayer?.UpdateVisibility(true);
        }

        if (map.Biome.defName == "OuterSpaceBiome" || RfowSettings.MapRevealAtStart)
        {
            for (var l = 0; l < mapCellLength; l++)
            {
                knownCells[l] = true;
            }
        }

        mapDrawer.RegenerateEverythingNow();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        DataExposeUtility.LookBoolArray(ref knownCells, map.Size.x * map.Size.z, "revealedCells");
    }

    public void RevealCell(int idx)
    {
        if (knownCells[idx])
        {
            return;
        }

        ref var ptr = ref idxToCellCache[idx];
        knownCells[idx] = true;
        var designation = mineDesignationGrid[idx];
        if (designation != null && ptr.GetFirstMineable(map) == null)
        {
            designation.Delete();
        }

        if (Initialized)
        {
            setMapMeshDirtyFlag(idx);
            map.fertilityGrid.Drawer.SetDirty();
            map.roofGrid.Drawer.SetDirty();
            map.terrainGrid.Drawer.SetDirty();
        }

        if (compHideFromPlayerGridCount[idx] <= 0)
        {
            return;
        }

        var list = compHideFromPlayerGrid[idx];
        var count = list.Count;
        for (var i = 0; i < count; i++)
        {
            list[i].UpdateVisibility(true);
        }
    }

    public void IncrementSeen(Faction faction, short[] factionShownCells, int idx)
    {
        var num = (short)(factionShownCells[idx] + 1);


        factionShownCells[idx] = num;
        if (num != 1 || !faction.def.isPlayer)
        {
            return;
        }

        ref var ptr = ref idxToCellCache[idx];
        if (!knownCells[idx])
        {
            knownCells[idx] = true;
            if (RealFoWModStarter.DubsMintMinimapLoaded)
            {
                var currentDirtyGirls =
                    (bool[])RealFoWModStarter.DubsMintMinimap_MainTabWindow_MiniMap_dirtyGirls.GetValue(null);
                if (currentDirtyGirls != null)
                {
                    currentDirtyGirls[idx] = true;
                    RealFoWModStarter.DubsMintMinimap_MainTabWindow_MiniMap_dirtyGirls
                        .SetValue(null, currentDirtyGirls);
                }
            }

            if (Initialized)
            {
                map.fertilityGrid.Drawer.SetDirty();
                map.roofGrid.Drawer.SetDirty();
                map.terrainGrid.Drawer.SetDirty();
            }

            var designation = mineDesignationGrid[idx];
            var hideMineGrid = designation != null && ptr.GetFirstMineable(map) == null;
            if (hideMineGrid)
            {
                designation?.Delete();
            }
        }

        if (Initialized)
        {
            setMapMeshDirtyFlag(idx);
        }

        if (compHideFromPlayerGridCount[idx] <= 0)
        {
            return;
        }

        var list = compHideFromPlayerGrid[idx];
        var count = list.Count;
        for (var i = 0; i < count; i++)
        {
            list[i].UpdateVisibility(true);
        }
    }

    public void DecrementSeen(Faction faction, short[] factionShownCells, int idx)
    {
        var num = (short)(factionShownCells[idx] - 1);
        factionShownCells[idx] = num;
        if (num != 0 || !faction.def.isPlayer)
        {
            return;
        }

        playerVisibilityChangeTick[idx] = currentGameTick;
        if (Initialized)
        {
            setMapMeshDirtyFlag(idx);
        }

        if (compHideFromPlayerGridCount[idx] <= 0)
        {
            return;
        }

        var list = compHideFromPlayerGrid[idx];
        var count = list.Count;
        for (var i = 0; i < count; i++)
        {
            list[i].UpdateVisibility(true);
        }
    }

    private void setMapMeshDirtyFlag(int idx)
    {
        var num = idx % mapSizeX;
        var num2 = idx / mapSizeX;
        var num3 = num / 17;
        var num4 = num2 / 17;
        var num5 = Math.Max(0, num - 1);
        var num6 = Math.Min(num2 + 2, mapSizeZ);
        var num7 = Math.Min(num + 2, mapSizeX) - num5;
        for (var i = Math.Max(0, num2 - 1); i < num6; i++)
        {
            var num8 = (i * mapSizeX) + num5;
            for (var j = 0; j < num7; j++)
            {
                playerVisibilityChangeTick[num8 + j] = currentGameTick;
            }
        }

        sections[(num4 * sectionsSizeX) + num3].dirtyFlags |= FoWDef.RealFogOfWar;
        var num9 = num % 17;
        var num10 = num2 % 17;
        switch (num9)
        {
            case 0:
            {
                if (num3 != 0)
                {
                    sections[(num4 * sectionsSizeX) + num3].dirtyFlags |= FoWDef.RealFogOfWar;
                    switch (num10)
                    {
                        case 0:
                        {
                            if (num4 != 0)
                            {
                                sections[((num4 - 1) * sectionsSizeX) + (num3 - 1)].dirtyFlags |=
                                    FoWDef.RealFogOfWar;
                            }

                            break;
                        }
                        case 16:
                        {
                            if (num4 + 1 < sectionsSizeY)
                            {
                                sections[((num4 + 1) * sectionsSizeX) + (num3 - 1)].dirtyFlags |=
                                    FoWDef.RealFogOfWar;
                            }

                            break;
                        }
                    }
                }

                break;
            }
            case 16:
            {
                if (num3 + 1 < sectionsSizeX)
                {
                    sections[(num4 * sectionsSizeX) + num3 + 1].dirtyFlags |= FoWDef.RealFogOfWar;
                    switch (num10)
                    {
                        case 0:
                        {
                            if (num4 != 0)
                            {
                                sections[((num4 - 1) * sectionsSizeX) + num3 + 1].dirtyFlags |=
                                    FoWDef.RealFogOfWar;
                            }

                            break;
                        }
                        case 16:
                        {
                            if (num4 + 1 < sectionsSizeY)
                            {
                                sections[((num4 + 1) * sectionsSizeX) + num3 + 1].dirtyFlags |=
                                    FoWDef.RealFogOfWar;
                            }

                            break;
                        }
                    }
                }

                break;
            }
        }

        if (num10 == 0)
        {
            if (num4 != 0)
            {
                sections[((num4 - 1) * sectionsSizeX) + num3].dirtyFlags |= FoWDef.RealFogOfWar;
            }
        }
        else
        {
            if (num10 != 16)
            {
                return;
            }

            if (num4 + 1 < sectionsSizeY)
            {
                sections[((num4 + 1) * sectionsSizeX) + num3].dirtyFlags |= FoWDef.RealFogOfWar;
            }
        }
    }
}