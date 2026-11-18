"""Look into the remaster's exe (FFT_enhanced.exe) and its decompile, by RVA (the address less the image base, as the
mod's patterns, self-tests and docs give them).

    python -I tools/exe.py dis 0x247E10 [0x80]          # disassembly (capstone) of N bytes
    python -I tools/exe.py func 0x284E4A                # the decompiled function holding an address
    python -I tools/exe.py callers 0x247E10             # the decompiled functions that call it
    python -I tools/exe.py refs 0xD493CC                # the decompiled functions that use a global
    python -I tools/exe.py read q 0x681400 0x3E         # pointers (as RVAs), or d / w / b: dwords, halfwords, bytes
    python -I tools/exe.py find "40 53 48 83 EC 20 48 63 D9 E8 ?? ?? ?? ??"   # where a pattern is (?? any byte)
    python -I tools/exe.py crash 0x7FF7CA154E4A 0x7FF7C9ED0000                 # an absolute address as RVA + function

The decompile is work/ghidra/decompiled/*.c (tools/decompile_game.py), each function headed by
"// ==== 0xRVA NAME (N bytes)"; Ghidra names things by absolute address (FUN_14xxxxxxx, DAT_14xxxxxxx: the RVA plus
0x140000000). Some small functions aren't in it (e.g. the world-map condition opcodes): `dis` reads the exe itself.
Capstone is in the user's site-packages, which -I hides; this adds that folder back for `dis`.
"""

from __future__ import annotations

import argparse
import re
import site
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from remixed import WORK, load_config  # noqa: E402

BASE = 0x140000000  # the exe's preferred image base, which Ghidra's names use
DECOMPILED = WORK / "ghidra" / "decompiled"
HEADER = re.compile(r"^// ==== 0x([0-9A-F]+) (\S+) \((\d+) bytes\)", re.M)


class Exe:
    """The exe's sections, for reading by RVA."""

    def __init__(self, path: Path) -> None:
        self.data = path.read_bytes()
        pe = struct.unpack_from("<I", self.data, 0x3C)[0]
        count = struct.unpack_from("<H", self.data, pe + 6)[0]
        optional = struct.unpack_from("<H", self.data, pe + 20)[0]
        self.sections = []
        for k in range(count):
            at = pe + 24 + optional + 40 * k
            name = self.data[at:at + 8].rstrip(b"\0").decode("ascii", "replace")
            vsize, va, rsize, raw = struct.unpack_from("<IIII", self.data, at + 8)
            self.sections.append((name, va, vsize, raw, rsize))

    def offset(self, rva: int) -> int:
        for _, va, vsize, raw, rsize in self.sections:
            if va <= rva < va + max(vsize, rsize):
                if rva - va >= rsize:
                    raise ValueError(f"0x{rva:X} is in uninitialized data (zero in the file, set at run time)")
                return rva - va + raw
        raise ValueError(f"0x{rva:X} isn't in any section")

    def read(self, rva: int, size: int) -> bytes:
        at = self.offset(rva)
        return self.data[at:at + size]

    def section(self, name: str) -> tuple[int, bytes]:
        for sname, va, vsize, raw, rsize in self.sections:
            if sname == name:
                return va, self.data[raw:raw + min(vsize, rsize)]
        raise ValueError(f"no section {name}")


def exe_path() -> Path:
    return Path(load_config()["target"]["tic_dir"]) / "FFT_enhanced.exe"


