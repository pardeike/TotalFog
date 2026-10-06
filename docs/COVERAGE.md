# Visibility coverage

This inventory separates implemented contracts, observed gameplay, and cases
that still need acceptance. Loading all DLCs does not prove every DLC scenario.

| Responsibility | Completed evidence | Remaining acceptance |
|---|---|---|
| Coverage/discovery | Sparse factions, checked counters, overlapping/removing sources, discovery persistence, rectangular bounds in independent tests | Full rectangular-map live scenario |
| Field of view | Newly authored symmetric caster; reciprocal floor sight, radius/edges, wall occlusion, unchanged blocker arrays, zero warm allocations in .NET tests | Dense multi-source end-to-end profiling and reliable native allocation evidence |
| Sources | Rewritten movement/map/faction ownership and deadline logic; live observer sight transitions and 250-tick blocker rotation/despawn; native owned zero-range wall supplies no sight | Dedicated turret power/fuel, camera console, animals, weather/night, sleeping/downed, door/tree scenarios |
| Rendering | Current-sight culling uses the native drawn pawn cell and preserves registration; earlier 900-tick fleshbeast test passes | Recheck lean build in the original save and fresh map. Static objects use native drawing after observation; exact last-observed appearance was removed for performance. The flyer/grid mutation error is fixed in native Mac reloads; Windows confirmation and broader custom renderers remain open. |
| UI information | Audited virtual overlay, tooltip, mouseover and both pawn-label hooks; remembered-object/proxy gating; native unseen-cell readouts/window decisions/beauty sampling and colony bypass; roof/fertility/terrain methods across discovery states | Direct pawn-label, wildlife and designator interaction acceptance; room-wide aggregates and topology |
| Simulation | Vanilla failed hit checks remain failed; current sight evaluated without a stale per-tick cache; hidden-item reservation/hauling guards retained | Dedicated targeting/reservation/hauling scenarios |
| Notifications | Source-linked observability/queue/policy tests; native health/global/visible and hidden threat letters obey the boundary; original payloads survive save/load and archive once after reveal. Optional Silent Raids passes actual raid/manhunter off/on arrival controls and native failure/exception parameter restoration | Multi-map targets, delayed sounds/slowdown in live scenarios; modded silent arrivals and natural combat playback with the new option |
| Audio | Actual danger-music getter toggles correctly; real Unity loops with cell and object sources mute, muffle, restore, and follow sight without ending or compounding volume | Physical listening and additional mod-defined sound classes |
| Content | Late-created definition receives one component per instance; custom non-pawn drawable and 3x2 rare-ticking blocker exercised live | Named optional mod installations and custom drawers outside the tested contracts |
| Persistence | Existing XML-facing names and exploration/object observation save keys retained; notification payloads round-trip | Recheck lean save/reload, including saves made by earlier preliminary builds. No drawing archives are written; large colonies, multiple maps and long-session soak remain open. |
| Performance | Sparse indices, batched invalidation, reused masks; specialized clear-row mask writer; native Mono comparisons with 512 identical masks/4,096 queries, including the real stress-map blockers and 53 colonist origins; zero warm kernel allocations in .NET tests; inline singleton listeners pass native lifecycle checks and reduce list-creation churn; the earlier matching six-process stress TPS floor passes at 485.147 versus 479.997; the corrected 1,000-zombie six-process comparison passes at 209.816 versus 195.486 (7.33% higher), with optional zombie instrumentation disabled and 999..1,000 living zombies retained | The earlier cold shrinking-horde failure remains retained; native cause tracing identifies initial-grace cleanup as its main population confound. The corrected fixture removes that benchmark confound without changing production defaults. Matching camera/zoom, public engine, both mod bytes, settings/configuration and warmup are enforced. A separate corrected work diagnostic services all visible/priority zombies and executes 14.4% more actual CustomTicks per second; its instrumentation remains distinct from the passing floor. A paused dispatch ablation measures about 0.003 ms added per filter pass. Simulation randomness, frame tails and controlled spikes and broader maps/settings/mod loadouts remain open. Measure combat point-query repetition, blocker-change duplicate casts, hearing population scaling, hidden-sound work and repeated GUI count queries separately. Reliable native allocations, broader FPS/memory/scaling evidence and Windows remain open; raw kernel timing cannot establish acceptance |

Optional integrations are isolated from the core. Their contracts were inspected,
but Minimap and Interaction Bubbles have not been accepted in a live loadout.

Zombieland's standalone fallback passes a fresh Steam/all-DLC profile with Total
Fog disabled. All four optional delegates remain null. Native contamination UI,
close/distant drawing smoke, 179 Normal-speed ticks and an in-process save/reload
pass on the unchanged delivered gameplay DLL. This closes the bounded standalone
startup/fallback check, not Zombieland's broader mechanics or overlay performance
without Total Fog. See `docs/VALIDATION.md` for the exact evidence and limits.

Mortal's baseline `Basics_QOL_Only.rml`, received on 2026-10-06, contains 33 actual
load-order entries, including all five DLCs, No Pause Challenge, Float Sub Menus,
Improved Map Search, Music Manager, multiple music packs and Vanilla Expanded
Framework. Its metadata contains 41 records; the eight extra records are not
active entries and must not be silently added to a compatibility fixture. The
expanded `More_Mods.rml`, subsequently received the same morning, has 55 actual
entries and 43 metadata records. It adds Dubs Bad Hygiene, further Vanilla
Expanded content, additional animal/content mods and custom storyteller mods.
Mortal may use only a subset. These named mods remain unverified in Total Fog's
combined runtime loadout. In his clarification,
Mortal does not plan to add a separate Silent Raids mod if fog already covers it.
The new independent option changes only native enemy-raid and manhunter arrival
cues. It adds no new incidents, raid strategies or fog-update work; it is off by
default and distinct from deferred hidden-event warnings and combat music.

## Full overhaul acceptance

The full release remains open until its required integration checks have actual
evidence. An implemented hook, clean startup, or all-DLC loadout does not close a
row. The inventory below also retains broader coverage gaps; those gaps do not
all block a preliminary tester build.

### Current delivery boundary

Prioritize the next useful Zombieland tester build over expanding the feature
matrix. Its gate is demonstrated fog leaks, regressions from changed code,
representative performance, startup/play/save-load smoke checks and exact
packaged bytes. Reuse accepted evidence when the gameplay code it covers is
unchanged; repeat affected scenarios rather than the complete suite after every
presentation fix. Confirm the contamination overlay's sight transitions and
refresh behavior before delivering its current fix.

The C0..C5 contamination groups and other unchanged Zombieland mechanics remain
follow-up coverage unless source evidence or a player report identifies a fog
interaction. Do not mark them accepted merely to shorten the list. Do not add
new gameplay machinery to close a speculative coverage gap. After delivering
the current Zombieland fix, finish actual CE combat/turret/mortar checks, then
Multiplayer's two-client determinism, settings, refresh and save/rejoin checks.
Broader loadouts and long-session soak remain release work, guided by concrete
failures and tester feedback.

