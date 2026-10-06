# Total Fog

Development source for a preliminary RimWorld 1.6 mod. Development and
compatibility testing are ongoing. Build from source before installing this
checkout; tracked assemblies are previous snapshots. Separately delivered test
ZIPs contain their validated assemblies. See [PRELIMINARY.md](PRELIMINARY.md) for
installation and testing notes.

Source and issues: [pardeike/TotalFog](https://github.com/pardeike/TotalFog).

Fog of war and shared field of view for RimWorld. An independent continuation of
Real Fog of War, based on Mlie's NWN continuation. See [NOTICE.md](NOTICE.md) for
attribution and [LICENSE.md](LICENSE.md) for Apache 2.0 terms.

Enable Harmony and Total Fog. Disable other Real Fog of War versions.

The active runtime uses `TotalFog` namespaces and independently named types.
Specific original save types are read through the engine compatibility boundary;
Total Fog does not export classes in the original mod's namespace.

## Development

Run `./scripts/mod build`, `./scripts/mod deploy`, or `./scripts/mod package`.
Run `./scripts/mod benchmark` for the retained pure-caster comparison.
Run `./scripts/mod runtime-benchmark <label> <save name> [Normal|Ultrafast]`
against a running paused test game for three native load/playback samples.
Use the same save, settings, view and hardware for each binary; results are in
`artifacts/runtime-<label>.json`. The development companion measures whole-tick
CPU time and frame intervals, removes its instrumentation, and leaves the game paused.
Full command output is retained in `artifacts/logs/`. Local reproduction uses
RimWorld 1.6, all DLCs, Harmony, Total Fog, and RimBridgeServer for automation.
`./scripts/mod setup` installs the current build and creates the isolated Steam GABS profile.
`./scripts/mod baseline` installs the inherited binary for comparison tests.
Only RimWorld 1.6 is currently advertised as supported. Historical folders stay in the source tree.
The clip and test saves stay in ignored `artifacts/`.

The canonical commands run the independent contract suite before building the
main mod and its development-only BridgeTools companion. The solution is
`Source/TotalFog.slnx`. See [architecture](docs/ARCHITECTURE.md),
[coverage](docs/COVERAGE.md), and [validation](docs/VALIDATION.md) for boundaries
and evidence.

Other mods can integrate without depending on Total Fog. See the
[optional integration example](docs/ARCHITECTURE.md#optional-integration-example)
and [public API](Source/TotalFog/Visibility.cs). Bind the queries once and retain
ordinary behavior when Total Fog is absent. Confirmed upstream integration
blockers follow the issue-reporting rule in [AGENTS.md](AGENTS.md#blocked-integrations-with-other-mods).

`./scripts/mod source-publish` pushes committed source and verifies the remote
commit. It does not publish a player ZIP or a Steam Workshop update.

Combat-music suppression, hidden-source muting, hearing-based audio filtering,
and hearing indicators are separate settings. Filtering range and muffling
strength are adjustable.
The public ZIP contains one `TotalFog/` folder. Extract it into `RimWorld/Mods`.
