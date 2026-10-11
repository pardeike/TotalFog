# Watchtower design draft

Status: proposal for review, not implemented. This replaces the retired watchtower
concept; existing legacy towers do not grow into a new 3x3 footprint on loading.
The new definition and runtime types will belong to Total Fog.

## Requested behavior

- One rotatable 3x3 structure made from Woody, Stony or Metallic stuff, with
  ordinary material-based health, construction, repair and flammability.
- The lookout stands in the center. The perimeter supplies the railing, supports
  and slanted-wall artwork. One outer middle cell supplies the ladder.
- Climbing up and down takes game time and is visibly gradual.
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
blocker grids to achieve this exemption. Elevated sight must still hide interiors under
opaque roofs and respect natural rock, mountain roofs and other towers. Seeing
over a wall must not reveal a pawn or item through its building's roof.
Tree handling should initially retain the configured tree-blocking policy.

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
