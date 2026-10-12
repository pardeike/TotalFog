# Total Fog

Read ../steam-mods/AGENTS.md and its development/testing guidance.
Preserve inherited attribution, license, frozen historical version folders and
the active portable textures. The public upstream-baseline tag owns the original
source/payload; do not keep redundant upstream scratch files in the active tree.
Build with the .NET 10.0.301 SDK pinned in global.json. The quiet workflow uses
the existing SDK install under ~/Library/Application Support/dotnet when present,
including for nested companion builds. Gameplay and companion DLLs still target
net472 for RimWorld; independent tests run on net10.0. Krafs.Publicizer 2.3.2
prepares build-only references in obj/, with virtual members excluded. Keep
native virtual access levels, including protected job/drawing overrides. The
companion reuses the main DLL's internal access-check attribute through its
existing InternalsVisibleTo contract. Canonical workflows default NuGet output
to artifacts/nuget so another project's publicised cache files cannot change our
reference resolution; an explicit NUGET_PACKAGES overrides this for CI/probes.
About/About.xml advertises only RimWorld 1.6. Its LoadFolders.xml entry loads
Current/ and LegacyAssets. Current/ is a declared project exception to the
steam-mods standard's version-numbered active folder: keep this folder, the
1.6 reference package and .steam-mods.json rimworldVersion together. Do not
relax the shared validator. Folders below 1.6 stay frozen.
1.7 support is withdrawn pending an available reference package and renewed
native acceptance. Preserve dated experimental evidence without treating it
as current support. A future 1.7 support decision belongs to Andreas.
For source-only review builds that must preserve the tracked release DLL, set
`TotalFogBuildOutput="$PWD/artifacts/review/Assemblies/"` when running
`./scripts/mod build`. The companion references that build output directly.
Use the canonical quiet command `./scripts/mod test|build|verify-binary|deploy|package|setup|baseline|benchmark`.
Every gameplay source/build-input commit must carry a matching Release DLL and
Current/Assemblies/TotalFog.build.json receipt. The canonical build writes the
receipt only after compilation succeeds. CI checks it against the checkout and
DLL before rebuilding, so a fresh CI build cannot conceal a stale tracked file.
The snapshot uses normalized text hashes, not Git commit age: documentation-only
commits and Windows CRLF checkouts do not invalidate it, while changed, added or
removed compiled inputs do. Explicit source-only review batches may redirect
output, but refresh the tracked pair before completing/pushing the batch.
Use `./scripts/mod metadata` for installed About/About.xml, LoadFolders.xml and
README.md changes without rebuilding or replacing gameplay/companion DLLs.
The target game must be stopped. Set RIMWORLD_MOD_DIR to the intended Mods root;
both macOS executable names and the external Mods layout are guarded.
`./scripts/mod test` runs formatting checks and pure tests without a game install
or gameplay/companion DLL changes. CI uses `python scripts/mod verify-binary`, then
`python scripts/mod build` with output redirected to artifacts on Linux and Windows. Native deployment/runtime commands
retain their macOS Steam/GABS requirements; CI is not native compatibility proof.
The pinned CSharpier and Ruff formatters own active C# and Python layout. Run
`./scripts/mod format` after edits; build checks formatting before compiling.
`./scripts/mod format-check` checks without changing files. Frozen historical
payloads are excluded. Do not add an auto-modifying commit hook.
Cancellation prefixes default to `Priority.Last` through `PatchSafety.Patch`,
so other mods' ordinary prefixes run before Total Fog can skip the original.
Specify a different priority only for a proven ordering contract. Shared fog/
tower targets have one installation owner in `TotalFogMod`; keep that order
explicit instead of adding another registration in an adapter.
Use `./scripts/mod source-publish` to push committed source to
https://github.com/pardeike/TotalFog. It verifies the public repository and
remote commit without building, tagging, releasing or updating Steam.
The public development branch is `public-main`, tracking `origin/main`.
The earlier local `total-fog` branch retains the private development history;
do not push it or other private branches/tags. The public `upstream-baseline`
tag retains the original payload used by baseline comparisons.
After a completed paired runtime comparison, use
`./scripts/mod feedback-verify <comparison label>` to capture fresh native
Symbiant pixels/interaction and all four 400/4,000-cell render-cost matrices.
It also checks actual enemy raid/manhunter arrivals with Silent Raids off/on,
ordinary slowdown and incident parameter restoration after failure/exception.
It repeats seven native danger-area warning controls, including Symbiant
root/core sight disagreements and preservation of simulation cache entries.
It repeats 21 Albino scream controls using native AI and Normal playback,
held waiting victims, sight/audio changes, damage-job continuation and expiry.
It repeats sixteen native explosion camera controls for engine and Zombieland
producers, with heat and completed blast-cell processing retained.
It checks fourteen native contamination sight/refresh controls with paired
pixels, including hidden mutations, reopening, zoom return and clearing.
It also verifies that all five fog audio modes preserve global creepy ambience.
Use `totalfog/stream_feedback` on a paused initialized paired map for the changed
blueprint/corpse/lighting/door/highlight controls and portable camera
screenshots. It stages temporary native objects and reloads the unchanged save.
Inspect each returned row and the actual pixels; this is not a natural incident
or full corpse-resurrection test. The companion remains outside player ZIPs.
The following artwork/access tools retain historical prototype controls; use the
production watchtower tools below for current gameplay acceptance.
Use `totalfog/watchtower_access` for the separate non-shipping footprint/ladder
prototype. It checks four solid 3x3 edifices, exterior approach pathfinding,
120-tick native climbs/descents, reservations, blocked entry, interrupted-ascent
cleanup and downed retrieval followed by native bed rescue. It reloads the
unchanged named fixture after advancing native ticks. Its immediate interruption
cleanup is not the production descent policy. This prototype does not establish
automatic rescue, blocked exits, save/load, collapse, sight or combat; their
production evidence is recorded in WATCHTOWER.md and VALIDATION.md.
Use `totalfog/watchtower_visuals` for the first new 3x3 tower artwork review.
It stages eight test-only wood/granite native buildings and four native colonists,
captures a combined overview and tight empty/occupied and straight/corner views,
checks all four ladder orientations, advances no ticks and reloads the unchanged
named fixture. The drawing code and definitions stay in the companion. It proves
visual geometry only, not climbing, placement, breaching, sight or combat.
The compact deck fixture also verifies four cached native wall overlaps and
that every occupant's logical position remains the tower center cell. Wall
fragments preserve native Print materials/vertex data and are cached before
capture; their extra draw calls are reported per tower. This does not establish
modded-wall rendering or automatic neighbour invalidation.
The accepted colored-mesh tower remains the default. Optional `nativeFrontPieces`
copies real native wall-front pieces using translation/clipping only, with two
unchanged designs beside six experiments. It checks source UVs against map-space
positions to reject scaling/shearing. Inspect the pixels as well. Side faces/deck
remain unchanged; custom wall shapes/shaders and styled-wall selection are unverified.
Optional `silhouetteOutlines` compares six studies with two unchanged controls:
heavy outer contours, broad shaded faces, soft beam bevels and lighter native
wall seams, informed by Andreas's three building references. Keep the accepted
artwork as the default until Andreas accepts a change. This option adds no
material passes; inspect the actual pixels rather than accepting mesh assertions.
Set `retainControls=false` for a larger overview with the study on all eight
towers; the default keeps two unchanged designs for comparisons.
Set `materialComparison=true` for one labelled board of ten useful materials,
each with matching native walls on both sides. This mode uses the current
outline study throughout, joins contour/beam ends with rounded cached geometry,
and distinguishes timber braces, masonry courses and smooth metallic faces.
It temporarily disables the native lighting overlay for colour comparison,
restores that setting and reloads the unchanged fixture. It cannot be combined
with `nativeFrontPieces`. Inspect actual pixels and keep reference screenshots
in ignored artifacts; this board does not test occupants or gameplay.
It checks exact measured/deployed gameplay bytes, preserves superseded proof,
and refreshes the feedback package gates without rebuilding the candidate.
Use `./scripts/mod feedback-package` for a private paired Total Fog/Zombieland
test ZIP. It runs independent tests, verifies the preserved native pixel/targeting
and render-cost matrices against the exact deployed gameplay DLLs, and packages only
the public active game payload without companion instrumentation.
It also creates separate complete Total Fog and Zombieland ZIPs, including music.
Recorded companion hashes identify the native proof instrumentation; companions
are excluded from delivery and adding a probe does not invalidate identical
tested gameplay DLLs.
Use `./scripts/mod runtime-benchmark <label> <save name> [Normal|Fast|Superfast|Ultrafast]`
for three native samples on the currently installed binary. Each sample starts
in a fresh game process, reloads the unchanged fixture, verifies window focus,
and preserves its Player.log. Leave other desktop apps idle during timing.
Use `./scripts/mod runtime-compare <label> <save name> [Normal|Fast|Superfast|Ultrafast]`
to build once and alternate three original/candidate pairs in fresh processes.
It checks matching fixture/settings/rendering evidence and exact DLL bytes,
fails if candidate median TPS is lower, and leaves the candidate deployed.
The runner also verifies the actual camera rectangle and, when present,
Zombieland's DLL bytes, scalar settings and configuration-file hashes. Camera
zoom and public engine version/MVID must also match. Zombie population endpoints
are counted outside playback even with zombie work telemetry disabled. The
runtime runner also rejects missing native logs and errors recognized by
Zombieland's existing log summarizer, preserving the native result/log first.
Combat Extended comparisons also match its exact loaded DLL/MVID, effective
scalar settings and configuration-file hashes outside the measured interval.
Benchmark loads temporarily enable native Pause on load and restore it without
saving preferences. Pausing only after visual readiness can advance a different
number of native ticks on each binary; comparison warmup endpoints must match.
Set `TOTALFOG_WARMUP_TICKS=300` to request a predeclared 300-native-tick warmup
(0..600, default 0). Set `TOTALFOG_PROFILE_ZOMBIES=1` with the Zombieland profile
to record its existing selected/actual zombie work, priority/remote service,
population and saturation telemetry. This adds instrumentation and is a
diagnostic comparison, not uninstrumented performance acceptance. Declare the
warmup before running either variant; retain cold-start failures separately.
Set `TOTALFOG_CONTAMINATION_OVERLAY=1` with the Zombieland profile for the
predeclared distant overlay comparison. It stages 4,000 sparse ground cells at
0.65 in a 159x99 area, frames that whole area, and keeps each mod's ordinary
visibility policy. The unchanged save is never written. Overlay mode, input,
camera and settings must match; this is whole-game TPS, not equal-geometry GPU
cost. Paused matched-visibility ground/item cost uses `contamination_render_cost`.
Runtime measurements default to ordinary player speed: forced speed is off and
the bridge's private UltraSpeedBoost default is explicitly disabled. Every
measured native tick must retain the requested time-speed selection. Use
`TOTALFOG_FORCE_SPEED=1` only for a separately labeled debug stress comparison;
its result does not establish ordinary player fourth-speed performance. Both
mode flags are retained and matched across original/candidate samples.
Native tick-rate multipliers are recorded separately because RimWorld can force
normal speed while the selected speed remains Ultrafast. A rate-limited sample
does not prove sustained nominal fourth-speed throughput.
For a small wide-zoom sample, set `TOTALFOG_WIDE_VIEW=1` and
`TOTALFOG_COMPARISON_PAIRS=1` with runtime-compare. It centers the map at camera
root size 100 through the session-only bridge zoom extension, records actual
visible map cells and pawn root cells in view, and restores the prior camera
and extension. Wide contamination input is centered in the map. Paused wide
screenshots are captured before timing. One/two-pair results are spot checks:
they cannot satisfy the three-pair feedback performance gate. Keep the default
three pairs for the existing performance-floor acceptance.
Verify gameplay screenshots and semantic evidence with
`./scripts/mod verify-rendered artifacts/<evidence-folder>`. This uses Pillow
from artifacts/pixel-analysis-env when present, otherwise the current Python.
The isolated 1.6 baseline savedata is artifacts/UserData, normally associated
with GABS total-fog-16. Its configured SteamManaged launcher now follows Steam
1.7 and is not a valid 1.6 launch without an explicit temporary reconfiguration.
The installed offline 1.6 engine can run Core-only checks; its expansion symlinks
point at the removed old Steam app path. Repair using actual 1.6 DLC content
before all-DLC gates, never substitute 1.7 expansion data. Verify the actual
engine version and loaded package IDs, not merely ModsConfig.xml. Review round
2 temporarily used DirectPath and Core-only content, then restored the original
GABS entry and isolated config. The intended full loadout remains Core, five
DLCs, Harmony and Total Fog; RimBridgeServer is test instrumentation. For a bug in
Andreas's running game, reconnect to that existing GABS game instead of launching
a second instance or replacing its mods/settings. The current ordinary Steam
game is `rimworld` on 1.7; discover its state before using it. Preserve the active
save and loadout. A focused 1.7 result does not refresh the 1.6 comparison or
optional-mod gates.
Use `totalfog/fog_mesh_equivalence` on a paused map for independent vertex-alpha
checks. `verifyDrawn=true` also draws every layer and compares uploaded colors,
exercising paused fade completion. The default checks targets only; a target
pass alone cannot rule out stale rendered colors. This is companion
instrumentation, not player gameplay code.
Use `./scripts/mod dpa-setup` to verify the installed Steam Dubs Performance
Analyzer supports 1.6 and enable it in this isolated profile. Profile through
RimBridgeServer's `dpa_*` tools, then stop and clean up instrumentation before
performance comparisons. DPA is excluded from feedback ZIPs.
Set `TOTALFOG_GAME_ID` to another configured Total Fog profile to profile that
loadout, including `total-fog-zombieland-fallback-16` for Zombieland without
Total Fog. `./scripts/mod dpa-setup off` removes only DPA from the selected profile
after native stop/cleanup; retain its original active-mod list as evidence.
DPA setup changes configuration only and never redeploys a tracked release DLL.
Use `./scripts/mod ce-setup` for `total-fog-ce-16`, the separate Steam profile
under artifacts/CEUserData with installed Combat Extended and all DLCs. It
preserves the core performance profile and existing CE settings/loadout.
Use `./scripts/mod mp-setup` to configure isolated public Steam 1.6 host/client
profiles `total-fog-mp-host-16` and `total-fog-mp-client-16` under
artifacts/MPHostUserData and artifacts/MPClientUserData. It verifies installed
Multiplayer/Prepatcher support, preserves existing profiles and does not build
or deploy. Each profile uses its own GABS endpoint and tracked SteamManaged PID;
do not add a shared process-name stop fallback. Startup alone does not establish
Multiplayer compatibility.
After each launch of the pair, run `./scripts/mod mp-layout`. It identifies the
two processes by their isolated savedata arguments and verifies host-left,
client-right half-screen windows on the main display. It does not build or
change gameplay state.
Use `totalfog/multiplayer_snapshot` to compare read-only fog state only after
both native status receipts show the same paused tick. A submitted pause is
asynchronous and does not establish a comparison boundary. The settings tool
uses the same native MPAPI watcher as the UI; verify the applied value on both
clients. Never run single-player mutation fixtures on only one connected client.
For tower checks, register `totalfog/multiplayer_watchtower` ALONE on both main
menus in a fresh session, with equal appended handler IDs. Do not mix its saves
with the older four-probe registration sequence. Setup/collapse/cleanup are
synchronized commands; watch/descend invoke the production float-menu action
outside simulation to exercise Multiplayer's native ordered-job serializer.
Compare native actor/tower state and complete fog snapshots at matching paused
clocks. Its status resolves saved native objects by ID after rejoin. A lethal
fall must be checked through `multiplayer_pawns`, which also finds corpse inner
pawns. The settings probe supports all four tower settings with the UI's ranges.
Cold live rejoin is separate from loading a saved replay, separate factions and
cross-platform acceptance. Preserve known loader errors in the evidence.
Use `totalfog/multiplayer_hearing` to read native cue recipients and per-map RNG
states. Cues are read from the native drawing collection, not ListerThings.
The settings probe accepts the UI's BaseHearingRange range of 0..30; compare
applied values and cue state at a matching paused native map clock.
For recipient/save/replay checks, explicitly register
`totalfog/multiplayer_notifications` on both main menus before loading or
hosting. Require the same returned handler ID and preserved existing command IDs.
Its queue action sends a native synchronized command; status is read-only.
These probe saves require the same opt-in instrumentation to replay their commands.
The companion and probe are excluded from player ZIPs.
For the native caravan lifecycle control, register
`totalfog/multiplayer_caravan` SECOND on both main menus after notifications.
Require equal appended IDs and unchanged existing IDs. Its transfer action uses
a synchronized world command and native caravan exit/entry, skipping travel time.
Compare registrations and sight/hearing/observation deadlines against the
destination map's clock, then play that map normally. Saves with this probe also
require its handler when loading/rejoining. Never use a single-client teleport
as Multiplayer transfer evidence.
For world-context delayed letters, register notification action `register-world`
THIRD on both main menus after those two probes, then use `queue-world`.
Its primitive map ID preserves native world-command timing. Check the queued
deadline against the target map's clock, not the world clock, before normal
playback. Saves containing this third command require all three handlers.
For a bounded enemy attack-pipeline control, register
`totalfog/multiplayer_enemy_combat` FOURTH on both main menus. Its setup,
attack, reveal and cleanup use native synchronized world commands. It uses
hostile ancients and rejects missing faction prerequisites before submission.
Compare vanilla eligibility with enemy fog off/on, then hidden and visible
ordinary playback. This is not autonomous target-selection coverage. Clean up
owned objects before saving; these probe saves also require handler four.
Its `setup-ai` and `acquire` actions stage a drafted unarmed player pawn and run
the native enemy target finder with ordinary flags and only that owned candidate.
Compare fog off, hidden fog on, then restored sight on both clients. This is
target-acquisition coverage, not autonomous raid-job coverage.
Its `block`/`unblock` actions spawn/destroy eleven native steel walls across the
owned firing lane through the same synchronized command. Neither forces a fog
refresh. Use ordinary native playback, then compare paused clocks, blocker state,
target visibility and complete fog snapshots on both clients. The fixture uses
finite stationary combat-wait jobs with automatic shooting disabled; explicit
`attack` still issues the native attack job. Clean up before saving. This proves
native blocker lifecycle, not construction jobs or explosion destruction.
Use `totalfog/multiplayer_cell` for a read-only native building/door and fog
blocker/sight comparison at a matching paused boundary. It never creates grids.
Native coverage now includes shared-colony time, separate colony factions and
independent map clocks, recipient-private queues, cold rejoin, native caravan
transfers, player/enemy rifles, target acquisition and a short fourth-speed run.
These bounded results do not complete Multiplayer acceptance; use the current
remaining-checks table in docs/COVERAGE.md and match each result to its recorded
gameplay bytes. Do not reopen passed slices merely because an older checkpoint
listed them as outstanding.
Native MP save loading enters replay mode. The companion's host_local can resume
hosting that loaded replay; it rejects an existing live session. Inspect status
for completed hosting/join and compare fog only at matching paused ticks.
Use `totalfog/ce_ammo_fixture` for the scoped no-magazine short-bow control.
It observes CE's own attack orders, ammunition preparation and shot results;
play through ordinary Normal playback. Remove/recreate its fixture sight source
to change sight during warmup, then call cleanup to restore the temporary base
range and remove owned objects/probe patches. It is companion instrumentation,
excluded from player ZIPs, and does not support save/load.
Use `totalfog/ce_turret_fixture` trace-start/trace-stop to observe bounded native
burst fallbacks before and after Total Fog, base CE projectile impacts and
applied damage from the fixture turret. Each stream holds at most 128 records;
reject an overflowing diagnostic. The trace preserves native results and is
not used during performance acceptance. Stop traces and remove owned objects
before other tests. configure-aim-mode uses CE's native toggle. fallback-policy
is a readonly loaded-guard contract with supplied boolean inputs; it does not
prove that CE naturally executed the still-Thing fallback branch.
Use `./scripts/mod zombieland-setup` for `total-fog-zombieland-16`, the isolated
Steam profile under artifacts/ZombielandUserData with Total Fog, local Zombieland
and all DLCs. This builds/deploys both mod/companion pairs and verifies their
bytes before registering the profile. It preserves the core and CE test profiles.
Use `./scripts/mod ce-zombieland-setup` for the separate
`total-fog-ce-zombieland-16` profile under artifacts/CEZombielandUserData.
It reuses the paired build/deploy path, validates installed CE and loads CE before
Zombieland, preserving the standalone test profiles. Only a new combined profile
is seeded with existing CE preferences/settings; subsequent settings are retained.
Missing Zombieland-profile preferences are seeded from the core test profile;
existing preferences remain untouched.
Use `./scripts/mod zombieland-fallback-setup` to register a separate Steam
profile without Total Fog under artifacts/ZombielandFallbackUserData. It verifies
the already-built/deployed Zombieland pair without rebuilding. Use
`zombieland-setup` first after gameplay source changes. Existing paired settings
and loadout remain separate. Create a fresh no-fog fixture in this profile.
To benchmark its saved horde fixtures, set
`TOTALFOG_GAME_ID=total-fog-zombieland-16` with the existing runtime-benchmark
or runtime-compare commands. Comparisons still alternate fresh original/candidate
processes and verify matching settings, rendering, loadout and saved fixture.
Create comparison saves under the inherited binary: Total Fog imports its old
types, but the inherited binary cannot read saves with rewritten Total Fog types.
After selecting targets with `rimworld/dpa_patch_methods`, capture a traced
interval with `totalfog/dpa_playback`. It resets, plays, snapshots and stops
inside the bridge so external request delays do not add paused frames. Confirm
cleanup with `rimworld/dpa_status` before further comparisons.
Do not publish or send a build unless requested. Keep local engine references,
diagnostic media, and generated reports in ignored artifacts.

