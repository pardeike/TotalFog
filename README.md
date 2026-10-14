# Total Fog

![Total Fog](About/Preview.png)

**You only know what your colonists can see.**

Total Fog adds real fog of war to RimWorld. The map is dark until someone looks at it.
When your colonists walk away, the area goes grey again, and you no longer know
what happens there. A raid can come out of the dark. A sound in the night may be
a wild boar or something much worse. To be safe, you have to look.

> **Early version for RimWorld 1.6 and 1.7.** Testing is still going on. Feedback, bug
> reports and saves that show problems are very welcome:
> [Brrainz Discord](https://discord.gg/G4r84eN7w6) or
> [GitHub issues](https://github.com/pardeike/TotalFog/issues).

## What you get

### See only what your people see

- A new colony starts unexplored. The landing area becomes known when your colonists actually see it.
- Colonists, colony mechs, trained animals and turrets each see a circle around them. Walls block their view.
  Trees can block it too, if you want.
- Areas you explored stay on the map, but they are grey. You see the ground, not
  who walks on it right now.
- Previously observed scenery uses the game's normal drawing. Trees and buildings
  can change while unseen; Total Fog does not preserve a picture of their last appearance.
- Your construction blueprints and frames remain drawn and controllable outside
  current sight, with normal selection, Cancel, Forbid/Allow and Build copy commands.
  Foreign corpses and
  artificial light require current sight. Your own corpses on known cells can be recovered.
- Your own pawns remain selectable and controllable, even animals that provide no
  vision. Their presence does not reveal nearby things or hidden effects.
- Use the area Forbid/Allow tools for previously seen player doors outside sight.
  With remembered selection disabled, their inspector stays closed until someone
  can see them again.
- Map search includes previously seen static things and your pawns' inventories.
  Never-seen things and hidden enemies stay out of the results.
- Remembered buildings and objects can be selected at their last observed
  location by default. Disable **Select remembered objects** under Information
  to require current sight instead. Hidden pawns and undiscovered objects stay
  unavailable. This setting grants no combat sight or event information.
  Objects remembered in older saves need one fresh observation to record their
  location. No appearance snapshots are saved.
- Allies and neutral visitors can share their view with you through one setting;
  prisoners have a separate sharing option.

### Build your own eyes

- Surveillance cameras: on posts, on walls, under roofs, hidden in bushes, or solar
  powered. By default they need an operator at a working camera console. Turn off
  the operator requirement to use cameras automatically without console jobs.
  Set camera staffing through the separate Cameras priority in the Work tab.
  After adding this work category to an existing save, assign it to an operator.
  An operator leaves when their console stops working or Cameras work is disabled.
- A watch telescope doubles the sight range of pawns standing on it, up to the
  separately configurable telescope sight limit.
- An experimental 3x3 watchtower has a timed ladder and room for one lookout.
  Order a watch of 1, 2 or 4 game hours, counted from reaching the deck, or let a
  drafted lookout hold until undrafted. Moving elsewhere or undrafting brings
  them down first. Drafting an undrafted lookout on the deck converts the watch
  into a drafted hold. Explicit watch orders do not end just because the pawn
  starts hungry or tired.
  Drafted Go here and dragged formation orders can use the tower tops, including
  Achtung's move orders. Undrafted lookouts turn through all four directions;
  idle drafted lookouts face the player, with combat aiming taking priority.
  Its lookout sees over freestanding walls, while roofs, mountains and other
  towers block the view. Shooting from it is off by default. Tower testing is
  still in progress. Completed towers block all nine footprint cells and need
  heavy ground support. New placements put the ladder at the front; rotate
  before placing to choose another side. Blueprints remain walkable until built.
- The new tower needs Complex furniture research. Better cameras and the telescope also need research.
- Keep tower ladders clear. A trapped lookout waits, with a persistent warning. Critical needs, burning or a mental state can force a jump with fall injuries if there is free ground nearby. Retrieve a downed lookout from the tower or pawn menu before tending or feeding them; a bed is optional.
- Shooting from a tower keeps the target's normal cover. A lookout on the deck is exposed to incoming fire.
  Acquiring or tracking an enemy still needs current faction sight. Deliberate
  shots at cells retain blind fire.

Inspector animals, security bells, light beacons and the old watchtowers are
retired. Existing buildings, blueprints and frames remain loadable, but these
buildings no longer provide light or vision, extend sight, or consume fuel.
They cannot be selected for new construction.

### Sounds keep secrets, or give hints

- Combat and Anomaly music are off by default, so the music cannot warn you about unseen threats.
- Sounds from hidden things can be muted, or muffled by distance. Only living
  pawns in your faction provide hearing, according to their hearing capacity.
- Small hearing indicators can show that *something* moves out there.

### News only when you see it

- Letters and messages about things you cannot see can wait until you see them.
- Or you can throw away unseen events, sorted by type (big threats, small threats,
  good news, bad news).
- With delayed notifications enabled, selected map-threat alerts and their jump targets require sight. Health and global alerts remain native.
- Colony health and global events always arrive at once.
- New Anomaly entities enter the journal and send their discovery notice when someone first sees them.
- **Silent raids** (optional): no arrival letter for raids and manhunter packs.
  Arrival slowdown is suppressed too; spawning and normal combat slowdown stay intact.

### Fair enemies

- Optional: humanlike enemies can only target what *their* faction can see.
  Your hidden sniper stays hidden.

### Your rules

Four settings pages (Appearance, Vision, Information, Audio) let you choose how dark
the fog is, how fast it fades, how far everyone sees and what may give away
hidden activity. You can play it as a hard survival challenge or as a light
atmosphere change. Colony mechs have their own vision multiplier, from 0.2 to 2.
Its default of 1 keeps their normal sight range.
The native Clear All Fog, Refog Map and rectangle fog debug commands also affect
Total Fog. Fog drawing updates while paused, including when scrolling an area
out of view and back.

### Defaults and information limits

The current defaults enable map fog and suppress Combat and Anomaly music.
Hidden-source sound checks and delayed unseen-event letters/messages are off,
and the Wildlife tab remains available. To prevent those channels from revealing
hidden activity, enable audio checks and delayed notifications and hide the
Wildlife tab in the settings. Silent Raids, discarding unseen messages and
muting hidden sounds are separate choices and default off. Existing saved
settings are retained when updating. Player-owned things on known home-area cells,
fire in that area and your own corpses on known cells remain available for work.
Those work exceptions do not grant hidden effects or notifications current sight.
An owned blueprint or frame also lets workers clear its hidden obstructions on
known terrain, including hauling, plant cutting and removal. Cleaning can clear
fresh dirt in a known home area. These permissions do not reveal the targets.

Fog does not hide research availability, storyteller threat calculations,
world-map information or the game's pathfinding and flee decisions. Combat can
still trigger native slowdown. Hearing uses distance rather than sound travelling
through rooms. Ordinary sight is a two-dimensional cell grid; roofs affect tower
sight, but ordinary sources do not model height or overhangs. Optional enemy fog
currently applies to humanlike enemies, rather than every animal, mech or insect.

### Made for big colonies

The sight engine is new and written for speed. Performance comes before extra
features. Old visual snapshots of explored areas were removed because they cost
too much.

## Good to know

- **Needs** [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) and RimWorld 1.6 or 1.7. Use the released Steam Harmony for your game version.
- **Replaces** Real Fog of War and NWN Real Fog of War. Turn those off. Total Fog
  can read the explored map and waiting messages from their saves. Please try a
  copy of your save first.
- **Zombieland, on 1.6:** the updated preliminary test copy includes fog support for its
  effects, warnings and multi-cell Symbiant body. These fixes are not all in the
  Workshop version yet. Total Fog also runs without Zombieland.
- **Combat Extended, on 1.6:** weapons, powered turrets, mortars and some combined
  Zombieland controls have passed native tests. Broader loadouts remain under review.
- **Multiplayer, on 1.6:** shared settings, separate player factions, independent map
  clocks, fog refresh, saved-session rejoin and bounded combat checks have passed
  on two local clients. Overall compatibility is still unverified; combined CE,
  cross-platform play and longer sessions need testing.
  Further Multiplayer development is deferred until a concrete report needs it.

RimWorld 1.6 and 1.7 load the same payload, compiled against the 1.6 API.
Native 1.7 checks cover tower orders, blueprints, lookout facing, debug fog
commands and paused fog drawing. Most broader integration checks above use
macOS 1.6; broader 1.7 loadouts, Windows gameplay and longer sessions still
need confirmation.

See [coverage](docs/COVERAGE.md#current-status) for the current
evidence and remaining checks. No public player release or Workshop item exists yet.
The tracked Release DLL has a source/binary receipt checked by CI. Build after
editing gameplay source, or use the separately supplied
preliminary ZIPs. Dated testing and older private archives retain their own
recorded hashes and are not automatically refreshed by a source commit.

## For mod developers: support Total Fog in your mod

Does your mod draw things, play sounds, show counters or choose targets? Then it can
leak information through the fog. For example, a glowing effect in the dark shows
the player where an enemy is.

You can fix that in a few minutes. **Your mod does not need Total Fog.** You do not
add a reference or a dependency. When Total Fog is not installed, your mod works
exactly like before. [Zombieland](https://github.com/pardeike/Zombieland) uses the
same approach in its `TotalFogSupport.cs`.

### Step 1: Add this file to your mod

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

### Step 2: Ask before you draw, play or show

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

### Step 3: Follow three rules

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

### Step 4: Test both ways

- With Total Fog: put a non-player thing into the dark. Its effects and labels
  must disappear. Walk a colonist close: they must come back. Disable colony
  bypasses for this control so they do not intentionally reveal the test area.
- For sounds, enable Mute hidden sounds and check silence while hidden, then
  normal volume when seen. Also check hearing-distance filtering separately.
  Audio filtering is optional and off by default.
- Without Total Fog: everything must work like before. No errors in the log.

### Advanced: things bigger than one cell

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

### All public methods

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
[Visibility.cs](Source/TotalFog/Visibility.cs) and
[SoundAudibility.cs](Source/TotalFog/SoundAudibility.cs).

### Using an AI assistant?

Give it this section and the two files above, then ask: *"Add optional Total Fog
support to my mod. Only hide drawing, sounds and UI. Do not change game logic.
My mod must work without Total Fog."* Check the result against the three rules
in Step 3.

### Stuck?

If your mod needs something these methods cannot do,
[open an issue](https://github.com/pardeike/TotalFog/issues) and describe what you
want to hide and where your code draws it. Small, clear examples help the most.

## Building from source

Total Fog builds with the .NET SDK version in [global.json](global.json). The
solution is `Source/TotalFog.slnx`. The SDK is **10.0.301**; gameplay and companion
DLLs target **.NET Framework 4.7.2**, while independent tests run on .NET 10.
The workflow needs Python 3, Git and `uv` for the pinned Ruff formatter. Live
setup, deployment and measurement commands currently use the macOS Steam install
and local GABS/RimBridgeServer tools. Set `RIMWORLD_MOD_DIR` to use another
installed game's `Mods` folder.

| Command | What it does |
|---|---|
| `./scripts/mod format` | Formats active C# and Python source with the pinned tools |
| `./scripts/mod format-check` | Checks formatting without editing files |
| `./scripts/mod test` | Checks formatting and runs pure tests without a game install or DLL changes |
| `./scripts/mod build` | Checks formatting, runs the tests, builds the mod and records its source/binary receipt |
| `./scripts/mod verify-binary` | Checks that the tracked DLL and build receipt match the current gameplay source and build inputs |
| `./scripts/mod deploy` | Builds and copies the mod into your RimWorld `Mods` folder |
| `./scripts/mod deploy-built` | Installs the already-built gameplay/companion pair without rebuilding; the game must be stopped |
| `./scripts/mod metadata` | Updates the installed description and version/load-folder metadata without changing DLLs; the game must be stopped |
| `./scripts/mod package` | Builds the player ZIP (one `TotalFog/` folder) |
| `./scripts/mod setup` | Installs the build and creates the isolated test profile |
| `./scripts/mod ce-setup` | Builds and deploys the mod and creates the separate Combat Extended test profile |
| `./scripts/mod ce-setup core16` | Uses the offline 1.6 app with Core and the installed Workshop CE payload, in a separate profile; set its logical `Mods` path in `RIMWORLD_MOD_DIR` |
| `./scripts/mod zombieland-setup` | Builds and deploys both mod/companion pairs into the Zombieland test profile |
| `./scripts/mod ce-zombieland-setup` | Builds both mods and creates a separate test profile with Combat Extended, Zombieland and all DLCs |
| `./scripts/mod mp-setup` | Configures separate Multiplayer host/client profiles without building or deploying |
| `./scripts/mod mp-layout` | Places the running host on the left and client on the right of the main display |
| `./scripts/mod dpa-setup [on\|off]` | Adds or removes Dubs Performance Analyzer in a stopped test profile; set `TOTALFOG_GAME_ID` to select another profile |
| `./scripts/mod baseline` | Installs the original mod for comparison tests |
| `./scripts/mod benchmark` | Runs the sight-engine speed comparison |
| `./scripts/mod runtime-benchmark <label> <save> [speed]` | Measures three fresh game processes on the named save; results go to `artifacts/` |
| `./scripts/mod runtime-compare <label> <save> [speed]` | Alternates three baseline/candidate pairs; set `TOTALFOG_COMPARISON_BASELINE_ROOT` to the accepted Total Fog checkout |
| `./scripts/mod feedback-verify <label> [core16]` | Renews exact-binary paired native correctness gates; `core16` uses the isolated offline Core-only fixture |
| `./scripts/mod feedback-package` | Verifies the existing paired gates and creates separate preliminary Total Fog and Zombieland ZIPs |
| `./scripts/mod source-publish` | Pushes committed source and verifies GitHub; does not publish a player ZIP or update Steam |

For a short wide-zoom comparison, prefix `runtime-compare` with
`TOTALFOG_WIDE_VIEW=1 TOTALFOG_COMPARISON_PAIRS=1`. It records the actual visible
map area at root size 100. This is a spot check; use three pairs when a suspected
regression needs confirmation.

The accepted performance baseline is Total Fog commit `07d128b`. The current
optimisation work is complete. Future comparisons preserve correct fog behaviour
and use that build, rather than requiring the same speed as the original mod.
Set `TOTALFOG_BENCHMARK_REF=07d128b` for kernel comparisons. The comparison tools
retain historical defaults when no baseline override is provided; those defaults
are not release requirements. See the
[baseline and measurement limits](docs/VALIDATION.md#accepted-performance-baseline-2026-10-13).

Full logs are kept in `artifacts/logs/`. RimWorld 1.6 and 1.7 load the active
DLL, definitions and assets from `Current/`; the older version folders stay
for history. This active folder is a declared exception to the shared mod
standard. The shared DLL intentionally compiles against the 1.6 reference
package, following the engine developers' compatibility strategy. We will
adopt a specific 1.7 reference when available. Native results retain their
recorded engine versions, hashes and limits.
More details:
[architecture](docs/ARCHITECTURE.md), [coverage](docs/COVERAGE.md),
[validation](docs/VALIDATION.md) and [AGENTS.md](AGENTS.md).

For source-only review builds that should preserve the tracked DLL, use
`TotalFogBuildOutput="$PWD/artifacts/review/Assemblies/" ./scripts/mod build`.
The local `0.1.0` tag identifies an earlier reviewed build, not a public release.
Later gameplay edits need a build before installation. GitHub Actions checks
formatting and pure tests on Linux and Windows for pushes and pull requests; it
does not test RimWorld, mod loading or native rendering.
Preliminary delivery instructions are in [PRELIMINARY.md](PRELIMINARY.md).

## Credits and license

Total Fog is an independent continuation of **Real Fog of War** by Luca De Petrillo
and the **NWN Real Fog of War** continuation maintained by Mlie. Thank you both.
Maintained by Andreas Pardeike. Apache License 2.0: see [LICENSE.md](LICENSE.md)
and [NOTICE.md](NOTICE.md).
