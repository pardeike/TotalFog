# Support Total Fog in your mod

Does your mod draw things, play sounds, show counters or choose targets? Then it can
leak information through the fog. For example, a glowing effect in the dark shows
the player where an enemy is.

You can fix that in a few minutes. **Your mod does not need Total Fog.** You do not
add a reference or a dependency. When Total Fog is not installed, your mod works
exactly like before. [Zombieland](https://github.com/pardeike/Zombieland) uses the
same approach in its `TotalFogSupport.cs`.

## Step 1: Add this file to your mod

Create `TotalFogSupport.cs` and change `MyMod` to your namespace. You need only
Harmony, which almost every mod already uses.

```csharp
using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MyMod
{
    // Optional Total Fog support. Your mod does not need a reference to Total Fog.
    // Without Total Fog, everything is "visible" and every sound plays at full volume.
    [StaticConstructorOnStartup]
    public static class TotalFogSupport
    {
        static readonly Func<Thing, bool> thingVisible =
            Bind<Func<Thing, bool>>("Visibility", "IsVisible", typeof(Thing));
        static readonly Func<Map, IntVec3, bool> cellVisible =
            Bind<Func<Map, IntVec3, bool>>("Visibility", "IsVisible", typeof(Map), typeof(IntVec3));
        static readonly Func<Thing, IntVec3, bool> allowsTarget =
            Bind<Func<Thing, IntVec3, bool>>("Visibility", "AllowsTarget", typeof(Thing), typeof(IntVec3));
        static readonly Func<TargetInfo, float> audibility =
            Bind<Func<TargetInfo, float>>("SoundAudibility", "GetAudibilityFactor", typeof(TargetInfo));

        // True when Total Fog is running
        public static bool Active => thingVisible != null;

        // Can the player see this thing right now?
        public static bool IsVisible(Thing thing) => thingVisible == null || thingVisible(thing);

        // Can the player see this cell right now?
        public static bool IsVisible(Map map, IntVec3 cell) => cellVisible == null || cellVisible(map, cell);

        // May this (enemy) pawn target this cell? Use after your normal range and line-of-sight checks.
        public static bool AllowsTarget(Thing observer, IntVec3 cell) => allowsTarget == null || allowsTarget(observer, cell);

        // How loud may a sound from this source be? 0 = silent, 1 = full volume.
        public static float Audibility(TargetInfo source) => audibility == null ? 1f : audibility(source);

        static T Bind<T>(string typeName, string methodName, params Type[] parameters) where T : Delegate
        {
            if (ModsConfig.IsActive("brrainz.totalfog") == false)
                return null;
            var type = AccessTools.TypeByName("TotalFog." + typeName);
            var method = type == null ? null : AccessTools.DeclaredMethod(type, methodName, parameters);
            if (method == null)
            {
                Log.Warning($"[MyMod] Total Fog is active, but TotalFog.{typeName}.{methodName} was not found.");
                return null;
            }
            return (T)Delegate.CreateDelegate(typeof(T), method, false);
        }
    }
}
```

The file looks up Total Fog **once**, when the game starts. After that, each call
is a normal fast method call.

## Step 2: Ask before you draw, play or show

Put the check in the place where your mod draws, plays a sound or shows
information:

```csharp
// Drawing an effect
if (TotalFogSupport.IsVisible(effectSource) == false)
    return;
// ... your normal drawing code ...

// Drawing something per cell (an overlay, a beam, a big area)
if (TotalFogSupport.IsVisible(map, cell))
    DrawMyOverlay(cell);

// Playing a sound
var volume = baseVolume * TotalFogSupport.Audibility(new TargetInfo(noisyThing));

// A counter or readout in the UI: count only what the player can see
if (TotalFogSupport.IsVisible(pawn))
    visibleCount++;

// Enemy targeting: after your own range, hostility and line-of-sight checks
if (TotalFogSupport.AllowsTarget(shooter, targetCell) == false)
    continue;
```

## Step 3: Follow three rules

1. **Hide the picture, not the game.** Use these checks only for drawing, sound,
   UI and targeting. Never use them to stop ticking, damage, healing, spawning or
   any other game logic. A hidden zombie must still walk and bite.
2. **Game thread only.** Call the methods from normal game code (ticks, drawing,
   UI), never from your own background threads.
3. **"Visible" means "seen right now", subject to the player's bypass settings.** An explored but grey area is not visible.
   This is on purpose: the player must not see live activity there. A temporary
   gravship landing preview does not grant sight through these methods.
   In Multiplayer, presentation queries follow the current faction context.
   `AllowsTarget` uses the observing pawn's faction instead. Never use the local
   viewer's sight to decide another faction's gameplay actions.

## Step 4: Test both ways

- With Total Fog: put a non-player thing into the dark. Its effects and labels
  must disappear. Walk a colonist close: they must come back. Disable colony
  bypasses for this control so they do not intentionally reveal the test area.
- For sounds, enable Mute hidden sounds and check silence while hidden, then
  normal volume when seen. Also check hearing-distance filtering separately.
  Audio filtering is optional and off by default.
- Without Total Fog: everything must work like before. No errors in the log.

## Advanced: things bigger than one cell

Total Fog checks normal things by their own cells. If your thing is drawn larger
than its cells, or the player clicks a special spot on it (like a core), register
two small callbacks once at startup:

```csharp
[StaticConstructorOnStartup]
static class MyBigThingFog
{
    static MyBigThingFog()
    {
        if (!ModsConfig.IsActive("brrainz.totalfog"))
            return;
        var type = AccessTools.TypeByName("TotalFog.Visibility");
        if (type == null)
            return; // Total Fog is not running

        // Should any part of this thing be drawn? Your renderer must still
        // clip each part with TotalFogSupport.IsVisible(map, cell).
        var registerRenderer = AccessTools.DeclaredMethod(type, "RegisterRenderer",
            new[] { typeof(Type), typeof(Func<Thing, bool>) });
        Func<Thing, bool> anyPartVisible = AnyPartVisible;
        registerRenderer?.Invoke(null, new object[] { typeof(MyBigThing), anyPartVisible });

        // Which cell counts for selecting and inspecting it?
        // Return IntVec3.Invalid while it is not ready.
        var registerInspection = AccessTools.DeclaredMethod(type, "RegisterInspectionCell",
            new[] { typeof(Type), typeof(Func<Thing, IntVec3>) });
        Func<Thing, IntVec3> coreCell = thing => thing.Spawned ? thing.Position : IntVec3.Invalid;
        registerInspection?.Invoke(null, new object[] { typeof(MyBigThing), coreCell });
    }

    static bool AnyPartVisible(Thing thing)
    {
        foreach (var cell in thing.OccupiedRect())
            if (TotalFogSupport.IsVisible(thing.Map, cell))
                return true;
        return false;
    }
}
```

Keep the callbacks short and read-only. They run often. If a callback throws,
Total Fog suppresses that registered rendering or inspection path until the
callback is replaced. Registration is for the exact type, not its subclasses;
pass a null callback to unregister. The renderer callback does not override
native camera culling and does not make hidden body cells selectable.

## All public methods

| Method | Answers | Use it for |
|---|---|---|
| `Visibility.IsVisible(Thing)` | Can the player see this thing now? | Effects, labels, counters, highlights |
| `Visibility.IsVisible(Map, IntVec3)` | Can the player see this cell now? | Overlays, beams, multi-cell drawing |
| `Visibility.AllowsTarget(Thing, IntVec3)` | May this pawn's faction target this cell? | Enemy AI targeting |
| `SoundAudibility.GetAudibilityFactor(TargetInfo)` | How loud may this source be (0 to 1)? | Custom sound and volume code |
| `SoundAudibility.GetAudibilityFactor(TargetInfo, int)` | How loud is this source with an explicit hearing distance? | Source-specific mixers; this overload applies the policy even when called directly |
| `Visibility.RegisterRenderer(Type, Func<Thing, bool>)` | Should this type be drawn? | Things drawn larger than their cells |
| `Visibility.RegisterInspectionCell(Type, Func<Thing, IntVec3>)` | Which cell is used for selection? | Things with a special click spot |

The exact rules are in the comments of
[Visibility.cs](../Source/TotalFog/Visibility.cs) and
[SoundAudibility.cs](../Source/TotalFog/SoundAudibility.cs).

## Using an AI assistant?

Give it this section and the two files above, then ask: *"Add optional Total Fog
support to my mod. Only hide drawing, sounds and UI. Do not change game logic.
My mod must work without Total Fog."* Check the result against the three rules
in Step 3.

## Stuck?

If your mod needs something these methods cannot do,
[open an issue](https://github.com/pardeike/TotalFog/issues) and describe what you
want to hide and where your code draws it. Small, clear examples help the most.