| Requirement | Evidence required or remaining gap |
|---|---|
| Information outside sight | Source-linked and real-item scenarios prove fresh static objects stay hidden until observed and static-target messages require current sight. Ownership no longer reveals static objects or projectiles, and a native owned wall creates no sight. Live UI requires current sight; a native remembered-item/proxy scenario proves deselection, blocked reselection and zero live UI callbacks while unseen. Static objects use native rendering once observed, so appearance and terrain can change while unseen. Exact visual memory is intentionally excluded for performance. Player-observer/held-pawn exceptions and optional-mod inspection paths also remain to audit. |
| Base-game sources | Movement, sleep, downing, death, faction changes, doors, trees, lighting, weather, turrets, cameras, watcher occupancy, allied/prisoner/animal vision and overlapping sources. |
| Base-game information | Rendering and shadows, selection, labels, mouseover, inspection tabs, wildlife, alerts, threat indicators, targeting, hauling, reservations, designators, resource counts, beauty/room/environment readouts and sounds. |
| Royalty | Psycasts, invisibility, shields, jump/teleport and held/flying pawns. |
| Ideology | Rituals, roles, allied/slave/prisoner vision and event targets. |
| Biotech | Mechs, remote control, gestation/held pawns, children, pollution and shield effects. |
| Anomaly | Fleshbeast split/flight/landing, unnatural entities, invisibility, pit/multiple maps, held entities, anomalies with custom renderers and notifications. The existing fleshbeast scenario closes only its measured rendering contract. |
| Odyssey | Gravship landing/targeting, map transitions, outer-space maps, sensors and flying/held entities. Existing global reveal exceptions need review against the information requirement. |
| Overhaul mods | Exact installed 1.6 versions of Combat Extended, Vehicle Framework, Zombieland and relevant Vanilla Expanded content. Inspect overrides and direct drawing paths, then test separate and combined supported loadouts. |
| Zetrith's Multiplayer | **Unverified for Total Fog.** Establish deterministic simulation, synchronized gameplay settings/actions, fog refresh on both clients, combat targeting, save/rejoin and faction visibility. The reported upstream spreadsheet result does not close this target. See the evidence and acceptance checklist below. |
| Fog of War parity | Compare original source/settings and gameplay responsibilities. Preserve useful exploration, vision sources, targeting, notification/audio options and integrations while correcting leaks. |
| Performance | Profile representative small/large maps and colonies, stationary and moving sources, many factions, dense blockers and event bursts. The original mod is the minimum baseline. Compare load time, actual ticks per second, frame-time percentiles and allocations on matching saves/settings/hardware, as well as the casting kernel. Reject slower optional features and prove improvements without weakening sight correctness. |
| Settings | Grouped, readable page, truthful descriptions, sensible defaults, reset, live setting changes and save persistence. Privacy options must explain intentional information cues. |
| Translations | Use the language set of Andreas' most translated mod, as requested. The local inventory identifies Achtung with 12 languages: English, Simplified/Traditional Chinese, Dutch, French, German, Polish, Russian, Spanish, Latin American Spanish, Swedish and Turkish. Validate all player-visible keys, placeholders and actual settings layout. |
| Persistence and platform | Core-only and DLC loadouts, older saves, multiple maps, map removal/respawn, large-colony soak, exact deployed bytes and Windows render-error reproduction or confirmation. |

Current source research includes Combat Extended, Vehicle Framework and Vanilla
Expanded Framework. Source evidence is recorded separately from installed-version
and runtime acceptance.

