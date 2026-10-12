# Total Fog and Zombieland preliminary feedback builds

Private feedback update, 12 October 2026, for RimWorld 1.6. These are two
separate, complete mod ZIPs. Replace the previous local TotalFog and ZombieLand
folders. Earlier feedback fixes are included. This is not a public release.
Keep the separate No Pause Challenge setup fix if you use that mod.

Support: [Brrainz Discord](https://discord.gg/G4r84eN7w6).
Source: [Total Fog](https://github.com/pardeike/TotalFog) and
[Zombieland](https://github.com/pardeike/Zombieland).
The [coverage checklist](https://github.com/pardeike/TotalFog/blob/main/docs/COVERAGE.md)
records remaining checks. Later source commits are not automatically included
in these archives.

## Installation

Close RimWorld, replace each previous local mod folder, and restart. The paths
must end in `Mods/TotalFog/About/About.xml` and
`Mods/ZombieLand/About/About.xml`. Disable the Workshop copy of Zombieland so
only one copy is enabled. Disable NWN Real Fog of War and other fog mods.
Use a copy of your save. Neither mod requires the other to be loaded.

## New watchtower

Build the 3x3 watchtower under Security using wood, stone or metal. Rotate it
to choose the ladder side and leave the cell immediately outside the ladder
open. Select a colonist and right-click the tower to watch from it. There is
room for one lookout. The old retired towers remain loadable; they do not turn
into the new building.

- Climbing and descending use a normal progress bar and take 120 game ticks
  each by default. The colonist moves visually from the ladder to the raised
  deck. Orders and needs bring the lookout down before ordinary work resumes.
- Sight doubles by default, capped at 96 cells. Freestanding walls can be seen
  over; roofed rooms, mountains and other towers block sight.
- Shooting from the tower defaults to off. Enable it in Total Fog settings
  if wanted. A lookout is exposed to incoming fire. The solid base still
  protects a partly climbed colonist from the opposite side of the ladder.
- Collapse causes fall injuries and can kill. Empty the tower before
  deconstruction. Downed occupants and corpses can be retrieved from the ladder;
  rescue continues through RimWorld's normal bed rules.
- Towers are more likely to receive an existing lightning strike. Material
  resistance and fire behavior remain native; an exposed occupant can be hurt.

Sight multiplier, sight cap, climb time and shooting are settings. No automatic
lookout scheduling is included. The optional near-wall blind strip discussed
while designing the tower is not part of this build.

## Zombieland update and retained fixes

Spitter balls that land on a solid building now place their zombie on nearby
open ground. The impact and smoke stay at the actual impact cell. If no open
cell exists within six cells, no zombie payload is spawned.

The earlier corpse-work facing, chainsaw repair, shocker activation, roping,
dead/downed Spitter and hidden Heart alert fixes are retained. Total Fog retains
the colony-mech sight slider, hidden refugee-pod letters, delayed Anomaly codex
discovery, Anomaly music suppression and hauling known vanilla chunks after
sight is lost. Zombieland's optional normal Flee response with avoidance off
remains off by default. This update does not claim to fix every natural
Fleshmass Heart slowdown or reproduce the complete streamed mod list.

## What was checked

All 364 independent Total Fog tests pass. Native Mac RimWorld 1.6 checks with
all five DLCs cover four ladder rotations, blocked exits, orders and needs,
ascent/descent/rescue/corpse save-load, native bed rescue, collapse, camera-edge
visibility and the raised body, label and selection position. Actual native
incoming/outgoing bullets, grenade flight/fuse, shields, particles, fire,
lightning, construction completion, wall joins and a supplied-target breaching
job have bounded controls. The zombie boundary checks run actual structure
damage, native spitter flight/impact/spawn and a bomber's queued blast. They
supply some targets; they do not establish autonomous horde target selection.

The exact gameplay pair passes a fresh three-pair, uninstrumented 1,000-zombie
comparison at native fourth speed: 317.97 versus 280.36 median TPS for the
original fog mod. A one-pair wide spot check covers 50,750 map cells and gives
161.15 versus 155.68 TPS, but frame p95 is worse at 86.95 versus 74.38 ms.
These are bounded results, not a promise about every map or mod list. The common
baseline has no occupied towers; a separate small close/wide occupation check
addresses stationary tower cost.

The package requires fresh exact-pair native rendering, interaction, audio,
contamination, incident and warning gates in addition to those tower checks.
Archive gameplay DLLs must match the native-tested builds. Diagnostic companions
are excluded.

## Useful feedback

Try building a tower into a wall, climbing from each ladder direction, giving
orders during a climb, rescuing a downed lookout, saving/loading while occupied,
and fighting zombies around it. If shooting is enabled, try it with your usual
weapons. A short description plus Player.log or a save is enough to start
investigating a problem; no need to run this whole list.

Windows, long sessions, further weapons, autonomous hauling/raids and broader
combined mod lists remain open. Research availability and similar deep game
systems can still reveal activity; the fog does not rewrite those systems.
Explored scenery uses native current drawing rather than an exact archive of
its last observed appearance.
