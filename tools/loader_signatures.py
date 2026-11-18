"""Patterns that let the FFT mod loader find more tables in the classic build.

    python -I tools/loader_signatures.py            check the patterns against both exes
    python -I tools/loader_signatures.py --apply    write them into the installed loader (keeps a backup)
    python -I tools/loader_signatures.py --restore  put the loader's original file back

Mod loader 1.7.5 finds each table by searching memory for a run of the
table's own bytes. Its patterns for the job and Charge tables hold enhanced's
rebalanced values, so they never match classic, which keeps the PS1 values.
The patterns below wildcard the bytes where the two builds differ, and match
exactly once in each exe at the same record the original pattern targets.

Equipment bonuses and Move-Find can't be fixed this way: classic stores them
in different record sizes (25 and 16 bytes) than the loader expects (26 and
24), so the loader itself would need changing.
"""

from __future__ import annotations

import argparse
import re
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from remixed import load_config  # noqa: E402

LOADER_ID = "fftivc.utility.modloader"
PATTERNS = {
    # Record 1 (Ramza, chapter 1). Enhanced adds Perfume/Hair Adornment to the
    # equipment bytes, so those two are wildcards.
    "JobDataTable": "19 00 00 00 00 00 00 00 00 D0 40 ?? ?? 00 0B 78 0B 69 5F 64 32 6E 30 64 04 03 0A 00 00 00 00 00 00 40",
}
# Not used: a Charge pattern has to start at the table's first byte, which is a
# CT value that differs between the builds, so it would start with a wildcard.
# The loader hung on launch with "?? 01 ?? 02 ?? 03 ?? 04 ?? 05 ?? 07 ?? 0A ?? 14".
# Charge is blocked by the loader's id bug anyway.


def regex(pattern: str) -> re.Pattern[bytes]:
    return re.compile(b"".join(b"." if t == "??" else re.escape(bytes([int(t, 16)])) for t in pattern.split()), re.S)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    action = parser.add_mutually_exclusive_group()
    action.add_argument("--apply", action="store_true", help="write the patterns into the installed loader")
    action.add_argument("--restore", action="store_true", help="restore the loader's original signature file")
    args = parser.parse_args()

    cfg = load_config()
    mods = cfg.get("target", {}).get("reloaded_mods")
    if not mods:
        sys.exit("Set target.reloaded_mods in local.toml to Reloaded-II's Mods folder.")
    ini = Path(mods) / LOADER_ID / "Signatures" / "ffto.ini"
    backup = ini.with_suffix(".ini.original")
    if not ini.exists():
        sys.exit(f"No loader signature file at {ini}")

    if args.restore:
        if not backup.exists():
            sys.exit("No backup to restore.")
        shutil.copy(backup, ini)
        print(f"Restored {ini}")
        return

    game = Path(cfg["target"]["tic_dir"])
    exes = {mode: (game / f"FFT_{mode}.exe").read_bytes() for mode in ("classic", "enhanced")}
    ok = True
    for name, pattern in PATTERNS.items():
        if pattern.split()[0] == "??":  # the loader hangs on these
            print(f"{name:28} starts with a wildcard, which the loader can't handle")
            ok = False
            continue
        counts = {mode: len(regex(pattern).findall(data)) for mode, data in exes.items()}
        good = all(n == 1 for n in counts.values())
        ok &= good
        print(f"{name:28} classic {counts['classic']}, enhanced {counts['enhanced']} {'OK' if good else 'NOT UNIQUE'}")
    if not ok:
        sys.exit("Not writing: every pattern must match exactly once in both exes.")
    if not args.apply:
        print("Patterns check out. Run with --apply to write them into the loader.")
        return

    # Always start from the loader's original file, so patterns dropped from
    # this tool go back to their original line.
    if not backup.exists():
        shutil.copy(ini, backup)
    text = backup.read_text(encoding="utf-8")
    for name, pattern in PATTERNS.items():
        text, n = re.subn(rf"^{name}\s*=.*$", f"{name} = {pattern}", text, flags=re.M)
        if n != 1:
            sys.exit(f"Couldn't find {name} in {ini}; nothing written.")
    ini.write_text(text, encoding="utf-8")
    print(f"Wrote {ini} (original kept as {backup.name})")


if __name__ == "__main__":
    main()