- Installed Combat Extended 16.7.3.0 uses its own Vector3-source hit-check
  overload. An optional adapter now applies the shared targeting policy to
  successful results from that method and any declared overrides. Eight
  original source-linked regressions pass. Four additional turret/non-pawn
  cases now bring that adapter suite to twelve passing cases. The actual installed CE method passes native
  hidden/revealed target, blocked-ballistics and enemy-fog setting checks with
  a CE pistol shooter. Two CE fixture reloads pass 1,926 section regenerations
  without failures or grid mutations. The current e24d50df... gameplay DLL also
  passes an ordinary M1911 equip/ammunition/reload/attack sequence. Its active
  CE reload and queued attack survive in-process and fresh-process loads, then
  consume another seven rounds and deal further damage during 360 Normal ticks
  in each replay. The later 6527f4d8... candidate fixes a native powered CE
  mini-turret with zero sight that fired at unseen targets. Its shared engine
  turret contract now supplies a 34-cell sight radius for the native 48-cell
  weapon at the configured 0.7 modifier. Ordinary automatic firing/damage works
  nearby; an enemy at 42 cells receives no shots while hidden, including after
  reveal/hide and both in-process and fresh-process loads. Shared sight from a
  real security bell permits native firing at that same distant target.
  Native power loss removes the turret's coverage. The later c7205ee1...
  candidate also fixes overhead weapons extending crew sight to their weapon
  range: the same saved CE mortar scene drops from 455 cells to 39, rather than
  treating its 700-cell indirect range as vision. CE's native turret validator
  now filters automatic acquisition using the current observing faction;
  deliberate indirect-fire orders retain their native path. A healthy enemy
  60 cells away remains hidden and receives no automatic shots while the mortar
  is manned and loaded. A real security bell permits acquisition and firing at
  that same target. Hidden controls also pass in-process and fresh-process
  loads, and ordinary CE turret reload jobs consume real shells and refill the
  magazine. Nineteen source-linked CE cases and the eight native pawn hit-check
  controls pass. The later 0d19b952... candidate also filters the engine's
  manual-target candidate list: an actual hidden-pawn mortar click creates a
  cell order and fires, a revealed click retains the Pawn target and fires,
  and the native minimum-range rejection remains intact. Twelve Symbiant
  rendering/interaction cases and all 54 manual body-cell checks pass on that
  same candidate, including visible body targets with a hidden inspection core.
  The temporary native list is filtered in place without changing the thing
  grid, ordinary selection, cell fallback or simulation. The later beab8196...
  candidate fixes a mortar acquiring a visible Pawn and firing after losing
  sight during warmup. The matched native control now retains its shell while
  hidden, fires when sight returns, and still permits deliberate blind cell
  fire and native minimum-range rejection. A manned M240B remains idle while
  its healthy target is hidden. After three visible rounds, native CE converts
  the lost target to a last known cell and finishes its burst there; that
  fallback remains intact, with no subsequent hidden acquisition. Its real
  reload consumes exactly 30 rounds and both the magazine and remaining supply
  survive in-process and fresh-process loads. Twenty-nine source-linked CE
  cases pass. Other manned weapons, live suppressive Thing fallback,
  other weapon/arc combinations, combat-heavy CE performance and combined overhaul
  loadouts remain open. The same beab8196... gameplay bytes now also pass a
  native enemy mini-turret against a healthy drafted player target 42 cells
  away: enemy fog off permits ordinary firing, enabled 34-cell sight stops
  acquisition, and a real enemy-faction security bell restores firing. The
  player's visibility of its own Pawn does not grant enemy-faction sight.
  Power loss still stops shots despite valid shared sight. The hidden controls
  pass in-process and fresh-process loads; the restarted profile's unchanged
  default enemy-fog setting is explicitly re-enabled for that test. This closes
  the bounded automatic enemy mini-turret check, not every enemy weapon.
  A matched 350x350/410-pawn CE stress save now passes three alternating
  fresh-process pairs with the same beab8196... candidate: median 459.27 TPS
  versus 406.85 TPS, 12.9% higher. All measured ticks retain native fourth-speed
  multiplier 15, unforced and without UltraSpeedBoost. Absolute starting ticks,
  effective CE/fog settings, camera and bytes match; logs are clean. Timing
  varies and frame p95 is essentially unchanged. This closes that synthetic
  colony/animal fixture's performance floor, not combat-heavy or combined-loadout performance;
  a separate native M240B 90-degree arc control also passes. A visible target
  outside the arc consumes no ammo; turning the arc toward it allows native
  fire; removing shared sight stops fire even while geometry remains valid.
  The saved outside-visible and inside-hidden controls pass after restart.
  CE's arc option is restored to its original false value without disk writes.
  The fixture sets valid native configuration fields and invokes CE's adjustment
  callback; editor input remains unverified because the attempted mouse paths
  left the stored angle/span unchanged. No gameplay fix is needed for this case;
  this is not full CE acceptance. A later native M240B control proves CE's
  ongoing burst switches from a lost primary Pawn to a visible nearby Pawn,
  without a new turret acquisition. With both Pawns hidden, CE instead finishes
  at the primary's last known cell and never selects either hidden Pawn. Native
  projectile collisions remain intact. Loading the visible-alternate burst
  exposed Total Fog publishing 39-cell sight before engine lighting was ready,
  despite the ready pawn calculating 59 cells. The 2504105d... candidate runs
  the engine's sky/glow updates once before initial sight publication. The same
  fresh-process save immediately publishes 59 cells and completes its six
  remaining shots at the visible alternate; the primary stays excluded.
  The hidden-alternate save also prevents either hidden acquisition after
  restart. Its native saved cell burst does not resume, so this is not proof
  of cell-burst persistence. Seventeen receipt-backed controls pass under
  `artifacts/ce-retarget-native`. Off-current-map, darkness and lamp-only load
  controls and the new candidate's broader package/performance gates remain
  open. Its completed six-process CE stress comparison is rejected at 481.43
  versus 491.73 median TPS (2.1% lower); the earlier beab8196... pass cannot
  close this candidate's floor. Raw failing reports and separate diagnostic
  traces remain retained. No performance acceptance is inferred from those
  instrumented traces. The 450329a0... candidate then avoids constructing a
  footprint for single-cell visibility queries, without caches or changed
  update deadlines. All 291 tests pass and its own complete six-process CE
  stress comparison meets the median floor at 511.55 versus 489.75 TPS (4.45%
  higher). One pair is slower; this is bounded fixture evidence, not a uniform
  gain. This current candidate also repeats the saved visible-alternate burst
  and hidden-target controls with eight receipt assertions. Its exact
  450329a0... / 56daa1f2... pair passes the six-process Zombieland overlay floor
  at 219.28 versus 203.98 TPS, 7.50% higher, with frame p95 worse at 81.27
  versus 76.89 ms. The current broad paired rendering/behavior suite and
  preliminary package guards pass. CE combat-heavy/combined-loadout and
  Windows performance remain open. Native evidence is in
  `artifacts/ce-turret-native`, `artifacts/ce-mortar-native`, `artifacts/ce-m240-native`,
  `artifacts/ce-enemy-turret-native`, `artifacts/ce-arc-native`, `artifacts/ce-retarget-native` and
  `artifacts/manual-target-symbiant`; current paired package evidence is in
  `artifacts/zombieland-current-package-gates.json`. Earlier scenario evidence
  retains its own exact gameplay DLL hashes.
  A subsequent current-byte native short-bow check covers real no-magazine
  stone-arrow inventory. CE's own attack order fires once at a visible steel
  wall (20 to 19 arrows). Removing the real sight bell during the next native
  warmup cancels before ammunition preparation; 240 Normal ticks retain all
  19 arrows. A new hidden attack order also prepares/fires nothing. Revealing
  the wall resumes a successful native shot (19 to 18). Ten receipt assertions
  pass across 844 reported Normal playback ticks; the shooter remains still.
  No ammunition-loss defect is reproduced in this ordinary short-bow path.
  Other no-magazine verbs, mod-issued direct shots, live suppressive Thing
  fallback and no-magazine save/load remain open. The wall is not damaged by
  these two shots, so this is not damage acceptance. Cleanup restores the
  original range, removes owned objects/probes, reloads the base and stops the
  process; recognized logs are clean. See `artifacts/ce-ammo-native`.
  Current gameplay also passes thirteen suppressive-fire receipt assertions.
  An actual M240B burst begins with one visible shot, then completes nine native
  blind-cell fallbacks after the sight bell is removed: CE converts the target
  to its last known cell, both native/final results remain true, and magazine
  179 drops to 170 without hidden-Pawn reacquisition. Four separate readonly
  loaded-guard contracts reject hidden Thing success and preserve visible
  success/native failure. These supplied inputs do not establish a naturally
  executed still-Thing fallback. Ordinary automatic and manual forced-target
  warmups cancel before a shot while hidden; the stock mortar has no suppressive
  aim mode. Native mode toggle, bounded trace removal, owned cleanup, unchanged
  base reload and clean logs pass. See `artifacts/ce-suppressive-native`.
  The new isolated CE/Zombieland profile reproduces a cross-mod shot-line bug
  on 450329a0...: CE/Zombieland choose a visible logical body cell, but Total Fog
  rejects their successful result because the Pawn root is hidden. Candidate
  2f15cad0... gates the selected native shoot-line destination, including Thing
  fallbacks, with the same single sight query and no new API/cache/search.
  Four regression cases cover matching and differing root/destination sight;
  both disagreement cases fail before the fix and pass afterward. All 295 tests
  pass. Five actual combined native
  shot-line states pass, and Normal playback damages shared health 3995 to 3975
  with the host's injury sum and sight bell unchanged. The raider later chooses
  a native Steal job, so this is not sustained-combat/save-rejoin acceptance.
  Combined saved M240B visible/hidden retarget controls and six actual blind-cell
  fallbacks also pass. Exact pairs/receipts and clean log are in
  `artifacts/ce-zombieland-native`. Combined turret logical-cell acquisition,
  long fights, combat-heavy performance and Windows remain open. Earlier
  candidate performance/package gates retain their own gameplay hashes. The
  completed current-byte six-process standalone CE comparison misses the floor:
  456.01 versus 460.41 median TPS (0.96% lower), tick elapsed 1.57487 versus
  1.56362 ms and frame p95 70.27 versus 68.64 ms. Matching identity, actual
  fourth-speed multiplier 15 and clean logs pass. This failed measurement is
  retained; 2f15cad0... is not performance-accepted or a new feedback package.
  Its subsequent instrumented CE tick captures identify ComputeMask and
  ApplyDifference as measured fog costs, about 0.07073 and 0.02710 ms per tick
  respectively. The CE guards resolve but produce no snapshot rows. These
  forced/debug multiplier-150 captures are diagnostic, not player-speed or
  before/after acceptance. DPA cleanup, stopped-process state, its removal from
  the CE profile and unchanged installed DLL pair are verified. The performance
  floor remains open until an evidence-backed change passes a matched native
  comparison. See `artifacts/ce-dpa-logical-cell-*.json`.
  A following row-boundary candidate passes 296 tests, including 18,432
  exhaustive small masks against unchanged callback geometry. Its first
  original/candidate native pair completes, but the next process crashes in
  startup reference resolution. The incomplete comparison is retained in
  `artifacts/ce-row-center-startup-failure`; no native performance acceptance
  or new feedback ZIP follows from this source change.
  A later fresh startup/load succeeds and a same-process native comparison
  verifies 512 masks/4,096 queries plus each timed origin's full footprint.
  Compared with exact pre-change source 846e7c4, paused casting cost drops
  7.77% on the stress map's blockers/53 colonist positions and 8.80% on open
  terrain. This establishes the casting improvement, not whole-game TPS or
  native allocation behavior. DPA is absent, logs are clean, and temporary
  comparison code is removed before further acceptance measurements.
  The completed normal-view six-process comparison passes its median at
  421.133 versus 398.386 TPS, but large sample variation prevents a stable
  causal gain claim. A deliberately small wide-view sample then covers a CE
  colony and Zombieland contamination in one original/candidate pair each.
  Wide CE covers 71,050 cells and measures 402.053 versus 411.314 TPS, a 2.25%
  decrease with a worse frame tail. It remains a failed spot check. The older
  Zombieland fixture covers 50,750 cells but loses most zombies and spends
  most ticks at native multiplier 1, so its 2.28% higher TPS cannot establish
  sustained fourth-speed or equal-work acceptance. Paused wide overlay pixels
  show the intended current-sight clipping. These two pairs do not close the
  three-pair delivery gate, and there is no new feedback package. See the
  small wide-view section in VALIDATION.md and the retained comparison reports.
- Vanilla Expanded Framework's extended biosculpter draws held occupants directly
  through `PawnRenderer.RenderPawnAt`. Test the containing object and held-pawn
  visibility together rather than assuming map drawable culling covers both.
