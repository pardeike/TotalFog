# Building Total Fog from source

Total Fog builds with the .NET SDK version in [global.json](../global.json). The
solution is `Source/TotalFog.slnx`. The SDK is **10.0.400**; gameplay and companion
DLLs target **.NET 10 / AnyCPU**, while independent tests run on .NET 10.
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
| `./scripts/mod deploy` | Builds, installs the gameplay/companion pair and verifies `Mods/TotalFog.zip`; the game must be stopped |
| `./scripts/mod deploy-built` | Installs and packages the verified existing pair without rebuilding; the game must be stopped |
| `./scripts/mod metadata` | Refuses partial updates; use the complete paired deploy command |
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
| `./scripts/mod showcase [capture\|stills\|stage\|verify\|publish]` | Reproduces or publishes the fixed-size screenshots and short video; see [the media recipe](../Media/Showcase/README.md) |

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

Full logs are kept in `artifacts/logs/`. RimWorld 1.6 keeps its existing
`Current/` and `LegacyAssets` mapping. RimWorld 1.7 loads `1.7/`, which contains
its own definitions, textures and languages. The 1.7 mapping loads only `1.7/`.
The main project and companion build only for 1.7. They cannot write into the
frozen payload. `scripts/frozen-payload.json` checks the existing historical,
1.6, and language bytes before and after canonical commands.

The references are pinned to RimWorld `1.7.4990-unstable` and Harmony
`3.0.0-rc.4`. The companion uses the published `RimBridgeServer.Sdk` `2.0.0`
package from NuGet.org. No local SDK package is required.
Do not rebuild it from another repository as an incidental Total Fog step.

`./scripts/mod build` runs formatting checks, independent tests, and the main
and companion builds. It writes the 1.7 source receipt and paired hashes.
The Release project build writes those receipts after the companion succeeds,
before any deployment target. Direct project builds use the same receipt writer.
`./scripts/mod verify-binary` verifies the frozen payload and the 1.7 receipt.
The local frozen snapshot contains unpublished changes. Packaging uses published
v1.0.4 bytes from a checksum-pinned archive instead. That archive matches all
277 files in the accepted Steam upload. Steam's current update timestamp still
matches that publication. Local `Current/`, root languages and historical folders
remain unchanged. Historical folders absent from that publication stay out of
the new ZIP. Published DLLs, languages, artwork and other runtime resources retain their
bytes. Existing installations also retain those files' inode and mtime.
`LoadFolders.xml` retains its legacy mappings and selects `1.7/` for 1.7.
`Directory.Build.props` owns the mod version. The normal build syncs only the
version values in `About/About.xml` and `About/Manifest.xml` from that owner.

For a normal build, paired install and ZIP, run:

```sh
RIMWORLD_MOD_DIR="/path/to/RimWorld/Mods" ./scripts/mod deploy
```

The companion goes to the selected Mods root's sibling `BridgeTools/TotalFog`
directory. The command verifies `dist/TotalFog.zip` and `Mods/TotalFog.zip` against
the installed player payload. Companions stay outside the ZIP.
Before changing an existing install, the command refuses missing, different or
additional legacy files and symlinks. It never rewrites existing published runtime/resource files.
Use `./scripts/mod package` to build and verify only the ZIP. It never installs,
including when `RIMWORLD_MOD_DIR` is inherited. Metadata-only updates are disabled.

The older runtime, feedback, setup, and showcase commands in the table are
historical 1.6 harnesses. The canonical command refuses them until they receive
an explicit 1.7 port. Existing evidence keeps its original engine and binary
limits. Acceptance for this port requires clean 1.7 startup and a new game.
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
