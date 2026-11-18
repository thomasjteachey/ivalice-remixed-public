"""Find ReMixed's code changes: patched vanilla code, new routines, and hook sites.

    python -I tools/codemap.py

Run `remixed.py prepare`, `remixed.py diff` and `tables.py` first. Writes:

    work/asm/*.asm                  side-by-side disassembly of every changed block (not committed)
    data/remixed/hooks.csv          every call or jump ReMixed changed
    data/remixed/routines.csv       functions in ReMixed's new code
    docs/generated/<patch>-code.md  summary

Changed bytes inside known data tables are left out (tables.py covers those).
"""

from __future__ import annotations

import csv
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from mips import code_score, decode, is_prologue, words  # noqa: E402
from psxdisc import FORM1_DATA, Disc  # noqa: E402
from remixed import REPORT_DIR, ROOT, WORK, load_config, ppf_path, remixed_bin, slug, vanilla_bin  # noqa: E402
from tables import BATTLE, DATA_DIR, SCUS, TABLES, WLDCORE, WORLD, FilePair, blocks, subtract  # noqa: E402

ASM_DIR = WORK / "asm"

# RAM address of each code file's first byte. The main exe's 0x800-byte header
# isn't loaded, so its file offset 0x800 lands at 0x80010000. The overlay slots
# (0x80067000, 0x800E0000, 0x801BF000, 0x801DF000) are listed at the start of
# the main exe; which file uses which slot was checked against each file's own calls.
LOAD_ADDRESS = {
    SCUS: 0x8000F800,
    BATTLE: 0x80067000,
    WLDCORE: 0x80067000,
    WORLD: 0x800E0000,
    **{f"EVENT/{name}.OUT": 0x801BF000 for name in ("ATTACK", "REQUIRE", "ETC", "OPTION", "BUNIT", "EQUIP", "CARD")},
    **{f"EVENT/{name}.OUT": 0x801DF000 for name in ("JOBSTTS", "HELPMENU")},
}
CODE_THRESHOLD = 0.8


@dataclass
class Block:
    file: str
    start: int  # file offset, 4-byte aligned
    end: int
    changed: int  # bytes changed
    kind: str = ""
    old: bytes = b""
    new: bytes = b""

    def ram(self, offset: int) -> int:
        return LOAD_ADDRESS[self.file] + offset


@dataclass
class Routine:
    file: str
    start: int  # RAM
    end: int
    calls: list[int] = field(default_factory=list)
    refs: list[int] = field(default_factory=list)
    callers: list[str] = field(default_factory=list)


def table_symbols() -> list[tuple[str, int, int, str]]:
    """(file, ram start, ram end, table title) for every known table in a code file."""
    out = []
    for t in TABLES:
        base = LOAD_ADDRESS.get(t.file.upper())
        if base is not None:
            out.append((t.file.upper(), base + t.offset, base + t.end, t.title))
    return out


def classify(block: Block, old_file: bytes, new_file: bytes) -> str:
    # Small blocks say little on their own, so judge them by their surroundings.
    pad = 0 if block.end - block.start >= 32 else 64
    s, e = max(0, block.start - pad), block.end + pad
    old_code = code_score(old_file[s:e]) >= CODE_THRESHOLD
    new_code = code_score(new_file[s:e]) >= CODE_THRESHOLD
    if old_code and new_code:
        return "patched code"
    if new_code:
        return "new code"
    if old_code:
        return "code removed"
    return "data"


def find_blocks(path: str, info: dict, files: FilePair) -> list[Block]:
    holes = [(t.offset, t.end) for t in TABLES if t.file.upper() == path.upper()]
    left = subtract(info["ranges"], holes)
    old_file, new_file = files.get(path)
    out = []
    for start, end, changed in blocks(left):
        start, end = start & ~3, (end + 3) & ~3
        b = Block(path, start, end, changed, old=old_file[start:end], new=new_file[start:end])
        b.kind = classify(b, old_file, new_file)
        out.append(b)
    return out


def data_refs(ws: list[int], start: int) -> list[int]:
    """Absolute addresses built with lui + addiu/ori/load/store, tracked per register."""
    hi: dict[str, int] = {}
    refs = []
    for i, w in enumerate(ws):
        ins = decode(w, start + i * 4)
        if ins is None:
            continue
        if ins.mnemonic == "lui":
            hi[ins.rt] = ins.imm << 16
            continue
        if ins.base and ins.base in hi:
            refs.append((hi[ins.base] + ins.offset) & 0xFFFFFFFF)
        elif ins.mnemonic in ("addiu", "ori") and ins.operands.split(", ")[1] in hi:
            src = hi[ins.operands.split(", ")[1]]
            refs.append((src + (ins.imm if ins.mnemonic == "ori" else (ins.imm ^ 0x8000) - 0x8000)) & 0xFFFFFFFF)
        if ins.rt and ins.mnemonic not in ("sw", "sh", "sb", "swl", "swr"):
            hi.pop(ins.rt, None)
    return sorted(set(r for r in refs if 0x80000000 <= r < 0x80200000))


