# Total Fog preliminary startup fix

Private feedback update, 10 October 2026, for RimWorld 1.6. Total Fog remains
version 0.1.0. This Total Fog ZIP replaces its earlier 10 October archive.
Keep the Zombieland test build from that pair and the separate No Pause Challenge
setup fix. It is not a public release. Broader mod lists, Windows and long
sessions still need testing.

Support: [Brrainz Discord](https://discord.gg/G4r84eN7w6).
Source and current coverage: [Total Fog](https://github.com/pardeike/TotalFog)
and [coverage](https://github.com/pardeike/TotalFog/blob/main/docs/COVERAGE.md).
Later source commits are not automatically included in these archives.

## Installation

Close RimWorld and replace the previous local TotalFog folder with the one in
this ZIP. Keep your existing ZombieLand folder from the paired test ZIP. The paths
must end in `Mods/TotalFog/About/About.xml` and
`Mods/ZombieLand/About/About.xml`. Disable the Workshop copy of Zombieland so
only one copy is enabled. Disable NWN Real Fog of War and other fog mods.

Start with Harmony, Core, your DLCs, Zombieland and Total Fog, then try your
usual mod list. Restart after changing builds and use a copy of your save.
Saves from NWN Real Fog of War report a different mod list. Total Fog imports
its exploration and observed-object data, but general save compatibility is
still under test. The included Zombieland has integration fixes that are not
yet all in its Workshop release. Neither mod needs the other to be loaded.

## Changes since the previous test pair

- Avoid a startup error when another mod bundles the Multiplayer API but
  Multiplayer itself is not loaded. This is a single-player startup fix too.

The other changes below were already included in the earlier 10 October pair.

- Discard settings also catch unseen white event messages, including neutral
  and positive messages. Health, global and currently visible notifications
  still appear. Each category has its own setting.
- Artificial light is clipped outside current sight. Fire and torch gameplay,
  including heat and illumination, keep running. A source may still cast light
  onto visible ground, which you can actually see.
- Your blueprints remain visible on explored cells outside current sight.
  Ownership does not expose live object information or undiscovered cells.
- Corpses disappear from the display when sight is lost. They remain in the
  simulation, avoiding the clue of a remembered corpse disappearing only when
  it becomes a zombie.
- Previously observed player doors can be forbidden or allowed through the
  area Forbid/Allow tools outside current sight. This does not open the hidden
  door's live inspector.
- Zombie infection labels are green.
- Ordinary zombies use RimWorld's native silhouette highlights at far zoom.
  Camera+ is optional. Select the desired native highlight mode in game options.
- Drafted colonists can be ordered to Double Tap with Hunting unassigned.
  Incapable colonists still cannot perform the job. Automatic work is unchanged.
- Zombieland's settings page is inserted once into the new-game page chain.
  Reusing a page list does not add duplicate settings pages.

The earlier paired fixes remain: Symbiant bodies retain their connected native
rendering while clipping at the fog boundary, visible cores can be inspected
and targeted independently of a hidden root, and healer effects, zombie
counters, contamination overlays, local sound cues and danger warnings follow
sight. Wall-climbing facing advances by game ticks. Hidden explosions no longer
shake the camera. Optional enemy fog targeting and Silent Raids are separate
settings. No appearance archive or drawing capture is saved to disk.

## Settings and remaining limits

Open Options, Mod settings, Total Fog. In Audio, combat music suppression is
on by default for fresh settings. Existing settings can override that default.
Review hidden-source audio and hearing choices for the amount of information
you want. In Information, Silent Raids is off by default. Enabling it removes
raid and manhunter arrival warnings even when visible; normal combat slowdown
and spawning remain native. Discard unseen messages and letters applies only
to hidden events. It is not a blanket mute for every event of that category.

Explored scenery uses the game's current drawing. Remembered walls can change
or disappear when damaged or destroyed outside sight. Exact last-observed
appearance was removed to prioritize performance. This limitation is still
open in this pair.

The local new-game controls now reach a playable colony, including site and
character selection, with one Zombieland settings screen. The current Zombieland
DLL and the fixed No Pause Challenge DLL were used. Mortal still reports a
second Zombieland screen after site selection in his setup; that remains
unreproduced and his exact DLL/loadout confirmation is pending.
Hidden transport-pod and harbinger notification filtering was
checked through the native message/letter pipeline; their entire natural
incident sequences were not reproduced for this check.

## Checks on this update

All 323 independent tests pass, including a failing-before/fixed-after test for
an uninitialized bundled Multiplayer API. Native Mac startup reproduces the
original error using the same API build, and the fixed Total Fog starts and
loads the existing 1,000-zombie fixture without recognized errors. A separate
native startup control with Multiplayer and its Prepatcher dependency also has
no recognized errors. This does not establish fresh two-client compatibility.

## Earlier paired checks

The earlier pair passed 322 tests. Its native Mac controls cover the changed
message categories and health/global/visible exceptions, real fire and torch
lighting, own blueprints, corpses, hidden door area orders, native far-zoom
zombie silhouettes and an actual drafted Double Tap job with Hunting set to
zero. Normal page-chain reuse and unrelated-page controls pass. Close and
far-zoom screenshots check sight loss and reveal. These are bounded controls,
not proof of every gameplay situation or Windows behavior.

That pair's full gate also passed 12 Symbiant pixel/interaction cases,
54 targeting checks, 40 dense/sparse rendering-cost rows, four Silent Raids
arrival controls, seven danger-warning controls, 21 Albino controls,
16 explosion-camera controls and 14 contamination refresh controls.
Recognized native logs were clean. Those rendering and performance records
identify the earlier gameplay DLLs; they were not rerun for this startup-only
change. Bridge instrumentation is excluded from player archives.

Six fresh matched Mac samples at native fourth speed on the 1,000-zombie
fixture measure median 326.73 TPS for Total Fog versus 294.64 for
the original, +10.89%. Frame p95 medians are 69.82 versus
70.12 ms. Actual ticks use multiplier 15 with debug speed boost
off. Save, camera, settings and Zombieland bytes match. Each mod retains its
own visibility policy. This does not establish performance for all maps,
combat-heavy mod lists, Windows or long sessions. Current paired acceptance
and its exact hashes are in the source coverage documentation. One wide-view
spot check showing 50,750 map cells measures 169.70 versus 159.87
TPS, while frame p95 is worse at 85.49 versus
75.66 ms. This is not a general FPS improvement. The old
archives' figures are not measurements of these DLLs.

## Feedback wanted

First try the issues you reported, then your normal save and busy horde at your
usual speed. Include a distant zoom view as well as ordinary play. Report hidden
activity leaking through notifications, effects, highlights or sound, visible
things disappearing, setup loops and noticeable slowdowns. A Player.log, save,
short clip and enabled mod list make these much easier to reproduce.
