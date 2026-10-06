using System.Collections.Generic;
using System.Linq;
using TotalFog.Utils;
namespace TotalFog.Detours;
public static class MainTabWindowWildlife
{
    public static void get_Pawns_Postfix(ref IEnumerable<Verse.Pawn> __result)
    {
        if (!FogSettings.WildLifeTabVisible) __result = __result.Where(p => p.IsFogVisible());
    }
}
