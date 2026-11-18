"""Build the ReMixed reference disc and map what the patch changes.

    python -I tools/remixed.py prepare           unpack the vanilla disc and apply ReMixed
    python -I tools/remixed.py diff [--extract]  map every changed byte to a disc file

Paths come from local.toml (copy local.example.toml). Output goes to work/,
which is gitignored, except the change summary in docs/generated/.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import tomllib
from pathlib import Path

# `python -I` leaves the script's folder off sys.path, so add it for the sibling modules.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from ppf import PpfPatch, apply_ppf, read_ppf  # noqa: E402
from psxdisc import FORM1_DATA, SECTOR_SIZE, Disc, DiscEntry  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / "work"
VANILLA_DIR = WORK / "disc" / "vanilla"
REMIXED_DIR = WORK / "disc" / "remixed"
FILES_DIR = WORK / "files"
REPORT_DIR = ROOT / "docs" / "generated"

# Rough guide to what FFT's disc files hold, from FFHacktics documentation.
# First match wins.
FILE_NOTES = [
    ("is", "SCUS_942.21", "Main executable: engine code and core data tables"),
    ("is", "FFTR_942.21", "Main executable (ReMixed's name for SCUS_942.21)"),
    ("is", "BATTLE.BIN", "Battle engine code and data"),
    ("is", "WORLD/WORLD.BIN", "World map and menu code"),
    ("is", "WORLD/WLDCORE.BIN", "World map core code"),
    ("is", "EVENT/TEST.EVT", "Event scripts (cutscenes, battle dialogue)"),
    ("is", "EVENT/ATTACK.OUT", "Event and scenario setup data"),
    ("starts", "BATTLE/ENTD", "Battle unit placement (ENTD)"),
    ("ends", ".OUT", "Overlay code module"),
    ("ends", ".SPR", "Unit sprite sheet"),
    ("starts", "MAP/", "Map data"),
    ("starts", "EFFECT/", "Ability effect graphics and code"),
    ("starts", "SOUND/", "Sound data"),
]


def describe(path: str) -> str:
    if path.endswith("/"):
        return "Directory record"
    upper = path.upper()
    for kind, pattern, note in FILE_NOTES:
        if (
            (kind == "is" and upper == pattern)
            or (kind == "starts" and upper.startswith(pattern))
            or (kind == "ends" and upper.endswith(pattern))
        ):
            return note
    return ""


def slug(name: str) -> str:
    return name.replace(" ", "-")


def files_by_lba(disc: Disc) -> dict[int, DiscEntry]:
    return {e.lba: e for e in disc.entries if not e.is_dir and e.size}


def load_config() -> dict:
    path = ROOT / "local.toml"
    if not path.exists():
        sys.exit("local.toml not found. Copy local.example.toml to local.toml and fill in your paths.")
    return tomllib.loads(path.read_text(encoding="utf-8"))


def sha1(path: Path) -> str:
    digest = hashlib.sha1()
    with open(path, "rb") as f:
        while chunk := f.read(1 << 20):
            digest.update(chunk)
    return digest.hexdigest()


def vanilla_bin(cfg: dict) -> Path:
    """The vanilla .bin, unpacking it from the .7z into work/ the first time."""
    source = Path(cfg["inputs"]["vanilla_disc"])
    if source.suffix.lower() == ".bin":
        return source
    bins = sorted(VANILLA_DIR.glob("*.bin"))
    if not bins:
        seven_zip = cfg.get("tools", {}).get("seven_zip") or shutil.which("7z")
        if not seven_zip or not Path(seven_zip).exists():
            sys.exit("7-Zip not found. Set tools.seven_zip in local.toml.")
        VANILLA_DIR.mkdir(parents=True, exist_ok=True)
        print(f"Unpacking {source.name} ...")
        subprocess.run([seven_zip, "x", str(source), f"-o{VANILLA_DIR}", "-y", "-bso0", "-bsp0"], check=True)
        bins = sorted(VANILLA_DIR.glob("*.bin"))
    if len(bins) != 1:
        sys.exit(f"Expected one .bin in {VANILLA_DIR}, found {len(bins)}.")
    return bins[0]


def ppf_path(cfg: dict) -> Path:
    return Path(cfg["inputs"]["remixed_ppf"])


def remixed_bin(cfg: dict) -> Path:
    return REMIXED_DIR / f"{ppf_path(cfg).stem}.bin"


def write_cue(vanilla: Path, patched: Path) -> None:
    """A .cue next to the patched .bin so it can be played in an emulator."""
    cue = vanilla.with_suffix(".cue")
    if cue.exists():
        lines = cue.read_text(encoding="latin-1").splitlines()
        lines = [f'FILE "{patched.name}" BINARY' if ln.strip().upper().startswith("FILE ") else ln for ln in lines]
    else:
        lines = [f'FILE "{patched.name}" BINARY', "  TRACK 01 MODE2/2352", "    INDEX 01 00:00:00"]
    patched.with_suffix(".cue").write_text("\n".join(lines) + "\n", encoding="latin-1")


def cmd_prepare(cfg: dict) -> None:
    source = vanilla_bin(cfg)
    with Disc(source) as disc:
        cnf = disc.find("SYSTEM.CNF")
        boot = disc.read(cnf).decode("ascii", "replace").splitlines()[0].strip() if cnf else "no SYSTEM.CNF"
    print(f"Vanilla disc: {source.name}")
    print(f"  sha1 {sha1(source)}")
    print(f"  {boot}")
    if "SCUS_942.21" not in boot:
        print("  warning: ReMixed is made for the US disc (SCUS-94221); this looks like a different one")

    patch = read_ppf(ppf_path(cfg))
    out = remixed_bin(cfg)
    out.parent.mkdir(parents=True, exist_ok=True)
    print(f"Applying {ppf_path(cfg).name} ({len(patch.records):,} records, {patch.bytes_written:,} bytes) ...")
    apply_ppf(patch, source, out)
    write_cue(source, out)
    print(f"ReMixed disc: {out.relative_to(ROOT)}")
    print(f"  sha1 {sha1(out)}")


def map_changes(patch: PpfPatch, vanilla: Disc, patched: Disc) -> dict:
    """Sort every byte the patch changes into the disc file that owns it.

    Ownership comes from the patched disc's file table, falling back to the
    vanilla one. Files ReMixed renamed keep a note of their vanilla name.
    """
    vanilla_at = files_by_lba(vanilla)
    files: dict[str, dict] = {}
    outside: dict[int, int] = {}  # lba -> changed data bytes not owned by any file
    sector_overhead = 0  # sync/header/error-correction bytes
    appended = 0
    identical = 0
    raw = vanilla.raw

    lba_cache = -1
    span = (0, 0)
    bucket = None
    file_base = 0
    for record in patch.records:
        original = raw[record.offset : record.offset + len(record.data)]
        if len(original) < len(record.data):
            appended += len(record.data) - len(original)
        for i in range(len(original)):
            new = record.data[i]
            if original[i] == new:
                identical += 1
                continue
            lba, within = divmod(record.offset + i, SECTOR_SIZE)
            if lba != lba_cache:
                lba_cache = lba
                span = patched.data_span(lba)
                owner = patched.owner(lba) or vanilla.owner(lba)
                bucket = None
                if owner is not None:
                    bucket = files.get(owner.path)
                    if bucket is None:
                        bucket = {"size": owner.size, "lba": owner.lba, "changed": 0, "ranges": []}
                        was = vanilla_at.get(owner.lba)
                        if not owner.is_dir and was is not None and was.path != owner.path:
                            bucket["was"] = was.path
                        files[owner.path] = bucket
                    file_base = (lba - owner.lba) * FORM1_DATA - span[0]
            if not span[0] <= within < span[0] + span[1]:
                sector_overhead += 1
                continue
            if bucket is None:
                outside[lba] = outside.get(lba, 0) + 1
                continue
            at = file_base + within
            bucket["changed"] += 1
            ranges = bucket["ranges"]
            if ranges and ranges[-1][1] == at:
                ranges[-1][1] = at + 1
            else:
                ranges.append([at, at + 1])

    for info in files.values():
        merged: list[list[int]] = []
        for start, end in sorted(info["ranges"]):
            if merged and start <= merged[-1][1]:
                merged[-1][1] = max(merged[-1][1], end)
            else:
                merged.append([start, end])
        info["ranges"] = merged

    outside_ranges: list[list[int]] = []  # [first_lba, last_lba, bytes]
    for lba in sorted(outside):
        if outside_ranges and lba == outside_ranges[-1][1] + 1:
            outside_ranges[-1][1] = lba
            outside_ranges[-1][2] += outside[lba]
        else:
            outside_ranges.append([lba, lba, outside[lba]])

    return {
        "files": files,
        "outside": outside_ranges,
        "sector_overhead": sector_overhead,
        "appended": appended,
        "identical": identical,
    }


def compare_trees(vanilla: Disc, patched: Disc) -> dict:
    """File table differences. A file at the same sector under a new name counts as renamed."""
    before = {e.path: e for e in vanilla.entries}
    after = {e.path: e for e in patched.entries}
    before_at = files_by_lba(vanilla)
    renamed, added = [], []
    for path in sorted(p for p in after if p not in before):
        old = before_at.get(after[path].lba)
        if old is not None and old.path not in after:
            renamed.append([old.path, path])
        else:
            added.append(path)
    renamed_from = {old for old, _ in renamed}
    return {
        "renamed": renamed,
        "added": added,
        "removed": sorted(p for p in before if p not in after and p not in renamed_from),
        "moved_or_resized": sorted(
            p for p in after if p in before and (after[p].lba, after[p].size) != (before[p].lba, before[p].size)
        ),
    }


def extract_changed(files: dict[str, dict], vanilla: Disc, patched: Disc) -> None:
    """Copy each changed file off both discs, saved under its ReMixed name."""
    for path, info in files.items():
        if path.endswith("/"):
            continue
        for disc, side, name in ((vanilla, "vanilla", info.get("was", path)), (patched, "remixed", path)):
            entry = disc.find(name)
            if entry is not None:
                out = FILES_DIR / side / path
                out.parent.mkdir(parents=True, exist_ok=True)
                out.write_bytes(disc.read(entry))


def write_report(name: str, report: dict) -> Path:
    changes = report["changes"]
    files = sorted(changes["files"].items(), key=lambda kv: -kv[1]["changed"])
    data_bytes = sum(info["changed"] for _, info in files)
    outside_bytes = sum(r[2] for r in changes["outside"])
    tree = report["tree"]

    lines = [
        f"# {name}: disc changes",
        "",
        "Generated by `python -I tools/remixed.py diff`. Don't edit by hand.",
        "",
        f"- Vanilla disc: `{report['vanilla']['name']}`, SHA-1 `{report['vanilla']['sha1']}`",
        f"- Patch: `{report['patch']['name']}`, {report['patch']['records']:,} records, "
        f"{report['patch']['bytes']:,} bytes written",
        f"- Bytes changed inside files: **{data_bytes:,}** across **{len(files)}** files",
        f"- Bytes changed outside any file: {outside_bytes:,}",
        f"- Sector header and error-correction bytes changed (ignored): {changes['sector_overhead']:,}",
        f"- Bytes written that match vanilla anyway: {changes['identical']:,}",
    ]
    if changes["appended"]:
        lines.append(f"- Bytes written past the end of the vanilla image: {changes['appended']:,}")
    lines += [
        "",
        "The \"What it is\" column is a rough guide from FFHacktics documentation.",
        "Full byte ranges for every file are in `work/reports/` after running the tool.",
        "",
        "## Files changed",
        "",
        "| File | Size | Bytes changed | Changed ranges | What it is |",
        "|---|--:|--:|--:|---|",
    ]
    for path, info in files:
        label = f"`{path}`" + (f" (was `{info['was']}`)" if "was" in info else "")
        note = describe(path) or describe(info.get("was", ""))
        lines.append(f"| {label} | {info['size']:,} | {info['changed']:,} | {len(info['ranges']):,} | {note} |")

    if changes["outside"]:
        lines += ["", "## Changes outside any file", "", "| Sectors | Bytes changed |", "|---|--:|"]
        for first, last, count in changes["outside"]:
            sectors = f"{first}" if first == last else f"{first}-{last}"
            lines.append(f"| {sectors} | {count:,} |")

    lines += ["", "## File table changes", ""]
    if not any(tree.values()):
        lines.append("None. Every file keeps its vanilla name, position and size.")
    if tree["renamed"]:
        lines += [
            f"{len(tree['renamed'])} files renamed in place (same sectors, new name):",
            "",
            "<details><summary>Renamed files</summary>",
            "",
            "| Vanilla name | ReMixed name |",
            "|---|---|",
        ]
        lines += [f"| `{old}` | `{new}` |" for old, new in tree["renamed"]]
        lines += ["", "</details>", ""]
    for label, key in (("Added", "added"), ("Removed", "removed"), ("Moved or resized", "moved_or_resized")):
        if tree[key]:
            listing = ", ".join(f"`{p}`" for p in tree[key])
            lines += [f"<details><summary>{label}: {len(tree[key])}</summary>", "", listing, "", "</details>", ""]

    out = REPORT_DIR / f"{slug(name)}-disc-changes.md"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return out


def cmd_diff(cfg: dict, extract: bool) -> None:
    source = vanilla_bin(cfg)
    patched_path = remixed_bin(cfg)
    if not patched_path.exists():
        sys.exit("No ReMixed disc yet. Run `python -I tools/remixed.py prepare` first.")
    patch = read_ppf(ppf_path(cfg))
    name = ppf_path(cfg).stem

    with Disc(source) as vanilla, Disc(patched_path) as patched:
        print("Mapping changes ...")
        changes = map_changes(patch, vanilla, patched)
        tree = compare_trees(vanilla, patched)
        if extract:
            print(f"Extracting {len(changes['files'])} changed files to {FILES_DIR.relative_to(ROOT)} ...")
            extract_changed(changes["files"], vanilla, patched)

    report = {
        "vanilla": {"name": source.name, "sha1": sha1(source)},
        "patch": {"name": ppf_path(cfg).name, "records": len(patch.records), "bytes": patch.bytes_written},
        "changes": changes,
        "tree": tree,
    }
    json_out = WORK / "reports" / f"{slug(name)}-disc-changes.json"
    json_out.parent.mkdir(parents=True, exist_ok=True)
    json_out.write_text(json.dumps(report, indent=1), encoding="utf-8")
    md_out = write_report(name, report)
    print(f"Wrote {md_out.relative_to(ROOT)}")
    print(f"Wrote {json_out.relative_to(ROOT)}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("prepare", help="unpack the vanilla disc and apply the ReMixed patch")
    diff = sub.add_parser("diff", help="map every changed byte to a disc file")
    diff.add_argument("--extract", action="store_true", help="also copy each changed file out of both discs")
    args = parser.parse_args()

    cfg = load_config()
    if args.command == "prepare":
        cmd_prepare(cfg)
    else:
        cmd_diff(cfg, args.extract)


if __name__ == "__main__":
    main()
