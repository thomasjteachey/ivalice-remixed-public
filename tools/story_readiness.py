"""What each event ReMixed changed needs before the remaster can run it (the story port's count).

    python -I tools/story_readiness.py          # writes docs/generated/story-readiness.md

For every event whose code differs from vanilla's (EVENT/TEST.EVT, 0x2000 bytes each: a u32 text offset, then code),
it lists the ReMixed opcodes the code uses that the code mod doesn't run yet, the PS1 addresses its PEEKs and POKEs
reach that have no place in the remaster yet, and its native calls. It also says whether the event has dialogue
(opcode 0x10) and how much the remaster changed the vanilla event (its own opcodes, such as the voice cues), since
shipping ReMixed's code whole drops the remaster's edits of that event.

The ported set and the address map mirror code/IvaliceRemixed/Patches/RemixedScript.cs (keep them in step).
"""

from __future__ import annotations

import collections
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from psxdisc import Disc  # noqa: E402
from remixed import load_config, remixed_bin, vanilla_bin  # noqa: E402
from tables import FilePair  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
REMASTER_SCRIPTS = ROOT / "work" / "remaster" / "extract" / "all0005" / "script" / "enhanced"
OUT = ROOT / "docs" / "generated" / "story-readiness.md"
BATTLE_BASE = 0x80067000
LENGTHS = 0x8014D170
REMASTER_LENGTHS_FILE = 0x67FF00  # .rdata 0x680F00 in FFT_enhanced.exe

# RemixedScript.Ours / Done.
OURS = ({0x14, 0x15, 0x17, 0x2F, 0x8D, 0xBF, 0xE0} | set(range(0x00, 0x10)) | set(range(0x9A, 0xA0))
        | set(range(0xAC, 0xB0)) | set(range(0xC1, 0xC8)) | set(range(0xCC, 0xD0)) | set(range(0xE6, 0xF0)))
PORTED = set(range(0x00, 0x10)) | {0x14, 0x2F, 0x8D} | set(range(0x9A, 0x9F)) | {0xAC, 0xAD, 0xAE, 0xAF, 0xBF, 0xC1,
                                                                    0xC2, 0xC3, 0xC4, 0xC7, 0xCD, 0xCE, 0xCF, 0xE0,
                                                                    0xE6, 0xE7, 0xE8, 0xE9}
NATIVES = {0x80151000, 0x80160600}  # RemixedEvents' LevelScalingRerun and RosterSwap
UNITS, UNIT_SIZE, ROSTER, ROSTER_SIZE = 0x801908CC, 0x1C0, 0x80057F74, 0x100
GLOBALS = ({0x80193844, 0x80193845, 0x80193920} | set(range(0x8004E5D0, 0x8004E5D0 + 0x6EE))  # level sums, map settings
           | set(range(0x80165EF4, 0x80165EF4 + 0x400))  # ReMixed's words (variables 0xB00-0xB7F, its list)
           | set(range(0x800475E0, 0x800475E0 + 0x20)) | set(range(0x80049A18, 0x80049A18 + 0x200))  # conditions
           | set(range(0x80045944, 0x80045950))  # the buttons: held, the frame before, pressed
           | set(range(0x8005771C, 0x8005771C + 0x2AC))  # the event variables: words, flags, fields
           | set(range(0x80065CB4, 0x80065CB4 + 0x100))  # the skillset types
           # the units' snapshot (RemixedEvents.MapUnitPlaces): statuses, their second copy and counters, +0x15D, +7, +5
           | set(range(0x8019F9B4, 0x8019F9B4 + 0x13B)) | set(range(0x8019F9B4 + 0x20D, 0x8019F9B4 + 0x39C)))


def unit_mapped(off: int) -> bool:
    # The upgrade's Ps1Memory.UnitField (code/eiu): since its b77b6bd also raw stats, unlocked jobs, learned abilities,
    # job levels, JP and total JP.
    return (off < 0x21 or 0x21 <= off <= 0x26 or 0x28 <= off <= 0x71 or 0x8B <= off <= 0x95
            or 0x15C <= off <= 0x164 or off in (0x16C, 0x16D) or 0x182 <= off <= 0x18C or 0x1B8 <= off <= 0x1BF
            or 0x72 <= off <= 0x80 or 0x96 <= off <= 0x12B
            or off == 0x169)  # ReMixed's unit preset: the code mod's (RemixedEvents.MapUnitPlaces)


