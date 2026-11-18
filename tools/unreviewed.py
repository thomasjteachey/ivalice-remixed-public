"""List ReMixed's in-place patches to a code file that no doc mentions yet, with vanilla's and ReMixed's code.

    python -I tools/unreviewed.py                      # BATTLE.BIN
    python -I tools/unreviewed.py BATTLE.BIN --from 0x80184000 --to 0x80190000
    python -I tools/unreviewed.py SCUS_942.21 --all    # documented ones too

A block counts as documented when docs/*.md or docs/analysis/*.md mention an address within 0x40 bytes of it
(--near to change that). Blocks that only change a call or jump into ReMixed's new code are marked [hook]: the
new routines are catalogued in docs/analysis/. Run `remixed.py diff` first (codemap.py's change map).
"""

from __future__ import annotations

import argparse
import bisect
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from codemap import LOAD_ADDRESS, find_blocks  # noqa: E402
from mips import decode  # noqa: E402
from psxdisc import Disc  # noqa: E402
from remixed import ROOT, WORK, load_config, ppf_path, remixed_bin, slug, vanilla_bin  # noqa: E402
from tables import FilePair  # noqa: E402


def doc_addresses() -> list[int]:
    docs = sorted((ROOT / "docs").glob("*.md")) + sorted((ROOT / "docs" / "analysis").glob("*.md"))
    found = set()
    for d in docs:
        for m in re.finditer(r"0x(80[0-9A-Fa-f]{6})", d.read_text(encoding="utf-8")):
            found.add(int(m.group(1), 16))
    return sorted(found)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("file", nargs="?", default="BATTLE.BIN")
    parser.add_argument("--from", dest="start", type=lambda s: int(s, 0), default=0)
    parser.add_argument("--to", dest="end", type=lambda s: int(s, 0), default=0xFFFFFFFF)
    parser.add_argument("--near", type=lambda s: int(s, 0), default=0x40)
    parser.add_argument("--all", action="store_true", help="list documented blocks too")
    args = parser.parse_args()

    path = args.file.replace("\\", "/").upper()
    base = LOAD_ADDRESS.get(path)
    if base is None:
        sys.exit(f"No load address known for {args.file}. Known: {', '.join(LOAD_ADDRESS)}")
    cfg = load_config()
    change_map = WORK / "reports" / f"{slug(ppf_path(cfg).stem)}-disc-changes.json"
    if not change_map.exists():
        sys.exit("Run `python -I tools/remixed.py diff` first.")
    files_changed = json.loads(change_map.read_text(encoding="utf-8"))["changes"]["files"]
    info = next((i for p, i in files_changed.items() if i.get("was", p).upper() == path), None)
    if info is None:
        sys.exit(f"ReMixed doesn't change {path}.")
    mentioned = doc_addresses()

    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        files = FilePair(vanilla, patched)
        old, new = files.get(path)
        blocks = find_blocks(path, info, files)

    def word(data: bytes, ram: int) -> int:
        return int.from_bytes(data[ram - base:ram - base + 4], "little")

    def function_start(ram: int) -> int:
        a = ram & ~3
        for _ in range(4000):  # back to the nearest vanilla prologue: addiu sp, sp, -N
            if a < base:
                break
            w = word(old, a)
            if (w >> 16) == 0x27BD and w & 0x8000:
                return a
            a -= 4
        return 0

    counts = {"documented": 0, "hook": 0, "unreviewed": 0}
    out = []
    for b in blocks:
        if b.kind != "patched code":
            continue
        start, end = base + b.start, base + b.end
        if end <= args.start or start >= args.end:
            continue
        k = bisect.bisect_left(mentioned, start - args.near)
        documented = k < len(mentioned) and mentioned[k] < end + args.near
        changed = [(a, word(old, a), word(new, a)) for a in range(start, end, 4) if word(old, a) != word(new, a)]
        hook = len(changed) <= 2 and any((n >> 26) in (2, 3) for _, _, n in changed)
        counts["documented" if documented else "hook" if hook else "unreviewed"] += 1
        if documented and not args.all:
            continue
        mark = " [documented]" if documented else " [hook]" if hook else ""
        out.append(f"== 0x{start:08X} (+{end - start}) in 0x{function_start(start):08X}{mark}")
        for a, o, n in changed[:24]:
            o_text = str(decode(o, a) or f".word 0x{o:08X}")
            n_text = str(decode(n, a) or f".word 0x{n:08X}")
            out.append(f"   {a:08X}  {o_text:<34} | {n_text}")
        if len(changed) > 24:
            out.append(f"   ... {len(changed) - 24} more")
    print(f"{path}: {counts['documented']} documented, {counts['hook']} hooks only, {counts['unreviewed']} unreviewed")
    print("\n".join(out))


if __name__ == "__main__":
    main()