def split_routines(block: Block) -> list[Routine]:
    ws = words(block.new)
    base = block.ram(block.start)
    starts = [0] + [i for i, w in enumerate(ws) if i and is_prologue(w)]
    out = []
    for k, first in enumerate(starts):
        last = starts[k + 1] if k + 1 < len(starts) else len(ws)
        chunk = ws[first:last]
        if not any(chunk):
            continue
        r = Routine(block.file, base + first * 4, base + last * 4)
        for i, w in enumerate(chunk):
            ins = decode(w, r.start + i * 4)
            if ins is not None and ins.mnemonic == "jal":
                r.calls.append(ins.target)
        r.refs = data_refs(chunk, r.start)
        out.append(r)
    return out


def hook_sites(block: Block) -> list[dict]:
    """Calls and jumps ReMixed added or redirected inside changed code."""
    rows = []
    old_ws, new_ws = words(block.old), words(block.new)
    for i, (ow, nw) in enumerate(zip(old_ws, new_ws)):
        if ow == nw:
            continue
        addr = block.ram(block.start + i * 4)
        new = decode(nw, addr)
        if new is None or new.mnemonic not in ("j", "jal"):
            continue
        old = decode(ow, addr)
        if old is not None and old.mnemonic == new.mnemonic and old.target == new.target:
            continue
        rows.append({"file": block.file, "address": f"0x{addr:08X}", "kind": block.kind,
                     "vanilla": str(old) if old else f".word 0x{ow:08X}", "remixed": str(new),
                     "target": new.target})
    return rows


def listing(block: Block) -> list[str]:
    head = (f"; ==== {block.file} file 0x{block.start:X}-0x{block.end:X} "
            f"(RAM 0x{block.ram(block.start):08X}) {block.kind}, {block.changed} bytes changed")
    lines = [head]
    for i, (ow, nw) in enumerate(zip(words(block.old), words(block.new))):
        addr = block.ram(block.start + i * 4)
        old, new = decode(ow, addr), decode(nw, addr)
        o = str(old) if old else f".word 0x{ow:08X}"
        n = str(new) if new else f".word 0x{nw:08X}"
        mark = "*" if ow != nw else " "
        lines.append(f"{addr:08X} {mark} {ow:08X} {o:<36} | {nw:08X} {n}")
    return lines


def describe_target(target: int, routines: list[Routine], file: str) -> str:
    # Overlays share address ranges, so prefer a routine in the same file, then the main exe.
    hits = [r for r in routines if r.start <= target < r.end]
    hits.sort(key=lambda r: (r.file != file, r.file != SCUS))
    if hits:
        r = hits[0]
        where = "" if r.start == target else f"+0x{target - r.start:X}"
        return f"new code in {r.file} (0x{r.start:08X}{where})"
    return "vanilla code"


def name_ref(ref: int, symbols: list[tuple[str, int, int, str]], file: str) -> str | None:
    for sym_file, start, end, title in symbols:
        if start <= ref < end and sym_file in (file.upper(), SCUS):
            return title
    return None


