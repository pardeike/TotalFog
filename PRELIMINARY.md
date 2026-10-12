# Total Fog and Zombieland preliminary build guide

Updated 12 October 2026 for the current source. Paired Zombieland feedback
testing uses RimWorld 1.6 and two separate, complete mod ZIPs. Total Fog's current
source also supports 1.7 through the shared `Current/` payload, with focused
1.7 tower/fog checks; the paired Zombieland and broader compatibility gates
have not been repeated on 1.7.

The last private ZIP pair was delivered on 12 October before the later watch-order,
wall-link, blueprint, lookout-facing and paused-fog corrections. This guide
describes current source behavior, not a claim that those fixes are in an older
archive. Build the source before installing it, or use the version identified
in the delivery message. A source commit does not replace an installed ZIP.
This is not a public release.
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
Use released Steam Harmony for the chosen game version. When bridge
instrumentation is used, RimBridgeServer belongs immediately below Harmony.

## New watchtower

After Complex furniture research, build the 3x3 watchtower under Security using
wood, stone or metal on terrain with Heavy support. All nine completed cells
block walking; a blueprint remains walkable until built. New placements put the
ladder toward the player. Rotate it
to choose the ladder side and leave the cell immediately outside the ladder
open. Select a colonist and right-click the tower to watch from it. There is
room for one lookout. The old retired towers remain loadable; they do not turn
into the new building.

- Choose a watch of 1, 2 or 4 game hours, or hold until undrafted if the colonist
  is drafted. Timed watches keep the current draft status, start on reaching
  the deck and show their remaining time in the tower's inspection panel.
  Undrafting or ordering the lookout elsewhere makes them climb down first.
  Explicit player orders do not end at ordinary hunger/rest thresholds.
  Downing, mental states, burning and native interruptions still end duty.
  Remaining watch time survives save/load. Drafting an undrafted lookout already
  on the deck converts the timed watch to holding until undrafted.
- Drafted Go here and dragged formation orders can place colonists on tower
  tops, including Achtung's move orders. Undrafted lookouts turn through four
  directions; idle drafted lookouts face the player, with combat aiming taking
  priority. Undrafted lookouts step clear of the ladder after duty unless queued
  work already gives them a destination.
- Climbing and descending use a normal progress bar and take 120 game ticks
  each by default. The colonist moves visually from the ladder to the raised
  deck. Orders and needs bring the lookout down before ordinary work resumes.
- Sight doubles by default, capped at 96 cells. Freestanding walls can be seen
  over; roofed rooms, mountains and other towers block sight.
- Shooting from the tower defaults to off. Enable it in Total Fog settings
  if wanted; automatic shooting also respects Fire at will. A lookout is
  exposed to incoming fire. The solid base still
  protects a partly climbed colonist from the opposite side of the ladder.
- Collapse causes fall injuries and can kill. Empty the tower before
  deconstruction. Downed occupants and corpses can be retrieved from the ladder;
  rescue continues through RimWorld's normal bed rules.
- Towers are more likely to receive an existing lightning strike. Material
  resistance and fire behavior remain native; an exposed occupant can be hurt.

Sight multiplier, sight cap, climb time and shooting are settings. No automatic
lookout scheduling is included. The optional near-wall blind strip discussed
while designing the tower is not part of this build.

## Fog settings and debugging

Combat and Anomaly music suppression is on by default. Hidden-source audio
filtering, delayed unseen-event notifications, Silent Raids and discarding
unseen events are optional and off by default; the Wildlife tab stays available.
Enable those settings for stricter information hiding. Existing settings survive
updates. Clear All Fog, Refog Map and the rectangle fog commands also change
Total Fog while paused. An unfinished fog fade settles when drawn while paused,
so panning does not leave stale straight section edges.

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

The current source passes 405 independent tests. GitHub source checks pass on
Linux and Windows; they do not run RimWorld. Focused native 1.7 follow-ups cover
platform orders, blueprints, lookout facing, debug fog commands and paused fog
seams. Their recorded hashes and limits are in docs/VALIDATION.md.

The 12 October delivered pair passed 364 independent tests. Its broader native
Mac RimWorld 1.6 checks with
all five DLCs cover four ladder rotations, blocked exits, orders and needs,
ascent/descent/rescue/corpse save-load, native bed rescue, collapse, camera-edge
visibility and the raised body, label and selection position. Actual native
incoming/outgoing bullets, grenade flight/fuse, shields, particles, fire,
lightning, construction completion, wall joins and a supplied-target breaching
job have bounded controls. The zombie boundary checks run actual structure
damage, native spitter flight/impact/spawn and a bomber's queued blast. They
supply some targets; they do not establish autonomous horde target selection.

That delivered gameplay pair passed a three-pair, uninstrumented 1,000-zombie
comparison at native fourth speed: 317.97 versus 280.36 median TPS for the
original fog mod. A one-pair wide spot check covers 50,750 map cells and gives
161.15 versus 155.68 TPS, but frame p95 is worse at 86.95 versus 74.38 ms.
These are bounded results, not a promise about every map or mod list. The common
baseline has no occupied towers; a separate small close/wide occupation check
addresses stationary tower cost.

The next feedback package requires exact-pair native rendering, interaction, audio,
contamination, incident and warning gates in addition to those tower checks.
Archive gameplay DLLs must match the native-tested builds. Reuse unaffected
evidence and repeat checks affected by new changes. The older comparison is not
a fresh performance result for the latest source. Diagnostic companions are excluded.

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