def functions() -> list[tuple[int, int, str, Path, int]]:
    """Every decompiled function: (rva, size, name, file, line), by rva."""
    out = []
    for path in sorted(DECOMPILED.glob("*.c")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in HEADER.finditer(text):
            out.append((int(m.group(1), 16), int(m.group(3)), m.group(2), path, text.count("\n", 0, m.start()) + 1))
    out.sort()
    return out


def containing(rva: int, table: list | None = None) -> tuple[int, int, str, Path, int] | None:
    for entry in table or functions():
        if entry[0] <= rva < entry[0] + entry[1]:
            return entry
    return None


def function_text(entry: tuple[int, int, str, Path, int]) -> str:
    lines = entry[3].read_text(encoding="utf-8", errors="replace").splitlines()
    start = entry[4] - 1
    end = start + 1
    while end < len(lines) and not lines[end].startswith("// ==== "):
        end += 1
    return "\n".join(lines[start:end]).rstrip()


def users(symbol: str) -> list[tuple[int, int, str, Path, int, int]]:
    """The functions whose body mentions symbol (a whole word), with how often."""
    word = re.compile(r"\b" + re.escape(symbol) + r"\b")
    found = []
    for path in sorted(DECOMPILED.glob("*.c")):
        text = path.read_text(encoding="utf-8", errors="replace")
        heads = list(HEADER.finditer(text))
        for k, m in enumerate(heads):
            body = text[m.end():heads[k + 1].start() if k + 1 < len(heads) else len(text)]
            hits = len(word.findall(body))
            if hits:
                found.append((int(m.group(1), 16), int(m.group(3)), m.group(2), path,
                              text.count("\n", 0, m.start()) + 1, hits))
    return found


def number(text: str) -> int:
    return int(text, 16) if text.lower().startswith("0x") else int(text, 0)


def rva_of(text: str) -> int:
    value = number(text)
    return value - BASE if value >= BASE else value


def cmd_dis(args: argparse.Namespace) -> None:
    site_dir = site.getusersitepackages()
    if site_dir not in sys.path:
        sys.path.append(site_dir)
    try:
        from capstone import CS_ARCH_X86, CS_MODE_64, Cs
    except ImportError:
        sys.exit("capstone isn't installed (pip install --user capstone)")
    exe = Exe(exe_path())
    start = rva_of(args.rva)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    for ins in md.disasm(exe.read(start, number(args.length)), start):
        target = ""
        m = re.search(r"\[rip ([+-]) 0x([0-9a-f]+)\]", ins.op_str)
        if m:
            where = ins.address + ins.size + (1 if m.group(1) == "+" else -1) * int(m.group(2), 16)
            target = f"    ; 0x{where:X}"
        print(f"{ins.address:8X}  {ins.bytes.hex(' '):<30} {ins.mnemonic} {ins.op_str}{target}")


def cmd_func(args: argparse.Namespace) -> None:
    rva = rva_of(args.rva)
    entry = containing(rva)
    if entry is None:
        sys.exit(f"0x{rva:X} isn't in a decompiled function (try `dis`)")
    print(f"{entry[3].name}:{entry[4]}  (0x{rva:X} is +0x{rva - entry[0]:X} into it)")
    text = function_text(entry)
    lines = text.splitlines()
    if args.lines and len(lines) > args.lines:
        text = "\n".join(lines[:args.lines]) + f"\n... ({len(lines) - args.lines} more lines; --lines 0 for all)"
    print(text)


def cmd_callers(args: argparse.Namespace) -> None:
    rva = rva_of(args.rva)
    name = f"FUN_{BASE + rva:x}"
    for start, size, fname, path, line, hits in users(name):
        if start != rva:
            print(f"0x{start:X} {fname} ({size} bytes) {path.name}:{line}  calls it {hits}x")


def cmd_refs(args: argparse.Namespace) -> None:
    rva = rva_of(args.rva)
    names = [f"DAT_{BASE + rva:x}", f"PTR_DAT_{BASE + rva:x}", f"_DAT_{BASE + rva:x}", f"PTR_FUN_{BASE + rva:x}"]
    seen = set()
    for name in names:
        for start, size, fname, path, line, hits in users(name):
            if start not in seen:
                seen.add(start)
                print(f"0x{start:X} {fname} ({size} bytes) {path.name}:{line}  {name} {hits}x")
    if not seen:
        print("no decompiled function names it (Ghidra may name it as part of a larger global; try nearby addresses)")


def cmd_read(args: argparse.Namespace) -> None:
    exe = Exe(exe_path())
    rva, count = rva_of(args.rva), number(args.count)
    size = {"q": 8, "d": 4, "w": 2, "b": 1}[args.kind]
    data = exe.read(rva, size * count)
    if args.kind == "b":
        for k in range(0, len(data), 16):
            print(f"{rva + k:8X}  {data[k:k + 16].hex(' ')}")
        return
    fmt = {"q": "<Q", "d": "<I", "w": "<H"}[args.kind]
    for k in range(count):
        value = struct.unpack_from(fmt, data, k * size)[0]
        shown = f"0x{value:X}"
        if args.kind == "q" and BASE <= value < BASE + 0x10000000:
            entry = containing(value - BASE)
            shown = f"-> 0x{value - BASE:X}" + (f" {entry[2]}" if entry and entry[0] == value - BASE else "")
        print(f"{rva + k * size:8X}  [{k:#x}] {shown}")


def cmd_find(args: argparse.Namespace) -> None:
    exe = Exe(exe_path())
    tokens = args.pattern.split()
    regex = re.compile(b"".join(b"." if t == "??" else re.escape(bytes([int(t, 16)])) for t in tokens), re.S)
    found = 0
    for name, va, vsize, raw, rsize in exe.sections:
        if args.section and name != args.section:
            continue
        data = exe.data[raw:raw + min(vsize, rsize)]
        for m in regex.finditer(data):
            rva = va + m.start()
            entry = containing(rva) if found < 20 else None
            print(f"0x{rva:X} ({name})" + (f"  in 0x{entry[0]:X} {entry[2]} +0x{rva - entry[0]:X}" if entry else ""))
            found += 1
    print(f"{found} place(s)")


def cmd_crash(args: argparse.Namespace) -> None:
    address, base = number(args.address), number(args.base)
    rva = address - base
    entry = containing(rva)
    print(f"exe+0x{rva:X}" + (f": in 0x{entry[0]:X} {entry[2]} +0x{rva - entry[0]:X} ({entry[3].name}:{entry[4]})"
                              if entry else ": not in a decompiled function"))


def main() -> None:
    sys.stdout.reconfigure(errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="what", required=True)
    p = sub.add_parser("dis", help="disassemble")
    p.add_argument("rva")
    p.add_argument("length", nargs="?", default="0x60")
    p.set_defaults(run=cmd_dis)
    p = sub.add_parser("func", help="the decompiled function holding an address")
    p.add_argument("rva")
    p.add_argument("--lines", type=int, default=200)
    p.set_defaults(run=cmd_func)
    p = sub.add_parser("callers", help="the decompiled functions calling one")
    p.add_argument("rva")
    p.set_defaults(run=cmd_callers)
    p = sub.add_parser("refs", help="the decompiled functions using a global")
    p.add_argument("rva")
    p.set_defaults(run=cmd_refs)
    p = sub.add_parser("read", help="values from the exe file")
    p.add_argument("kind", choices=["q", "d", "w", "b"])
    p.add_argument("rva")
    p.add_argument("count", nargs="?", default="16")
    p.set_defaults(run=cmd_read)
    p = sub.add_parser("find", help="where a byte pattern is")
    p.add_argument("pattern")
    p.add_argument("--section", help="only this section (.text, .rdata, .data)")
    p.set_defaults(run=cmd_find)
    p = sub.add_parser("crash", help="an absolute address (a crash report's) as RVA and function")
    p.add_argument("address")
    p.add_argument("base", help="the image base the game ran at (the mod's log names it)")
    p.set_defaults(run=cmd_crash)
    args = parser.parse_args()
    args.run(args)


if __name__ == "__main__":
    main()
