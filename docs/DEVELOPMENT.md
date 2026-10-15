# Building Total Fog from source

Total Fog builds with the .NET SDK version in [global.json](../global.json). The
solution is `Source/TotalFog.slnx`. The SDK is **10.0.301**; gameplay and companion
DLLs target **.NET Framework 4.7.2**, while independent tests run on .NET 10.
The workflow needs Python 3, Git and `uv` for the pinned Ruff formatter. Live
setup, deployment and measurement commands currently use the macOS Steam install
and local GABS/RimBridgeServer tools. Set `RIMWORLD_MOD_DIR` to use another
installed game's `Mods` folder.

| Command | What it does |
|---|---|
| `./scripts/mod format` | Formats active C# and Python source with the pinned tools |
| `./scripts/mod format-check` | Checks formatting without editing files |
| `./scripts/mod test` | Checks formatting and runs pure tests without a game install or DLL changes |
| `./scripts/mod build` | Checks formatting, runs the tests, builds the mod and records its source/binary receipt |
| `./scripts/mod verify-binary` | Checks that the tracked DLL and build receipt match the current gameplay source and build inputs |
| `./scripts/mod deploy` | Builds and copies the mod into your RimWorld `Mods` folder |
| `./scripts/mod deploy-built` | Installs the already-built gameplay/companion pair without rebuilding; the game must be stopped |
| `./scripts/mod metadata` | Updates the installed description and version/load-folder metadata without changing DLLs; the game must be stopped |
| `./scripts/mod package` | Builds the player ZIP (one `TotalFog/` folder) |
| `./scripts/mod package-built` | Packages the existing verified DLL without rebuilding, including first-release Workshop ID metadata |
| `./scripts/mod setup` | Installs the build and creates the isolated test profile |
| `./scripts/mod ce-setup` | Builds and deploys the mod and creates the separate Combat Extended test profile |
| `./scripts/mod ce-setup core16` | Uses the offline 1.6 app with Core and the installed Workshop CE payload, in a separate profile; set its logical `Mods` path in `RIMWORLD_MOD_DIR` |
| `./scripts/mod zombieland-setup` | Builds and deploys both mod/companion pairs into the Zombieland test profile |
| `./scripts/mod ce-zombieland-setup` | Builds both mods and creates a separate test profile with Combat Extended, Zombieland and all DLCs |
| `./scripts/mod mp-setup` | Configures separate Multiplayer host/client profiles without building or deploying |
| `./scripts/mod mp-layout` | Places the running host on the left and client on the right of the main display |
| `./scripts/mod dpa-setup [on\|off]` | Adds or removes Dubs Performance Analyzer in a stopped test profile; set `TOTALFOG_GAME_ID` to select another profile |
| `./scripts/mod baseline` | Installs the original mod for comparison tests |
| `./scripts/mod benchmark` | Runs the sight-engine speed comparison |
| `./scripts/mod runtime-benchmark <label> <save> [speed]` | Measures three fresh game processes on the named save; results go to `artifacts/` |
| `./scripts/mod runtime-compare <label> <save> [speed]` | Alternates three baseline/candidate pairs; set `TOTALFOG_COMPARISON_BASELINE_ROOT` to the accepted Total Fog checkout |
| `./scripts/mod feedback-verify <label> [core16]` | Renews exact-binary paired native correctness gates; `core16` uses the isolated offline Core-only fixture |
| `./scripts/mod feedback-package` | Verifies the existing paired gates and creates separate preliminary Total Fog and Zombieland ZIPs |
| `./scripts/mod source-publish` | Pushes committed source and verifies GitHub; does not publish a player ZIP or update Steam |

For a short wide-zoom comparison, prefix `runtime-compare` with
`TOTALFOG_WIDE_VIEW=1 TOTALFOG_COMPARISON_PAIRS=1`. It records the actual visible
map area at root size 100. This is a spot check; use three pairs when a suspected
regression needs confirmation.

The accepted performance baseline is Total Fog commit `07d128b`. The current
optimisation work is complete. Future comparisons preserve correct fog behaviour
and use that build, rather than requiring the same speed as the original mod.
Set `TOTALFOG_BENCHMARK_REF=07d128b` for kernel comparisons. The comparison tools
retain historical defaults when no baseline override is provided; those defaults
are not release requirements. See the
[baseline and measurement limits](VALIDATION.md#accepted-performance-baseline-2026-10-13).

Full logs are kept in `artifacts/logs/`. RimWorld 1.6 and 1.7 load the active
DLL, definitions and assets from `Current/`; the older version folders stay
for history. This active folder is a declared exception to the shared mod
standard. The shared DLL intentionally compiles against the 1.6 reference
package, following the engine developers' compatibility strategy. We will
adopt a specific 1.7 reference when available. Native results retain their
recorded engine versions, hashes and limits.
More details:
[architecture](ARCHITECTURE.md), [coverage](COVERAGE.md),
[validation](VALIDATION.md) and [AGENTS.md](../AGENTS.md).

For source-only review builds that should preserve the tracked DLL, use
`TotalFogBuildOutput="$PWD/artifacts/review/Assemblies/" ./scripts/mod build`.
The local `0.1.0` tag identifies an earlier reviewed build, not a public release.
Later gameplay edits need a build before installation. GitHub Actions checks
formatting and pure tests on Linux and Windows for pushes and pull requests; it
does not test RimWorld, mod loading or native rendering.
Preliminary delivery instructions are in [PRELIMINARY.md](../PRELIMINARY.md).
