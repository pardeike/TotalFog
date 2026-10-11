# Watchtower design draft

Status: accepted direction with wide and compact-deck rendering prototypes
inspected in public 1.6 on 11 October 2026. This is an artwork review checkpoint.
Gameplay is not implemented. This replaces the retired watchtower
concept; existing legacy towers do not grow into a new 3x3 footprint on loading.
The new definition and runtime types will belong to Total Fog.

## Requested behavior

- One rotatable 3x3 structure made from Woody, Stony or Metallic stuff, with
  ordinary material-based health, construction, repair and flammability.
- The lookout stands in the center on a compact upper deck, currently drawn
  1.4 cells wide. Railings sit close to that standing point; the outer footprint
  supplies supports and slanted walls. One outer middle cell supplies the ladder.
- Climbing up and down takes game time and has the normal native progress bar.
  The pawn moves gradually from the cell adjacent to the ladder all the way to
  the center, facing the direction of travel. Descent reverses that movement.
- Only an occupant who has finished climbing gains substantially increased,
  configurable sight. Low walls do not block that sight; mountains and other
  watchtowers do. Normal ground-level sight remains unchanged.
- The occupant is easier to shoot. Destruction can cause severe fall/crush injury.
- Shooting from the platform is disabled by default and configurable.
- When enabled, tower shots can pass over low walls. This was confirmed during
  design review on 2026-10-11; ordinary projectile behavior is insufficient.
- Towers attract a greater share of ordinary lightning strikes. Occupants can
  be injured and flammable towers can catch fire.

## Proposed operating rules

Use one platform occupant and an explicit watch job. Keep the pawn spawned on
the map, selectable and governed by native health, damage, needs and faction
rules. Do not place the pawn in a container or create a second simulation layer.
The logical pawn position remains the tower's center cell; the upper-platform
position is a presentation projection. The paused artwork fixture verifies this
for all four occupants and records their projected DrawPos separately. Its
scoped DrawPos postfix is test instrumentation, not the final gameplay patch:
production rendering must avoid changing unrelated consumers of DrawPos, and
elevated sight/projectile/cover rules must be implemented explicitly.
Climbing, on-platform and descending are simulation states; rendering only reads
their progress. Ladder rotation changes the approach cell and drawing together.

Drafted orders and ordinary job interruptions must descend before leaving.
Downing, death, fire/panic, mental breaks and destruction must release the job and
reservation safely. Rescue access must work for a helpless occupant; the one-person
platform rule must permit the temporary rescuer needed to carry that pawn down.
An inaccessible ladder prevents new occupation. It must not silently strand an
existing occupant or teleport a healthy pawn out of danger.

Initially prefer an explicit player order, with needs able to end the watch.
Automatic staffing and schedules remain proposed, not requirements for the first
implementation. Enemies and faction ownership must use the same occupation rules.
Towers must not support roofs or become portable minified structures.

The physical footprint is the first prototype question. A single native 3x3
building normally applies its passability throughout its footprint, so an
accessible center/ladder cannot be assumed from the artwork. Prove real entry,
exit, pathfinding, rescue and breacher behavior before choosing the representation.
Prefer one authoritative structural health value; avoid nine independently
maintained building objects or a broad replacement of native pathfinding.

## Sight and combat

An occupied tower uses an elevated sight policy. Ground observers retain the
existing wall-blocking policy. Its own structure must not block its occupant's
elevated cast; other towers remain opaque. Do not temporarily mutate shared
blocker grids to achieve this exemption. Roofed rooms both hide their interiors
and block elevated sight beyond them. They are opaque obstacles, not merely
cells omitted from the visible result. Natural rock, mountain roofs and other
towers also remain opaque. Seeing over a freestanding wall must not reveal a
pawn or item through its building's roof.
Tree handling should initially retain the configured tree-blocking policy.

A narrow blind strip immediately behind low walls is a tentative option, only
if it can reuse the cast without expensive extra passes. A blind area near the
tower is also tentative. Neither is part of the first prototype. If adopted,
these rules affect this elevated observer only; another observer who sees the
same cells still reveals them. Do not hide nearby cells globally.