- Vehicle Framework supplies its own phased renderer; its native acceptance is
  still open. Zombieland's ordinary pawn, eight special types and direct-drawing
  spitter pass the native presentation transitions described below. A visible
  healer's beam/glow originally disclosed a hidden target in explored fog; the
  local paired Zombieland/Total Fog fix passes native draw-count and screenshot
  checks, with positive effects in sight and no effects outside sight. The
  original scene changed 58,049 pixels with the effect on; the fixed hidden-target
  effect-on/off images are identical. Native playback also confirms the original
  frame-aged records could fill the eight-record healing limit without expiring
  when the caster was culled. The local Zombieland fix moves aging/expiry to its
  existing simulation path. All nine controlled injury/healing opportunities pass
  with the healer and target hidden and off-camera; 615 healer CustomTick calls
  occur without healer draw calls. A scoped fixture-only prefix suppresses
  unrelated zero-threat zombie cleanup and is removed afterward. Rendering stays
  read-only, expired records do not draw, and paused ticks remain 3 to 3.
  These fixes require the updated Zombieland source as well as Total Fog's optional
  query; they do not establish compatibility for the currently published
  Zombieland DLL. Other active effects, attacks, emerging rubble, invalid/removed
  healing targets, reduced-tick combat, standalone Zombieland, broader integration
  and Windows acceptance remain open. The GlobalControlsUtility.DoDate readout
  now uses the optional query at the presentation boundary. The extended native
  movement fixture first fails on the preceding Zombieland build: it displays
  all 13 zombies and highlights all 13 even when none are visible. The fixed
  build displays/highlights exactly the seven visible zombies in each partially
  visible phase and none in the all-hidden phase. The simulation count remains
  13 and ticks remain 3 to 3. Queued spawning is excluded from the sight readout;
  the population count used for spawning is unchanged. The counter background
  no longer exposes the map-wide ticking fraction under Total Fog.
  A paused 1,000-zombie query-only benchmark measures a median 0.2101 ms for
  the uncached sight count versus 0.0143 ms for the unchanged population query.
  Whole-game performance is assessed separately. The spitter spawn letter has
  a cell/map target and passes through the existing event-letter setting; its
  camera siren has no positional target. The updated Zombieland queries the
  actual event cell before camera playback; twelve native spitter/wave audio
  controls pass. Hidden-spitter letter acceptance remains outstanding.

## Zombieland feature audit

The 2026-10-05 audit covers all 22 groups in Zombieland's advisory
`coverage/ZL_COVERAGE_INDEX.tsv`, reconciled against current source owners and
direct drawing, UI, sound and event routes. The index contains two references
to the removed `ZombieSymbiantRenderer.cs`; the current implementation is in
`ZombieSymbiant.cs`. Existing standalone Zombieland results do not establish
paired Total Fog compatibility. This is a complete feature inventory with
explicit acceptance gaps, not a claim that every feature has passed.

