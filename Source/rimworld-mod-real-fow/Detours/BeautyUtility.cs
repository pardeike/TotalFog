using Verse;

namespace RimWorldRealFoW.Detours;

public static class BeautyUtility
{
    public static void FillBeautyRelevantCells_Postfix(Map map)
    {
        var mapCmq = map.GetMapComponentSeenFog();
        if (mapCmq != null)
        {
            RimWorld.BeautyUtility.beautyRelevantCells.RemoveAll(c =>
                !mapCmq.knownCells[map.cellIndices.CellToIndex(c)]);
        }
    }
}