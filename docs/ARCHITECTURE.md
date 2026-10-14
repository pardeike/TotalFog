# Total Fog architecture

Performance takes priority over optional features, while required fog correctness
is preserved. Andreas accepted Total Fog commit 07d128b as the performance
baseline on 13 October 2026 and closed the optimisation topic. Future checks
compare against that build on matching saves, settings, camera views, engine
and hardware. Kernel timings alone cannot establish whole-game performance.
The inherited mod is historical evidence, not a minimum speed requirement.
Reopen performance work for a repeatable material player slowdown or a demonstrated
defect; do not trade visibility correctness for a better timing number. See the
[accepted baseline](VALIDATION.md#accepted-performance-baseline-2026-10-13).

The replacement [watchtower](WATCHTOWER.md#production-checkpoint) is an implemented
3x3 player building with ladder jobs, elevated sight and combat rules. Its pawn
stays spawned at the logical platform cell; body, UI and projectile drawing use
separate elevation projections. Optional CE behavior is isolated in its adapter.
Earlier artwork/access prototypes remain companion diagnostics, outside the
player payload. The tower is experimental and broader acceptance remains open.

## Ownership

RimWorld owns spawned objects, simulation, renderer registration, meshes,
materials and textures. Fog does not despawn or recreate game objects, copy GPU
resources, intercept material writes, or save drawing archives. Native drawing
continues for observed static objects. Their appearance can change while unseen;
exact last-observed pictures are intentionally outside this mod's requirements.

Exploration is a saved boolean grid for each player faction. Current sight is a
reference-counted grid rebuilt from faction observers. Object observation records
which player factions have seen it, rather than storing an appearance. The
primary faction retains the original `seenByPlayer` save value; extra factions
have separate observation identities. A new object in an explored cell stays
hidden until observed. Pawns, flyers, projectiles, corpses and
ordinary motes require current sight. Player pawn observers and owned blueprints
and construction frames retain presentation exceptions. Owned pawns and those
construction orders also retain selection, labels, inspection and control menus
outside current sight.
That exception never grants current sight to effects, notifications, other
targets or neighbouring cells. Native undiscovered fog still takes precedence.

- `Source/Core` owns coverage, discovery, geometry, sight casting and information
  policies. It has no Verse, Unity, Harmony, DLC or optional-mod references.
- RimWorld components translate movement, ownership, capacities, buildings and
  settings into sight. Footprint differences change coverage only where needed.
- Presentation gates print eligibility and the engine's completed culling flags.
  All pawn draw phases use the same decision and native interpolated drawing cell.
  Other labels, overlays, tooltips, selection and inspection require current
  sight even when an observed static object remains eligible for native rendering.
- Notifications retain original payloads until their targets are observed, survive
  saves and replay once. Audio has independent settings for combat music, hidden
  sources, hearing filtering and hearing indicators.
- Optional integrations own their feature detection and exceptions. They do not
  change the core visibility algorithm.

Artificial glow is clipped in SectionLayer_LightingOverlay, using the current
viewer's shown-cell array once per mesh generation. Fog section changes also dirty
GroundGlow. Native GlowGrid illumination, glower propagation, heat and other
simulation remain unchanged. The remembered native scenery policy can retain
visual details baked into an object's ordinary drawing, including power warnings;
it is not a snapshot of the last observed appearance.

Previously observed player doors accept area Forbid/Allow orders on explored
cells outside sight. This expresses player intent without enabling hidden
selection, inspection or live target information. Other things retain the
normal current-sight designator gates.

### Work permission and search

`ThingVisibility.IsActionable` is the shared work-permission boundary for
reservations, reachability and storage hauling. Native false results remain
false. On known, native-unfogged terrain, an owned blueprint/frame authorizes
clearing actual construction obstructions: haulable items, plants and buildings.
The native construction system still chooses its ordinary haul-aside, cut,
deconstruct or mine job and retains its work-capability and forbidden rules.
The known home area similarly authorizes cleaning fresh filth. These exceptions
never grant observation, remembered drawing or live information.

`MapSearchVisibility` restricts native search eligibility and protects cached
navigation using the same remembered-static policy as drawing. Observed static
things can remain searchable at their native positions; unknown replacements,
hidden mobile targets and hidden enemy inventories cannot. Player pawn contents
and current registered inspection cores retain their corresponding access.
Selection still requires current sight under the ordinary inspection policy.
Owned pawns, blueprints and frames retain their explicit control exceptions.
Click and double-click selection filter hidden Things from the native mixed
object sequence while retaining zones and plans. Rectangle selection filters
hidden candidates before native category priority, so a hidden resource or pawn
cannot preempt eligible construction orders. The engine still owns ordering,
matching, selection limits and the commands themselves.
The native explicit debug restriction override remains available. Search does
not mutate listers, simulation or saved state, and adds no location archive.

## Update and save rules

### Fog presentation

Fog sections fade using simulation ticks during normal play. A paused draw
finishes any outstanding fade at its target, including a section returning from
outside the camera view. This changes presentation only and prevents neighboring
sections from retaining different stale shades indefinitely while paused.

A watchtower's solid 3x3 base takes the least fog opacity among its footprint
cells, matching the building's whole-object drawing. This changes section samples
only, never discovery, observer counts or targeting. Neighbouring ground retains
its own fog. Sight changes and tower spawn/despawn dirty the whole footprint and
its section borders; clipped map-edge samples retain native edge repetition.

The raised artwork extends beyond that physical base. Fog vertices within its
drawing bounds sit below the native opaque tower material, so the deck and
lookout write depth before fog shades the background. Alpha and logical sight
remain unchanged there. Spawn/despawn revises this local height mask; ordinary
sight updates still upload colors only. Remembered towers and native wall joins
receive the remembered shade through reused material property blocks.

### Debug fog commands

The native Clear All Fog, Refog Map, Fog (rect) and Unfog (rect) debug commands
also update Total Fog. A debug reveal contributes one idempotent observer to
the current player's existing coverage grid. Removing it never overwrites real
sight-source counts. Its bookkeeping is allocated and read only by debug
commands, and is not saved. Discovery changes still use the usual saved grids.
Rectangle hooks wrap the native rectangle action; ordinary native unfogging is
unchanged. Debug commands reconcile registered presentation immediately and
regenerate with fading temporarily disabled so edits work while paused.
Before applying a debug fog edit, refresh native wall/door/tree blocker comps,
invalidate the elevated blocker snapshot and force every registered sight source
to recast. Refog therefore restores actual current sight after paused map edits,
instead of copying stale observer counts. This full refresh runs only on explicit
debug commands; ordinary source scheduling is unchanged.

### Multiplayer context

Multiplayer is optional. Its API is bound once, with retained delegates and no
hard assembly dependency. Shared primitive settings are registered in stable
ordinal name order, watched through MPAPI around UI changes, and persisted in
Multiplayer sessions before map initialization. Ordinary single-player saves do
not acquire a second settings store. Appearance changes do not recalculate sight;
vision settings take effect within the existing source-refresh interval.

Simulation uses the observer's faction, independently of the client's current
viewer. Primary-faction discovery uses direct arrays; extra player grids and
object-observation lists are allocated only when needed. Legacy global knowledge
belongs to the saved primary owner, rather than whichever client first draws it.
Hearing cues retain their recipient faction. Deferred notifications save their
recipient and temporarily use Multiplayer's native faction context when checked
or replayed. Hidden non-historical local command feedback is not added to a shared
saved queue.

Tick callbacks already carry the native map context. Deadlines created by world
commands or UI callbacks explicitly read the owning map's asynchronous clock,
so sight, hearing, observation and delayed letters cannot inherit another map's
or the world's time. This binding adds no clock lookup to ordinary sight loops.
Temporary gravship previews change drawing only, not discovery, observations,
notification eligibility or audio sight.

Two-client evidence and remaining acceptance are in
[COVERAGE.md](COVERAGE.md#zetriths-multiplayer-unverified). The optional bridge
commands are diagnostics, not dependencies of the player mod.
Further Multiplayer work is deferred until concrete feedback justifies it;
retaining the integration does not claim overall compatibility.

### Other mods

Other mods can bind `TotalFog.Visibility.IsVisible(Verse.Thing)` once as a
`Func<Thing, bool>` without a build dependency. It applies the existing current
sight policy, including held-map lookup, vanilla fog and configured bypasses.
Remembered static appearance and player ownership do not grant visibility for
live effects. Null/mapless things keep the existing unrestricted presentation
policy; hearing indicators follow their recipient faction. Call on the game thread; the query does not
advance simulation, refresh sight, change registration or create effect state.

Zombieland uses this optional query for the target of a visible healer's
beam/glow and its live zombie counter/hover highlights. Its normal pawn rendering
continues through native culling. Binding occurs once at startup; there is no
per-frame reflection. The readout counts only living spawned zombies in current
sight, excludes queued spawning, and uses a static background rather than the
map-wide ticking fraction. Its hover limit uses the displayed count and filters
each highlighted zombie again. Without the query, Zombieland keeps its ordinary
population readout and unrestricted effects. Its simulation population count and
spawning limits are unchanged. Healing
records belong to Zombieland's simulation: each selected healer ages its at-most
eight records by elapsed game ticks and removes expired or invalid targets.
The renderer only reads the records. Their 60-tick lifetime follows game speed
and pause, not FPS, camera position or fog. No extra Total Fog scheduler or saved
format is introduced by this integration.

Renderers with a footprint beyond their root can bind
`Visibility.IsVisible(Map, IntVec3)` and register an exact Thing type through
`Visibility.RegisterRenderer(Type, Func<Thing, bool>)`. This is a draw-only gate
after native camera/depth rejection. It does not broaden root-based inspection,
labels or targeting. The callback must be read-only and its renderer must clip
body, effects and shadows to current cell sight, including vanilla fog. A failed
callback suppresses that type and reports once until explicitly replaced.

For a separately moving interaction core, the mod can opt its exact type into
`Visibility.RegisterInspectionCell(Type, Func<Thing, IntVec3>)`. Current live
inspection and selection use that cell, including vanilla fog and the existing
initialization/colony bypasses. Tooltips anchor there; labels and overlays also
require current sight at their own rendered root. A visible core does not grant
visible UI at a hidden root. Rendering and memory remain separate.
The callback must be read-only and bounded; Invalid or a fault closes inspection.
A fault is reported once and remains closed until replacement. Unregistering
restores the ordinary footprint policy. This is not an arbitrary visibility
grant: manual whole-body targeting still checks each clicked cell through the
cell API. The registry is queried only for live information, not ordinary body
rendering. Zombieland supplies an already established core without initializing
or advancing its state; the moving-core endpoint lookup uses no array or sort.

Zombieland's Symbiant registers through this optional API without a compile-time
dependency. It clips the existing body and rotating selection-core quads into
visible cell runs, preserving their mask UVs and world-space shading. It keeps
scratch buffers and meshes, bounds mask reads to the native camera/gravship view,
and rebuilds a stationary body mesh only when its transform, bounds or sight
changes. Hidden patch footprints reject before overlapping element probes;
sparse bodies also check real cell/motion bounds using a conservative shader-field
distance with bilinear texel padding. Resource release disposes the added meshes.
The gate still permits visible logical cells to rebuild missing patch resources.
No shaders, GPU snapshots, saved state or simulation rules are added. Supported
GPU fallback keeps a clipped core cue; full fallback body rendering is unverified.
Without Total Fog's registration/cell API, Zombieland uses its original renderer.

Logical targets spanning several cells can query
`Visibility.AllowsTarget(Thing, IntVec3)` after their native target checks.
It applies the existing optional enemy-fog policy to non-player humanlike pawns,
using their own faction's current sight. It returns true outside that policy and
never grants hostility, line of sight, range or reachability. Zombieland binds the
query once and filters automatic body-cell acquisition and melee-cell selection.
Player presentation fog does not restrict enemy combat when enemy fog is disabled.
The query adds no sight state, cache or saved data; absent or older APIs preserve
Zombieland's native targeting.

Sight masks and delegates are reused. Moving sources recompute on movement;
stationary sources reconcile on deadlines or explicit signals. Blocker changes
scan only sources with a positive sight radius. Sources join or leave that small
map-owned list when their range crosses zero, and unlink on despawn or map
transfer. The complete source list remains available for settings refreshes.

Each affected source has at most one reusable, lazily allocated pending node.
A real synchronous refresh cancels that node before publishing its difference;
an unchanged periodic check leaves it pending. The FIFO drain detaches each node
before refreshing it, so callbacks can request fresh work without losing that
request or invalidating an enumerator. A pass processes at most its initial entry
count. This is cancellation/coalescing, not the proposed per-tick quota or
freshness-deadline scheduler; that experiment is deferred under the accepted
performance baseline. Pending nodes and the
active-source list are transient and reconstructed on load. Coverage changes
are batched into dirty fog sections. Native terrain, roof and fertility drawers
become dirty only when discovery changes. Idle frames must not cause fog work.

Runtime types use TotalFog namespaces and independently named components.
Exploration and object observation keys remain stable. Specific old save types
are imported at the engine loading boundary, without declaring old type aliases. Older preliminary `observedSections` archive data is ignored and is not
written again. Saves still contain RimWorld's actual world state.

## Optional integration example

Bind once during startup after mod assemblies are loaded. This example needs
only the other mod's existing Verse and Harmony references, not a Total Fog
assembly reference. It retains ordinary drawing when Total Fog is absent or
the query cannot be bound.

```csharp
using System;
using HarmonyLib;
using Verse;

[StaticConstructorOnStartup]
static class OptionalFog
{
    static readonly Func<Thing, bool> isVisible = Bind();

    static Func<Thing, bool> Bind()
    {
        if (!ModsConfig.IsActive("brrainz.totalfog")) return null;
        var type = AccessTools.TypeByName("TotalFog.Visibility");
        var method = type == null ? null :
            AccessTools.Method(type, "IsVisible", new[] { typeof(Thing) });
        return method == null ? null :
            (Func<Thing, bool>)Delegate.CreateDelegate(typeof(Func<Thing, bool>), method, false);
    }

    internal static bool CanDraw(Thing source) => isVisible?.Invoke(source) ?? true;
}

// In the existing draw method, on the game thread:
// if (!OptionalFog.CanDraw(effectSource)) return;
// Continue the existing drawing code.
```

The gate belongs in drawing or UI code. It must not stop ticking, damage,
healing, spawning or other simulation. A renderer spanning multiple cells must
also clip its geometry through the cell query; the thing query alone does not
clip it. For gameplay target acquisition, bind `AllowsTarget(Thing, IntVec3)`
with the same startup pattern and apply it after native hostility, range and
line-of-sight checks. Player presentation sight must not replace faction sight.
The [public API source](../Source/TotalFog/Visibility.cs) documents the cell,
renderer and inspection contracts. [SoundAudibility](../Source/TotalFog/SoundAudibility.cs)
provides the separate positional audio query.

An upstream report should link this section and the relevant source using the
published commit, include its minimal native reproduction and adapt the short
example to the actual affected function. The escalation rule is in
[AGENTS.md](../AGENTS.md#blocked-integrations-with-other-mods).

## Evidence and acceptance

The caster is newly authored from the symmetric shadowcasting description at
https://www.albertford.com/shadowcasting/. Independent tests cover overlapping
sources, reciprocal sight, radius, blockers, rectangular maps, footprints and
lifecycle behavior. Engine tests must cover rendering, registration, save/load,
UI, notifications and audio separately.

For a demonstrated regression, compare load time, actual tick progress,
frame-time distribution and allocations against the accepted Total Fog baseline
on the affected workload. The inherited binary retains historical comparisons.
Preserve visibility correctness while improving measured hot paths.
Windows and Mac results are separate evidence. A clean startup and unit tests do
not establish runtime performance or compatibility with all mods.

See COVERAGE.md for remaining acceptance and VALIDATION.md for retained evidence.
