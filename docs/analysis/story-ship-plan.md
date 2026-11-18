# Which of ReMixed's events to ship

Planned 2026-11-05, under the rule that the remaster's scenes, dialogue and voice acting stay and only ReMixed's
event logic comes over. Numbers from `tools/story_readiness.py`, a diff of each event's instructions against
vanilla's, and a comparison of the two games' scenario tables and event text.

**Outcome: nothing more ships whole, and running ReMixed's gameplay beside the remaster's scenes (the plan's first
version) isn't built: it would apply the wrong battle's setup.** ReMixed's gameplay in story battles comes over
through its battle data (done) and the code mod's mechanics; its event scripts belong to its own numbering and
flow of scenes. Random battles' 0x191, which doesn't depend on either, stays shipped.

## What ReMixed changed

290 events differ from vanilla in their code. The remaster changed the code of 181 of them (its own instructions,
around the dialogue) and left 109 as vanilla's; its text for all of them is its own translation.

| Events | What ReMixed has in that slot | Its text | Ready today |
|---|---|---|---|
| 276 | A scene with dialogue: lines, staging and battle setup | all changed | 178 |
| 7 | Nothing (1 byte): 0x000, 0x0C8, 0x0CA, 0x0CB, 0x0CC, 0x10B, 0x194 | — | 7 |
| 2 | A silent event where vanilla has a 6-byte stub: 0x092, 0x0A9 | 0x0A9 has some | 1 |
| 2 | A silent event: 0x088, 0x191 (random battles, shipped) | — | 0 |
| 3 | A short scene without dialogue: 0x093, 0x0C1, 0x0C7 | — | 2 |

Across the 290, ReMixed added or changed about 3,500 dialogue instructions, 67,000 scene instructions and 59,000
gameplay ones (variable sets, PEEK/POKE, its control flow).

## ReMixed renumbered its events

An event's number is its scenario's (var 0x27). ReMixed rewrote 464 of the 480 records of the scenario table
(ATTACK.OUT 0x10938, 24 bytes each: +5 the map, +7 the battle entry) and merged vanilla's runs of short scenes into
one event each: its event 2 is vanilla's 3–6 (all at Orbonne, battle entry 0x183), its 4 is vanilla's 9–0xC
(Mandalia), its 5 vanilla's 0xD–0xE. Its battle entries follow its own numbers (0x101, 0x102, 0x104…). Its later
slots hold its own story order and its new battles (from 0xA8 on, entries 0x19B and up, which vanilla doesn't
have).

So ReMixed's event N is rarely the remaster's event N. Matching each event's text by its rarer words against all
of vanilla's events, the best match is the same slot for 1 of the 179 events that have text in both; 0x04A's is
vanilla's 0x113 word for word. The readiness report still measures what each event needs, but its "Remaster"
column compares with the remaster's event of the same number, which is mostly another scene.

## Why the gameplay can't run beside the remaster's scenes

The first version of this plan ran ReMixed's event N's gameplay (scene and dialogue instructions skipped) when the
remaster starts its event N. Leaving the numbering aside (a map through the battle entries could pair most battles),
what that gameplay is:

- **The battle event conditions (0xCE, 262 events)** say when a battle's mid-battle scenes and its end fire. They are
  written for ReMixed's own event code, which branches on them; the remaster's event for that battle is vanilla's
  and expects vanilla's conditions. Loading ReMixed's could fire its scenes at the wrong time or not end the battle.
- **The map settings (0xCD, 257)** are per-scenario words indexed by var 0x27, in ReMixed's numbering.
- **Improved Level Scaling's re-run (98)**: the code mod already re-runs the enemies' levels in every battle at the
  first countdown; the events only add the opt-out (var 0x70 0xFEFE).
- **The unit POKEs (175)** find units by their event ids in ReMixed's battle entries; the remaster's battles keep
  vanilla's entries with ReMixed's builds applied (the ENTD port), so the ids don't always line up.

So the sidecar would add little that's safe and risk breaking story battles.

## Shipping whole

The same numbering problem rules out the rest:

- **0x092, 0x0A9** fill slots that are stubs in vanilla, but they're ReMixed's scenes for its own scenario 0x92 and
  0xA9. The remaster reaches those slots at vanilla's scenario, so they would play at the wrong moment.
- **0x088** replaces vanilla's 0x088 with ReMixed's scene for its own scenario 0x88.
- **The 7 events ReMixed emptied and the 3 it cut the dialogue from**: the remaster's scenes and lines stay.
- **0x191**, random battles: shipped. It is the same event in both, with no scenario of its own.

## What would bring more of ReMixed's story over

Only ReMixed's own story flow as a whole: its scenario table, its events, its battle entries and its new battles
together. That replaces the remaster's scenes and needs ReMixed's text. Its new battles (26 entries only its events
reach) have no remaster scene to lose, but they need ReMixed's own lines or none. That breaks the current rule, so it
needs a decision; it isn't planned.
