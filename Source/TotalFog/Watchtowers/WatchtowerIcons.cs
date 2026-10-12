using UnityEngine;
using Verse;

namespace TotalFog.Watchtowers;

[StaticConstructorOnStartup]
internal static class WatchtowerIcons
{
    internal static readonly Texture2D Watch = ContentFinder<Texture2D>.Get("UI/Watchtower/Build");
    internal static readonly Texture2D Descend = ContentFinder<Texture2D>.Get(
        "UI/Watchtower/Descend"
    );
    internal static readonly Texture2D Retrieve = ContentFinder<Texture2D>.Get(
        "UI/Watchtower/Retrieve"
    );
}
