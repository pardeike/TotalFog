using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TotalFog.Utils;

public static class FogThingUtility
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

    public static DeferredNotifications GetDeferredNotifications(this Map _this)
    {
        return _this.GetComponent<DeferredNotifications>();
    }

    extension(Thing _this)
    {
        public bool IsFogVisible(bool forRender = false) => Presentation.ThingVisibility.IsVisible(_this);

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

        public CompPresentationState GetPresentationState()
        {
            var compMainComponent = (CompFog)_this.TryGetCompLocal(CompFog.CompDef);
            var result = compMainComponent?.Hiddenable;

            return result;
        }
    }
}