## Artwork and adjacent walls

Start with empty and occupied wood/granite towers. Show the 3x3 footprint, a
raised compact center platform, close railings, supports or slanted masonry walls,
and the rotated ladder. The pawn remains a real native pawn. The drawing must
put the foreground railing in front of it without hiding the whole pawn.

Wall placement is a normal use case. Include a straight perimeter wall meeting
opposite tower edges and a corner meeting two adjacent edges in the first
visual review. Walls meet the outer footprint; they do not share tower cells.
The platform stays visibly above wall height. Keep actual native walls and
their link flags, reusing small fragments of their printed geometry for visual
overlap rather than spawning extra wall objects. The ladder and its approach
cell must remain clear, including at corners. Final construction/pathfinding checks must
reject blocked access without inventing automatic openings in player walls.

The initial visual prototype belongs to the non-shipping BridgeTools companion.
It uses colored meshes built once when spawned and RimWorld's native material;
it needs no new AssetBundle. It renders a paused fixture only, then removes its
temporary drawing patch and objects and reloads the unchanged save. It does not
establish placement, climbing, elevated sight, targeting or combat behavior.
The native review covers wood/granite empty and occupied, a straight wall,
a corner and four ladder rotations in one combined screenshot. In the compact
draft, the rear ladder is drawn before the floor so the deck hides the section
behind it; its railing opening remains visible. The front railing covers only
the lower part of an occupied pawn. These are geometry sketches, not final
artwork or animation. See [VALIDATION.md](VALIDATION.md) for the exact tested pair.

### Compact upper deck

The 11 October follow-up implements Andreas's smaller-deck direction in the
test-only drawing. The base and wall joins remain 3x3; the upper deck is 1.4x1.4
cells and the railing sits 0.65 cells from the center. The visual height projects
1.2 cells toward map north in every rotation. This is a drawing offset, not
physical pawn elevation or implemented climb progress. Four tapered faces join
the wider base to the small deck. The ladder leans inward from the outer approach.

At Andreas's request, the artwork now uses broad plank/masonry divisions, no
wood-grain marks, fewer ladder rungs and thicker dark outlines. Real native
colonists remain legible on the compact platform. The 1.4-cell proportions are
a reviewable draft, not a final art commitment. The older wide-deck screenshot
remains preserved for comparison.

### Cached native wall overlap prototype

The companion now prints the actual adjacent wall into a temporary native
SectionLayer, retaining its materials, texture coordinates, vertex colours,
secondary texture coordinates, normals and connection state. It shifts a small
fragment into the tower and clips its triangles once to the overlap boundaries.
The side join's diagonal clipping edge and dark visible seam use the same
definition. Layering puts these fragments above the lower supports and below
the platform/railing. The ladder's geometry is unchanged. No replacement masking
shader or extra simulation wall object is created.

Public 1.6 source inspection confirms that Graphic_Linked.MatSingleFor resolves
the connected material from the actual wall and its neighbours. A material-only
copy does not include all geometry: Graphic_LinkedCornerFiller.Print adds extra
corner planes. Graphic.Print and SectionLayer/MapDrawLayer provide a possible
route for retaining the actual printed geometry and materials. The straight
wood and granite corner fixtures now establish the appearance of native centered
joins. This is not a compatibility guarantee for other renderers.
Mods with dynamic DrawAt rendering, animated shaders or external drawing patches
need explicit checks before claiming support.

Resolve reusable graphics after mod loading on the game thread. Build only the
small set needed by the prototype or encountered tower/material combinations;
do not eagerly generate every modded wall/stuff permutation. The exact join
depends on neighbouring placed walls, so build it on tower spawn/load and
invalidate it on relevant local wall or appearance changes. Rendering reads
cached meshes and materials without rebuilding, scanning neighbours or allocating
each frame. The fixture explicitly rebuilds its caches after staging the walls.
Native counts are two draw calls for an unjoined tower, three for the straight
wood example and four for the granite corner, with joins grouped by material.
This is a draw-call count, not a benchmark. Wall spawn/destruction/appearance
invalidation, off-center edge joins and a launch warmup remain implementation
work; the prototype is not a player building.

