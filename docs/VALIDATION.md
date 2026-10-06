# Local validation

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