| Feature group | Current source and fog-sensitive behavior | Paired evidence and outstanding checks |
| --- | --- | --- |
| A.STARTUP.LOAD | Main, Assets, Patches/Startup, CustomPawnState and ZombieRenderCompat; package/load folders and native resources | Exact paired Mac cold load and reload pass. Windows and optional combined loadouts remain open. |
| A.DEFS.COLDLOAD | ZombieDefs, Assets, GraphicToolbox and graphics/material/atlas owners | Current paired load reports no recognized definition/configuration errors. Per-effect resources and Windows shader support need separate acceptance. |
| B.SETTINGS.WORLD_DEFAULTS | ZombieSettings, settings dialogs, colonist toggles and defense controls | Effective benchmark settings are recorded. Paired live changes/reset, source rules, audio/privacy interactions and persistence remain open. |
| B.SETTINGS.KEYFRAMES | ZombieSettings and DialogTimeHeader; day-based interpolation | Paired interpolation, transitions and save/reload remain open. Settings must not depend on local camera/frame cadence. |
| C.CORE.ZOMBIE_LOOP | Zombie, generator, state handler, Stumble, pathing/pheromone, TickManager and corpse owners | Basic pawn presentation/reload passes. The corrected 1,000-zombie TPS floor passes against the original; the earlier cold shrinking-horde failure is retained with its diagnosed grace-period confound. Spawn modes, emerging rubble, fire, bleeding, eating, death/despawn, budgeted combat and long play remain open. Hidden simulation must continue. |
| C.SPECIAL.ZOMBIES: body rendering | Normal, SuicideBomber, ToxicSplasher, TankyOperator, Miner, Electrifier, Albino, DarkSlimer, Healer and custom spitter renderer | All ten native hidden/visible/hidden presentation rows pass before/after reload. This does not accept their active attacks/effects. |
| C.SPECIAL.ZOMBIES: active effects | BombVest, TarSmoke/TarSlime, OverlayDrawer, AlbinoScreamVomit, sabotage, paralysis and AnomalySkipAbduction | Healer endpoint privacy and hidden/off-camera healing pass with the scoped Zombieland fix. Actual TarSmoke DrawAt gives 0/positive/0/positive calls across sight transitions, with one unchanged registration; its natural expiry runs through 2,300 actual game ticks while hidden and removes the registration. Actual dark-slimer damage produces 13 TarSmoke in all six audio states; native ranged targeting/aim chance is blocked for zombie and human targets. Tank armor, electric absorption, tar and toxic action sounds pass 24 native controls without suppressing damage/effect production. Six 128-hit electric bursts retain zero hidden markers or at most eight visible markers, and both electric effect helpers preserve the outer random stream. All 23 controlled Albino sabotage contracts pass, including scream/vomit lifecycle, paralysis cleanup, safety-path revalidation and door/equipment helpers. Albino native AI scream playback passes 21 controls with held waiting victims, including meshes/bubble across sight changes, sound, victim effects, damage-job continuation and natural expiry. Gas spread/boundary pixels, natural melee/shot execution, bomb/explosion, electrical arcs, broader victim combat and equipment/door sabotage presentation, abduction and remaining effect lifetimes stay open. |
| C.SPECIAL.ZOMBIES: spitter/balls | ZombieSpitter, JobDriver_Spitter and ZombieBall; custom mesh, projectile flight/impact, explosion and newly spawned zombies | Spitter body calls and twelve spawn-siren controls pass. The paired real job-driver firing path produces one ball. Native flight moves/rotates, impacts without a direct Impact call and creates a live zombie. Six remote-corridor hidden/visible/hidden frame controls pass for the moving ball and newly landed zombie, with draw counts 0/13/0 and one registration; the expired ball deregisters. A reproduced hidden impact-flash leak is fixed: five real flights preserve zombie spawning and positional impact audio, with zero hidden flash submissions and 31 visible submissions. Separate native Smoke/ExplosionFlash controls accept sight loss/reveal, hidden movement and natural expiry under default rendering and a forced native-worker path. A native in-flight save and fresh-process reload also pass: exact flight fields match the uninterrupted reference at the same game tick, discovery is preserved, both flights impact naturally at tick 85, and the reloaded ball/new zombie repeat six sight/registration controls. These controls use staged native tick advances and paused rendered frames. Blast pixels, other fleck families, shot target-selection rules, ordinary budgeted playback and the hidden-spitter letter remain open. |
| C.SPECIAL.ZOMBIES: explosion camera | Native DamageWorker.ExplosionStart, suicide-bomb producer and toxic/electrical definitions | Eight native hidden camera-shake leaks reproduce before the fix. The common sight gate then passes all sixteen hidden/visible/hidden-again/colony-bypass rows, retaining native shake magnitudes in positive controls, heat, wall damage and completed blast-cell processing. This is staged producer proof on TF `6f60493b...` / ZL `cb27dbd1...`; natural attack AI, particle pixels and third-party worker overrides remain open. The updated six-process fixture floor passes at 311.3350 versus 291.3977 TPS, 6.84% higher; frame p95 is 71.2763 / 70.2711 ms. The combined native/package gates pass, and Mortal receives separate complete ZIPs in post `[private message reference]`; remote bytes match before the superseded own tester post is deleted. See `artifacts/explosion-camera-{before,after}.json`, `artifacts/comparison-explosion-camera-player-1000.json` and the delivery receipts in VALIDATION.md. |
| C.CORE/F.RENDERING: wall crossing | ZombieStateHandler.CheckWallPushing/WallPushing and Zombie.DrawPos; interpolated drawing, landing teleport/roof removal, bump mote, positional sound and warning letter | Six native render samples at progress 0/.5/.9 pass the actual drawn-cell gate, including opposite logical/drawn sight states. A real Stumble crossing lands at step 102, preserves the wall and removes the destination roof with fixture cleanup temporarily suppressed. All three production bump helpers now pass hidden/visible/rotation/reveal/native-expiry controls with one retained registration. Nine real Stumble starts under normal playback also accept native wall sound mute/hearing options, delayed home-wall warnings and exactly-once reveal, separate sound/warning options, outside-home walls and disabled delay. These use a scoped minimum of four with no fake horde counts. The updated pair accepts six complete normal crossings at the default minimum of eighteen with fourteen real staged supporters registered through production ExecuteMove, without invented counts. All 606 wall ticks preserve read purity and simulation-owned facing after fixing Zombie.DrawPos; near/far camera, hidden/visible, colony bypass and extra-read controls retain landing, wall/roof and registration behavior. The updated pair repeats all nine cue and 28 mote controls. Unassisted crowd formation, bump threshold initiation/pixels, off-current-map crossing, climbing save/reload and combat stress remain open. |
| F.RENDERING: attached block mote | Tools.CastBlockBubble and the smart-melee branch in Patches; native MoteAttached follows its defender and offset | Production CastBlockBubble plus the actual attachment update gives zero/positive/zero native Mote.DrawAt calls through hidden/visible/hidden at positive alpha. Native cull and drawn cells match. The subsequent lifecycle probe uses native aging and a real sprinting Goto job for sixteen normal ticks while hidden. The block follows its defender, retains one registration through sight transitions, reappears with positive alpha and naturally expires/deregisters after a full lifetime window. All four block/bump families pass 24 presentation/expiry and four motion rows. An explicitly selected native ZombieBite attack now exercises the real smart-melee branch with a capable level-twenty defender and scoped safeMeleeLimit two: injury remains unchanged, the actual attack creates the attached bubble, hidden mute produces no new Smash samples, and the full 28 lifecycle/motion controls pass. The subsequent seven-control normal-playback matrix accepts automatic Stumble-to-AttackMelee jobs and verb selection: 21 attacks, 16 real parries, hidden/visible/off-camera bubble drawing, muted or audible positional Smash sound, per-shot hearing gain and natural expiry of all 16 observed bubbles. A separate smart-melee-disabled hidden control runs three original attacks and produces actual injury without parries. This is controlled normal-zombie/NPC combat; other zombie types, crowd combat, cross-boundary geometry/pixels, reload and Windows remain open. |
| C.SPECIAL.SYMBIANT_INFESTATION | ZombieSymbiant/Combat, host/health patches, feeding and severing jobs/recipe; logical slime cells, direct metaball meshes and selection core | Both mature partial-body defects are fixed in ten native Mac rendering cases with paired pixels: hidden root/vanilla-fogged root with a visible body end, clipped rotating core, all-hidden/off-camera rejection, forced fallback cue and rebuilding released resources. Registration remains one. Root/host UI and simulation are not broadened. The subsequent 12-case native/pixel check also accepts core/root sight disagreements and a staged half-progress handoff: current-core inspection and 54 per-cell manual-target checks pass. The updated native policy checks also accept hover eligibility and tooltip anchors at the visible core while rejecting labels at a hidden root; natural tooltip timing/lifetime remains open. The final pair repeats all 12 cases/54 targets after two large-body CPU fixes; forty paused dense/sparse 400/4,000-cell rows cover cold/warm hidden resources, partial sight, off-camera rejection and clipped/unclipped controls. The existing GPU shader is preserved. Extreme sparse clipping still adds measurable frame cost; these diagnostics do not establish TPS. The existing feeding contract accepts non-root core job routing, organic corpse grouping/exclusions, exclusive reservations and direct six/four-cell growth. Three subsequent live right-click jobs carry human/fresh-rat/rotten-rat corpses and finish after 633 normal playback ticks with exact six/four/two-cell growth and empty hands. A subsequent human feed saves during hauling, retains the exact job/corpse/link through in-process and fresh-process load, then finishes with six-cell growth and empty hands after another 179 Normal ticks. Native benefit, host-effect and direct severance-recipe contracts now pass, as does the damage/map-lifecycle matrix after its indoor-only health oracle was corrected; exterior cells add no health. These are controlled helper/transition checks, not full pregnancy or ingredient-hauling/surgery jobs. A paused two-map save/load also retains the exact host bond and dormant state in process and after a fresh restart; controlled return restores immunity and surgery, with cleanup verified. This does not establish acquired-benefit reactivation or active jobs. The logical-cell combat contract now passes with a valid whole-map corridor, and ordinary ranged/melee assault AI deals shared damage across 3,000 Normal ticks and active-fight save/reload without new host injury. A following current-sight menu matrix exposes hidden core/corpse choices and fixes them in Zombieland through the existing visibility queries. Four native rows and one 212-tick Normal feed job pass on the new pair; actual sight is two under a scoped base range of three. The exact new pair subsequently repeats twelve native pixel cases, 54 manual-target checks, forty paused render-cost rows and five global ambience controls. Its matched six-process 1,000-zombie ordinary fourth-speed comparison measures 313.398 versus 289.930 TPS, 8.095% higher, with native logs clean. Broader maps, spikes and long sessions remain open. A following real faction-sight matrix fixes an optional enemy-fog bypass: five ranged and five melee paused acquisition/cell checks pass, including a visible body cell with a hidden root and immediate sight loss. Actual security-bell sight now removes that observer confound: native melee and ranged combat deal shared damage with one of five body cells visible to the player and the root hidden. Corrected fixtures pass both active-fight in-process save/reload/resume cases, retaining exact health, body, host, bell and attack job, then dealing more damage over another 601 Normal ticks each. Native logs are clean and the unchanged base is restored. These checks use the already delivered gameplay pair; only diagnostics changed. Its latest six-process 1,000-zombie native fourth-speed comparison measures 312.894 versus 288.506 TPS, 8.453% higher, with refreshed performance/package gates accepted. Both prepared partial-fog fights also survive separate full game restarts with exact saved health/body/host/job/bell state, then deal more shared damage during ten seconds of resumed Normal combat while player sight remains one of five cells and the root hidden. Native logs are clean and prepared saves unchanged. Full core travel, broader weapons/sight ranges, moving-core feeding, other feed save phases, full surgery, notification privacy/sounds, full GPU fallback body and Windows remain open. |
| D.PATHING.DOOR_AVOIDANCE | ZombieAvoider, ZombieAreaManager, WorkGivers and path/door patches | Danger grids can contain unseen zombies. Native danger-area warning leaks are reproduced and fixed in Zombieland's renderer. Seven staged-cache hidden/visible/hidden, mixed-colonist and Symbiant root/core-disagreement controls pass without advancing ticks or deleting simulation entries. Sight is checked at the warning's actual grid location. Natural cache classification, other zombie types, hover/click pixels, door refusal and broader avoidance/work overlays remain open. |
| E.HOSTILITY.TARGETING | Patches_Hostility, ZombieAttackTargetIndex, ZombieAreaManager and vanilla/modded verbs | Ordinary fog policy has core/CE evidence, not Zombieland-wide acceptance. Test colonists, enemies, animals, mechs, turrets, forced jobs, special zombie states and Symbiant logical-cell targeting under the paired loadout. |
| F.RENDERING.VISUALS: shared paths | CustomPawnState, ZombieRenderCompat, leaner/flasher, corpse/spitter-corpse, graphic/overlay owners and VictimHead portraits | Pawn registration/phase/overlay transitions pass. Labels/portraits, roping lines, corpse/downed/head variants, rubble, shadows and direct overlays still need active-state/native-pixel cases. |
| G.CONTAMINATION.C0_STATE | ContaminationManager/Serializer, storage ranges, needs/hediffs/mental state | Audit hidden simulation independently from presentation. Paired state conservation and save/reload remain open; drawing must not own updates. |
| G.CONTAMINATION.C1_STACKS | Stack/container/carry/ingestion patches | Paired real transfers remain open. Ensure fog-related selection/reservation gates do not break conservation or legitimate owned inventory. |
| G.CONTAMINATION.C2_BUILD_REPLACE | Construction/minify/install/smoothing patches | Paired replacement and serialization remain open. Reuse remembered-static policy for appearance, while current-sight UI must not expose hidden contamination changes. |
| G.CONTAMINATION.C3_WORLD_PRODUCTS | Filth, mineable, plant, incident and byproduct patches | Hidden products and conversions must simulate without publishing live values. Paired production/spawn/observation checks remain open. |
| G.CONTAMINATION.C4_ENVIRONMENT | Pollution/snow/sand/fire/cleanup and manager paths | Paired DLC environment interactions, hidden overlays and conservation remain open. |
| G.CONTAMINATION.C5_PLAYER_EFFECTS | Rest/comfort and breakdown, force-rest, hallucination, hoarding, mimic and sleepwalk jobs | Preserve reportable owned-pawn health information. Check remote targets, bubbles, hallucination effects and jobs; paired gameplay/reload acceptance remains open. |
| G.CONTAMINATION.C6_UI_QUEST | ContaminationPatches, icon/stats/mouseover/inspect overlays and decontamination quest | Direct UI patches need current-sight checks in their actual contexts, including remembered things and contained items. Owned-colonist quest information must remain usable. Paired native acceptance is open. |
| H.QUESTS.INCIDENTS | Incidents, weather, ZombieFreeEvent, TickManager, alerts and dynamic forecasts | Hidden targeted letters/events, spawn-on-vanilla-unfogging and sudden-room zombies remain open. Public global weather/forecast information is a separate policy from hidden pawn activity. Do not suppress it solely by letter category. |
| I.INFECTION.MEDICAL | Bites/infection/tending, corpse conversion, serum/cure, double tap, rope and filters | Owned-colonist disease information remains reportable. Hidden non-player conversion/motes, corpse inspection, work/reservation, treatment and reload need paired evidence. |
| J.BUILDINGS.ITEMS | Thumper, shocker/effecter, chainsaw, power/fuel/repair/placement and gizmo owners | Test active rings/arcs, positional loops/one-shots, shocker room targets, fuel/breakage and current UI outside sight. Remembered native appearance is an accepted performance tradeoff; live state disclosure is not. Paired acceptance remains open. |
| K.SOCIAL.SELECTION | Thoughts, former-colonist exceptions, selection, corpse alerts and damage memory | Root selection/deselection passes for staged zombies/Symbiant. Social/portrait/alert and former-colonist behavior still need paired tests. Native zombie HUD count/hover now matches only visible zombies; spawning population is unchanged. |
| M.UNINSTALL.HYGIENE | ZombieRemover, uninstall dialog and serializers | Existing standalone no-mod-copy proof is not paired proof. A copy made with both mods must preserve Total Fog components/discovery, remove Zombieland references and leave the source save untouched. Still open. |

