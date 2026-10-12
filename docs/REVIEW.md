# Total Fog 0.1.0 review

Two independent review rounds of the first version. The second round
(13 October 2026, commit 6207fc9) is first; it verifies the fixes made in
response to the first round and adds new findings. The first round
(12 October 2026, commit 12e30f6) and its point-by-point response follow,
unchanged, so the responses can be audited. For current implementation and
open native checks, use [COVERAGE.md](COVERAGE.md#current-status).
Severity is judged for a first public version: small stuff is left out.

## Second review, 13 October 2026 (commit 6207fc9)

Four read-only passes: verification of the 19 fix commits, features and
payload decisions the first round skipped, a design pass on the mod's own
terms, and a complete Harmony hook inventory (about 153 always-installed
patch sites, 19 transpilers, 22 per-frame-per-thing hooks). Line numbers
refer to commit 6207fc9.

### Status of the first-round items

| Item | Verdict |
|---|---|
| 1 patch isolation | Partially fixed: install is isolated, but two new transpilers throw again (R2-1) |
| 2 discovery before Initialized | Fixed; the MP second-faction branch lacks the guard (R2-11) |
| 3 stale tracked DLL | Fixed, then regressed: the DLL is the 28106cf build, six gameplay commits behind HEAD (R2-3) |
| 4 MP binding | Fixed |
| 5 mortars | Fixed |
| 6 defaults | Kept by Andreas's decision; documented |
| 7 mesh flags | Fixed |
| 8 alerts | Fixed, off by default |
| 9 fire and own corpses | Fixed; home-cell fire is drawn without sight by design (R2-13) |
| 10 gas layer | Fixed; now regenerated on every fog-section flip, unmeasured (R2-14) |
| 11 listener cache | Fixed |
| 12 deferred letters | Fixed; archived letters carry no arrival tick (R2-6) |
| 13 to 15 tower | Fixed |
| 16 camera operator | Fixed |
| 17 translations | Fixed structurally; no native-speaker review |

### Must fix before a public build

- **R2-1 Throwing transpilers are back.** Source/TotalFog/Watchtowers/TowerMoveOrders.cs:173
  and :239 throw on shape mismatch. Install is caught, but Harmony re-runs
  every transpiler when another mod later patches `Pawn_DraftController.Drafted`,
  `FloatMenuOptionProvider_DraftedMove`, `Selector.HandleMultiselectGoto` or
  `MultiPawnGotoController`; the throw then aborts that mod's patch call.
  Achtung touches exactly this area. Use `PatchSafety.Matches` like the others.

- **R2-2 Reference counts corrupt on any exception inside a sight update.**
  CompSightSource.cs:253-256 applies the mask difference and only then swaps
  `(current, next)`. `IncrementSeen` runs `VisibilityChanged`
  (MapVisibility.cs:563-586), which calls listener `UpdateVisibility`,
  `RecordObservation`, selection, mesh dirtying, codex discovery and letters
  inside that transaction. Any throw skips the swap; the next refresh
  re-applies the increments and the cells stay seen forever. Complete the swap
  in a `finally`, or queue listener side effects until after the swap.

- **R2-3 Tracked DLL is stale again.** Current/Assemblies/TotalFog.dll is the
  28106cf build (renamed in 5a0d5fb); it lacks `TowerMoveOrders`,
  `DebugFogCommands`, `WatchtowerIcons` and `WatchFacing`. Def bindings still
  resolve so nothing fails to load, but the first-round requirement stands:
  either every gameplay commit carries its DLL, or the DLL is untracked and
  the README points at releases. A CI job that fails when the DLL's source
  commit is older than the newest gameplay commit would enforce this.

- **R2-4 1.7 is advertised on a 1.6-compiled, untested payload.**
  About/About.xml:7-10 and LoadFolders.xml:26-30 map 1.7 to Current/. The
  installed 1.7.4976 is the Steam unstable beta; no Krafs 1.7 reference exists
  and no 1.7 run is in the evidence. Hard type references are caught at
  install, but runtime references inside job drivers and comps are not. When
  1.7 goes stable, Steam will show "compatible" with zero re-verification.
  Drop 1.7 from supportedVersions until a 1.7 reference exists and the gates
  were rerun, or say "1.7 unstable" in the description.

- **R2-5 Startup logs three red config errors.** Current/Defs/ThingDefs_Buildings/TotalFog_Watchtower.xml:11
  sets `drawerType RealtimeOnly` while :34 sets `linkFlags Wall`; Verse
  reports "does not add to map mesh but has a link drawer" for the tower and
  its blueprint on every launch (artifacts/steam-startup-20261012). Mod
  managers and players with dev mode will blame Total Fog. Either drop the
  link flags and keep the custom wall-join drawing, or move to a drawer type
  that prints.

### Should fix in the first update

- **R2-6 Archived dead-target letters sort to day one.**
  DeferredNotifications.cs:92 calls `Find.Archive.Add` directly, so
  `arrivalTick` stays 0 and `Received()` never runs. Set the arrival tick from
  `TicksFor(map)` or route through `ReceiveLetter` with the replay flag.

- **R2-7 Deferred queue has no per-item isolation.** Source/Core/PendingQueue.cs:17-37:
  a `replay` that throws leaves the item in place, aborts the loop, and retries
  every 30 ticks with a log error forever. Catch per item and drop or park it.

- **R2-8 Tower lookup has no zero-tower fast path.**
  Watchtowers/TowerPresentation.cs:18-19 walks MapHeld, a ConditionalWeakTable
  and a dictionary for every pawn draw, DrawPos read, TrueCenter, mote draw,
  CanSee, shoot line, TakeDamage and FlammableNow, in colonies that never
  build a tower. A static spawned-tower counter checked first removes
  roughly a thousand weak-table lookups per frame.

- **R2-9 Tooltip transpiler scans every giver map-wide each frame.**
  Presentation/InterfaceVisibility.Patches.cs:116-138 replaces both `Position`
  reads, including the one inside the camera-rect test, so `CanRead` plus a
  linear `TryGetComp` run for every tooltip giver on the map, offscreen
  included. Replace only the second read.

- **R2-10 Hot-path lookups that should be cached.**
  `GravshipVisibility.Revealed` (Compatibility/GravshipVisibility.cs:9-15)
  resolves `Find.GravshipController` through a World component scan per
  visibility query with Odyssey; `IsShown` compares the biome defName string
  per call (MapVisibility.cs:130,150); `KnownActionable` fetches
  `areaManager.Home` through a list scan (ThingVisibility.cs:297); the
  roof/terrain/fertility overlay postfixes resolve `Bypass` and the weak table
  per cell on every discovery tick (Presentation/DiscoveryOverlays.cs:10-22).
  Cache per map or snapshot per regeneration as the gas and lighting
  transpilers already do.

- **R2-11 Second-faction discovery bypasses the Initialized guard.**
  MapVisibility.cs:493-500 writes `known[index]` for extra player factions
  regardless of `coverage.Initialized`. Unreachable before init today only
  because of one gated call site; add the guard in the grid.

- **R2-12 Camera console job is permanent busywork by default.**
  `NeedWatcher` defaults true, the WorkGiver is BasicWorker priority 150 with
  `prioritizeSustains`, and JobDriver_MonitorVision.cs:33 never completes.
  One console pins a colonist ahead of all non-basic work with no skill gain.
  Give it its own WorkType or default the setting off.

- **R2-13 Telescope is uncapped and undercuts the tower.**
  CompSightSource.cs:346-379 takes max(base, weapon range) times 2 with no
  cap, so a sniper on a telescope sees 120+ cells while the tower caps at 96;
  the telescope is Medieval research with no prerequisite. Apply the tower cap.

- **R2-14 Unmeasured regeneration costs.** Gas is now dirtied on every
  fog-section flip (MapVisibility.cs:595) next to GroundGlow, and the paused
  fade fix (SectionLayerFog.cs:164-182) completes every on-screen fade
  instantly on pause rather than only off-screen leftovers. Both need one
  runtime-compare run and a pixel look.

- **R2-15 Prefix etiquette on shared targets.** `CanSeePreFix`, the tower
  shoot-line prefix, `PlayOneShot.Prefix`, `DangerModePrefix` and the 27
  designator prefixes return false with no Priority, which skips every later
  bool prefix from Zombieland, CE and others. `OrderedJobPrefix`
  (TowerMoveOrders.cs:316) rewrites `ref job` on a method Multiplayer syncs
  with unspecified ordering. Six targets are patched from two of our classes
  (`TryFindShootLineFromTo`, `CanSee`, `MoteBubble.DrawAt`, `FleckStatic.Draw`,
  `ComputeCulledThings`, `MouseoverReadoutOnGUI`) and only work because of
  install order. Pin priorities and consolidate.

### Keep in mind

- `AnimalVision` requires both a master and Release training; README says
  "trained animals". Expect "my animals do not see" reports.
- `DebugFogCommands` write saved discovery and coverage that gates turret
  targeting; not verified under Multiplayer, and the Fog rect tool is matched
  by the literal label "Fog".
- `Current/Defs/Mod_Defaults.xml` defines a `FogDefaultsDef` nobody reads.
- CI runs format-check and tests but never compiles the gameplay project, so
  a compile break in Source/TotalFog passes CI.
- The `Current/` multi-version folder is outside the steam-mods standard
  (`currentRimWorldVersion 1.6`); record it as a declared exception in
  AGENTS.md rather than relaxing the validator silently.
- The draft-setter replacement (TowerMoveOrders.cs:191-194) mutates job state
  from inside a query; works for the native shape, worth a guard.
- Save compatibility across the 19 commits is clean; only an unused settings
  key was removed.

### Design decisions for Andreas

These are design questions, not bugs. Each has a recommended answer.

- **D1 Noise as the second sense.** Today the only signals from a dark raid
  are hearing cues for moving pawns and native slowdown once shots land. A
  siege or sapper team sets up and fires from fog with no cue. Recommended:
  transient noise events (gunfire, explosions, mortar launches, pod impacts,
  breaching) spawn a fading ring at a jittered position for a few seconds when
  inside a listener's hearing. Per-sound muting becomes a sub-option. This is
  what makes silent raids fair.

- **D2 A clock rule for delayed events.** With delayed alerts on, a mech
  cluster countdown or pit-gate emergence can run out unseen. Recommended:
  events whose consequence waits for the player (visitors, quests, items)
  delay fully; events with a running timer arrive as a vague letter with
  direction and rough distance, no jump target.

- **D3 Escape hatch and "why is this hidden".** `OnlyOutsideColony`
  (MapVisibility.cs:150) disables fog on every player-home map, not outside
  the home area as the label suggests, and it is the only in-game off switch.
  Recommended: rename it, add an explicit "suspend fog" toggle plus a dev-mode
  cell overlay showing seen count, known flag, blocking source and bypass
  reason, and a settings button that copies version, settings and mod list to
  the clipboard for reports.

- **D4 Enemy fog as a visible stealth mechanic.** `AISmart` is one checkbox
  under Information, humanlike-only, with no player feedback. Recommended:
  move to Vision as "Enemies have fog too", define species rules explicitly
  (mechs see all, animals short sight and long hearing, insects see all
  underground), and show a drafted-pawn indicator for "seen by enemy".

- **D5 Target illumination.** Light only shrinks the observer's radius; a
  colonist under a lamp is no easier to see than one in the dark. One glow
  lookup per target cell in the enemy gate would make base lighting a real
  choice during raids.

- **D6 One hearing stat.** Cues use 10 times hearing capacity, the audio check
  uses a fixed 30-cell range with any living faction pawn as listener, and a
  sleeping pawn's sight is 8 times hearing. Unify into one stat, halve while
  sleeping, and attenuate by door crossings rather than Euclidean distance
  through walls.

- **D7 Vision sources as a progression.** Telescope, tower and cameras overlap
  in role. Recommended currencies: telescope costs pawn time early, tower
  costs pawn time plus exposure mid, cameras cost power plus operator, with
  operator-free cameras behind Microelectronics.

- **D8 Ally and prisoner vision.** Traders reporting what they see at 0.5x and
  prisoners at 0.2x is narratively odd and leaks little. Recommended: drop
  both, or reframe as "friendly pawns are visible to you".

- **D9 Presets.** Atmosphere (visuals only), Survival (delayed letters, audio
  check, Wildlife hidden, cues) and Hardcore (silent raids, enemy fog, mute
  hidden, discard threats) plus Custom. Flag the traps: discard flags lose
  letters permanently, trees-block is reload-only, map reveal applies to new
  maps only, tower shooting is a balance change dressed as a toggle.

- **D10 Multiplayer sharing.** Per-faction fog is right for separate colonies.
  For allied player factions `AllyGiveVision` would leak the other player's
  sight at 0.5x; a synced "allied players share sight" setting and a defined
  spectator view (union of player factions) are needed.

- **D11 Compatibility message.** The generic gates (dynamic draw, flecks,
  labels, overlays, GUI, sounds, targeting) already cover most mods.
  Documentation should say that `TotalFogSupport.cs` is only for custom mesh
  drawing, counters and AI, and a dev-mode "leak list" of things drawn at
  hidden cells would find the residue faster than asking authors to opt in.

### Work order 2

One commit per step, pure tests where the step names one.

1. R2-1 and R2-7: shape-tolerant TowerMoveOrders transpilers; per-item
   isolation in `PendingQueue.Drain` with a test that a throwing replay is
   dropped and later items still replay.
2. R2-2 and R2-11: complete the mask swap in a `finally` or defer listener
   side effects; guard second-faction discovery in the grid. Test: a throwing
   listener leaves counts consistent on the next refresh.
3. R2-6: arrival tick for archived letters.
4. R2-8, R2-9, R2-10: zero-tower fast path, tooltip single-read, cached
   gravship controller, home-map and biome flags, overlay snapshots.
5. R2-15: priorities and consolidation of duplicate targets.
6. R2-12 and R2-13: console WorkType or default, telescope cap.
7. R2-5: resolve the tower config errors; confirm a clean Player.log.
8. R2-4: remove 1.7 from supportedVersions and LoadFolders until a 1.7
   reference exists; keep Current/ and record the folder as a declared
   exception in AGENTS.md.
9. R2-14: one runtime-compare and pixel look when a game session is permitted.
10. R2-3 last: one Release build, commit the DLL with its source, and add the
    CI check that fails on a stale tracked DLL. Add `build` to CI.

D1 to D11 wait for Andreas's decisions; do not implement them unasked.

## First review, 12 October 2026 (commit 12e30f6)

Four read-only passes: sight engine, information-leak surface, watchtower,
settings/compatibility/process. Line numbers refer to commit 12e30f6. The
point-by-point response follows at the end.

## Must fix before a public build

1. **Startup is all-or-nothing.** The static constructor of `TotalFogMod`
   (Source/TotalFog/TotalFogMod.cs:34-40) runs every patch install with no
   isolation. `patchMethod` throws on any missing target (:613) and the
   transpilers in Presentation/InterfaceVisibility.Patches.cs:33,72,99,126,
   Presentation/LightingVisibility.cs:61, Presentation/ExplosionVisibility.cs:40,
   Watchtowers/TowerBedSearch.cs:29, Watchtowers/TowerPresentation.cs:169,209,
   Watchtowers/TowerCombat.cs:461 and Compatibility/CombatExtendedTowers.cs:185
   throw when the instruction shape differs by one. Harmony re-runs every
   transpiler whenever any mod patches the same method, so a mod that
   transpiles `MouseoverReadoutOnGUI`, `Projectile.DrawAt` or
   `RestUtility.FindBedFor` before or after us either aborts our constructor
   (one TypeInitializationException, every later gate silently missing: letters,
   audio, music, codex, AttackTargetFinder) or has its own patch call aborted by
   our throw. Wrap each install separately, log once, and degrade the single
   feature; register the notification and audio gates before the cosmetic ones.

2. **Load-time exploration leak.** `CompSightSource.PostSpawnSetup`
   (CompSightSource.cs:47-56) casts immediately during `Map.FinalizeLoading`,
   before walls saved later in the thing list exist. `VisibilityGrid.Add`
   (Source/Core/VisibilityGrid.cs:62-69) sets `Known` with no `Initialized`
   guard and `Known` is monotonic, so every load can permanently explore cells
   behind walls. `Initialize` (MapVisibility.cs:367-391) recasts counts but
   cannot undo discovery. Skip casting, or at least discovery, until
   `Initialized` is true.

3. **Tracked payload is stale and self-inconsistent.** 1.6/Assemblies/TotalFog.dll
   was last committed in e28a51b (6 October); 31 gameplay commits followed. The
   DLL contains neither `Building_Watchtower` nor `ColonyMechVisionModifier`
   while 1.6/Defs/ThingDefs_Buildings/TotalFog_Watchtower.xml references the
   tower classes. Anyone who clones the public repo into Mods/ gets def-load
   errors. Either commit a fresh DLL with every gameplay change or stop
   tracking it and point the README at releases only.

4. **Multiplayer binding fails hard, every 30 ticks.** `isActive` is assigned
   (Compatibility/MultiplayerIntegration.cs:72) before the non-MPAPI bindings of
   `Multiplayer.Client.Extensions.AsyncTime`, its `mapTicks` field and
   `PushFaction`/`PopFaction` (:82-86, :115), all of which `throw` on absence.
   An MP rename logs one exception at startup but leaves `Active == true`, and
   `BeginFactionContext` then throws from `DeferredNotifications` on each check.
   Bind `isActive` last and disable the whole integration with one warning on
   any failure.

5. **Manned mortars fire into the fog.** Detours/Verb.cs:17 returns early when
   `requireLineOfSight` is false and `CanSeePreFix` (HarmonyPatches.cs:50-64)
   only acts for non-player seers with `AISmart`. Turret target search therefore
   accepts any hidden hostile in range; the player watches the mortar swivel
   and fire at nothing. For player casters with `ProjectileFliesOverhead()`,
   require the target cell to be seen by the faction while keeping manual blind
   fire on cells. Also use `PositionHeld` instead of `Position` in
   `CanSeePreFix` for carried targets.

6. **Defaults contradict the pitch.** FogSettings.cs:36-55: `DoAudioCheck`,
   `MuteHiddenSounds`, `DelayAlertsUntilSeen`, `SilentRaids` and all discard
   flags default off while `SuppressCombatMusic` defaults on and
   `WildLifeTabVisible` defaults on. A new player loses combat music but still
   hears every hidden gunshot, receives every letter from the dark, and reads
   manhunter positions in the Wildlife tab. Decide one default policy
   (recommended: delay unseen events on, audio check on, wildlife tab off) and
   document it in README and About.

## Should fix in the first update

7. **Exploring regenerates every heavy mesh layer.**
   `CompPresentationState.updateMeshes` (CompPresentationState.cs:72-90) dirties
   Terrain, Roofs, Snow, Pollution, Zone, PowerGrid, Gas and more for every
   cell of every map-mesh thing whose visibility flips. A walking pawn flips
   hundreds of plants and rocks per tick. docs/ARCHITECTURE.md says only
   discovery dirties terrain and roof drawers; the code does not match.

8. **Alerts are an ungated channel.** No `Alert` or `AlertsReadout` patch exists.
   Fire in home area, cultist ritual, emerging pit gate, timed raids,
   countdowns and infestation delay all report hidden map state and their
   culprit jump reveals the position. Postfix `AlertsReadout` or `Alert.Active`
   for a whitelist of threat alerts using the existing
   `NotificationVisibility.HasVisibleTarget`.

9. **Fire policy is unplayable.** `Fire` is `category Attachment`, so
   `InitializeCompsPrefix` (TotalFogMod.cs:86-96) never attaches `CompFog`; fire
   needs current sight (Presentation/ThingVisibility.cs:92-107). Firefighting
   calls `CanReserve` for distant fires, which Detours/ReservationUtility.cs:10-24
   fails, so colonists are not dispatched to an unseen home-area fire while the
   smoke (gas layer, ungated) and the fire alert show it exists. Treat fire on
   known home-area cells as reservable and targetable. Related: player-faction
   corpses need current sight too (ThingVisibility.cs:95), so a colonist who died
   in the field cannot be hauled until someone walks there; exempt own-faction
   corpses.

10. **Gas layer leaks.** `SectionLayer_Gas` is unpatched; smoke from hidden
    fires and tox gas from hidden grenades render on explored cells. Gate per
    cell in its `Regenerate` with the faction shown array; fog already dirties
    sections.

11. **Audio scan is O(pawns) per sample per frame.** Audio/SustainerVisibility
    runs on `SampleSustainer.Volume`; for hidden non-muted sources
    SoundAudibility.cs:38-48 iterates `AllPawnsSpawned`. Zombieland hordes with
    ambient loops make this hundreds of sustainers times hundreds of pawns per
    frame. Cache nearest-listener distance per tick.

12. **Deferred letters use two clocks and can vanish.** `earliestTick` comes
    from `MultiplayerIntegration.TicksFor(map)` while `Ready` and `nextCheck`
    compare against `Find.TickManager.TicksGame` (DeferredNotifications.cs:47-64);
    under async time the delay is wrong or infinite. Separately, a letter whose
    targets all die while hidden is dropped without archiving
    (Source/Core/PendingQueue.cs:23-27): a raid killed off-screen never reaches
    History. Replay dead-target letters to the archive, or mark them ready.

13. **Watchtower: blocked ladder strands the lookout.** The "wait for clear
    ladder" toil (Watchtowers/JobDriver_Watchtower.cs:170-187) has no needs or
    downed exit, the one warning fires once, the report reads "Climbing down"
    forever and the inspect string shows nothing. Nothing prevents a wall or
    frame being placed on the approach cell after occupation. Add a needs exit,
    a persistent inspect warning, and a placement block on the approach cell.

14. **Watchtower: downed lookout cannot be tended, rescue is bed-gated.** The
    occupant's position is the impassable center, so tend and feed are
    unreachable; auto-rescue requires `FindBedFor` (WatchtowerPatches.cs:518)
    and the manual option lives only on the tower's float menu
    (Building_Watchtower.cs:238), while right-clicking the pawn says "No path".
    Offer the rescue option on the pawn too and allow retrieval without a bed.

15. **Watchtower balance.** No `researchPrerequisites`, 150 stuff, 6000 work
    (1.6/Defs/ThingDefs_Buildings/TotalFog_Watchtower.xml) while cameras need
    research. Double sight to 96 cells from a wooden tower on day one makes
    outer fog largely optional. Platform shooting also zeros target cover in
    both directions (TowerCombat.cs:331-335); the outgoing half is an
    undocumented buff. Gate behind research and keep enemy cover for outgoing
    shots.

16. **Camera operator setting is half-wired.** MapVisibility.cs:50 honours
    `FogSettings.NeedWatcher` but `WorkGiver_MonitorVision.cs:36` and
    `JobDriver_MonitorVision.cs:20` call `Building_VisionConsole.NeedWatcher()`
    which always returns true. With the setting off, colonists still take
    pointless console jobs. `needMemoryStorage` is saved but never read.

17. **Translations describe retired buildings as working.** German, French and
    Chinese DefInjected files for cameras, vision extenders and inspector
    animals still promise vision; English says "retired". The 58-key English
    Keyed file and the watchtower defs have no translations at all. Keyed falls
    back to English, DefInjected does not.

## Keep in mind (not blocking)

- Blocker polling: `CompWallOcclusion.CompTick` (CompWallOcclusion.cs:25-32)
  re-evaluates every 30 ticks, so a door reaches sight up to half a second
  late and each flip scans all blocker sources. A door-state signal would be
  cheaper and more correct.
- Refresh drain is unbounded per tick (MapVisibility.cs:330-337); a collapse
  near many sources casts hundreds of times in one tick. Correct, but spiky.
- `Initialize` calls `SkyManagerUpdate()` for non-current maps
  (MapVisibility.cs:376), briefly overwriting the current map's sky.
- Comp fan-out: every plant, rock, filth and item gets 4-6 extra comps
  (TotalFogMod.cs:82-101, CompFog.cs:17-44), including `CompWallOcclusion` on
  every natural rock. Memory and load time on mountain maps; no per-def opt-out.
- `DynamicVisibility` and the tower `CullBounds` both do a lookup per drawn
  thing per frame (Presentation/DynamicVisibility.cs:19-42,
  TowerPresentation.cs:182-190). Cheap type checks first would remove the
  common case.
- `IsPrisonerOfColony` and local-colonist seeding (CompSightSource.cs:124,
  MapVisibility.cs:397-403) are player-relative inputs; with `AISmart` they
  feed simulation and are Multiplayer desync candidates.
- Any Full-fill edifice (cooler, vent, battery) counts as tall for tower sight
  (MapComponent_Watchtowers.cs:79, TowerCombat.cs:287); a cooler in a wall
  cuts a wedge out of the panorama.
- `DangerMusicMode` prefix is unconditional when the setting is on
  (Audio/MusicVisibility.cs:7-13), even for fully visible threats, and music
  mods reading it get a false value.
- Lightning bias for towers is about 0.1% per strike (WatchtowerPatches.cs:549);
  PRELIMINARY.md promises more than that.
- The player ZIP ships PRELIMINARY.md and the developer README (scripts/mod:13-23).
- `incompatibleWith` lists only the NWN fork; the original Real Fog of War
  packageId and other vision mods are missing.

## Conceptual blind spots

- **Strict "seen right now" collides with ownership.** Fire, corpses,
  stockpile contents, downed colonists and blueprints in your own base are the
  cases where strict hiding stops being atmosphere and becomes a puzzle. The
  design already carves exceptions for blueprints and doors; it needs a stated
  rule, for example "player-owned things on known home-area cells are always
  actionable", instead of case-by-case detours.
- **The engine is purely 2D.** Roofs and overhangs are ignored except on the
  tower path, light only shrinks range, multi-cell pawns cast from their root,
  hearing is Euclidean distance through walls, and animals, mechs and insects
  are exempt from enemy fog. Pocket maps and gravship maps start unexplored
  with their own full grids. None of this is wrong for 0.1, but it is not
  written down for players, who will file each as a bug.
- **Deep systems still leak.** Research availability, storyteller threat
  points, world map, pathfinding refusals through hidden danger, colonist flee
  behaviour (vanilla line of sight), the time-slowdown line when a hidden enemy
  fires. The README mentions research; the rest should be in a short "what the
  fog does not hide" list.
- **Verification is macOS-only and live-only.** scripts/mod hardcodes the Mac
  app, there is no CI, and the 364 tests are pure. Windows has zero coverage
  while being the majority platform. A GitHub Actions job running
  `format-check` and the tests on every push is cheap insurance.
- **Documentation is an agent ledger.** docs/VALIDATION.md is 500 KB,
  COVERAGE.md 71 KB and AGENTS.md 28 KB of tool procedure, against 2k words of
  architecture. A human maintainer or contributor cannot find the contract.
  Move evidence ledgers to release artifacts or a wiki, and keep ARCHITECTURE,
  a short COVERAGE status table and a "known limitations" page in the tree.
- **Velocity versus verification.** 95 commits in seven days, nearly all
  agent-authored, with a 31-commit gap between source and tracked binary. The
  process optimizes for proof artifacts over shippable state. The first public
  build should be one Release build, tested, committed with its DLL, tagged.

## What is solid

The shadowcaster (Source/Core/FieldOfView.cs) handles radius 0, out-of-bounds
origins, rectangular maps and range shadows correctly, with reciprocity and
allocation tests. Reference counting is clean across faction and map changes,
despawn and blocker-list membership. Idle frames do no fog work. Legacy import
covers every saveable upstream class and keeps the `revealedCells` key.
Combat Extended integration is soft and version-tolerant. Zombieland support
lives on Zombieland's side through a documented reflection-free pattern that is
a model for other mods. Watchtower exit paths (death, carrier incapacitation,
corpse despawn, destruction, despawn) funnel through `Release` exactly once, and
no patch alters native projectile `ExactPosition`. Packaging strips debug and
companion files and the quiet workflow follows its convention. The About
description is honest about early status.

## Work order

Suggested order for the next session, grouped so each step is one commit with
tests where they are pure:

1. Items 1 and 4: per-feature patch isolation with one-line warnings; bind
   Multiplayer `isActive` last. Add a test that a missing patch target leaves
   other gates installed (a fake Harmony target is enough).
2. Item 2: no discovery before `Initialized`; add a pure test that
   `VisibilityGrid.Add` on an uninitialized grid does not set `Known`.
3. Item 5: mortar and turret targeting through faction sight; `PositionHeld`.
4. Item 6: default policy decision, README/About update.
5. Items 7 and 10: mesh-flag diet and gas layer gate; measure with the existing
   runtime-compare.
6. Items 8 and 9: alert gate and the "own home-area things are actionable" rule
   covering fire and own corpses.
7. Items 11 and 12: listener cache; one clock for deferred letters; archive
   dead-target letters.
8. Items 13, 14, 15: tower ladder exit and warning, rescue from the pawn menu,
   research gate, outgoing cover.
9. Items 16 and 17: camera-operator wiring, translation cleanup.
10. Item 3 last: one Release build, commit the DLL with the source it matches,
    tag 0.1.0, and add a CI job for format-check and tests.

## Review response

- Step 1, items 1 and 4: verified the cited constructor, patch helper,
  transpiler shape checks and early Multiplayer activation at 12e30f6.
  Installation now isolates individual patches and optional features, logs one
  concise warning per failed feature, and installs notification/audio gates
  before cosmetic hooks. Unsupported cited transpiler shapes leave the input
  unchanged, including when Harmony reapplies them later. Multiplayer stays
  inactive unless its clock, faction and settings bindings all succeed. Pure
  tests cover a missing target followed by successful gates and missing MP
  clock/context bindings. Review builds use ignored output without writing the
  tracked gameplay DLL. `format` and `build` passed, including 368 tests and
  zero build warnings/errors. No live deployment or compatibility claim is made.

- Step 2, item 2: verified CompSightSource:47-56, VisibilityGrid:62-69
  and MapVisibility:367-391 at the review commit. Native 1.6
  `Map.FinalizeLoading` explicitly spawns non-buildings before buildings,
  confirming the cause. Sources register during loading but do not cast until
  initialization; the core grid also refuses discovery before initialization.
  A pure test covers transient pre-init coverage, preserved loaded discovery,
  and durable discovery after initialization.

- Step 3, item 5: verified the cited early return and enemy-only target gate.
  Native 1.6 turret acquisition passes `IsValidTarget` into target search;
  overhead shoot-line checks otherwise return true without checking sight.
  Player turret candidates now require faction sight and overhead Thing shots
  are checked again at firing. Explicit cell targets retain native blind fire.
  Carried target checks use PositionHeld. Pure controls cover hidden/seen
  targets, blind cells, non-player turrets and preservation of native failures.

- Step 4, item 6: verified FogSettings:36-55 and its serialization defaults.
  README and About now state the actual default policy, optional information
  settings and simple engine-level limits. On 2026-10-12 Andreas chose to keep
  the current defaults. Enabling audio checks and delaying unseen events, with
  Wildlife hidden, remains a possible future policy rather than an accepted
  change. No setting defaults or existing saved settings changed.

- Step 5, items 7 and 10: verified CompPresentationState:72-90, the
  discovery-only overlay invalidation and the absent gas patch. Native 1.6 gas
  regeneration has one AnyGasAt call per cell. Thing transitions now dirty
  only Things/Buildings/BuildingsDamage. Fog-section changes dirty Gas and
  GroundGlow once per affected section; gas emission uses the viewed faction
  sight array captured once per regeneration without changing gas simulation.
  Pure tests cover dirty flags and hidden/seen/native-absent gas. The existing
  runtime-compare redeploys the shared Steam Mods root between samples, so it
  has not been run while Andreas's game is running and deployment is forbidden.
  This step has no new measured TPS claim; native pixels and comparison remain
  outstanding for a permitted runtime session.

- Step 6, items 8 and 9: verified no prior Alert patch, fire's attachment
  category, reservation gates and the corpse exclusion. Selected native threat
  GetReport overrides now filter hidden culprits and camera-jump targets when
  delayed information is enabled; health, world-only and unlocated reports stay
  native. Fire on known home cells, player-owned things there and own corpses
  on known cells are actionable without granting live effects current sight.
  Native reservations/reachability still have to succeed. Pure controls cover
  home/unknown fire, own/foreign corpses and filtered/unlocated alert reports.

- Step 7, items 11 and 12: verified SoundAudibility:38-48, the sustainer
  volume hook and DeferredNotifications:47-65 against PendingQueue:23-27.
  Audio now snapshots living hearing-capable faction listeners once per map
  tick/faction, and caches nearest distance per source cell for that tick;
  storage is reused and stays owned by the map. Sight/muting still checks live
  before that cache. Deferred deadlines and polling both use TicksFor(map).
  Letters whose targets disappear are retained until their deadline and added
  to the native archive in recipient faction context, instead of silently
  dropping. Pure tests cover cache clocks/factions/hearing/allocations and
  single replay retention; native async-time/history playback remains untested
  on these bytes.

- Step 8, items 13-15: verified the blocked-ladder toil, bed requirement,
  pawn-menu gap, missing research and outgoing cover reset at the cited lines.
  Impassable blueprint placement now protects existing ladders, and new towers
  reject planned ladder obstructions. Waiting has a persistent report/inspect
  warning and ends on downing; urgent needs can use the existing injured fall
  cleanup if free ground exists. A completely enclosed forced obstruction
  still needs clearing. Native own-pawn rescue menus now offer retrieval,
  automatic/manual retrieval do not require a bed, and tending/feeding follows
  retrieval. Towers require Complex furniture; shooting out retains native
  target cover. These engine-bound changes still need native play checks.

- Step 9, items 16 and 17: verified the cited camera helper, job giver,
  job driver, settings serialization and translated descriptions. The helper
  now reads NeedWatcher, so operator-free cameras skip console work and existing
  monitor jobs end through their native failure condition. Removed the unused
  memory setting and reset entry. German, French, Simplified Chinese and Korean
  now have all current TotalFog keyed text and new tower Thing/Job/WorkGiver
  translations, with matching placeholders. Retired towers, bells and inspector
  ornaments no longer promise sight, weather exemption, light or food use.
  The broader camera claim does not reproduce: surveillance cameras remain
  active, so their descriptions were retained. English Keyed fallback was a
  localization gap rather than a gameplay failure. These translations have
  XML/placeholder checks but have not had native-speaker or in-game review.

- Step 10, item 3: confirmed that the old tracked DLL lacked both cited
  symbols. One fresh Release build now supplies the tracked DLL matching the
  reviewed source; DecompilerServer finds Building_Watchtower,
  ColonyMechVisionModifier and the corrected camera setting helper.
  `format`, game-free `format-check`/`test`, and final `build` pass with
  380 tests and zero build warnings/errors. The DLL SHA-256 is
  `32f889abbe24d7b26e292c7f63303035ba4c890340547388004bcd9f5186770d`; its MVID is
  `e856b568ca5241d9a63ad1464c9595b8`. A local `0.1.0` tag identifies this
  checkpoint, not a published or runtime-approved release. CI is configured
  for format checks and pure tests on Linux and Windows using the same quiet
  project commands; game installation is no longer required for source checks.
  The YAML was parsed locally. Neither Windows execution nor GitHub Actions
  execution has occurred in this session, since nothing was pushed.

Defaults remain unchanged by Andreas's decision from step 4. Native runtime
comparison, gas pixels, tower exit/rescue interaction and deferred-letter playback
remain outstanding on this candidate. No running game was stopped or overwritten,
and no build, tag or commit was published or sent.

### Native follow-up during tower feedback checks, 2026-10-12

Fresh public 1.6 startup reproduced two signature-resolution defects that the
reference build did not catch. GasVisibility's AnyGasAt lookup was ambiguous
between native overloads, and the new rescue-menu lookup included inherited
GetSingleOptionFor overloads. Both now bind explicit parameter signatures.
Tower patch-target lookup is protected per method, so an unavailable target
cannot abort installation of the remaining tower hooks. The final native launch
has no Total Fog patch failures or runtime exceptions; the existing linked-graphic
XML configuration warnings remain. This is startup/order evidence, not a gas
pixel comparison or full acceptance of the tagged review payload.

### Documentation follow-up, 12 October 2026

The source has since moved to the shared `Current/` payload for 1.6 and 1.7.
Later local candidates pass native tower orders, wall-link/building checks,
blueprint presentation, lookout facing, debug fog commands and paused fog
rendering. Their exact hashes and limits are in VALIDATION.md. The tracked DLL
remains an earlier review snapshot; the README now requires building a source
checkout before installing it, rather than implying it follows every source edit.
GitHub's [source-check run for `537762b`](https://github.com/pardeike/TotalFog/actions/runs/38312341294)
passes formatting and pure tests on both Linux and Windows. This supersedes the
earlier lack of executed CI evidence, without claiming Windows game testing.
The earlier responses' native safety, gas-pixel, deferred-letter and broader
performance gaps are retained in the current coverage checklist.

## Review response 2

### Step 1: safe tower hooks and isolated deferred replay

Verified R2-1 at `TowerMoveOrders.cs:173,239` and R2-7 at
`PendingQueue.cs:17-37` against `6207fc9`. Both reproduce in source: the
transpilers throw after changing/yielding instructions, and replay exceptions
leave the payload queued and skip later items. Tower replacements now check the
complete incoming shape with `PatchSafety.Matches` before editing; unsupported
shapes return the incoming body unchanged. A failed notification is logged once
and dropped while later ready entries continue. The pure replay regression test
also retains a hidden entry and verifies no duplicate replay on the next drain.
`./scripts/mod format` and a Release `./scripts/mod build` pass all 405 pure
tests with zero warnings/errors. Output was redirected to ignored
`artifacts/review-round-2/Assemblies/`; the tracked DLL is unchanged. Native
patch reapplication with Achtung remains a separate runtime check.

### Step 2: consistent sight updates and initialized faction discovery

Verified R2-2 at `CompSightSource.cs:253-256` and the side-effect path at
`MapVisibility.cs:473-586`. A callback failure interrupted the difference before
the mask swap. A swap alone would leave the unprocessed cells inconsistent.
The difference now completes all count callbacks, preserves the first exception,
and swaps in `finally` before rethrowing. This also covers source removal. The
callback contract is explicit: count mutation precedes external side effects.
The pure regression covers a throwing listener, unchanged subsequent refresh,
overlap retention, removals after failure and a complete clear.

R2-11 also reproduces at `MapVisibility.cs:493-500`: secondary discovery was
written before initialization. Secondary discovery and discovery side effects
now require `Initialized`; transient counts retain their existing behavior.
Formatting and the redirected Release build pass 406 pure tests with zero
warnings/errors. The tracked DLL remains byte-identical to the starting tree;
native listener-failure and Multiplayer initialization controls remain open.
