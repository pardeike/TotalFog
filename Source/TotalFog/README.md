# Total Fog gameplay assembly

This is the mod's gameplay DLL (`TotalFog.dll`, net472). The pure sight engine
it builds on lives in [`../Core`](../Core) and is compiled into this assembly
without Verse or Unity references. Test instrumentation lives in
[`../BridgeTools`](../BridgeTools) and never ships to players.

## Folder map

| Folder | Owns |
|---|---|
| root | The optional public API for other mods: `Visibility.cs` and `SoundAudibility.cs`. Bind by name, see the main README. |
| `Mod/` | Mod entry and Harmony installation (`TotalFogMod.cs`, `TotalFogMod.Patches.cs`), `PatchSafety`, settings model (`FogSettings`) and window (`SettingsWindow`), def references, debug commands. |
| `Sight/` | The sight model on a map: `MapVisibility` (per-map component, grids, refresh queue), `CompSightSource` (what an observer sees), `CompFog` and its subcomponents (per-thing visibility, cell registration, presentation state), wall and tree occlusion, the fog `SectionLayerFog`, shared tick intervals in `FogTiming`. |
| `Presentation/` | Gates between the sight model and what the player sees: drawing, labels, overlays, tooltips, selection, lighting, gas, flecks, explosions, custom renderer and inspection registries. |
| `Detours/` | Harmony patches on specific engine types, one file per patched type. |
| `Notifications/` | Deferred letters and messages, threat alert filtering, silent raids, entity discovery. |
| `Audio/` | Combat and Anomaly music suppression, hidden sound muting and hearing-based filtering, sound markers. |
| `Cameras/` | Surveillance cameras, the camera console and its job, the watch telescope and placement rules. |
| `Watchtowers/` | The 3x3 watchtower: building, job driver, orders, combat, presentation, artwork, wall joins. |
| `Compatibility/` | Optional integrations bound at startup: Combat Extended, Multiplayer, Zombieland-facing registries, gravship previews, minimaps, legacy save types. |
| `Utils/` | Small extension helpers. |

## Conventions

- A type's full name (namespace plus type name) is a contract. `Current/Defs`
  XML, saved games and `Compatibility/LegacySaveTypes.cs` bind types by full
  name, so a rename can break existing games. Folders do not set namespaces, so
  moving a file is free. `BridgeTools` is test instrumentation; it reflects on
  some names and is simply updated alongside a rename.
- Every Harmony install goes through `PatchSafety.Install` so one failing hook
  disables only its feature and logs once. Shared targets have one installation
  owner in `TotalFogMod.Patches.cs`.
- Presentation code asks the sight model; it never changes it.
- Formatting is owned by CSharpier (`./scripts/mod format`); the build and CI
  reject unformatted files. `.editorconfig` is its only configuration, and
  `.gitattributes` keeps C# files LF in every checkout, including Windows.
- Player-facing text lives in `Languages/English`; code uses keys only.

See [`docs/ARCHITECTURE.md`](../../docs/ARCHITECTURE.md) for the design and
[`AGENTS.md`](../../AGENTS.md) for the workflow commands.
