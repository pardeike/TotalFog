using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimWorldRealFoW.Utils;

public static class FoWThingUtils
{
    private static readonly Dictionary<IntVec3, IntVec3[]> peekArrayCache = new(15);

    public static bool PlantBlocksView(Plant plant)
    {
        if (plant?.def.plant == null)
        {
            return false;
        }

        if (!plant.def.plant.IsTree)
        {
            return false;
        }

        return !plant.def.plant.isStump;
    }

    public static IntVec3[] GetPeekArray(IntVec3 intVec3)
    {
        IntVec3[] result;
        if (peekArrayCache.TryGetValue(intVec3, out var value))
        {
            result = value;
        }
        else
        {
            var array = new[]
            {
                intVec3
            };
            peekArrayCache[intVec3] = array;
            result = array;
        }

        return result;
    }

    public static PendingAlertManager GetPendingAlertManager(this Map _this)
    {
        return _this.GetComponent<PendingAlertManager>();
    }

    extension(Thing _this)
    {
        public bool FowIsVisible(bool forRender = false)
        {
            if (!_this.Spawned)
            {
                return true;
            }

            if (RfowSettings.OnlyOutsideColony && _this.Map?.IsPlayerHome == true)
            {
                return true;
            }

            if (ModLister.OdysseyInstalled && RfowSettings.ClearFogDuringTargeting &&
                WorldComponent_GravshipController.CutsceneInProgress ||
                Find.GravshipController?.LandingAreaConfirmationInProgress == true)
            {
                return true;
            }

            if (!_this.def.isSaveable || _this.def.saveCompressible)
            {
                return forRender || _this.Map != null && _this.fowInKnownCell();
            }

            var compHiddenable = _this.TryGetCompHiddenable();
            if (compHiddenable != null)
            {
                return !compHiddenable.Hidden;
            }

            return forRender || _this.Map != null && _this.fowInKnownCell();
        }

        private bool fowInKnownCell()
        {
            var mapComponentSeenFog = _this.Map.GetMapComponentSeenFog();
            bool result;
            if (mapComponentSeenFog != null)
            {
                var mapSizeX = mapComponentSeenFog.mapSizeX;
                var position = _this.Position;
                var size = _this.def.size;
                if (size is { x: 1, z: 1 })
                {
                    result = mapComponentSeenFog.knownCells[(position.z * mapSizeX) + position.x];
                }
                else
                {
                    var cellRect = GenAdj.OccupiedRect(position, _this.Rotation, size);
                    for (var i = cellRect.minX; i <= cellRect.maxX; i++)
                    {
                        for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
                        {
                            if (mapComponentSeenFog.knownCells[(j * mapSizeX) + i])
                            {
                                return true;
                            }
                        }
                    }

                    result = false;
                }
            }
            else
            {
                result = true;
            }

            return result;
        }

        public ThingComp TryGetCompLocal(CompProperties def)
        {
            var category = _this.def.category;

            if (category != ThingCategory.Pawn
                && category != ThingCategory.Building
                && category != ThingCategory.Item
                && category != ThingCategory.Filth
                && category != ThingCategory.Gas
                && !_this.def.IsBlueprint)
            {
                return null;
            }

            if (_this is ThingWithComps thingWithComps)
            {
                return thingWithComps.GetCompByDefType(def);
            }

            return null;
        }

        public CompHiddenable TryGetCompHiddenable()
        {
            var compMainComponent = (CompMainComponent)_this.TryGetCompLocal(CompMainComponent.CompDef);
            var result = compMainComponent?.Hiddenable;

            return result;
        }
    }
}