The active projects are in Source/TotalFog, Core, Tests, and BridgeTools;
Source/TotalFog.slnx opens all four. Upstream source and comparison assets are
preserved in the public upstream-baseline tag. The active payload uses portable
LegacyAssets/Textures PNGs, with no inherited AssetBundle. Baseline staging restores
the original bundle and LoadFolders.xml from that tag, adding the same portable
LegacyAssets texture fallback used by the candidate on macOS. Original gameplay
DLLs and XML type bindings remain unchanged. The tests source-link
visibility/lifecycle code with a small engine boundary;
live BridgeTools scenarios prove actual registration and render behavior.
Use TotalFog namespaces and independently named runtime types. Preserve frozen historical payloads.
Legacy save import belongs only in Compatibility/LegacySaveTypes.cs; never add old namespace aliases.
Historical version folders are frozen and excluded from the active ZIP.
The private feedback ZIP includes PRELIMINARY.md. The quiet build explicitly
disables incremental compilation so a restored tracked release DLL cannot be
mistaken for a fresh candidate. Compare packaged and live-tested DLL hashes
before sending a feedback build.

Shared Languages/ is intentionally loaded through the root across historical
versions. Public source publication is separate from a mod release. There is no
Steam release workflow or Workshop item yet; PublishedFileId 0 is the unpublished
sentinel.