Audit artifacts are `zombieland-fog-feature-source-inventory.json` and
`zombieland-presentation-feature-routes.txt`. Native evidence is recorded in
VALIDATION.md. Prioritize the named block-mote/wall/Symbiant gaps, active effects,
targeting, privacy/audio, save/reload and frame pacing for Mortal's session.
Keep Windows, CE, Vehicle Framework, VEF and Multiplayer gates open separately.

Electric/tank camera ambient loops now have paired native audio acceptance.
The original mixers bypassed positional filtering and played hidden sources at
Unity volumes 0.1/0.4 even with mute enabled. The updated optional audibility API
reuses existing settings and listener policy. Sixteen native rows cover both
loops through mute/hearing changes, sight loss/reveal and mixed near-hidden/
far-visible sources. Hidden-only Unity volume is zero, visible sources remain
audible, existing loops are not ended, and ticks remain unchanged. The following
siren fix also passes twelve native spitter/wave controls and repeats all sixteen
ambient rows on its exact pair. Creepy night ambience intentionally remains a
global environmental cue, like weather, and is excluded from fog filtering at
Andreas's direction. Its shared night value also drives wandering and must stay
unchanged. Five native controls on the delivered gameplay pair keep actual
creepy-loop Unity volume at 0.3 and audibility factor at one across unfiltered,
mute, hearing, both filters and restored modes; the shared dictionary stays
unchanged. The same five controls repeat on the current 9a76ddd3... / ff207b8c... gameplay pair, again at Unity volume 0.3 and audibility one with no shared night/wandering change. This accepts the filter exemption, not natural night-cycle timing.
Tank armor, electric absorption, tar-pop and toxic-splash action
one-shots now pass the 24 native controls documented in VALIDATION.md. Other
one-shots and natural source lifecycle checks remain open.
See VALIDATION.md and audio artifacts.

## Blocker bursts and pending refreshes

The native 350x350 stress fixture has 6,531 registered objects with sight
components, but only 53 active sight sources. A repeated 53-cell blocker burst
previously scanned the complete roster per changed cell. Filtering that roster
reduces median enqueueing from 0.6956 to 0.0241 ms in seven paused-fixture/live-
playback samples on each build. The 53 actual casts still take about 3.3 ms;
this is not a whole-game speedup or bounded-spike acceptance.

The same reusable contract fails the prior binary's synchronous-refresh
cancellation, disabled-source removal, despawn unlinking and callback-preservation
cases, then passes all nine cases on the new binary. The filtered list matches
every positive-radius registered source, with no duplicates, before/after
faction changes and respawn. All burst samples remain stationary, perform exactly
53 casts and empty pending work; the named save is reloaded afterward. Native
logs are clean. True map transfer, dedicated zero-range powered building
transitions, repeated event saturation and the quota/deadline scheduler remain
open. Multiplayer compatibility remains unverified.

## Zetrith's Multiplayer: unverified

