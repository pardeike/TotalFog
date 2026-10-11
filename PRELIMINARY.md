# Total Fog and Zombieland preliminary feedback builds

Private feedback update, 11 October 2026, for RimWorld 1.6. These are two
separate, complete mod ZIPs. Replace the previous local TotalFog and ZombieLand
folders. They retain the earlier test-build fixes and add the settings below.
This is not a public release. Keep the separate No Pause Challenge setup fix
if you use that mod.

Support: [Brrainz Discord](https://discord.gg/G4r84eN7w6).
Source: [Total Fog](https://github.com/pardeike/TotalFog) and
[Zombieland](https://github.com/pardeike/Zombieland).
The [coverage checklist](https://github.com/pardeike/TotalFog/blob/main/docs/COVERAGE.md)
records remaining checks. Later source commits are not automatically included
in these archives.

## Installation

Close RimWorld, replace each previous local mod folder, and restart. The paths
must end in `Mods/TotalFog/About/About.xml` and
`Mods/ZombieLand/About/About.xml`. Disable the Workshop copy of Zombieland so
only one copy is enabled. Disable NWN Real Fog of War and other fog mods.
Use a copy of your save. Neither mod requires the other to be loaded.

## New settings

- Total Fog, Vision: **Colony mech vision multiplier**. It defaults to 1 and
  ranges from 0.2 to 2. It changes colony-mech sight, including fog targeting,
  without changing human sight, hostile-mech sight, or weapon range.
- Zombieland, Miscellaneous: **Use normal flee response when avoidance is off**.
  It defaults to off. Enable it while **Automatic avoidance of zombies** is
  off to let the Assign-tab Flee response use RimWorld's normal checks for
  nearby zombies. Ignore remains Ignore. The option does not enable automatic
  path avoidance or change the existing avoidance-on behavior.

The cumulative Total Fog fixes include hidden refugee-pod letters sent before
the passenger spawns, delayed Anomaly codex discovery, Anomaly music suppression,
and hauling known vanilla chunks after sight is lost. Combat music suppression
also suppresses Anomaly music. General ambient phenomena remain unchanged.

## What was checked

All 347 independent Total Fog tests pass. Fresh native Mac RimWorld 1.6 checks
with all DLCs and both mods confirm mech ranges at multipliers 1, 0.2, 0.5, 2,
and 1 again. Human and hostile-mech controls remain unchanged. The native Vision
page shows the slider and its default.

Native zombie controls confirm the new Flee option is off by default, allows
the normal zombie threat check when enabled with avoidance off, preserves Ignore
and hostile-human behavior, and retains the existing special-zombie rules with
avoidance on. The scoped fixture also obtains a native FleeAndCower job. It does
not prove every autonomous escape, special-zombie combination, or long session.
Native error journals are empty after these checks.

The archive gameplay DLLs must match the exact native-tested builds. Diagnostic
companions are excluded. Earlier rendering and performance gates identify older
DLLs and were not rerun for these small settings changes. These archives do not
establish fresh performance acceptance, Windows behavior, or every mod list.

## Still open

The reported mature Fleshmass Heart slowdown is still under investigation.
The local geometry fixture does not reproduce the complete streamed mod list
or the Heart's natural growth history. This update does not claim to fix it.
Research availability and similar deep game systems can still reveal activity;
the fog does not rewrite those systems. Explored scenery uses native current
drawing rather than an exact archive of its last observed appearance.

For this update, try the mech slider and the Flee setting in your normal save.
If they misbehave, a short description and Player.log or save are enough to
start investigating.
