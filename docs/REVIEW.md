# Total Fog 0.1.0 review

Independent review of the first version, 12 October 2026, commit 12e30f6.
Four read-only passes: sight engine, information-leak surface, watchtower,
settings/compatibility/process. Severity is judged for a first public version:
small stuff is deliberately left out. Line numbers refer to commit 12e30f6.

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
