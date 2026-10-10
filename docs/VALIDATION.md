# Local validation

This is the retained evidence ledger. Read dated checkpoints with their exact
gameplay hashes, loadouts and stated limits: older test counts, “current” builds
and outstanding lists refer to their checkpoint, not today. Rejected candidates
remain recorded. For the current acceptance summary and remaining work, use
[COVERAGE.md](COVERAGE.md#current-status-7-october-2026); for implementation rules,
use [ARCHITECTURE.md](ARCHITECTURE.md). The 6 October delivered ZIPs are a separate
snapshot from later source commits.

## Symbiant cell clipping, 2026-10-05

The production integration fixes both mature-body defects retained below. The
same hostless 18-cell, one-patch fixture matures through 61 real ticks, from 3 to
64; all ten measurements remain paused at 64. Range 5 and disabled fog fading
make the explored hidden interiors testable. The optional draw gate uses current
cells independently of root-based live information. Zombieland clips its native
body/core quads while preserving shader mask coordinates and native registration.

| Native case | Measured result |
| --- | --- |
| Root visible, half the body visible | 13 native DrawAt calls, eight clipped body triangles; hidden far-body interior is pixel-identical to body-off. |
| Root hidden, opposite half visible | 13 DrawAt calls and eight body triangles; no root overlay/core geometry, and the hidden root/core crop is pixel-identical. |
| Whole body hidden | Zero native draw/phase/overlay calls; full crop is pixel-identical. |
| Vanilla-fogged root, visible far end | Far body still draws; hidden root/core crop remains pixel-identical. |
| Body off-camera | Native camera rejection remains effective; zero draw/phase calls despite nine visible body cells. |
| Forced GPU fallback, core visible | The clipped core cue produces 258 changed pixels; hidden far-body crop remains identical. This is a forced path check, not device-level fallback acceptance. |
| Forced fallback, core hidden | No core triangles or changed pixels; no unfiltered base pawn graphic is drawn. Full visible fallback body support remains open. |
| Rotating core across sight boundary | Core clips to seven visible cells out of nine bounded cells, producing seven triangles; the checked hidden body and core interiors remain identical. |
| Released resources, hidden root/visible end | Native drawing rebuilds the patch, producing the same visible far-body pixels while the hidden root/core crop stays identical. |
| Return after fallback/resource rebuild | Normal GPU body/core drawing resumes with the correct visible and hidden crops. |

Every emitted geometry sample has zero hidden triangle centers and invalid UVs;
registration remains exactly one. Root inspection/selection policy is unchanged.
The final pixel report has 104,650 and 110,087 changed pixels for the two visible
body ends; checked hidden interiors have exactly zero difference, not merely a
thresholded difference. Evidence is `artifacts/symbiant-boundary-final.json` and
`symbiant-boundary-final-pixels.json` plus their native cell-rect screenshots.
The scenario restores sight/diagnostic settings and reloads the unchanged save.

A fresh checkpoint repeats all ten cases after the final companion cleanup guard.
The initial repeat correctly found the pulsing core wholly inside one visible
cell; its diagnostic boundary assumption failed without any hidden pixels.
The scenario now stages an existing core handoff at 50% between one visible
and one hidden cell, rather than relying on real-time rotation to cross an edge.
The corrected handoff clips three shown cells out of six bounded cells into
four triangles. All ten native cases and the exact hidden-interior pixel checks
pass again. This is a paused midpoint rendering check, not moving-core gameplay
acceptance. Evidence is `artifacts/symbiant-boundary-handoff.json` and its
`-pixels.json` report; the prior checkpoint evidence is retained separately.
All four built/deployed main/companion SHA-256 pairs match in
`symbiant-boundary-final-build-pairs.json`. The retained native
`symbiant-boundary-final.log` has an empty recognized error summary. Paired
Zombieland SHA-256 is
`3d1683e6e6a2e145ceb8bd4bdc8acca954e1a13a1ba2ece3142e9f82c9a2b514`.


Six additional independent test rows cover a custom footprint disagreeing with
root sight in both directions, native cull rejection, unrelated exact types,
failed-provider suppression/replacement, and vanilla-fog/bounds/bypass cell
policy. The quiet paired build passes all 229 tests. The callback binds once;
ordinary zombies add an exact-type lookup without per-pawn reflection or new
collection/delegate allocation. Astra's source review found and removed a
three-argument Mathf.Max params-array allocation in the patch padding calculation.
Native zero-allocation and whole-game performance require separate evidence.

The required six-process 1,000-zombie comparison then fails the original-mod
performance floor. It alternates O/C, C/O, O/C in fresh processes with the same
250x250 save, 1,070 starting pawns, Core/all five DLCs, matching effective fog
settings, Metal 4592x2646, vSync 1/target 60 and verified focus. DPA is absent.
Original median TPS is 288.4108; candidate is 266.1424, a 7.7211% decrease.
Mean whole-tick elapsed medians are 2.4741 versus 2.6717 ms; frame-p95 medians
are 77.7553 versus 79.3631 ms. The paired TPS results are 290.5298/266.1424,
267.2726/245.0007 in C/O order, and 288.4108/252.2392. The runner returns
nonzero, preserves all six reports/logs and restores the candidate. This is
failed acceptance, not a delivery candidate. Evidence is
`artifacts/comparison-symbiant-render-gates-1000.json` and its runtime reports.
TF candidate SHA-256 is
`58f70d68c8bb9b16f6f17e9168b4d430fc6fe3bab0766ae5e8e27caf0091c618`.

This cold-start comparison has no simulation warmup and records no Zombieland
scheduler/actual-zombie-tick accounting or actual camera rectangle. Zombieland's
adaptive scheduler responds to elapsed update/frame pressure, so identical
initial saves do not establish equal executed zombie work. The earlier positive
comparison does not cancel this failure or prove that the new dispatch caused
it. Across the two comparisons the unchanged original binary varies materially.
The next gameplay comparison needs predeclared warmup, both mod identities,
camera/settings and selected/actual zombie work with documented probe overhead.

The retained `totalfog/draw_gate_cost` probe first isolates that dispatch. It
captures real pre-fog native cull inputs on the paused no-Symbiant fixture,
reuses scratch NativeArray storage and alternates the same candidate filter
with the Symbiant provider registered/unregistered. All output draw/shadow flags
match. Seven alternating 300-iteration batches cover 1,119 draw-list entries,
113 surviving native camera/fog/depth entries, and no game-tick advance, 3 to 3.
Median filter time is 0.040653 ms registered versus 0.036959 ms common;
median paired added time is 0.002928 ms. Reset/copy work is included equally.
Registration and diagnostic patches restore in finally. This bounds dispatch
cost on this captured view; it is not an all-on-camera allocation or TPS test.
It rejects the simple explanation that the direct lookup cost alone accounts
for the 7.7% TPS decrease. Timing-sensitive scheduler effects remain open.
Evidence is `artifacts/draw-gate-cost-1000.json`. No lookup cache was added.

## Ordinary player speed comparison setup, 2026-10-06

The canonical native runner previously requested forced speed for every higher
speed and inherited RimBridge's private UltraSpeedBoost startup default. Those
results remain debug stress measurements, not ordinary player fourth-speed
acceptance. The runner now defaults both overrides off, explicitly establishes
the boost state after fresh loading, matches both mode flags across variants
and checks actual selected speed on every measured native tick. Debug stress
requires the separate `TOTALFOG_FORCE_SPEED=1` option.

Native tick-rate multipliers are recorded separately. The installed public
engine's TickRateMultiplier returns 1 when its slower forces normal speed even
if the selected speed remains Ultrafast; ordinary Ultrafast otherwise returns
15, versus 150 with its private boost. A player-settings interval limited by
normal slowdown cannot establish sustained nominal fourth-speed throughput.
Decompiler evidence is `decompiler-public-tick-rate-{resolve,source}.json`.

The first new attempt selects the older Upstream1000 fixture, whose effective
initial grace remains three days. Its original sample loses 897 zombies after
warmup. The attempt is interrupted, the candidate restored and the report
explicitly rejected; no floor result follows. Evidence is
`comparison-action-audio-player-1000.json`, its runtime reports/logs and
`logs/runtime-compare-action-audio-player-old-fixture-interrupted.log`.

The corrected NoGrace1000 attempt verifies the existing unchanged fixture hash
`63421fa9372913bc165bb9708478b3c0d05fc26b527e3bd370dcb609cbed3d26`,
300 native warmup ticks and disabled overrides. The original retains all 1,000
zombies. Its candidate interval completes playback but the native log records
one NullReferenceException in PathGridDoorsBlockedJob.Execute; it ends at 999
zombies. The comparison correctly stops before accepting that sample and
restores the candidate. This is failed/incomplete acceptance, not an established
Total Fog defect or a passing performance floor.

The reported engine region reads pawn.jobs.curDriver.collideWithPawns without
a driver null guard, after checking CurJob. A job-state race is a hypothesis;
the affected pawn and ownership of the change are unverified. Total Fog has no
path-job/job-tracker mutation in this source pass. Decompiler source and real
IL are retained as `decompiler-public-path-grid-job-{resolve,source,il}.json`.
Runtime evidence is `comparison-action-audio-player-no-grace-1000.json`, its
original/candidate reports/logs and
`logs/runtime-compare-action-audio-player-no-grace-path-job-failure.log`.
The six-process repeat with rate accounting completes and passes its bounded
TPS floor. Original medians are 58.0153 TPS, 6.7986 ms mean tick elapsed and
37.7828 ms frame p95; candidate medians are 59.1572 TPS, 5.8273 ms and 36.9436 ms.
That is about 1.97% higher TPS and 14.29% lower mean tick elapsed on this slice.
Every measured tick uses selected Ultrafast but actual multiplier 1. Both
overrides are off, the original/candidate identities match, DPA is absent,
window focus is verified and all six native logs have empty recognized error
summaries. Five intervals retain 1,000 zombies; candidate one ends at 999.
The prior path-job error does not recur in these six intervals, but its cause
remains unverified. A separate quiet horde fixture is required for unrestricted
player Ultrafast acceptance; this rate-limited result cannot substitute for it.
Evidence is `comparison-action-audio-player-rates-1000.json`, all six native
runtime JSON/logs, `action-audio-player-rates-1000-verification.json` and
`logs/runtime-compare-action-audio-player-rates-1000.log`.

The existing original-binary fixture creator then builds Quiet1000 from
UpstreamBase, using seed 711100 and camera 40,40. It retains the same
983 ordinary/17 special type mix, places 100 in the native camera rectangle and
900 outside its protection margin, disables all population cleanup routes and
saves under the original fog types. SHA-256 is
`930f4d7e260685535a5b7b44d684443ad756eab2bcc043e3140744eec132320e`.
An instrumented original-binary preflight uses 300 warmup ticks and eight seconds
of actual player Ultrafast with both overrides off. All 1,000 survive with zero
kills/despawns; selected work equals 364,096 actual CustomTicks. Actual rates are
15 for 16 ticks and 1 for 466 ticks, so recurring slowdown remains even in this
view. The recognized native log summary is empty. This validates population
stability on that interval, not unrestricted player throughput or its cause.
Evidence is `quiet-player-fixture-create.json`, `quiet-player-preflight.json`,
its native log and `quiet-player-preflight-verification.json`. The next named
check was the native TimeSlower trigger. Candidate deployment is restored and
game termination verified after preflight.

The optional bounded slowdown trace in the existing performance companion
records the first eight request stacks and restores its patches in finally.
The candidate requests originate through Verb.TryStartCastOn, TryMeleeAttack
and JobDriver_AttackMelee. The colonist snapshot has two AttackMelee jobs and
one Flee job. Moving the camera did not make this a quiet colony; this rate
limit is real combat behavior. Evidence is `quiet-player-slowdown-trace.json`,
its clean native log and `quiet-player-colonists-after-trace.json`, plus public
native caller/source records. No slowdown/gameplay policy changes; tracing is
off for normal timing.

The existing Zombieland fixture creator adds an optional minimum distance from
actual current colonist positions for visible, remote and fallback candidates.
Zero preserves prior combat placement. A 60-cell gap gives 100 camera/900 remote
zombies with closest squared distance 3,602. The first Gap1000 save is candidate-
authored after baseline staging refuses to overwrite a prematurely started
game. Its preflight uses multiplier 15 but ends at 998. This save remains
separate and is excluded from original comparisons. Evidence is
`quiet-gap-fixture-create.json`, `quiet-gap-player-preflight.json`, its log and
`logs/baseline-quiet-gap-start-order-refused.log`.

After verified termination and successful baseline staging,
`TotalFog_Zombieland_UpstreamQuietGap1000` is created under the confirmed
rimworld-mod-real-fow assembly. It contains two original namespace references
and zero candidate references; SHA-256 is
`5b86ee73d00ac9a05002616485960091dd325e83930f77b87c450fb8effc2b80`.
An eight-second instrumented preflight after 300 warmup ticks preserves all
1,000 zombies, records zero kills/despawns/slowdowns and services all 382,152
selected opportunities. All 2,372 measured ticks use multiplier 15 with both
overrides off; the recognized native log summary is empty. This proves an
unrestricted-speed prerequisite, not the original/candidate floor. Evidence is
`upstream-quiet-gap-fixture-create.json`,
`upstream-quiet-gap-player-preflight.json` and its native log.

The subsequent six-process O/C, C/O, O/C comparison passes the original-mod
floor on the current gameplay pair. Each interval uses 300 native warmup ticks
and 15 seconds of ordinary player Ultrafast; forced speed and UltraSpeedBoost
are both off. Every measured native tick in all six intervals has multiplier
15. Exact original/candidate identities, saved fixture, common fog settings,
Zombieland settings/configuration, loadout, public engine bytes/MVID, camera,
renderer and focus match. DPA and zombie work telemetry are absent.

Original median TPS is 289.7978 and candidate median TPS is 312.1742, a 7.72%
gain. Median mean tick elapsed improves from 2.4408 to 2.2492 ms, 7.85% lower.
Frame p95 is essentially unchanged: 69.6921 versus 69.7293 ms, 0.05% higher.
The three paired TPS gains are 7.13%, 7.72% and 9.76%. All intervals start at
1,000 zombies; endpoints are 1,000 or 999 in both variants. These small population
changes and the bounded fixture limit any general performance claim; this is
not exact work replay, long-session or controlled load-spike acceptance.
All six native logs have empty recognized error summaries. The earlier native
path-job error does not recur here, but its cause remains unverified.
Evidence is `comparison-quiet-gap-player-1000.json`, its six native runtime
reports/logs, `quiet-gap-player-floor-verification.json` and
`logs/runtime-compare-quiet-gap-player-1000.log`.

Before the next handoff, the exact current gameplay pair passes a fresh
twelve-case Symbiant boundary matrix, 54 manual targeting controls and the
preserved pixel verifier. Four dense/sparse 400/4,000-cell runs pass all forty
native render-cost rows: focus retained, one registration, expected cell count,
and zero current body cells in hidden controls. The native log has no recognized
errors, exceptions or warnings. Normal connected-blob shader rendering remains
unchanged. Current evidence replaces the gate files only after actual reruns;
the preceding evidence remains in ignored backups.
Gameplay hashes are Total Fog `43929c0b6913df225ee4b9d71ef264176ed608aecb83dc7dbe5879005e2a93a0`
and Zombieland `20e9a34de151d48ac1ac7bcd6afb0e3427809007bed191ad3ce21415ca600172`.
Companions are `d2188de904bb805a01c235d63776560de39aad2f5bb5ace35b83cbe7399a13d9`
and `92372519b7aef6cfe7f3e6d8a72f211843195705abc578c1c20ed8e90ec1b3a3`.
All four built/deployed files are byte-identical. Evidence lives in
`symbiant-render-cost-acceptance`, the four `symbiant-render-cost-*-green.json`
matrices, `symbiant-render-cost-green-build-pairs.json` and
`symbiant-render-cost-action-audio-final-{player.log,log-summary.json}`.

## Horde work accounting, 2026-10-06

The existing native comparison now records the actual camera rectangle,
Zombieland main DLL/MVID, effective scalar settings and configuration-file
hashes. The optional work probe binds the existing Zombieland telemetry once;
it records selection, actual CustomTicks, camera/priority/remote service,
population, fairness and saturation. A shared diagnostic gate protects the
warmup and sample. Cleanup restores automatic-pause mode, stops telemetry,
removes its patches, pauses and releases the gate. Rejected native results
remain in the runner's report rather than being lost to a later evidence guard.

The predeclared diagnostic uses 300 native warmup ticks, exactly 3 to 303,
then six fresh 15-second O/C, C/O, O/C samples. DPA is absent; zombie telemetry
is enabled in both variants, so this is instrumented diagnosis. Actual camera,
both main DLLs, settings/configuration and the unchanged fixture match. Every
sample's whole-tick count equals its zombie telemetry tick interval, and every
selected opportunity equals an actual CustomTick.

| Variant in execution order | TPS | End living zombies | Actual CustomTicks |
| --- | ---: | ---: | ---: |
| Original 1 | 291.598 | 615 | 67,486 |
| Total Fog 1 | 228.413 | 668 | 58,444 |
| Total Fog 2 | 316.497 | 571 | 66,478 |
| Original 2 | 260.726 | 644 | 66,780 |
| Original 3 | 294.145 | 624 | 67,137 |
| Total Fog 3 | 319.640 | 591 | 68,944 |

All samples start with 1,000 living zombies after warmup. Camera-visible
population falls from 100 to zero during each measured interval. Ordinary
zero-threat cleanup and scheduled zombie-free events are disabled. A later
native removal probe establishes initial-grace cleanup as the main cause, as
recorded below. The different living
populations and executed work prevent attributing the TPS difference solely
to fog cost. The identical candidate ranges from 228 to 320 TPS. Median TPS
is 316.497 versus 291.598, but median actual CustomTicks per second is 4,345.676
versus 4,394.228. This positive instrumented TPS result does not cancel the
prior failed cold-start acceptance or prove a delivery-ready speed improvement.
Frame-p95 medians are 76.371 versus 74.441 ms, also still worse.

Camera-visible and priority opportunities have zero skips; no zombie is never
selected; maximum selection gaps are 121 or 122 game ticks. These are paired
scheduler-service observations, not fog visibility or every-feature acceptance.
The next comparison must control population drift and simulation randomness,
and profile fog costs against actual work. A changing-combat fixture remains
useful separately, with its work/population reported instead of hidden.

Evidence is `artifacts/comparison-accounted-horde-1000-v2.json`, its six runtime
reports/logs and `accounted-horde-1000-v2-log-review.json`. All six recognized
native error summaries are empty; the tested candidate is restored and the
game is stopped. The first accounting attempt failed before measurement
because the probe treated Zombieland's static Values field as a property;
`runtime-accounted-horde-1000-original-1.json` retains that prerequisite error.
The corrected source uses the exact source-declared field. All 229 independent
tests pass. The final evidence guards were also checked against all six retained
samples; no unchanged gameplay comparison was repeated for those guard edits.

### Initial-grace fixture correction

An eight-second native removal probe on the old fixture records 269 crush kills,
one toxic-gas kill and 270 dead despawns, with zero living despawns. The crush
caller is the actual `Zombie.CustomTick` zero-threat branch. The fixture starts
at tick 3 with `daysBeforeZombiesCome=3`; `ZombieFreeEventManager.IsActiveNow`
includes initial grace independently of the disabled ordinary/scheduled cleanup
settings. Its 0.01 crush chance explains the main population loss. The gas caller
is the native Pawn interval path, and ordinary zombies have ToxicResistance 0.
Neither observation justifies changing production grace or gas behavior.
`horde-removal-causes.json`, its retained Player.log and `-verification.json`
record the causes and exact four build/deploy pairs; the recognized log summary
is empty.

The existing Zombieland fixture creator and temporary benchmark mode now set
initial grace to zero as well as suppressing ordinary/scheduled cleanup. Fixture
creation rejects a remaining active zombie-free window. The restoration contract
includes grace in current/timeline fingerprints; both injected failure and
cancellation restore `3:False:False|3:False:False` exactly. No production setting
default or cleanup path changed.

`TotalFog_Zombieland_NoGrace1000` is created under the original fog binary from
the old fixture with the same seed 711100, map and 983 ordinary/17 special type
mix. Its SHA-256 is
`63421fa9372913bc165bb9708478b3c0d05fc26b527e3bd370dcb609cbed3d26`;
the old save remains byte-identical. After 300 native warmup ticks, the new
8-second probe retains all 1,000 zombies, records zero kills/despawns, and keeps
all visible/priority work serviced with selected work equal to 250,013 actual
CustomTicks. No zombie is never selected; maximum selection gap is 121 ticks.
This validates the fixture correction, not fog performance acceptance. Evidence
is `no-grace-fixture-create.json`, `no-grace-preflight.json`, their retained log
and `no-grace-fixture-verification.json`. The recognized log summary is empty.
Endpoint zombie counts are now also captured outside playback with the optional
work observer disabled, so uninstrumented zombie comparisons retain population
evidence. Comparisons additionally match camera zoom, game version and engine
MVID. All native timing still includes the common whole-tick/frame observer.

### Corrected horde diagnostic

Six fresh O/C, C/O, O/C 15-second samples on the new fixture complete after the
predeclared 300-native-tick warmup. Camera/zoom, both main DLLs, settings/config,
fixture SHA-256 and public engine 1.6.4871 rev597/MVID match. Optional zombie
telemetry/removal tracing is active in both variants; DPA is absent. Every sample
starts with 1,000 zombies. All three candidates end with 1,000; original samples
end with 1,000, 999 and 998. The small original losses use the native toxic-gas
caller, not crush cleanup; zero living despawns occur. Simulation was not frozen.

| Variant in execution order | TPS | End living zombies | Actual CustomTicks |
| --- | ---: | ---: | ---: |
| Original 1 | 199.710 | 1,000 | 422,633 |
| Total Fog 1 | 220.377 | 1,000 | 471,052 |
| Total Fog 2 | 227.135 | 1,000 | 489,229 |
| Original 2 | 213.482 | 999 | 450,674 |
| Original 3 | 189.261 | 998 | 399,378 |
| Total Fog 3 | 223.287 | 1,000 | 482,754 |

Median TPS is 223.287 versus 199.710, 11.8% higher. Actual CustomTicks per second
are also higher, 31,603.677 versus 27,623.512, 14.4%, so the faster candidate
results do not come from executing less selected zombie work. Every selection
matches actual work, every visible/priority opportunity is serviced, no zombie
is never selected, and every maximum selection gap is 121 native ticks. Mean
whole-tick elapsed medians are 3.1541 versus 3.5863 ms. Frame-p95 medians are
73.8813 versus 73.0704 ms, still 1.1% worse. These are forced Ultrafast stress
samples, with RimBridge's debug speed boost, not a normal player-speed promise.

The diagnostic floor passes and all six recognized native log summaries are
empty. Evidence is `comparison-no-grace-horde-1000-diagnostic.json`, its six
runtime reports/logs and `no-grace-horde-1000-diagnostic-review.json`. This is
instrumented diagnosis; the comparison with optional zombie instrumentation
disabled is a separate required gate. The old cold-start failure and confounded
shrinking-horde results remain retained, not overwritten by this positive result.

### Corrected horde performance floor

The separate six-process comparison disables optional zombie telemetry/removal
tracing and fog-method profiling. It retains only the common native whole-tick
and frame observer, and counts living zombies outside playback at each endpoint.
The same fixture/settings/loadout/rendering/camera/zoom and both main DLLs match;
all six warmups run exactly 4 to 304 before 15-second forced Ultrafast samples.
The diagnostic set has the same warmup window. The comparison runner now also
matches that complete window; the added guard was checked against all 12 retained
samples without repeating unchanged native measurements.

| Variant in execution order | TPS | End living zombies |
| --- | ---: | ---: |
| Original 1 | 195.486 | 999 |
| Total Fog 1 | 225.956 | 999 |
| Total Fog 2 | 208.845 | 1,000 |
| Original 2 | 199.061 | 999 |
| Original 3 | 191.982 | 999 |
| Total Fog 3 | 209.816 | 999 |

Every sample starts with 1,000 zombies; simulation remains active. Candidate
median TPS is 209.816 versus 195.486, 7.33% higher. Paired gains range from
4.92% to 15.59%, so this is a measured fixture result rather than a fixed general
speedup. Mean whole-tick elapsed medians are 3.3724 versus 3.6165 ms, 6.75%
lower; frame-p95 medians are 71.3418 versus 73.0713 ms, 2.37% lower. The canonical
command returns ok and the original-mod floor passes for this corrected steady
horde. It does not prove normal player 4x speed, every map/loadout, large Symbiant
rendering cost or a portable hardware TPS target. The earlier cold shrinking-
horde failure remains retained with its diagnosed population confound.

Evidence is `comparison-no-grace-horde-1000-floor.json`, its six native runtime
reports/logs and `no-grace-horde-1000-floor-verification.json`. All six recognized
native log summaries are empty, all four latest main/companion build/deploy pairs
match, the tested candidate is restored and GABS confirms the game stopped.
Main TF/Zombieland hashes remain 58f70d68... and 3d1683e6... as above; only their
companions changed for diagnostics/fixture correction. The quiet workflow passes
all 229 independent tests. The updated Python scripts compile successfully.

### Symbiant inspection and cell targeting

Native regression on the paired Steam/all-DLC fixture establishes two failures:
a visible core at (220,79) with the root at (212,79) hidden is offered under the
mouse but cannot be selected; manual targeting offers all 18 logical cells,
including seven hidden cells, for both core/root sight disagreements. Evidence
is `symbiant-interaction-red.json` and its retained native log/hash verification.
The recovered operation result omits some nested snapshot fields; it still
contains the actual core listing, selection and 36 native TargetsAt results.

An exact-type, read-only inspection-cell registration now makes live inspection
follow the established core, while Zombieland's existing GenUI/selector adapters
check the current clicked cell. Rendering and simulation remain independent.
The motion endpoint query no longer allocates/sorts an array of two candidates.
The updated pair passes 240 independent tests, including root/core disagreement,
unchanged ordinary rendering, vanilla fog/bounds, initialization/bypass,
mapless policy, exact-type registration, provider failure/unregister, proxy UI
and closing selection when core sight is lost.

The final native scenario passes all twelve render/cull/resource/fallback cases
and 54 manual-target checks across two core/root disagreements and a staged
half-progress core handoff. Visible-core selection succeeds with the root hidden;
hidden-core listing/selection fail with the root visible. All visible logical
cells remain manual targets, and all hidden cells reject. Registration stays one.
Eleven body-on/off screenshot pairs have positive visible controls and exactly
unchanged pixels inside every recorded hidden cell interior. The all-hidden and
hidden fallback-core images are entirely identical. Off-camera submission is zero.
Native triangle centers remain visible and mask UVs valid.

The earlier pixel helper assumed a fixed boundary from an older pawn's sight.
It misclassified currently visible cells and failed. The fixture now stages the
core across a measured native boundary and records the complete capture-cell
sight grid. The canonical `./scripts/mod verify-rendered
artifacts/symbiant-interaction-acceptance` verifies preserved screenshots against
that grid, the native targeting results and Player.log. The earlier failed check
is retained; no production rendering change was made in response to it.

Evidence is `symbiant-interaction-boundary-final.json`, its pixels/verification
JSON, retained log and the preserved `symbiant-interaction-acceptance` folder.
All four build/deploy pairs match: TF main 5848138f..., companion e84d4d40...;
Zombieland main d6a22424..., companion 02a906e0.... Recognized native log summaries
are empty. Diagnostics/settings are restored and the base fixture reloaded paused.
This accepts these paused native interaction/pixel cases. Actual automatic
combat/hit behavior, full core travel, hidden host/gameplay changes, large
400/4000-cell cost, standalone absent-API behavior and Windows remain open.
The earlier corrected horde floor predates these main DLL changes and must be
checked again before applying that result to the current pair.

### Symbiant labels, tooltip anchors and direct core hover

The adjacent UI audit finds a separate direct hover route in Zombieland's
MapInterface postfix. Its actual eligibility gate now requires current sight at
the clicked core. Total Fog's general tooltip position follows the registered
inspection core; labels and overlays additionally require current sight at their
rendered root. A visible interaction core cannot authorize drawing a root label
inside hidden ground. Rendering/simulation remain unchanged by these UI checks.

The final updated pair passes 242 independent tests and the native twelve-case
matrix again. All 54 per-cell manual-target checks pass. Native calls to the
live hover owner's eligibility gate, tooltip-position policy and pawn-label
policy confirm: visible core/hidden root allows selection and core hover,
anchors the tooltip at (220,79), and rejects the root label; hidden core/visible
root rejects selection/hover/labels and supplies an invalid tooltip position.
The staged partial handoff also rejects its currently hidden click core while
preserving visible-body manual targets. This is native eligibility/anchor proof;
natural GUI tooltip timing/lifetime remains a separate check.

The canonical verify-rendered command passes the preserved eleven screenshot
pairs, including positive visible controls and zero difference in measured hidden
interiors. A deliberately corrupted hidden manual target is rejected offline and
leaves no success record. Evidence is `symbiant-interface-final.json`, its retained
log/build-pair JSON, `symbiant-interface-verifier-negative.json` and
`artifacts/symbiant-interface-acceptance`. All four pairs match: TF bb0c6b61... /
e0fae245...; Zombieland fc7c7bad... / 02a906e0.... Both independent and native log
checks pass. Settings/diagnostics are restored and the base fixture is reloaded.
The current UI pair's fresh horde comparison is tracked separately below.

### Current Symbiant UI pair: horde floor

The current UI/native-tested pair passes a fresh original/candidate alternating
six-process comparison with optional zombie/fog telemetry disabled, all DLCs,
matching camera/zoom/settings/engine/loadout and 300 native warmup ticks (4..304).
Each 15-second sample starts with 1,000 living zombies and finishes with 999 or
1,000; native simulation continues.

| Variant in execution order | TPS | End living zombies |
| --- | ---: | ---: |
| Original 1 | 183.673 | 999 |
| Total Fog 1 | 217.342 | 999 |
| Total Fog 2 | 210.863 | 1,000 |
| Original 2 | 193.691 | 999 |
| Original 3 | 187.998 | 1,000 |
| Total Fog 3 | 227.053 | 1,000 |

Median TPS is 217.342 versus 187.998, 15.61% higher. Mean whole-tick elapsed
medians are 3.2458 versus 3.7770 ms, 14.07% lower. Frame-p95 medians are
71.5906 versus 78.2333 ms, 8.49% lower. Every pair is positive; this is a fixture
result under forced Ultrafast/debug speed boost, not a fixed general gain,
normal player 4x acceptance or large Symbiant-rendering cost acceptance.
All six current native log summaries are empty. All four current build/deploy
pairs exactly match the bytes used for the preceding native UI/pixel proof.
The candidate is restored, the game stopped, and 242 independent tests pass.

Evidence is `comparison-symbiant-interface-horde-1000.json`, its six native
runtime reports/logs and `symbiant-horde-comparisons-verification.json`.
The preceding inspection-only pair also completed a positive comparison
(221.746 versus 201.465 TPS), but its third original run records one native
PathGridDoorsBlockedJob NullReferenceException. Its cause is untriaged; the
stack establishes the engine job path, not blame for Total Fog. That set is not
clean-log acceptance and remains retained separately. It is not used as the
current performance result.

The canonical Zombieland runtime runner now checks retained logs before accepting
a sample. Its existing log summarizer rejects the known failing original log and
accepts all eleven other retained logs, including all six current logs. This
harness-only change was checked offline against those twelve native records;
unchanged gameplay was not rerun. Missing logs also fail. The pending measurement
and native log are saved before the guard can reject a result. Evidence is
`native-horde-log-guard-verification.json`.

### Native interval work

Astra's focused review and DecompilerServer inspection of public Steam
1.6.4871 rev597, MVID `967ddb80559449f0a776dafa26a855d1`, establish that
`Thing.DoTick` dispatches both Tick and TickInterval. Zombieland's budgeted
CustomTick handles movement, verbs, job actions and per-tick hediff callbacks;
the inherited Pawn interval path handles distinct job lifecycle/interval actions,
interval health, environment, components and other trackers. Removing interval
work would discard real behavior. No production interval change was made.

For humanlike ordinary zombies, the engine chooses the interval rate from camera
geometry and zoom, clamped to 1..15 ticks, without consulting fog visibility.
An on-camera hidden horde therefore can receive interval work more often than an
off-camera horde outside Zombieland's CustomTick budget. Existing Zombieland
needs/interactions/immunity patches already suppress parts of that work. Actual
inclusive interval cost and a safe improvement remain unmeasured. Profile it
against actual selected zombie work before considering any change; preserve job,
health, environmental, component, holder/map and save/reload behavior. Spitter and
Symbiant derive directly from Pawn and require separate checks. The retained
`thing-dotick-current.json`, `pawn-tick*-current.json` and
`health-tick*-current.json` contain the exact public engine evidence.

The first expanded instrumentation used an inaccessible engine getter, then
incorrectly cast its reflected result. Those prerequisite failures are retained
as `symbiant-boundary-extended-prerequisite-failure.json` and
`symbiant-boundary-extended-cast-failure.json`; they are not mod compatibility
failures. The installed Steam getter was then inspected with DecompilerServer
and the probe uses its actual Unity.Collections.NativeBitArray through reflection.

The first production field-distance gate conservatively admitted an all-hidden
body but emitted zero mesh geometry. The final gate rejects hidden actual patch
footprints before overlapping element checks, preserves sparse-cell/motion checks
inside visible footprints, and allows missing resources to rebuild from logical
cells. It includes bilinear sampling padding and the native camera/gravship view.
Dense/sparse 400/4,000-cell cost, active incoming/outgoing motion, core travel and
selection away from the root, targeting/host simulation, feeding/severing,
notifications/audio and Windows remain open. Hidden patch texture preparation
still follows Zombieland's preexisting all-patch loop; clipping alone does not
prove reduced GPU upload work for hidden islands.

## Zombieland spawn sirens, 2026-10-06

Real `ZombieSpitter.Spawn` and substantial `ZombiesRising.SpawnEventProcess`
paths reproduced a camera-sound leak. With hidden-source mute enabled, each
created a playing Unity sample at volume 0.5. The enlarged hearing positive
control expected a factor of 0.826046, but the same unfiltered volume remained.
The camera `SoundInfo` has no map maker; the generic positional prefix cannot
infer the event's known spawn cell.

Zombieland now passes that actual cell/map to its existing optional audibility
delegate before camera playback. The shared siren owner retains its original
enabled/ambient-volume checks, and preserves the camera SoundInfo while applying
the factor once. Neither spawn path or simulation policy changes.

The final pair passes twelve native rows, both real spawn owners across hidden
mute, unfiltered, hearing, visible mute, hidden again and disabled-siren states.
Muted/disabled states create no native sample. Unfiltered/visible samples remain
playing at 0.5; hearing samples play at 0.413023 with info factor 0.826046.
Each row spawns exactly one spitter or four ordinary wave zombies, respectively,
and ticks stay 18. Scoped options/sight are restored and the unchanged named
fixture is reloaded. All sixteen electric/tank ambient cases pass again on the
same final pair; the retained native log contains no recognized errors.

The canonical paired build and independent 242-test deploy pass. Built/deployed
gameplay hashes match: Total Fog `43929c0b...`, Zombieland `5ea999b0...`.
Evidence is in `zombieland-event-audio-{red,green}.json`, their retained native
logs/hash records, and `zombieland-ambient-audio-siren-regression.json` plus its
log/hash record. The red's final hidden-again samples were no longer playing;
the initial hidden-muted samples establish the audible leak. These tests accept
the real spawn entry points and audio controls, not natural incident scheduling,
letters, projectile flight/impact, absent-API runtime or Windows.
The previously delivered ZIPs predate these audio fixes. Broader final-pair
rendering/performance gates remain required before the next handoff.

## Zombieland combat action audio, 2026-10-06

The native negative establishes a renderer-owned electric cue defect. Actual
bullet damage is absorbed, but hidden unfiltered/hearing states create no Bzzt
sample. A 128-hit burst leaves 591..643 cosmetic markers across the six states;
the visible control emits repeated samples while rendering drains the backlog.
These are actual native damage and AudioSource observations. A tar negative
also encounters an ordinary armor block, so its simulation assertion fails;
the final fixture uses bullet armor penetration 1 to exercise positive damage
reliably. That fixture refinement is not a tar production fix.

Zombieland now plays absorption/discharge audio at the event. The ranged helper
retains only the latest visible cosmetic batch, bounded by eight markers, and
retains none when hidden. Discharge likewise replaces the prior cosmetic batch.
Cosmetic marker randomization and Bzzt playback use scoped Rand state. The
rendering patch no longer owns audio; native absorption and discharge geometry
remain under the existing renderer. No creepy ambience code is changed.

The exact final pair passes all 24 native tank-armor, electric-absorb, tar-pop
and toxic-splash cases across hidden mute, unfiltered, hearing, visible mute,
hidden again and disabled action cues. Each electric state applies 128 real
damage packets and absorbs all damage. Hidden queue maxima are zero; visible
maximum is eight and the queue drains to zero over 15 native frames. Both
electric helpers preserve the next outer Rand.Value in all six states. Actual
sound sample factors match policy and positive Unity playback is observed:
tank/electric peak 0.6 unfiltered and 0.5041126 hearing; tar/toxic peak 0.8 and
0.6721501. Hidden-muted/disabled controls create no sample. Native voice limits
mean sample count does not equal the 128 event requests.

Tank armor loses durability while taking zero damage. Tar cases deal positive
damage and produce 13 actual TarSmoke each; toxic cases actually die and produce
8..10 StickyGoo. Their exact damage/body-part outcomes need not match across
randomized states. Ticks remain 18 and the unchanged base fixture is reloaded.
The same exact pair repeats all 12 real spitter/wave siren and 16 electric/tank
loop controls successfully. Its retained native log has no recognized errors.

Three existing native Zombieland contracts also pass on this paired loadout.
The active electric melee damage-info path produces ElectricalShock amount 1,
while its disabled control remains Blunt. Actual tar damage creates 13 smoke;
native rifle CanHit changes true to false and aim chance to zero for both zombie
and human targets. These accept damage-info generation and native targeting,
not natural melee health damage, projectile flight/hits, gas spread, boundary
pixels, complete effects/lifetimes or Windows. Full Multiplayer determinism is
not established by the scoped random-stream checks.

Evidence is `zombieland-action-audio-{initial,red,green}.json`,
`zombieland-{event,ambient}-audio-electric-regression.json`,
`zombieland-electric-melee-paired.json`, `zombieland-tar-zombie-target-paired.json`
and `zombieland-tar-human-target-paired.json`, with native logs/build-pair records.
The final paired build passes 242 independent tests. Final gameplay SHA-256:
Total Fog `43929c0b6913df225ee4b9d71ef264176ed608aecb83dc7dbe5879005e2a93a0`;
Zombieland `20e9a34de151d48ac1ac7bcd6afb0e3427809007bed191ad3ce21415ca600172`.
Companions are `ac0629ff34e734808cda6098939b4da8dc87d66b46fc2c2f0a2e283c35ffff22`
and `02a906e0f33a5a80b0803e5aa08444065dae4a93e377e09f02d50f4e782d1411`.
The next delivery still requires final-pair rendering/performance gates.

## Zombieland camera ambient audio, 2026-10-06

The electric/tank ambient mixers use on-camera sustainers with no map maker, so
the generic positional loop filter cannot find their actual zombie sources.
The paired native negative reproduces both leaks with mute enabled: hidden
electric/tank Unity sources remain playing at volumes 0.1/0.4. Hearing mode also
leaves them unchanged. This is actual AudioSource evidence, not just a sound
trigger record.

The optional configured-audibility query reuses Total Fog's existing settings
and listener policy. Zombieland binds it once without an assembly dependency,
then takes the maximum of each source's existing camera-distance falloff times
its audibility. It retains its tracking sets, native loop lifetime and update
cadence. Sources whose maximum possible contribution cannot beat the current
volume skip the query. With no API or audio filtering disabled, the same native
nearest-source volume is preserved. This changes presentation only.

The exact deployed pair passes sixteen native rows, electric/tank across eight
phases: muted start, unfiltered, hearing, muted existing loop, all-visible,
near-hidden/far-visible mixed sources, hidden again and restored filtering.
All hidden-only sources have zero Unity volume and all loops stay playing.
Visible and disabled-filter controls retain 0.1/0.4. The mixed electric/tank
controls preserve the farther visible source at 0.0300128/0.3523736 instead of
letting the nearer hidden source determine volume. Hearing factors follow the
real living listeners. Game ticks remain 18 throughout; scoped settings and
sight contributions are restored, then the unchanged fixture is reloaded.
The native log has no recognized errors. The probe uses an enlarged hearing
range only for a positive muffling control and is excluded from delivery.

Evidence lives in `zombieland-ambient-audio-{red,green}.json`, corresponding
native logs and build-pair hash records. The green fixture separates the two
sources farther to exercise unequal camera falloff. This accepts these native
ambient mixers and live options, not spitter/event sirens,
action one-shots, lifetime/spawn/despawn play, all audio routes or Windows.
The prior delivered ZIPs predate this fix. Broader rendering/performance gates
must be repeated for the next exact gameplay pair before its delivery.

Andreas clarified that creepy night ambience is a global environmental cue,
like weather, and must remain unaffected by fog. No creepy-ambience production
code was changed. It retains native camera playback; Total Fog's mapless-source
policy preserves it. The `creepyAmbientSoundVolumes` value also drives zombie
wandering, so it must not be modified by presentation filtering.

The native `totalfog/zombieland_global_ambient` regression uses the actual
ZombiesClosingIn sound definition with the same OnCamera/MaintenanceType.None
sound info as the production manager. Its own loop retains volume factor 0.5;
actual Unity playback stays at 0.3 with audibility factor 1 in all five modes:
unfiltered, hidden mute, hearing, both filters and restored. All rows are playing,
unmuted and mapless; the production wandering-volume dictionary is unchanged
and game ticks stay at 18. Scoped audio preferences/options are restored and
only the probe's loop is ended. The paired quiet build passes 242 tests and the
native log is clean. The gameplay bytes match Mortal's delivered pair; only
test instrumentation changes. This accepts the fog-filter exemption, not
natural night-cycle/scheduler timing. No production creepiness/wandering code
changes. Evidence is `zombieland-global-ambient-green.json`, its native log and
matching build-pair record.

The exemption is repeated on 2026-10-06 with the current 9a76ddd3... Total Fog /
ff207b8c... Zombieland gameplay pair. All five native controls again keep actual
Unity playback at 0.3 and audibility at one, with the shared night/wandering
dictionary unchanged. No gameplay change is needed. Evidence is
`zombieland-global-ambient-tick-rotation.{json,log}` and the matching
`zombieland-melee-combat-settings-build-pairs.json`. This remains a fog-filter
exemption check rather than acceptance of natural night-cycle scheduling.

## Zombieland ball flight and landed-pawn visibility, 2026-10-06

The current delivered gameplay pair additionally passes the existing native
ZombieBall flight contract under the paired loadout. It moves and rotates at the
28-tick halfway point, then completes the remaining 33 native ticks, disappears
and creates one live zombie. The independent real spitter job-driver firing
contract produces exactly one ball without changing the zombie population.
These are real simulation paths, not fog drawing acceptance by themselves.

The focused `totalfog/zombieland_ball_flight` companion now observes actual
native drawing across an explored remote corridor. It launches the real ball,
advances three 13-tick flight segments and captures twelve native frames after
each while paused. The ball keeps moving in all three segments; draw counts
are 0/13/0 for hidden/visible/hidden, with one registration throughout. It then
completes natural impact without calling Impact directly. The live zombie at
(230,79) passes the same 0/13/0 drawing controls, retaining one registration.
The destroyed ball has zero registrations. Ticks advance from 18 to 134; the
new-pawn presentation transitions stay paused at 134. Actual player sight is
outside the corridor; scoped contributions provide the positive controls.
The original fixture is reloaded and options/contributions/probes restored.

The first probe run failed because Harmony was given an inherited method
reflection wrapper. Resolving its actual declaring method, as the existing pawn
probe does, corrects the harness. That failed attempt is retained separately;
it is not a gameplay defect or successful acceptance. A fresh-process corrected
run and the subsequent native firing contract both have clean native logs.
The paired quiet build passes 242 tests; gameplay bytes remain identical to the
delivered `43929c0b...` Total Fog / `20e9a34d...` Zombieland pair. Only companion
instrumentation changes. Evidence is `zombieland-paired-native-ball-flight.json`,
`zombieland-ball-flight-visibility-green.json`, its native log/build-pair record,
`zombieland-paired-native-spit.json` and its native log. These checks do not accept
explosion pixels/audio, job target-selection policy, ordinary budgeted playback,
in-flight save/reload or Windows. No extra build is sent for this diagnostic.

The subsequent real-impact probe reproduces a live flash leak on the delivered
gameplay pair. Five native flights each create one flash and one live zombie;
positive-alpha hidden flash submissions are 16, 31, 31 and 31 in four hidden
controls. The visible control submits 31. Positional BallImpact audio already
passes hidden mute, unfiltered, hearing and visible controls. Evidence is
`artifacts/zombieland-ball-impact-red.json` and its native log/build-pair record.

The public engine's FleckStatic.Draw has no sight check, and its map field is
never assigned by Setup, TimeInterval or FleckSystemBase.CreateFleck. Total Fog
now resolves the owner once around FleckManagerDraw and checks the current exact
draw cell before native static/thrown particle submission. The manager joins its
workers before returning; a finalizer restores the prior context on failure.
The check changes no particle lists, movement, age, lifetime or gameplay effects.
No per-particle owner dictionary or new simulation state is added.

On the changed Total Fog gameplay SHA-256 `9a76ddd3...` with unchanged Zombieland
`20e9a34d...`, all five native impact controls pass. Each flight finishes in
66..68 actual ticks and produces one zombie. Hidden flash submissions are zero
in every hidden control; the visible control retains 31. Actual Unity impact
audio remains muted, native or muffled according to the selected policy.
Evidence is `zombieland-ball-impact-green.json` and its native log/build-pairs.

The separate particle lifecycle probe creates just one native Smoke and one
ExplosionFlash. Paused sight loss gives 13/0 submissions for each visible/hidden
particle without removal. Smoke advances 30 real ticks while hidden from x217.10
to x218.60, keeps its native age and reappears on reveal with 13 submissions,
then becomes hidden again with zero. The flash expires during those ticks;
smoke later expires through its normal lifetime while still in map bounds.
Both native collections are empty afterward. Smoke's unused owner field remains
null, confirming that batch ownership is necessary.

An initial lifecycle attempt incorrectly required worker threads and counted
pre-expiry playback as post-expiry frames. That failed probe is retained as
`fleck-sight-lifecycle-worker-assumption-failed.json`, not gameplay acceptance.
The native FleckSystemBase.ParallelizedDrawing getter is false. Corrected
default-renderer controls pass all six phases with no worker calls. A separate,
explicitly forced native parallel-renderer control passes the same six phases,
observing 13 distinct worker IDs per live frame window. Its temporary getter
patch is removed afterward. This proves the read-only map context reaches native
workers; it does not claim the default game uses parallel drawing.

The quiet paired build passes 248 independent tests, including worker reads,
current-position/sight transitions, nested failure restoration, correct map
ownership, bounds, initialization and colony/vanilla-fog policy. Both corrected
lifecycle runs and a repeated creepy-ambience control have clean native logs.
Global creepy-loop volume remains 0.3 and audibility one in every fog audio mode;
the production night/wandering dictionary is unchanged. Evidence is
`fleck-sight-lifecycle-native-green.json`,
`fleck-sight-lifecycle-forced-workers-green.json`, their common native log and
`fleck-sight-lifecycle-green-build-pairs.json`, plus
`fleck-candidate-global-ambient-green.json`.

These are native batch-submission and simulation/audio controls. Particle pixel
comparisons, other independent fleck families, direct foreign ForceDraw callers,
ordinary budgeted combat playback and Windows remain open. Current-byte
Symbiant and performance acceptance are recorded immediately below.

The subsequent matched comparison accepts the new gameplay bytes in all six
fresh O/C, C/O, O/C runs on the unchanged upstream-authored 1,000-zombie quiet-gap
fixture. A predeclared 300-native-tick warmup runs before each 15-second sample;
DPA and zombie-work telemetry are absent. Engine, all DLCs, loadout, camera,
resolution, settings, fixture and Zombieland bytes match. Every measured tick
uses multiplier 15 with both forced speed and private debug boost disabled.
Median TPS is 290.0849 original versus 314.9007 candidate, an 8.5547% gain. Mean
whole-tick elapsed medians are 2.4293 versus 2.2264 ms, an 8.3539% decrease.
Frame-p95 medians are 70.4262 versus 69.6270 ms; this small difference does not
establish a general frame-rate improvement. All samples begin with 1,000 zombies
and end with 998..1,000. This is a matched bounded fixture, not identical work
replay or a long-session/mod-list promise. All six native logs pass the existing
error guard. Evidence is `comparison-fleck-sight-player-1000.json` and its six
runtime reports/logs.

The same final gameplay bytes repeat all twelve mature Symbiant pixel cases,
54 per-cell targeting controls and forty paused dense/sparse 400/4,000-cell
render-cost rows. Native logs are clean; registration, hidden cells and focus
guards pass. The connected native shader remains unchanged and its positive
control screenshot is visually checked. The first 400-cell timing attempt ran
alongside image verification, so it is retained separately with the
`pixel-verifier-overlap` label and excluded from acceptance. The corrected
timing matrices run after image verification finishes. Evidence is
`fleck-sight-symbiant-boundary-live.json`, `fleck-sight-render-acceptance/` and
the four `fleck-sight-render-cost-*.json` reports/logs. The previous acceptance
owner is preserved under `symbiant-render-cost-before-fleck-sight/`; only then
are the existing package-gate records promoted to this new evidence.

The canonical paired feedback package passes 248 tests, current-byte pixel/
targeting/render-cost gates, ZIP integrity and gameplay-byte/metadata audits.
Both separate ZIPs contain updated TESTING.md. Zombieland gameplay is unchanged;
all 54 music-folder files are byte-identical to source. TotalFog-preliminary.zip
is 2,335,161 bytes, SHA-256
`8c5c27d9900b2f4ec96f4b04d57fbed108975e2f3a920cc648e2beeda3195c30`;
ZombieLand-preliminary.zip is 193,882,570 bytes, SHA-256
`6eb705dbe41149151f5fafec1cfc9930bc17906cfa7e23a4ef5a6ce77cfd07c0`.

Installed-connector API upload/read-back verifies both complete attachments,
their names/sizes, exact text and the intended private channel on message
`[private message reference]`. The message has the required inline robot prefix, states
that the creepy ambience is unchanged and keeps the Mac-fixture/Windows/combat
limits explicit. Zombieland's unchanged gameplay means Mortal may retain his
previous complete folder. Only after successful replacement read-back is the
superseded `[private message reference]` post removed; deletion succeeds without failures.
Evidence is `mortal-fleck-sight-package-audit.json`,
`mortal-fleck-sight-feedback-{post,edit,verified,delivery-summary}.json` and
`mortal-fleck-sight-feedback-old-post-deletion.json`. This is preliminary tester
delivery, not release-candidate or Windows acceptance.

Before the red impact tool was invoked, one fresh public-game startup/load
failed with native thread-join/SIGABRT and SIGSEGV crash reports. The original
gameplay bytes were unchanged and the new companion probe had not run. A fresh
restart then loaded and completed the red control with a clean native log, and
the subsequent candidate starts completed normally. Cause is unverified; this
is not attributed to Total Fog or declared fixed. Preserved evidence is
`ball-impact-preflight-native-thread-join-failure.log` and the paired
`ball-impact-preflight-RimWorld by Ludeon Studios-2026-10-06-045444*.ips` files.

The same delivered gameplay pair also passes a fresh-process save/reload of a
real hidden ZombieBall during flight. The companion creates the ball through
native Launch, advances twenty actual game ticks, verifies zero hidden draw
calls and one registration, then writes a new native save without overwriting
the base fixture. It records the uninterrupted remaining flight at every game
tick and its natural impact. The save contains exactly one serialized
ZombieLand.ZombieBall.

After verified process termination and a fresh launch, the native visual-ready
loader pauses at tick 39 rather than the saved tick 38. The reloaded snapshot
matches the uninterrupted control at tick 39 exactly: identity, logical/exact
position, rotation and rate, origin/destination, remaining flight/lifetime,
launcher, targets, hit flags and other captured native flight fields. Discovery
count/hash also match. Both paths impact naturally at tick 85, without a direct
Impact call, and spawn one zombie in the expected landing cell. Six paused
hidden/visible/hidden controls for the reloaded ball and landed zombie retain
0/13/0 draw calls and one registration. The impacted ball is destroyed and
deregistered. The probe removes its sight contributions/patches, restores its
options and reloads the original base. Both save files retain their original
hashes and both native logs pass the existing error guard.

An earlier attempt compared saved tick 38 directly with loaded tick 39. Its raw
reports are retained as a loader-timing diagnostic, not a serialization defect.
The matching-tick reference resolves that difference without changing gameplay.
Evidence is `zombieland-ball-cold-reload-reference-{prepare,resume}.json`, their
native logs, `zombieland-ball-cold-reload-reference-save-identity.json`,
`zombieland-ball-cold-reload-reference-acceptance.json` and the four build-pair
records in `zombieland-ball-cold-reload-reference-build-pairs.json`. The quiet
paired workflow passes all 248 tests; gameplay hashes remain the delivered
`9a76ddd3...` / `20e9a34d...` pair. Only excluded companion instrumentation is
added, so no replacement tester ZIP is needed for this checkpoint. This accepts
native cold serialization, discovery and presentation rebuilding for this
flight. Ordinary budgeted combat, blast pixels, target-selection rules,
hidden-spitter letters and Windows remain open.

## Tar smoke visibility and hidden lifetime, 2026-10-06

The paired public 1.6 profile runs `totalfog/gas_presentation` against the real
Zombieland `TarSmoke` definition. Its native Gas lifetime is unchanged. Four
paused hidden/visible/hidden/visible phases give 0/13/0/13 actual DrawAt calls.
Each phase retains one draw registration, an unchanged destroy tick of 2,302,
and game tick 18. The probe then removes its sight contribution and advances
2,300 actual game ticks. At tick 2,318, the smoke is destroyed, its cell remains
hidden and its draw registration is zero. The unchanged fixture is reloaded.
The native log contains no recognized exception/load error lines.

This uses the same gameplay DLLs as the delivered preliminary pair. The probe
is confined to the excluded companion DLL. Evidence is preserved in
`zombieland-tar-smoke-presentation-green.json`, its native log and the corresponding
build-pair hash record. The first probe's ten-second tick-step deadline advanced
only 582 ticks and never reached expiry; that result is retained separately as
an incomplete check, not a gas-lifetime failure. The corrected probe checks
the SDK step result and allows a frame-stepped natural lifetime to finish.

TarSmoke inherits Gas/Thing, so it has no Total Fog remembered-appearance comp.
Its native realtime gate already uses current sight. No gameplay change is
needed for these transitions. This accepts native draw invocation and hidden
expiry, not actual zombie gas production, spread, cross-cell pixels, TarSlime
filth or Windows rendering.

## Large Symbiant render cost, 2026-10-06

The paused native matrix covers dense and sparse bodies at 400 and 4,000 cells.
Each body matures for 61 ticks, then samples hidden released resources, partial
sight, hidden retained resources and an off-camera view. Three alternating
clipped/unclipped controls bypass colony fog, isolating cell-clipping cost.
Sparse cells are deliberately two cells apart. Both shapes use the existing GPU
metaball renderer; its shader, field blending and mask generation are preserved.
This is an artificial render stress test, not a normal Symbiant-growth fixture.

Two source-backed bottlenecks reproduce before the changes. Released resources
bypass the whole-footprint rejection and repeatedly scan overlapping expanded
cell rectangles. The new gate checks their conservative combined envelope once,
including motion endpoints, before retaining the finer sparse/motion checks.
The renderer also repeatedly calls logical-state normalization, whose list
membership repairs are quadratic. Spawn/load and footprint mutation already own
that work; resource preparation no longer repeats it every frame. The native
probe records zero steady-frame normalization calls afterward, versus 61 before.

Inclusive hidden-cold visibility-gate cost, milliseconds per observed frame:

| Body | Before | After |
| --- | ---: | ---: |
| 400 dense | 12.713 | 0.477 |
| 400 sparse | 13.043 | 1.095 |
| 4,000 dense | 331.502 | 3.241 |
| 4,000 sparse | 313.747 | 4.555 |

All 40 final rows retain the requested cell count, one pawn registration, window
focus and the paused tick. Hidden rows have zero currently visible body cells;
off-camera rows make no profiled gate/draw/preparation calls. Each requested
60-frame sample records 59 probe intervals and 61 native method calls. The
Stopwatch timings include instrumentation and scheduling, not GPU completion.
They do not establish TPS or ordinary fourth-speed performance. The first sparse
4,000-cell negative had 38 visible cells and is retained as a confounded result;
the corrected before/after rows reset every observer after maturation and verify
zero hidden-row sight.

Extreme sparse clipping still costs work: the 4,000-cell clipped draw controls
take about 5.74–5.87 ms per frame versus 2.73–2.81 ms without the cell API, with
mean frame intervals around 22.6–23.5 versus 16.6 ms. Hidden-island preparation
still follows the existing all-patch loop. This improvement does not close that
remaining overhead, reliable GPU/allocations measurements or long-session gates.

The resulting exact pair repeats all twelve native boundary/resource/core/UI
cases and all 54 manual-target checks. Preserved hidden-interior pixel differences
remain zero and visible controls draw; `./scripts/mod verify-rendered
artifacts/symbiant-render-cost-acceptance` passes. The independent suite passes
242 tests. Total Fog main/companion hashes are bb0c6b61... / 867e1929...;
Zombieland main/companion are 6a2afc22... / 02a906e0.... The earlier six-process
horde floor used Zombieland fc7c7bad... and is not a fresh floor for this pair.

Evidence: the four `symbiant-render-cost-*-green.json` reports and native logs,
the matching red reports (using `4000-sparse-red-corrected`),
`symbiant-render-cost-comparison.json`, `symbiant-render-cost-green-build-pairs.json`
and `symbiant-render-cost-acceptance/`. Native recognized error summaries are
empty. Temporary profiling/delegates/settings are restored and the unchanged
base save reloaded before stopping the game. Full core travel, automatic combat,
feeding/severing/host effects, natural wall playback, active special effects,
full fallback body and Windows remain open.

The canonical `feedback-package` command accepts this native proof, requires
unchanged built/deployed gameplay DLLs and audits ZIP contents, metadata and DLL
bytes. The original small Zombieland update omitted music checked byte-identical
to Workshop 928376710 (54 files). After the account's upload limit changed,
complete separate mod ZIPs supersede that update; no Workshop-file copy is
required. Recorded companion hashes identify the instrumentation used for the
native proof but new probes do not invalidate identical gameplay bytes. No
companion DLL or diagnostic is shipped.

The earlier 2026-10-06 action-audio update passed the canonical feedback-package
command on the gameplay pair recorded above. It is subsequently replaced by the
verified particle-fix delivery under the ball-flight section. Its TotalFog-preliminary.zip is
2,334,556 bytes, SHA-256
`eba66e95aeacbab3778488b6c4dd2d286b64b779e04725b112cc0d2410f980b0`;
ZombieLand-preliminary.zip is 193,882,408 bytes, SHA-256
`b369f791aeffe264b777dd8c5cf1f1dfc46037b63e0b2b81382aa21735596b9b`.
Both contain TESTING.md, exact native-tested gameplay DLLs and no companion or
diagnostic files; Zombieland includes all 54 music files.

The installed Discord connector now exposes ZIP upload and retained attachments,
while this host's advertised schema is stale. Its real stdio tools/list confirms
the capability. Direct MCP upload posts the Total Fog file, then adds Zombieland
while retaining that attachment. The first local receipt parse expected an
unwrapped message and failed after the successful first upload. Recovery reuses
the preserved message ID, verifies its attachment and completes that same post;
it creates no duplicate. API read-back verifies exact text, channel, filenames
and sizes on message `[private message reference]`. Only then is the superseded message
`[private message reference]` removed; deletion reports success with no failures.
The preliminary notes explicitly preserve global creepy ambience and bound the
native fourth-speed fixture claim. Windows, natural attacks/effects and long
play remain open. Delivery evidence is `mortal-feedback-{post,edit,verified}.json`
and `mortal-feedback-delivery-summary.json` in ignored artifacts.

## Symbiant mature-body boundary defects, 2026-10-05

The retained `totalfog/symbiant_boundary` companion scenario reproduced two
paired defects before the cell-clipping integration above. It creates a hostless 18-cell body through Zombieland's
existing render-blob fixture, fixes the simulation with its existing renderOnly
diagnostic profile, and lets incoming cell animation mature through 61 actual
ticks, from 3 to 64. All measured draw/screenshot phases then stay paused at 64.
The body has one GPU metaball patch and 18 render elements. One observer with
range 5 views the left end, the right end, and neither end. The entire strip was
observed first, so explored translucent fog cannot hide a drawing leak behind
unexplored black terrain.

With the root in sight and nine of eighteen body cells visible, the native
renderer submits thirteen DrawAt calls and draws slime through the hidden far
end. Body-on/off screenshots share the same camera, crop and tick. There are
55,352 pixels with an RGB difference greater than eight in the conservative
interior of hidden world cells x=218..220, z=79..80; these cells are beyond the
observer's five-cell range at x=212. The paired crops were visually checked.
This proves actual hidden-body disclosure, rather than merely an unfiltered
mesh submission.

At the opposite end, nine body cells are visible but the root is hidden. Every
native phase, DrawAt and overlay call is suppressed. Body-on/off screenshots
are pixel-identical. The all-hidden sample also submits no calls and has
pixel-identical controls. Registration remains exactly one in all three cases.
The common root-based pawn visibility rule therefore cannot represent this
multi-cell renderer; allowing the full body whenever any cell is visible would
leave the positive root's disclosure intact.

The initial paused probes at tick 3 established the root gate but could not prove
body pixels: new cells begin their 60-tick incoming animation at zero radius.
Those prerequisite-limited artifacts are retained separately. The mature probe
restores the exact prior diagnostic profile, sight range, fog fading and removes
all temporary Harmony observers, then reloads the unchanged named fixture.
Gameplay DLLs are unchanged by this diagnostic addition. Evidence:
`artifacts/symbiant-boundary-mature.json`,
`symbiant-boundary-mature-pixels.json`, the saved cell-rect screenshots and
`symbiant-hidden-body-on.png` / `symbiant-hidden-body-off.png`.
The quiet deployment passes 223 tests and the main/companion build. All four
built/deployed Total Fog and Zombieland pairs match. The unchanged fixture is
back at tick 3 with the exact default Symbiant diagnostic profile restored.
The canonical log summary recognizes no errors. Exact pair and log records are
`symbiant-boundary-mature-build-pairs.json` and
`symbiant-boundary-mature-log-review.json`.

The fix must keep custom body presentation separate from live root/host UI and
simulation. It must clip hidden body and selection-core pixels, allow visible
parts when the root is hidden, preserve native camera/vanilla-fog culling, and
cover moving cell animations, resource lifecycle, save/reload and targeting.
Any optional integration must bind once and avoid adding a whole-map scan or
per-frame reflection to ordinary zombies. This preceding red result does not
establish whole-game performance or Windows acceptance for the fix above.

## Zombieland feature inventory, readout and Symbiant root, 2026-10-05

The feature inventory now accounts for every one of the 22 advisory Zombieland
coverage groups, including attached block motes, wall crossing, all contamination
groups, defense buildings/items, infection/medical, avoidance/targeting, incidents,
social/selection and uninstall. Current source owners and direct presentation
routes were checked separately from native acceptance. Two advisory references
point to the removed ZombieSymbiantRenderer file; ZombieSymbiant.cs owns the
current renderer. COVERAGE.md contains the current matrix and open gates. This
does not transfer standalone Zombieland acceptance to the paired loadout.

The extended native healer/readout scenario observes the actual
GlobalControlsUtility.DoDate counter through Widgets.Label and its actual
TargetHighlighter hover calls, using the drawn counter's screen position. It
first fails on the preceding Zombieland DLL, SHA-256
`b48baf76c35b912dbb3d0c3e16250c978cb079d20fb086ec2d4a87ceb96ded9b`:
all three phases display/highlight all 13 zombies. The two partially visible
phases disclose six hidden zombies; the all-hidden phase discloses all 13.

With the scoped Zombieland fix, the two partially visible phases display and
highlight exactly seven visible zombies; the all-hidden phase displays none and
highlights none. Queued spawning is excluded from this presentation count and
the background no longer exposes the map-wide ticking fraction. The population
query used for spawning remains 13, and paused ticks remain 3 to 3. The healer
still submits positive effects in sight, zero across a hidden target/both hidden,
retains read-only records and suppresses expired effects. The fixed hidden-target
effect-on/off screenshots are pixel-identical at 4592x2646. Counter crops were
visually checked. An earlier HashSet-to-IList diagnostic cast failure is retained
separately; it is not the native failing readout result or a gameplay defect.

Zombieland gameplay SHA-256 is
`8fd7605155955bda5210d24de7ce9897259bd1845129e871cfb5554ecc605ffb`,
MVID `a30ede27-427f-43e2-9d9f-9a1924ca03b6`. Main Total Fog remains
`dada41d3034323760c9306b6cb34ab646b6deb20e7e1b028d91e7931b2298001`.
The optional delegate is bound once and the sight count is uncached; this adds
no simulation count, update scheduler or per-frame reflection. Evidence:
`artifacts/zombie-readout-before.json`, `zombie-readout-after.json`,
`zombie-readout-pixels.json` and saved HUD crops.

The retained query-only tool alternates seven paused samples of the production
population and sight-count delegates, 200 queries per sample, after warmup.
On the 1,000-zombie fixture it measures 94 currently visible zombies, unchanged
population 1,000 and ticks 3 to 3. Median query cost is 0.014274 ms for population
and 0.210051 ms for the uncached sight count, about 0.196 ms additional work.
This is neither a whole-game speedup nor a native allocation result. Repeated
GUI query work remains a profiling target. Evidence:
`artifacts/zombie-readout-cost-1000.json`.

The generic modded-pawn presentation observer now includes the actual override
of DynamicDrawPhaseAt, rather than observing only Pawn's base body. Native
ZombieSymbiant41222 root presentation passes hidden/visible/hidden at paused
ticks 3 to 3. Hidden phases submit zero phase/DrawAt/overlay calls. The visible
phase executes the actual ZombieSymbiant phase/DrawAt bodies and Pawn overlay;
selection succeeds in sight and is removed on sight loss. Registration stays
exactly one. This proves the root gate, not partial-body clipping, logical-cell
targeting, health/host/feeding/severing, moving selection core or complete reload
acceptance. Evidence: `artifacts/zombieland-symbiant-root-presentation.json`
and its saved log.

## Zombieland Symbiant gameplay and Albino contracts, 2026-10-06

The current 9a76ddd3... Total Fog / ff207b8c... Zombieland pair passes the existing
Symbiant feeding contract. Its selection core is at (117,118), away from the
canonical root at (118,118). Feed jobs route to the core; the root has no feed
options. Two human corpses produce separate options, three fresh animal corpses
share one representative option, and a rotten animal has a separate option.
Forbidden, unreachable and mechanoid corpses are excluded. Reserving the
Symbiant removes all four options for a competing feeder, and releasing it
restores them. Direct helper feeds consume their corpses and add exactly six
human or four fresh-animal cells. The contract's 64 capacity reads require one
evaluation and 25 room-cell scans, with no exact audit; 256 growth-state reads
require no capacity evaluation or room scan. These are native work counters,
not a wall-clock or allocation benchmark. The fixture and settings are cleaned.
Evidence is `zombieland-symbiant-feeding-paired.{json,log}`.

The later carrying-save fixture exposed a companion assertion that read the
human job after ending it and starting the animal order. The same job-target
checks are now captured while the human order is active. The repeated native
contract passes all menu, exclusion, reservation, pulse and cached-read checks;
`humanJobTargetsValid` is true and cleanup removes its temporary state. The
earlier red result is retained in `zombieland-symbiant-feed-save-fixture.json`.
This changes only test instrumentation. The paired quiet build passes 248 tests;
both deployed gameplay DLLs remain identical to Mortal's current ZIPs. Evidence
is `zombieland-symbiant-feed-job-snapshot-contract.{json,log}` and its build-pair
record.

A separate live player-flow check retains this fixture, selects its host and
right-clicks the non-root core through RimWorld's play UI. Three real menu orders
feed a human corpse, a fresh rat and a rotten rat. After normal playback, each
is observed in the pawn's carry tracker during FeedZombieSymbiant, then leaves
empty hands and a completed feed job. Exact sizes are 12 to 18, 18 to 22 and
22 to 24 cells, matching the six/four/two-cell menu promises. The three orders
span 633 normal-speed playback ticks without direct feeding or toil calls.
Crossing twenty cells also installs the host's first Manipulation benefit.
The later 100-thing area sample is capped and is only diagnostic; absence from
it is not separate proof of destruction. Native job/carry/growth observations
establish the feeding result. The existing job's finish path consumes its
carried corpse. The unchanged base is reloaded visually ready and paused, and
the native log passes the recognized-error guard. Evidence is
`zombieland-symbiant-feeding-native.json`, its native log/reload result and
`zombieland-wall-rotation-feeding-acceptance.json`. Partial fog disagreement
between root and core, full colony surgery and Windows remain open. This accepts
the three player feed flows, not every Symbiant interaction.

A real human feed order additionally passes save/load while the host carries
its corpse. The non-root core menu starts the order; 31 Normal playback ticks
leave Human48093 carrying Corpse_Human48115 for ZombieSymbiant48096. The saved
job retains targets A/B and core target C at (117,118), with the same corpse in
the carry container and the driver in its hauling phase. In-process load and a
full stop/start/load both preserve the exact job, corpse, host link, twelve-cell
body and interaction core. Another 179 Normal playback ticks complete the job,
leave empty hands and grow the same body to eighteen cells. No helper feed or
toil is called in this player flow. Both preserved native logs pass the
recognized-error guard, and the unchanged base is reloaded visual-ready and
paused. Evidence is `zombieland-symbiant-feed-save-{inflight,loaded,fresh-finish,
serialized-job-verified,acceptance,base-restored}.json` and the in-process/fresh
logs. This accepts one human feed saved during hauling, not every feed save
phase, partial-fog disagreement or an in-progress surgery job.

The subsequent native sight matrix exposes and fixes two Zombieland menu leaks.
At (119,117), the root is visible but the interaction core is hidden; the old
menu still offers three feeds. At (118,117), the core/root are visible but two
eligible rat corpses are hidden; the old menu still names both. Zombieland's
existing custom-menu postfix now checks the clicked cell through its bound
Total Fog cell query, and the feed candidate predicate checks current thing
sight before capacity/reservation/path work. With Total Fog absent, those
existing optional queries still return true. No simulation or per-tick work is
added. This also gates the postfix's other custom choices at hidden clicked
cells, whose individual visible-action workflows are not accepted by this test.

On Total Fog 9a76ddd3... / Zombieland 2357a5ec..., all four native rows pass:
all-hidden and hidden-core/visible-root menus offer zero feeds; a visible core
with one visible human and two hidden rats offers only the human; a visible
core/hidden root with all three corpses visible still offers all three. The
native menu action starts the human job with the exact A/B/C targets, and
31 + 181 Normal playback ticks carry, consume and complete it with twelve to
eighteen cells and empty hands. The fixture uses a scoped base sight range of
three, yielding actual range two, with real observer movement/refreshes rather
than visibility-array overrides. It accepts these current-sight menu boundaries
and one normal job, not every range, moving-core phase, feed save phase or
surgery. The initial broader feeding contract also passes on the new pair.
The unchanged base and sight setting are restored; native logs pass the
recognized-error guard. All five creepy ambience controls repeat on the new
pair at Unity volume 0.3/audibility one, with the night/wandering dictionary
unchanged. The quiet paired build passes 248 tests with no errors and the one
existing nullable warning. Red/green evidence is
`zombieland-symbiant-feeding-sight-{red,mixed-red,green}.{json,log}`, exact
build-pair records, and `zombieland-symbiant-feeding-sight-global-ambient.{json,log}`.
The exact 9a76ddd3... / 2357a5ec... pair subsequently passes the canonical
`./scripts/mod feedback-verify menu-privacy-player-1000`: twelve native pixel
cases, 54 per-cell manual-target checks and forty paused 400/4,000-cell
dense/sparse render-cost rows. All five creepy-ambience controls again keep
actual Unity playback at 0.3, audibility one and the shared night/wandering
dictionary unchanged. Native logs pass, the base save remains unchanged and
all four built/deployed gameplay/companion hashes match throughout. The workflow
preserves superseded native evidence before refreshing the package gates.

The completed six-process ordinary fourth-speed comparison on the unchanged
1,000-zombie fixture measures median 313.398 TPS for Total Fog versus 289.930
for the original, an 8.095% increase. Median per-tick CPU drops 7.713%, from 2.43366
to 2.24595 ms; median frame p95 remains essentially unchanged at 70.1033 versus
70.1334 ms. Both variants use the same Zombieland 2357a5ec... DLL, save, scalar
settings/configuration, camera and public engine; DPA/optional work telemetry,
forced speed and the debug boost are disabled. Every measured tick retains
the native fourth-speed multiplier 15 after a predeclared 300-tick warmup.
All three candidate samples exceed all three original samples. This accepts
this matched fixture's performance floor, with broader maps, load spikes,
long sessions and Windows still open. Evidence is
`comparison-menu-privacy-player-1000.json`, its six native runtime reports/logs,
`symbiant-render-cost-acceptance`, four render-cost JSON/log pairs and
`zombieland-current-package-gates.json`; the latter retains the exact native
attempt and comparison hash.

The exact pair is delivered as separate complete ZIPs in Mortal's private
message `[private message reference]`. Discord readback verifies the content, filenames
and sizes; downloading through the installed Discord connector proves both
remote SHA-256 values equal the audited local ZIPs. Zombieland includes all
53 music tracks and their README. Only after both matches, the exact superseded
post `[private message reference]` is deleted. Delivery/package/hash/deletion receipts
are `mortal-menu-privacy-*` in ignored artifacts. This is preliminary tester
delivery, with the remaining compatibility and broader gameplay gates open.

The same gameplay pair now also passes the existing benefit, host-effect
isolation and severance contracts. Benefits cover deduplication, stacked
capacities/skills, real injury healing, contamination exclusion and immediate
bite immunity. The isolation contract exercises real pregnancy/anesthesia
hediffs and controlled-safe birth completion, preserving the bond and shared
pool; it is not a full pregnancy playthrough. Severance calls the native recipe
with ten supplied extract and checks medicine eligibility, safe detachment and
accelerated retreat. This does not prove ingredient hauling/consumption or a
complete colony surgery job. All three clean their fixtures and pass the native
recognized-error guard. Evidence is
`zombieland-symbiant-{benefits,host-effects,severance}-wall-rotation-paired.{json,log}`.

The broader damage/map-lifecycle contract initially failed only its old
size-scaling oracle: it added outdoor cells but expected every logical cell to
contribute shared health. The feature owner and production classifier explicitly
restrict health capacity to eligible indoor floor/door cells. The companion-only
correction uses the fixture's known indoor footprint, then adds eight confirmed
outdoor cells as a negative control. Native results are 23 indoor cells,
multiplier sqrt(23), maximum 19,184; adding eight exterior cells increases the
footprint to 31 without changing eligible cells, multiplier or health maximum.
The full contract passes genuine injury sharing, effect-only damage isolation,
echo/recovery, safe versus pool-exhaustion detachment, cross-map host/pool cases,
valid Pawn.Kill corpse creation, direct map deinitialization and gravship
abandonment. The ordinary-map Metal Hell case is a negative control, not an
entire pocket-map story sequence. Cleanup leaves no active/world Symbiant; the
native log passes the recognized-error guard and the unchanged base is reloaded
visual-ready and paused. Both gameplay DLLs remain byte-identical to Mortal's
delivered pair. Evidence is
`zombieland-symbiant-damage-lifecycle-indoor-scaling-paired.{json,log}`, its
reload result and `zombieland-symbiant-indoor-scaling-build-pairs.json`. The earlier
red result is retained separately; no gameplay policy changes were needed.

The existing persistent two-map Symbiant fixture additionally passes true
save/load in process and after a clean stop/start. Both loads retain the same
Human48068 host and ZombieSymbiant48071 organism on maps 1 and 0, respectively,
with the authoritative link and exactly one host marker. Across maps the bond
remains dormant: benefit/aura factors are zero, infection immunity is false,
and severance is unavailable. The native controlled return restores the same
link, one marker, infection immunity and surgery eligibility. This single-cell
fixture does not establish reactivation of acquired benefit stacks, caravan
travel or an in-progress feed/surgery job. Cleanup destroys/discards the fixture
Symbiant, removes the temporary map and parent, and leaves no world-pawn
Symbiant. Both native logs pass the recognized-error guard. The original base
is unchanged and reloaded visual-ready and paused. Evidence is
`zombieland-symbiant-cross-map-{stage,save-load,fresh-load,return,cleanup}-paired.json`,
the two preserved native logs, base-restoration result and saved-fixture hash.

The combat contract's original 48-cell centre search could not establish its
clear-cross/firing-corridor prerequisite, including after native flood-unfog.
Those two raw prerequisite failures are retained. The companion now prefers the
same centre search and then tries the remaining map cells, retaining every
standability, occupancy, vanilla-fog, gas and shoot-line check. It finds a valid
cross at (153,185), outside the old radius, without clearing terrain. The complete
contract passes one identity/registration, five logical cells, independent
non-root shoot lines, root-out-of-range acquisition, validator/thick-roof
rejection, scan memoization, beam/spray exclusion, non-mutating melee queries
and explicit rebinding. A non-root explosion produces exactly one five-point
shared-health hit with no registration at that cell; excluded inner cells
produce none. Evidence is
`zombieland-symbiant-combat-whole-map-contract-paired.{json,log}` and its exact
build-pair record. Gameplay bytes remain the delivered pair.

Two ordinary bolt-action raiders then run a real LordJob_AssaultColony, with the
linked host in same-map cryptosleep and competing fixture bystanders removed.
899 Normal playback ticks produce 36 gunshot damage; both raiders' focus and
last-attacked target are the Symbiant. Saving/reloading the active fight preserves
the same actors, targets and pool. Another 601 Normal ticks adds 36 more gunshot
damage, for 72 total, while the host remains alive with zero real injuries.
Neither phase forces speed or invokes hits/toils directly. This accepts that
ordinary ranged flow, not partial-fog targeting, every weapon or CE. Evidence is
`zombieland-symbiant-combat-assault-{setup,playback,reload,cleanup}-paired.json`,
the before/after-reload logs and the combat-fixture build-pair record.

The same companion contract adds `setup-melee`, which removes only its temporary
rifles after staging and leaves target selection/pathing/damage to ordinary
assault AI. Both raiders reach AttackMelee with B/C binding to non-root cells
(153,184) and (154,185). 900 Normal ticks add 60.46207 melee damage to the echo
history. Save/reload preserves both jobs and their logical strike cells; another
600 Normal ticks increases that damage to 141.450226 total, excluding the
fixture's initial five-point blast. The host's one pre-existing injury stays at
severity two, with unchanged zero pain and full summary health; no new injury
appears. Both normal melee phases pass the native recognized-error guard. The
melee setup also repeats every applicable geometry/scan/explosion contract row;
the shape-mutation rebind row is deliberately skipped to preserve the assault
footprint and was accepted separately in contract mode. Both assault cleanups
remove four fixture pawns and one casket, then the unchanged base is restored
visual-ready and paused. Total normal combat playback is 3,000 ticks. Evidence is
`zombieland-symbiant-melee-{setup,playback,reload,cleanup}-paired.json`, its native
logs, combat-melee build pairs and base-restoration result. These are native
gameplay/save-load checks, not a performance benchmark or partial-fog matrix.

The following faction-sight matrix reproduces an optional enemy-fog bypass in
both ranged acquisition and melee-cell selection. With the prior delivered
pair, each variant passes three of five paused rows but still acquires a body
with zero cells in its faction sight, including immediately after losing sight.
The read-only `Visibility.AllowsTarget` query now shares the existing humanlike
non-player policy, and Zombieland filters its native-valid logical cells through
that query. The query does not grant native LOS, range, hostility or reachability.

Both corrected variants pass all five paused rows using actual range-three
observers and the native fight scan flags: enemy fog disabled, all hidden,
one visible body cell with a hidden root, full visibility, and hidden again.
The partial control selects (153,184) while the root (153,185) is unseen. No
coverage values are injected and no stale tick cache is used. The paired quiet
build passes 250 independent tests. Exact built/deployed gameplay SHA-256 values
are Total Fog `93522955df5bbb6df9081808d0a033bceebbe03c72fc68d22caae7e004199c50`
and Zombieland `039a4fe1d5e0090751697cce6f802d2ac2e4acdf3acce60416eb8e29b811491c`.

The earlier partial-player-fog playback fixtures were not accepted. The initial setup
starts too many observer jobs in a paused tick and relocates a second raider
into a job loop. Those errors are retained and now recognized by Zombieland's
native-log summarizer. With the setup corrected, 1,497 Normal ticks leave the
partial view stable but the raider attacks the observer instead of the Symbiant.
A subsequent explicitly ordered melee run also switches to the observer and
records no shared-pool damage. Neither run establishes partial-fog hits or a
Symbiant gameplay defect. Both restore the unchanged base. Evidence is
`zombieland-symbiant-combat-sight-{ranged,melee}-red.json`,
`zombieland-symbiant-combat-sight-melee-{isolated,ordered}-green.json`, the paired
logs and exact build-pair records. The earlier full-view combat/save-load
acceptance remains distinct. These failed fixtures are retained as history;
the following native building-sight checks supersede that acceptance gap.

An actual `SecurityBellSmall` now supplies the partial player view without a
nearby colonist competing with the Symbiant for the raider's target. Its native
three-cell sight sees only (153,184) of the five-cell body; the root (153,185)
stays unseen. The unfiltered native fight scan must select the Symbiant before
ordering the real AttackMelee or AttackStatic job. Enemy fog remains enabled.
Both first bell trials pass all five paused faction-sight rows, then deal
shared damage during 25 seconds at Normal speed: 60.88159 melee and 90 ranged,
with unchanged host injury and bell health. The attacker's own sight naturally
expands as it approaches or aims, so this does not claim its root stays hidden
throughout combat. Evidence is `zombieland-symbiant-combat-sight-{melee,ranged}-bell-1.json`,
their clean native logs and `zombieland-symbiant-combat-sight-bell-build-pairs.json`.

The save/reload extension retains displaced pawns as world pawns and removes
the displaced second raider from its assault group. A distant colonist remains
on the map for native colony queries. The first two save fixtures are rejected:
one assumed no load tick and discarded referenced pawns; the next preserved
references but left a free world pawn in its assault group and no colonist on
the map. The public engine's `Game.LoadGame` explicitly performs one native tick
before pausing when `Prefs.PauseOnLoad` is enabled. The corrected contract
requires that setting and the exact saved tick plus one, rather than allowing
an arbitrary timing tolerance. The rejected results/logs remain in
`zombieland-symbiant-combat-sight-melee-save-{1,2}.{json,log}`.

Both corrected variants pass five paused rows, 25 seconds of native Normal
combat, in-process save/reload, and another 10 seconds of resumed combat. The
two playbacks report 2,103 melee and 2,102 ranged ticks, in addition to the
engine's deliberate paused-load tick. Reload preserves exact shared health,
five logical body cells, host identity/injury, bell identity/position/health,
and the active attack job's target. Melee deals 37.93359 shared damage before
saving and another 23.57941 afterward; ranged deals 18 and then 54. Host injury
stays zero and the bell stays at five hit points. The player still sees one
body cell and no root after both phases. Native logs are clean, GABS has no
open attention, and each finally block restores options and reloads the
unchanged base. Fresh-process partial-fight loading was still open at this point;
other weapons/sight ranges and additional maps remain open.

These are the gameplay bytes already delivered to Mortal: Total Fog
`600083e5e3101beeed8597a82b42444933f2f1d96cfc6da2c5c9d7fe223cfda1` and
Zombieland `039a4fe1d5e0090751697cce6f802d2ac2e4acdf3acce60416eb8e29b811491c`.
Only diagnostic companion code changed, so the accepted silent-arrivals
performance/package gates still apply to the same gameplay bytes. The quiet
paired deploy passes all 263 independent tests. Evidence is
`zombieland-symbiant-combat-sight-{melee,ranged}-save-3.json`, their native logs,
`zombieland-symbiant-partial-save-build-pairs.json` and
`zombieland-symbiant-partial-save-acceptance.json`. The latter retains both
prepared save hashes and the unchanged base hash.

The same two prepared fights subsequently pass separate fresh-process loads.
The existing sight contract has a resumption mode that creates no new fixture
and retains no old pawn references. It applies the preparation's sight settings
before the native load, records the loaded state, runs ten seconds at Normal
speed, restores the original options and reloads the unchanged base. GABS
verifies termination between variants; their new process IDs are 7032 and 7123.
Both loads retain the exact saved shared health, five absolute body cells,
host identity/injury, bell identity/cell/health, attack job/target and expected
saved tick plus the native paused-load tick. The loaded body's coordinates are
also compared directly with the prepared save's root and relative cell list.
Both prepared save hashes remain unchanged.

Each resumed fight reports 601 playback ticks. Melee deals another 10.24541
shared damage and ranged another 18; the host retains zero injury and the bell
retains five hit points. Player sight remains one body cell with the root hidden
at the loaded and resumed endpoints. Both fresh native logs are clean and GABS
has no open attention. This accepts these partial-sight combat saves across
restarts, not every weapon, map or transient sight change. Evidence is
`zombieland-symbiant-fresh-{melee,ranged}.{json,log}` and
`zombieland-symbiant-fresh-fight-acceptance.json`. The paired quiet build passes
all 263 independent tests; the gameplay hashes remain the already delivered
pair. The new Total Fog companion hash is
`9019d03f2b83a6efdbd12ae14a7708fbe3be32a086d9e2e210229df66afc2731`,
recorded with both exact mod/companion pairs in
`zombieland-symbiant-fresh-fight-build-pairs.json`.

The initial paired Albino sabotage suite was not accepted. Its first run passes 20 of
23 cases, with one fixture NullReferenceException, a missing clear radius-fourteen
area and a failed safety-path commitment assertion. With the existing native
zombie test mode enabled, the attack-mode/scream case passes, but both safety
cases remain open. A different upstream base still has no clear radius-fourteen
area and exposes a door-route fixture whose door is already open before hacking.
Production safety revalidation rejects a destination only when its pressure
reaches the recorded limit. The test always adds two unarmed pawns, but its
initial limits vary between four and six; the changed destination pressure is
not recorded. Native ticks used to await path creation can also run the planner.
These runs do not yet distinguish a gameplay defect from invalid fixture
assumptions. No gameplay fix is made to force the assertions. All three retained
logs pass the recognized-error guard; test mode is restored and the original
base reloaded paused. Evidence is `zombieland-albino-sabotage-{paired,controlled,
upstream-base}.{json,log}`. Closed-door setup and pressure/path preconditions
needed corrected fixtures before those rows could be accepted.

The safety-path commitment/revalidation row subsequently passes with corrected
diagnostic preconditions on the unchanged delivered gameplay pair. The suite
now accepts an optional exact `onlyCase`, rejects unknown case names, and can
run this row without repeating the other 22 cases. It measures the real native
destination pressure and adds actual unarmed pawns until that pressure reaches
the route's recorded limit; it never changes the production limit. The first
measured run correctly abandons the pressured destination but remains rejected
because its supposedly frozen initial pawns acquire Wait_Combat jobs while the
asynchronous path is built. That raises pressure and invalidates the earlier
"unchanged route" assumption. Evidence is `zombieland-albino-pressure-measured.json`
and its retained log/build pairs.

The corrected row explicitly orders both initial pawns into native melee jobs
before requesting the path. Initial movement pressure is eight and remains
eight after two native path-building ticks. Revalidation preserves the exact
path object, destination and movement progress. Four newly spawned unarmed
pawns then raise destination pressure from zero to eight, matching the recorded
limit, and the planner abandons that destination. The single row passes with a
clean native log and no open GABS attention; native test mode is restored and
the unchanged original base is reloaded paused. These are controlled helper
and native path preconditions, not unassisted Albino gameplay. At this stage the
closed-door safety row still required a suitable test area. Evidence is
`zombieland-albino-pressure-stable.{json,log}` and
`zombieland-albino-pressure-stable-build-pairs.json`. Both gameplay hashes remain
`600083e5e3101beeed8597a82b42444933f2f1d96cfc6da2c5c9d7fe223cfda1` and
`039a4fe1d5e0090751697cce6f802d2ac2e4acdf3acce60416eb8e29b811491c`;
only companion diagnostics changed.

Closed-door setup now requires the actual nine-by-nine room to be clear and
at least 192 valid outside cells within radius fourteen. It no longer requires
every cell in that circle to be empty. This accepts the same route-filtering
assertions on the forest base without removing existing map objects or changing
production logic. The focused native row finds 475 valid outside candidates,
confirms that the closed door blocks the Albino, selects an inside-room
destination with no door crossing, and preserves the exact path through
revalidation. Evidence is `zombieland-albino-room-native.{json,log}`.

The default full suite subsequently passes all 23 cases together, including
scream/vomit lifecycle, cooldown and paralysis cleanup, native safety-path
creation/revalidation, door resumption and interception, stale hack targets,
and power/flickable/breakdownable/weapon contracts. Its native log passes the
recognized-error guard and GABS has no open attention. Test mode is restored
and the unchanged original base is loaded paused. This accepts the controlled
contracts, not natural unassisted AI playback or complete fog privacy.
Evidence is `zombieland-albino-corrected-suite.{json,log}` and
`zombieland-albino-corrected-suite-acceptance.json`. Current deployed companion
hashes are Total Fog `b7385074889738041e00bbe2a9b14ed4efb4b7c76d69122abe3e6bfaca540bb5`
and Zombieland `4cf1537e9eeaf84c0180e090450b33d2c4f940b7ed604936f3b75d4ead3dde01`,
recorded in `zombieland-albino-room-build-pairs.json`. Gameplay bytes still match
Mortal's current ZIP pair and its existing performance/package acceptance.

## Zombieland block mote and wall crossing, 2026-10-05

The retained mobile-effects scenario belongs to the existing Zombieland effects
companion class and changes no gameplay DLL. It stages paused climbing progress
0, .5 and .9 and moves one player observer to either side of the sight boundary.
The engine's ComputeCulledThings is observed after the production fog postfix,
alongside actual pawn draw-phase calls. All six samples pass. At .9 the logical
starting cell is visible on one side but the drawn cell is hidden, yielding zero
draw calls. On the other side the starting cell is hidden but the drawn cell is
visible, yielding 13 draw calls. The engine cull cell equals the actual DrawPos
cell throughout. This is native interpolation/culling proof, not a standing-pawn
substitute. Engine inspection confirms DrawPos is sampled before our mask, even
for objects subsequently culled. Zombie.DrawPos's rotation mutation still needs
separate camera/frame-rate and Multiplayer review.

The same scenario calls production Tools.CastBlockBubble, verifies the resulting
MoteAttached links to the defender and uses its real attachment update with zero
elapsed world time. At positive alpha .5, hidden/visible/hidden samples submit
0/13/0 calls to the actual Mote.DrawAt body. Native cull and drawn positions
match. The visual samples leave paused ticks 3 to 3. This does not yet accept
natural melee/parry initiation, moving attachment, pixels, boundary overlap or
expiry. The engine attachment updates exactPosition and Position from the
linked defender's drawn position; root-only pawn assumptions were not used.

The existing Zombieland real Stumble contract is then reused in this paired
loadout. A fixture-only CustomTick prefix replaces nonpositive threat values
during this 102-step test, preventing unrelated zero-threat zombie cleanup.
The climber starts, remains alive, lands at step 102, leaves the wall intact and
removes the constructed roof at the landing cell. These steps use the existing
direct native tick helper; normal budgeted playback and active warning/audio
acceptance remain separate. An initial attempt without the fixture correction
was interrupted by zombie removal and did not establish a gameplay defect.

The initial companion attempts also exposed fixture prerequisites: the original
position lacked an open strip; ordinary AllThings does not contain these motes;
and observer/attachment staging needed to isolate actual mote sight. Those
diagnostic failures are retained separately from the passing result. The final
scenario chooses a valid strip, observes the real drawable list and moves the
other vision sources away. All temporary patches/settings/world changes are
removed by restoring the unchanged named save, which remains loaded and paused.
Its final log has no recognized errors/configuration failures and GABS reports
no open attention. All 223 tests pass and the quiet paired build has zero
warnings/errors. All four built/deployed gameplay/companion hashes match.
The Total Fog gameplay hash remains dada41d3; the companion hash is
`78d29bd1cf182f3a11866b3164725805edaf1f1460982b4418d72bf1236cd346`.

Evidence: `artifacts/zombieland-mobile-effects-checked.json`, its saved log,
`zombieland-mobile-log-review.json`, `zombieland-feature-audit-final-pairs.json`,
`block-mote-attached-engine.json`, `block-mote-base-engine.json` and
`mobile-cull-details-engine.json`. Block/sound/letter and broader feature gates
remain explicit in COVERAGE.md. No release or feedback archive was sent.

The current delivered gameplay pair subsequently accepts all three production
bump-mote sizes and a moving native attached block mote. The lifecycle companion
calls Tools.CastBlockBubble/CastBumpMote, without forcing ages, changing native
definitions or directly calling TimeInterval. After seven native ticks all
motes have positive alpha. Each passes hidden/visible/hidden-moved/revealed/
hidden-again controls with 0/13/0/13/0 native Mote.DrawAt calls and one unchanged
registration. Bump rotation advances during eight hidden ticks. For the block
mote, a real sprinting Goto job advances sixteen normal-speed ticks while hidden;
the native attachment follows its defender from x214.50 to x215.17, and its X/Z
position matches the updated link plus native attachment offset. Native drawing
can replace its Y altitude, so movement acceptance uses the horizontal position.
Sixty further native ticks span each real lifetime; all four motes are destroyed,
unspawned and deregistered without a direct test deletion. All 24 presentation/
expiry rows and four motion rows pass. This accepts native helper production,
attachment movement, tick aging and cleanup, not melee/parry initiation,
wall-threshold bump initiation, pixels or geometry overlap.

The paired wall-cue companion also accepts nine real Stumble wall-push starts
under normal-speed playback. A scoped minimum of four uses Zombieland's native
single-wall bonus; it creates no fake horde counts. Every start takes one or two
actual game ticks. Native WallPushing one-shots have no samples under hidden
mute or the disabled sound option. Unfiltered and visible controls retain Unity
volume 0.35; the hearing control has factor 0.904229462 and Unity volume 0.3164803.
Hidden home-wall warnings queue the actual DangerousSituation letter without
archiving it, then appear once after reveal. A second normal 35-tick window does
not duplicate them. Visible warnings appear immediately. Sound-off preserves
the warning; warning-off preserves the sound. Outside-home walls produce no
warning, and disabling Total Fog's delay retains immediate native delivery.
These are ordinary playback cue controls with a small scoped fixture. Full
normal horde crossing, camera/frame-dependent rotation and combat stress remain
open independently of the earlier direct 102-step crossing proof.

Initial attempts are preserved as fixture diagnostics. One required every cell
around the wall to be standable instead of selecting a valid crossing strip.
The next used paused DoSingleTick stepping, which skips the unpaused
TickManagerUpdate that initializes Zombieland's scheduler. Eight stepped ticks
also did not establish a walking attachment. Normal playback resolves those
controls. A further wall attempt edited only the current settings object;
Zombieland's interpolation replaced it from the day-based timeline after the
first control. The corrected probe applies/restores just its three scoped
settings across current/timeline owners and verifies the effective values in
every row. These attempts do not establish gameplay defects.

All temporary patches, sight counts, three timeline options and original bump
throttle values are restored. Only owned one-shots are stopped. The unchanged
base save is reloaded and paused; its SHA-256 remains 88ea9ed3... . Both accepted
native logs pass the existing error guard. The canonical paired workflow passes
248 tests. Gameplay remains the delivered 9a76ddd3... / 20e9a34d... pair; only
excluded instrumentation is added, so this checkpoint requires no new tester
ZIP. Evidence is `zombieland-mote-lifecycle-native-normal.{json,log}` and its
separate recorded build-pairs, `zombieland-wall-cues-native-timeline.{json,log}`,
`zombieland-wall-mote-lifecycle-build-pairs.json` and
`zombieland-wall-mote-native-acceptance.json`. Windows remains open.

On 2026-10-06 a normal-playback crossing probe confirms a separate Zombieland
bug: Zombie.DrawPos changed Rotation on each drawing query during ticks divisible
by ten. The before-fix native trace records different facing after a read without
advancing game time. Rotation now advances in ZombieStateHandler.WallPushing;
DrawPos only computes the interpolated position. This adds no saved state,
compatibility hook or fog exception.

Six complete native Stumble crossings pass on the updated pair: hidden/visible
with near/far cameras, colony bypass, and twenty-five extra DrawPos reads per
climbing tick. All use the default minimum of eighteen and fourteen real
supporting zombies. Their native counts are registered through the production
ExecuteMove path, then native Wait jobs hold the staged crowd. No invented grid
counts or direct wall progression calls are used. Each crossing performs 101
native wall ticks and lands after 102 or 103 total game ticks. All 606 observed
wall ticks preserve read purity and the expected simulation-owned rotation.
The wall remains intact, the constructed destination roof is removed, and the
climber retains exactly one draw registration. Hidden and off-camera controls
submit zero pawn draws; visible near-camera and bypass controls submit draws.
This accepts complete normal crossings with a controlled crowd, not unassisted
crowd formation, active combat, off-current-map crossing, climbing save/reload,
boundary pixels or Windows. Zero-threat fixture cleanup is temporarily suppressed.

The first probe also retained four failed later controls: its completed climbers
were destroyed but still present in the native cached crowd set, blocking reuse
of the landing cell. The corrected probe removes only its own completed fixtures
from that set between controls. Those prerequisite failures are not gameplay
crossing regressions. The successful before-fix crossings independently establish
the DrawPos mutation.

The exact updated pair repeats all nine wall sound/warning controls and all 24
mote presentation/expiry plus four motion controls. The canonical workflow passes
248 tests; all retained native logs pass the existing recognized-error guard.
Temporary probes, three current/timeline settings and sight counts are restored,
and the original base save is unchanged and reloaded paused. Gameplay hashes are
Total Fog 9a76ddd3... and Zombieland ff207b8c... . Mortal initially retained the earlier
20e9a34d... Zombieland tester ZIP while the updated pair's required gates ran.
The new exact-pair six-process comparison now passes on the unchanged upstream
1,000-zombie quiet-gap fixture. Three original/candidate pairs alternate O/C,
C/O, O/C with fresh processes, a predeclared 300-native-tick warmup, the same
ff207b8c... Zombieland DLL on both sides, identical settings/camera/loadout,
and no DPA or zombie-work instrumentation. All sampled ticks use ordinary
Ultrafast multiplier fifteen with both speed overrides disabled. Median TPS is
296.6253 original versus 315.0459 Total Fog, 6.2100% higher. Median mean tick
elapsed falls from 2.3799 to 2.2265 ms, 6.4455% lower. Frame p95 is broadly
unchanged at 70.7547 versus 70.3384 ms. All six native logs pass the recognized
error guard. This accepts the current pair's TPS floor on this fixture, not
universal FPS, identical simulation work, load spikes or other mod loadouts.
The exact-pair Symbiant gates subsequently pass again: twelve native/pixel
cases, 54 manual targeting checks and forty dense/sparse 400/4,000-cell
render-cost rows, with clean native logs and all four build/deploy pairs
matching. The prior delivered-pair receipts are preserved separately before
promoting the current records. Evidence is `symbiant-wall-rotation-acceptance`,
`zombieland-current-package-gates.json` and the four
`symbiant-render-cost-*-wall-rotation.{json,log}` records. These are native Mac
rendering and paused diagnostics, not broader gameplay or Windows acceptance.
Performance evidence is
`comparison-wall-rotation-player-1000.json`, its six native reports/logs and
`zombieland-wall-rotation-performance-build-pairs.json`.

The complete current pair was then delivered privately to Mortal at
the replacement tester post,
with the inline robot prefix and preliminary scope. Total Fog's gameplay DLL
is unchanged; Zombieland includes the wall-rotation fix. Separate ZIP sizes
are 2,335,673 and 193,882,831 bytes. Both attachments are downloaded through
the installed Discord connector and match the packaged SHA-256 bytes. The
complete music folder has 53 Ogg tracks plus its README, all identical to the
source. It is not 54 audio tracks. Only after remote-byte verification was the
exact superseded post [private message reference] removed. Evidence is
`mortal-wall-rotation-package-audit.json`, `mortal-wall-rotation-remote-verified.json`
and `mortal-wall-rotation-superseded-delete.json`. The delivery excludes
companion instrumentation and supports only public RimWorld 1.6. Broader
Zombieland gameplay, Windows and long-session acceptance remain open.
Evidence is `zombieland-wall-crossings-{before,fixed}.{json,log}`,
`zombieland-wall-crossings-{baseline,fixed}-build-pairs.json`,
`zombieland-wall-crossings-acceptance.json`,
`zombieland-wall-cues-tick-rotation.{json,log}` and
`zombieland-mote-lifecycle-tick-rotation.{json,log}`.

The existing lifecycle scenario subsequently accepts block initiation through
RimWorld's public Pawn_MeleeVerbs.TryMeleeAttack path, with an explicitly selected
available ZombieBite verb. A capable level-twenty defender and scoped current/
timeline safeMeleeLimit two exercise Zombieland's actual smart-melee branch.
The native attack starts, injury severity remains 9 to 9, and the resulting
Mote_Block is attached to the defender. No direct CastBlockBubble call creates
this mote. It then passes all existing sight, native walking attachment and
natural-expiry controls; the full four-family matrix again passes 24 presentation/
expiry and four motion rows. Hidden mute produces zero new Smash samples. A
positive visible sound control, automatic job/verb selection and repeated combat
are still open; this is one controlled native attack, not a complete combat test.

Two earlier excluded probe versions attempted direct access to curMeleeVerb and
currentTarget because the reference package exposes them. Both are private in
the native engine. DecompilerServer confirms the actual member visibility and
public native attack path; the corrected probe uses public methods and succeeds.
These retained instrumentation failures do not establish gameplay defects.
The current successful native log passes the recognized-error guard, the base
save remains unchanged/reloaded paused, and both gameplay DLLs retain the above
9a76ddd3... / ff207b8c... bytes. The quiet build passes 248 tests and has one
existing nullable warning in InterfaceVisibility.cs, with no compile errors.
Evidence is `zombieland-mote-lifecycle-melee-native.{json,log}`,
`zombieland-mote-lifecycle-melee-native-build-pairs.json` and
`zombieland-mote-lifecycle-melee-native-acceptance.json`.

The ordinary combat matrix subsequently passes seven controls on this same
gameplay pair. Real Stumble jobs select the staged non-player defender and start
native AttackMelee jobs; the probe neither selects attack verbs nor directly
starts attack jobs. Six protected controls cover unfiltered hidden, hidden mute,
visible mute, hearing, hidden again and off-camera mute. Every control performs
at least three automatic attacks and exercises a real ZombieBite parry. A
seventh hidden control disables smart melee and requires unblocked native
attacks with actual injury. The defender is capable, healthy and melee level
twenty; only safeMeleeLimit is scoped across current/timeline settings, and the
existing fixture cleanup suppression remains active.

The seven controls record 21 observed attacks, 16 parries and 3,470 normal-speed
combat observation ticks. Every parry preserves injury severity and skips the
original bite body. The disabled control records three original attacks and
two injury-producing shots, with zero parries. Both actors remain alive and
spawned in their staged cells; the defender is not downed. Hidden mute submits zero bubble draws and
produces zero new Smash samples; the visible control submits 118 actual bubble
draws and retains audible Unity playback. Unfiltered hidden sound remains
audible. Hearing gain matches the live listener distance at each shot, including
the observed change from 0.912764668 to 0.9069785 as other listeners move.

All sixteen bubbles observed at the end of their combat controls naturally
expire and leave the native draw roster after ninety further normal ticks.
No ages or native tick methods are forced. The follow-up may resume NPC combat:
one later original attack occurs in the disabled control, without a later block
bubble. Expiry acceptance follows the exact observed bubble references rather
than assuming the follow-up creates no new effects.

Earlier probe results are retained separately. One hearing assertion incorrectly
used a fixed initial gain despite moving listeners. Another required an original
attack body in every protected control, although automatic selection can legally
choose only bites. The separate disabled control now proves the original route
explicitly. An unguarded follow-up job also raised an exception in the excluded
companion; the native stack resolves to that job assignment. The corrected probe
guards removed actors and records terminal state. The original terminal cause
was not retained, so this does not establish a gameplay defect or its cause.

The canonical paired workflow passes 248 tests, with one existing nullable
warning and no compile errors. Native recognized-error guards are clean, the
four built/deployed DLL pairs match, all temporary probes/settings/sight are
restored, and the unchanged base save is reloaded paused at tick eighteen.
This accepts controlled normal-zombie/NPC combat, sound policy and bubble
lifetimes. It does not accept other zombie types, crowd combat, boundary pixels,
climbing reload, Windows or Multiplayer. No new tester ZIP is sent for companion
instrumentation alone. Evidence is `zombieland-melee-combat-terminal.{json,log}`,
`zombieland-melee-combat-terminal-build-pairs.json` and
`zombieland-melee-combat-terminal-acceptance.json`; earlier diagnostics remain in
`zombieland-melee-combat-{first,observed,settings}.{json,log}`.

## Zombieland horde floor with visible-only HUD, 2026-10-05

The canonical six-process comparison completes O/C, C/O, O/C, 15 seconds per
fresh process at Ultrafast on the unchanged upstream-authored
TotalFog_Zombieland_Upstream1000 save: 250x250, 1,000 zombies and 1,070 total
pawns. Both sides use the updated Zombieland DLL above and all five DLCs.
The candidate gameplay hash remains dada41d3; original fog hash is
`9d011de00451b75d9e27e8b3d09272edc338d30825ee107d757af194f3f1e72c`.
Save SHA-256 is
`2c9a99d39c069a9a6c637e9bcba96d2feb3d000f5654994999df486812f51f2f`.
The workflow verifies matching effective settings, save, loadout, camera/focus
and renderer. Hardware is M4 Max, Metal 4592x2646, vSync 1/target 60 FPS. DPA is
absent/unpatched/not profiling in this isolated Zombieland profile. Hearing cues
are explicitly enabled. This fixture disables targeted letter/category hiding,
delayed alerts and audio checks, so their acceptance cannot be inferred here.

| Median of three sample statistics | Original fog | Total Fog |
| --- | ---: | ---: |
| TPS | 261.496 | 278.414 |
| Mean tick elapsed, ms | 2.7253 | 2.5142 |
| Tick p95, ms | 6.8067 | 6.4445 |
| Tick p99, ms | 12.6601 | 11.7290 |
| Tick maximum, ms | 64.5537 | 61.9540 |
| Mean frame interval, ms | 67.8596 | 69.9228 |
| Frame p95, ms | 76.6415 | 82.1646 |
| Frame p99, ms | 91.1813 | 96.3038 |
| Frame maximum, ms | 113.6686 | 108.8066 |

The targeted TPS floor passes, median +6.470%. Paired TPS changes are +11.492%,
+16.585% and -11.376%, so one pair regresses and these data do not establish a
universal improvement. Frame p95 is 7.206% worse and frame p99 is also worse.
Controlled load-spike behavior, broad frame pacing and native allocations remain
open. The quiet workflow exits 0 with ok and restores the candidate after the
comparison. Six saved comparison logs and the Symbiant root log contain no
recognized errors/configuration failures in the canonical log summary. Evidence:
`artifacts/comparison-visible-zombie-counter-1000.json`, its six runtime reports
and saved logs, `visible-zombie-counter-1000-performance-summary.json`,
`zombie-readout-build-pair.json` and `zombie-readout-and-symbiant-log-review.json`.

## Blocker-source filtering and refresh cancellation, 2026-10-05

A new retained `totalfog/blocker_burst` scenario uses the original-authored
350x350 stress save with all five DLCs, 53 stationary player observers and 6,531
registered watcher components. It changes one distinct open fog-blocker cell
near each observer using the production blocker API, then uses actual Normal
playback through the game's tick loop. The minimum four-tick window can overshoot
at a frame boundary; all actual tick numbers are recorded (six to nine ticks in
these runs). It measures enqueueing separately from MapComponentTick's casts
and elapsed time. Temporary wait jobs, blockers and Harmony observers are
removed/restored by reloading the unchanged named save. No global settings change.

The preceding Total Fog candidate performs exactly 53 casts in the first tick of each
seven-sample burst. Median enqueueing is 0.6956 ms and the busiest map-component
tick is 3.2710 ms. Only 53 sources have a positive sight radius, yet enqueueing
scans all 6,531 registrations for each of 53 changed cells.

The production change retains the complete source roster and maintains a small
positive-radius list for blocker invalidation. Range transitions add/remove a
source; despawn and transfer unlink it. Pending work uses one reusable lazy node
per affected source in FIFO order. A completed synchronous refresh removes its
pending node, but an unchanged periodic check does not. The drain removes each
node before refreshing and processes at most its initial queue length, so a
callback can enqueue fresh work without it being discarded by a trailing Clear.
There is no per-tick quota, freshness deadline or wall-time budget in this change.

The same native regression contract is first run on exact prior gameplay SHA-256
`fb5ef5342a80514b0f88eda4754d0d3c9e41049cb369014ed94d47e9be5d9067`.
It fails synchronous-refresh cancellation, disabled-source pending removal,
despawn unlinking and callback-triggered refresh preservation. The new candidate
passes all nine lifecycle checks: duplicate coalescing, actual-refresh
cancellation, unchanged-check preservation, faction disable/restore, despawn/
respawn, preserving a callback request and completing it on the following drain.
The active list has no duplicates and equals the full roster's positive-radius
subset after those changes.

All seven new burst samples remain stationary, process exactly 53 casts and
empty the pending list. Median enqueueing becomes 0.0241 ms: 96.535% less time,
or a 28.86 ratio for this operation. The busiest map-component tick remains
3.2650 ms, so these data do not establish a casting-kernel or whole-game speedup.
All 223 independent tests pass; paired build/deploy has zero warnings/errors.
Native startup, contract and restored-save logs are clean.

New gameplay SHA-256:
`dada41d3034323760c9306b6cb34ab646b6deb20e7e1b028d91e7931b2298001`.
Evidence: `artifacts/blocker-burst-synchronous-before.json`,
`blocker-burst-old-regression.json`, `blocker-burst-filtered-after.json`,
`blocker-burst-filter-summary.json` and their saved logs. True map transfer,
zero-range powered-building transitions, wider loadouts and Windows remain
open. The separate bounded quota/deadline experiment and Multiplayer
synchronization are not established by this pass.

The same exact candidate also passes the fresh six-process whole-game comparison
against the inherited original fog DLL on `TotalFogStress350`: O/C, C/O, O/C,
15 seconds each at Ultrafast, 350x350, 410 pawns, all five DLCs, identical effective
settings/save/camera/focus/display. DPA is installed in this preserved core
profile, but reports not profiling and unpatched in all six samples; it is not
absent. ShowHearingCues is explicitly true. The unchanged save SHA-256 is
`7e5b6d5c8c8e9e69e394e352c1a48cb4d09f661198ab161af6957d173a300429`.

| Median of three sample statistics | Original fog | Total Fog |
| --- | ---: | ---: |
| TPS | 479.997 | 485.147 |
| Mean tick elapsed, ms | 1.4560 | 1.4402 |
| Tick p95, ms | 2.4202 | 2.4079 |
| Tick p99, ms | 3.4488 | 3.3953 |
| Tick maximum, ms | 25.8093 | 26.7019 |
| Mean frame interval, ms | 66.6518 | 66.6523 |
| Frame p95, ms | 70.1390 | 70.0134 |
| Frame p99, ms | 71.0799 | 71.0730 |
| Frame maximum, ms | 76.5191 | 74.4544 |

Median TPS is 1.073% higher; paired changes are -0.089%, +2.799%, +1.324%.
The TPS floor passes for this fixture, with essentially unchanged typical frame
intervals. Maximum tick time is higher in this run; this is not full tail-latency
acceptance or evidence that the enqueueing ratio applies to whole gameplay.
The workflow finishes `ok`, stops the game and restores the exact candidate;
main/companion bytes match their builds and all six saved logs are clean.
Evidence: `artifacts/comparison-blocker-active-sources-stress.json`, its runtime
reports/logs, `blocker-active-sources-performance-summary.json` and
`blocker-active-sources-paired-deployment.json`.

The new Total Fog gameplay bytes are then checked with the unchanged paired
Zombieland healer fix. All nine hidden/off-camera wound opportunities heal;
ticks advance 3 to 617, with 614 healer CustomTick calls and zero healer draws.
The real rendering contract retains positive 13 beam/glow submissions in sight,
zero in both hidden phases, read-only record identity/age and expired-draw
suppression at paused ticks 3 to 3. The hidden-target effect-on/off images remain
pixel-identical at 4592x2646. Both save transitions, native logs and attention
state are clean. Both mod/companion pairs still match their build bytes.
Evidence: `artifacts/blocker-roster-zombieland.json`,
`blocker-roster-zombieland-pixels.json`, `blocker-roster-zombieland.log` and
`blocker-roster-final-paired-deployment.json`. This rechecks the healer boundary,
not all Zombieland behavior or a new 1,000-zombie performance comparison.

## Zombieland healer simulation and save transitions, 2026-10-05

The earlier visual fix left an independent simulation defect: a completely
culled/off-camera healer never aged its frame-driven effect records. Native
Normal playback on the prior installed Zombieland build records 612 healer
CustomTick calls and zero healer draws. Its list reaches the eight-record
healing limit with every age still zero; later staged injuries stop healing.
Direct DoSingleTick stepping does not exercise Zombieland's production scheduler
and is not accepted as this scenario's simulation evidence.

Zombieland now ages its at-most eight healing records in CustomTick by elapsed
game ticks and removes expired or invalid targets there. The renderer only
reads them. The lifetime is explicitly 60 simulation ticks instead of rendered
frames, so game speed and pause govern expiry rather than FPS or camera position.
Healing selection and the existing Every12 pulse stay unchanged; no Total Fog
simulation layer or saved-format migration is added.

On the final installed pair, all nine staged real Cut injuries heal while both
zombies stay living, spawned, hidden and off-camera. Ticks advance 3 to 618,
615 production healer CustomTick calls occur, and healer draw count stays zero.
Records expire/release capacity normally. The contract uses a scoped fixture-only
prefix to suppress unrelated zero-threat zombie cleanup; the prefix, observers,
test mode and temporary fog setting are restored before the named save reload.
An earlier attempt diagnosed cleanup Crush damage in Zombie.CustomTick, not a
clock-fix death. Armor-dependent damage and direct tick-stepping attempts are
retained only as failed diagnostic setups.

The installed Mono boundary contract passes elapsed ages 0, 12, 55, repeated 55,
60 and 120 (clamped at 60), plus preserving an existing age. The updated real
render contract still produces 13 glow/beam draws when both pawns are visible,
zero with a hidden target and zero when both are hidden. Expired records do not
draw; record identity/count/age remain unchanged by rendering. Paused ticks stay
3 to 3. The hidden-target effect-on/off 4592x2646 images are pixel-identical.

A save transition exposed a separate Zombieland audio-postfix exception in
Map.GetComponent. DecompilerServer inspection of the installed Steam assembly
shows Root_Play.Update returns for LongEventHandler.ShouldWaitForEvent, while
Harmony postfixes still execute; map memory teardown nulls class fields, including
components. The audio postfix now skips waiting or cleared maps. Three
same-process transitions between the visual and simulation fixtures then finish
with no warning-or-higher log entries. Startup and all 223 independent Total Fog
tests pass; the canonical paired build/deploy reports zero warnings/errors.

The native installed Zombieland MVID is
`1ac7a511-c1d4-4b77-bd84-9eb42a6af99a`, gameplay SHA-256
`b48baf76c35b912dbb3d0c3e16250c978cb079d20fb086ec2d4a87ceb96ded9b`.
Total Fog gameplay remains byte-identical to the preceding visibility candidate,
SHA-256 `fb5ef5342a80514b0f88eda4754d0d3c9e41049cb369014ed94d47e9be5d9067`.
Both gameplay/companion pairs match their local builds. Evidence:
`artifacts/zombieland-healing-final-functional.json`,
`zombieland-healing-render-clock-pixels.json`,
`zombieland-healing-final-functional.log`, `zombieland-healing-reload-before.log`,
`zombieland-healing-clock-paired-deployment.json`, and the installed-assembly
source records. This is narrow Mac acceptance; Windows, standalone effects,
removed/transferred targets, reduced-tick combat and broader special-effect
compatibility remain open. No release or feedback ZIP was sent for this change.

The final pair also passes a fresh six-process original/candidate comparison on
`TotalFog_Zombieland_Upstream1000`: 1,000 zombies, 1,070 total pawns, 250x250,
all five DLCs, the updated Zombieland on both sides, and DPA absent. Order is
O/C, C/O, O/C; each measurement runs 15 seconds at Ultrafast with the same
original-authored save, effective fog settings, camera, display and window focus.
ShowHearingCues is explicitly true. The fixture SHA-256 stays
`2c9a99d39c069a9a6c637e9bcba96d2feb3d000f5654994999df486812f51f2f`.

| Median of three sample statistics | Original | Total Fog |
| --- | ---: | ---: |
| TPS | 271.005 | 281.067 |
| Mean tick elapsed, ms | 2.6022 | 2.4367 |
| Tick p95, ms | 6.6218 | 6.4183 |
| Tick p99, ms | 12.0911 | 11.7160 |
| Tick maximum, ms | 68.8022 | 67.2714 |
| Mean frame interval, ms | 68.3815 | 69.3662 |
| Frame p95, ms | 79.1930 | 80.8750 |
| Frame p99, ms | 91.8519 | 87.5807 |
| Frame maximum, ms | 109.7595 | 97.0289 |

Median TPS is 3.713% higher; paired gains are +11.799%, -0.951% and +5.189%.
The TPS floor passes for this fixture. Mean frame interval and p95 are slightly
higher in this run, while p99 and maximum are lower. These mixed frame results
and variable paired TPS do not establish a fixed speedup, full frame-time
acceptance or bounded spike handling. Native environment remains M4 Max/Metal,
4592x2646, vSync 1/target 60 FPS. All six preserved Player.logs are clean. The
workflow finishes `ok`, verifies game termination, and restores the exact
accepted candidate. Both mod/companion pairs still match their functional-test
bytes afterward. Evidence: `artifacts/comparison-zombieland-simulation-clock-1000.json`,
its six runtime reports/logs, `zombieland-simulation-clock-performance-summary.json`
and `zombieland-healing-clock-after-comparison-pair.json`.

## Zombieland healing effects, 2026-10-05

The local paired .NET 10.0.301 build passes 223 source-linked tests. Four new
integration regressions cover owned pawns, remembered objects, vanilla fog and
colony bypass, and a read-only current-sight query. Runtime targets remain
net472. `TotalFog.Visibility.IsVisible(Thing)` exposes the existing live
visibility policy; Zombieland binds it once without a build dependency and
queries only the target of a visible healer's beam/glow.

The existing production Zombieland visual lineup stages a healer and wounded
target six cells apart. The new companion-only contract uses a temporary
five-cell range and real observer movement, restores positions/settings/effect
records in finally, and advances no ticks. It initially observes both targets
so the negative case uses explored, translucent fog. Under the original local
Zombieland DLL, both-visible and visible-healer/hidden-target phases each submit
13 glow and 13 beam draws; both-hidden submits zero. The target's native fog
component is hidden in the negative phase. The effect-on/off screenshot pair
changes 58,049 pixels within `(2182, 825)-(2408, 1572)` at 4592x2646. The beam and
healing silhouette visibly disclose the target.

The initial drawing-only Zombieland fix preserved animation aging/removal outside the drawing
guard and checks that the target is still spawned on the same map. With the
updated local pair, the both-visible phase still submits 13 glow/beam draws;
both negative phases submit zero. The hidden-target effect-on/off screenshot
pair is pixel-identical. Expired records are removed while the target is hidden;
ticks remain 3 to 3. Fresh runtime logs contain no errors or warnings.
This is acceptance for one cross-pawn effect, not full Zombieland compatibility.
The remaining simulation/effect checks are in COVERAGE.md.

The first diagnostic attempt accessed GenDraw's private LineMatCyan field through
the publicized reference assembly and threw FieldAccessException in the probe.
The contract now resolves that field once by reflection; both retained clean
before/after runs use that corrected probe. The failed attempt is diagnostic
evidence only, not a game/mod defect. A first new unit fixture omitted component
setup; it was corrected before the paired build and native tests.

Evidence: `artifacts/zombieland-healing-before.json`,
`zombieland-healing-after.json`, `zombieland-healing-pixels.json`,
`zombieland-healing-first-attempt.json`, and the isolated profile's screenshots.
The tested gameplay SHA-256 values are
`fb5ef5342a80514b0f88eda4754d0d3c9e41049cb369014ed94d47e9be5d9067`
for Total Fog and
`e59ae42fec7dfca8ba92d31b68ae319865ed0c79439ee4a3cecf1b6722471f2c`
for Zombieland. The matching companion/build evidence remains ignored; no ZIP
or release has been sent. The currently published Zombieland DLL does not
include this source fix.

The same 1,000-zombie/1,070-pawn, 250x250 original-authored fixture passes a
fresh six-process original/candidate comparison after the healing fix, using
the updated Zombieland DLL on both sides. Order remains O/C, C/O, O/C; each
measurement runs 15 seconds at Ultrafast, with matching save/settings/loadout,
camera, display and focus. DPA is absent. The original fog DLL is still exact
SHA-256 `9d011de00451b75d9e27e8b3d09272edc338d30825ee107d757af194f3f1e72c`.

| Median of three sample statistics | Original | Total Fog |
| --- | ---: | ---: |
| TPS | 280.100 | 299.294 |
| Mean tick elapsed, ms | 2.5661 | 2.3544 |
| Tick p95, ms | 6.4671 | 6.2138 |
| Tick p99, ms | 12.1954 | 11.7439 |
| Tick maximum, ms | 65.5496 | 63.8780 |
| Mean frame interval, ms | 68.0499 | 67.9982 |
| Frame p95, ms | 78.0475 | 77.4320 |
| Frame p99, ms | 96.4678 | 93.9991 |
| Frame maximum, ms | 109.7541 | 109.1193 |

Median TPS is 6.852% higher. Paired gains are 7.222%, 0.909% and 9.049%, so
neither this nor the earlier run establishes a fixed general speedup. This
fixture passes the TPS floor and has slightly lower frame statistics in this
run. Controlled spike handling and other fixtures remain open. The workflow
finishes `ok`, stops the game and restores the exact tested candidate; both
main/companion pairs match their built bytes. Evidence:
`artifacts/comparison-zombieland-healing-fix-1000.json`, its six runtime reports,
`zombieland-healing-performance-summary.json`, and
`zombieland-healing-paired-deployment.json`.

## Zombieland presentation and .NET SDK, 2026-10-05

The quiet workflow is pinned to .NET 10.0.301, including nested companion builds.
All 219 tests pass and the main/companion build has zero warnings/errors. Gameplay
and companion targets remain net472; tests run on net10.0. The SDK change leaves
the gameplay DLL byte-identical to the previous candidate:
`690ba09ecfb6df4833a5ee9f9b32a2f67ea387255f4d677944d30e476f7afff2`.

`./scripts/mod zombieland-setup` builds/deploys the Total Fog and local Zombieland
pairs through their supported workflows, verifies both pairs and registers the
isolated Steam profile. Zombieland gameplay SHA-256 remains
`899b4f80b4bd760dbde022a2567c0115ac198dff6099862c5056b07d09d29ddb`.
No Zombieland gameplay source was changed. Formatter-only metadata edits from its
canonical build were removed after confirming the repository was initially clean.

Core/all five DLCs, Harmony, RimBridgeServer, Zombieland 5.6.3.0 and Total Fog start
without mod configuration warnings. A 250x250 save stages 100 production-random
zombies plus representatives of Normal, SuicideBomber, ToxicSplasher,
TankyOperator, Miner, Electrifier, Albino, DarkSlimer, Healer and ZombieSpitter.
The reusable diagnostic contract observes native callbacks rather than invoking
draw methods directly. All ten rows receive zero draw-phase/DrawAt/overlay calls
while hidden, positive calls under real observer coverage, and zero after sight
loss. They remain in the renderer exactly once. The same rows pass after reload:
selection is accepted in sight, dropped on sight loss, and ticks remain 83 to 83
during the entire transition contract. Two native reloads regenerate 1,864
sections with no failures or thing-grid mutations. Diagnostic hooks are removed
in finally; they are companion-only and excluded from feedback ZIPs.

Initial diagnostic attempts are retained separately: one target was no longer
living after an unpaused load, inherited Mono MethodInfos needed normalization to
their declaring method, and a first cluster was within the default 60-cell sight
range. The successful remote fixture supplies both positive and negative paths.
The isolated profile now seeds missing preferences from the existing core test
profile (including pause on load), without replacing existing preferences.
These attempts did not establish a Total Fog compatibility defect.

See `artifacts/zombieland-presentation-remote.json`,
`zombieland-presentation-reloaded.json`, `zombieland-render-reload.json` and
`zombieland-paired-deployment.json`. Active special effects, emerging rubble,
projectiles/combat, faction changes, audio/notifications, broader horde performance and
Windows acceptance remain open in COVERAGE.md.

Runtime comparisons support the three isolated profiles via `TOTALFOG_GAME_ID`.
They need a save authored under the inherited binary, which Total Fog can import;
the inherited binary cannot resolve rewritten Total Fog component types. An
attempt using a Total Fog-authored save stopped on the original's XML type errors
before timing and restored the candidate. No performance result was claimed.
The performance export now records ShowHearingCues explicitly. The exact retained
upstream CompTick always runs hearing checks when its range/capacity gates permit
them and has no toggle; its normalized comparison value is True. This causes the
matching-settings gate to reject a candidate with hearing cues disabled rather
than treating DoAudioCheck=False as proof that hearing work is off.

The full six-process comparison subsequently completes on an upstream-authored
250x250 save with 1,000 zombies / 1,070 total pawns, three colonists, a 100-zombie
camera cluster and 900 remote zombies. Sample order is original/candidate,
candidate/original, original/candidate. Each sample lasts 15 seconds at Ultrafast
in a fresh process, reloading the unchanged fixture. All DLL/save/settings,
loadout, rendering and focus checks pass; DPA is absent and no fog method profiling
is enabled. The candidate is restored after the final verified game termination.

| Median of sample measurements | Original | Total Fog | Change |
|---|---:|---:|---:|
| Actual TPS | 269.1089 | 302.6298 | +12.46% |
| Mean elapsed time per tick, ms | 2.65085 | 2.34279 | -11.62% |
| Tick p99, ms | 12.5292 | 10.9715 | -12.43% |
| Tick maximum, ms | 66.6429 | 64.9186 | -2.59% |
| Frame mean, ms | 67.7668 | 68.1715 | +0.60% |
| Frame p95, ms | 77.3646 | 78.4412 | +1.39% |
| Frame p99, ms | 93.3332 | 88.2305 | -5.47% |
| Frame maximum, ms | 117.2926 | 105.6228 | -9.95% |

Original TPS samples are 288.7370, 269.1089 and 259.2666; candidate samples are
302.6298, 285.6481 and 308.7169. Paired gains vary, so comparing sample medians
does not establish a constant 12.46% improvement. The TPS floor passes for this
fixture; its mixed frame results do not close broad FPS acceptance or the separate
bounded-spike work. The native environment is Apple M4 Max/Metal, 4592x2646,
vSync 1/target 60 FPS, all five DLCs and Zombieland 5.6.3.0. Enemy targeting fog,
trees and hidden-audio filtering are off; hearing cues are explicitly on.

Evidence: `artifacts/comparison-zombieland-upstream-1000.json`, the six
`runtime-zombieland-upstream-1000-{original,candidate}-{1,2,3}.json` reports and
their preserved Player.logs, `zombieland-1000-upstream-fixture.json`,
`zombieland-1000-summary.json` and `logs/runtime-compare.log`.

The rebuilt candidate was tested on Steam RimWorld 1.6.4871 rev597 on macOS
with all five DLCs, Harmony and Total Fog. RimBridgeServer and the separate
BridgeTools DLL provided development instrumentation. They are absent from the ZIP.

## Observation changes, 2026-10-04

The source-linked observation suite first reproduced two failures: a new item in
an explored unseen cell was visible, and initialization fallback incorrectly
marked it as observed. Both are corrected. The full suite passed 81 tests and
the main/companion build had zero warnings and errors before deployment.

The real Steam scenario created steel and gold on separate explored unseen
cells. A new item was hidden with `seen=false`. Actual coverage made it visible
and set `seen=true`; removing coverage retained the observed item. A second,
unobserved item stayed hidden. Messages about both items queued while outside
sight, including the remembered visible target. Restoring coverage and advancing
35 ticks emptied the queue and archived each original message exactly once.
Both items stayed at their expected cells, spawned and not destroyed. Fixture
items, synthetic sight and the temporary delay setting were cleaned up.

Tested main SHA-256:
`053e2cb7b247f40af29f83f2c83786570bcf6ade270134cbb40d571f42c067c1`

Tested companion SHA-256:
`f4f1035b2fed66ab0e2aaafc455aaf0f16cf749ff9bdbae2e5ab43ed8de291a8`

The first fixture put both item definitions on the same cell. The engine moved
the first item aside, so its notification correctly remained queued outside the
synthetic sight cell. Separate cells and forbidden fixture items remove spawn
collision and hauling from this observation test. That failed fixture is not
evidence of a notification defect.

Remembered objects still expose live state changes. Frozen last-seen information
and the wider channels listed in COVERAGE.md remain open. These development
changes have not replaced the packaged candidate below.

The optimized development build repeated the real-item transition and then saved
the two observed items in `TotalFogObservedItems`. Their save entries each contain
`seenByPlayer=True`. Reloading visual-ready and paused retained `seen=true`,
`visible=true`, `hidden=false`, the original IDs and positions for both items.
The fixtures were removed after readback. This verifies the observation field's
real save/load path, not every remembered-object state or compressed content type.

Main SHA-256 for this run:
`72d9017cb44b160b9f8a8a921e852e1ccc7dbde1295c4345ec61ef0626094657`

Companion SHA-256:
`656f892494f3649b45b202daec1f0cc31fb3008ee655f89ff749f7662fe22659`

Built and deployed main, companion, preview and icon hashes matched. The supplied
artwork is preserved byte for byte. Repository validation has zero errors and
three documented warnings: shared Languages, the unpublished release workflow,
and the supplied preview's 640 by 360 size versus the 640 by 358 recommendation.

## Remembered objects and live UI, 2026-10-04

Four source-linked regression tests first failed on overlays/tooltips, owned
objects, mouseover filtering and already-selected remembered objects. After the
change, all 89 tests passed and both builds had zero warnings/errors.

The Steam scenario used a late-created static item and a visible selection proxy.
The item was observed and selected, then lost coverage. Its live stack changed
from one to seven. During 13 ticks and 120 hidden frames, the item remained
`seen=true`, `visible=true`, `hidden=false`, but had `inSight=false`, was deselected,
and made zero overlays, tooltips and mouseover calls. The readout was enabled;
both the unseen item's cell and the visible proxy's cell were hovered. Direct
and proxy selection could not select the hidden target. After coverage returned,
60 frames produced 63 overlays, 61 tooltips and 63 mouseovers, and proxy selection
worked again. Synthetic coverage, both fixture objects and persistent hover were
cleaned up. The game stayed paused. The 92-line runtime log had no error or
exception diagnostics.

Tested/deployed main SHA-256:
`0bd37d0d7eba7cd85e7fdb969a2e38f0037e1cb721ea92359819874503429cfc`

Tested/deployed companion SHA-256:
`faccb99991df0c4da890405c1d0ec7b61b2ae3d2c90aef32ac483fb46ae23525`

The earlier fixture kept the inspect tab open and did not exercise mouseover.
The revised run explicitly enabled the readout and demonstrated positive
mouseover calls after revealing the item. This proves live UI gating, not frozen
remembered graphics, terrain or environmental information.

## Live cell information, 2026-10-04

The source-linked suite reproduced unseen environment windows, beauty samples
from unseen cells, and an invalid mouse-cell index. The completed change passes
99 tests, with zero main/companion warnings or errors. Tests also preserve base
rejections, initialization fallback, the colony bypass and vanilla fog.

The Steam probe instrumented real mouseover terrain reads and the final engine
environment-window decision. It queried the engine's actual beauty sample list.
The map was paused and the readout stayed enabled for every measured phase.

| Phase | Raw coverage count | Terrain reads | Allowed window decisions | Beauty samples | Samples outside coverage |
|---|---:|---:|---:|---:|---:|
| Observed, 60 frames | 1 | 61 | 122 | 1 | 0 |
| Unseen, 120 frames | 0 | 0 | 0 | 0 | 0 |
| Revealed, 60 frames | 1 | 61 | 122 | 1 | 0 |
| Colony bypass, 60 frames | 0 | 61 | 122 | 241 | 241 |

The earlier probe reported the engine's visibility policy as `inSight`; that
policy includes the colony bypass. The revised probe measures raw coverage
separately, and observes the window result after all prefixes/postfixes. This
confirms the bypass without mistaking it for observer coverage.

The remembered-item/proxy UI scenario also passed against this build. While
unseen it remained remembered but deselected, with zero live overlays, tooltips
and mouseovers. After sight returned it made 63 overlays, 61 tooltips and 63
mouseovers and could be selected through the visible proxy again.

Main SHA-256:
`ce0bdc1b0dcdee66cd42831470b8dec317ed4d98011517d0876bd091ef146616`

Companion SHA-256:
`f32fee132b5b0020699203ba03c9432b88657a0cf870d667135e2e184e4e171a`

Both deployed hashes match their built files. The 92-line runtime log contains
no error or exception diagnostics. Temporary instrumentation, hover, synthetic
coverage, and UI fixture objects were removed. Temporary beauty and colony
settings were restored. This verifies unseen-cell UI gating, not frozen remembered
graphics or room-wide values whose unseen inputs can still change.

## Packaged candidate b2c53cf, 2026-10-04

- Independent suite: 75 passing tests. Main and companion builds: zero warnings
  and errors. Coverage includes source-linked lifecycle, sparse coverage,
  reciprocal FOV, shared-edge fog alpha, bounded fade arithmetic, loop settings
  and discovery overlays.
- Existing saves load visual-ready and paused with fog already initialized.
- Custom non-pawn drawable: visible instance ran 142 calls in each draw phase,
  140 overlays, 61 tooltips and 61 mouseovers. The hidden instance ran zero in
  every channel. Late definitions produced exactly one component per instance
  and one component entry in their definition.
- Actual rare-ticking 3x2 blocker: all six cells blocked initially and after
  rotation; old exclusive cells cleared; despawn cleared the rotated footprint.
  The scenario advanced 250 ticks.
- Roof, fertility and terrain overlay methods each returned true on a qualifying
  known cell, false when discovery was cleared, and true when restored.
  Independent tests also preserve vanilla false results and the colony bypass.
- Real hidden message and letter: pending count 2, save, load, pending count 2.
  Revealing their target and advancing 60 ticks yielded pending count 0, exactly
  one archived original message and one archived original letter.
- Actual music getter: danger override true, suppression on returned false;
  suppression off returned true. Options were restored after the fixture.
- Real HissJet loops, both cell and Thing sources: Unity AudioSource volume was
  0 when hidden/muted, 0.320000023 unfiltered, and 0.241254285 hearing-filtered.
  With muting still enabled, sight restored the same loop to 0.320000023 and
  losing sight returned it to 0. Disabling filtering restored full volume.
  Original SoundInfo volume remained 0.8; the loop never ended during transitions.
  These are engine volume measurements, not a physical listening test.
- Actual 10,000-point FleshbeastAttack spawned 60 pawns, 42 hidden at the
  reported endpoint. Across 900 simulation ticks and 900 Unity frames, no hidden
  draw phases or captured drawing errors occurred. Visible and held-visible pawn
  draw/preparation phases continued. The bounded game log contains no exceptions.
- The ZIP has one TotalFog/ root, only supported version 1.6, and no source,
  diagnostics, historical version folders, private engine references, or companion.
  Its runtime DLL is byte-identical to the build and deployed Steam copy.

Main DLL SHA-256:
`65b8533efcb3f29060257796a0a4be9f46891fa846d4b9e262316d89906b2a3b`

ZIP SHA-256:
`f90af6c1540413825cd4617ec52c0983ffa2eabc3773677ca7662261796abc63`

Raw candidate scenario results, logs, test saves and hash reports are retained
locally in ignored artifacts/. Run ./scripts/mod package for the canonical
checks and packaging, and ./scripts/mod benchmark for the comparison workload.

## Comparison and corrected failures

The focused c7056eb fork is the comparison checkpoint. The inherited binary
registered a hidden pawn twice after respawn and recorded 24,440 Draw calls
behind fog in the splitting-fleshbeast fixture. The focused fix preserved one
registration and eliminated hidden drawing. The rebuilt renderer continues to
leave all registration and preparation lifetime under the engine's ownership.

An early rebuilt load-finalization hook requested Unity meshes on the background
loading thread and crashed. It was moved to LongEventHandler.ExecuteWhenFinished.
Both regression and notification saves then loaded successfully while paused.
The diagnostic failure log is retained locally; it is not a current failure.

The inherited roof overlay had an incorrect TerrainGrid instance signature for
RoofGrid. The replacement injects the common Map field directly for all three
grids. No claim is made that the old signature caused an observed runtime crash.
Per-cell Traverse calls were also removed from discovery and placement adapters.
The inherited hit-check cache was removed because discovery, blockers or faction
coverage can change without a new game tick.

## Owned static objects and inactive sources

Three source-linked regressions first failed: ownership rendered an unseen
static object, invented its observation, and revealed an owned projectile.
The corrected adapter reserves the presentation exception for player pawns and
their flight containers. Additional source-policy tests distinguish an inactive
zero-range building from a zero-range pawn observing its own cell. The complete
suite now passes 109 tests with zero build warnings or errors.

The real owned granite wall had coverage 0, vision range 0, `seen=false`,
`visible=false` and `hidden=true` after spawn and after 35 ticks. Applying real
coverage changed these to coverage 1, `seen=true`, `visible=true`, `hidden=false`.
Removing coverage and advancing another 35 ticks kept its remembered drawing
eligible but returned current-information visibility to false. This verifies the
wall's actual sight-source reconciliation; dedicated camera/turret power and
watcher cases remain open.

The same deployed candidate repeated the remembered-item/proxy UI scenario:
zero overlays, tooltips and mouseovers while unseen, selection blocked, then
63 overlays, 61 tooltips and 63 mouseovers after reveal. The bounded runtime log
contains no errors or exceptions. Main and companion installed bytes match the
build; hash reports and raw results remain in artifacts/.

## Native rendering replacement, 5 October 2026

The lean candidate's independent suite passes 124 tests, zero warnings/errors.
Updated drawing contracts first rejected the leftover static cull behavior, then
passed after native drawing was restored for observed static objects. Unobserved
objects and moving pawns still require the appropriate observation/current sight.

Main DLL SHA-256 `e0306f412d7a3331485591f392e6ee5b4973497f0ef207fe633e5970744b6d15`.
Fresh 250x250 native debug game became visually ready in 9,581 ms, with fog
initialization 393 ms. Ten seconds of normal native playback advanced 596 ticks
in 10,064 ms. Mortal's original save became visually ready in 4,807 ms, with
fog initialization 454 ms. Its native 900-tick combat run included one initial
Fingerspike death, no recorded render errors, no duplicate registration and no
hidden-pawn draws. These are Mac Steam 1.6 measurements, not Windows results.

The real static-item/proxy scenario keeps native draw phases running after sight
is lost, while selection, overlays, tooltips and mouseover callbacks remain
blocked. Sight restoration enables them again. Dedicated GPU capture/archive
scenarios and their production code are removed. New frame/tick instrumentation
belongs only to the development companion and is not included in the player ZIP.

Whole-game comparison with the byte-identical inherited upstream binary and
Mortal's Windows confirmation remain open. Normal-speed TPS near its cap cannot
establish a performance advantage.

## Removed visual-memory experiment

The strict drawing-snapshot feature and its dedicated GPU/archive tests were
removed on 5 October 2026 after Mortal reported severe Windows stalls. The
startup diagnostic measured 38,158 ms for fog initialization on his 250x250 map.
The first four logged GPU captures completed in 2–3 ms individually; this does
not isolate the full cost or prove a single responsible operation.

Earlier resource and pixel experiments are preserved in Git history. Their
results are not acceptance evidence for the current native rendering design.

### Original player combat save

Mortal identified intermittent Fingerspike deaths as the error trigger and
supplied FleshbeastAttack.rws, saved on Windows 1.6.4871 rev591 with Harmony,
Core, all five DLCs and the NWN Real Fog of War package. The preserved original
is 20,229,326 bytes, SHA-256
9848e9a95564639a4e3b13313779b4df4c49d1d9c2a7e5b228629fbb21c75aa2.
Loading with Total Fog deliberately substitutes the missing original package;
the compatibility mismatch is recorded rather than presented as a compatible
original loadout. The isolated Steam Mac engine is rev595.

Native DynamicDrawManager culls pawns at their interpolated DrawPos cell. The
previous Total Fog gate instead checked the logical position, allowing or hiding
walking pawns on the wrong side of a sight boundary. The gate now uses the
native cull entry's cell and translates the complete occupied footprint. Other
callers retain logical-position semantics. Six source-linked regression cases
cover both crossing directions, matching cells, oversized footprints and an
out-of-map drawing cell. Four failed before the fix; the full suite now passes
299 tests with zero build warnings or errors.

The unchanged player save ran from tick 1588 through 2488 without spawned
fixtures or forced deaths. Two original Fingerspikes and one Toughspike died.
All three visible drawing phases executed 79,987 times each; visible held-pawn
phases executed 175 times each. No observed phase ran outside current sight,
no duplicate draw registration remained and the runtime log was clean. The
probe records completed native phases and independently checks their actual
drawing positions, rather than using the older cached component-hidden flag.

The canonical loaded-combat verifier passes preserved evidence in
artifacts/mortal-combat-current-sight. A deliberately inserted hidden draw
count fails the verifier and removes its seeded stale success manifest.
Main SHA-256 is
bcfb5ec9482b941588d18a255c534ef43a2371c6fd902346b69d03fd93c7310c;
companion is
15c8647896620b5e6c3e9ce1a1181acb61a7c49d1225117f90cd2779667c9c6c.
This establishes the measured substituted-save contract on this Mac. It does
not reproduce or close the exact Windows null-render-node error, prove every
alert or provide a whole-game performance comparison.

## Installed-state diagnostic

`totalfog/installed_state` checks the loaded assembly, actual mod instance and
current-map fog component. Its optional initializer recheck captures a cached
startup failure rather than treating an active-mod-list entry as running code.
The deployed 1.6 pair was checked at the main menu and after a real visual-ready
load of TotalFogWindControl. Both initializer checks succeed with no exceptions;
the loaded map has an initialized fog component. The main DLL remains the exact
preliminary feedback candidate. Evidence is in artifacts/installed-state-*.

## Settings window

The native 900x700 settings dialog has Appearance, Vision, Information and Audio
tabs. Its scroll viewport is 820x452 logical pixels; the content width is 776,
reserving 44 pixels for the scrollbar and a 28-pixel gap beside the controls.
This increases the previous gap by 10 pixels. The deployed layout was checked
on 2026-10-05: Appearance and Information use the narrower content width, and
Information was captured at both scroll extremes with no horizontal scrolling.
The right-side checkboxes and descriptions remain clear of the scrollbar, and
the fixed Reset and Close controls remain visible. All 195 independent tests
pass; the main and companion build has no warnings or errors. Built and deployed
DLL hashes match. Layouts, screenshots, and hashes remain in artifacts/settings-gap-*.

Live widget clicks enable hearing-based filtering and reveal its numeric range
and muffling slider. Disabling hearing indicators removes the indicator-range
control. Both fixture toggles were returned to their values before the test.
The tree-blocking checkbox reports disabled while playing and rejects a click;
it now passes the disabled flag to the native widget explicitly. Merely setting
GUI.enabled was insufficient for RimWorld's input-driven checkbox implementation.
Using a direct native checkbox also removes a bridge click-target mismatch
between the listing's estimated row rectangle and the actual widget rectangle.

The deployed main and companion bytes match the build. The canonical deployment
passes all 109 tests with zero build warnings/errors, and the bounded runtime log
has no errors, exceptions or missing English keys. Settings save/load, reset
behavior, other languages and layouts at smaller UI scales remain open checks.

## Performance boundaries

Retained pure-caster benchmark: 500x350 grid, radius 60, seed 413, five samples
of 5,000 casts after warmup, median elapsed time, tiered compilation disabled.
The previous Total Fog caster is pinned to b2c53cf. Before timing, 512 randomized
rectangular maps and 4,096 point queries must match that reference exactly.
The current independent suite passes 83 tests with zero build warnings/errors.

| Blocker density | Inherited ms / 5,000 | Previous Total Fog | Optimized Total Fog |
|---|---:|---:|---:|
| 0 | 183.7511 | 184.7783 | 163.2514 |
| 0.03 | 115.0276 | 141.8688 | 131.5853 |
| 0.15 | 19.4437 | 23.4173 | 22.8327 |
| 0.35 | 8.3858 | 8.6394 | 8.7218 |

| Blocker density | Previous point queries ms / 5,000 | Optimized point queries |
|---|---:|---:|
| 0 | 18.0360 | 5.6328 |
| 0.03 | 19.4668 | 4.4626 |
| 0.15 | 2.9182 | 1.1525 |
| 0.35 | 0.9109 | 0.3574 |

All measured kernels allocate zero after warmup. Previous and optimized Total
Fog reveal identical cells and return identical query results. The inherited
caster uses different geometry; its times alone cannot justify exchanging the
algorithms. The retained optimization improves the sampled point queries by
2.5–4.4 times. Full casts improve less, and the densest sample is roughly
unchanged. A coordinate-increment variant was rejected for its open-field
regression. These results use .NET 10 outside Unity; no engine-JIT, whole-game
FPS or memory improvement is claimed yet.

Mortal's clip shows a null render-node error on Windows rev591. That exact error
loop has not been reproduced on this Mac. Windows confirmation and named optional
mod acceptance remain outstanding. See COVERAGE.md for the remaining scenarios.


## Native performance floor, 2026-10-05

The new `runtime-benchmark` command reloads the unchanged MortalFleshbeastAttack
fixture before each of three 15-second native playback samples. A temporary
companion hook times the complete native `TickManager.DoSingleTick`; frame
intervals are recorded without GPU readback. These are normal-speed samples,
so capped TPS does not establish simulation capacity.

The inherited runtime DLL matches upstream SHA-256
`9d011de00451b75d9e27e8b3d09272edc338d30825ee107d757af194f3f1e72c`.
Both variants use the same 250x250 map, 179 spawned pawns, settings hash and
4592x2646 Metal view, target 60 FPS/vSync 1, on the Apple M4 Max. The fixture hash
remains `9848e9a95564639a4e3b13313779b4df4c49d1d9c2a7e5b228629fbb21c75aa2`.

| Variant | Median sample mean tick elapsed, ms | Median sample frame p95, ms |
|---|---:|---:|
| Original mod | 2.835321 | 28.4138 |
| First replacement without drawing snapshots | 3.492582 | 23.8848 |
| Row-based footprint differences | 3.168790 | 23.2346 |

The row-based differences retain the exact changed cells, additions before
removals, empty/reused masks and differing-grid-width behavior. Two regression
tests exercise 256 seeded footprint pairs and the cross-width contract. The
126-test build passes with zero warnings/errors. Tested main SHA-256 is
`8037b1b655665cfccdc332943645939a94df90b5c93ae6e2773e4408d1368481`.

This saves about 9% of whole-tick elapsed relative to the first lean replacement,
but remains about 12% above the original. The minimum performance requirement
is **not met**. Better frame intervals do not close that gap. Reports remain in
`artifacts/runtime-original-normal.json`, `runtime-lean-normal.json` and
`runtime-row-mask-normal.json`. Windows confirmation is also pending.


## Independent runtime identity, 2026-10-05

The runtime now uses TotalFog namespaces and independently named types, including
TotalFogMod, FogSettings, MapVisibility, CompFog, CompSightSource, CompVisibility
and CompPresentationState. All 14 active XML files resolve the new names.
Historical version payloads and original attribution remain separate.

DecompilerServer and the actual loaded assembly both report zero
RimWorldRealFoW namespace types. The native mod instance is TotalFog.TotalFogMod;
the loaded and initialized map component is TotalFog.MapVisibility. Specific
old save types are handled only as unresolved loading-time fallbacks. Existing
resolved types, unrelated names and incompatible base types are preserved;
no original-namespace alias classes are declared.

MortalFleshbeastAttack loads visual-ready in 4,849 ms. Saving as
TotalFogIdentity-20261005 writes exactly one TotalFog.MapVisibility and one
TotalFog.DeferredNotifications, with no original-namespace Class attributes.
The discovery bytes are identical to the original save, retaining all 62,500
known cells. The original fixture remains unchanged. The new save reloads
visual-ready in 4,288 ms with matching active-mod metadata.

A later native power-grid regeneration produced the same list-index exception
seen in Mortal's log. The identity and discovery checks pass, but this is not a
clean-render or release-readiness claim. The cause and its subsequent fix are documented below.

The source-linked import boundary suite verifies eight explicit mappings,
already-resolved types, unknown names, wrong base types, and all non-loading
modes. The complete build passes 139 tests with zero warnings/errors. Evidence
remains in artifacts/identity-*. The renamed mod entry point has its own settings
filename; earlier unpublished preliminary settings can be selected again.

Final identity main SHA-256: `322916b08867ea87c32e16d50b1e337f0637f665a1114d4ccc1e27ed254430a3`.
Packaged and deployed main bytes match; the ordinary source commit does not
promote the tracked release-snapshot DLL.

## Native flyer/grid regeneration fix, 2026-10-05

A temporary companion probe reproduced the power-grid exception twice at cell
84,122. Its thing list shrank from four to three entries while the renderer
iterated it. The trace identified PawnFlyer.DrawPos -> RecomputePosition ->
Thing.Position -> ThingGrid.DeregisterInCell, called by our shared visibility
policy inside SectionLayer_ThingsPowerGrid.TakePrintFrom. DecompilerServer
confirms that DrawPos recomputes and changes the flyer ground position.

The visibility policy now reads PositionHeld or an explicit rendered cell,
without calling the mutating getter. The dynamic-render adapter requests the
flyer drawing position outside section iteration, preserving the actual visual
sight boundary. Regression tests cover getter side effects and differing
logical/rendered visibility in both directions. No exception is swallowed and
no engine list is copied, filtered or patched around the failure.

Three native reloads of MortalFleshbeastAttack pass all 3,258 section
regenerations, with zero captured errors and zero grid mutations inside them.
Evidence: artifacts/render-probe-identity-result.json and
artifacts/render-flyer-fix-result.json. Windows confirmation remains pending.
The diagnostic Harmony patches are installed only for the companion scenario
and removed in its finally block.

## Maximum-speed comparison, first packed-mask candidate

The user confirmed the fourth speed button as the acceptance target. Three
15-second native Ultrafast samples reload MortalFleshbeastAttack each time. The
original DLL remains byte-identical to the preserved baseline; its original
XML runtime bindings are restored from the initial retained definitions, since
the renamed Total Fog classes cannot resolve against that DLL. The 26 common
effective settings, fixture hash, map/pawn counts, hardware and renderer match.

Packed mask/flyer-fix candidate 9d3170ab9d654b1e2a5085869c158b7fb020960988384d671ab2f9b05f1dc546
measures median sample mean tick elapsed 2.839902 ms and median actual 218.54 TPS.
Original measures 2.774175 ms and 238.06 TPS. The tick elapsed gap is approximately
2.4%, but actual throughput is approximately 8.2% lower. The performance floor
remains unmet for this candidate. Native render work also matters. Reports:
artifacts/runtime-packed-flyer-ultrafast.json and runtime-original-ultrafast.json.

## Observable notification boundary, 2026-10-05

Discard settings now apply only to unseen targets. Targetless global events
and colony pawn/prisoner health events remain immediate. Visible events remain
immediate even when their category is configured for discarding. The broad
category settings are no longer an unconditional filter on observed events;
labels and the settings explanation describe this behavior.

Source-linked regressions cover owned/prisoner events outside current sight,
unseen enemies, remembered enemy buildings, targetless global events and
destroyed colony targets. The native notification_observability scenario sends
real letters through LetterStack with discard/delay enabled. Colony health,
global-condition and visible-threat letters enter the archive; hidden discarded
and deferred threats do not, and exactly one hidden letter is queued. Settings
and coverage counts are restored, then the fixture reloads to remove all test
notifications. Evidence: artifacts/observable-notification-native.json. This
proves the notification boundary, not every incident worker's choice of targets.

## Sight work follows the enemy-targeting option, 2026-10-05

Non-player coverage is maintained only while enemy fog targeting is enabled.
Player observers and configured allied/prisoner observers retain their coverage.
Disabling the option preserves the incoming enemy hit-check result, including
results from earlier patches; player targeting still checks current sight.
Applying settings forces source refreshes, including while paused.

The native sight_work_scope scenario starts with a refreshed paused fixture,
toggles the option both ways and then restores it. Enemy sight sources change
from 0 to 73; every player coverage count is unchanged in both directions.
Enemy native true/false hit results are preserved with the option disabled,
while hidden player targets and hidden enemy targets with the option enabled
are rejected. Evidence: artifacts/sight-work-scope-stable-native.json.

Three clean fourth-speed samples of the candidate f27af4aa674fd19f4903b49baf34ffc9ce84201ffca971bf102927be28dcd959
measure median sample mean tick elapsed 2.556763 ms and actual throughput 232.50 TPS.
The original measured 2.774175 ms and 238.06 TPS. Elapsed time per tick improves about
7.8%, but actual throughput remains about 2.3% lower; the performance floor is
still unmet. Reports: artifacts/runtime-sight-scope-ultrafast.json and
artifacts/runtime-original-ultrafast.json. Shared settings and fixture match.

An earlier interrupted run logged a PawnUtility.PawnBlockedBy null reference
from PathGridDoorsBlockedJob between reloads. Its stack contains no Total Fog
method. The full log was preserved in artifacts/scope-benchmark-pathing.log,
and the run was discarded. The subsequent fresh-process three-sample run
contains no such error; the original cause is not established.

## Map component lookup and rendering work, 2026-10-05

A map-owned weak-key lookup replaces repeated component-list scans. Four
source-linked regressions verify existing-component identity, separate maps,
missing-component registration and collection of an unloaded map whose component
refers back to it. All 168 tests pass at this checkpoint.

The native map_lookup_cost probe alternates three 500,000-call samples per
implementation on the paused fixture, with 13 native map components. Cached
lookup median is 29.1327 ms; engine GetComponent median is 201.7858 ms, about
6.9 times slower for this operation. This is a lookup measurement, not a
whole-game speed claim. Evidence: artifacts/lookup-cost-native.json.

The full-game lookup candidate 3ebe505f23ca73d1f21c96a4afd2287d7443210b305b0e10149fcb06f844b590
measures median actual throughput 244.60 TPS against the nearby original control
at 243.30 TPS. Sample variation is substantially larger than the 0.5% difference;
a repeatable whole-game advantage is not established. Reports:
artifacts/runtime-weak-lookup-ultrafast.json and runtime-original-control-ultrafast.json.

The next source change samples each fog section's expanded cell region once
instead of rereading nine neighbors per cell, then emits the same native vertex
topology. 512 random clipped sections match independently calculated vertex
maxima, and the batch operation allocates nothing after warmup. No-faction sight
sources clear their previous coverage and return before allocating/resetting
unused footprints. Native verification and timings for this change follow.


## Fresh-process performance controls and larger fixture, 2026-10-05

The routine benchmark now uses a new native process for every sample and
records focus continuously. Each sample preserves its Player.log; a failed or
partial report is explicitly incomplete. Changing the map, rendering environment,
effective settings, installed DLL or fixture stops the run. No error attention
is automatically acknowledged.

Two repeated-reload runs produced a native PawnUtility.PawnBlockedBy null
reference from PathGridDoorsBlockedJob. Decompiled public engine code shows
that the worker reads mutable pawn/job state. The root cause is not established;
there is no exception-swallowing or vanilla pathing patch. Fresh-process runs
avoid that condition, but do not establish ownership of the underlying defect.
An earlier batch timing run overlapped desktop interaction, and cannot support
a performance regression claim. Later original controls also varied substantially
while unrelated Simulator/crash-reporting work was active.

For main e76f03a2e00ba69283eb539f50e2a468b80858b15074179b3dc6fdaa3a01d355,
three fresh-process 15-second Ultrafast samples on MortalFleshbeastAttack give:

| Binary | Median sample mean tick elapsed, ms | Median actual TPS | Median frame p95, ms |
|---|---:|---:|---:|
| Original | 2.777071 | 233.32 | 86.4464 |
| Total Fog | 2.575501 | 253.05 | 87.8944 |

All 26 shared effective settings, fixture, 250x250 dimensions, 179 pawns,
hardware and renderer match. Enemy fog targeting is disabled. Tick elapsed time is
about 7.3% lower and throughput about 8.5% higher on this fixture, while frame
p95 is slightly worse. The six logs contain no exception diagnostics. Reports:
artifacts/runtime-original-fresh-ultrafast.json and runtime-batch-fresh-ultrafast.json.
These bounded Mac results do not establish a large-modpack or Windows guarantee.

The batched section sampler matches every target opacity across 225 native
sections and 562,500 vertices, with zero mismatches. Evidence:
artifacts/batch-mesh-native.json. The sight-option scenario also passes on these
exact packaged bytes: artifacts/batch-sight-scope-native.json.

A companion-only tool creates a native 350x350 map with 50 added moving
colonists and 250 added wild muffalo. The resulting fixture has 53 colonists,
357 animals and 410 total pawns. It is generated/saved using the original DLL
so both binaries can load the same frozen data. Diagnostic patches are removed
in finally; this tool is absent from the gameplay ZIP. It is a synthetic fresh
colony stress case, not a developed late-game colony or a modpack.

The first playback stopped on the normal Ancient danger major-threat automatic
pause at tick 3415. That incomplete run is discarded. The measurement companion
now temporarily disables automatic letter pauses, preserves all letters and
restores the preference in finally. Subsequent three-sample comparisons use
identical instrumentation and MajorThreat preferences before each sample.

| Binary | Median sample mean tick elapsed, ms | Median actual TPS |
|---|---:|---:|
| Original | 1.431108 | 489.01 |
| Total Fog e76f03a | 1.495759 | 468.28 |

The larger fixture exposes a roughly 4.2% throughput regression; the overall
performance floor is therefore still unmet. Reports:
artifacts/runtime-original-stress-uninterrupted-ultrafast.json and
runtime-totalfog-stress-ultrafast.json. This candidate has not been sent as an
improved-performance feedback build. Profiling and optimization continue.

The whole-tick probe uses Stopwatch elapsed time. Its legacy report field is
`tickCpuMs`; it includes OS scheduling delays and is not isolated thread CPU
time. Host contention limits interpretation of sequential timing runs.

## Dubs Performance Analyzer integration, 2026-10-05

Steam Workshop Dubs Performance Analyzer 1.6.10 is enabled only in the isolated
public 1.6 test profile. The bridge confirms its package, runtime types and
members are available. `./scripts/mod dpa-setup` verifies the installed metadata,
retains the profile and deploys the current main/companion pair.

The native 350x350 / 410-pawn fixture was profiled for eight seconds at
Ultrafast. All 19 targets, including the general tick preset and the explicit
Total Fog methods, resolved and patched successfully. The DPA snapshot retains
the last 2,000 tick entries. Inclusive average elapsed milliseconds per tick:

| Method | ms per tick |
|---|---:|
| CompFog.CompTick | 0.246886 |
| CompSightSource.CompTick | 0.154212 |
| CompSightSource.UpdateFoV | 0.138228 |
| FieldOfView.ComputeMask | 0.089317 |
| VisibilityMask.ApplyDifference | 0.058063 |
| ThingVisibility.IsVisible | 0.033080 |
| CompVisibility.UpdateVisibility | 0.031752 |
| MapVisibility.VisibilityChanged | 0.024908 |
| FogMapUtility.GetVisibility | 0.011676 |

These nested totals overlap, include profiling overhead and must not be added
or treated as uninstrumented TPS acceptance. The snapshot points primarily to
sight work, rather than map lookup or presentation checks. After stopping and
cleaning up, a follow-up DPA status confirms `currentlyProfiling=false` and
`isPatched=false`. The native game was then stopped with verified termination.
Evidence: artifacts/dpa-stress-tick-patch.json,
dpa-stress-tick-playback.json, dpa-stress-tick-snapshot.json,
dpa-clean-status-native.json and dpa-profile-end-stop.json.

The alternating comparison now explicitly stops and verifies each native game
before the next DLL deployment. Closing the MCP subprocess alone proved to
request asynchronous shutdown, which allowed a deployment to begin while the
old process remained alive. Incomplete comparisons remain marked incomplete.

The alternating three-pair comparison completed on candidate main
45431d9dd1bf3f6a3ddbc415aaa867a1bd38a7c9794c832ab36c1bf1f5dc17dc.
Original TPS samples: 407.49, 479.64, 470.29. Candidate: 411.02, 457.06, 458.78.
The median remains below the original: 457.06 vs 470.29 TPS, approximately
2.8% slower. Median sample mean tick elapsed is 1.531715 vs 1.486957 ms;
frame p95 is 69.4289 vs 69.3190 ms. All six runs retained focus, matching
fixture/settings/hardware and exact DLL hashes. The command correctly failed
the TPS floor and restored the candidate with every game process terminated.
Report: artifacts/comparison-dpa-hot-path-stress.json. The performance floor
is still unmet; the candidate is withheld from the improved-performance ZIP.

The original was then profiled with its corresponding component methods and
the same DPA tick preset. All 15 targets resolve, and an eight-second native
playback produces a 2,000-entry snapshot. Original inclusive per-tick costs
include position tracking 0.011112 ms and visibility refresh 0.007448 ms,
compared with candidate 0.027002 and 0.031752 ms. These profiles have different
nested targets and trace windows, so they locate work rather than establish a
precise cross-binary CPU ratio. The original caster also updates coverage
inside casting; its raw casting boundary differs from ComputeMask. Its whole
sight update is about the same scale as ours. Evidence:
artifacts/dpa-original-stress-tick-patch.json,
dpa-original-stress-tick-playback.json and dpa-original-stress-tick-snapshot.json.
Original profiling was stopped, cleaned up and confirmed unpatched before
verified native termination.

Six production-linked cell-registration tests cover single-cell turning,
movement before the periodic deadline, larger rotated footprints, definition
size changes, map changes and repeated despawn cleanup. The first test run
reproduced excess writes on turning and the missed size-only change. The fix
tracks the definition size, ignores rotation only for one-cell footprints and
returns before the map lookup when geometry and map are unchanged. All 179
tests pass and main/companion builds have zero warnings or errors.

Candidate main 219d33d3f65b261a6edf648e2cb1dc5ddc48c2138c0951fbaa85a8a49a9225f3
passes Mortal save loading with 1,082 native section regenerations and no errors
or thing-grid mutations. Enemy-targeting scope also passes after 250 native
ticks: disabled zero enemy sources; enabled 94; zero changed player coverage
cells in both transitions, with native enemy hit results preserved when disabled.
Evidence: artifacts/cell-registration-render-native.json and
cell-registration-sight-scope-native.json.

The expanded native 3x2 rare-ticking blocker scenario confirms all six cells
are registered initially and after rotation, the old exclusive cells are
unregistered, and despawn clears both registrations and occlusion. Evidence:
artifacts/cell-registration-registry-native.json. DPA state and the active
package set are now recorded and compared in every fresh timing process;
active profiling patches stop the performance run.

The follow-up comparison of main 219d33d3 completed but overlaps new Simulator
UI-test startup and subsequent compilation work. Candidate TPS samples were
380.28, 245.49 and 431.05; original samples 454.02, 373.53 and 412.20. This run
cannot establish a code regression or satisfy the performance floor. All
processes confirmed DPA inactive and unpatched. Preserve the raw report in
artifacts/comparison-cell-registration-stress.json and the contemporaneous
host evidence in cell-registration-stress-host-contention.json. Reliable TPS
acceptance of this source revision remains pending an idle-host comparison.

The next quiet-host alternating comparison completes on the same candidate
219d33d3. Original TPS: 472.56, 475.64, 472.44; candidate: 464.22, 481.70, 469.54.
The median is now 469.54 vs 472.56 TPS, approximately 0.64% below the original.
Median sample mean tick elapsed is 1.493068 vs 1.477626 ms. This is near parity
within observed sample variation, but the strict TPS floor still fails and
no improvement is claimed. All six DPA checks report inactive/unpatched,
matching active packages, and the host observations show low outside CPU load.
Report: artifacts/comparison-cell-registration-idle-stress.json. Original
samples are closely grouped; candidate variation still warrants investigation.

DPA common-query follow-up on the same 350x350 fixture, 2,000 captured ticks:
main 219d33d3 calls the gravship exception 372,207 times and the engine
controller getter 377,276 times. The accessor linearly searches world components.
The candidate a4e759f6 retains the controller under a weak world key, reads
landing state live, does not cache missing controllers and avoids caching during
initialization. Owned pawn presentation also returns after the vanilla fog guard
before the component/exception queries. Native engine getter calls fall to 4,978
in the follow-up trace; the gravship exception's inclusive per-tick average falls
from 0.013289 to 0.011367 ms. These are profiling traces, not TPS acceptance.
Evidence: artifacts/gravship-lookup-profile-snapshot.json and
gravship-cache-profile-snapshot.json. Both runs stop/clean DPA and confirm no
profiling patches before verified game termination.

The initial test run reproduces redundant pawn lookup and repeated controller
searches. All 188 tests now pass; main and companion builds have no warnings
or errors. Tests use production source and cover live landing changes, distinct
worlds, initialization replacement, missing controllers, unloaded-world GC,
zero warm allocations, disabled DLC/setting and the owned pawn vs live UI policy.

Native a4e759f6 landing-state scenario passes: absent false, confirmation true,
setting disabled false, confirmation removed false. All 1,000 warm queries
avoid the actual Find.GravshipController getter. The marker, setting and
diagnostic Harmony patch are restored before returning. Evidence:
artifacts/gravship-cache-native-state.json.

The same deployed main a4e759f6 also passes Mortal fixture reloading with 1,082
native section regenerations, zero exceptions and zero thing-grid mutations.
Evidence: artifacts/gravship-cache-native-render.json. Strict alternating TPS
acceptance of this candidate is still pending.

The subsequent six-process alternating comparison completes on a4e759f6 but
fails the TPS floor. Original samples: 485.13, 481.17, 462.89 TPS. Candidate:
449.96, 475.77, 464.65 TPS. Medians are 481.17 vs 464.65, about 3.43% lower
for the candidate. Median sample mean tick elapsed: 1.451085 vs 1.507917 ms;
frame p95: 69.1067 vs 69.1218 ms. All package/settings/fixture/renderer checks
match and all six processes confirm DPA inactive/unpatched. This run does not
establish that caching itself caused a regression; previous candidates were
measured in a different cohort. It does establish that the current candidate
cannot be accepted as meeting the runtime floor. Evidence:
artifacts/comparison-gravship-cache-stress.json and the host start/observations
files. Candidate exact bytes are restored by the workflow; no feedback ZIP
is promoted from this result.

The native query microbenchmark rejects the weak gravship-controller cache.
With DPA confirmed inactive/unpatched, three alternating 500,000-query pairs
measure cache 47.864, 52.833, 47.1498 ns/query versus direct lookup 40.9474,
42.3448, 43.0468. Median cache cost is about 13% higher on this fixture.
Instrumentation made eliminating the getter calls appear better than normal
execution. The cache is removed; the owned-pawn shortcut remains. This narrow
result does not explain the whole TPS difference by itself. Native evidence:
artifacts/gravship-query-timing-native.json and
gravship-query-timing-dpa-status.json. Live landing behavior remains protected
by the production-linked tests and native scenario.

After removing the cache, main 4598ae16 builds with the companion without
warnings/errors and all 187 tests pass. The deleted test covered retention by
the removed cache; landing liveness, world changes and warm allocation guards
remain. Whole-game TPS acceptance of these new bytes is pending.

Native 4598ae16 after cache removal passes all landing-state transitions.
Three query batches measure 45.2764, 46.9872, 39.3876 ns/query; the equivalent
direct reference measures 39.9136, 40.6002, 40.9526. Even identical direct query
bodies show call-site/JIT/timing variation, so the cache experiment provides no
reliable normal-execution benefit and cannot justify added lookup state. The
cache remains removed on that basis. Evidence: artifacts/direct-gravship-native-state.json
and direct-gravship-native-dpa-status.json.

The external-call Update snapshots (dpa-direct-gravship-update-snapshot.json
and dpa-update-aligned-snapshot.json) include paused frames between requests.
Their 2,000/1,422 entries substantially exceed the actual 122 played frames;
they are exploratory evidence and are not a played-frame comparison. The
companion dpa_playback operation now performs reset, playback and snapshot
inside one native async operation, with stop/cleanup in finally. It requires
an active patched DPA session before advancing the game.

The native dpa_playback preflight correctly refuses an unpatched session.
The Update capture now succeeds with 136 DPA entries for 120 measured playback
frame intervals, covering the played interval plus short setup/teardown frames.
The previous external-call traces had 1,422/2,000 entries. Native snapshot
averages: Root_Play.Update 44.510396 ms; MapUpdate 2.085264; dynamic drawing
0.934327; native culling 0.602105; Total Fog cull filtering 0.012436; fog section
regeneration 0.003849. Common IsVisible 0.841351 includes tick and UI queries
inside frames, not just drawing. These numbers identify costs under profiling;
they are not uninstrumented TPS. Cleanup confirms inactive/unpatched. Evidence:
artifacts/dpa-native-capture-preflight.json, dpa-native-capture-candidate.json
and dpa-native-capture-candidate-clean.json.

The matching original capture passes with 138 entries/122 played intervals,
the same fixture/settings/render environment and inactive/unpatched cleanup.
Common profiled averages: Root_Play.Update 44.505111 ms; MapUpdate 1.952347;
dynamic drawing 0.852004; culling 0.550212; pre-draw 0.119709; pawn post-tick
visuals 0.114950; fog regeneration 0.004734. Dynamic drawing is about 0.082 ms
per profiled entry cheaper than the candidate. The shared native targets match,
but candidate has two extra instrumented boundaries and both captures include
short setup/teardown frames. They do not prove a whole-game TPS difference.
Visibility checks within ticking remain the next larger candidate cost to
compare using matching high-level boundaries, without per-cell probes in just
one binary. Evidence: artifacts/dpa-native-capture-original.json and
dpa-native-capture-original-clean.json.

Restored candidate main 4598ae16 passes Mortal fixture loading in a fresh native
process: 1,082 section regenerations, zero failures and zero thing-grid mutations
while regenerating. No error/exception diagnostics are present in Player.log.
Evidence: artifacts/direct-gravship-mortal-render.json. The exact candidate
remains deployed. All 187 independent tests and main/companion builds pass;
full uninstrumented TPS acceptance of 4598ae16 remains pending.


Matched high-level Tick profiling on main 4598ae16 uses eight targets in each
fresh process: the same four native tick methods, plus each mod's root component,
sight component, visibility update and cell-registration tick. Both snapshots
contain 2,000 ticks from the unchanged 350x350/410-pawn fixture. Inclusive average
costs in ms/tick (candidate / original): root component 0.198279 / 0.195533;
sight 0.132133 / 0.157687; visibility 0.025619 / 0.008946; registration
0.016314 / 0.010124. Do not add nested costs. Total Fog sight is cheaper in this
trace; its visibility and registration paths warrant attention. Probe counts and
runtime component counts differ, so this is diagnostic evidence rather than TPS
acceptance. Evidence: artifacts/dpa-matched-tick-{candidate,original}.json.
Both captures clean DPA before verified termination, and the candidate is restored.

Native map lookup timing confirms the existing weak map cache has a benefit on
this fixture: three 500,000-query batches take 29.2600, 29.2862, 31.8114 ms,
versus 203.0837, 199.8459, 212.3645 ms for the engine's 13-component list search.
This tests only lookup cost, not full-map throughput.

Main 47679867 removes the cell-registration timer: every tick already compares
map, position, size and applicable rotation, and its periodic call would return
on that identical footprint. A coverage/movement/signal visibility check now
starts the twelve-tick fallback interval, avoiding a second check at the prior
deadline. A production-linked regression first fails on that redundant query;
all 190 tests pass after the change. Immediate signals and unsignalled vanilla
fog changes remain covered. Main/companion builds have zero warnings or errors.

The follow-up matched DPA trace does not prove a timing improvement: registration
0.016300 ms/tick, visibility 0.025209, root component 0.202512. Visibility calls
increase from 166,709 to 202,286 in the 2,000-tick snapshot as event/fallback phases
change. Whole-game performance must be assessed separately. Evidence:
artifacts/dpa-cadence-candidate.json. DPA is inactive/unpatched before termination.

The same exact main 47679867 passes two native reloads of Mortal's save:
2,164 section regenerations, no failures and no thing-grid mutations during
regeneration. Evidence: artifacts/cadence-mortal-render.json. The subsequent six-process alternating
uninstrumented comparison completes but fails the floor: original TPS 487.87,
481.81, 485.88; candidate 482.18, 471.15, 482.18. Medians 485.88 / 482.18,
about 0.76% below the original. Median sample mean tick elapsed is 1.436336 /
1.448990 ms; frame p95 69.7068 / 68.9279 ms. All six samples confirm matching
fixture/settings/rendering/packages and DPA inactive/unpatched. Host snapshots
record brief Token Coffee and Simulator helper activity, so this is not claimed
as a completely idle host. The result does not establish an improvement from
the previous cohort or satisfy the TPS floor. Exact candidate bytes are restored;
no feedback ZIP is promoted. Evidence: artifacts/comparison-cadence-stress.json
and cadence-stress-host.jsonl.


The loaded original baseline assembly (MVID d6468c7800ff4cb6a6d292a78fd151f8)
confirms that its visibility updater retains map/fog references and skips
unchanged-position periodic checks. That explains part of the matched trace;
Total Fog must retain its live-information and vanilla-fog safeguards. Native
member evidence: artifacts/dpa-original-visibility-source.json.

Main 044bb362 reuses the current registered map component and the visibility
adapter's own observation flag. Registration exposes its component only while
registered on the parent's current map; a map change falls back to a fresh
lookup until registration catches up. General render/UI queries still resolve
observation through the thing. Both paths evaluate the same policy body; no
visibility results, geometry or additional map/controller cache state are added.
A regression first reproduces the adapter querying its own component; all 192
tests pass with the redundant query removed. The stale-map transition test keeps
old coverage from revealing a thing on its new map. Main/companion builds have
zero warnings and errors.

Matched eight-target Tick profiling reduces UpdateVisibility from 0.025209 to
0.019268 ms/tick (about 24%), despite 205,478 versus 202,286 update calls across
2,000 ticks. Inclusive root component is 0.192802 ms/tick, sight 0.130649 and
registration 0.016191. This is a profiling result, not whole-game acceptance.
Evidence: artifacts/dpa-existing-context-candidate.json. Cleanup confirms
inactive/unpatched DPA before verified termination.

Exact main 044bb362 passes two native Mortal fixture reloads: 2,164 section
regenerations, zero failures and zero thing-grid mutations. Its static-interface
scenario also passes the relevant transitions: a newly observed proxy target
can be selected and shows overlays/tooltips; while remembered outside sight it
stays rendered but loses selection and produces zero overlay, tooltip or
mouseover callbacks; reveal restores those callbacks and selection through the
proxy. The initial observed mouseover count is zero with the inspector open,
so that initial readout is not claimed as tested. Evidence:
artifacts/existing-context-mortal-render.json and
existing-context-native-interface.json. Fixture objects and diagnostic patches
are removed afterward.

The subsequent six-process comparison on 044bb362 fails the TPS floor: original
479.77, 489.74, 493.60; candidate 477.75, 477.55, 489.83. Medians 489.74 /
477.75, about 2.45% lower. Median sample mean tick elapsed is 1.425913 /
1.464503 ms; frame p95 69.0335 / 69.5701 ms. All fixture/settings/packages/
rendering checks match and DPA is inactive/unpatched in all six. The cohort
contains background Simulator, Token Coffee and metadata/WindowServer activity;
it does not prove causation by this source change, and the method-level gain
does not establish a whole-game improvement. Exact candidate is restored and
no ZIP promoted. Evidence: artifacts/comparison-existing-context-stress.json
and existing-context-stress-host.jsonl. The additional wrapper is subsequently
removed so every caller uses one policy method with optional owned inputs.


Final main 4959b962 keeps a single IsVisible policy method with optional current
registration/observation inputs, eliminating the wrapper. All 192 tests and
main/companion builds pass without warnings/errors. Exact bytes pass two native
Mortal reloads (2,164 regenerations, no failures or thing-grid mutations) and
the same static-interface hide/reveal/proxy transitions. Evidence:
artifacts/direct-context-mortal-render.json and direct-context-native-interface.json.

The six-process direct-context comparison is complete and formally fails its
strict median TPS gate: original 483.79, 484.85, 426.02; candidate 482.13, 483.69,
407.80. Medians 483.79 / 482.13 (about 0.34% lower). Median sample mean tick
elapsed 1.449042 / 1.450663 ms; frame p95 70.3697 / 69.4907 ms. All six confirm
matching fixture/settings/packages/rendering and DPA inactive/unpatched.

The final original/candidate pair overlaps a major unrelated Xcode build and
Simulator startup: ibtoold, SwiftBuild, many concurrent swift-frontend processes,
validation helpers and Simulator apps. Both mods' TPS drops sharply in that
pair. This cohort is recorded as host-contaminated and cannot establish reliable
TPS acceptance or improvement. Earlier samples alone do not prove a performance
advantage either. Preserve the raw failed report; do not promote a ZIP or rewrite
the gate to pass. Evidence: artifacts/comparison-direct-context-stress.json and
direct-context-stress-host.jsonl. Exact candidate remains deployed after verified
termination, with DPA still enabled in the isolated profile but unpatched.


## Combat Extended targeting

Installed Steam Combat Extended 16.7.3.0, MVID
2154499a15df47859832e4d0c305b722, implements its own protected virtual
CanHitCellFromCellIgnoringRange(Vector3, IntVec3, Thing). Installed-binary
Decompiler evidence confirms the normal CE targeting path uses this method,
so the vanilla IntVec3 hook did not establish coverage. An optional adapter
now adds the shared fog restriction to successful native CE results, including
declared overrides supplied by other mods. Failed results and no-LOS verbs
retain CE's behavior. No CE assembly dependency or extra ticking state is added.

Eight production-linked regressions bring the suite to 200 passing tests.
They cover the exact overload and declared-only descendants, inactive/missing
CE, failed native hits, immediate coverage changes, no-LOS verbs, the enemy
setting and the horizontal conversion of CE's elevated shot origin.
Main and companion builds have zero warnings/errors. The canonical ce-setup
command creates a separate Steam profile with all DLCs, CE and Total Fog; it
preserves the ordinary performance profile and existing CE settings/loadout.

Main SHA-256 26ecdd8a4850d2d08ed6935cfffa030f9c1cda7e4f7c881233a466a0d74ea258
is byte-identical to the deployed main. Companion
2ab818480a9e6548dbf23846064d0e885f9c8c0f728ce7043572e127186083a6 was also
byte-identical during the native checks. On the paused 250x250 CE fixture,
the actual CE Verb_ShootCE method gives native open=true, hidden player=false,
revealed player=true, native blocked=false, fog blocked=false, native
enemy=true, enemy-fog disabled=true and enabled=false. Two native ticks expire
CE's own cover-height cache after adding/removing the wall. The first paused
wall test failed its expectation because CE cached the earlier empty cell
until the next tick; the scenario was corrected to respect native invalidation.
No gameplay change was made to that CE cache. The tool restores the actual
postfix, settings and sight, and removes its temporary shooter/wall/coverage.
Evidence: artifacts/ce-native-hit-checks.json,
ce-native-hit-checks-first.json and ce-installed-lighting-tracker.json.

Two native CE save reloads pass 1,926 section regenerations with no errors
and no thing-grid mutations. The bounded Player.log check finds no exceptions
or integration warnings. Termination is verified after testing. Evidence:
artifacts/ce-native-render-reloads.json. Turret/mortar targeting, actual shots
and combined CE/other overhaul loadouts remain open. No performance floor
claim or new feedback ZIP follows from this compatibility result.


## Clear-row casting and ReadOnlySpan, 2026-10-05

An explicitly requested Astra xhigh read-only review identified clear horizontal
rows as the strongest bounded next casting optimization. The retained profile
puts ComputeMask at 0.089778 ms/tick. Saving half that time would illustrate
about 2.2% whole-run throughput at 483 TPS if it converted directly to elapsed
savings; this is a cost budget, not a forecast. The 0.192802 ms/tick CompTick
profile is not a complete mod-cost ceiling: CompTickRare, map and UI work also
execute outside that method. Direct cell indexing for visibility listeners is
a separate hypothesis evaluated below. No density cache or adaptive mode is added.

ReadOnlySpan<bool> is available from the installed engine's own mscorlib,
MVID 79a1904c928a4c2d9eba5ede6e6aa92e, and was inspected with DecompilerServer
then exercised in the native game. No System.Memory runtime dependency is added.
The standalone Core test library targets netstandard2.1 to compile span use;
the shipped main still targets net472 and source-links Core. The existing lazy
player-count sentinel is annotated nullable, with no state or behavior change.

A span-field-only prototype preserves exact masks/queries and reports zero allocations,
but is only modestly faster in paired native mask batches and slower on open
.NET 10 callback batches. Its six-process original comparison formally fails:
481.68 original versus 474.91 candidate median TPS, about 1.41% lower. The host
monitor overlaps Simulator paired-sync activity up to 76.3% CPU and metadata
activity up to 61.9%; it begins after the first sample. Preserve this failed
report as limited evidence, not acceptance or proof of causation. Evidence:
artifacts/comparison-span-caster-stress.json, span-caster-stress-host.jsonl,
span-native-kernel-comparison.json and span-native-kernel-comparison-long.json.
Row-stepping and index-reuse trials are also rejected; neither establishes a
consistent gain. Their raw reports remain in ignored artifacts.

The retained optimization uses a span only to search the current contiguous
blocker row. A successful proof requires all candidate cells in bounds and
inside the radius, with no opaque cell. It writes the exact symmetric floor
interval into reusable mask words, preserving existing bits and clipped tails.
Rows shorter than 16 cells keep the direct cell loop. No blocker mutation,
extra persistent state or changed visibility cadence is introduced.

The initial shared-loop prototypes regress dense masks and point-query timing.
A measured specialization keeps callback/point casting separate from mask
writing, removing target/delegate dispatch from the latter. Recursion, slope
rounding and transition order remain equal. Ten new tests bring the suite to
210 passing tests; they cover clipping, 63/64/65 and wider word boundaries,
padding, existing bits, asymmetric shadows, edges, huge radii and peeking unions.
Main/companion builds pass with zero warnings/errors.

The final native same-process comparison alternates five warmed reference/current
batches, using Total Fog e1b07e9 as the source reference, not the original NWN
algorithm. All 512 random masks, 4,096 point queries and the direct masks at every
workload origin match. Both versions report zero allocated bytes in every timed
batch, but the later positive-control check below invalidates that native metric.
The actual-map case clones the current native blocker grid and uses all 53
spawned colonist positions without changing the fixture. Mask time changes:

| Workload | Time change |
| --- | ---: |
| Open | -29.35% |
| 3% blockers | -3.15% |
| 15% blockers | -2.34% |
| 35% blockers | -5.83% |
| Late row blockers | -28.08% |
| Narrow corridor | -2.43% |
| Map edges | -14.27% |
| Actual native colonist positions | -15.92% |

Native point queries change -4.09% on the actual-position workload; the 15%
synthetic case is +0.71% on very short batches. This does not establish a universal
point-query speedup. Evidence: artifacts/open-row-native-kernel-specialized.json,
main SHA-256 96fa4c08ada3968d5a53d0e6b9fa9da0aefa086d450660974a8f1ed5353f349d,
MVID e37d0bde12f4474cbb0f4d820695b85b. The disposable native comparison sources
are excluded again from the companion build. The canonical pure benchmark now
also checks and times direct masks against e1b07e9; its .NET 10 timing is separate
from the native result. Whole-game evidence follows below.

The subsequent six-process comparison on the exact 96fa4c08 candidate formally
fails the TPS floor. Original samples: 481.09, 477.96, 475.88; candidate: 472.47,
477.19, 481.46. Medians are 477.96 / 477.19 TPS, about 0.16% lower. Median sample
mean tick elapsed is 1.466439 / 1.465226 ms; frame p95 is 70.3086 / 70.203 ms.
All six verify matching save/settings/packages/rendering and inactive/unpatched
DPA. The host monitor overlaps metadata at up to 82.7% CPU, Simulator paired-sync
at 71.6% and media analysis at 52.3%. The small difference is below the observed
sample spread and does not establish a reliable throughput advantage or its
cause. Preserve the failed gate; no ZIP is promoted. Evidence:
artifacts/comparison-open-row-stress.json and open-row-stress-host.jsonl.

The same candidate passes two native reloads of MortalFleshbeastAttack, with
2,164 section regenerations, zero failures and zero thing-grid mutations. The
bounded Player.log search finds 16 native loader fallback messages and no
exceptions or other matching load errors. Built/deployed main and companion
hashes match; the companion is back to its normal 450-tool surface with the
temporary comparison excluded. DPA remains inactive/unpatched and game
termination is verified. Evidence: artifacts/open-row-mortal-render-reloads.json
and open-row-native-final-state.json. Original-mod performance acceptance,
broader scaling and Windows native checks remain open.

## Native allocation-counter limit, 2026-10-05

The listener-index benchmark detects GC.GetAllocatedBytesForCurrentThread()
returning zero for a real 122,500-reference array and dictionary construction.
A separate retained 4,096-byte array also produces a zero delta. Installed
mscorlib DecompilerServer evidence identifies the method as an InternalCall,
not a managed allocation counter. This native runtime's readings fail the
positive control and cannot prove zero allocation. The earlier native zero
readings, including clear-row/span batches, must be interpreted as unsupported
measurement rather than allocation acceptance. Native timing/mask comparisons
remain valid; the independent .NET allocation tests have not been invalidated.

The new listener benchmark reports calibration and raw counter readings
separately. Full-collection retained-heap estimates on the paused map measure
1,765,376 bytes for a dictionary clone versus 983,040 bytes for the direct array.
They share the actual listener lists and compare index storage, not complete
process memory. Evidence: artifacts/listener-index-native-dictionary-calibrated.json
and engine-allocation-counter-source.json. A reliable native warmed-kernel
allocation check remains open.

## Direct visibility-listener indexing, 2026-10-05

The native stress map contains 56,885 occupied listener cells and 57,100 listener
registrations across 122,500 cells. Before editing production, a paused native
comparison of actual registrations measures 78.8% lower full-grid lookup time
and 89.8% lower occupied-cell lookup time for direct indexing. Retained-heap
index estimates are 983,040 bytes for the array versus 1,765,376 bytes for an
equivalent dictionary clone, sharing the same listener lists. This is index
storage and kernel timing, not whole-game TPS or complete process memory.

MapVisibility now replaces its authoritative hiddenAt dictionary with one
cell-indexed reference array. Lists remain lazy and the final removal releases
the slot. Bounds checks, duplicate suppression, insertion order and listener
notification timing are unchanged. Sparse sight modifiers, trees and mining
designations retain their existing dictionaries. There is no secondary index,
new invalidation scheme or gameplay setting.

A detached native component verifies the production registration methods before
and after replacement: duplicate adds leave two ordered listeners, all four
out-of-bounds coordinates are rejected, absent/repeated removal preserves shared
listeners, and the final removal empties the slot. The live map remains unchanged.
The actual rare-ticking 3x2 building scenario also passes after 250 native ticks:
all six cells register and block sight, rotation clears old exclusive cells,
and despawn clears registration and blockers. All 210 tests and main/companion
builds pass with zero warnings/errors.

The array-side native lookup comparison preserves the exact initial listener
counts. It measures 78.7% lower full-grid lookup time and 87.7% lower occupied
lookup time. Its first attempt failed because an unbraced conditional in the
new diagnostic reader attached the fallback exception to an empty-cell test.
The diagnostic was corrected, the game restarted, and the comparison and
blocker scenario then pass. The failed report is retained; it was not a
production registration failure. Evidence: artifacts/listener-registration-dictionary.json,
listener-registration-array.json, listener-index-native-array-corrected.json,
listener-array-blocker-geometry-corrected.json and listener-index-native-array.json.

Current main SHA-256 be7f75488b807c0d0320bab8694259dced0020d1353dd0955a41cf6a70f66710,
MVID e99fd967cff5404cb23e4b7e526cb5b4. The full original-mod result follows below.

## Original-comparison diagnostic isolation, 2026-10-05

The first listener-array comparison stops before producing timing samples. The
new typed listener diagnostic shares PerformanceScenarios' compiler-generated
delegate cache with the reflection-only timing driver. Native resolution of a
CompVisibility field fails against the original mod's different namespace. The
bridge's generic SDK-mismatch hint does not identify this cause; no SDK or
RimBridgeServer change is needed. The diagnostic now lives in its own concrete
ListenerScenarios class, isolating its generated cache. The subsequent original
sample successfully runs against the exact original DLL.

The runner previously awaited game termination only after a successful sample,
allowing a failed sample to race the caller's candidate restoration. It now
awaits stop and verifies stopped status for any owned startup, including failures.
Cleanup errors remain visible without replacing the primary failure. A controlled
native InvalidSpeed request fails with the expected unknown-speed result and
leaves the game verified stopped. The main DLL remains byte-identical to be7f7548;
the isolated companion is e8b6ccac5436440d10d3a33e72b7be20619115f2debe6c78d030a12ec4b563f0.
Build/tests pass with zero warnings/errors. Evidence:
artifacts/runtime-listener-array-stress-original-1.json,
logs/runtime-compare-listener-array-first.log,
runtime-listener-cleanup-probe.json and logs/runtime-listener-cleanup-probe.log.

The subsequent complete six-process comparison still formally fails the floor.
Original TPS samples are 483.63, 476.73, 475.28; candidate samples are 475.12,
474.65, 481.91. Medians are 476.7342 / 475.1155 TPS, 0.34% lower for the candidate.
Median sample mean tick elapsed is 1.467880 / 1.469986 ms; frame p95 is
69.0812 / 69.4458 ms. Every sample verifies the unchanged fixture, settings,
packages, rendering environment, focus and exact main DLL. DPA is installed
and active in both variants but not profiling or patched. Background monitoring
overlaps Token Coffee up to 86.0% CPU, Simulator paired-sync at 80.7% and media
analysis at 75.4%. Sample ranges overlap; this result does not establish a
reliable advantage or the cause of the shortfall. Retain the failed gate.
Evidence: artifacts/comparison-listener-array-fixed-stress.json,
listener-array-fixed-stress-host.jsonl and
logs/runtime-compare-listener-array-fixed-stress.log.

The exact restored candidate then passes two native MortalFleshbeastAttack
reloads with 2,164 section regenerations, no errors and no thing-grid mutations.
Built/deployed main and companion hashes match the pair above. The bounded
152-line Player.log check finds 16 native loader fallback lines and no other
error-pattern matches. DPA is not profiling or patched, and termination is
verified. No ZIP is promoted. Evidence:
artifacts/listener-array-mortal-render-reloads.json,
listener-array-native-final-state.json and listener-array-mortal-player.log.

## Astra xhigh follow-up cost assessment, 2026-10-05

The requested read-only review finds no evidence for another multi-percent
whole-game gain in the retained profile, which predates the current optimizations.
The listener kernel's relative gain
has a small absolute budget: approximately 9.7-10.4 ns saved per lookup. Using
the earlier VisibilityChanged count of 55,594 calls over 2,000 ticks estimates
only about 0.27 microseconds saved per tick by the read lookup alone, around
0.013% of a 2.1 ms elapsed tick. Registration mutations and memory effects are
additional and unmeasured. This estimate explains why a large lookup percentage
does not imply a visible TPS change; it is not a new whole-run measurement.

The next useful profile includes CompFog.CompTickRare and the blocker-change
chain through MapVisibility.SetBlocker and MapComponentTick, as well as the
existing tick/render roots. SetBlocker scans all fowWatchers, including buildings
and inactive sources, before dirty casts. The earlier custom MapComponentTick
trace is only about 0.0015 ms/tick; source structure alone cannot justify adding
a spatial index. Measure the complete current cost before changing it.

Actual occupancy also establishes at least 56,670 singleton listener cells:
57,100 registrations occupy 56,885 cells. A cell slot with an inline first
listener and lazy overflow list could remove most small list objects and list
creation during empty-to-occupied moves. At this assessment it is an unimplemented
representation hypothesis. It is subsequently implemented and measured in the
Inline singleton listener storage section below. Vertical clear-run arithmetic
remains a lower-priority hypothesis with no measured advantage and without the
horizontal contiguous-word write.

A further explicitly requested Astra xhigh read-only review covers 1b4b510,
after inline storage and its six-process TPS comparison. It finds no supported
new multi-percent whole-game gain. At 485 TPS, 5% higher throughput would require
about 0.098 ms less elapsed time per tick, and 10% about 0.187 ms. These are
arithmetic budgets, not measured removable work. The older instrumented DPA rows
cannot provide exclusive accounting: ComputeMask plus ApplyDifference averages
exceed their UpdateFoV parent. Do not sum or subtract nested averages into a
claimed mod total.

The ranked outstanding measurements are:

1. Combat point-query work through Detours/Verb.cs and FieldOfView.CanSee.
   Count eligible fallback calls, range-calculation and geometry time, repeated
   exact (origin, target, radius) keys, reuse distance and intervening blocker
   changes on a combat-heavy fixture. Check planned firing positions, corner
   peeking, opaque targets, manned turrets, CE, both enemy-fog modes, and paused
   or same-tick blocker/range changes. Current CanSee scans one quadrant to target
   depth. DecompilerServer inspection verifies the exact upstream baseline
   9d011de0 DLL used in the comparison: upstream caches final hit results per tick;
   misses allocate a one-element bool array and scan an octant, potentially to
   full radius. Real upstream cache behavior must remain part of the baseline.
   A few-entry map-owned cache of the pure geometry result is a conditional
   candidate, not an implemented strategy. All other checks and fresh range
   calculation remain outside it. Invalidate on actual blocker boolean changes
   before SetBlocker's initialization early return. The public mutable blocker
   array requires foreign-writer ownership checks before this contract can be
   accepted. Stateless pruning of recursive intervals unable to include the
   target is an alternative; preserve rounded inclusion for opaque targets.
   Vanilla line of sight is not an equivalent geometry replacement.
2. Deliberate blocker-change bursts through SetBlocker and the dirty-source drain.
   Per changed cell, SetBlocker scans all registered watchers, including ordinary
   buildings and inactive sources. Measure changed cells, watcher distance checks,
   unique dirty sources, actual casts and refreshes occurring before drain on a
   large built colony with overlapping observers, moving doors, multi-cell
   rotation/destruction and trees crossing their blocking threshold. Include map
   edges, despawn and save reload. Duplicate-cast elimination is a smaller
   conditional change than a spatial source index. No measured cost currently
   justifies that index's movement/radius/faction/settings/lifecycle maintenance.
3. Hearing and hidden-sound population scaling through EmitHearingCues and
   SoundAudibility. Hearing scans all spawned pawns per eligible player human
   every 100 ticks, giving H*P/100 average pair checks and possible aligned bursts.
   Record ShowHearingCues explicitly: the retained comparison settings omit it.
   DoAudioCheck=False establishes only the sound-audibility option, not hearing
   cue state. Measure listener-pawn checks, total time, cue creation and sustained
   sound getter frequency with large populations, aligned deadlines and high
   hearing ranges. An existing engine-owned local search or narrower player-pawn
   collection is only a candidate until it beats the pawn scan on both dense-pawn
   and sparse-pawn/dense-item maps. Preserve deadlines, per-listener cues and
   spawn/RNG behavior.

All proposed gains remain unverified. A general ReadOnlySpan conversion does
not follow from this review; the retained clear-row span search is useful because
it enables contiguous bulk mask writes. Further kernel changes, pooling,
parallel casting and extra caches have no supported large whole-game budget
here. No runtime/build/benchmark is performed by this review. Coverage remains
open across other settings, workloads and platforms.

Andreas subsequently accepts slightly deferred work and briefly stale information
to smooth other-mod load spikes, with a firm bound against indefinite deferral.
The Astra xhigh follow-up therefore treats bounded scheduling as a credible tail
latency experiment. Mortal's Total Fog plus Zombieland Halloween session becomes
the next compatibility priority; combat tracing remains an outstanding speed
measurement rather than the only next workload.

The first scheduling experiment is limited to the existing blocker-driven
MapVisibility dirty-source drain. Use one removable, reusable pending entry per
source in FIFO order, preserving its first enqueue tick across repeated requests.
A provisional quota of eight sources per map tick and three-tick maximum age
must be measured, not treated as final tuning. Overdue sources run even when the
quota is exhausted. Keep movement, explicit public refresh, initialization and
settings changes synchronous initially. A completed actual synchronous refresh
satisfies queued work. Unlink pending source removal/transfer promptly, and
remove a pending entry before refreshing so callback-driven new invalidation is
not accidentally erased. Publish each complete mask difference in one uninterrupted call through
the existing callbacks; do not suspend halfway through it.

A soft quota and hard freshness deadline cannot also guarantee a fixed CPU cap
under permanent overload. Deadline overruns must be counted. Three game ticks
are 50 ms of normal simulated time, not a guaranteed 50 ms wall-time bound in an
overloaded engine. Pause needs a safe drain without a timer. A pause-only
MapComponentUpdate drain is only a single-player experimental option: Steam 1.6
updates map components after drawing, correcting the following frame. Authoritative
coverage affects discovery, notifications and targeting, so local frame/wall-time
scheduling must not be declared Multiplayer deterministic. That target remains
unverified. Queue order and deadlines should be deterministic, map-owned and
independent of camera/selection. Pending state is transient; load reconstructs
coverage and starts empty.

Retaining an old complete footprint has concrete effects: door closure can leave
sight briefly stale, and door opening can reveal late. New objects can also gain
persistent observation, notifications can be released and targeting can use that
coverage. The experiment must check these effects explicitly rather than treating
staleness as purely cosmetic. Prompt loss withdrawal or urgent recasts are possible
policies; clearing old coverage wholesale adds callbacks and can darken the colony.
No production policy is selected or deployed by this assessment.

Use a paired baseline/candidate blocker-source burst workload with identical,
measured development-only main-thread load spikes. Zombieland already owns
calibrated slow-host/spike instrumentation and population fixtures under
scripts/README.md and its companion; verify reuse in the combined loadout before
adding another simulator. Existing Total Fog individual reports contain tick and
frame p95/p99/max. Add only scheduler counts, pending/high-water count, game-tick
and observed wall-clock ages, coalesced and synchronously satisfied requests,
deadline-forced work and per-tick maximum executions. Check final masks/discovery
and an empty or correctly bounded remaining queue, including sustained arrivals
over quota, pause/resume, speed changes, multiple maps, movement, removal/faction
changes and save/load. A lower tick p99 alone does not prove lower frame p99:
Steam 1.6 runs whole ticks until its approximately 45.45 ms frame-loop budget is
exceeded, so several quota slices can still occur in one frame. Report average
TPS, total work and overruns separately from tail latency. No scheduler has yet
been implemented or benchmarked.

## Broader current-build native profile, 2026-10-05

The suggested bounded profile is then run on the same be7f7548 candidate and
unchanged TotalFogStress350 fixture. All 13 requested methods resolve and patch.
The bridge plays 3,752 ticks over 8,101 ms at Ultrafast, with focus retained and
no probe failures. DPA preserves the latest 2,000 entries. These are instrumented
inclusive costs, not uninstrumented performance-floor acceptance; nested totals
must not be added to their parent totals.

| Method | Average ms per DPA entry | Recorded calls |
| --- | ---: | ---: |
| CompFog.CompTick | 0.159213 | 978,357 |
| CompSightSource.UpdateFoV | 0.099110 | 33,372 |
| FieldOfView.ComputeMask | 0.071114 | 3,629 |
| VisibilityMask.ApplyDifference | 0.036207 | 3,665 |
| CompVisibility.UpdateVisibility | 0.020864 | 209,100 |
| MapVisibility.VisibilityChanged | 0.020169 | 68,304 |
| ThingVisibility.IsVisible | 0.019095 | 203,852 |
| FogMapUtility.GetVisibility | 0.001927 | 58,789 |
| MapVisibility.MapComponentTick | 0.000921 | 2,000 |
| SectionLayerFog.DrawLayer | 0.000351 | 1,638 |
| CompFog.CompTickRare | 0.000130 | 3,712 |

SetBlocker has no row in this snapshot, leaving blocker-change cost unmeasured.
Regenerate reports 0.000169 ms per entry with zero recorded calls despite 15
nonzero timing entries; do not interpret that inconsistent count as proof that
regeneration did not run. Rare ticks and map ticks are small on this steady
fixture. The dominant measured fog work remains normal sight/mask/difference
processing. This does not establish a large missing cost or justify another
source index, and it does not cover a deliberate blocker-change workload.

DPA cleanup is verified not profiling/not patched and native termination is
verified. No sources, settings, loadout or save files are changed by the profile.
Evidence: artifacts/astra-broader-native-profile.json. The native per-thread
allocation counter is still unsupported; this profile does not validate it.

## Inline singleton listener storage, 2026-10-05

The native movement probe counts actual list-storage-producing registration
transitions, without using the unsupported per-thread allocation counter. The
list-array baseline creates 15,233 lists and releases 15,211 over 3,834 advanced
ticks. The difference exactly matches occupied cells increasing from 56,885 to
56,907. There are 24,139 registrations and matching deregistrations. This is
recurring movement churn, not only startup allocation.

Before replacing the map representation, detached native copies preserve actual
list capacities and all listener identities/order. On 122,500 cells, 56,885 are
occupied, 56,677 are singletons, and 57,100 listeners occupy 227,540 list-capacity
references. Three alternating full-collection retained-heap estimates are
5,079,040 / 5,550,080 / 5,554,176 bytes for list storage and 1,978,368 for inline
storage. These are local storage estimates, not total process memory or allocated
bytes. The first prototype allows older comparison cohorts to be collected
between samples, yielding a lower third reference delta. It is preserved; the
controlled version keeps all cohorts rooted through the measurement. Estimates
still vary, so no exact byte-size claim follows from GC.GetTotalMemory.

ListenerCell<T> now holds the first listener directly and allocates a list only
for additional listeners. It replaces the map's authoritative array element,
with no duplicate index, invalidation state or changed notification cadence.
The first removal promotes overflow in insertion order, and drained storage
releases all references. This trades one extra reference per map cell for fewer
lists on mostly-singleton maps; it does not promise lower memory on every sparse
or heavily shared map. Nine tests bring the suite to 219. They cover ordered
uniqueness, first/middle/last removal, repeated removal and reuse, equality,
invalid inputs, 10,000 mixed operations against a reference list, array element
mutation, reference release and zero warmed singleton allocation on .NET 10.
The missing-type red build is retained before implementation. Main and companion
builds pass with zero warnings/errors.

Native production registration passes duplicate suppression, all four coordinate
bounds, absence/repeated removal, shared-cell preservation, three-listener
promotion, overflow removal and complete clearing on a detached component.
The actual 3x2 rare-ticking building passes all six registrations/blockers,
rotation cleanup after 250 ticks and despawn cleanup.

Inline movement creates 8,773 overflow lists and releases 8,797 over 3,822
advanced ticks, with 24,521 registrations and matching deregistrations. Stored
lists fall from 208 to 184, exactly matching the creation/release balance. Total
registrations remain 57,100. List creation per advanced tick is about 42.23%
lower than the baseline. These are separate similar playback intervals, not an
identical recorded operation replay, and do not establish a TPS advantage.

The same candidate passes two MortalFleshbeastAttack reloads with 2,164 section
regenerations, no failures and no thing-grid mutations. The bounded 195-line log
has 16 native loader fallback lines and no other error-pattern matches. DPA is
not profiling/not patched and native termination is verified. Built/deployed
main and companion hashes match: main
690ba09ecfb6df4833a5ee9f9b32a2f67ea387255f4d677944d30e476f7afff2,
MVID 70b20147841841e3a5d886eb3d3d0464; companion
34ca104a44a1f0c05a6ec49af46716d508c7943346d351c36215fd6334196871.
The full original-mod TPS comparison is pending; no ZIP is promoted.

Evidence: artifacts/listener-churn-array-baseline.json,
listener-storage-native-prototype.json, listener-storage-native-retained.json,
listener-inline-native-registration.json, listener-inline-native-churn.json,
listener-inline-native-blocker.json, listener-inline-native-render-reloads.json,
listener-inline-native-final-state.json and logs/listener-cell-red.log.

The first full comparison exits during original startup before timing samples.
Player.log identifies an unresolved ListenerScenarios.RegisterBegin signature
expecting the candidate MapVisibility type. Native Mono then crashes in
RuntimeMethodInfo.get_IsGenericMethod / get_ContainsGenericParameters while
RimBridgeExtensionDiscovery.GetAnnotatedMethods examines public methods before
checking their Tool attribute. These development-only Harmony hooks were
unnecessarily public. Making all four private excludes them from discovery;
the subsequent original sample and first original/candidate pair succeed. No
RimBridgeServer change is made. The tested main DLL remains byte-identical;
only the companion changes. The crash log and incomplete report are preserved
in artifacts/listener-inline-original-startup.log,
runtime-listener-inline-stress-original-1.json and
logs/runtime-compare-listener-inline-first.log. The first host monitor is
explicitly interrupted after the comparison is confirmed terminal.

The repaired six-process comparison completes and passes its strict TPS floor
on this fixture. Original samples are 480.56, 481.98, 483.53 TPS; candidate
samples are 484.91, 485.01, 488.08. Medians are 481.9802 / 485.0145 TPS, about
0.63% higher for the candidate. Median sample mean tick elapsed is
1.450844 / 1.440267 ms. Median sample frame p95 is 68.5134 / 70.0057 ms, about
2.18% higher for the candidate; no frame-time improvement is claimed. All six
verify matching unchanged save/settings/packages/rendering, focus, exact main
DLL bytes and inactive/unpatched DPA instrumentation.

The host monitor, restricted to actual sample operation timestamps, observes
Token Coffee up to 84.8% CPU, system proactive-event processing at 42.0%, mlhostd
at 40.2% and metadata storage at 20.1%. No process is stopped or causal claim
made. This is one successful stress-fixture TPS gate, not universal throughput,
all-settings scaling or cross-platform acceptance. Earlier failed/incomplete
comparisons remain preserved. Evidence:
artifacts/comparison-listener-inline-fixed-stress.json,
listener-inline-fixed-stress-host.jsonl and
logs/runtime-compare-listener-inline-fixed-stress.log.

A final native one-second playback proves the private Harmony hooks still work:
3,894 registrations and matching deregistrations, 1,381 created overflow lists
and 1,403 releases, with stored lists declining from 208 to 186. The balance
matches exactly and total registrations remain 57,100. The post-hook bounded
110-line log has no error-pattern matches beyond native loader fallback lines.
Final built/deployed main is still the tested 690ba09e candidate; final companion
is c1db0b4b9cf2decbf3d2e838e4e25ed1e54c47fd69ce59d8b62d654c83e39054.
Termination is verified. Evidence: artifacts/listener-inline-private-hooks.json,
listener-inline-private-hooks-player.log and listener-inline-native-final-state.json.

## Optional silent arrivals (2026-10-06)

The public 1.6 incident pipeline supplies `IncidentParms.silent`. Total Fog now
sets it only during selected enemy raid and manhunter execution and restores
its original value afterward, including failure and exceptions. Native raids
already respect it for arrival letters and short slowdown. The native aggressive
animal worker's one unconditional arrival slowdown call is redirected through a
scoped helper for ManhunterPack; other incidents and ordinary slowdown remain
native. No incidents, strategies, schedules, difficulty or fog tick work are added.
The setting defaults off and is independent of deferred warnings and combat music.

All 263 independent tests pass, including 13 new policy cases. The first actual
native matrix on Total Fog `600083e5e3101bee...` and Zombieland
`039a4fe1d5e009075...` passes four paused arrival cases in the unchanged paired
base: both raid variants spawn the same six pawn kinds and increment the enemy
raid statistic once; both manhunter variants spawn the same five pandas in
permanent manhunter state. Disabled mode produces one letter and a slowdown
deadline at tick + 240. Enabled mode produces neither and queues no warning.
The ordinary short slowdown still sets tick + 240 in every row. Native false
return and thrown worker cases both restore the input flag, and the exception
remains observable. Player.log has no recognized runtime diagnostics. This
accepts arrival handling, not scheduling, every modded worker, or natural combat
playback. `feedback-verify` repeats and preserves this matrix with the exact
final gameplay pair before promoting package gates.

The refreshed `silent-arrivals-player-1000` comparison completes six fresh
alternating original/candidate processes with the unchanged 1,000-zombie save,
the same Zombieland `039a4fe1...` binary, camera/settings/hardware, 300 native
warmup ticks and no profiling instrumentation or private speed boost. Silent
arrivals are explicitly false in the effective settings for both variants.
The original median is 288.5058 TPS and candidate median 312.8943 TPS (8.45%
higher); all three candidate samples exceed all three original samples. Mean
tick-cost medians are 2.444797 / 2.246706 ms. Frame p95 medians are 70.0378 /
69.9574 ms, effectively unchanged. All measured ticks retain the unrestricted
native fourth-speed multiplier. This closes this matched fixture's TPS floor,
not broader maps, Windows, Mortal's complete mod list or long-session behavior.
Evidence: artifacts/comparison-silent-arrivals-player-1000.json and the six
runtime-silent-arrivals-player-1000 reports/native logs.

The first refreshed rendering verification is retained as failed in
artifacts/feedback-verification-silent-arrivals-player-1000-r3872fcn. Its core
geometry is partial, with one of two bounded cells visible, four vertices/two
triangles, zero hidden triangle centers and valid UVs. The previous verifier
required more than two triangles, which is not a clipping invariant: the native
core rotates and pulses in real time, and clipping can leave a four-vertex
polygon. The companion now records actual world-space triangle area and original
quad area; acceptance requires a positive drawn area strictly below the original
area, retaining all hidden-pixel, centroid, UV and interaction checks. Only
diagnostic measurement and verification change; gameplay and shaders are unchanged.

The corrected final verification passes on the same gameplay pair. It preserves
four native arrival controls and failure/exception lifetime checks, twelve
Symbiant screenshot/visibility cases, 54 manual-target checks, and all 40 rows in
the 400/4,000-cell dense/sparse render-cost matrices. The partial core's actual
world-space area is 0.456270784 versus original 0.8719185, with zero hidden
triangle centers and valid UVs; hidden-cell pixel differences remain zero.
Recognized native logs are clean, the base save is unchanged, and termination
is verified. The retained result directory is
artifacts/feedback-verification-silent-arrivals-player-1000-vh_c7p49; the initial
failed triangle-count check is retained separately. Exact native/performance
package gates promote only after all of these checks pass. The canonical
feedback-package command then passes, verifying both separate complete ZIPs
contain the tested gameplay DLLs and supported-version metadata only for 1.6.

## Zombieland danger-area warning privacy (2026-10-06)

The real `ZombieAreaManager.DrawDangerous` renderer displayed its warning for a
currently hidden Normal zombie, including after reveal and subsequent sight
loss. The five-row fixture stages the existing warning cache, pauses its cache
updater, observes native drawing over twelve paused frames per row, and restores
cache, updater, sight, temporary areas and settings before reloading the unchanged
base. It neither advances simulation nor changes area-risk classification.
Two initial fixture failures are retained separately: direct access to a private
area label and removal method was invalid against the native engine. The unused
label writes were removed; temporary area removal uses the existing reflective
test boundary. Neither failure is evidence of a gameplay defect.

The corrected red run uses Total Fog `600083e5...` / Zombieland `039a4fe1...`.
Both hidden-only rows incorrectly show a warning. The source places area
selection, zombie icon collection, hover highlighting and camera-jump targets
downstream of that unfiltered cache entry. Zombieland now skips unseen zombie
entries before choosing the presentation area. It retains the simulation cache
and colonist warning path.

The first green run uses Total Fog `600083e5...` / Zombieland `1d6a07d1...` and passes
all five native rows: hidden-only, visible-only, hidden-after-reveal,
colonist-and-hidden in one area, and a hidden zombie inserted before a colonist
in another area. Each row observes 26 native drawing calls at unchanged tick 18;
all staged zombie cache entries survive. The unchanged base is restored paused
and Player.log has no recognized diagnostics. This proves controlled warning
presentation for one Normal zombie, not natural asynchronous area classification,
other zombie types or actual hover/click pixels. This pair subsequently passes
its six-process ordinary fourth-speed gate on the matched 1,000-zombie fixture:
286.6062 original versus 319.5516 candidate median TPS, 11.49% higher. Frame p95
is 70.2622 / 71.1080 ms, 1.20% higher. Its twelve native pixel cases, 54 manual
targets and forty render-cost rows also pass. These results remain proof of this
preceding pair, not the later location-policy correction below.

The optional extended probe creates a native two-cell hostless Symbiant and
stages its authoritative selection core one cell east of the warning's root.
At unchanged tick 18 the first fix incorrectly shows a warning when only the
core is visible and suppresses one when only the root is visible. This is the
difference between inspection visibility and the grid location used by danger
classification, camera jumps and position highlights.

The final Zombieland filter queries current sight at `pawn.Map, pawn.Position`,
retaining the same area cache and colonist path. The seven-row green run uses
Total Fog `600083e5...` / Zombieland `5f6380a8...`: the original five controls and
both root/core disagreements pass with 26 native drawing calls each, unchanged
tick 18 and retained cache entries. Root/core cells are 19,11 / 20,11. The base
is restored paused and native logs are clean. This is controlled presentation
acceptance, not natural cache classification or actual hover/click pixels.
The final pair's six-process comparison passes with 300 native warmup ticks,
ordinary fourth speed, no debug boost or profiling, matching fixture/loadout,
camera/settings and exact Zombieland bytes. Median TPS is 295.1910 original /
319.5605 candidate, 8.26% higher. Mean tick-cost medians are 2.389189 /
2.203570 ms; frame p95 is 70.2655 / 70.7764 ms, 0.73% higher. All six native
logs are clean. This establishes the matched fixture's TPS floor, not general
FPS, all maps/mod lists, Windows or long sessions.

The refreshed native gate repeats all seven warning controls, four native
arrival cases and failure/exception lifetime checks, twelve Symbiant pixel
cases, 54 manual targets and forty 400/4,000-cell dense/sparse render-cost
rows on the exact final gameplay pair. Recognized logs are clean, the base save
is unchanged and termination is verified. Both `feedback-verify` and
`feedback-package` now require the seven warning rows, including retained cache
entries and unchanged simulation tick. The canonical `feedback-package` command
then passes without rebuilding gameplay. The separate complete ZIPs contain the
tested DLLs, current testing guide and only public 1.6 supported-version metadata;
Zombieland retains all 53 music tracks. Both attachments are delivered to Mortal
and downloaded through the installed Discord connector to verify their full
SHA-256 hashes before deleting the exact superseded own post. Direct CDN reads
returned 403; the connector's attachment download succeeds. This verifies remote
bytes, not Windows gameplay or that Mortal has installed the new pair.

The tester post is retained in local delivery receipts.
Total Fog ZIP SHA-256 is
`a50dec798b66aef8a09f603737c6676bbb12d824fb1e4bc1f3017d0a5d825f32`;
Zombieland ZIP SHA-256 is
`93700007731184256bf8d073380e8d517e7da32993b5528d40b0f5387743c725`.
Remote proof and deletion receipt are
artifacts/mortal-area-warning-remote-verified.json and
artifacts/mortal-area-warning-superseded-post-deletion.json.

Evidence: artifacts/zombieland-area-warning-{before,after}.json and native logs,
their exact build/deploy manifests, and
artifacts/zombieland-area-warning-acceptance.json. The extended before/after run,
logs and exact paired manifests are artifacts/symbiant-area-warning-*; its
acceptance is artifacts/symbiant-area-warning-acceptance.json. The preceding
pair's timing is artifacts/comparison-area-warning-player-1000.json and its
native gates are artifacts/feedback-verification-area-warning-player-1000-skyhdb01.
The final pair's comparison is
artifacts/comparison-area-warning-position-player-1000.json; its native gate is
artifacts/feedback-verification-area-warning-position-player-1000-c5u1xyhg and
artifacts/zombieland-current-package-gates.json.

## Albino native scream and job continuation, 2026-10-06

The native AI probe reproduced a real Zombieland job reset. At scream phase 40,
a victim's melee hit reached Pawn_JobTracker.Notify_DamageTaken and its job
reconsideration. JobGiver_Sabotage stopped the existing job while proposing
Sabotage, so InitAction cleared the active scream. The trace and clean native
log are retained in `artifacts/zombieland-albino-scream-visible-muted-before.*`.
Earlier lifetime-limit failures are retained in `*-fixture-1.*` and
`*-fixture-2.*`; a later victim killing the Albino before acquisition is
`*-fixture-3.*`. None is replaced by a later successful result.

DecompilerServer's public-engine source confirms CheckForJobOverride asks the
think tree for a job, then ShouldStartJobFromThinkTree compares the current job,
its driver continuation and the source node. JobDriver.IsContinuation defaults
to true. Removing the JobGiver_Sabotage StopAll call restores that decision;
there is no new gameplay state or Harmony patch. Evidence is retained in
`artifacts/albino-{damage-job,job-override,should-start-job,continuation}-source.json`.
The engine MVID matches the native interruption stack.

`totalfog/zombieland_albino_scream` runs the Albino's native AI and ordinary
Normal playback in a remote staged room. Its two hostile human targets are held
in their initial Wait jobs by a scoped override-check prefix. The combined
gate exposed that native Wait can also strike an adjacent enemy without a new
job. Per-victim copied PawnKindDefs therefore disable automatic melee; the
shared definitions remain unchanged. This prevents a fixture opponent killing
the Albino before the action under test. The first combined gate failure is
retained in `artifacts/feedback-verification-albino-continuation-player-1000-gxy628wp`
and is not accepted as a gameplay regression or replaced by later proof. The Albino
itself is never assigned a job or forced scream/queue/cooldown state; its
initial cooldown and native emergency decision remain intact. A separate
native damage-job notification exercises the exact continuation branch without
changing health. Only that notification is staged; the scream helper and tick
stepping are never called. Scoped probes/options are removed, created things
are destroyed and the unchanged named Base is reloaded paused.

All three controls, hidden-muted, visible-muted and hidden-unfiltered, pass
seven native checks each. The initial focused runs acquired at tick 332,
observed phase 12 at tick 343 and completed at tick 731. With automatic victim
melee disabled, the combined gate independently acquires at ticks
2254 / 1897 / 1923, observes phase 12 at ticks 2265 / 1908 / 1934, and expires
at ticks 2653 / 2296 / 2322, respectively. These are distinct retained runs;
no cooldown or acquisition time is forced. Visible phases submit 26 scream
meshes and 13 bubble draws; hidden phases submit zero.
The hidden-muted start has no Scream sample; both positive audio controls have
factor 1, real source peak 0.5 and observed playback. Both targets are affected
and enter native Vomit/Stun. The damage notification preserves the exact job,
driver and phase 12. The bubble expires and deregisters, while the hidden Albino
remains alive with one draw registration and a future cooldown. No active
scream reset is observed. All logs pass the native error guard.

`artifacts/zombieland-albino-scream-acceptance.json` records all 21 checks and
exact built/deployed gameplay and companion hashes. Base remains SHA-256
`88ea9ed3b6c25ca58331678aabcd86b276310c04da29500c556b0a8bea958207`.
Broader victim combat, hack presentation and interrupted save/load remain open.
This is separate from the prior 23 controlled sabotage helper contracts.

The changed gameplay pair, Total Fog `600083e5...` and Zombieland `cb27dbd1...`,
passes a fresh matched six-process comparison, O/C/C/O/O/C, with the updated
Zombieland DLL used in both variants. Fixture/settings/camera/engine/hardware
match; all five DLCs are active. The 250x250 fixture has 1,070 pawns including
1,000 zombies. Warmup is 300 native ticks. Ordinary Ultrafast is selected with
force/debug boost off, native tick multiplier 15 throughout, and no DPA or
zombie-work instrumentation. Original/candidate median TPS is
293.7767533750 / 313.3143726437, 6.6505% higher for Total Fog. Mean tick elapsed
is 2.3968611248 / 2.2343347404 ms, 6.7808% lower. Frame p95 is
70.2680 / 71.2907 ms, 1.4554% higher. All six native logs are clean. This is a
bounded fixture floor, not universal FPS/TPS or a claim about Windows, full
loadouts, spikes or long sessions. See
`artifacts/comparison-albino-continuation-player-1000.json`.
The complete exact-pair native gate now also passes, preserving the first
failed combined attempt. Current proof is
`artifacts/feedback-verification-albino-continuation-player-1000-w3o4806p`:
21 Albino controls, seven danger-area warnings, four arrival controls and
incident failure/exception restoration, twelve Symbiant native/pixel cases,
54 manual targets and forty 400/4,000-cell dense/sparse render-cost rows.
All native logs are clean, Base is unchanged, and game termination is verified.
`artifacts/zombieland-current-package-gates.json` records the current exact pair.

The canonical `./scripts/mod feedback-package` then passes 263 independent tests
and its exact-byte native/performance package gates. Both separate ZIPs pass
integrity, public 1.6 metadata and payload checks. TotalFog-preliminary.zip is
2,338,489 bytes, SHA-256
`3cb03abdeefc564572e0819c9df866c55623218b5026cff50a32841e21fafe31`;
ZombieLand-preliminary.zip is 193,883,756 bytes, SHA-256
`60e9780b438bb7be42a7b909be5d104cf879ba848783132c3c0aea67ae055134`.
All 53 OGG tracks and the music README match the source payload. Packaged main
DLLs match the accepted `600083e5...` / `cb27dbd1...` pair; companions, symbols,
source and Finder metadata are excluded. The audit is retained in
`artifacts/albino-continuation-package-audit.json`. These packages are local;
Mortal still has the preceding verified pair. This records packaging, not a new
Discord delivery.

## Explosion camera privacy, 2026-10-06

Public-engine DecompilerServer source shows DamageWorker.ExplosionStart creates
its flash, directly shakes the current-map camera, and then creates center
effects. The shake has no sight check. Zombieland's suicide-bomb producer and
its toxic/electrical damage definitions use this native worker path. This is
separate from particle rendering and positional sound policy.

The staged producer matrix reproduces eight hidden failures on Total Fog
`600083e5...`: ordinary bomb, native ZombieLand.Explosion.Explode, toxic splatter
and electrical shock each request one real shake both initially hidden and
after sight loss. All eight visible/colony-bypass controls pass. The retained
RED is `artifacts/explosion-camera-before.json` and its clean native log.

ExplosionVisibility replaces only that native DoShake call with the existing
current-cell visibility query. It adds no tick/frame work, stored state or
Zombieland dependency. The transpiler requires the one audited call site.
On Total Fog `6f60493b...` and unchanged Zombieland `cb27dbd1...`, all sixteen
native rows pass. Hidden calls and native shake magnitude are zero. Positive
controls retain the exact prior requested magnitudes, 0.268880874 for radius
1.9 and 0.424548745 for the actual suicide bomb, with native capped magnitude
0.2. All blast waves complete after sixty staged native ticks; cell processing
remains nine or twenty-nine calls respectively. Bomb heat remains one native
push, toxic/electrical heat remains zero, and bomb damage still affects the
fixture wall. Toxic splatter retains its non-damaging behavior. The named Base
is restored paused and remains unchanged on disk.

GREEN and exact main hashes are `artifacts/explosion-camera-after.json`, its
clean native log and `artifacts/explosion-camera-build-pair.json`. This accepts
staged native producers and camera privacy, not natural zombie attack AI,
particle pixels or third-party DamageWorker overrides. The canonical native
and packaging commands now require all sixteen rows. Updated whole-game
performance and packaging for this changed Total Fog DLL were pending at this
focused-check boundary.

The same `6f60493b...` / `cb27dbd1...` pair now passes a fresh six-process
O/C/C/O/O/C comparison, preserving the same 250x250, 1,070-pawn/1,000-zombie
fixture, all-DLC profile, native fourth-speed multiplier 15, settings and
camera. Warmup is 300 ticks; debug boost, forced speed and work/DPA profiling
are off. Original/candidate median TPS is 291.3977078119 / 311.3350457749,
6.8420% higher; mean tick elapsed is 2.4215529824 / 2.2636185491 ms, 6.5220%
lower. Frame p95 is 70.2711 / 71.2763 ms, 1.4305% higher. All six native logs
are clean and the exact-pair floor passes. This remains bounded Mac fixture
evidence, not universal FPS/TPS or Windows/full-loadout/spike/soak acceptance.
See `artifacts/comparison-explosion-camera-player-1000.json`. Combined native
verification and packaging are running; no new Discord delivery is recorded.

The complete current-pair native gate then passes in
`artifacts/feedback-verification-explosion-camera-player-1000-sfxl8as6`:
21 Albino controls, sixteen explosion-camera controls, seven danger warnings,
four arrivals and incident-parameter restoration, twelve Symbiant native/pixel
cases, 54 manual target checks and forty dense/sparse 400/4,000-cell render-cost
rows. Native logs are clean, Base is unchanged and termination is verified.
The canonical `feedback-package` then passes its 263 independent tests and all
exact-byte gates. Separate ZIPs pass the full payload audit, including all 53
OGG tracks and the README. The TF/ZL main pair remains `6f60493b...` / `cb27dbd1...`.

Mortal receives the pair in DM post `[private message reference]`, with the inline robot
prefix, two concrete fixes, matched fixture TPS and Windows/full-loadout limits.
Both remote attachments are downloaded through the installed Discord connector
and match the packaged bytes: TotalFog-preliminary.zip 2,339,026 bytes, SHA-256
`86fc82a14d8a45c38187c440f83d8bccb5e990533958e6b08f4eda3eafe2c5ea`;
ZombieLand-preliminary.zip 193,883,910 bytes, SHA-256
`849250f7f668c1705232e2b6410cad116d2770d93d6a1358ab2e78ba538c1108`.
Only after that proof, the exact superseded own tester post
`[private message reference]` is deleted. Readback finds the new post and both files;
the old message is no longer returned. Receipts are
`artifacts/mortal-explosion-camera-{package-audit,delivery-summary,remote-sha,old-post-deleted,old-post-readback,after-delete-readback}.json`.

The existing reply watcher is verified loaded with a 900-second interval and
a successful no-error check after the restart. Its cursor is preserved, so a
reply predating the new tester post is not skipped. Its bounded expiry is
extended by 24 hours for this test handoff. No Windows or full-loadout outcome
is inferred from posting the files.

### Contamination overlay reproducer, 2026-10-06

The delivered TF `6f60493b...` / ZL `cb27dbd1...` gameplay pair reproduces
contamination markers outside current sight in both native overlay paths.
Eight paused rows stage hidden/visible/hidden-again/colony-bypass sight in a
remote remembered 3x3 area with ground and one steel stack at 0.65. The detailed
path submits 26 marker calls in every row; cached ground and item answers stay
true outside sight. On/off crops contain 1,066/973 new green pixels in the two
detailed hidden rows and 484 in each cached hidden row. Positive controls retain
markers. Selection is correctly blocked in all four hidden rows, all values
stay 0.65, ticks stay 18, native logs are clean, and the unchanged base save SHA
remains `88ea9ed3b6c25ca58331678aabcd86b276310c04da29500c556b0a8bea958207`.
Evidence is `artifacts/contamination-overlay/remembered-before{,-pixels}.json`;
the native companion captures actual on/off drawing, restores its options and
temporary Harmony probe, and reloads the unchanged base.

The earlier first attempt rejected a central sandstone chunk before staging.
A subsequent unremembered-cell run also omitted paused fog-mesh regeneration,
so its detailed pixels do not prove remembered-terrain privacy. Both are kept
as fixture diagnostics, not mod defects or acceptance. The corrected probe
explicitly reveals the staging cells and regenerates the paused native meshes.
Raw contamination stat helpers remain diagnostic until their player UI callers
are exercised. This is a confirmed presentation defect, not simulation loss;
the cached fix and post-fix/performance checks were still open at reproduction.

On TF `e24d50df8dfb768a60f21f7e803643eb06051819830dda78b470f3dc0702fc89`
and ZL `ad349aa2e82f9edef7ede8128f9b6fbcf0cde1a936aae4a3854f0b99c820d4d1`,
all eight staged controls pass after the detailed gate and native section-cache
fix. Hidden on/off crops are exactly identical; detailed positives retain 3,700
changed pixels and cached positives 444. Selection and both data values match
their controls. A further eight rows replace staged coverage with actual
security-bell spawn/despawn while paused. All eight native rows and the same
pixel controls pass without advancing tick 18 or explicitly regenerating map
meshes. The native observer's PostDeSpawn clears coverage and the guarded
MapComponentUpdate publishes its queued section changes during the pause.
The unchanged base SHA remains the same and native logs pass the existing
recognized-error guard. Preserved reports, pixels, exact identity and log proof
are under `artifacts/contamination-overlay/{staged,native-source}`.

The broad renderer rebuilds only dirty/returning on-camera sections, reuses
native mesh ownership and the original color/opacity formulas, and owns one
dedicated vertex-color material. It never changes the shared engine material.
Ground data dirtiness uses its existing debouncer, item dirtiness its existing
refresh point; a contamination-only flag avoids rebuilding unrelated layers.
No visibility revision/event API is added. Returning after a drawing gap forces
a local refresh; empty meshes are not finalized or submitted. The native cached
route is intercepted in ContaminationGridUpdate so Symbiant cleanup cannot
submit the old drawer with Total Fog present.

The new layer increases draw calls per populated section. At this initial gate,
its steady and moving overlay-enabled cost, hidden mutations, reopening/off-camera,
map switching, standalone fallback and larger maps remained unaccepted. Later
enabled-overlay and standalone results below close only their stated checks.
A fresh six-process ordinary fourth-speed 1,000-zombie comparison passes under
label `contamination-player-1000`: median TPS is 317.7687 versus 287.0974,
10.6832% higher; mean tick CPU is 2.213596 versus 2.458962 ms, 9.9784% lower.
Frame p95 is 70.5089 versus 69.9316 ms, 0.8255% higher. All samples preserve
ordinary Ultrafast at native multiplier 15, with a predeclared 300-tick warmup,
matching initial fixture/settings/camera/loadout/engine/hardware and identical
Zombieland ad349aa2... bytes. No DPA or zombie-work instrumentation is enabled;
native logs pass and process termination is verified. Gameplay bytes remain
e24d50df... / ad349aa2.... This measures closed-overlay gameplay,
not this overlay's enabled frame cost. Both preserved pixel bundles pass
the canonical verify-rendered command. The staged bundle explicitly labels its
retained Player.log as the later native-source run on identical gameplay bytes;
the staged run's log was checked live but not retained before the restart.
Raw stat/icon/inspection caller contexts
remain open. No new tester build is sent based solely on these sixteen controls.


The optimized contamination refresh loop reads each ground/item color once and
uses the single optional current-cell visibility guard. The exact public pair
e24d50df8dfb768a60f21f7e803643eb06051819830dda78b470f3dc0702fc89 /
56daa1f257a72d3a69e960a97b48a442f1b92798eb0d0dbd73df48f80667d109
passes all 14 native sight/refresh rows and 28 on/off crops under
`artifacts/contamination-overlay/native-refresh-lean`. Hidden data mutation,
reveal, off-camera return, zoom return, close/reopen and clearing are covered;
hidden and cleared crops are identical and visible strengths follow changed
values. Native sight is provided by actual bell spawn/despawn, game ticks stay
paused, simulation values are retained, and the unchanged base save is reloaded.
The retained native log has no recognized runtime errors.

Paused matched-visibility 4,000-cell diagnostics under
`artifacts/contamination-overlay/cost/lean-4000-{dense,sparse}.json` show dense
section refresh 19.1991 versus native drawer regeneration 21.9416 ms; three
sparse pairs show 23.8153 versus 21.7093 ms. Warm overhead for the native
section route is about 0.02 ms/frame dense and 0.05 ms/frame sparse. These are
inclusive native timings with probe/scheduling overhead, not GPU completion
or TPS proof. A timed-out earlier 4,000-item setup is preserved separately;
it repeatedly searched all map things for each item. The diagnostic setup now
uses already-owned thing references and finishes normally. This was probe
setup, not the production renderer.

`artifacts/comparison-contamination-overlay-player-1000.json` supplies the
separate enabled-overlay whole-game acceptance on those exact gameplay bytes.
Six fresh processes alternate original/candidate with 300 predeclared warmup
ticks, unchanged 250x250 saved fixture, matching settings/engine/hardware/camera
and 4,000 sparse ground cells staged at 0.65. Native ordinary Ultrafast remains
at multiplier 15 throughout; debug boost and forced speed are off. All 1,000
zombies remain at each measured endpoint. No DPA or zombie-work probes run.
Median TPS is 226.800736 versus 212.672067, 6.6434% higher. Mean tick elapsed
is 3.049135 versus 3.304660 ms, 7.7323% lower; median frame p95 is 82.9023
versus 78.2577 ms, 5.9350% worse. Both visibility policies remain active, so
rendered geometry need not match. Native logs are clean, saved input is unchanged
and every process terminates before switching binaries. This accepts the
bounded TPS floor, with a recorded frame-tail tradeoff, not universal performance.
Moving cameras, map switches, larger maps and standalone no-Total-Fog startup
remain separate outstanding checks.


The final paired feedback attempt at
`artifacts/feedback-verification-contamination-overlay-player-1000-f4ui6jd4`
completed all native scenarios. Its last pixel checker initially stopped because
the runner omitted `player-log-summary.txt`; this was a verification-artifact
failure, with no recognized native game errors. The runner now writes the actual
validated summary output. All captured canonical semantic, pixel, log, unchanged
save and current exact-pair guards were rechecked without rerunning scenarios;
GABS separately confirmed stopped with no runtime/process. `recovery.json` and
`recovery-termination.json` record this repair. The refreshed paired package gates
accept 12 Symbiant pixel cases, 54 interaction checks, 40 render-cost rows,
4 arrivals, 7 danger warnings, 21 Albino actions, 16 explosion-camera controls
and 14 contamination sight/refresh cases on e24d50df... / 56daa1f2....
Windows and broader gameplay/loadout checks remain open.


The current preliminary pair was delivered through Discord Standin in separate
complete ZIPs to Mortal, message [private message reference]. Remote attachments were
downloaded through the connector and matched local SHA-256/size exactly:
TotalFog-preliminary.zip, 2,339,373 bytes,
fbcb3aa874ef01850d6ccecbc1f8908512e59524e4bad1655116017edde2fd93;
ZombieLand-preliminary.zip, 193,885,529 bytes,
a9e6461c29b9e4fce3261ed22e8611b7b4256a8f96d60799adf6c07e7c05dc11.
Zombieland contains all 53 music tracks and their README. The agent post starts
with the required inline robot prefix, reports the TPS gain and frame-tail
tradeoff, and requests Windows/busy-save feedback. The exact superseded own
post [private message reference] was deleted only after remote hash verification.
See `artifacts/mortal-contamination-feedback-{package-audit,remote-sha,delivery-summary}.json`.
Tester feedback remains pending; private discussion and delivery receipts stay local.

### Zombieland without Total Fog

A fresh isolated Steam/all-DLC profile, `total-fog-zombieland-fallback-16`,
loads nine active/loaded mods with Total Fog absent and no configuration issues.
On the unchanged delivered Zombieland gameplay SHA 56daa1f2..., the native
startup diagnostic confirms all four optional fog delegates are null. The
contamination UI contract passes before and after saving/loading
`Zombieland_Fallback_Overlay`; the selected steel retains 0.65 contamination.
Ordinary unforced Normal playback advances 179 ticks over 3.049 seconds. Native
close and distant screenshots succeed, recognized Player.log errors are absent,
and GABS verifies process termination. These are bounded native drawing smoke
checks with the beauty overlay enabled, not isolated contamination pixel
comparisons or standalone performance acceptance. No full Zombieland mechanics
or long-session claim follows from this fixture.

The old UI probe searched the companion assembly for gameplay patch helpers.
Its retained failure is diagnostic-only; using `typeof(Zombie).Assembly` repairs
that lookup. The new read-only binding diagnostic and corrected helper run from
companion SHA c32b16ec.... No gameplay code changed. The quiet pinned-SDK paired
build/deploy succeeds and preserves both hashes. Receipts, native screenshots,
the retained Player.log and bounded summary are under
`artifacts/native-session-total-fog-zombieland-fallback-16`.

### CE pistol firing and active reload persistence

On current Total Fog gameplay SHA e24d50df... and installed CE MVID
2154499a15df47859832e4d0c305b722, the isolated ten-mod Steam/all-DLC CE profile
passes ordinary player equip, ammunition pickup, reload and ranged-attack
commands. A drafted colonist at (107,115) fires its CE M1911 at a battery eight
cells away. During 360 unforced Normal ticks, the battery falls from 100 to
79 HP and the seven-round magazine empties. No shot method or damage method is
invoked directly by the test. Native save XML confirms `CombatExtended.Verb_ShootCE`,
the current `ReloadWeapon` job, its queued `AttackStatic` target, zero loaded
rounds and fourteen remaining .45 ACP FMJ rounds.

`TotalFogCE_Active_Pistol` retains that state through both an in-process load
and a full game restart. Each replay starts at 79 HP with the reload active,
then deals another 21 damage during 360 Normal ticks. The fresh-process resumed
save has seven reserve rounds and an empty magazine, with the same gun, target
and queued attack. The active input save retains SHA 3d3159139141eccb...;
the ordinary base fixture is loaded again before verified process termination.
Both retained Player.logs have no recognized errors. The native CE hidden/reveal,
failed-ballistics and enemy-fog hit checks also pass on this current DLL.

An earlier attempt to use the inactive Zombieland companion's generic pawn
fixture failed to resolve its gameplay helper type in this CE-only profile.
Its receipt and blocking attention are retained; it is not a CE or Total Fog
gameplay failure. The accepted sequence uses RimBridge's native item/building,
selection and command tools. Source inspection confirms CE's overhead-projectile
shoot-line shortcut; the existing no-LOS policy remains unchanged. Actual
turret/mortar, sight-loss-during-firing, enemy acquisition and broader weapon
coverage are still open. Receipts, save-state assertions and logs are retained
under `artifacts/ce-combat-native`.


### Public source and upstream escalation

The public development repository is https://github.com/pardeike/TotalFog,
with main as its default branch and GitHub issues enabled. Its first source
snapshot is e28a51ba7e47ae40e67a73c803dc6c4d0a139741. Private development history
remains on the local total-fog branch; public-main tracks origin/main. Private
chat links, delivery message identifiers and scheduling discussion are removed
from the public snapshot. Ignored artifacts and unfinished working changes
are not published. Original source, assets, license and attribution remain.
The upstream-baseline tag is an unmodified snapshot of upstream commit
6d176be50656e3056f6760eaa4e3c94a2ba9f22c; its definition trees and both original
assemblies are byte-identical to the retained performance baseline.

A fresh temporary clone from GitHub passes the canonical build and 263 tests,
producing the exact e24d50df... gameplay DLL from the current tester pair. The
canonical staging function recreates the original baseline from the public
tag without relying on private history or pre-existing artifacts. The temporary
clone is removed; receipts and logs stay in artifacts/public-source-fresh-clone.
This is source publication, not a player release or Steam publication.

AGENTS.md now requires an upstream GitHub issue when a native reproduction
establishes a blocker in another mod that cannot reasonably be solved in Total
Fog. The report must link the public integration contract and relevant source
at the published commit, include a short optional API example, and leave the
compatibility check open until our own native validation passes. The architecture
document supplies a once-bound optional query example without a Total Fog build
dependency. No upstream blocker is established by the turret defect below;
both corrections belong in Total Fog.

### Powered CE mini-turret sight and firing

The installed Steam CE 16.7.3.0 turret derives from Building_Turret rather than
Building_TurretGun. On e24d50df..., native power becomes active but sight remains
zero. Releasing its actual Hold fire gizmo and playing 361 Normal ticks consumes
ten rounds while the target remains unseen. The earlier receipt named
before-fix-powered actually has PowerOn false; the later power-playback/state
receipts establish the powered reproduction. Neither fixture wiring delay nor
that premature filename is treated as a gameplay failure.

The correction uses Building_Turret.AttackVerb for unmanned turret sight and
requires current faction coverage for unmanned turret targeting. Other non-pawn
casters retain their native policy, and the existing manning-pawn and no-LOS
paths remain. There is no CE gameplay assembly reference, new scheduler or new
saved data. Two targeted source-linked cases fail before the correction. The
canonical deploy then passes all 267 independent tests, including twelve CE
adapter cases. Built/deployed main SHA-256 is
6527f4d85118066ff4185d91279f082a4ee0a97fe8710c9a7aa359694f8c2843;
companion SHA-256 is
0636c4bd3d41f9ea4f3df13a49c7590cebd8ae50731db137c18e73c29f35a02b.
The existing native CE pawn hit-check matrix also passes on this candidate.

Native automatic-firing controls use a real CE mini-turret, charged battery,
conduits and a generated hostile pawn. Only the initial magazine is preloaded
by the fixture; shooting uses the turret's ordinary AI and native verb. At
16 cells, power supplies the expected rounded 34-cell sight radius from native
range 48 and modifier 0.7. Hold fire retains all 100 rounds; releasing it consumes
ten rounds and increases injuries from 3 to 49.6667, downing the target. Destroying
only the owned battery/conduits through their normal native lifecycle removes
power, reduces sight to zero and hides the target during 242 Normal ticks. That
power row proves coverage removal, not interruption of active firing, because
the target is already downed and Hold fire is enabled.

At 42 cells, outside the turret's own sight but within its native weapon range,
the live enemy remains hidden, healthy and waiting. Two 360-tick windows retain
100 rounds and lastShotTick -999999, including after a real security bell reveals
and hides the target while paused. The powered, unheld fixture is saved as
TotalFogCE_MiniTurret_Hidden. Both an in-process load and a fresh-process load
retain power, 34-cell sight, ammunition and the hidden healthy target. Another
361 and 360 Normal ticks respectively produce no shots. Revealing that same
far target after the fresh load consumes ten rounds and advances lastShotTick
to 1697. These distant shots miss; nearby damage is independently proved above.
An initial test assertion incorrectly required distant shots to cause injury.
The retained far-firing-assertion-review explains the correction; ammunition
and lastShotTick establish actual firing without changing native accuracy.

The input save SHA-256 remains
e8a851a7711e5128327921ff79a1068a75c96c974b41d580eecc2a168ced6179.
Both candidate Player.logs contain no recognized errors, fixture objects are
removed, the unchanged base save is reloaded and both process terminations are
verified. Receipts, complete logs and build identities are retained under
artifacts/ce-turret-native. All playback is Normal and unforced; the bridge's
startup debug boost does not affect that speed. This does not establish
fourth-speed performance, native ammo reloading for turrets, active-burst sight
loss, manned weapons, mortars or broader combined loadouts. Mortal retains the
preceding tested pair; this CE-only change has not been sent as a tester update.

### CE mortar sight, automatic acquisition and native reload

The installed public Steam CE 16.7.3.0 mortar marks its verb as requiring LOS,
but the engine's ProjectileFliesOverhead query returns true. On the preceding
6527f4d8... gameplay DLL, manning it extends the crew's sight to the 700-cell
weapon range, reduced to 455 cells by the scene's normal sight modifiers. The
same saved scene on the range correction reports 39 cells and hides the enemy
60 cells away. Later playback uses the changing native lighting/weather and
reports ordinary crew radii of 53..58; it does not pin those modifiers.

The range correction alone exposes a second native failure: CE's overhead
automatic-target scan skips its LOS hook, consumes a shell at an unseen healthy
waiting pawn and advances lastShotTick to 5845. Total Fog now filters CE's
existing IsValidTarget validator through current faction coverage. It uses the
manning pawn's faction when present, preserves failed native validation and
the disabled enemy-fog policy, and leaves deliberate indirect-fire orders on
their native path. Reflection resolves the hook at startup; no new scheduler,
saved data or hard CE reference is introduced. Four source-linked CE cases fail
before that correction. The canonical deploy passes 274 tests, including
nineteen CE adapter cases. Its native eight-control CE pawn hit matrix also
passes without promoting failed ballistics.

The final main SHA-256 is
c7205ee19ab8ce0ff3ebc02554f54f540d3f7704883c2b1614d3a6aa20ea63b2;
the native companion is
58dae363b0f8f8202482df5ea66ab7d40d8b8bbfd8869e6112f2c987214405ae.
The fresh mortar fixture uses ordinary generated pawns, a preloaded initial
magazine, actual Man mortar menu orders and unforced Normal playback. Other
colonists remain drafted more than 75 cells from the target. The manned,
loaded mortar cannot acquire the healthy unseen enemy 60 cells away and keeps
its shell and lastShotTick -999999 throughout 481 native ticks. An in-process
load adds 361 ticks and a fresh process adds 361 ticks with the same result.
A real security bell reveals that same healthy pawn: native acquisition changes
from false to true, and ordinary automatic fire consumes its shell and advances
lastShotTick to 4060 during 479 ticks. This proves firing, not a guaranteed hit.

Reloading is independently exercised through the ordinary Prioritize reloading
menu and CE's ReloadTurret job. A real two-shell supply drops to one while the
magazine goes from zero to one during 719 native ticks; it is not filled through
the fixture's setup-only ResetAmmoCount call. A saved active native reload also
completes on the new candidate. That older saved scene contains an already-fired
shell which subsequently kills its target. The corpse-based no-fire window is
excluded from acquisition acceptance; the healthy fresh fixture supplies those
controls. An initial fixture-layout attempt found no clear strip sufficiently
far from the colony. Its diagnostic attention was inspected and acknowledged;
moving the colonists through ordinary orders supplies the required layout.

The unchanged healthy hidden save has SHA-256
4999423f63df00f0d955d9f858c2d1d8f296a864b434dfc0d92c0c817fc6f838.
Receipts, identities and complete logs remain under artifacts/ce-mortar-native.
Both final native control/restart logs have no recognized errors, fixture
objects are removed, the unchanged base save is reloaded, and termination is
verified. Other manned weapons, manual indirect-fire orders, active-burst sight
loss, actual enemy turret acquisition, CE fourth-speed performance and combined
loadouts remain unverified. Earlier Zombieland/performance/package gates still
refer to their exact previous gameplay bytes. Mortal's tester pair is unchanged.

### Manual target candidates and blind mortar fire

An actual forced-target gizmo and map click on the c7205ee1... candidate binds
the unseen healthy enemy to the CE mortar's CurrentTarget as Human75713, despite
nativeAcquirable and player sight both being false. It consumes a shell during
480 Normal ticks. The selection can therefore track the hidden Pawn rather
than the chosen cell. This is a Total Fog UI omission, not an upstream blocker.

DecompilerServer confirms the public 1.6 GenUI.TargetsAt iterator contains one
ThingsUnderMouse call at IL_003A. The native method owns a fresh candidate list;
the iterator separately retains psychological invisibility checks, native
candidate order and its bounds/CanTarget-validated cell fallback. Total Fog
replaces only that list call with a wrapper which compacts unseen candidates
in place. It introduces no extra list, simulation mutation, saved state or
tick work. Ordinary selection and float-menu enumeration remain on their
original call paths. Registered inspection cores keep a separate targeting
policy: their native adapter supplies body-shape candidates and Total Fog checks
current sight at the clicked cell. An unseen inspection core does not prevent
attacking a visible body cell, and a visible core does not disclose hidden cells.

Three focused candidate tests fail before filtering. The final canonical build
passes all 281 independent tests, including remembered/owned objects, list
identity/order, authoritative-grid preservation, selection proxies,
uninitialized fog and root/core sight disagreements. Its main SHA-256 is
0d19b952d29918d1c45dd085a8f13c534346e07c982372b2cb794f7c35a34620;
the companion is
016459c1bf5c73f231724b57f9bff4f3df241e94e7868aa655239f4bfd045cce.

Fresh-process native acceptance uses the unchanged healthy mortar save, SHA-256
4999423f63df00f0d955d9f858c2d1d8f296a864b434dfc0d92c0c817fc6f838.
The actual hidden-enemy click now creates a valid cell target at (136,0,70),
HasThing false, with the healthy enemy still unseen. During 481 unforced Normal
ticks, native fire consumes the shell and advances lastShotTick to 3685. After
reloading the unchanged fixture and revealing the same enemy with a real
security bell, its actual click binds Human75713, HasThing true. Another 481
ticks consumes the shell and advances lastShotTick to 3693. A fresh load and
click 10 cells from the mortar, inside its 32-cell native minimum range, creates
no valid order and retains its shell and lastShotTick -999999. These are firing
and selection controls, not guaranteed-hit or fourth-speed performance evidence.

On those same Total Fog bytes with the unchanged tested Zombieland gameplay
DLL 56daa1f2..., the extended native Symbiant boundary fixture passes all 12
rendering/interaction cases and 54 manual body-cell checks. Twenty-two paired
screenshots pass the existing pixel verifier, including hidden-cell exclusion,
all-hidden silence, native/fallback positives and core/root disagreement. The
fixture matures through 61 native ticks, then keeps its evidence phases paused.
Its settings and diagnostic patches are restored and its unchanged base save
is reloaded. Both native Player.logs have no recognized runtime errors; fixture
objects are removed and both process terminations are verified.

Receipts and the retained candidate DLL are under artifacts/ce-mortar-native,
artifacts/manual-target-symbiant and artifacts/candidates/manual-target-0d19b952.
Other manned CE weapons, active-burst sight loss, actual enemy acquisition,
broader loadouts and current-candidate performance/package gates remain open.
The public API and commands are unchanged; the separate README rewrite is
preserved uncommitted. Mortal retains the earlier tested ZIP pair.

### CE firing after sight loss and native M240B controls

The beab8196... candidate closes a separate native mortar tracking omission.
On the previous 0d19b952... gameplay DLL, the same healthy mortar fixture
acquires the visible Pawn after 60 unforced Normal ticks, with its shell still
loaded. Removing the real revealing security bell makes that Pawn hidden and
not natively acquirable. Another 418 ticks nevertheless fires the shell at
tick 3685. This is a Total Fog omission; no upstream blocker or issue is needed.
The before receipts are retained. Its Player.log was not retained because the
initial copy used the wrong path and the next launch replaced the log.

DecompilerServer resolves CE 16.7.3.0's exact four-argument
TryFindCEShootLineFromTo overload and KeepBurstOnNoShootLine. The former bypasses
hit-cell checks for overhead projectiles. The latter may either convert a lost
Thing to a last known cell for a locked burst or retain a Thing for suppressive
fire. Total Fog applies current observing-faction sight to successful
Thing-bearing results from both paths, including declared overrides. Native
Retarget still runs before the shot-line check. Native failures, cell orders,
last known cell fallback, ammo preparation, projectile creation and burst
completion remain on CE's own paths. There is no tick scheduler, saved state,
per-shot reflection or new gameplay allocation. The two new adapters share the
existing turret-acquisition policy, including crew faction and disabled enemy fog.

The canonical build passes 291 independent tests, including 29 CE cases.
The ten added cases cover current sight changes, overhead weapons, native
failures, blind cell orders and converted fallback, suppressive Thing tracking,
the newly retargeted argument, crew factions and incomplete map/fog state.
The public API and workflow commands are unchanged; the separate README remains
untouched and uncommitted. Exact gameplay SHA-256:
beab8196373bd0f8ab371e68cb8085def1eba6f048b6a1fc611e989d62b9f734;
companion SHA-256:
ba5fafb6e20a2b3806ba2b1ba5a275171bdf0796f71adab41418c53d99d96bce.

On these exact bytes, the unchanged healthy mortar save repeats the same
visible acquisition and sight loss. During 421 Normal ticks while hidden,
the shell remains loaded, lastShotTick stays -999999 and the automatic target
clears. Restoring the bell permits a normal Pawn-bearing shot during 480 ticks,
consuming its shell at tick 4167. A separate unheld manual click on the hidden
Pawn's cell fires during 481 ticks; the weapon retains a cell target rather
than a Thing. A fresh unheld order ten cells from the mortar, inside its
32-cell native minimum range, receives no shot during another 481 ticks.
The first manual-order attempt had hold fire enabled; its window is excluded
from blind-fire acceptance and retained separately as a fixture precondition.

The native M240B fixture uses a healthy waiting enemy 60 cells east of the
weapon, ordinary ManTurret, the default 360-degree fire arc and native Fog
weather. The weather reduces the crew's sight below that distance; no fog
grid is manually changed. Its unchanged hidden save is
246711950f8cb3c042e4e1749c207918aaa750d1f149c6f402f253a524c820de.
During 481 Normal ticks it remains manned, hidden and loaded at 180 rounds,
lastShotTick 12690. With a real revealing bell, 90 ticks acquire the same Pawn
and fire three rounds. Removing the bell and advancing 14 ticks shows the
weapon itself has switched to a cell target at (121,0,70), HasThing false,
while still bursting. Another 362 ticks completes the native ten-round burst
at 170 rounds, then clears automatic targeting without further hidden shots.
The older turret-only probe could not distinguish this cell fallback from
continued Pawn tracking. The retained probe now reports both targets separately.

Ordinary Prioritize reloading M240B consumes exactly the 30 missing rounds
from a real 400-round supply during 721 Normal ticks, producing a full
200-round magazine and 370 rounds remaining. Both counts survive in-process
save/load. A fresh game process also retains those counts and separately
replays the unchanged manned hidden save for 362 ticks without any new shot.
The reload save is
b9455fc6c8e04a3f89fa0a51deb0bff89e2ceba40383e35759a4c7448d95363a.
Fixture objects are removed, the unchanged base save is reloaded, both final
logs pass the existing runtime-error summarizer and termination is verified.

Receipts, the two final Player.logs, identity checks and the bounded acceptance
summary are under artifacts/ce-m240-native. The exact tested pair is retained
under artifacts/candidates/ce-shottracking-beab8196. These Normal-speed controls
do not prove guaranteed hits, narrowed fire arcs, live suppressive Thing
fallback or nearby retargeting, non-magazine ammo handling, actual enemy
turrets, broader CE/Zombieland combinations or current-candidate performance.
Those checks, Multiplayer and the full release-candidate gates remain open.
Mortal's tested ZIP pair is unchanged.

### Native narrowed M240B firing arc and sight controls

The unchanged beab8196... gameplay bytes also pass a native 90-degree M240B
arc control with CE 16.7.3.0 and all DLCs. The proof companion SHA-256 is
90f2c2e0fd73a66c3d04ec9fcd4e6bf3cd4776e943f72da8cb3eea8cd60fdbb6.
CE's enableArcOfFire setting is initially false. The native semantic settings
tool enables it only in memory, with write=false. The fixture validates the
span against native CompFireArc bounds, writes its configuration fields and
invokes Building_TurretGunCE.PostAdjustFireArc, the same callback used when
the editor commits. Native target acquisition, bursts and ammunition remain
untouched. No gameplay fix is needed.

The healthy hostile waiting Pawn remains 60 cells east of the manned gun,
inside its native 62-cell weapon range. A real player-faction security bell
provides shared sight. With center 0 and span 90, native WithinFireArc and
acquisition are false despite valid sight. During 507 unforced Normal ticks,
all 180 rounds and lastShotTick 12690 remain. Turning only the center to 90
makes the same target eligible. Another 112 native ticks fire six rounds,
leaving 174 and lastShotTick 13966, with a real Thing target and native burst.

The pre-fire native inside-visible save is reloaded to retain a healthy target
and all 180 rounds. Removing only the real bell makes sight/acquisition false
while native WithinFireArc remains true. Another 502 ticks consume no ammo
and retain lastShotTick 12690. Fresh-process reloads preserve the native arc
fields: inside-hidden center 90/span 90 produces no shot during 540 ticks,
and outside-visible center 0/span 90 produces no shot during 541 ticks. The
restarted profile retains its original false arc setting, which is explicitly
enabled for these controls; this is not changed-settings persistence proof.
Both fresh controls remain healthy, manned and unheld.

The original CE setting is restored without writing it to disk. The seven
owned objects are removed, TotalFogCEFixture is reloaded, and process
termination is verified. Both retained control/restart logs pass the existing
error summarizer and there is no open attention. The sixteen checked receipts,
exact gameplay/companion bytes and three native save hashes are retained under
artifacts/ce-arc-native. The canonical build passes all 291 independent tests.

Editor interaction is excluded from acceptance. Both bridge virtual clicks
and actual application mouse attempts left CurrentCenterAngle/CurrentSpan
unchanged. Their receipts/log are retained separately; this does not establish
a Total Fog bug or an upstream blocker. The native configuration control
proves targeting/fog behavior and persistence for this M240B case, not editor
input, every CE weapon/arc, suppressive Thing fallback or nearby retargeting.

### Native enemy CE mini-turret and observing-faction controls

The unchanged beab8196... gameplay DLL passes the native automatic enemy
mini-turret control on the separate Steam CE/all-DLC profile. The companion
4c27fc7ec64a2c49c746ad2bba05be064d4fe3b0c1410d90a586c0b075d98fb1
adds enemy ownership, observing-faction readouts and a controlled enemy-fog
setting action using the ordinary settings refresh. No gameplay fix is needed.
The fixture uses OfAncientsHostile turret faction 9 and a healthy drafted
player-faction 16 Pawn, waiting 42 cells east. Native weapon range is 48;
the configured 0.7 modifier yields 34 cells of enemy sight. Player visibility
and the observing faction's grid are reported separately.

With enemy fog disabled, ordinary automatic fire consumes ten rounds during
361 unforced Normal ticks, from 100 to 90, lastShotTick 442. The enemy source
has radius -1 and supplies no enemy-fog sight work. Enabling the option while
paused supplies 34-cell sight and immediately makes the healthy distant Pawn
not acquirable. Another 362 ticks retain all 90 rounds and lastShotTick 442;
the automatic target clears even though the player's own Pawn remains visible.
A real security bell belonging to the turret faction grants shared sight at
the same target. Native acquisition becomes valid, and another 360 ticks
consume ten rounds, from 90 to 80, lastShotTick 1014.

Reloading the unchanged hidden save in-process retains the healthy waiting
target, power, 34-cell sight and 90 rounds. Another 361 ticks produce no shot.
A separate positive sight control removes only the fixture battery/conduits
while keeping the real enemy-faction bell. Shared target sight and CE's bare
target validator remain true, but PowerOn is false and the turret's own sight
is zero. During 360 ticks, native CE Active prevents fire and all 90 rounds
remain, preserving its availability check independently of Total Fog's gate.

A fresh process reloads the same hidden save. Its profile retains the original
enemy-fog setting false, so the test explicitly re-enables it before playback;
this does not prove persistence of a changed settings value. The healthy target
remains unseen by the turret faction and receives no shot during 360 ticks.
The first 62 setup ticks still had PowerOn false and are excluded from powered
negative acceptance. Decompiled native CE Tick clears enemy hold fire when its
player toggle is unavailable; no held interval is used to prove this policy.

The original test setting is restored, all owned fixture objects are removed,
the unchanged base save is reloaded and termination is verified. Both retained
logs pass the existing error summarizer. Receipts, the exact companion and the
hidden save hash are recorded under artifacts/ce-enemy-turret-native. These
checks prove bounded faction/availability behavior for the automatic
mini-turret. Other enemy weapons, native narrowed arcs/suppressive fallback,
nearby retargeting and broad CE combat/performance remain separate targets.

### CE stress-fixture preparation and timing guards

The existing original-fog 350x350 stress save contains 410 pawns, including
53 native-listed colonists. Its first unchanged copy into the CE profile is
rejected before timing: CE requires a stuff value for seventeen saved flak
vests, and the vanilla save also carries old ThinkNode keys. This is a fixture
conversion problem, not a measured performance failure. The original save is
preserved byte for byte at SHA-256
7e5b6d5c8c8e9e69e394e352c1a48cb4d09f661198ab161af6957d173a300429.

Under the original fog DLL, native CE loading supplies its ordinary garment
defaults. A native save creates TotalFogCE_Stress350_Compatible; no XML is
edited and no actors are removed. The result retains all 410 pawns, the
350x350 map and the original fog map/alert types. Its SHA-256 is
09db9bda50f9f995ec4adca1cdf21b56f75d53f696882fa57b2dd9fb4ea3b72f.
A fresh process loads it cleanly with no open attention or recognized native
errors. The adaptation receipts, original rejection, migration log and clean
fresh-load log are retained under artifacts/ce-enemy-turret-native.

The first adapted pair, ce-stress-compatible-beab8196, is also excluded:
visual-ready pausing starts the original at tick 3 and the candidate at tick
19. All other compared conditions match, and both native samples complete
without recognized errors, but the absolute warmup endpoints correctly reject
this pair. The comparison helper now enables native Pause on load only during
loading, restores the previous preference without saving it and retains that
receipt. The game, saved tick count and absolute-start comparison remain
unchanged. The first helper attempt, ce-stress-paused-beab8196, completes native
loading/playback but fails in runner load-duration accounting; it supplies no
accepted timing result. The accounting is corrected and raw load receipts are
now preserved before validation. These rejected attempts remain separately
labeled and do not establish the performance floor.

### CE large-map performance floor on the current sight-loss fix

The corrected ce-stress-paused2-beab8196 comparison completes three alternating
original/candidate pairs in six fresh public Steam processes. All-DLC CE 16.7.3.0
is loaded with identical DLL SHA-256
3102bc2276c583e51fe85ae340e5b986f80452171db63ea72d04f7651b96afe3,
MVID 2154499a-15df-4785-9832-e4d0c305b722 and effective scalar settings;
no CE configuration file exists in this isolated profile. Ammo is enabled,
mid-burst retargeting is enabled and the optional arc-of-fire setting is disabled.
This run does not validate arc behavior. The 350x350/410-pawn fixture hash,
fog settings, loadout, engine, hardware, rendering settings and camera all
match. The camera is Middle zoom at x106..194, z171..221.

Every sample starts at saved tick 3 with zero requested warmup, retains focus,
restores Pause on load from false to false, and completes 15 seconds of native
playback without recognized runtime errors. Every measured tick retains
Ultrafast with native multiplier 15; forced speed and UltraSpeedBoost are both
off. DPA and zombie telemetry are off. Sample order is original/candidate,
candidate/original, original/candidate:

| Pair | Original TPS | Total Fog TPS |
| --- | ---: | ---: |
| 1 | 476.55 | 474.39 |
| 2 | 406.85 | 459.27 |
| 3 | 403.42 | 420.91 |
| Median | 406.85 | 459.27 |

The unchanged beab8196... gameplay DLL meets the strict TPS floor here: median
TPS is 12.9% higher. Median whole-tick elapsed time, which includes scheduling,
is 1.526ms versus 1.721ms. Frame p95 is effectively unchanged, 69.47ms versus
69.32ms. The first pair is slightly slower and runs vary appreciably, so this
is bounded paired evidence, not a uniform 13% improvement or nominal 900 TPS.
The fixture is a synthetic moving colony/animal load with CE active; broader
combat-heavy CE and combined CE/Zombieland performance remain open.

The complete comparison and all six raw reports/Player.logs are retained under
artifacts. The proof companion SHA-256 is
f207d426cedabecf7b78e811b80cb7cd1568141d18a0a99ef88ab84d85fbe91f;
it is retained with the candidate under artifacts/ce-stress-native. The save
is unchanged, all six processes terminate, and the workflow restores the
candidate install. The canonical build passes all 291 independent tests.
Broader current-candidate Zombieland/package gates and Multiplayer remain open;
Mortal's tested ZIP pair is unchanged.

### Native CE nearby retargeting and initial-lighting regression

The native M240B control uses public Steam 1.6, all five DLCs and CE 16.7.3.0
with ordinary unforced Normal playback. CE's mid-burst retarget setting remains
enabled and its arc setting remains disabled; no setting is written. A native
manning pawn stands at (61,69), the gun at (61,70), and the healthy hostile
primary waits at (121,70). The magazine starts at 180. A real player security
bell reveals the primary; after 86 ticks the gun is bursting with 178 rounds.

The companion adds a native-generated waiting pawn of the same hostile faction
at (111,70). This pawn is alive, not downed and natively eligible; its four
points of generated injury are retained rather than healed by the fixture.
Removing only the bell hides the primary while crew sight still sees the
alternate. During ten further ticks CE switches the weapon's Thing target to
the alternate and consumes two rounds, reaching 176. The turret's acquisition
target is still the original primary, proving this is an ongoing native burst
retarget rather than a fresh turret acquisition. The burst finishes at 170
rounds, with the original primary hidden and untouched. No private CE burst
flags, ammo values or target fields are injected during playback.

A save taken immediately after that switch preserves the visible-alternate
Thing target, 176 rounds and active burst. Before the lighting fix, a fresh
process publishes a 39-cell crew sight radius even though the ready engine
calculates 59.268 cells, reports full ground glow and has a stationary manning
pawn. The alternate at 50 cells is therefore incorrectly hidden. Five further
ticks retain the stale radius and cancel the saved burst without consuming
ammunition. This is a Total Fog initialization defect, not a CE blocker.

Public engine source confirms that Map.FinalizeInit queues component callbacks
before MapUpdate first populates sky and accumulated lamp glow. Native Pause
on load can also execute a first tick before that frame update. Total Fog now
invokes the existing SkyManagerUpdate and GlowGridUpdate_First once in its
initialization callback, before forcing initial sight publication. It adds no
recurring work or camera-dependent hook and uses the engine's own lighting
calculations. Off-current-map and lamp-only/darkness runtime controls remain
separate outstanding checks; source ordering alone does not close them.

The unchanged switch save in a fresh process on gameplay SHA-256
2504105d82f004d6c1794ff4160f5ee10f4b89806f58678f7764b9e449bed58b
immediately publishes radius 59, sees and accepts the alternate, and continues
to reject the hidden primary. During 63 Normal ticks CE completes the six
remaining burst shots, reaching 170 and lastShotTick 13482. The earlier
beab8196... result, stale-radius diagnostics and rejected burst replay remain
retained separately. The proof companion is
07821ea57a602cba9f67062d6708835cfd86ac70c9f5ae7a7481822a4cc226d7.

The negative control reloads the same base scene, begins the original burst
and adds a native healthy alternate at (120,70), 59 cells east. Removing the
bell hides both Pawns because the crew is one row south and has radius 59.
Both are rejected by CE's native target validator. Ten further ticks convert
the weapon target to the primary's last known cell (121,70), with no Thing
target, and consume one round. The native cell burst completes at 170; no
hidden Pawn is selected. Native projectiles collide with the alternate along
that trajectory and deal 13.0979 injury, preserving simulation rather than
making hidden pawns immune. On fresh-process reload the saved cell burst does
not resume; 60 Normal ticks retain 177 rounds and lastShotTick 13440, with
neither hidden Pawn acquired. This proves the negative targeting control, not
cell-burst resumption.

Seventeen explicit receipt assertions pass in
artifacts/ce-retarget-native/summary.json. Raw before/after/negative receipts,
both mid-burst save hashes, candidate DLLs and native logs are retained there.
The logs pass the existing recognized-error check and the canonical deploy
build passes all 291 independent tests. These functional controls make no TPS
claim. The earlier large-map floor belongs to beab8196...; the new gameplay
candidate needs its own comparison and broader Zombieland/package checks.
Suppressive still-Thing fallback, no-magazine ammo, broader CE weapons, combined
loadouts and actual Multiplayer acceptance remain open. Mortal's delivered
ZIP pair is unchanged.

### Initial-lighting candidate: rejected CE performance floor

The ce-load-lighting-2504105d comparison completes all six fresh processes on
the unchanged public CE 350x350/410-pawn save. Exact candidate bytes are
2504105d...; the engine, CE bytes/settings, fog settings, camera, saved tick 3
and zero warmup match. Native fourth-speed multiplier 15 is retained with
forced speed and UltraSpeedBoost off, DPA off, no focus loss and clean logs.
Pair TPS values (original/candidate) are 490.07/495.26, 491.73/478.27 and
509.26/481.43. Median candidate 481.4302 is below original 491.7317 by 2.095%;
the strict workflow rejects the floor and returns nonzero. Tick elapsed
medians are 1.51727/1.51251 ms; frame p95 is 68.759/68.6777 ms. The complete
failed result and all raw reports/logs are retained. The earlier passing
beab8196... comparison does not establish this candidate's performance.

Separate diagnostic intervals use profileFog=true on the same save and
ordinary fourth speed, then remove instrumentation. Total Fog records 694,815
visibility reconciliation calls during 6,607 ticks; the original records
363,754 during 6,686 ticks. Total Fog's fog layer regenerations/draws take
0.6972/2.4859 ms during that whole interval. Inclusive method times overlap
and the available nested method sets differ, so these traces cannot prove a
throughput floor or attribute the uninstrumented gap. They identify repeated
visibility work for investigation. Engine map/mesh/dynamic-draw counters are
added to the same optional probe for the next bounded diagnostic; they remain
off in acceptance comparisons. No production performance shortcut is applied
based solely on these traces. The canonical build still passes 291 tests.
Raw diagnostics are artifacts/ce-load-lighting-{original-,}profile.json with
matching native logs. The candidate is restored after original diagnosis.

### Engine drawing diagnosis and direct single-cell queries

The expanded optional profile records the same native map/mesh/dynamic-draw
methods on both binaries. Mean per-call times (Total Fog/original) are
1.9468/1.8453 ms for MapUpdate, 0.2915/0.2528 ms for DrawMapMesh and
0.8793/0.8169 ms for DrawDynamicThings. These overlapping diagnostic times
do not justify changing engine registration ownership or explain the rejected
2.1% throughput floor by themselves. The exact profile companion is
b3d63df3d2c7e07b4d4360ef7d66295e2ea17bfd77507783c60381913b35d41d;
all instrumentation is removed afterward. Gameplay remains 2504105d... for
this diagnosis. Raw receipts/logs are artifacts/ce-engine-draw-{candidate,original}-profile.*.

A small production candidate avoids occupied-rectangle construction for
single-cell objects and finishes a remembered known-anchor hit before scanning
the rest of a footprint. Current vanilla fog, initialization/bypass, ownership,
interactive-core and mobile/current-sight rules still run before these paths.
Larger footprints retain their ordinary scan and rendered-cell translation.
No cache, delayed work, polling interval or registration ownership changes.
The source-linked build passes all 291 tests, including the non-anchor memory,
rotated footprint, held pawn, flyer, current-information and selection controls.

Gameplay 450329a092f654a85ffd637c8327fa3297e9ac9bebc524773ff791b4e1954db5
records 680,684 common queries at 228.8728 ms in the same optional diagnostic,
compared with 677,145 at 247.7712 ms on 2504105d.... Mean measured query time
is about 8.1% lower. Method instrumentation contributes overhead; these
different native intervals establish neither an isolated microbenchmark nor
a whole-game TPS gain. The changed candidate proceeds to its own strict
paired comparison; the prior rejected result remains retained. Its profile
receipt/log is artifacts/ce-single-cell-profile.* and the DLL pair is retained
under artifacts/candidates/single-cell-visibility.

### Single-cell candidate: bounded CE stress floor

The changed 450329a0... candidate completes ce-single-cell-450329a0 on the
unchanged CE stress fixture in six fresh processes. All identity and clean-log
checks pass, and every measured tick retains native fourth-speed multiplier
15 with forced speed and UltraSpeedBoost off. Paused loading starts at saved
tick 3 with zero warmup throughout; camera/settings/CE and engine bytes match.
Original/candidate pair TPS values are 461.11/526.36, 489.75/511.55 and
518.97/476.73. The strict median floor passes: 511.5512 versus 489.7529 TPS,
4.4509% higher. Median tick elapsed is 1.485738/1.475872 ms and frame p95 is
67.7776/67.7914 ms.

The third pair is slower and the native runs vary appreciably. This proves
only the predeclared median floor for this fixture; it does not establish a
uniform improvement, attribute all TPS variation to the direct query change,
or close broad combat-heavy/combined-loadout or Windows performance. The
prior 2504105d... failure remains retained. All six reports/logs and the
complete result are artifacts/comparison-ce-single-cell-450329a0.json and
its runtime reports. The workflow terminates all processes and restores the
exact candidate install; all 291 tests pass. Current Zombieland rendering,
behavior and package gates still need that exact gameplay pair's refresh.

### Current single-cell candidate: save-load retest and Zombieland floor

Gameplay 450329a0... with companion b3d63df3... repeats the native CE visible-
alternate save in another fresh process. Radius 59 matches the ready 59.268
calculation immediately, the visible alternate remains accepted and the hidden
primary remains rejected. During 62 Normal ticks the saved burst completes
from 176 to 170 rounds, lastShotTick 13482. Loading the hidden-alternate save
in that process rejects both hidden Pawns; 60 ticks preserve 177 rounds and
lastShotTick 13440 with no acquisition. Eight receipt assertions pass in
artifacts/ce-retarget-native/single-cell-candidate-summary.json. This repeats
the current load regression and negative control, not every native CE scenario.
Owned objects are removed, the unchanged base fixture is reloaded and the
process terminates. The current recognized-error check is clean.

The canonical Zombieland setup rebuilds unchanged exact gameplay bytes:
Total Fog 450329a0... and Zombieland
56daa1f257a72d3a69e960a97b48a442f1b92798eb0d0dbd73df48f80667d109.
The predeclared single-cell-overlay-player-1000 comparison uses the unchanged
TotalFog_Zombieland_UpstreamQuietGap1000 save, 1,000-zombie fixture and distant
4,000-cell contamination overlay. All six fresh native processes pass the
identity, population, settings, camera, speed/focus and recognized-log guards.
Fourth speed retains multiplier 15, without forced speed, UltraSpeedBoost or
zombie/DPA diagnostic instrumentation.

Original/candidate pair TPS values are 205.03/219.28, 203.34/221.63 and
203.98/216.65. Median candidate 219.2750 exceeds original 203.9821 by 7.4972%,
meeting the strict floor. Median whole-tick elapsed is 3.11073 versus
3.44015 ms. Frame p95 is worse at 81.2696 versus 76.8874 ms; the throughput
result does not imply every aspect improved. This remains bounded evidence
for this declared loadout/fixture, not all maps, combat or Windows. The
complete comparison and six raw reports/logs are retained under artifacts.
The workflow terminates all six processes and restores the candidate pair.
The exact-byte native rendering/behavior suite passes through the canonical
feedback verification: twelve Symbiant pixel cases, 54 manual targets, forty
paused render-cost rows, four actual silent arrivals with parameter-lifetime
checks, seven warning controls, 21 Albino scream controls, sixteen explosion
camera controls and fourteen contamination sight/refresh cases. Global ambient
controls also retain their previous behavior. Recognized native logs are clean;
the attempt is artifacts/feedback-verification-single-cell-overlay-player-1000-qyix2mn3.
The refreshed package gates identify the exact gameplay and instrumentation
pairs. The canonical feedback-package command passes all 291 independent tests
and native/package guards; both separate complete ZIPs retain the tested DLLs.
The testing guide is updated to this candidate's counts and bounded performance
results. Both separate complete ZIPs are delivered privately and downloaded
back with matching archive SHA-256 hashes. The superseded own preview post is
deleted only after that readback. Private message IDs and receipts remain in
ignored artifacts; the message requests Windows/busy-save and save-load
feedback and contains no CE/Multiplayer update. Zombieland's gameplay DLL is
unchanged from the preceding delivery.

### Native no-magazine short-bow ammunition control

The installed CE short bow uses Verb_ShootCE and inventory stone arrows with
HasMagazine false and UseAmmo true. Its TryCastShot prepares ammunition before
the projectile verb's shoot-line rejection, so failed preparation/shot paths
need native evidence rather than an unconditional early fog prefix. The new
companion fixture observes preparation and shot returns without modifying them.
It uses CE's own OrderForceTarget and a real security bell; its temporary
five-cell base range is restored without writing settings.

The initial fixture is inconclusive: an AttackStatic job lacking the native
selected verb never fires, and the armed pawn's effective sight subsequently
extends to twelve cells. That attempt is retained separately. The corrected
fixture uses CE's native order and places the wall thirteen cells away, inside
the fourteen-cell bow range but beyond the twelve-cell effective sight.

On the unchanged 450329a0... gameplay with companion 99b49b79..., the positive
control prepares/fires at tick 156, consuming exactly one of twenty arrows.
The shooter is stationary and healthy in this corrected attempt. At tick 266
the next native stance is Warmup; removing the bell makes the target hidden
and native CanHitTarget false. Another 240 Normal ticks produce no ammunition
preparation or shot call and retain nineteen arrows. A new hidden attack order
also prepares/fires nothing through another 180 reported Normal ticks.
Restoring the bell allows a native shot at tick 816 and consumes exactly one
more arrow. Both recorded preparation/shot pairs succeed while visible; there
is no hidden failed-shot preparation or detached pending round.

Ten receipt assertions pass across 844 reported Normal playback ticks, with
native endpoints 20..869. The steel wall remains at 300 HP, so these controls
prove ordinary shot execution and ammunition behavior, not damage. No
no-magazine ammunition loss is reproduced in this path; the native warmup
cancels before preparation. Other no-magazine verbs, direct mod-issued shots,
live suppressive Thing fallback and no-magazine save/load remain open. No
gameplay workaround or upstream issue is justified by this result.

Cleanup removes owned actors/objects and probe patches, restores range sixty,
reloads the unchanged base and verifies process termination. Recognized native
logs are clean. Raw receipts, initial inconclusive control, summary, exact DLL
pair and Player.log are retained in artifacts/ce-ammo-native. All 291 tests pass
through the canonical deploy; the gameplay bytes sent to Mortal are unchanged.

### Native CE suppressive burst and loaded-guard contracts

The unchanged Total Fog 450329a0... gameplay and companion 2b8e2742... are
archived with thirteen passing receipt assertions in
artifacts/ce-suppressive-native. The companion observes CE's actual fallback
before and after Total Fog, retaining at most 128 rows without changing results.
An initial probe setup failed because Harmony's registry needed the declared
MethodInfo rather than the inherited member's differing ReflectedType. That
prerequisite failure and blocking attention are retained separately; the fixed
probe verifies the existing production hook before installing its observers.

The native M240B is already in SuppressFire mode. With the real sight bell,
one ordinary Normal-playback shot starts the burst and leaves 179 rounds.
Removing the bell during that burst produces nine actual fallback calls at
ticks 13434..13482. Every call retains native=true/final=true, HasThing=false,
the last known cell (121,70), and faction sight=false. The burst ends with 170
rounds and no hidden-Pawn acquisition. This proves preservation of CE's blind
cell fire, not tracking a hidden Thing.

Four separate readonly calls to the loaded production guard use the actual
verb, Thing and sight state with supplied false/true incoming results. Hidden
Thing success is rejected; visible success and both native failures are
preserved. This is an adapter contract, not a naturally executed still-Thing
fallback. Earlier separate automatic and native forced-target warmup controls
cancel while hidden before any shot/fallback. The stock mortar exposes no
CompFireModes, so no unsupported suppressive mode is fabricated.

CE's native aim-mode toggle switches to Snapshot and back to SuppressFire.
The bounded trace is stopped, owned objects are removed, the unchanged base
fixture reloads and the process terminates; recognized native logs are clean.
All 291 independent tests pass. A later tool-description-only rebuild has
distinct companion bytes; the archived 2b8e2742... pair identifies this proof.
Gameplay bytes and Mortal's delivered ZIP remain unchanged. Naturally executed
still-Thing fallback, combined overhaul loadouts and combat-heavy performance
remain open; these checks do not complete all CE acceptance.

### Combined CE/Zombieland logical shot destination

The canonical ce-zombieland-setup reuses the paired build/deploy path and
registers a separate eleven-mod Steam/all-DLC profile. It validates CE's
installed public version, loads it before Zombieland, seeds only a new profile
with existing preferences/settings and preserves the standalone profiles.
The readonly native shot-line observer captures the actual result before Total
Fog and the final result after it, removing its scoped Harmony patch after each
query. Initial setup/load and all five paused acquisition states pass on
450329a0..., but 25 seconds of actual combat never fires. The more specific
native trace establishes the responsible guard: with a hidden root and visible
logical destination (113,103), CE/Zombieland return true while Total Fog returns
false. The other four shot-line controls retain their expected native results.
Initial failure receipts are retained rather than rewritten as successful proof.

Candidate 2f15cad01366deb29929a62daaf36af80a1344d0d907967c5135e91b49eb2a83
checks CE's selected ShootLine.Dest for shot and still-Thing fallback guards.
The existing map/faction/crew/initialization policies remain, as do false native
results and blind-cell orders. No Zombieland type dependency, optional API,
target search, per-tick state or additional visibility query is introduced.
Four source-linked regression cases cover matching and differing sight. Both
root/destination disagreement cases fail before the fix and pass afterward.
All 295 tests
pass through the quiet canonical deploy.

With companion 71f9507d... and unchanged Zombieland 56daa1f2..., all five actual
CE shot-line states pass after a fresh Steam startup. Ordinary Normal playback
then fires at the visible body with the root hidden: shared health drops from
3995 to 3975 and the linked host's injury sum remains two. The real sight bell
keeps five HP and one of five body cells visible. The raider subsequently moves
away on a native Steal job, which the fixture does not suppress; this proves
actual firing/damage, not a sustained fight or active-fight save/rejoin.

The same combined loadout replays the unchanged saved M240B visible-alternate
burst (176 to 170 rounds, last shot 13482, hidden primary still rejected) and
hidden-alternate negative control (177 rounds and last shot 13440 unchanged).
A fresh visible burst starts with four shots, loses its real sight bell, then
finishes six native suppressive fallbacks at its last known cell. All six retain
native=true/final=true, HasThing=false and faction sight=false; it ends at 170
rounds without hidden acquisition. Trace removal, owned-object cleanup,
unchanged-base reload and verified process termination pass. Eleven receipt
assertions and the clean recognized-error log are retained with the exact DLL
pair in artifacts/ce-zombieland-native. Current candidate performance validation
is separate; older candidate performance and preliminary ZIP evidence do not
establish these new bytes. No new Mortal message or ZIP is justified by this
CE-specific fix alone.

The exact 2f15cad0... gameplay subsequently completes the existing standalone
CE 350x350/410-pawn comparison in six fresh alternating processes, ordinary
Ultrafast selection with actual multiplier 15, forced speed/UltraSpeedBoost off
and DPA off. Original/candidate pair TPS values are 460.41/456.01,
452.75/458.53 and 477.13/449.81. Candidate median 456.0069 is below original
460.4127 by 0.9569%, so the canonical strict performance floor fails. Median
tick elapsed is 1.57487 versus 1.56362 ms, and frame p95 70.2669 versus
68.6353 ms. The identity/settings/camera/fixture/native-log guards all pass;
the failure is retained in artifacts/comparison-ce-logical-cell-player-350.json.
The workflow restores the exact candidate and stops all game processes. A
preceding wrong-save-name preflight fails before any measurement and is kept
separately. This is a correctness-tested source candidate, not performance
acceptance or a superseding Mortal ZIP. The comparison does not establish the
cause of the measured slowdown; profile before changing code or repeating it.

### Current-candidate CE tick profiling

Two eight-second DPA captures use the unchanged CE 350x350 fixture and the
same 2f15cad0... gameplay. DPA resolves all thirteen initial targets. The
latest 2,000 captured tick entries average 1.55510 ms for DoSingleTick,
0.16853 ms for CompFog.CompTick, 0.11007 ms for CompSightSource.CompTick,
0.09385 ms for UpdateFoV and 0.01780 ms for CompVisibility.CompTick. The CE
guards resolve but produce no snapshot rows; this does not establish whether
they executed, and does not attribute the failed TPS floor to those guards.

The deeper capture resolves all twelve targets and reports inclusive averages
of 0.07073 ms for FieldOfView.ComputeMask and 0.02710 ms for
VisibilityMask.ApplyDifference, with about 1.93 calls per tick each.
CompCellRegistration.CompTick averages 0.01301 ms. Nested instrumentation adds
overhead, so averages from the two captures are not a before/after comparison.
The initial deep-target request mistakenly names CompCellTracker, resolves only
eleven targets, and is cleaned up before retrying with CompCellRegistration.
Only the complete captures are used as profiling evidence.

These diagnostic captures use the existing dpa_playback forced/debug mode,
with actual native tick multiplier 150. They do not establish ordinary player
fourth-speed performance or explain a 0.96% whole-game difference. Their
purpose is to select actual fog hot paths for a bounded optimization.
Receipts: artifacts/ce-dpa-logical-cell-tick.json and
artifacts/ce-dpa-logical-cell-deep-tick.json. Native DPA cleanup is observed
not profiling/not patched before verified process termination.

The canonical dpa-setup is now configuration-only, supports on/off and selects
the existing isolated profile through TOTALFOG_GAME_ID. Enabling DPA must not
redeploy the tracked last-release DLL over the installed development candidate.
The CE on/capture/cleanup/stop/off sequence retains the exact installed
2f15cad0... gameplay and 71f9507d... companion. The final ten-mod active order
excludes DPA. The original configuration XML byte hash was not recorded, so
only active-order and installed-binary preservation are established. See
artifacts/ce-dpa-logical-cell-cleanup.json. The README's command table is
updated within the user's otherwise uncommitted rewrite.

### Exact row-boundary candidate: geometry accepted, native performance open

The next bounded change hoists mask-caster floor-center slope checks from each
cell to the two exact rounded row endpoints. A wall-to-floor transition moves
the starting slope behind that floor, leaving the remaining interior floor
centers inside the interval. Wall/radius/map-edge handling and recursive shadow
transitions remain unchanged. Callback casting and point queries retain their
existing implementations as differential references. No scheduling, cache,
visibility delay or public API change is added.

The canonical build passes 296 tests. The new exhaustive contract compares
all 512 three-by-three blocker arrangements, nine observer cells and four
radii against callback casting, totaling 18,432 masks. Existing larger
randomized/edge/peek contracts and warm allocation tests also pass.

Gameplay ddb5c8fe3df8714d2bffa7d80723fb7e65b5bb2444a3b24692afe05958a8bd84
and companion 71f9507d... begin the ordinary CE 350x350 six-process comparison.
The first original/candidate pair completes, but the next candidate process
exits during startup, before its bridge connects or fixture loads. The native
Player.log records a SIGBUS with DefDatabase.ResolveAllReferences in a
GenThreading.ParallelForEach worker. The associated forked crash-report child
also aborts. This identifies the failing startup phase; it does not establish
the underlying cause or attribute it to the mask-loop change.

The incomplete run and native crash receipts are preserved under
artifacts/ce-row-center-startup-failure, including both completed sample files.
No six-process median or performance acceptance is available. The workflow
restores the exact candidate and GABS confirms all game processes stopped.
The previous 2f15cad0... failed floor remains separate. This source candidate
is not a new feedback package; a complete comparison and affected native
geometry checks remain required before accepting it for delivery.

The subsequent fresh Steam startup and unchanged CE stress-save load succeed.
A disposable companion compares the exact 846e7c4 pre-change caster with the
ddb5c8fe... candidate in the same paused native Mono process. It alternates five
warmed batches for each workload and verifies 512 masks/4,096 point queries,
plus full footprints at each measured origin. All geometries match.

| Mask workload | Prior median ms | Candidate median ms | Candidate casting cost |
|---|---:|---:|---:|
| Open terrain, 5,000 casts | 193.1490 | 176.1479 | 8.80% lower |
| 3% blockers, 5,000 casts | 186.5053 | 178.6518 | 4.21% lower |
| 15% blockers, 5,000 casts | 27.4182 | 26.5526 | 3.16% lower |
| 35% blockers, 5,000 casts | 6.7311 | 6.6378 | 1.39% lower |
| Late row blockers, 5,000 casts | 161.5012 | 147.6114 | 8.60% lower |
| Corridor, 5,000 casts | 20.3641 | 20.1911 | 0.85% lower |
| Map edges, 4,998 casts | 84.8485 | 78.1000 | 7.95% lower |
| Stress-map blockers/53 colonist origins, 4,982 casts | 156.8877 | 144.7041 | 7.77% lower |

Point-query code is unchanged; its small mixed timing differences are control
noise, not a claimed point-query improvement. The native allocation counter
returns zero, but its validity is not established, so these rows do not prove
native zero allocation. These are paused casting batches, not whole-game TPS.
Evidence: artifacts/row-center-native-kernel.json, with exact sources/binaries
and clean Player.log retained in artifacts/row-center-native. DPA is absent and
unpatched, process termination is verified, and the temporary project includes
are removed before the following ordinary-player comparison. See
artifacts/row-center-native-cleanup.json.

## Current candidate Zombieland performance gate, 2026-10-06

The unchanged ddb5c8fe... Total Fog candidate and 56daa1f2... Zombieland DLL
complete the existing three-pair ordinary fourth-speed comparison on
`TotalFog_Zombieland_UpstreamQuietGap1000`. Original/candidate TPS is
203.4972/214.0356, 198.4477/219.2105 and 209.8320/212.1995; the order alternates
original/candidate, candidate/original, original/candidate in fresh processes.
The candidate median is 214.0356 versus 203.4972 TPS, 5.18% higher. Median
per-run frame p95 is 77.8705 versus 76.3189 ms, so this does not establish a
frame-tail improvement.

All six runs start with 1,000 zombies and finish with 999..1,000. Every measured
tick uses native multiplier 15 with selected Ultrafast, forced speed and the
private speed boost disabled. DPA and zombie work profiling are absent. Save,
settings, both gameplay binaries, engine, camera and sparse contamination
input match. The camera uses the existing native root size 60 and covers
25,953 cells; this is the regular fixture, not another wide-view test.
All six recognized native log summaries are empty, processes are stopped,
the fixture is unchanged and the candidate remains deployed. Evidence is
`artifacts/comparison-release-row-center-zombieland-1000.json` with its six
native reports and logs. This passes that fixture's performance gate only.
The earlier wide CE failure remains retained, and the delivered tester
package and its native presentation gates still identify their earlier bytes.

## Small wide-view sample, 2026-10-06

The completed normal-view CE comparison uses the same ddb5c8fe... gameplay
bytes and 350x350/410-pawn save. Three alternating original/candidate pairs
measure 458.0445/502.0067, 398.3858/225.6061 and 223.8727/421.1327 TPS.
The median comparison passes at 398.3858/421.1327 TPS, but the large variation
prevents attributing a stable 5.71% whole-game gain to the casting change.
That view covers 4,539 cells. Evidence is
`artifacts/comparison-ce-row-center-player-350-clean.json`; all six logs are clean.

At Andreas' request, wide-view coverage is limited to two one-pair spot checks
and paused visual inspection. The companion uses the bridge's session-only
zoom extension at root size 100, verifies the actual camera rectangle, and
restores the prior camera/extension. Screenshots happen before timing. Neither
one-pair result can satisfy the existing three-pair delivery gate.

| Wide case | Map cells in camera rectangle | Original / candidate TPS | Original / candidate frame p95 ms |
|---|---:|---:|---:|
| CE colony, 350x350, 410 pawns | 71,050; 225 pawn root cells | 411.3143 / 402.0530 | 79.7915 / 85.3077 |
| Zombieland with 4,000 sparse contamination cells, 250x250 | 50,750; 874 pawn root cells, including 815 zombie root cells | 38.8981 / 39.7862 | 98.3831 / 91.0861 |

The CE pair measures 2.25% lower TPS and a worse frame tail. It is retained as
a failed spot check, without repeating until green. Every measured native
tick retains ordinary Ultrafast multiplier 15, with debug overrides and DPA off.
Its wide rectangle covers about 58% of the map, nearly sixteen times the
normal-view area; this is not a whole-map claim. Root-cell counts describe
objects within the camera rectangle, not objects actually drawn through fog.

The Zombieland pair uses the older `TotalFog_Zombieland_Upstream1000` fixture,
not the stable `UpstreamQuietGap1000` used by the earlier overlay floor.
Both variants start the measured interval with 1,000 zombies but finish with
77 and 68. Actual native multipliers are mostly 1 despite selected Ultrafast.
The candidate's 2.28% higher TPS is a result for this changing, rate-limited
workload, not evidence of equal zombie work or sustained nominal fourth speed.
The prior initial-grace cleanup investigation remains separate; this interval
does not instrument removals to establish their cause. There is no retry or
new acceptance claim from this pair.

Paused wide screenshots show the candidate clipping the sparse contamination
overlay to current sight, while the original shows the staged rectangle across
hidden terrain. The policies draw different geometry, so this is a whole-game
spot check, not an equal-geometry GPU comparison. The images accept only this
paused overlay presentation, not wide Symbiant or every label/effect behavior.

Both pairs complete with matching fixture/configuration/render identities and
clean native logs. The runner stops each game and restores the candidate.
Evidence is `artifacts/comparison-{ce-wide-view,zombieland-wide-overlay}-spot-check.json`
and the corresponding runtime reports/logs. Exact candidate gameplay bytes,
the Zombieland wide companion, both inspected screenshots and stopped-process
evidence are retained in `artifacts/wide-view-spot-check/manifest.json`.
Current gameplay SHA-256 remains ddb5c8fe..., with Zombieland 56daa1f2....
The normal-view median pass does not resolve the wide CE failure; no new
feedback ZIP is accepted from these measurements.

### Bounded wide-view rendering diagnosis

One eight-second DPA Update capture uses the unchanged CE stress save and
ddb5c8fe... gameplay at the same 100-root-size camera rectangle, covering
71,050 map cells and 225 pawn roots. All nine requested methods resolve.
The snapshot contains 131 DPA entries, including paused setup/tail entries;
the playback probe records 111 frame intervals. These denominators differ.

Inclusive mean cost per DPA entry is 9.23110 ms for native MapUpdate,
2.55167 ms for DrawDynamicThings, 1.48909 ms for DrawMapMesh and 0.52430 ms
for ComputeCulledThings. Total Fog's culling postfix averages 0.06853 ms;
custom-render lookup averages 0.00354 ms. SectionLayerFog.Regenerate averages
0.52860 ms at 37.11 calls per entry, and DrawLayer 0.21939 ms at 273 calls per
entry. ThingVisibility.IsVisible averages 0.93155 ms across 4,681.44 calls per
entry; this includes simulation and interface callers, not only rendering.
Nested inclusive timings must not be added or treated as uninstrumented cost.

The bridge's existing UltraSpeedBoost is enabled in this fresh diagnostic
process. Despite forceRequestedSpeed=false, every captured tick has native
multiplier 150. This is explicitly diagnostic evidence, not ordinary fourth
speed, an original/candidate comparison or an explanation of the 2.25% TPS
difference. It supplies no reason to add culling caches or defer visibility
work. No additional wide acceptance pair or production change follows.

The camera and zoom extension are restored, DPA is observed stopped/unpatched,
and process 66536 termination is verified. Removing DPA restores the exact
pre-capture ModsConfig.xml bytes. Installed gameplay/companion bytes and the
save hash remain unchanged, and the retained Player.log has an empty native
error summary. Receipts and checks are in `artifacts/ce-wide-diagnostic`.

## Combined CE turret and ordinary zombie, 2026-10-06

Source review corrects the proposed Symbiant turret expectation. Zombieland's
`AttackTargetFinder_BestAttackTarget_Patch` rejects Symbiants for non-pawn
searchers, and `ZombieSymbiantCombat.IsPermittedHostileAttacker` excludes
turrets. Do not introduce positive automatic turret acquisition merely to make
Total Fog's root-cell guard accept a logical body cell. Preserving that native
exclusion is now preserved by the bounded combined native control below;
source evidence alone did not complete it.

Existing native fixtures are sufficient to check an ordinary zombie instead.
A player CE mini-turret has a native 48-cell weapon and 34-cell sight range.
The staged target starts 42 cells east and moves through ordinary Stumble AI.
After native power settles, the hidden target remains unacquirable, receives
no shots and leaves the full 100-round magazine unchanged. A real security bell
reveals it. During 26 Normal ticks the turret acquires the actual zombie and
enters warmup with 52 ticks left. Removing the bell immediately removes sight;
121 further Normal ticks clear the turret target, retain 100 rounds and leave
lastShotTick at its never-fired sentinel. Renewed bell sight permits 20 native
shots over 603 Normal ticks. No zombie injury is recorded in that interval,
which does not by itself identify a compatibility defect.

A nearby positive control likewise initially fires without a recorded injury.
The existing bounded CE turret trace is extended to observe the installed base
`ProjectileCE.Impact(Thing)` and `Thing.TakeDamage(DamageInfo)`, filtered to the
fixture turret's launcher/instigator. It records actual results without changing
them. Impact, damage and burst-fallback streams each have a 128-record ceiling;
all diagnostic patches are removed through the same owner on trace-stop/cleanup.
These hooks are companion-only and excluded from player ZIPs and TPS acceptance.

The fresh traced control uses an ordinary zombie initially 12 cells from the
turret. After 422 Normal power-settle ticks with hold-fire enabled, a 721-tick
Normal interval fires 20 rounds. Eighteen observed base impacts include two
zombie hits, terrain/plants and misses. Two matching damage results each deal
seven damage to the zombie; the final injury sum is seven, so the sum is not
used as a substitute for cumulative damage results. The zombie remains alive,
continues native Stumble movement and stays visible to the turret faction.
The camera rectangle is x68..156/z94..144 while the turret/zombie are near
x79..93/z6..7, so native off-camera damage works in this control. This does not
establish zoomed-out FPS or explain the earlier scene's uninjured shot window.

The temporary existing Zombieland no-cleanup test mode suppresses only initial
grace, zero-threat and scheduled zombie-free removal during these controls.
Its exact previous settings timeline is restored before the unchanged base
reload. Owned objects and traces are removed, GABS verifies native termination,
and the retained trace-process log has an empty recognized error summary.
All 296 independent tests pass. TF gameplay remains
ddb5c8fe3df8714d2bffa7d80723fb7e65b5bb2444a3b24692afe05958a8bd84;
Zombieland remains 56daa1f2.... Trace companion SHA-256 is
00ff91d612e1b0c9d299dac8b4d54c51298bec96bd9cd2801c79d8110f247f05.
Exact paired bytes, receipts, Player.log and assertions are retained in
`artifacts/ce-zombie-turret/summary.json` and that folder's source reports.
This accepts only the ordinary player CE mini-turret acquisition/warmup/damage
slice, not all special zombies, weapons, save/load or combined performance.

### Ordinary zombie/turret active-fight save and fresh restart

The same ddb5c8fe.../56daa1f2... gameplay pair and 00ff91d6... companion now
complete one bounded native persistence check. CE's installed ExposeData
explicitly serializes warmup, target, hold fire, cooldown and its owned gun.
The existing fixture stages player mini-turret 75692 and ordinary zombie 75703
nearby. The zombie continues its native Stumble job. Population-cleanup test
mode suppresses only initial-grace, ordinary and scheduled removal during
playback; it is restored before saving and again after resumed playback.

After 420 Normal settling ticks and 720 Normal combat ticks, the magazine is
70, the current target is the living visible zombie, and warmup is 18 ticks.
The bounded native trace records a zombie damage result of 13.7573338 at tick
851, but the native injury list is empty by save time. This is not a nonzero
injury persistence check. Traces are stopped before writing the new isolated
save, `TotalFog_CEZL_OrdinaryTurret_SaveReload`; saved game tick is 1184 and
the exact XML also records warmup 18 and Thing_Zombie75703 as target.

Process 67205 is stopped and process 67598 loads that save with the temporary
Pause on load preference. The preference is restored without writing it.
At paused native tick 1185, the same target/factions/positions/job, sight 34,
native range 48, power, hold-fire state, ammunition, magazine 70 and
last-shot tick 1065 remain. Warmup is 17, exactly accounting for the one
native load tick. Transient aim flags are recomputed, not asserted identical.
Another 719 unforced Normal ticks consume 38 rounds, reaching magazine 32
with the same visible living target. The trace has 35 actual base projectile
impacts, including cover and misses, but no additional zombie damage event.
This proves resumed native firing, not guaranteed hits or further damage.

The trace remains below its 128-record caps and is stopped. Original cleanup
settings are restored, owned objects are removed, the unchanged base is
reloaded and the second process terminates. Installed gameplay/companion bytes,
ModsConfig.xml and both the base and prepared-fight saves remain unchanged.
Both retained native logs have empty recognized error summaries. Exact
receipts, saved XML, native logs and assertions are in
`artifacts/ce-zombie-save`. M240/mortar/special-zombie persistence, nonzero
injury retention, broader combat and performance remain separate checks.

### Native Symbiant turret exclusion and ordinary-zombie positive control

The unchanged ddb5c8fe.../56daa1f2... gameplay pair and 00ff91d6... companion
complete two short Normal-playback controls in one fresh process. The powered
player CE mini-turret is at (79,6), has native range 48 and current sight 34,
and initially holds fire while its power settles for 420 ticks. The generated
human target is removed before firing. A controlled hostless Symbiant is staged
12 cells east with four logical body cells. Its default native tick, rendering,
host sync, path cost and benefit flags remain enabled. It stays alive, visible,
not downed and in its native Symbiant job.

With hold fire disabled, 238 unforced Normal ticks leave the turret without a
current target or warmup, its magazine at 100 and lastShotTick at the never-fired
sentinel. The existing fixture field nativeAcquirable is true, but that field
checks only CE's IsValidTarget. It does not establish the result of automatic
acquisition, which also applies Zombieland's targeting policy. This control
preserves the deliberate Symbiant exclusion.

The Symbiant is cleaned up and an ordinary zombie is spawned at the same starting
cell. Without changing the powered turret, another 241 unforced Normal ticks
acquire that zombie, consume ten rounds and record lastShotTick 859. The zombie
remains alive, visible and in its native Stumble job at (92,7). Its final injury
sum is zero. This is a positive acquisition/firing control, not proof of a hit
or damage. Neither interval enables the diagnostic burst/impact/damage trace.
No rendering claim follows from this off-camera targeting test, and it does
not establish other CE weapon paths or linked-host behavior.

The temporary population-cleanup override is disabled and its original grace,
scheduled events and zero-threat removal settings are restored. Owned objects
are removed, the unchanged base is reloaded, and GABS verifies process 68992
has terminated. Installed gameplay/companion bytes, the base save and
ModsConfig.xml match the preflight hashes. The retained native log has an empty
recognized error summary. Exact native receipts, source findings, byte guards
and assertions are in `artifacts/ce-special-targets`. No production change or
new performance acceptance follows from these controls.

### Native CE electrifier exclusion and EMP state transition

The same ddb5c8fe.../56daa1f2... gameplay pair and 00ff91d6... companion pass
one bounded visible-electrifier control in process 70375. The existing player
mini-turret fixture uses native range 48 and current sight 34. After 419 Normal
power-settle ticks with hold fire enabled, its human target is removed and an
electrifier is spawned 12 cells east. With hold fire disabled, 241 unforced
Normal ticks leave its magazine at 100, no acquired target or warmup, and the
never-fired lastShotTick sentinel. The zombie remains alive, visible, not downed
and in native Stumble AI. CE's basic IsValidTarget accepts it, which again does
not establish the full automatic target scan.

The existing EMP tool applies actual DamageInfo EMP input 10 to that same
electrifier. It observes electricDisabledUntil changing from 0 to 1282 at native
tick 682, active electric true before and false after, and zero injury damage.
The disabling path is Zombieland's native damage notification patch. This does
not test delivery by a CE EMP projectile. Over another 241 unforced Normal ticks,
the unchanged turret acquires the same zombie and consumes ten rounds, with
lastShotTick 861 and the disabling interval still active. Its final injury sum
is zero, so this proves native acquisition/firing rather than a projectile hit.
No diagnostic trace or DPA instrumentation is enabled.

A subsequent player-command attempt uses an ordinary unconfused zombie and
correctly produces no rope option. GetRopableZombie requires IsConfused, so that
setup does not establish roped-target acquisition or identify a defect. The
roped/confused check remains open rather than forcing an ineligible menu entry.

The population-cleanup test mode is restored to its original settings, owned
zombies/colonist/turret/power are removed, selection is cleared, the unchanged
base is reloaded and native process termination is verified. Gameplay/companion
bytes, base save and ModsConfig.xml remain unchanged; the retained Player.log has
an empty recognized error summary. Native receipts, assertions and hashes are in
`artifacts/ce-electric-targets`. Reactivation, other weapons, CE EMP delivery,
save/load and combined combat performance remain separate checks. No production
change follows from this bounded pass.

## Native Multiplayer bridge controls, 2026-10-06

`./scripts/mod mp-setup` creates isolated Steam-managed host/client profiles with
separate savedata, bridge endpoints and tracked PIDs. Their loadout is Prepatcher,
Harmony, RimBridgeServer, Core/all five DLCs, Multiplayer and Total Fog. Native
engine: 1.6.4871 rev597. Multiplayer: 0.11.5+4a3be27-dirty, MVID
34169ac4-d99e-463c-b48c-0af3be9783f8. Total Fog remains
ddb5c8fe3df8714d2bffa7d80723fb7e65b5bb2444a3b24692afe05958a8bd84.

RimBridgeServer's optional `Companions/Multiplayer` has no hard Multiplayer or
Total Fog assembly reference. Its sole DLL deploys into sibling global
`BridgeTools/Multiplayer`. Native host, local join, status, leave and synchronized
shared-time commands run on the game thread. Native `hostReady` requires a
running local server and a completed initial data snapshot before joining.
Initiation/submission receipts do not establish completed player states.

The first tracing-enabled run joins both players but fails in Multiplayer's
Arm64 deferred stack tracer, with `Unknown function header` from its Rand hook
and subsequent null references. Disabling native diagnostic stack capture
removes that tracer failure in subsequent controls; native desync detection
remains available. An early join also fails in native disconnect-packet
serialization while hosting is still preparing. Both failed runs are retained.

Native cleanup reapplies preferences, restoring `runInBackground=False` and
stalling an unfocused game's bridge. RimBridgeServer now preserves its existing
runtime background execution contract after preference refreshes, without
changing the saved preference. A native check verifies client leave to Entry/no
session, rejoin as a new player without process restart, and host leave to
Entry/no session. An unexpected host disconnection also leaves the client bridge
responsive. The leave tool handles a native disconnected window and preserves
an unrelated idle single-player game.

The final control starts both players Playing at tick 1215. A host's native
synchronized Normal command advances simulation, and a client's synchronized
pause leaves both at tick 3004, desired speed Paused, both players Playing and
both `desynced=False`. Those 1,789 ticks prove time-command propagation and a
short connection check. They do not compare fog masks or serialized gameplay
state, or validate settings, targeting, save/resync and faction visibility.
Both leave commands subsequently reach Entry/no session. Opening the native
ServerBrowser through the repaired window-type lookup also succeeds.

The reflection-only `UnityEngine.InputLegacyModule` dependency exception still
occurs during native loading; its owner is not isolated. Destroyed-thing
deep-save warnings also occur during simulation. These are not clean-log
compatibility acceptance. Full Multiplayer compatibility remains open, with no
Total Fog gameplay fix or tester delivery from this tooling work.

Receipts/logs are in `artifacts/multiplayer-startup`, including
`background-cleanup-receipts.json`, `native-time-control-receipts.json`, failed
tracing/readiness logs and exact installed hashes. Final native tooling hashes:
RimBridgeServer aad042e56a1c7127019107c32f7812828d903e2ae851cc5c200a09343f89a32a;
Multiplayer companion 994389846f773fb35182a363028e43f17f1065ae450ec31b4f15326dc29caab9.
The existing 202 RimBridgeServer tests pass. Deployment compares installed/ZIP
DLLs with build bytes and excludes test companions from player ZIPs.

After each pair launch, `./scripts/mod mp-layout` identifies exact savedata/PIDs,
arranges host-left/client-right half-screen windows on the main display and
verifies both rectangles. Layout is presentation, not determinism evidence.


## Native Multiplayer fog state and settings checkpoint, 2026-10-06

Public RimWorld 1.6.4871 rev597, Multiplayer 0.11.5+4a3be27-dirty,
Prepatcher, Harmony, all five DLCs and Total Fog run in separate Steam processes.
The host/client profiles use independent save-data folders and bridge endpoints.
Shared-colony faction 16, synchronous time, loopback hosting, no arbiter and
desync tracing disabled are the exact tested configuration.

The pre-format and formatted gameplay DLL both hash
`ddb5c8fe3df8714d2bffa7d80723fb7e65b5bb2444a3b24692afe05958a8bd84`.
CSharpier validated C# syntax equivalence, Python ASTs match before/after, and
296 existing tests pass. The formatting-only commit is b066822 and is listed
in .git-blame-ignore-revs. Frozen historical payloads and Originals/ are untouched.

The read-only multiplayer_snapshot tool does not call state-creating
GetVisibility/Counts accessors. It reads native state on the game thread, hashes
coverage counts, known cells, wall/tree blocker masks and thing observation flags,
and reports source state/order and pending refreshes.

- Initial join: both tick 1218, all compared state matches.
- Ordinary Superfast playback: both paused at tick 1820, 602 ticks later; state matches.
- Native client leave and rejoin: both tick 1821; state matches after native replay.
- These controls use the unchanged ddb5c8fe gameplay DLL. Receipts are
  artifacts/multiplayer-startup/fog-baseline-{snapshots,played,rejoin}.json.

The settings/cleanup gameplay candidate hashes
`402b52dac1bc9fd9775697d6ff025924fc42801ed4b90cfd63ed7c8e1b7ce455`.
The optional MPAPI adapter binds buffered settings watchers once, without a
Multiplayer assembly reference. The unit binding test covers the absent-mod
fallback, stable field order, buffering, inactive mode and cleanup after a watch
throws. The canonical build passes 297 tests.

- Client submits BaseViewRange 60 to 10 through the same watcher used by the UI.
  The immediate value stays 60 until MP applies its synchronized command.
- Both pause at tick 1593 with range 10, 1,183 visible cells, and identical complete
  fog snapshots including source deadlines.
- Host writes the actual mod settings file at paused tick 1593. The complete
  before/after fog snapshot is unchanged.
- Host restores range 60; client submits SilentRaids=true. After ordinary
  Superfast playback both pause at tick 2629, range 60, SilentRaids=true,
  15,538 visible cells and identical complete fog snapshots. Neither reports
  a desync. This candidate interval spans 1,411 ticks, including natural movement.
- Owned test changes are restored through MP commands to range 60 and SilentRaids=false.
- One preliminary range-10 capture was made while the pause command was still
  propagating: host tick 1589 and client tick 1593. It is retained separately
  and is not a valid same-tick comparison. The subsequent matched capture is
  fog-candidate-range10-matched.json; range-60 evidence is fog-candidate-range60.json.

The load/rejoin logs still contain the reflection-only UnityEngine.InputLegacyModule
resolution exception, without an identifying managed stack. The native Arm64
stack-tracing failure matches upstream Multiplayer issue 944 and is excluded
by the explicit diagnostic setting. These are not clean-log compatibility passes.
The full saved-session/replay, dedicated combat, blockers/transfers, separate
factions, independent map clocks, deferred notifications and combined CE gates
remain outstanding. No compatibility completion is inferred from these controls.

## Multiplayer saved settings and independent views, 2026-10-06

The cold-load control found a real Total Fog defect in candidate 402b52da.
The live session saved at tick 3336 with BaseViewRange=10. A fresh process
loaded that native archive with its local default of 60, publishing much wider
coverage. MPAPI field registration synchronizes live changes but does not itself
persist static mod fields in the game snapshot. The one-tick advance made by
native replay loading is not a desync claim; the differing setting is the defect.
The failure is retained in fog-coldsave-failure.json.

MultiplayerSettingsGameComponent now saves shared primitive settings and the
startup tree-blocking policy only while Multiplayer is active. It restores them
in LoadingVars before map initialization. Ordinary single-player saves do not
gain a shared-settings snapshot. Numbers round-trip with invariant culture;
registration uses ordinal name order. The canonical build passes 298 tests,
including exact floating-point settings restored across French/English cultures,
unknown future keys and the existing optional-binding controls. There is no new
per-tick work or hard Multiplayer reference.

The first fixed gameplay DLL hashes
`f818f0e42be22097cb0b468c9d076185afb79ef57c33b3a52050a2834e7a86a1`.
Both live clients match completely at paused tick 1559 with range 10 before saving
TotalFog_MP_20261006_C through native Autosaving. The archive passes ZIP CRC
validation and contains the shared-settings game component. Both local preference
files omit the range field, retaining its default of 60. Independently restarted
host and client load identical archive bytes to paused replay tick 1560 with
range 10 and identical complete fog snapshots, including transient schedules:
375 sources, 1,206 visible cells and 12,163 known cells. Evidence is in
fog-fixed-coldsave-{source,result,pair}.json. Native loading advances the saved
endpoint by one tick, so the pre-save and post-load snapshots are not treated
as a same-tick comparison.

The final ordinal-order gameplay candidate hashes
`4221b0d9198d22c3a6c9680b41bd2abd022990f75a7e0e5f049e9efb8572239f`.
A fresh host loads the same saved archive, resumes hosting through native
HostUtil.HostServer(settings, fromReplay: true), and a fresh client joins with
local default preferences. Their complete snapshots match at tick 1561 with
range 10 and 375 sources. The updated companion follows native HostWindow's
server initialization/replay-host path; it does not write MP session state.
The replay-host companion hashes 090a041d8c7a5656273536b6ba1ce4cc27966f2c5d956af1dd131b1478a64df3;
the deployed RimBridgeServer main DLL hashes
fdd46a996f683a815e7413739775bf40cac89263a35339c2458268b971ee2f16.
Native public game/Multiplayer identities and the shared synchronous loadout
remain those recorded above. Evidence is fog-fixed-resumed-join.json.

One bounded independent-camera control follows. The host stays at cell 137,125
with root size 12 and a 25x27 view. The client uses cell 200,50, root size 100
and a 187x203 view through the session-only bridge extension. Native synchronized
Superfast playback and a client pause advance both from 1561 to 2886, 1,325 ticks.
Their complete fog snapshots still match, including 374 surviving sources,
1,250 visible cells, discovery, observations, blockers and schedules. Neither
reports a native desync. This is a camera-independence control, not an FPS/TPS
benchmark or proof of every interface interaction. Evidence is
fog-fixed-different-cameras.json.

The recurring reflection-only InputLegacyModule loading exception remains;
these results do not establish clean-log acceptance. Dedicated ordered combat,
blocker edits/transfers, deferred notification replay, separate factions,
independent map clocks and combined CE remain open. Cold snapshot reconstruction
and resumed hosting close only the corresponding part of the save/rejoin target.
The deployment guard now rejects running games using the selected physical Mods
folder, while preserving another task's independent offline game/mod folder.
The pair layout verifies after fresh pair launch; restarting only one duplicate
app can temporarily leave the older process without AX-exposed windows. That
tooling failure is retained separately from fog evidence.

The bounded manual combat control uses the same 4221b0d9 gameplay bytes.
Native SyncMethods.Init registers Pawn_DraftController.Drafted and
Pawn_JobTracker.TryTakeOrderedJob; the bridge's ordinary draft and map-click
paths therefore submit native synchronized commands. Keno's host-side draft
submission initially reads false, then both clients report drafted=true.
At range 10, a right-click on hidden Toughspike18314 at 128,121 leaves no attack
menu; visible Trispike18077 at 120,108 offers an enabled "Melee attack trispike"
option. The hidden-cell click can still issue the ordinary move order; lack of
an attack menu is not described as a no-op. The visible melee option is then
executed and ordinary Normal playback completes.

Both clients pause at tick 5689 with identical complete fog snapshots and no
native desync report. The native saved world battle log records Keno/Human393
as initiator of a MeleeHit and the target's Transition_Died at absolute tick
321777, with Trispike18077 as recipient/subject. The first observer-only job
read missed the transient attack job, so battle-log attribution supplies the
actual hit/death evidence. The new multiplayer_pawns companion query reads
jobs, targets, health, equipment and fog visibility by ThingID without relying
on an open inspector, and also resolves corpse inner pawns when they remain.
It changes no gameplay DLL bytes. After cold-load/resumed hosting of the native
post-combat save, both clients match at paused tick 5691: Keno is healthy and
drafted with the same Wait_Combat job/id, the killed trispike is absent, and the
hidden toughspike has identical health, position and job. Evidence is
fog-native-combat-control.json, combat-log-D.xml and
fog-native-combat-pawns-rejoin.json. This is one vanilla manual-melee/control
case; ranged weapons, AI fog targeting, turrets, boundary loss during warmup
and the combined CE Multiplayer matrix remain unverified.

## Multiplayer preview and right-click boundaries, 2026-10-06

The reflection-only InputLegacyModule loading exception reproduces in a fresh
native quick game without Total Fog or its companion. It disappears in the
next control without Multiplayer and Prepatcher, while Harmony, RimBridgeServer,
Core and all DLCs remain. Both use the isolated client profile. Exact active
loadouts and Player.log files are retained as no-totalfog-* and no-multiplayer-*
under artifacts/multiplayer-startup. This does not isolate the responsible
function, distinguish the Multiplayer/Prepatcher/bridge interaction, or prove
a player-only loadout is affected. The temporary removals are restored and
verified against the original config hash; no user settings are rolled back.

Four source-linked regression cases demonstrate that a local gravship landing
preview previously grants current sight, persists object observation and makes
hidden thing/cell notifications eligible. Their first failing build is retained
as gravship-preview-regression-red.log. The fixed policy keeps the drawing
preview but requires actual sight for current queries and observation. Queued
notifications and positional sound ignore that temporary drawing exception.
These cases pass in the canonical 302-test build; they are engine-boundary
policy proofs, not native two-client gravship flight acceptance.

On that intermediate gameplay DLL, cdf9a510, Andrews equips the real
bolt-action rifle through a native right-click order. Both clients match fog and
pawn state at paused tick 6786 after normal playback. Drafting then leaves the
same native hidden Toughspike18314 eligible for both Fire at and Melee attack
menu options. The menu and its false current-visibility result are retained in
artifacts/rifle-menu-red.json. Public 1.6 DecompilerServer source identifies
FloatMenuContext's separately owned ClickedThings and ClickedPawns lists,
populated through GenUI.ThingsUnderMouse. Our existing GenUI.TargetsAt filter
does not cover this producer. A constructor postfix now filters both temporary
lists through the same policy, without changing the authoritative map grid.
The source-linked list/visibility test passes in the canonical 303-test build.

The final built/deployed gameplay DLL is SHA-256
88f41bedb0fa610ce14775d0e959c5370ac7588764645ee6946be83704d86fdb.
Both clients reload/resume/join the native post-equip archive at tick 6788 with
range 10 and matching complete fog state. Cold reconstruction applies the
already-completed draft/rifle state, legitimately expanding stationary weapon
sight; the earlier toughspike is now visible. The positive/negative controls
therefore use current visibility, not that old cell's earlier classification.
Hidden Trispike18346 at 148,131 has no attack menu. Visible Toughspike18334 at
155,106 keeps vanilla's disabled Cannot hit target option. Visible Trispike18404
at 123,105 offers an enabled Fire at option, which is executed through the
ordinary UI and native synchronized job path.

After 1,994 unforced Normal ticks, both clients pause at tick 8782 with identical
complete fog and selected pawn state and no reported native desync. The native
post-combat archive passes ZIP CRC; its battle log records Andrews/Human390's
bolt-action rifle fire, impact and the trispike's Gunshot death. The killed target
is absent on both clients and Andrews has the same healthy Wait_Combat state.
Evidence is multiplayer-startup/fog-rifle-{baseline,equip,playback}.json,
rifle-menu-green.json and rifle-battle-log.xml. The existing loading exception
and native gpath assertions remain in both preserved native logs, so this
is bounded manual rifle/determinism evidence, not clean-log release acceptance,
TPS/FPS acceptance, AI/turret targeting, native gravship flight, separate
factions, independent map clocks or combined CE Multiplayer validation.

One real door cycle follows on the same 88f41bed gameplay bytes. A companion-only
read-only cell query reports native building/door state alongside actual fog
blocker, current sight and discovery; it does not create map/grid state or issue
orders. A canonical rebuild passes the same 303 tests and proves gameplay byte
identity. The deployed companion hashes
db5bd03490c1bac18fd0f6f7d2c15f24f95874f8600678fd0cfc85ef5c91d73f.

Both clients resume/join the native post-rifle save at paused tick 8784. The real
Door13612 at 33,124 is closed, cannot be seen over and blocks fog sight; its cell
is not yet discovered. Andrews reaches and stands in that door through native
right-click movement and ordinary Normal playback. At matching paused tick
12870, both report the same healthy pawn at 33,124, native doorOpen=true,
canBeSeenOver=true, blocked=false and known=true. A second native move to 40,124
lets the door close naturally. Both pause at tick 14172 with the same pawn there,
doorOpen=false, canBeSeenOver=false, blocked=true and retained discovery.
Complete fog snapshots match at all three boundaries and native desync flags
remain false. This covers 5,388 Normal ticks and one ordinary door/movement
transition, not every blocker, sight behind the doorway, transfers or async maps.
Evidence is multiplayer-startup/fog-door-{before,open,closed}.json,
door-build-pair.json and both preserved Player.log files. The loading exception
remains, so clean-log acceptance is still open.


## Player-faction separation source checkpoint, 2026-10-07

The earlier key mapped every IsPlayer faction to coverage key zero. Discovery
was one durable array and CompVisibility had one unowned observation boolean.
The new source retains primary-faction direct storage and assigns other player
factions separate coverage/discovery and observer IDs. Original discovery and
observation fields remain readable; legacy observations are assigned to their
map's primary faction, including held things before spawn. Faction IDs persist
with their data, so moving an item to another player's map does not transfer
its observation ownership. Rendering, overlays, minimap and mining queries use
the current observer. Spawn/movement/coverage checks record each observer;
presentation queries themselves do not record observations.

Native Multiplayer's AsyncTimeComp.PreContext owns the map tick/faction/RNG
context. Its FactionExtensions.PushFaction/PopFaction also swap native map/world
data; cached delegates use that mechanism for deferred recipient checks/replay.
Each queued notification now saves its recipient faction. Native non-historical
command feedback can exist on only one client (SilenceMessagesNotTargetedAtMe),
so hidden feedback is dropped rather than serialized in the shared queue.
This notification path still needs native recipient/replay/save controls.

Hearing cues are generated from simulation state on a 100-tick deadline, with
no camera/mote-count rejection. Native Mote consumes random values during
construction, so production must remain deterministic across observers.
MoteBase uses normal ticking and is unsaved; ThingDef.HasThingIDNumber excludes
motes. Each cue is tagged with its observer faction for local drawing. Native
Multiplayer random/map context source is reviewed; actual cue generation under
different native client views remains unverified for this candidate.

The source-linked suite covers private remembered observation, explicit foreign
observation, legacy held observation, transfer to another faction's map, hearing
cue ownership, engine-owned tooltip registration and hidden foreign-prisoner
notifications. The optional-binding test balances the native faction-context
adapter and settings watches. Compilation and tests do not establish native
separate-faction, independent-clock, notification replay or performance acceptance.
The earlier shared-colony runtime remains on its separately recorded bytes until
a fresh pair is deployed. No Multiplayer build or update is sent to Mortal.


The 65c07a54... gameplay / 43426096... companion pair is then deployed and
independently restarted on both profiles. Loading native save G, resuming
hosting and joining reaches matching paused tick 14174 with identical complete
fog snapshots after removing bridge operation metadata. Both have 15,629 known
cells, 5,654 currently visible cells, 9,793 coverage contributions and 300
observed objects. Faction 16 owns that discovery/observation; spectator faction
17 has none. All source schedules also match. Evidence is
artifacts/multiplayer-startup/fog-faction-shared-cold-join.json. This is still
shared-colony acceptance only. Both logs retain the prior InputLegacyModule
loading exception and are not clean-log acceptance.

A subsequent source-linked regression catches an observation before a second
faction's grid exists: changing only the local viewer prevented the primary
observer from learning about a newly spawned visible item. The regression fails
before the fix (primary-observer-regression-red.log). CompVisibility now records
the primary observer explicitly when the local view differs, and the full suite
passes 311 tests. This preserves the common primary-view path without another
sight query. b1cc0a3c... is the next built gameplay candidate and is not covered
by the older 65c07a54 native result. Both remain separate from prior performance
and package acceptance.

### Native separate-colony startup, 2026-10-07

The b1cc0a3c gameplay candidate creates a second faction and map through native
Multiplayer's ideology/pawn wizard. The host views faction 16 and the client
views faction 18 at paused tick 14174. The original map retains identical
per-faction discovery and observations. The new map initially differs: the
host stores no discovered cells, while the client stores 317 around the landing
site. `MapVisibility.Initialize` used `map.IsPlayerHome` and
`ColonistsSpawnedCount`, which depend on the local viewing faction. The retained
red evidence is `artifacts/multiplayer-startup/native-multifaction-created.json`.

Initialization now checks the map's player owner and that owner's spawned
humanlike non-prisoner pawns. The full suite still passes 311 tests and the
formatter gate passes. The 30362595 gameplay / 43426096 Total Fog companion
pair repeats native second-colony creation at the same explicit surface site.
Both clients now store the same 317 discovered cells, and neither other faction
inherits them. Coverage, discovery, observed-object hashes, blockers and source
schedules agree after excluding empty coverage allocations made by local
presentation and the intentionally observer-specific presentation hash. Evidence:
`artifacts/multiplayer-startup/native-multifaction-startup-fixed.json` and
`owner-startup-installed-pair.json`.

The second native pawn wizard also logs a null food need for generated pawn
Waters during native pawn serialization on both clients. Its stack begins in
Hediff_Addiction/Need_Food, with Multiplayer stat/needs patches; no Total Fog
frame appears. This has not been isolated without Total Fog, so ownership is
unverified. The fog startup observation is not clean-log or complete-session
acceptance. Both full logs are retained under multiplayer-startup.

Before that startup fix, 3,852 shared synchronous ticks of the native two-map
session reach paused tick 18026 with matching per-faction discovery, observations,
coverage, blockers and all source schedules. Native faction switching does not
transfer knowledge. This older b1cc0a3c result is retained separately in
`native-multifaction-running-paused.json`; it does not accept the new gameplay
candidate's performance, independent clocks or deferred notifications.

RimBridgeServer's optional Multiplayer companion now exposes the native faction
setup wizard and join-faction packet, plus faction/time-mode status. These are
test controls outside player mod packages. An immediate status read while the
native replay loads also exercises the new world-null guard successfully.

### Separate-colony playback, independent clocks and rejoin, 2026-10-07

The 30362595 gameplay / 43426096 companion pair cold-loads the healthy two-colony
save H. Host faction 16 and client faction 18 retain matching per-faction fog
state and all source schedules after 12,312 shared synchronous ticks, ending at
30340. No additional runtime attention appears; the pre-existing loading
exception remains. Evidence: `native-multifaction-healthy-cold-join.json` and
`native-multifaction-healthy-running.json` under artifacts/multiplayer-startup.

RimBridgeServer companion 2ce67fd7 exposes native per-map time controls and
clock receipts. After cold loading save I, running only map 0 advances its clock
from 30342 to 31866 while map 1 stays at 30342. Then running only map 1 advances
it by 9,378 ticks to 39720 while map 0 stays at 31866. Both clients agree on all
normalized fog state and schedules; the paused map's complete state remains
unchanged during the second phase. World time ends at 41244. Native save J and
cold rejoin reconstruct matching state at world 41246, map 0 31868 and map 1
39722. Evidence: `native-async-host-map-only.json`,
`native-async-client-map-only.json`, `native-async-save-cold-join.json` and
`async-installed-pair.json`. Empty local presentation coverage allocations and
the intentionally observer-specific presentation hash are excluded; full source
lists and per-faction hashes are compared.

### Explicit observer colony exemption, 2026-10-07

A source-linked regression fails when an explicit faction query uses the local
viewer's home-map exemption (`colony-observer-regression-red.log`). The shared
home-map helper now evaluates the explicit player owner while preserving native
home-map eligibility, gravship landing and grav-engine rules. The current local
viewer and non-player contexts retain the native property. Option-off queries
short-circuit before this check. All 315 tests and formatting pass.

Gameplay f3ad33a1 cold-loads/rehosts J with matching native map clocks and complete
normalized fog state. The client changes OnlyOutsideColony through the native
MPAPI watcher while paused; both clients apply it. At an undiscovered corner on
each map, both clients report visible only for that map's owner (16 on map 0,
18 on map 1), independent of their local viewing faction. The host then submits
the option-off change through the same watcher. Evidence:
`native-colony-observer-option.json` and `colony-observer-build-pair.json`.

These checks do not accept deferred queue persistence/replay, all combat and
transfer cases, cross-platform determinism, long-session stability or the latest
gameplay bytes' performance floor. The known MP/Prepatcher InputLegacyModule
loading error still prevents clean-log acceptance; the earlier generated-pawn
needs error is not attributed to Total Fog without an isolation control.

### Native deferred notification recipients and persistence, 2026-10-07

Gameplay f3ad33a1 / Total Fog companion 563e42a4 registers an opt-in native sync
handler on both main menus, ID 761. Existing command IDs are checked unchanged;
registration is explicit test instrumentation and adds no player gameplay work.
The probe sends actual Messages.Message and LetterStack.ReceiveLetter calls
through Multiplayer's synchronized command path, with its native map/faction
context. It never runs a mutation fixture on only one client or steps local ticks.

At paused world 41246 / maps 31868 and 39722, both clients queue the same six
historical alerts: one message and letter per colony, plus a message and letter
on map 1 addressed to faction 16. Letter deadlines are 120 native map ticks
later. A hidden non-historical message is discarded without entering the queue
or archive. Native save K is CRC-verified; both processes restart, reload/rehost
and rejoin. At world 41248 / maps 31870 and 39724, all original queue payloads,
recipients, deadlines and empty recipient archives match the saved state exactly.
DelayAlertsUntilSeen also survives as the host's synchronized session setting.

With OnlyOutsideColony synchronized on, only map 1 runs first. At paused world
41701 / maps 31870 and 40177, faction 18's message/letter appear once in that
faction's native archive on both clients. Map 0's queue stays unchanged, and
map 1's foreign-recipient alerts stay hidden. Running only map 0 next reaches
world 42916 / maps 33086 and 40177: faction 16's own-colony alerts replay once,
while the foreign-recipient pair still remains on map 1. Disabling the delay
through the native watcher and running map 1 reaches paused world 43948 /
maps 33086 and 41209. All queues are empty; the remaining pair appears once
only in faction 16's archive. Faction 18 retains only its own pair, and the
spectator receives none. Both clients' complete probe queues/archives match at
each final paused boundary, with no desync or additional runtime attention.

Evidence under artifacts/multiplayer-startup: `notification-probe-build-pair.json`,
`native-notifications-queued.json`, `native-notifications-cold-rejoin.json`,
`native-notifications-client-reveal.json`, `native-notifications-owner-reveal.json`
and `native-notifications-delivered.json`. These probe saves require the same
opt-in handler for replaying their probe commands. The companion is excluded
from player packages. These checks accept the production queue's bounded
recipient/persistence/replay behavior, not every native incident producer,
destroyed target or map transfer. The known loading exception remains separate.

### Current gameplay rifle control and serialized fog data, 2026-10-07

Gameplay f3ad33a1 / companion 0de2047f cold-loads the post-alert save L. The
read-only pawn probe now accepts an explicit map, so the client can inspect map
0 while continuing to view faction 18's map 1. Host faction 16 uses normal
maximum camera root 60 (113x123 view rectangle); the client uses root 24 on map
1. The rifle's current sight radius is 24. At paused map tick 34303, its target
Trispike18356 is at (65,144), about 28.3 cells from the rifle at (50,120), within
weapon range but hidden. The native rifle targeting command leaves the pawn in
Wait_Combat. With OnlyOutsideColony synchronized on, the same targeting path
can issue the visible control. Ordinary Normal playback advances map 0 by 1,840
ticks to 36143 while map 1 stays paused at 41211; world time reaches 47005.

Native battle entries confirm three BoltActionRifle shots, two misses and then
an impact followed by Transition_Died/Gunshot for Trispike18356. Both clients
report the same remaining actor/job state and removed target, and native battle
entries match across their separately saved world data. Complete normalized fog
state and all source schedules match after the option-off change. No additional
runtime attention/desync appears. This is a wide-camera determinism/player-rifle
sample, not a TPS/FPS, enemy AI or combined CE acceptance.

Both native saves N are CRC-verified. Whole save entries are not byte-identical:
the viewing faction, current map, camera and native faction data differ, and the
raw hashes/XML differences are retained without asserting whole-save equality.
Total Fog's two saved components on each map, all saved observation fields
(1,181 map-0 / 933 map-1 fields) and synchronized session settings match exactly.
The seven native shot/impact/death entries also match. Evidence under
artifacts/multiplayer-startup: `combat-current-build-pair.json`,
`native-current-rifle-control.json`, `current-rifle-battle-log.xml`,
`native-current-serialized-comparison.json`,
`native-current-serialized-differences.json` and
`native-current-totalfog-saved-comparison.json`. The known loading exception
still prevents clean-log acceptance.

### Native Multiplayer hearing cues, 2026-10-07

Gameplay f3ad33a1 / companion 218f7786 cold-loads save N and joins native
Multiplayer with host faction 16 viewing map 0 and client faction 18 viewing map
1. Both maps start paused at 36145/41213 with identical native map RNG states.
The existing colony has moving hidden creatures within the ordinary 10-cell
hearing range. No synthetic cue, pawn mutation or single-client tick stepping
is used.

Ordinary Normal map-0 playback advances 136 ticks to 36281. Both clients report
the same three native Mote_HearingCue objects, including positions, spawn tick
36244, random offsets and non-real-time policy. Map-0 RNG state agrees exactly;
the paused map-1 clock and RNG state remain unchanged. Public cue visibility is
true only for faction 16 on both clients' respective viewing contexts. Motes do
not appear in native ListerThings unless configured for GUI overlays; the
read-only probe was corrected to use DynamicDrawManager.DrawThings before this
accepted sample. The earlier zero-count probe is not cue acceptance.

The client synchronizes ShowHearingCues off. Another 157 ordinary ticks reach
36438, with no active cues and matching native RNG states. The host restores
the option; a client hearing-range change to 30 and host restore to 10 both
apply on both clients while paused. Another 143 ordinary ticks reach 36581 and
produce three matching faction-16 cues again. Complete normalized fog/source
state agrees, both clients report no desync, and no new attention appears.

The full paired receipts and their evidence limits are retained in
artifacts/multiplayer-startup/native-current-hearing-control.json, with the
build pair in hearing-build-pair.json. This is one native production hearing
case with a separate-map viewer, not all faction/held-pawn/transfer cases or a
performance measurement. The known reflection-only loading exception still
prevents clean-log startup acceptance. The added probe is excluded from player
packages and does not change gameplay DLL bytes. All 315 tests and the build
formatter gate pass.

### Current Multiplayer candidate performance floor, 2026-10-07

The f3ad33a1 gameplay DLL is compared with inherited 9d011de0 on the unchanged
TotalFog_Zombieland_UpstreamQuietGap1000 save. The existing canonical runner
alternates three original/candidate pairs in fresh native processes, matches
fixture/settings/camera/loadout/DLL identities and preserves each native log.
These are single-player Zombieland checks of the current candidate; Multiplayer
is not loaded and no Multiplayer throughput claim follows from them.

All six samples retain ordinary selected Ultrafast and native rate multiplier
15, with forced speed and the private speed boost disabled. Original median
TPS is 286.78698; candidate median is 309.07648, +7.77%. Mean native tick CPU
cost medians are 2.46459/2.27179 ms. Frame-time p95 medians are 69.8270/70.3427
ms, slightly higher for the candidate. The canonical three-pair TPS floor passes.
The complete report is artifacts/comparison-mp-current-f3-zombieland-1000.json.

The bounded wide-view check uses one original/candidate pair, root size 100 and
the session-only zoom extension. Both actual camera rectangles include 50,750
map cells, 822 pawn root cells and 763 zombie root cells. Both samples retain
all 1,000 zombies at the endpoints, ordinary multiplier 15 and matching input.
TPS is 160.57622/166.69964, +3.81%. Median frame intervals are nearly equal at
67.2611/67.3639 ms, but p95 is 77.4934/86.4442 ms and p99 is 84.2721/93.1093
ms. That frame-pacing difference remains open; this single pair does not prove
a broad FPS improvement or satisfy the three-pair feedback gate. The full
report is artifacts/comparison-mp-current-f3-wide-zombieland-1000.json, with the
native wide screenshots and per-sample reports retained by the runner.

Both commands complete successfully and leave the measured candidate deployed.
No new feedback package or mod release is produced. These results do not
supersede the outstanding combined CE, Multiplayer transfer or wide-frame
acceptance checks.

### Multiplayer caravan deadlines and cold rejoin, 2026-10-07

The opt-in companion uses a native synchronized world command with primitive
IDs, CaravanExitMapUtility and CaravanEnterMapUtility. Native travel time is
skipped; exit, world-pawn ownership, entry and source cleanup are engine-owned.
The unchanged old f3ad33a1 gameplay sets arrival deadlines from world tick
47445 despite destination map tick 41215. After 142 ordinary destination ticks,
the deadlines remain unchanged and sight stays at movement range 6. Both clients
agree on that incorrect state. This is a Total Fog bug, not an upstream blocker.

Gameplay 7fb181fed468adc337e1dc1b7387c510d621fe9ce8f6c3a8894af401fbcf2391
with companion 3298d7da39feabb60fd189819e71140565b0d4ee3bda3833ccd5e2f5ca08d9eb
uses a once-bound native map-clock delegate for deadline creation. It adds no
reflection or clock lookup to the ordinary sight tick loop. Observation and
letter enqueue deadlines use the same helper; letter world-context delivery is
not separately established by this transfer sample. Three focused source tests
pass, including two observation cases that first failed. All 318 tests pass.

Both clients start world 47445, map 0 at 36583 and map 1 at 41215. Human390
exits map 0 into a native caravan with zero registrations, then enters map 1
with exactly one registration. Movement/next sight ticks are 41215, hearing
41315, observation 41227. After 125 Normal ticks, map 1 reaches 41340 while
map 0 stays paused; sight advances to 41366, hearing to 41415 and range to 24.
Returning through a second native caravan uses map-0 deadlines 36583/36683/
36595. Another 143 Normal ticks reach map 0 at 36726 with sight/hearing advanced
and range 24; map 1 stays paused. Complete normalized fog, source order and
schedules match at every boundary. No new attention/desync occurs.

Native save P is verified at 1,305,616 bytes. Two fresh processes register
notification handler 761 followed by caravan handler 762, preserving every
existing command ID. Cold load, hosting and client rejoin reach world 47714,
maps 36728/41342. Both retain 377/689 sources and matching complete fog plus
per-faction discovery, with no duplicate Human390 source. The known loading
InputLegacyModule exception still occurs and prevents clean-log acceptance.

Ignored evidence under artifacts/multiplayer-startup:
map-clock-tests-red.log, native-caravan-clock-red.json,
native-caravan-clock-green.json, native-caravan-clock-cold-rejoin.json and
map-clock-fixed-build-pair.json. The preceding f3ad33a1 performance result is
historical; the fixed bytes have their own comparison below.

### Map-clock fix performance floor, 2026-10-07

The canonical three-pair mp-map-clock-zombieland-1000 comparison passes on exact
gameplay 7fb181fe versus inherited 9d011de0. Six fresh processes load the unchanged
fixture with matching settings/loadout/camera and 1,000 zombies initially;
population endpoints range from 998 to 1,000. All measured native ticks retain
ordinary selected Ultrafast and rate multiplier 15, without forcing or the
private speed boost. Median TPS is 288.10090 original / 309.67403 candidate,
+7.49%. Tick CPU medians are 2.45376/2.27587 ms and frame-time p95 medians
71.1324/70.3766 ms. Complete native reports/logs and exact identities are retained
in artifacts/comparison-mp-map-clock-zombieland-1000.json and its six sample
reports. The runner completes successfully and leaves the candidate deployed.
This closes the changed-DLL floor on this single-player Zombieland fixture.
It does not establish Multiplayer throughput or close wide-view frame pacing.

### World-command delayed letter and saved deadline, 2026-10-07

The same 7fb181fe gameplay with companion 7fadf74a adds opt-in handler 763 after
notification 761 and caravan 762 on both main menus; all prior IDs stay unchanged.
The world command serializes an integer map ID, preserving the world clock.
Both clients start world 47714, map 0 at 36728, map 1 at 41342. With the host's
synchronized DelayAlertsUntilSeen enabled, the real neutral letter WorldClockA
targets hidden map-1 cell (2,2), recipient faction 16, delay 120. Its native
command receipt retains world 47714, but the queued deadline is correctly
41462. It remains pending after 32 Normal map-1 ticks; map 0 stays paused.

Native save Q is verified at 1,304,680 bytes. Fresh host/client processes load
and rejoin at world 47748 and map clocks 36730/41376, retaining the same letter
payload, target, recipient and deadline, synchronized delay setting, 377/689
sources and matching fog. The client disables visibility-based delay through
the native watcher. Both apply it; another 35 map ticks reach 41411 and the
letter still waits for 41462. A further 144 Normal ticks reach 41555 and release
it once into faction 16's archive only. Factions 17/18 never receive it. Another
46 ticks to 41601 retain exactly one archived copy and empty queues on both
clients. Complete normalized fog agrees; neither reports desync/new attention.
Native save R is verified at 1,306,393 bytes for continuation.

The full ignored paired evidence is
artifacts/multiplayer-startup/native-world-letter-clock-control.json, with the
pre-save state also retained in native-world-letter-before-cold.json. This closes
one real world-command deadline/save/recipient control. It does not close every
notification kind, gravship/map removal, enemy AI/CE Multiplayer combination or
the known reflection-only loading exception. All 318 tests and formatting/build
gates pass; adding this companion probe leaves gameplay bytes unchanged, so the
three-pair performance proof above still applies. The probe is excluded from
player ZIPs and its saved sessions require all three registered handlers.

### Multiplayer non-player rifle pipeline, 2026-10-07

Gameplay 7fb181fe / companion b88083ef registers opt-in handler 764 fourth on
both main menus without renumbering any previous commands. Fresh save-R host/
client processes reach world 47975, maps 36732/41603. A native synchronized
world command stages Human55211 (hostile-ancients faction 9, non-player) with
a bolt-action rifle and one missing eye, plus player steel Wall55216 20 cells
away on an empty firing lane. Native LOS is true, weapon range 36.9, the verb
is available, and CanHitTarget is true while AISmart is false. The client
synchronizes AISmart true; CanHitTarget becomes false on both clients.

The real AttackStatic job advances 335 Normal map-1 ticks to 41938; map 0 stays
paused at 36732. Enemy sight capacity is 0.75, range 18, target unseen, verb
idle and wall still 300 HP. Both clients match complete normalized fog and
actor state with no desync. A synchronized native health change restores the
missing eye. Capacity becomes 1, range 24, target visible and CanHitTarget true.
Another 362 Normal ticks reach 42300 with the native gun in cooldown and wall
at 282 HP on both clients; world reaches 48672. Fog still agrees and no new
attention/desync appears. Synchronized cleanup returns both to 377/689 sources
with matching fog and no active fixture. AISmart remains at its tested value.
Native continuation save S is verified at 1,307,841 bytes after cleanup.
Its host battle log attributes two native bolt-action rifle shots to Human55211,
including an impact on a Wall. The four matching fire/impact entries are retained
in artifacts/multiplayer-startup/native-enemy-caster-battle-log.xml. Damage and
actor/fog state match on both clients; the log attribution is from the host save.

The initial pirate-faction prerequisite failed before spawning on both clients;
the known bridge attention was reviewed, the inactive fixture confirmed, and
both processes stopped. This rejected setup is not gameplay acceptance. The
accepted probe instead uses the existing hostile-ancients faction from the
CE controls and checks that prerequisite before submitting setup. Independent
tests/formatting/build pass; gameplay bytes and their TPS proof stay unchanged.
Ignored evidence: artifacts/multiplayer-startup/native-enemy-caster-control.json
and native-enemy-caster-cleanup.json.
This establishes one non-player attack pipeline, not autonomous target selection,
enemy raids, turrets or combined CE Multiplayer targeting. The known loading
reflection-only exception still prevents clean-log acceptance.

## Multiplayer native target acquisition and speed spot check, 2026-10-07

Gameplay remains `7fb181fed468adc337e1dc1b7387c510d621fe9ce8f6c3a8894af401fbcf2391`.
The expanded opt-in enemy companion is `4965ea8f85efac04f167eb057530839ae1f8bc4f081e317d3933c735ed547c19`.
Both main menus append handlers 761..764 in the established order, preserving
native IDs. Native save S resumes at world 48674, map 0 36734 and map 1 42302.
The setup-ai world command stages hostile-ancients Human55221 with a bolt-action
rifle and one missing eye, and drafted unarmed player Human55225 twenty cells
away. Native LOS/range permit an attack, but enemy sight is 18 cells and the
target is hidden. The synchronized acquire action calls AttackTargetFinder with
the same threat/LOS/reachability flags as JobGiver_AIFightEnemy.FindAttackTarget,
restricting its candidates to the owned pawn without weakening native checks.
Both clients return no target with AISmart on, Human55225 with it off, and
Human55225 after AISmart is restored and the missing eye removed (range 24).
Normalized complete fog matches before and after all commands. Cleanup restores
377/689 registered sources and native save T is verified. No simulation ticks
advance during these controls and neither client reports desync. This proves
native target acquisition, not autonomous raid job selection or every AI branch.
Evidence: `artifacts/multiplayer-startup/native-enemy-target-acquisition.json`
and both preserved target-acquisition Player.log files.

The bridge's time control initially allowed only speeds through Superfast.
Installed Multiplayer 0.11.5 supports Ultrafast at native multiplier 15; its
public source has the same switch. The optional Multiplayer bridge now accepts
that native synchronized command and exposes TickRateMultiplier in clock status.
Fresh host/client processes use RimBridgeServer `82d9b37c95a933fea13ad24bf251678b5f96a5838a888fa808387c6ca544f66b`
and its Multiplayer companion `c13c0498447eea27be710d7cae60896f7edf950992a3839f0e2cb6f1d799b267`.
Cold save T resumes at world 48676, map 0 36736 and map 1 42304, with client
faction 18 and host faction 16. Both maps receive ordinary native Ultrafast
commands. Twelve live status samples span 12.967 seconds; all 48 sampled
client/map speed multipliers remain 15 with Ultrafast selected. Host map clocks
advance 5,445 ticks in the measured interval (419.912 TPS per map); the client's
advance 5,460 (421.069 TPS). These separate requests do not read the clients at
the same instant. After native pause commands, both agree exactly on world
57932, map 0 45947, map 1 51560, all paused, with identical normalized fog and
377/689 sources. Neither reports desync or new blocking attention. Save U is
verified. Evidence: `native-two-map-ultrafast.json`,
`target-acquisition-ultrafast-build-pair.json` and both preserved Player.log files
under artifacts/multiplayer-startup. This is one short instrumented two-process
Mac throughput spot check, not an original-mod comparison, many-mod/horde
benchmark, frame-pacing proof, cross-platform test or long-session acceptance.
The existing exact-gameplay-byte single-player performance floor remains valid.

A separate fresh quick-test control retains Prepatcher, Harmony, RimBridgeServer,
Core and all five DLCs, but removes Multiplayer and Total Fog from both the active
configuration and loaded-mod list. The same reflection-only InputLegacyModule
exception occurs. Configuration issues are zero and visual readiness succeeds.
The stopped profile's original mod selection is restored byte-for-byte; gameplay
settings are retained. Installed PrepatcherImpl MVID is
`532f91d20a724da5bec1158b6b915d86`; its inspected constructor registers a
ReflectionOnlyAssemblyResolve listener that logs then returns null, while its
reloader marks replaced assemblies reflection-only. This explains a plausible
dependency-resolution path, but does not establish the responsible requester.
Multiplayer is not required for this reproduction. A player-only loadout and the
exact failing caller remain unverified; no upstream issue or Total Fog loader
workaround is justified by this evidence yet. See
`prepatcher-without-mp-control.json`, `prepatcher-without-mp-Player.log` and the
preserved source/binary-inspection records under artifacts. The speed/load checks
retain this known exception and therefore are not clean-log acceptance.

The next opt-in diagnostic registers a bounded ReflectionOnlyAssemblyResolve
observer, returning null to retain native resolution. One fresh native save-U
load records a single InputLegacyModule request from
`Assembly-CSharp, Version=1.6.9676.17924`, with ReflectionOnly true. The only
managed caller is System.AppDomain.DoAssemblyResolve. Capture is stopped,
overflow is zero and the game is stopped; the same native error is preserved.
This identifies the old game assembly as the requester, not a Total Fog,
Multiplayer or bridge managed function. Native caller ownership remains unknown.
See `artifacts/multiplayer-startup/loader-requester.json` and its Player.log.

The player-only follow-up reaches the Steam 1.6.4871 main menu with Prepatcher,
Harmony, Core and all DLCs, excluding both Total Fog and Multiplayer and also
RimBridgeServer. The expected absent-bridge startup receipt is not a failed
game load. Native mouse controls are unreliable while keyboard page advancement
works; the control stops in fresh-colony setup without loading a map. It supplies
no player-only loading acceptance or failure. The historical base save is
rejected as a control because it contains the upstream fog component. Native
termination is verified and the original profile mod list restored byte-for-byte;
DevMode and gameplay settings are not changed. See the player-only-menu log and
preserved mod configuration. No upstream issue is filed from incomplete caller
and player-only evidence. The optional bridge diagnostic is excluded from player
ZIPs and does not install a resolver workaround.


## Documentation and inherited payload cleanup, 2026-10-07

The documentation review checks all twelve active Markdown/changelog owners,
local links/anchors, metadata and workflow descriptions. The README's three C#
examples and the architecture example compile at C# 7.3/net472 against the pinned
RimWorld/Harmony references. The Brrainz invite matches the user-owned Discord
starter template. Compatibility summaries now distinguish current development,
the delivered 6 October pair and dated evidence; Multiplayer remains unverified.

The obsolete Originals scratch/branding copies and inherited Texture2D bundle
are removed from the active checkout. All 47 bundle texture paths have portable
PNG counterparts, and active building texture references resolve. A temporary
baseline stage restores all 17 original bundle/load-folder/definition/patch files
byte-for-byte from upstream-baseline. Reusing that stage for Total Fog removes
the stale bundle/updater and restores the active load configuration and DLL.
Licensing, contributor attribution, active branding hashes, required PNGs and
frozen historical version payloads remain intact. Original files remain in Git
history, and the public baseline owns the original source/comparison payload.

The canonical formatting/build/test/package workflow passes 318 tests. The
player ZIP contains neither Originals nor the inherited bundle, and its gameplay
DLL is byte-identical to the previously native-tested 7fb181fe candidate. These
are source/example and packaging checks, not a new native visual or Windows
acceptance run. Removing the redundant bundle changes asset loading, so the next
native candidate check should include visible camera/watchtower textures. No
live game deployment, new feedback delivery or player release follows from this
cleanup. Local review details are in artifacts/documentation-review.json and
artifacts/logs/documentation-review.log; package output is in artifacts/logs/package.log.


## Stream feedback candidate, 2026-10-10

Mortal's 9 October report is addressed in both mods. Total Fog gameplay hashes
`ed78f3ae6233add9ac7126d12fdac3a32d89bba7e518c69e8bebd7b0db0ea424`;
Zombieland hashes
`a92ec372f7a9df5384659266005d4c004d7be30c6877f73c241e79a6daf0d108`.
The independent suite passes 322 tests. Gameplay and companion DLLs target
net472 and build with the pinned .NET 10 SDK. CE and Multiplayer work is retained
but is not the active acceptance scope.

Native Mac controls use Steam RimWorld 1.6.4871 with all five DLCs, Harmony,
RimBridgeServer, Zombieland and Total Fog. Camera+, CE and Multiplayer are absent.
Native white neutral/positive messages now obey the matching discard category
when unseen. Hidden fleeing, passerby, harbinger and pod-shaped targets are
submitted through the real message/letter pipeline; health/global and visible
controls remain immediate. This does not establish each natural incident
producer end to end.

Paused sight transitions hide a real corpse, fire drawing and artificial ground
glow, preserve own wall/door/floor blueprints on explored cells, and permit
Forbid/Allow area orders on an observed player door while rejecting its live
selection. The corpse stays spawned and undestroyed. Gameplay glow retains its
original color while the visual helper returns zero outside sight. No simulation
lighting or physical object is removed. Close screenshots show the same
objects before loss of sight, while hidden, and after reveal. Wide native
HighlightAll/Silhouettes controls show the red ordinary-zombie silhouette only
while visible, with no Camera+ dependency. These checks do not exercise a
natural hidden corpse resurrection sequence or every light source/shader.

Zombieland's native drafted Double Tap option is enabled with Hunting priority
zero; the undrafted control stays disabled. Invoking the actual option starts
the real job and removes the corpse's brain, preventing conversion. PageUtility
stitching leaves its input list untouched across two calls and yields one
settings page before world parameters. Unrelated pages are unchanged. Normal
UI entry reaches Scenario, Storyteller, Zombieland settings, World Generation
and Site Selection once each. The full reported setup loop remains unverified;
Mortal was asked which transition repeats. Infection label color is green.

The native records and screenshots are under ignored
`artifacts/stream-candidate-20261010/`. Explored wall drawing is unchanged pending
a visual-policy decision. Exact wall snapshots are not restored.

### Comparison setup repairs

Three incomplete attempts are retained in that evidence directory. The first
original-baseline stage could not load upstream textures after the active-tree
asset cleanup. Its restored original LoadFolders now includes the same portable
LegacyAssets PNG fallback as the candidate; the original DLL/XML bindings and
original bundle are retained. Subsequent attempts rejected optional null CE
identity and contamination input because the bridge omits null JSON fields.
The harness now accepts absent CE only when the native loadout has no CE, and
absent contamination input only in the non-overlay mode already checked by the
native runner. Matching identity and active-overlay input checks remain. These
failed attempts establish neither a candidate performance failure nor a pass.

### Current paired performance floor

`comparison-stream-candidate-20261010.json` completes three alternating
original/candidate pairs in fresh processes on the unchanged
TotalFog_Zombieland_UpstreamQuietGap1000 save. Every native tick uses the actual
Ultrafast multiplier 15, with private speed boost and work profiling off. Save,
camera, settings, engine and Zombieland identities match; native error summaries
are empty. Candidate median TPS is 326.73071 versus 294.63990 original,
+10.89%. Median mean tick CPU is 2.15396 versus
2.40241 ms; frame p95 medians are 69.8230 versus
70.1181 ms. This passes the declared three-pair TPS floor. It is
a bounded single-player Mac comparison, not identical rendered geometry,
Multiplayer performance, a busy-combat soak or proof for all mod lists.

The bounded wide-view check, `comparison-stream-candidate-wide-20261010.json`,
uses one original/candidate pair at root size 100, showing 50,750 native map
cells and 763 zombie root cells at the matched camera. TPS is 169.70297
versus 159.87089, +6.15%, but frame p95 is 85.4912
versus 75.6649 ms, worse for Total Fog. Every tick retains native
multiplier 15 and the error summaries are empty. This is a spot check, not
the three-pair gate or a broad FPS improvement.

### Fresh changed-feature and portable-asset controls

`stream-current-assets.json` repeats all six sight/presentation/highlight rows
on the frozen gameplay pair and the final Total Fog companion
`2e0ad899a84b81cdb3621b6a03d8374f3be3ff1a4fae4052815617b5a4865d09`.
All rows pass. Observed/hidden/revealed screenshots were inspected directly;
WatchTower, ground surveillance camera and CameraConsole use their correct
portable PNGs. The hidden fire, corpse and zombie disappear, the blueprints
remain, artificial ground glow is clipped, and the native distant zombie
silhouette follows current sight. Remembered scenery still uses live native
graphics, including their drawn power warnings. This check does not promise
immutable remembered scenery.

`stream-current-notifications.json` repeats the real message/letter pipeline on
these same bytes: hidden event messages and the pod letter are discarded,
health/global/visible messages remain, and the non-discarded hidden threat
enters the deferred queue exactly once. All returned controls pass. The native
log and four paired DLL hashes are preserved in that ignored evidence folder.
The two prior generic asset attempts used invalid fixtures: an unstuffy building
and a wall-attached camera without a wall. Their warnings/errors were not
claimed as production defects; the corrected probe uses WoodLog where required
and the valid ground camera.

The first full feedback gate stops after all four arrival controls pass because
the runner assumed an empty optional operation Warnings field would be emitted.
The current bridge omits that null field. Feedback verification, pixel validation
and packaging now treat absent/null warnings as empty while still requiring
operation success and every scenario invariant. The failed attempt is retained
in `feedback-verification-stream-candidate-20261010-tjn0ineh`; it is not a
completed acceptance gate.

The next attempt passes arrivals, area warnings, all 21 Albino controls and
16 explosion-camera controls, then stops in pixel validation on another omitted
null field: all-hidden Symbiant rows have no geometry submission. The validator
already accepted null geometry; it now accepts its omission too. Submission,
registration, pixel, UV and manual-target assertions stay required. Preserved
evidence is `feedback-verification-stream-candidate-20261010-_t9mlxoq`.

### Completed paired acceptance

The canonical `feedback-verify stream-candidate-20261010` succeeds. Its complete
record is `artifacts/zombieland-current-package-gates.json`, referencing
`feedback-verification-stream-candidate-20261010-jm7o_0sz`. Fresh evidence covers
12 Symbiant pixel/resource/interaction cases, 54 manual-target checks, all
40 rows in the four 400/4,000-cell dense/sparse rendering-cost matrices, four
silent arrivals and parameter lifetime, seven area-warning controls, 21 Albino
controls, 16 explosion-camera controls and 14 contamination refresh/pixel
controls. Recognized native logs are clean. Built/deployed gameplay and
companion pairs match the four hashes above. The input save remains unchanged
and native game termination is verified. Broader gameplay, Windows and long
sessions remain open. No bridge instrumentation is intended for the player ZIPs.

### Private package and delivery

The canonical `feedback-package` succeeds with 322 tests and refreshed exact-byte
native gates. The complete Total Fog archive is 1,161,907 bytes, SHA-256
`f861fb9ac8737c2949e96f96aa3d973e68599a8c5c9881206d1bd6f1fcfd5688`.
The complete Zombieland archive is 193,884,686 bytes, SHA-256
`b97deb82995ea31fbf4602fa993fd0736dccb62f1ba0e6c04da68e535a68d263`.
Both include the current TESTING.md, contain the tested gameplay DLLs, and
exclude companions and source. Zombieland includes all 54 music files.

Mortal receives the two separate archives in the verified existing private DM.
Both remote downloads match their local archive hashes. Only then is the
superseded own 6 October delivery post removed, as requested. Its original
archives were downloaded and verified first and remain in the ignored
`stream-candidate-20261010/previous-delivery` folder. The new delivery receipt
is likewise ignored. The message reports the wall-memory and unreproduced
setup-loop limits and asks for Windows/busy-save feedback. Its robot marker
is at the end. No public mod release or Steam upload is performed.

### No Pause Challenge setup follow-up, 10 October

Mortal's Custom-difficulty follow-up links a HugsLib report with a
`WindowStack.Add` exception in `NoPauseChallenge.WindowStack_Add_Patch`:
https://gist.github.com/HugsLibRecordKeeper/f7d7b6142490760a52194ca9cbf1b3fe.
This is a separate mod defect, not a new Total Fog or Zombieland gameplay change.

The delivered Total Fog/Zombieland pair, all five DLCs and the original No Pause
Challenge 3.7.2 gameplay DLL reproduce the exception in native Steam 1.6.4871.
Custom difficulty with No Pause enabled opens Zombieland settings, but leaves
the storyteller page open. `Page.DoNext` adds the next window before closing
the current one. The No Pause dialog hook sets time speed while the world is
still uninitialized; the native speed setter then dereferences the missing
Gravship controller. The baseline gameplay SHA-256 is
`8368187ecd7584816434935807926a4ddad481086b62a34c6793064457e5b331`.

No Pause now limits this dialog-speed adjustment to `ProgramState.Playing`.
The fixed native Custom transition closes the storyteller page, then closes
Zombieland settings when advancing to world parameters. Back navigation and a
Peaceful preset transition also pass without duplicate pages or captured errors.
The setup checkbox is verified checked; the companion enables it through the
production state transition. In an existing save, a real mod-settings dialog
still switches Superfast to Normal and advances ticks with No Pause enabled.
With it disabled, the dialog retains Superfast and pauses as usual. No errors
are captured in the fixed process. These checks do not generate a complete new
world or validate Mortal's entire mod list. His end-to-end confirmation remains
outstanding.

The fixed gameplay DLL SHA-256 is
`688524b93b6f5c1391823eadcf4e031dc56620ed1d0bae0b5b226307da06c2f8`.
The separate preliminary No Pause ZIP contains exactly those tested gameplay
bytes and excludes companions, private Harmony and debug/Finder files. The
isolated load-order file is restored byte for byte and the test game is stopped.
Proof and logs are ignored under
`../NoPauseChallenge/artifacts/custom-difficulty-20261010/`.
The paired Total Fog/Zombieland delivery remains unchanged.

### Optional Multiplayer API startup and later wizard stages, 10 October

Mortal's next report links
[this HugsLib record](https://gist.github.com/HugsLibRecordKeeper/d61714221f2d8f7bcf4ca525ac6f769b).
Total Fog calls `RegisterSyncField` on Multiplayer's dummy implementation and
gets `Multiplayer.API.UninitializedAPI`. His loadout includes More Planning
(Continued), which bundles Multiplayer API 0.6, but not Multiplayer itself.
Bundling the optional API is valid; Total Fog must check its `enabled` field
before registering. That field indicates an initialized implementation, not
whether a multiplayer session is currently running. Registration still occurs
at the main menu when Multiplayer is enabled.

A new regression test fails before the guard and passes afterward. All 323
independent tests pass; gameplay and companion builds have no warnings/errors.
Native Steam 1.6.4871 reproduces the exact startup exception with Achtung's
byte-identical API 0.6, MVID `c74c9bf7f68c4e12ac6ee2a0c2bd9227`.
The fixed Total Fog starts and loads the unchanged 1,000-zombie fixture with no
errors recognized by Zombieland's full Player.log summarizer. Its fog snapshot
has 9,122 known cells and 8,813 player-covered cells. The bridge log journal
alone misses the original early startup exception; the native Player.log is
the baseline error evidence.

An initial Multiplayer startup control has a missing Prepatcher dependency and
two ordering issues, so it is retained as incomplete. The existing MP host
profile provides the valid control with Prepatcher, Harmony, RimBridgeServer,
all DLCs, Multiplayer and Total Fog. It has zero configuration/dependency/order
issues, loaded mods match the active configuration, the native Multiplayer
status is available, and the full Player.log has no recognized errors. It is
a main-menu startup control, not a new two-client compatibility test.

Two native new-colony controls reach a playable map with one Zombieland settings
screen. The first uses the original paired profile with Custom difficulty; the
second enables the fixed No Pause Challenge and adds Achtung. Both complete
world generation, site selection, ideology and characters. The second retains
the known pre-fix API startup exception, but no later setup-page exception.
Mortal still reports another Zombieland screen after site selection. His exact
`ZombieLand.dll` and whether this occurs with the minimal or full mod list have
been requested. The local controls do not reproduce his full 39-mod loadout.

The new gameplay SHA-256 is
`10df84231ae2b812a909ecd35ac6cff61de771882bbd5f9ca349f1c71fc62df2`.
The canonical package rebuild matches those native-tested bytes. The separate
Total Fog preliminary ZIP is 1,159,009 bytes, SHA-256
`89fc0427258ca2101c5092ae7d0d3a100d86f8d7ef32b4e75ed283ae62bff8b1`.
It excludes companions and private Harmony. The remote Discord download matches
the local ZIP. Only afterward is the obsolete Total Fog attachment removed from
the earlier paired post; its unchanged Zombieland attachment is retained.
The earlier pair's broader native rendering and performance gates identify
its older Total Fog DLL and were not rerun for this startup-only change.

Proof and full native logs are ignored under
`artifacts/startup-feedback-20261010/`. Both games are stopped. The primary
isolated load-order file is restored byte for byte; the existing MP host profile
is unchanged. No public player release or Steam upload is performed.

### Tester retry, 10 October, 10:54 UTC

Mortal reports that he retried with all updated mods and could not reproduce
the issue. If it returns, he will provide more precise reproduction steps and
the mod list. The private feedback reference is retained in the ignored local
delivery records. This does not establish the exact cause of the second
Zombieland settings screen or validate every combination in his full loadout.
The duplicate-screen investigation is parked unless it recurs. No new code or archive is needed for
this feedback; he keeps the existing Total Fog, Zombieland and No Pause builds.

### Transport-pod notification preference, 10 October

The tester prefers silent transport-pod messages or finer controls, rather
than relying on broad categories. Clarification is requested for unseen-only
versus all arrivals, and hiding the notification versus muting its sound.
The current Information settings discard unseen positive/neutral/negative
events and threats. They preserve visible notifications and global events
without targets; there is no dedicated transport-pod or notification-sound switch.

DecompilerServer inspection of public Steam 1.6, engine MVID
`967ddb80559449f0a776dafa26a855d1`, finds:

- `IncidentWorker_ResourcePodCrash` sends a positive letter with a map-cell target.
- `QuestNode_Root_RefugeePodCrash.SendLetter_NewTemp` sends a neutral letter with
  a pawn target.
- `QuestPart_DropPods.Notify_QuestSignalReceived` sends a letter with a map-cell
  target and `customLetterDef ?? PositiveEvent`; custom quest delivery categories
  can therefore differ.

This source audit explains the existing broad settings and identifies actual
producers for a later focused native check. It is not a natural-incident runtime
pass or proof of modded pod producers. The earlier synthetic notification test
keeps its narrower claim. No gameplay code, settings or archives change pending
clarification. Private message IDs are kept only in ignored local feedback records.

The tester's follow-up says all discard categories are enabled, then notes that
he may have had sight of the landing when it arrived. This leaves an unseen-pod
failure unconfirmed; visible arrivals are intentionally preserved. The private
feedback checker now runs every 60 seconds during this testing window, retaining
its existing expiry. The shortened schedule has already delivered new replies
into this session. No new gameplay build is sent for the uncertain report.
