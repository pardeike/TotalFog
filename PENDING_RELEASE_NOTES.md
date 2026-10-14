# Pending release notes

Player-visible changes for the unreleased 0.1.0 version. Source commits and
local testing do not indicate that a new player build has been released.

## Vision and information

- Keep new colonies unexplored until an actual sight source sees them; do not reveal the landing area before colonists arrive.
- Support RimWorld 1.6 and 1.7 through the same Current/ payload.
- Use native drawing for previously observed scenery, without expensive archives of its last appearance. Fresh objects stay hidden until observed.
- Keep owned pawns controllable, labelled and available in menus even when they provide no shared vision. Their presence does not reveal nearby things or hidden effects.
- Require current sight for other live labels, tooltips, overlays, selection, inspection, environment details and target information. Close live inspection when sight is lost.
- Filter map search while retaining remembered static things and your pawns' inventories. Never-seen objects, hidden enemies and hidden enemy inventories remain excluded.
- Preserve deferred letters and messages across saves, with their sounds, targets, recipient factions and map clocks. Vanished targets retain ordinary History/archive behavior.
- Apply unseen-event settings to pre-arrival refugee pod letters, ordinary white messages and selected hidden-threat alerts and jump targets. Colony health, global events and visible notifications remain immediate.
- Delay Anomaly codex discovery and its notice until the entity is first seen.
- Add optional Silent Raids: suppress raid/manhunter arrival letters and arrival slowdown while retaining spawning and normal combat slowdown.
- Keep gravship previews from permanently exploring cells or granting live effects, notification eligibility or sound-source sight.
- Keep splitting fleshbeasts, held/flying pawns, projectiles, corpses, gas, explosions and artificial ground light behind their appropriate current-sight gates.
- Retain owned blueprints, Forbid/Allow area orders for observed player doors, and known home-area work without opening hidden live inspection.
- Keep owned blueprints and construction frames selectable and editable outside current sight, including drag selection and ordinary construction commands.
- Prevent hidden resources and pawns from taking selection priority away from visible things and your construction plans, including click cycling, double-click and drag selection.
- Preserve existing haul orders for known plain chunks. Let native construction clear hidden haulable items, plants and buildings blocking owned blueprints/frames on known terrain. Storage hauling uses that same work-permission rule; native construction still chooses its ordinary haul-aside job.
- Allow cleaning fresh dirt in a known home area without revealing it.

## Watchtowers and cameras

- Add an experimental 3x3 watchtower with one elevated lookout, a timed ladder, configurable sight, optional shooting, rescue and collapse injury.
- Require Complex furniture and heavy ground support. Completed towers block all nine cells; previews remain walkable and use translucent native-style blueprints. New ladders face the player by default.
- Render placed watchtower blueprints even before construction materials are available.
- Offer one-, two- and four-hour watches counted from reaching the deck, or drafted holding until undrafted or ordered away. Preserve timers across saves and respect explicit orders, draft transitions and Fire at will.
- Let drafted Go here and dragged formations use tower platforms, including Achtung's move orders. Ground routes begin at the ladder after descent; idle undrafted pawns step clear while retaining queued work.
- Face the ladder in both directions. Undrafted lookouts turn through all four directions; idle drafted lookouts face the player while combat aiming retains priority.
- Connect actual adjacent wall graphics to the solid base without unwanted end caps.
- Keep a visible tower's base, raised deck and lookout free of fog clipping while hiding the background. Dim remembered towers consistently.
- Keep raised bodies, labels, selection, effects and projectiles aligned. Preserve native trajectories, target cover, shield absorption and fire resistance; tall obstacles still block elevated shots.
- Preserve occupied towers, downed occupants, corpses, rescue and carrying state through saves. Warn about blocked exits, protect ladder access and permit retrieval without requiring a bed. Desperate exits and collapse can cause fall injuries.
- Give cameras a separate Cameras work priority. Choose working consoles, release operators when their console stops, and avoid console work when the operator requirement is disabled.
- Set telescope sight independently of tower sight and restore portable camera textures on macOS.
- Retire Chicken on a Stick, the other inspector animals, security bells, light beacons and old watchtowers. Saved objects remain loadable without their former sight, light or fuel consumption.

## Audio and settings

- Allow selecting remembered buildings and objects by default while they remain in their last observed location. Turn off Select remembered objects to require current sight. Unknown objects and hidden pawns stay unavailable.
- Keep hidden tooltips blocked on engines that treat invalid map cells as unfogged.
- Suppress Combat and Anomaly music by default while retaining ordinary music.
- Add optional hidden-source muting, hearing-distance filtering and hearing indicators. Living faction listeners supply hearing; general ambience stays native.
- Apply audio choices to existing loops and restore normal volume when filtering is disabled. Reuse hearing checks within a map tick.
- Organize Appearance, Vision, Information and Audio settings with a fixed footer, clear defaults and space beside the scrollbar. Preserve saved choices and explain intentional information cues.
- Add a separate colony-mech vision multiplier, keeping normal range by default. Keep tree-blocking choices disabled while a game is loaded.
- Apply native Clear All Fog, Refog Map and rectangle fog commands to Total Fog. Settle fog fades while paused so scrolling does not leave stale straight section edges.
- Refresh sight blockers and observers immediately for debug fog commands, including walls placed while paused.
- Restore real sight after Refog even when the colonist stands on a tower.
- Translate current settings and tower text into German, French, Simplified Chinese and Korean.

## Integrations and stability

- Apply Combat Extended targeting to each observing faction while preserving native ballistics, failed checks, power/manning sight, mortar blind-cell fire and last-known-cell bursts. Do not track unseen Things during firing.
- With the updated Zombieland, clip Symbiant bodies and their moving cores by visible cells, permit visible-core inspection with a hidden root, and restrict body-cell targeting by faction sight.
- With the updated Zombieland, filter source-specific effects, readouts, highlights, warnings and sounds through the optional integration. Keep each mod usable on its own.
- Retain Multiplayer synchronization for settings, player orders, faction knowledge, hearing, map clocks and saved sessions. Overall Multiplayer compatibility remains unverified.
- Keep one unavailable optional hook, broken listener or failed delayed notification from preventing unrelated fog behavior. Avoid startup failures from an inactive bundled Multiplayer API.
- Initialize sight after native blockers and lighting are ready. Preserve updates across larger tick intervals, movement, map/faction changes and callback refresh requests.
- Reduce recurring sight, footprint, registration and mesh work. Discovery dirties terrain/roof/fertility meshes; ordinary sight changes avoid unrelated rebuilds. Skip enemy sight work when enemy fog is disabled.
- Correct rectangular-map and shared-section boundaries, rare-ticking blockers and sources, power-grid rendering during flyer loading, and the renderer's ownership of held/flying things.