An empty tower provides no sight. Sleeping or downed occupants provide no elevated
sight. Tower range scales with the lookout's ordinary sight capacities and
weather/lighting rules. Proposed starting balance is about twice normal sight,
with a finite maximum radius chosen from measured cost rather than an unlimited
multiplier. Exact bounds and defaults remain open.

Sight, attack target eligibility, cover and projectile interception are separate
rules. Incoming fire also matters: merely increasing hit chance cannot make a
bullet cross a wall that still intercepts it. Prefer an exposure/cover penalty
over a damage multiplier; ordinary armor still applies. Ground melee attackers
must not hit a raised pawn merely because their map cells are adjacent. Attacking
the structure is their basic counter; special climbing creatures need an explicit
integration decision.

Confirmed choice: enabled platform shooting permits shots over low walls.
Outgoing and incoming trajectories therefore need elevation-aware interception,
with mountains and other towers still acting as tall obstacles. The policy must
not simply let the projectile ignore every building along its path. Native
targeting, target cover and intermediate obstacles must agree with the trajectory.
Combat Extended needs its own proven adapter for its trajectory rules. Elevated
incoming-fire geometry, enemy acquisition and compatibility need native proof
before final implementation.
The disabled-shooting policy must cover automatic and manual ranged attacks, with
an explicit decision for offensive abilities and thrown weapons.

## Destruction and weather

Damage, repair and burning remain native building behavior. Breachers should be
able to destroy the structure through their ordinary attack flow. Public 1.6
BreachingUtility requires an edifice and checks actual blocking/path conditions;
giving a decorative standable building more hit points alone does not establish
wall-like breaching behavior.

On destructive collapse, remove elevated sight, apply fall/crush damage once and
leave survivors in a valid ground cell. Distinguish hostile destruction/fire from
ordinary deconstruction: deconstruction waits for an empty platform. Occupants
are not exempt from native explosions or damage during climbing.

Use severe, variable crush injuries inspired by roof collapse, not literal
mountain-collapse logic. Public 1.6 RoofCollapserImmediate's thick-roof branch
applies lethal brain-directed damage. Do not create or collapse a real map roof
to simulate a tower falling.

Bias the destination of existing lightning events toward eligible unroofed
towers. Do not increase storm frequency or give every tower its own timer.
Use ordinary native strike/fire behavior with an explicit occupant damage rule,
avoiding duplicate damage if the native strike already hits the occupant.
Metal may burn less but must not automatically protect the occupant from lightning.
Weather/audio remains global under the existing fog policy.

## Performance, persistence and compatibility

Reuse the current casting and coverage machinery with the smallest separate
elevated blocker policy. A doubled radius can cover roughly four times the cells.
Recompute when occupation, source position, range or relevant blockers change;
avoid recasting a stationary tower every tick, full-map scans and per-frame
allocations. Any added indices should be sparse or initialized only when needed.

Occupant ownership, job phase and progress must survive save/load. Job completion,
collapse, lightning and settings use game ticks/native deterministic randomness.
Multiplayer synchronizes gameplay settings and actions; rendering never advances
climbing. Tower blocker changes follow the existing map/faction invalidation path.

Keep optional combat/mod exceptions outside the core sight algorithm. Before
adding public integration APIs, prove the actual missing contract. Zombieland
needs explicit melee/climbing/rescue checks; CE needs native targeting, cover and
projectile controls. Do not infer compatibility from successful vanilla sight.

## Smallest useful validation sequence

1. Prototype footprint, all four ladder rotations, real climb/descent, interruption
   and rescue; confirm the pawn remains one spawned native pawn and breachers work.
2. Check elevated sight across a low wall, mountain, other tower and roofed room,
   including sight gained/lost at completed climbs and collapse, with save/load.
3. Check incoming fire, disabled/enabled shooting, melee counters, destruction and
   a native lightning event; then the relevant Zombieland/CE and MP boundaries.
4. Compare a small representative workload with several occupied towers at normal
   and wide zoom. Establish the range cap and defaults before adding more options.

Do not begin full gameplay implementation until the footprint and combat choices
have a reviewable, workable design. Optional balance extras can wait.
