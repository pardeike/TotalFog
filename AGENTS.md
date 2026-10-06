# Total Fog

Read ../steam-mods/AGENTS.md and its development/testing guidance.
Preserve inherited attribution, license, historical version folders, and assets.
Build with the .NET 10.0.301 SDK pinned in global.json. The quiet workflow uses
the existing SDK install under ~/Library/Application Support/dotnet when present,
including for nested companion builds. Gameplay and companion DLLs still target
net472 for RimWorld; independent tests run on net10.0.
Use the canonical quiet command `./scripts/mod build|deploy|package|setup|baseline|benchmark`.
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
Verify gameplay screenshots and semantic evidence with
`./scripts/mod verify-rendered artifacts/<evidence-folder>`. This uses Pillow
from artifacts/pixel-analysis-env when present, otherwise the current Python.
Use the `total-fog-16` GABS game for live verification. Its savedata is isolated
under artifacts/UserData. Core and all five DLCs plus Harmony and Total Fog are
the gameplay loadout; RimBridgeServer is test instrumentation.
Use `./scripts/mod dpa-setup` to verify the installed Steam Dubs Performance
Analyzer supports 1.6 and enable it in this isolated profile. Profile through
RimBridgeServer's `dpa_*` tools, then stop and clean up instrumentation before
performance comparisons. DPA is excluded from feedback ZIPs.
Use `./scripts/mod ce-setup` for `total-fog-ce-16`, the separate Steam profile
under artifacts/CEUserData with installed Combat Extended and all DLCs. It
preserves the core performance profile and existing CE settings/loadout.
Use `totalfog/ce_ammo_fixture` for the scoped no-magazine short-bow control.
It observes CE's own attack orders, ammunition preparation and shot results;
play through ordinary Normal playback. Remove/recreate its real security bell
to change sight during warmup, then call cleanup to restore the temporary base
range and remove owned objects/probe patches. It is companion instrumentation,
excluded from player ZIPs, and does not support save/load.
Use `totalfog/ce_turret_fixture` trace-start/trace-stop to observe bounded native
burst fallbacks before and after Total Fog. Stop traces and remove owned objects
before other tests. configure-aim-mode uses CE's native toggle. fallback-policy
is a readonly loaded-guard contract with supplied boolean inputs; it does not
prove that CE naturally executed the still-Thing fallback branch.
Use `./scripts/mod zombieland-setup` for `total-fog-zombieland-16`, the isolated
Steam profile under artifacts/ZombielandUserData with Total Fog, local Zombieland
and all DLCs. This builds/deploys both mod/companion pairs and verifies their
bytes before registering the profile. It preserves the core and CE test profiles.
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
Source/TotalFog.slnx opens all four. Upstream scratch files are preserved in
Originals/UpstreamSource. The tests source-link visibility/lifecycle code with a small engine boundary;
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