def main() -> None:
    cfg = load_config()
    name = ppf_path(cfg).stem
    change_map_path = WORK / "reports" / f"{slug(name)}-disc-changes.json"
    if not change_map_path.exists():
        sys.exit("Run `python -I tools/remixed.py diff` first.")
    change_map = json.loads(change_map_path.read_text(encoding="utf-8"))

    all_blocks: list[Block] = []
    with Disc(vanilla_bin(cfg)) as vanilla, Disc(remixed_bin(cfg)) as patched:
        files = FilePair(vanilla, patched)
        for path, info in change_map["changes"]["files"].items():
            vanilla_path = info.get("was", path).upper()
            if vanilla_path in LOAD_ADDRESS:
                all_blocks += find_blocks(vanilla_path, info, files)

    routines = [r for b in all_blocks if b.kind == "new code" for r in split_routines(b)]
    hooks = [h for b in all_blocks if b.kind in ("patched code", "new code") for h in hook_sites(b)]
    for h in hooks:
        h["goes to"] = describe_target(h["target"], routines, h["file"])
        for r in routines:
            if r.start == h["target"]:
                r.callers.append(f"{h['file']} 0x{h['address'][2:]}")

    # Full listings stay in work/: they're disassembled game and ReMixed code.
    ASM_DIR.mkdir(parents=True, exist_ok=True)
    by_file: dict[str, list[Block]] = {}
    for b in all_blocks:
        by_file.setdefault(b.file, []).append(b)
    for path, file_blocks in by_file.items():
        out = ASM_DIR / (path.replace("/", "_") + ".asm")
        lines = []
        for b in sorted(file_blocks, key=lambda b: b.start):
            if b.kind != "data":
                lines += listing(b) + [""]
        out.write_text("\n".join(lines) + "\n", encoding="utf-8")

    symbols = table_symbols()
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    with open(DATA_DIR / "hooks.csv", "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, ["file", "address", "kind", "vanilla", "remixed", "goes to"])
        writer.writeheader()
        writer.writerows({k: h[k] for k in writer.fieldnames} for h in hooks)
    with open(DATA_DIR / "routines.csv", "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["file", "start", "size", "called from", "calls", "tables used"])
        for r in routines:
            tables_used = sorted({t for ref in r.refs if (t := name_ref(ref, symbols, r.file))})
            writer.writerow([r.file, f"0x{r.start:08X}", r.end - r.start, " ".join(r.callers),
                             " ".join(f"0x{c:08X}" for c in sorted(set(r.calls))), "; ".join(tables_used)])

    out = write_summary(name, all_blocks, hooks, routines, symbols)
    print(f"Wrote {out.relative_to(ROOT)}")
    print(f"Wrote {(DATA_DIR / 'hooks.csv').relative_to(ROOT)} ({len(hooks)} hook sites)")
    print(f"Wrote {(DATA_DIR / 'routines.csv').relative_to(ROOT)} ({len(routines)} routines)")
    print(f"Wrote listings to {ASM_DIR.relative_to(ROOT)}")


def write_summary(name, all_blocks, hooks, routines, symbols) -> Path:
    kinds = ["patched code", "new code", "code removed", "data"]
    lines = [
        f"# {name}: code changes",
        "",
        "Generated by `python -I tools/codemap.py`. Don't edit by hand.",
        "",
        "Changed blocks outside known data tables, sorted by whether they look like",
        "MIPS code. **Patched code** edits vanilla routines in place. **New code** is",
        "ReMixed's own routines written over unused or freed space. **Data** is",
        "everything else (mostly text). Full side-by-side disassembly is written to",
        "`work/asm/` when the tool runs.",
        "",
        "| File | Load address | " + " | ".join(k.capitalize() for k in kinds) + " |",
        "|---|---|" + "--:|" * len(kinds),
    ]
    files = sorted({b.file for b in all_blocks}, key=lambda f: -sum(b.changed for b in all_blocks if b.file == f))
    for path in files:
        cells = []
        for k in kinds:
            bs = [b for b in all_blocks if b.file == path and b.kind == k]
            cells.append(f"{len(bs)} / {sum(b.changed for b in bs):,} B" if bs else "")
        lines.append(f"| `{path}` | 0x{LOAD_ADDRESS[path]:08X} | " + " | ".join(cells) + " |")
    lines += ["", "Cells show blocks / bytes changed."]

    entries = [h for h in hooks if h["kind"] == "patched code" and h["goes to"].startswith("new code")]
    lines += ["", "## Entry points into ReMixed code", "",
              f"{len(hooks):,} calls or jumps were added or changed in total (all in",
              f"`data/remixed/hooks.csv`). {len(entries)} of them are in vanilla code and lead",
              "into ReMixed's new code: these are where each custom mechanic takes over.",
              "Grouped by the routine they enter:", "",
              "| Routine | In file | Size | Entered from | Tables used |", "|---|---|--:|---|---|"]
    by_target: dict[int, list[dict]] = {}
    for h in entries:
        by_target.setdefault(h["target"], []).append(h)
    for target, hs in sorted(by_target.items()):
        r = next((r for r in routines if r.start <= target < r.end), None)
        used = sorted({t for ref in r.refs if (t := name_ref(ref, symbols, r.file))}) if r else []
        sites = ", ".join(f"{h['file'].split('/')[-1]} {h['address']}" for h in hs[:4])
        if len(hs) > 4:
            sites += f" (+{len(hs) - 4})"
        size = r.end - r.start if r else ""
        lines.append(f"| 0x{target:08X} | `{r.file if r else '?'}` | {size} | {sites} | {'; '.join(used)} |")

    lines += ["", "## Largest new routines", "",
              "All of them are in `data/remixed/routines.csv`.", "",
              "| File | Start | Size | Entry points | Tables used |", "|---|---|--:|--:|---|"]
    for r in sorted(routines, key=lambda r: -(r.end - r.start))[:30]:
        used = sorted({t for ref in r.refs if (t := name_ref(ref, symbols, r.file))})
        lines.append(f"| `{r.file}` | 0x{r.start:08X} | {r.end - r.start} | {len(r.callers) or ''} | "
                     f"{'; '.join(used)} |")

    out = REPORT_DIR / f"{slug(name)}-code.md"
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return out


if __name__ == "__main__":
    main()
