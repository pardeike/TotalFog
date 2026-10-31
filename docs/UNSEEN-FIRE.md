# Reaction to unseen weapon fire

The optional **Enemies have fog too** setting can leave the combat AI without a
target when an attacker fires from outside its sight. The requested response is
to investigate the firing area while keeping the attacker hidden.

Report: [Richard Luo, 31 October 2026](https://discord.com/channels/900081000942567454/1390493088358858762/1565994917929680978).
Andreas confirmed that the attacker should remain unseen.

## Behavior

- A successful ranged shot directed at an eligible enemy pawn records a rough firing area for that pawn.
- The firing verb must declare a cast, tail or beam sound. Player audio suppression does not affect this check.
- The pawn must be awake, mobile, hostile to the caster, capable of violence and capable of hearing.
- The firing area is the center of a 5×5-cell square. It does not follow the attacker.
- The memory lasts at most 600 game ticks, or ten seconds at normal speed.
- The normal combat job takes priority. If it returns no job and has no acquired target, it can investigate the area.
- Investigation respects the combat node's guard radius, explicit duty boundaries, native reachability and destination reservations.
- Movement jobs reconsider their orders after at most 120 ticks. Reaching the area or losing eligibility clears the memory.
- The memory stores no shooter, map or faction object. It grants no visibility, targeting permission or shared faction knowledge.
- The memory is transient. Save/load clears it without adding save data.

The implementation handles directed weapon fire, including misses. It does not
scan nearby pawns or make all environmental sounds affect the simulation.

## Mod compatibility

The implementation uses `Verse.Verb` and `JobGiver_AIFightEnemy`. It has no lists
of race names, weapon names, pawn classes or mod IDs. Modded pawn subclasses,
nonhuman pawns and guard overrides have source-linked regression tests.

Weapons that use the standard successful-shot path share the firing hook.
Native projectile and beam verbs both call that path in the inspected engine.
The actual verb's sound properties determine whether the shot makes noise.

An AI that replaces the combat job giver entirely retains its own behavior.
An entirely separate firing implementation does not automatically use this hook.
These cases need checks in the affected mod. They are not universal compatibility
claims for Combat Extended, Zombieland or every custom AI.

The hook validates its instruction shape. If that shape changes, it retains the
original method and reports that the reaction is unavailable.

## Validation

The TDD red run failed the positive reaction controls before implementation.
The full source suite passes 606 tests after the change. Added controls cover
failed shots, silent weapons, beam and tail sounds, custom pawn subclasses,
nonhuman pawns, guard overrides, existing jobs, expiry, map/faction changes,
unreachable or reserved cells, pacifists, and reaching the remembered area.

The native fixture uses RimWorld 1.7.4995 rev309, all five DLCs, Workshop Harmony
3.0.1, RimBridgeServer 2.3.1 and the candidate Total Fog. It compares silent and
audible rifle shots against a hidden enemy. It checks the native combat fallback,
the fixed cell destination after the shooter moves, and actual movement over
45 ticks. It then reloads the unchanged baseline save.

The repeatable companion tool is `totalfog/unseen_fire`, with the name of a paused
initialized single-player baseline save. Use an isolated profile. The tool restores
its settings and reloads that save.

All 13 native assertions pass on gameplay MVID `9dd8bf6c-3ee5-4ba7-ad0f-3ce802fcfd6a`.
The gameplay SHA-256 is `9df8c7648240631612dd05a274277c3f46bbfe40f6b5692c8883a1c857f973b8`.
Local evidence is under ignored `artifacts/retaliation-20261031/`, including
`native-final.json` and `final-build.log`.
The 1.6 payload remains frozen. This feature is part of the current 1.7 source
work and has not been published or delivered to a tester.
