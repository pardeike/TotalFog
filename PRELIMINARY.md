# Total Fog preliminary feedback build

Private work-in-progress build, version 0.1.0, 6 October 2026, for RimWorld 1.6.
The overhaul is ongoing. Windows confirmation, broader mod compatibility and
long-session performance testing remain open. This is not a finished release.

## Installation

Discord delivery uses `TotalFog-preliminary.zip` and
`ZombieLand-preliminary.zip`, each with this guide as `TESTING.md`.
Both ZIPs are complete, including Zombieland's music. Close RimWorld and replace
previous local mod folders with the folders from these ZIPs. No Workshop-file
copy is required. This complete Zombieland ZIP supersedes the earlier small
`ZombieLand-preliminary-update.zip`.
The resulting mod paths must be `Mods/TotalFog/About/About.xml` and likewise
for ZombieLand. Disable the Workshop
copy of Zombieland while using this local test copy, so only one Zombieland is
enabled. The included Zombieland has fog integration fixes; the Workshop version
does not yet contain all of them. Use Harmony, Core, your DLCs, Zombieland and
Total Fog for the first combined test, then try your usual mod list. Restart
RimWorld after switching builds and test a copy of your save. Disable NWN Real
Fog of War and any other fog-of-war mod. A save made with NWN Real Fog of War will warn that its mod
list differs; Total Fog replaces that package. The original player's combat
save loaded and ran in the local Mac test, but general save compatibility is
still being checked.

## Included changes

- Initial sight uses ready daylight and lamp lighting after loading a map.
- Common single-cell visibility checks avoid repeated footprint construction.
- Zombieland healer beams/glows and the zombie counter/hover highlights respect current sight.
- Hidden healers continue their gameplay work when their drawing is suppressed.
- Danger-area warnings follow sight at the reported location, including when a Symbiant's core and root have different visibility. Colonist warnings keep working.
- Albinos keep an active scream or hack when a hit makes RimWorld reconsider their job.
- Hidden explosions no longer shake the camera. Visible blasts retain their native shake, damage and heat.
- Contamination markers follow current sight at both zoom levels, including after hidden changes, zooming and reopening the overlay.
- Fog section changes also appear while the game is paused.
- Wall-climbing facing advances with game ticks, so drawing or moving the camera does not change it.
- Symbiant body parts and its rotating core clip to the fog boundary, even when the root is hidden.
- Symbiant selection, manual targeting, labels and core hover follow their actual visible cells.
- Right-click feeding choices require a visible Symbiant core and visible corpses, including when its root lies outside sight.
- Large Symbiant bodies avoid repeated overlapping hidden-cell scans and per-frame logical-state normalization.
- Electric/tank ambient loops and spitter/rising-wave sirens follow the selected hidden-source and hearing options.
- Electric combat sounds play at the actual event; hidden or rapid hits no longer build a backlog of flashes and repeated sounds.
- Spitter impacts no longer reveal hidden explosion flashes. Existing smoke respects sight changes while continuing to move, age and expire normally.
- Independent Total Fog runtime namespace and component types.
- Import of existing original-mod map discovery and deferred-notification data.
- Fleshbeast flight visibility and landing registration fixes.
- Fix for the flying-pawn power-grid error during save loading.
- Colony health, global and visible-event notifications remain immediate.
- Discard-event settings apply only to unseen events.
- Enemy fog targeting is optional; disabling it skips enemy sight calculations.
- Humanlike enemy fog targeting checks each Symbiant body cell against the attacker's own faction sight.
- Optional Silent Raids removes enemy raid and manhunter arrival letters and arrival slowdown, preserving native spawning and ordinary combat slowdown.
- Walking pawns checked at their actual drawn position when crossing sight.
- Native rendering for explored scenery and observed static objects.
- Removed GPU drawing capture and visual-memory archives to reduce overhead.
- Separate combat-music, hidden-source audio and hearing settings.
- Reworked settings page with more space beside the scrollbar.

Open Options, Mod settings, Total Fog, then Audio. "Suppress combat music" is
enabled by default for new settings. Existing settings may override defaults.
The renamed mod entry point uses its own settings file. Earlier preliminary
Total Fog settings may need to be selected again.
Other audio and hearing options can intentionally provide clues, so review
them for the kind of fog-of-war play you want.

In Information, "Silent raids and manhunter packs" is off by default. Enable it
to remove those arrival warnings entirely, including for visible arrivals. No
delayed arrival warning is kept. Combat music is controlled separately in Audio.

## Feedback wanted