Performance has priority over optional features. The original mod is the minimum
runtime baseline; aim to improve it with matching save/settings/hardware evidence.
Exact visual-memory snapshots are retired. Do not reintroduce GPU capture,
material interception or drawing archives into gameplay to support that feature.

## Production watchtower tools

Use `totalfog/watchtower_gameplay` in the paused `TotalFogRegression` fixture for
the production tower access/sight/bullet/rescue/collapse slice. Its historical
`watchtower_access` and `watchtower_visuals` tools remain prototype controls.
Use `totalfog/watchtower_orders` for the smaller native facing/duty/wall-link
slice: four rotations, actual float-menu actions, native draft changes, timed
expiry, save/load and an ordinary drafted move after descent. It stages a
temporary area and restores the supplied unchanged paused single-player save.
Its wood/granite wall checks do not establish every modded wall shader.
The expanded production suite checks climb/descent/flight/carrying persistence,
temporary rescuer and corpse persistence, blocked exits, interrupted rescue,
recovery, death and projected corpse retrieval. Use `totalfog/watchtower_combat`
for native weapon producers, targeting, tall blockers, melee, projected bullet
rotation, grenade ground shadows, blueprint/ghost graphics, attached effects,
fire eligibility and lightning controls. Read native attention and preserve the
Player.log after every run; reference-package accessibility can differ from the
running game, and a probe exception must never be accepted as a mod result.
Use `totalfog/watchtower_safety` for carried patient/body drop and fall injury
during carrier collapse, downing and death. The combat suite includes native
shield absorption/drawing, moving particles and loose injury effects. The gameplay
suite includes actual camera-edge drawing; optional `performanceSample=true`
captures four short close/wide intervals and restores private speed boost.
It stages quiet native waiting pawns and allows combat slowdown to expire without
forcing the engine rate. The unchanged base save restores their prior state.
Inspect the actual tick-rate multipliers before treating an interval as fourth
speed; native normal-speed limits can remain active despite Ultrafast selection.
Use `totalfog/watchtower_projectiles` for partial-climb rear/ladder-side firing,
actual base interception, native frag flight/fuse and, when Zombieland is loaded,
its actual spinning projectile drawing. Use `totalfog/watchtower_building` for
native blueprint/frame completion, automatic wall-cache invalidation and a
supplied-target native breach attack. It prefills materials and supplies the
breaching target/lord; it is not hauling or autonomous raid coverage.
Use `totalfog/watchtower_ce` in the paused `TotalFog_Tower_CE_Native` fixture
under `total-fog-ce-16` for native collision heights, tall obstacles, actual
automatic outgoing and ordered incoming short-bow producers, projected arrows,
in-flight save/reload and descent. Generated targets are unarmored and capable
of violence; this is geometry/damage coverage, not CE armor or every weapon.
Compare reload against the uninterrupted reference tick because native
Pause-on-load advances one tick. Preserve the native launch/impact evidence.
Use `totalfog/watchtower_zombies` in the paused paired tower fixture for native
wall-push eligibility with an ordinary-wall control, melee exclusion, normal/
tanky/bomber structure eligibility, actual supplied-target zombie structure
damage, spitter center/ladder landings and bomber collapse. Spitter targets are
supplied; its Shoot, flight, impact and spawn remain native. Run the bomber in
ordinary playback: Zombieland's scheduler/explosion queue is outside single-tick
stepping. This does not establish autonomous spitter or tanky route selection.
These suites do not complete other combat producers,
hauling/autonomous raids, optional mod or original-baseline performance
acceptance; track these in WATCHTOWER.md
and VALIDATION.md. Never substitute the old test-only projection for production
rendering or change native projectile ExactPosition to fit the artwork.

## Blocked integrations with other mods

First establish the affected function, a minimal native reproduction and why a
correct, stable and performant fix cannot reasonably live in Total Fog. A
missing test or a fixable Total Fog bug is not an upstream blocker.
If the blocking behavior belongs to Combat Extended or another mod we cannot
edit, find its maintained GitHub repository, check for an existing matching
issue and file an issue there, or add the new evidence to the matching issue.
This instruction authorizes the corresponding upstream issue once that blocker
is established.
Include the public game/mod versions, minimal reproduction, expected and actual
behavior, the responsible function and why a local workaround is unsuitable.
Link to Total Fog's public integration documentation and relevant source at the
published commit, and provide a short example showing the proposed fix through
our optional API. Bind delegates once, keep the other mod usable without Total
Fog and preserve native simulation and target checks. If the current API cannot
express the fix, say so and propose only the smallest missing contract.
Use public, minimal evidence. Keep private conversations, saves, diagnostics,
credentials and confidential engine material out of the issue and repository.
Record the issue URL and outstanding checks in docs/COVERAGE.md. Compatibility
remains unverified until our own native checks pass; an issue or upstream fix
alone does not complete validation.