The separate compatibility-check session supplied the following evidence, checked
on 2026-10-05. These are upstream community reports, not Total Fog runtime results.
The [Multiplayer team's compatibility spreadsheet](https://docs.google.com/spreadsheets/d/1jaDxV8F7bcz4E9zeIRmZGKuaX7d0kvWWq28aKckISaY/edit)
must be interpreted by exact Workshop ID:

| Mod / Workshop ID | Reported status | Evidence limit |
|---|---|---|
| Mlie's `(NWN) Real Fog of War (Continued)`, **3391128917** | 4, "Everything works"; comment "Somehow it works well."; last change **2026-04-30T23:27:45Z** | Does not establish testing of Total Fog's rewrite or the latest upstream October build. |
| Older NWN continuation, **2560931731** | 1; fog loads once but does not refresh | A different Workshop item; do not apply its report to 3391128917 or Total Fog. |
| Older non-NWN continuation, **2559045153** | 1 | A separate item, not the current NWN continuation. |

The separate session searched accessible RimWorld Discord server
214523379766525963 for the exact Workshop ID/name and fog/FoW plus
multiplayer/MP/desync. It found no direct Multiplayer compatibility report.
The [August 23, 2026 "somewhat compatible" message](https://discord.com/channels/214523379766525963/214523406727512065/1541029925669052436)
concerns **CAI 5000** in context, not Multiplayer. The Multiplayer Discord was
not in the accessible account's server list. Absence of a report in this bounded
search is neither compatibility nor incompatibility evidence.

### Outstanding acceptance checks

Use the exact public 1.6 game, Total Fog DLL, Multiplayer version, dependencies,
load order and gameplay settings on two independently connected clients. Record
those versions/hashes and desync diagnostics with each result. Single-player
startup with Multiplayer enabled cannot close the target. The Multiplayer team's
[determinism patches](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/Determinism.cs)
distinguish interface work from ticking/commands and normalize simulation drawing
positions; apply that distinction when auditing our calls.

| Area | Required checks and pass condition |
|---|---|
| Deterministic simulation | Replay identical commands/ticks on both clients while sources move, blockers change, pawns sleep/down/die and events fire. Compare per-faction coverage, discovery, observation flags and deferred queues at matching map ticks; require matching serialized gameplay state and no desync reports. Change camera, selection, hover, open windows and frame rate on one client only; these must not alter simulation, RNG or thing IDs. Audit hearing-cue spawning and native calls for indirect RNG/state changes even though Total Fog has no direct random-number calls. |
| Synchronized settings/actions | Inventory gameplay-affecting ranges, source/faction rules, tree blocking, enemy targeting, discovery/reveal and notification queue policies. Verify initialization from the host and either synchronized in-game changes/reset at the same tick or an explicit supported restriction. Change each from host and client, including while paused; reject differing gameplay settings. Review display/audio settings separately before allowing local changes. Current `applySettings` recasts all loaded maps, so a UI-only option cannot be assumed harmless merely from its label. |
| Fog refresh on both clients | Move/teleport/transport sources, open/close/build/destroy blockers and overlap/remove observers. Check reveal and loss of sight on both clients, including a client watching another map at a different camera position and supported independent map speeds. No one-time initial fog, stale cells or visibility tied to the rendering client's activity. |
| Combat targeting | Order player and AI attacks into hidden/revealed cells, move the boundary, change blockers and toggle enemy fog through the supported synchronized path. Both clients must reach the same hit/job/shot result; native failed hits stay failed. Repeat with the exact supported CE loadout, manned/unmanned turrets and no-LOS weapons. |
| Save/rejoin | Save and reload the Multiplayer session, join late, disconnect/rejoin and exercise supported resync/replay. Coverage must reconstruct without duplicate sources; discovery/observation and original deferred notification payloads must survive and replay once. Repeat during movement/events and across map removal or transfers. |
| Faction visibility | Test shared-colony clients plus any separate-faction mode supported by the chosen Multiplayer version. Exercise allies, prisoners, animals, faction changes, held/flying pawns and transfers. Audit assumptions around `Faction.OfPlayer` and faction keys so one client/faction cannot reveal another's hidden activity or diverge in targeting. |

Current source review identifies the immediate audit points in
`FogSettings.applySettings`, `MapVisibility` source/discovery updates,
`CompSightSource` deadlines/hearing, `CompVisibility` observation state,
`DeferredNotifications`, and the shared vanilla/CE targeting policy. These are
review targets, not demonstrated Multiplayer defects. Core-kernel tests and
the upstream spreadsheet rating cannot substitute for this two-client acceptance.

Mortal supplied the original combat save and identified intermittent Fingerspike
deaths as the trigger. Substituting Total Fog into that save on the installed Mac
engine now passes 900 native ticks, including two Fingerspike deaths, without
render errors, duplicate registration or pawn drawing outside current sight.
Walking-pawn culling now uses the engine's interpolated drawing cell. The exact
Windows null-render-node error still needs Windows reproduction or confirmation;
its absence on this Mac does not establish that it is fixed. Alert examples will
follow his testing of a working build.


## Current private feedback

Mortal's planned Total Fog plus Zombieland session on Saturday 2026-10-31 is the
next named integration priority, ahead of further CE acceptance. This combined
loadout has targeted presentation evidence, but overall compatibility remains
unverified. Andreas authorizes a scoped Zombieland fix where a
reproduced integration problem needs it. Preserve the separate CE evidence and
all unrelated Zombieland work. The next fixture should exercise hidden/visible
ordinary and special zombies, their custom GUI/render paths, spawn/despawn and
large attacks, targeting, labels/selection, sound/music/alerts, and save/reload.
Measure realistic player speeds and controlled load spikes alongside bounded
fog-update age. Zombieland already documents 100/500/1000/2000-zombie fixtures and
calibrated slow-host/spike evidence; reuse supported instrumentation where the
combined loadout permits it. Reading those existing results does not establish
Total Fog compatibility. The paired review has since produced scoped healer,
save-transition, zombie readout and Symbiant cell-clipping fixes in Zombieland; those require its updated
local DLL, rather than proving the current published build compatible.
If a reproduced integration problem cannot be addressed through the normal
engine hooks, Andreas permits a small optional integration mechanism patterned
after Camera+'s customization support. Inspect that existing pattern and expose
only the demonstrated contract. The existing optional read-only
`TotalFog.Visibility.IsVisible(Thing)` query supports the healer endpoint and
HUD/hover fixes. The reproduced multi-cell Symbiant defects justify the separate
draw-only RegisterRenderer/cell-sight contract documented in ARCHITECTURE.md.
It does not broaden root/host information or alter simulation. No broader
integration framework is justified by this evidence.

On 2026-10-05 the isolated Steam loadout (Core/all five DLCs, Harmony,
RimBridgeServer, Zombieland 5.6.3.0 and Total Fog) starts without mod configuration
warnings. Main/companion deployment bytes match each repository's quiet build.
The native `totalfog/modded_pawn_presentation` contract passes ten staged rows:
Normal, SuicideBomber, ToxicSplasher, TankyOperator, Miner, Electrifier, Albino,
DarkSlimer, Healer and ZombieSpitter. Every row receives zero draw-phase,
virtual DrawAt and overlay calls while hidden, positive calls when revealed,
and zero calls after losing sight. The spitter's actual custom DrawAt executes
when visible. Each pawn retains exactly one renderer registration. The same ten
rows pass after reload, including selection accepted while visible and removed
on sight loss, with no ticks advanced during the second transition test.
Two native reloads regenerate 1,864 sections with no failures or thing-grid
mutations. Those initial body-presentation results use the common fog gates
and did not need Zombieland gameplay edits. Later active-effect and HUD fixes
are recorded separately above. Evidence is in ignored
`artifacts/zombieland-presentation-remote.json`,
`artifacts/zombieland-presentation-reloaded.json`,
`artifacts/zombieland-render-reload.json` and the paired-deployment manifest.
They do not prove pixels, active special effects, emerging rubble, ranged/melee
combat, zombie balls, faction changes, audio/notification policies or horde
performance beyond the fixture below. Those remain outstanding with Windows
acceptance and longer play.

A six-process original/candidate comparison on the same upstream-authored
250x250 save with 1,000 zombies (1,070 total pawns) completes and passes the TPS
floor. Median TPS is 269.11 upstream versus 302.63 Total Fog (+12.46%); paired
gains are 4.81%, 6.15% and 19.07%, so the run-to-run variability limits a general
speedup claim. Median sample tick p99 improves from 12.5292 to 10.9715 ms and
frame p99 from 93.3332 to 88.2305 ms. Frame p95 is 1.39% worse, 77.3646 versus
78.4412 ms. This establishes a targeted TPS floor, not universal FPS or controlled
spike acceptance. The comparison checks exact DLLs, unchanged save, settings
(including hearing cues enabled), loadout, focus and renderer in fresh processes.
See `artifacts/comparison-zombieland-upstream-1000.json` and VALIDATION.md.

The current audio-fix gameplay pair also passes a six-process comparison at
actual native fourth speed on the upstream-authored 1,000-zombie quiet-gap
fixture. All measured ticks use multiplier 15 with both speed overrides off,
and all six native logs have empty recognized error summaries. Median TPS is
289.80 original versus 312.17 Total Fog (+7.72%); mean tick elapsed is 7.85%
lower. Frame p95 is essentially unchanged, 69.6921 versus 69.7293 ms. Population
endpoints differ by at most one zombie in both variants. This establishes a
bounded player-speed TPS floor, not universal FPS, exact work replay, load-spike
or long-session acceptance. Evidence is
`artifacts/comparison-quiet-gap-player-1000.json` and
`artifacts/quiet-gap-player-floor-verification.json`.
The same exact gameplay pair passes a fresh twelve-case Symbiant pixel/visibility
matrix, 54 manual targeting controls and all forty dense/sparse render-cost
rows before packaging. Windows and broader gameplay remain outstanding.

The later wall-rotation pair, Total Fog 9a76ddd3... / Zombieland ff207b8c...,
passes another six-process comparison on this same fixture. With 300 native
warmup ticks, ordinary multiplier fifteen, no forced speed and no DPA/zombie
work instrumentation, medians are 296.6253 original versus 315.0459 candidate
TPS, 6.21% higher; mean tick elapsed is 6.45% lower and frame p95 is broadly
unchanged. All six logs pass the native error guard. This is the current
pair's bounded TPS floor, not universal performance. Its exact-pair gates now
repeat all twelve native/pixel cases, 54 manual targets and forty dense/sparse
render-cost rows with clean logs. See `artifacts/zombieland-current-package-gates.json`.
Broader gameplay and Windows remain open. Performance evidence is
`artifacts/comparison-wall-rotation-player-1000.json`.
The updated pair is now delivered to Mortal as two complete separate ZIPs,
with each remote attachment downloaded and matched to its packaged SHA-256
before deleting the superseded post. Zombieland's complete music folder contains
53 tracks and its README. This remains preliminary; see VALIDATION.md and
`artifacts/mortal-wall-rotation-remote-verified.json`.

All 23 controlled Albino sabotage contracts now pass together on the unchanged
delivered pair. Two diagnostic assumptions were corrected: native melee roles
stabilize initial pressure, and closed-door setup needs a clear nine-by-nine
room plus valid outside candidates rather than an entirely empty radius-fourteen
area. The focused safety checks retain exact path/movement, abandon destinations
at the real pressure limit, and reject routes through the closed door. The room
fixture supplies 475 valid outside candidates. The full suite also accepts its
scream, paralysis, movement, door-resume and equipment-hack helper contracts.
Logs are clean, native test mode is restored and the unchanged base is reloaded
paused. Those helper fixes changed only diagnostics; see VALIDATION.md and
`artifacts/zombieland-albino-corrected-suite-acceptance.json`.

The Albino now also acquires its own Sabotage job and scream during ordinary
Normal playback. Three audio controls each pass seven native checks for
acquisition, hidden/visible/hidden meshes and attached bubble, actual sound,
damage-job continuation, victim stun/vomit and natural expiry. The two hostile
victims are held in Wait to isolate the scream. No Albino job, scream phase,
queue or cooldown is forced, and no tick loop calls the action helper. A native
melee hit first exposed a job-giver bug: proposing Sabotage called StopAll and
reset the active scream at phase 40. Removing that call lets RimWorld retain a
continuing job. The focused damage-notification control now preserves the exact
job, driver and phase. Broader victim combat, native equipment/door hack
presentation and interrupted save/load remain open. Evidence is
`artifacts/zombieland-albino-scream-acceptance.json`. The combined gate also
passes all 21 controls after correcting the fixture's independent Wait melee
behavior with per-victim definition copies. No shared pawn definitions change.
The exact pair TF `600083e5...` / ZL `cb27dbd1...` passes six fresh matched
fourth-speed samples, median 313.3144 versus 293.7768 TPS, 6.65% higher; frame
p95 is 71.2907 / 70.2680 ms. All existing pixel/targeting/render-cost and native
arrival/warning gates repeat with clean logs. This remains bounded Mac proof.
See `artifacts/comparison-albino-continuation-player-1000.json` and the current
package gates. This pair has not been sent to Mortal yet.

The native danger-area warning exposed an unseen Normal zombie both initially
and after losing sight. A following root/core-disagreement check also reproduces
a Symbiant warning at its hidden root while its core is visible, and an absent
warning at a visible root while its core is hidden. Zombieland now checks current
sight at the grid position used by area classification and camera jumps before
choosing the warning's area or collecting zombie icons. Seven native presentation
controls pass on Total Fog `600083e5...` / Zombieland `5f6380a8...`, retaining
visible-location and colonist warnings. The simulation cache still holds every
staged zombie entry, and no game ticks advance. Recognized logs are clean and
the base save is unchanged. This accepts the controlled warning renderer, not
natural cache classification, every zombie type or hover/click pixels. The final
pair passes fresh six-process fourth-speed performance and native packaging
gates, including all seven warning cases. Median TPS is 319.5605 versus 295.1910
for the original, 8.26% higher; frame p95 is 70.7764 / 70.2655 ms. The first
warning pair's evidence is retained separately. See VALIDATION.md
and `artifacts/symbiant-area-warning-acceptance.json`.

Mortal reports the replacement without drawing snapshots performs much better.
His Windows 1.6 rev591 log measures fog initialization at 540 ms and contains
one `SectionLayer_ThingsPowerGrid` list-index exception during regeneration.
Investigate the actual mutation or engine trigger; do not hide it with a catch
or skip unrelated objects. The same exception was subsequently reproduced locally after a save/reload
during the runtime-identity check. The probe traced it to our use of the mutating PawnFlyer.DrawPos getter during section iteration. The pure policy no longer reads that getter; three native reloads now pass 3,258 regenerations without errors or grid mutations. Windows confirmation remains pending.

Mortal's notification examples: diseases affecting owned colonists, eclipse and
toxic rain should remain reportable. Broad letter-category suppression needs
separate acceptance from observation-based hiding. Record additional examples
through playtesting after the original-mod performance floor is met.

Paired contamination UI check, 2026-10-06: the native eight-row overlay probe
reproduces hidden and hidden-again markers over remembered terrain in both
detailed and zoomed-out cached paths. Paired on/off pixels confirm the leak;
selection is blocked correctly, game ticks do not advance, and ground/item
contamination remains 0.65. The unchanged base save is reloaded after the probe.
The detailed sight gate and local native-section cached renderer now pass
sixteen rows: eight staged sight controls and eight actual security-bell
spawn/despawn controls while paused. Every hidden on/off crop is pixel-identical;
positive controls retain their markers. No explicit mesh regeneration is used
after the fix. TF publishes queued section changes during paused map updates.
The section renderer reuses existing fog dirtiness and the existing contamination
refresh delays, with no new visibility API or whole-map sight scan. See
`artifacts/contamination-overlay/remembered-before{,-pixels}.json`.
Post-fix proof is under `artifacts/contamination-overlay/{staged,native-source}`.
The current optimized renderer also passes 14 native sight/refresh rows and 28
paired crops in `artifacts/contamination-overlay/native-refresh-lean`: hidden
mutations, reveal, off-camera return, close/reopen, zoom return and clearing.
Every hidden or cleared crop is pixel-identical; visible intensity follows the
new values. These controls retain data and advance no ticks. Map switching,
standalone no-Total-Fog startup, moving-camera performance and broader gameplay
data conservation/save-load remain open.
The matched-visibility paused 4,000-cell diagnostic alternates original native
drawers and sections. Dense refresh is 19.20 versus 21.94 ms; three sparse pairs
are 23.82 versus 21.71 ms. Warm section overhead is approximately 0.02 ms/frame
dense and 0.05 ms/frame sparse. These inclusive native timings include the
probe; they are not GPU completion or whole-game TPS acceptance. See
`artifacts/contamination-overlay/cost/lean-4000-{dense,sparse}.json`.
The new pair passes a fresh matched six-process 1,000-zombie ordinary fourth-speed
comparison with the overlay closed: 317.7687 versus 287.0974 median TPS,
10.6832% higher. Frame p95 is 70.5089 versus 69.9316 ms. This does not establish
enabled-overlay cost; see `artifacts/comparison-contamination-player-1000.json`.
The optimized e24d50df... / 56daa1f2... pair also passes six fresh matched ordinary
fourth-speed runs with the overlay enabled, staging 4,000 sparse ground cells
at 0.65 and framing the whole 159x99 area. Median TPS is 226.8007 versus
212.6721, 6.6434% higher. Mean tick elapsed time is 3.049135 versus 3.304660 ms,
7.7323% lower; frame p95 is 82.9023 versus 78.2577 ms, 5.9350% higher. No debug
boost, forced speed, DPA or zombie-work telemetry is used. All 1,000 zombies
remain at each measured endpoint. Visibility policies remain active, so this
accepts the bounded whole-game TPS floor with matched input, not identical
geometry/GPU cost or universally better frame pacing. See
`artifacts/comparison-contamination-overlay-player-1000.json`.
Raw stat helper values are diagnostic only, not evidence of reachable hidden
inspection. Stat/icon/mouseover caller contexts, legitimate owned inventory,
decontamination quests and actual data conservation/save-load remain open.
