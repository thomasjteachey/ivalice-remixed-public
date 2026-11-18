"""List an event script's instructions from ReMixed's (or vanilla's) EVENT/TEST.EVT.

    python -I tools/events.py 0xC9            # ReMixed's Extra Options event
    python -I tools/events.py 0xC9 --count 80
    python -I tools/events.py 0xC9 --vanilla
    python -I tools/events.py 0x11C --text    # the text of ReMixed's Combat Adjustments menus

Each event is 0x2000 bytes: a u32 offset to its text, then its code. Instruction lengths come
from the battle event interpreter's own table (BATTLE.BIN 0x8014D170 + opcode: the parameter
bytes after the opcode), which ReMixed extends for its new opcodes. Variable ids below 0x80 are
the 4-byte event variables (0x8005771C + 4 × id); 0x80 and up are flags (bits from 0x8005791C).
ReMixed's variable opcodes 0xAC/0xAD set var = value << 16 and 0xAE/0xAF set var = value
(the odd ones take another variable); 0xB0–0xBE are vanilla's arithmetic (docs/MECHANICS.md).
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from codemap import LOAD_ADDRESS  # noqa: E402
from fft_text import decode  # noqa: E402
from psxdisc import Disc  # noqa: E402
from remixed import load_config, remixed_bin, vanilla_bin  # noqa: E402
from tables import FilePair  # noqa: E402

LENGTHS = 0x8014D170  # BATTLE.BIN: parameter bytes per opcode
VARIABLE_OPS = {
    0xAC: "SETHI", 0xAD: "SETHI_VAR", 0xAE: "SET", 0xAF: "SET_VAR", 0xB0: "ADD", 0xB1: "ADD_VAR", 0xB2: "SUB",
    0xB3: "SUB_VAR", 0xB4: "MUL", 0xB5: "MUL_VAR", 0xB6: "DIV", 0xB7: "DIV_VAR", 0xB8: "MOD", 0xB9: "MOD_VAR",
    0xBA: "AND", 0xBB: "AND_VAR", 0xBC: "OR", 0xBD: "OR_VAR", 0xBE: "ZERO",
}
OTHER_OPS = {0xA0: "LTE", 0xA1: "GTE", 0xA2: "EQ", 0xA3: "NEQ", 0xA4: "LT", 0xA5: "GT", 0x10: "MESSAGE", 0xDB: "END",
             # Control flow, named from how the options events use them: IF tests var 0 or the last
             # comparison; SWITCH takes a variable, CASE a 16-bit value.
             0x01: "IF", 0x03: "ELSE", 0x04: "ENDIF", 0x05: "SWITCH", 0x06: "CASE", 0x07: "DEFAULT", 0x08: "BREAK",
             0x09: "ENDSWITCH",
             # ReMixed's, from its interpreter (docs/analysis/remixed-event-opcodes.md).
             0x00: "NOT", 0x02: "ELSEIF", 0x0A: "FUNCTION", 0x0B: "RETURN", 0x0C: "CALL", 0x0D: "GOTO", 0x0E: "LABEL",
             0xBF: "RANDOM", 0xC1: "PEEK", 0xC2: "UNIT_ADDRESS", 0xC3: "ROSTER_ADDRESS", 0xC4: "POKE",
             0xC7: "HAS_CHARACTER", 0xCF: "NATIVE_CALL", 0xE6: "PATCH_NEXT", 0xE7: "UNIT_ON_TILE"}


def name(var: int) -> str:
    return f"var 0x{var:X}" if var < 0x80 else f"flag 0x{var:X}"


def print_text(text: bytes) -> None:
    """The event's text, decoded: 0xF8 is a line break, 0xFE ends a message, 0xFB starts a menu's choices."""
    end = text.find(bytes(16))  # the zero padding after the last message
    t, _ = decode(text if end < 0 else text[:end])
    breaks = {"<F8>": "\n", "<FE>": "\n-- end of message\n", "<FB>": "\n", "<FC><FF>": "\n\n", "<FD><FF>": "\n"}
    for code, replacement in breaks.items():
        t = t.replace(code, replacement)
    print(t)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("event", type=lambda s: int(s, 0))
    parser.add_argument("--count", type=int, default=60, help="instructions to list (default 60)")
    parser.add_argument("--vanilla", action="store_true", help="read the vanilla disc")
    parser.add_argument("--text", action="store_true", help="print the event's text instead of its code")
    args = parser.parse_args()

    cfg = load_config()
    with Disc(vanilla_bin(cfg)) as v, Disc(remixed_bin(cfg)) as p:
        old, new = FilePair(v, p).get("BATTLE.BIN")
        disc = v if args.vanilla else p
        test = disc.read(disc.find("EVENT/TEST.EVT"))
    battle = old if args.vanilla else new
    base = LOAD_ADDRESS["BATTLE.BIN"]
    lengths = battle[LENGTHS - base: LENGTHS - base + 256]
    start = args.event * 0x2000
    text_at = int.from_bytes(test[start:start + 4], "little")
    code = test[start + 4: start + text_at]
    if args.text:
        print_text(test[start + text_at: start + 0x2000])
        return
    at = 0
    for _ in range(args.count):
        if at >= len(code):
            break
        op = code[at]
        params = code[at + 1: at + 1 + lengths[op]]
        if op in VARIABLE_OPS and len(params) >= 2:
            var = params[0] | params[1] << 8
            text = f"{VARIABLE_OPS[op]} {name(var)}"
            if op != 0xBE and len(params) >= 4:
                value = params[2] | params[3] << 8
                text += f", {name(value)}" if op & 1 else f", 0x{value:X}"
        else:
            text = f"{OTHER_OPS.get(op, f'op{op:02X}')} {params.hex(' ')}".rstrip()
        print(f"0x{start + 4 + at:06X}  {text}")
        at += 1 + len(params)


if __name__ == "__main__":
    main()