For Zombieland, please focus on ordinary/special zombie visibility, block bubbles,
wall climbing and landing, healer effects, the counter, danger-area warnings, and Symbiant body/core
visibility and interaction. Watch for hidden activity leaking through alerts,
audio or effects, and for visible things disappearing. Try saving/reloading and
a busy horde at your usual speed. Please include Player.log and, for a visual or
gameplay problem, a save or short clip plus the enabled mod list.

Mac checks cover twelve Symbiant visibility/resource/interaction cases and pixel
comparisons, plus paused dense/sparse stress samples at 400 and 4,000 cells.
The independent test suite passes 291 tests. These checks do not establish
Windows behavior or long-session performance. Native audio checks cover 24
tank/electric/tar/toxic action controls, 12 spawn-siren controls and 16 electric/
tank loop controls. Six 128-hit electric bursts preserve absorption and keep
cosmetic queues bounded. Five real spitter impacts also preserve zombie spawning
and positional impact audio while suppressing hidden flash submissions. Static
flash and moving-smoke controls cover sight loss/reveal, hidden movement and
normal expiry; particle pixels and broader explosion effects remain open.
Actual tar production and native zombie/human ranged target restrictions also
pass. Seven normal-playback melee controls record 21 attacks and 16 parries,
with correct bubble visibility, positional sound and natural expiry. Six staged
crowd controls complete normal wall crossings at the default threshold; wall
sounds and delayed warnings also pass their separate settings checks. Three
real right-click feeding jobs complete for human, fresh animal and rotten animal
corpses, adding exactly the advertised cells. Four additional current-sight menu
controls reject hidden cores and hidden corpses while retaining visible choices;
one visible human feed then carries and completes over 212 normal ticks. A
separate human-feed check also survives saving during hauling and a fresh game
restart. Broader special-zombie combat, gas spread/explosions, electrical effects,
unassisted horde formation, other feeding sight ranges and moving-core phases,
full severance surgery and full GPU fallback remain under review.

Seven native area-warning checks cover sight loss, mixed colonist/zombie areas,
and differing Symbiant root/core sight. Natural area classification and actual
warning hover/click behavior still need playtesting.

Twenty-one native Albino checks cover its own AI starting a scream, sight
changes for its meshes and bubble, sound settings, victim effects, damage-job
continuation and natural expiry. The two victims are held passive for this
focused check. Broader combat, equipment/door hacks and interrupted save/load
remain for testing.

Sixteen staged native explosion checks cover ordinary bombs and Zombieland's
suicide-bomb, toxic-splatter and electrical-shock producers. Hidden blasts do
not shake the camera; visible and colony-bypass controls do. Blast-cell work,
damage and heat retain their native behavior. Attack AI and particle pixels
are separate checks.

Native silent-arrival tests cover raids and manhunter packs with the option off
and on, ordinary slowdown outside incidents, and parameter restoration after a
failed or throwing worker. All four arrivals preserve the same spawned pawn
kinds and counts. The new Symbiant enemy-fog acquisition matrix passes five
ranged and five melee sight states. Native melee and ranged fights also deal
shared damage while the player sees one body cell and the root stays hidden;
the active fights survive both in-process reloads and separate full game restarts,
then continue dealing damage without injuring the host. Broader weapons and
additional sight ranges remain open. This does not establish
every modded incident or every combat situation.

Six fresh matched Mac samples at native fourth speed on a 1,000-zombie fixture
with the contamination overlay open and 4,000 staged sparse ground cells
measure median 219.28 TPS for Total Fog versus 203.98 for the original, about
7.50% higher on this current pair. The debug speed boost is off; every measured
tick uses the native fourth-speed multiplier. Frame p95 is 81.27 ms versus
76.89 ms, so this is a TPS improvement with a worse frame tail on this fixture.
The input, camera, settings and Zombieland bytes match; each fog mod keeps its
ordinary visibility policy. This does not compare identical rendered geometry
or prove performance for all maps, mod lists or long sessions. Please try your
usual busy save and report its behavior.

Please try FleshbeastAttack.rws, press Play and watch the Fingerspike deaths.
The replacement completed 900 ticks including a Fingerspike death with no render errors
in the Mac test. The exact Windows "Node is null" error has not reproduced
locally, so its disappearance on Windows still needs confirmation.

Please report alerts that reveal unseen events or fail to appear when they
should, pawns or effects visible through fog, unexpected audio clues, and
noticeable slowdowns. A save, Player.log, short clip and enabled mod list help
reproduce a problem. Explored scenery can change while unseen, as it uses the game's current drawing.
Exact last-observed visual snapshots have been removed to prioritize performance.
This replacement still needs Windows testing. The original mod is the minimum
performance baseline; broader compatibility and performance claims require
matching runtime measurements.