def roster_mapped(off: int) -> bool:
    return off < 0x67 or off in (0xCE, 0xCF, 0xD0)


def instructions(code: bytes, lengths: bytes):
    at = 0
    while at < len(code):
        op = code[at]
        yield at, op, code[at + 1: at + 1 + lengths[op]]
        if op == 0xDB and at + 1 >= len(code):
            break
        at += 1 + lengths[op]


def event_code(block: bytes, lengths: bytes) -> tuple[bytes, bool]:
    """The code, and whether the event has text of its own."""
    text = int.from_bytes(block[:4], "little")
    if 4 <= text <= 0x2000:
        return block[4:text], any(block[text:text + 64])
    at = 4
    while at < 0x2000 and block[at] != 0xDB:
        at += 1 + lengths[block[at]]
    return block[4:min(at + 1, 0x2000)], False


def accesses(code: bytes, lengths: bytes):
    """(kind, detail) for each PEEK/POKE the code makes, by a straight-line guess at its base variable."""
    val: dict[int, tuple[str, int] | None] = {}
    patched = False  # the instruction before was an 0xE6 writing this one's offset (parameter bytes 1-2)
    for _, op, p in instructions(code, lengths):
        if len(p) < lengths[op]:
            break
        offset_patched, patched = patched, op == 0xE6 and (p[0] & 0x3F) in (1, 2)
        if offset_patched and op in (0xC1, 0xC4):
            # The offset is only known at run time: a byte table's index (the base's first byte stands in when the
            # base is a table with a place), else counted as at run time.
            base = val.get(p[3] & 0x7F)
            if base is None or describe(base[1]) is not None or base[1] in (UNITS, ROSTER):
                yield "runtime", None
                if op == 0xC1:
                    val[p[0]] = None
                continue
            p = bytes([p[0], 0, 0]) + bytes(p[3:])
        if op in (0xAC, 0xAE):
            v = p[2] | p[3] << 8
            val[p[0]] = ("abs", v << 16 if op == 0xAC else v)
        elif op == 0xAF:
            val[p[0]] = val.get(p[2])
        elif op == 0xB0:
            v = val.get(p[0])
            val[p[0]] = (v[0], (v[1] + (p[2] | p[3] << 8)) & 0xFFFFFFFF) if v else None
        elif op == 0xBE:
            val[p[0]] = ("abs", 0)
        elif op in (0xC2, 0xC5):
            val[p[1] & 0x7F] = ("unit", UNITS)
        elif op == 0xC3:
            val[p[1] & 0x7F] = ("roster", ROSTER)
        elif op == 0xE7:
            val[0] = ("unit", UNITS)
        elif op in (0xC1, 0xC4):
            base = val.get(p[3] & 0x7F)
            off = p[1] | p[2] << 8
            size = 1 if p[4] == 0 else min(p[4], 4)
            if base is None:
                yield "runtime", None
            else:
                for k in range(size):
                    yield "address", (base[1] + off + k) & 0xFFFFFFFF
            if op == 0xC1:
                val[p[0]] = None
        elif 0xB1 <= op <= 0xBD:
            val[p[0]] = None


def describe(address: int) -> str | None:
    """None when the address has a place; else what it is."""
    if 0 <= address - UNITS < 21 * UNIT_SIZE:
        off = (address - UNITS) % UNIT_SIZE
        return None if unit_mapped(off) else f"unit +0x{off:X}"
    if 0 <= address - ROSTER < 20 * ROSTER_SIZE:
        off = (address - ROSTER) % ROSTER_SIZE
        return None if roster_mapped(off) else f"roster +0x{off:X}"
    if address in GLOBALS:
        return None
    if address < 0x80000000:
        return "low (a missing unit)"
    return f"0x{address & ~0xF:08X}"


