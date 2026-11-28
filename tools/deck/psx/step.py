"""python3 -I step.py FROM_STATE|- "command args; command args; ..." : load a save state from the run folder's states/
(or boot the disc with -), then run the commands in order. Shots go to its shots/, states to its states/. The run folder
is ~/psxrun on the Deck and work/psx/run on the PC (psxcommon.py).

Buttons: CROSS SQUARE TRIANGLE CIRCLE START SELECT UP DOWN LEFT RIGHT L1 R1 L2 R2, joined with + to press together.
    run N                    N frames, nothing pressed
    hold BTN N               BTN held for N frames
    tap BTN [AFTER]          BTN for 4 frames, then AFTER frames (default 20) released
    taps BTN K AFTER         tap K times
    film PREFIX FRAMES EVERY [HOLD|-] [MASH] [MASH_EVERY]
                             FRAMES frames holding HOLD and tapping MASH every MASH_EVERY (default 30), a shot every EVERY
    shot NAME | save NAME    a screenshot (NAME.png) or a save state (NAME.state)
    peek ADDR LEN | poke ADDR HEX
                             RAM, by PS1 address (hex)
    defeat                   the battle's deployed enemies: HP 0 and Dead, as the remaster harness's defeat does
    scenario HEX             from the state wm: start ReMixed's scenario HEX (the cursor to Mandalia Plains, Circle, and
                             the scenario written into var 0x27 in place of Mandalia's 5); film what follows
    instrument | counters    count the world map's window callbacks (WLDCORE 0x8009E690, 57 types) through stubs at
                             0x800A8000 (free on the world map), then print and reset the counts
    card                     write the memory card to the run folder's card.mcd
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from psxcommon import HOME, SHOTS, STATES, film, log, start  # noqa: E402

state = None if sys.argv[1] == "-" else sys.argv[1] + ".state"
emu = start(state=state, card="card.mcd")
for command in " ".join(sys.argv[2:]).split(";"):
    p = command.split()
    if not p:
        continue
    op = p[0]
    if op == "run":
        emu.run(int(p[1]))
    elif op == "hold":
        emu.hold(p[1], int(p[2]))
    elif op == "tap":
        emu.tap(p[1], int(p[2]) if len(p) > 2 else 20)
    elif op == "taps":
        for _ in range(int(p[2])):
            emu.tap(p[1], int(p[3]))
    elif op == "film":
        hold = p[4] if len(p) > 4 and p[4] != "-" else ""
        mash = p[5] if len(p) > 5 else ""
        film(emu, p[1], int(p[2]), int(p[3]), hold, mash, int(p[6]) if len(p) > 6 else 30)
    elif op == "shot":
        emu.shot(os.path.join(SHOTS, p[1] + ".png"))
    elif op == "save":
        emu.save(os.path.join(STATES, p[1] + ".state"))
    elif op == "peek":
        log(p[1], emu.peek(int(p[1], 16), int(p[2], 0)).hex(" "))
    elif op == "poke":
        emu.poke(int(p[1], 16), bytes.fromhex(p[2]))
    elif op == "defeat":  # the deployed enemies: HP 0, Dead in both status sets (+0x58, +0x1BB)
        for k in range(21):
            at = 0x801908CC + k * 0x1C0
            u = emu.peek(at, 0x1C0)
            if u[1] != 0xFF and u[5] & 0x30 and u[0x2A] | u[0x2B]:
                emu.poke(at + 0x28, bytes(2))
                emu.poke(at + 0x58, bytes([u[0x58] | 0x20]))
                emu.poke(at + 0x1BB, bytes([u[0x1BB] | 0x20]))
                log(f"defeated unit {k} (job {u[3]:02X})")
    elif op == "instrument":  # count each window type's callback calls
        for i in range(57):
            entry = 0x8009E690 + 4 * i
            target = emu.u32(entry)
            if 0x800A8000 <= target < 0x800A9000:
                continue
            lo = 0x9000 + i * 8
            code = [0x3C09800B, 0x8D280000 | lo, 0, 0x25080001, 0xAD280000 | lo, 0xAD240000 | (lo + 4),
                    0x08000000 | ((target & 0x0FFFFFFF) >> 2), 0]
            emu.poke(0x800A8000 + i * 32, b"".join(c.to_bytes(4, "little") for c in code))
            emu.poke(entry, (0x800A8000 + i * 32).to_bytes(4, "little"))
    elif op == "counters":  # print and reset the handler counts (and each one's last argument, and its first word)
        seen = []
        for i in range(57):
            n, a0 = emu.u32(0x800A9000 + i * 8), emu.u32(0x800A9004 + i * 8)
            if n:
                first = emu.u32(a0) if 0x80000000 <= a0 < 0x80200000 else None
                seen.append(f"{i}: {n}x a0 {a0:08X} [a0] {first if first is None else hex(first)}")
        log(p[1] if len(p) > 1 else "counters", "; ".join(seen) or "none")
        emu.poke(0x800A9000, bytes(57 * 8))
    elif op == "scenario":  # from `wm`: the cursor to Mandalia Plains, Circle, and var 0x27 (5 there) made the one asked
        emu.run(60)
        emu.hold("UP", 10)
        emu.run(20)
        emu.hold("LEFT", 10)
        emu.run(20)
        emu.tap("CIRCLE", 0)
        if emu.u32(0x800577B8) != 5:
            sys.exit(f"var 0x27 is 0x{emu.u32(0x800577B8):X}, not Mandalia's 5: `scenario` starts from the state wm")
        emu.poke(0x800577B8, int(p[1], 16).to_bytes(4, "little"))
        log(f"scenario 0x{int(p[1], 16):X} in var 0x27")
    elif op == "card":
        emu.save_card(os.path.join(HOME, "card.mcd"))
    else:
        raise SystemExit(f"unknown command {op}")
log("done, frame", emu.frames)
sys.stdout.flush()
os._exit(0)
