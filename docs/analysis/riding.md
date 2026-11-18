# Riding: ReMixed's rework and the remaster's riding, side by side (2026-11-04)

The plan for porting ReMixed's chocobo-riding rework. Read from ReMixed's PS1 code (BATTLE.BIN unless marked) and the
remaster's (FFT_enhanced.exe RVAs), not tried in the game. Unit offsets are PS1's; the remaster's link byte is +0x1B4
(PS1 +0x182, + 0x32 from there on). A rider's link holds `0x80 | mount`, a mount's `0x40 | rider`.

## In one paragraph

In vanilla, and in the remaster unchanged, a ridden mount is frozen: no CT, no turns, no commands, it can't react or be
targeted, and it only walks for its rider. ReMixed keeps it from taking its own turns too, but makes it useful: in each
of its rider's turns the rider may use the mount's skillset once, as an extra action (instant, with the mount as the
attacker, and the EXP and JP going to the mount). A rider that acts first may still use the mount's skill; using the
mount's skill first ends the rider's actions. Train converts an enemy mount at the rider's turn start, and only a
rider with Pass and Train can board an enemy's chocobo in the first place. The White Chocobo (job 0x95) is a mount too.

## The pieces

| # | Piece | ReMixed (PS1) | The remaster | Size |
|---|---|---|---|---|
| 1 | The White Chocobo is a mount | 0x801743C8 adds job 0x95 to 0x5E–0x60 | The mount test is inlined: the movement setup's free-mount rule (0x27B187–0x27B1B7) and the unit-at-tile lookup 0x282A7C (0x282B59, flag 0x80) | small |
| 2 | Who can board | The movement setup's per-unit tile flags (0x801749A4 → 0x8015A2D0, 0x80152860): a monster mover can't stop on any unit; an enemy's tile is 0x60 (an enemy's free chocobo can't be boarded); Pass clears 0x40 everywhere; Pass and Train clear 0x20 on unlinked units | 0x27ADB8's unit records (0x27B165–0x27B1B7, the free-mount rule; Pass's stub already sits at 0x27B1D7, `Patches/Pass.cs`) | small (with Pass's stub) |
| 3 | The mount's skillset in the rider's menu | 0x801813C0: 0x8015A200 adds the mount's primary (+0x12) at the end for a non-monster rider whose mount hasn't acted (+0x188 = 0); 0x8015A100, 0x8015A140, 0x8015E260 hide Attack, Defend, Equip Change and the rider's own skillsets once the rider has acted (+0x188 & 9 = 8); 7 entries | The command list 0x30E010 is already hooked (`Patches/TwoSpells.cs`, `DragonTech.cs`): the extra entry and the hiding go there. The list builder for a monster skillset (the remaster's equivalent of 0x801822CC) for the rider | medium |
| 4 | The rider's turn state | Turn start (0x80182B64 → 0x8015E018): mount CT 100 and its +0x187/+0x188 cleared. Commit (0x8017C8A8 → 0x8015E3F0): using the mount's skill sets mount +0x188 = 0x80 and its CT 0, the rider's 0x04, and ends the rider's actions if it already acted. Action end (0x80183C2C → 0x8015F100): after the rider's own action, +0x188 = (bits \| 0x08) & 0x7D, so the turn goes on with only the mount's skill left | Turn start 0x30F8D4 (the turn loop 0x30F030), the queue 0x281488 (already hooked, `Patches/Rules.cs`'s queue and Two Spells), the turn's mark 0x310548 and the action end (as Two Spells' turn rule). Two Spells already uses +0x188 bit 0x08 for its pair: the rider's states have to share it as ReMixed does (bit 0x08 for both, told apart by the link) | medium |
| 5 | The mount as the attacker | The formula call 0x8018B974 → 0x80160F00: for a rider using a 0xA8+ skillset, the attacker becomes the mount (and its result), the weapon fields are zeroed, then the team-only check and the formula, then everything is restored | Every formula table entry already goes through a team-targeting stub (`Patches/Rules.cs`, the team-only rule): the swap goes in the same wrapper | small–medium |
| 6 | No charge time, no move lock | 0x8017C878 → 0x8015E370 (CT 0); 0x8015E3F0 (no lock) | The queue's CT (Two Spells' queue hook); the charge lock is already ported (`Patches/ChargeLock.cs`) | small |
| 7 | A charging rider keeps its charge | The queue parks the rider's pending charge (+0x15D, +0x16E) on the mount (0x8015DE00/0x8015DE40); the action start doesn't clear the rider's Charging while parked (0x8015F400); the action display gives it back (0x8007781C → 0x8015DD00) | The queue (hooked; the record +0x1A0 and CT +0x18D), the action start 0x2828E4 (hooked by Two Spells, which already keeps Charging for its second slot), and the end of the rider's turn instead of the display | medium |
| 8 | EXP and JP to the mount | The award (0x8017E86C → 0x8015E150: a non-monster rider whose action's skillset is 0xA8+ hands the award to its mount) and the EXP amount (0x8018D620, the mount's level) | The award 0x28338C and the action EXP step, both already hooked (`Patches/Gains.cs`) | small |
| 9 | Train converts an enemy mount | At the rider's turn start: a mount on another team (+0x1BA & 0x30) under a rider with Train (+0x91 & 0x80) takes the rider's team bits (+0x1BA, +5) and Invite (+0x59, +0x1BC \| 0x40) | Turn start 0x30F8D4 | small |
| 10 | Unlinking | Placement (0x8017FDDC) hooks: a dead rider's CT goes to its mount (0x8015DF20, at the unlink); at boarding (the link set, 0x8017FEF4), 0x80156000 saves the mount into ReMixed's own move undo (0x80153F64; not ported, the remaster has its own Reset Move) and 0x8015F300 squares the pair's move flags in ReMixed's own +0x187/+0x188 scheme (a rider boarding a mount that has moved counts as moved; a rider that had acted hands the mount 0x80) | The place-unit routine 0x27192C (the only link writer; 24 callers; index, x, y, level and facing in four registers) | medium |
| 11 | The action window names the mount | 0x80071E18 → 0x8015E100 | Battle UI (rebuilt) | display |

Already ported and riding-aware: water movement for linked units (`MovementMode.cs`), knockback skips linked units
(`Knockback.cs`), the charge lock's exemption for riders (`ChargeLock.cs`), the AI's view of Transparent units
(`InPlace.cs`), Move-Cleanse's Don't Act toggle.

## The remaster's riding, briefly

- No Ride or Dismount command: a unit boards by ending a walk on a free chocobo (the movement setup lets it stop there;
  the path 0x27C7B4 and the walk end 0x1FD2D0 pair the sprites), and the place-unit routine 0x27192C sets both links.
  Placing a rider anywhere else unlinks it; a dead or crystal rider is placed on a free tile (the apply step's −1, then
  0x283784 and 0x268F68).
- The ridden mount: the clock check 0x30FB9C answers "can't" (no CT, never picked), the turn-order list leaves it out
  (0x30FE18), the command list gives it nothing (0x30E010, unless a test flag at 0x185670C), it can't react
  (0x30DD1C) or be targeted (0x280FB4), and a single target on the pair's tile is the rider (0x281BCE).
- The pair moves with the mount as the walker (0x20AC92, 0x20AF7B; the mount's Move, Jump and movement abilities), the
  rider placed on top at the walk's end (0x20B90C). The AI plans with the mount's Move and range.
- A rider can't receive most statuses (the mask .rdata 0x6829E4 in the status-add filter 0x3052DC, as on the PS1), and
  its Brave stays at 10 or more (0x30A690).

## Order

1. Pieces 1 and 2 (boarding rules) and 9 (Train): **ported** (`Patches/RidingBoarding.cs`, rule
   "riding-boarding"; the turn loop's two turn starts, 0x30F8D4 and 0x30F95C, call Train's check).
2. Pieces 3–6 together: **ported** (`Patches/RidingActions.cs`, rule "riding-actions"), through Two Spells' hooks on
   the command list, the turn's action mark, the queue and the end of turn, and the formula wrapper for the attacker
   swap. The state is kept in arrays, not in the action byte's 0x04 and 0x08 as ReMixed does. Open: the remaster's
   ability list for a monster skillset built for a human rider (ReMixed's 0x801822CC changes aren't mapped there).
3. Piece 7: **ported** (`RidingActions`: parked at the commit, kept Charging through Two Spells' action-start hook,
   given back at the end of the rider's turn, which comes before any countdown can tick it; Two Spells' second
   slot isn't emptied by the mount's skill). Pieces 8 and 10's dead rider: **ported** (`RidingActions.ActingMount`, used by `Patches/Gains.cs`' award and
   EXP hooks; a hook on the place-unit routine 0x27192C gives a dead rider's CT to its mount before the unlink).
   Left: 10's boarding bookkeeping, which belongs to ReMixed's own move flags and move undo; the remaster's moved
   flag (+0x1B9) and its own undo may already behave (to check in the game).

## Open questions

- Who pays MP for the mount's skill: ReMixed's action start charges the actor, which looks like the rider (Choco Cure
  costs 5 MP). Worth seeing in ReMixed.
- What happens when a ridden mount dies or is petrified (neither game unlinks it then).
- Whether every player move uses the mount as the walker (one path, 0x20A831, shows the range with the active sprite).
- The AI: ReMixed doesn't seem to offer AI riders the mount's skillset.
- ReMixed bugs to leave out: Two Spells' slot moved with unscaled indexes (0x8015EE80), and any 0xA8+ skillset of a
  rider (a story character's own) counting as the mount's in the attacker swap.