def main() -> None:
    cfg = load_config()
    with Disc(vanilla_bin(cfg)) as v, Disc(remixed_bin(cfg)) as p:
        files = FilePair(v, p)
        battle_old, battle_new = files.get("BATTLE.BIN")
        old_evt, new_evt = files.get("EVENT/TEST.EVT")
    exe = (Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe").read_bytes()
    lv = battle_old[LENGTHS - BATTLE_BASE: LENGTHS - BATTLE_BASE + 256]
    lr = battle_new[LENGTHS - BATTLE_BASE: LENGTHS - BATTLE_BASE + 256]
    lm = exe[REMASTER_LENGTHS_FILE: REMASTER_LENGTHS_FILE + 256]
    remaster_only = {op for op in range(256) if lm[op] != lv[op]}

    rows = []
    blockers = collections.Counter()
    for n in range(len(new_evt) // 0x2000):
        new_block, old_block = new_evt[n * 0x2000:(n + 1) * 0x2000], old_evt[n * 0x2000:(n + 1) * 0x2000]
        code, has_text = event_code(new_block, lr)
        old_code, _ = event_code(old_block, lv)
        if code == old_code or not code.strip(b"\0") or code.rstrip(b"\0") == old_code.rstrip(b"\0"):
            continue  # unchanged, or vanilla's code padded with zeros after its END up to the text
        ops = collections.Counter(op for _, op, _ in instructions(code, lr))
        messages = ops[0x10]
        unported = sorted(op for op in ops if op in OURS and op not in PORTED)
        natives = sorted({int.from_bytes(p, "little") for _, op, p in instructions(code, lr) if op == 0xCF and len(p) == 4}
                         - NATIVES)
        missing = collections.Counter()
        runtime = 0
        for kind, address in accesses(code, lr):
            if kind == "runtime":
                runtime += 1
            else:
                what = describe(address)
                if what:
                    missing[what] += 1
        remaster = REMASTER_SCRIPTS / f"event{n:03d}.e"
        edits = "no .e"
        if remaster.exists():
            mine = remaster.read_bytes()  # padded with leftover bytes after its END
            at = 0
            while at < len(mine) and mine[at] != 0xDB:
                at += 1 + lm[mine[at]]
            mine = mine[:at + 1]
            mops = collections.Counter(op for _, op, _ in instructions(mine, lm))
            own = sum(c for op, c in mops.items() if op in remaster_only)
            edits = ("as vanilla" if mine == old_code.rstrip(b"\0") else f"{own} of its own" if own
                     else "reworked")
        ready = not unported and not natives and not missing
        for op in unported:
            blockers[f"opcode 0x{op:02X}"] += 1
        for a in natives:
            blockers[f"native 0x{a:08X}"] += 1
        for what in missing:
            blockers[what] += 1
        rows.append((n, len(code), messages, has_text, edits, unported, natives, missing, runtime, ready))

    ready = [r for r in rows if r[9]]
    talking = [r for r in rows if r[2]]
    lines = [
        "# Story port: what each ReMixed event needs",
        "",
        "Generated by `python -I tools/story_readiness.py`. For each event whose code ReMixed changed: the ReMixed",
        "opcodes it uses that the code mod doesn't run yet, its native calls not ported, and the PS1 places its PEEKs",
        "and POKEs reach that have no place in the remaster (a guess from the code read straight through; \"at run",
        "time\" counts the ones whose address is only known then). \"Messages\" counts its dialogue instructions",
        "(0x10); \"Remaster\" is how the remaster's own `.e` for that event differs from vanilla's, which shipping",
        "ReMixed's code whole would drop (its voice cues and timing among them).",
        "",
        f"**{len(rows)} events changed; {len(ready)} need nothing more** (by this reading); {len(talking)} have dialogue.",
        "",
        "## What blocks the most events",
        "",
        "| Missing | Events |",
        "|---|---|",
    ]
    lines += [f"| {what} | {count} |" for what, count in blockers.most_common(40)]
    lines += ["", "## Each event", "",
              "| Event | Bytes | Messages | Remaster | Opcodes to port | Natives | Places missing | At run time |",
              "|---|---|---|---|---|---|---|---|"]
    for n, size, messages, has_text, edits, unported, natives, missing, runtime, ok in rows:
        lines.append(
            f"| 0x{n:03X}{' ✔' if ok else ''} | {size} | {messages} | {edits} | "
            f"{', '.join(f'0x{op:02X}' for op in unported) or '—'} | "
            f"{', '.join(f'0x{a:08X}' for a in natives) or '—'} | "
            f"{', '.join(f'{w} ×{c}' for w, c in missing.most_common(6)) + (' …' if len(missing) > 6 else '') or '—'} | "
            f"{runtime or '—'} |")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(rows)} events changed, {len(ready)} ready, {len(talking)} with dialogue; wrote {OUT.relative_to(ROOT)}")
    for what, count in blockers.most_common(15):
        print(f"  {count:4}  {what}")


if __name__ == "__main__":
    main()
