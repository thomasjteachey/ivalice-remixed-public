"""Test variant of the installed mod: abilities Ramza has from the start run a ported formula.

    python -I tools/test_formulas.py [--formula 0x46] [--x 8] [--abilities 0x93,0x94]
    python -I tools/test_formulas.py --restore

ReMixed's formulas belong to abilities that show up late (Cloud's Limits) or
on monsters. To check one on a new game, this points Ramza's chapter 1 Dash
and Throw Stone at it, in the installed copy of the mod only (Reloaded-II's
Mods folder). With formula 0x46 and X = 8, each hit should do half of what a
plain weapon strike would. The DLL logs each distinct result as a "trace"
line in the Reloaded-II log.

The next `build_mod.py --install` undoes it; so does --restore, without a build.
"""

from __future__ import annotations

import argparse
import shutil
import sqlite3
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from build_mod import MOD_DIR, MOD_ID, WORK, ff16tools  # noqa: E402
from remixed import load_config  # noqa: E402

NXD = Path("FFTIVC") / "data" / "enhanced" / "nxd" / "overrideabilityactiondata.nxd"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--formula", type=lambda s: int(s, 0), default=0x46)
    parser.add_argument("--x", type=int, default=8)
    parser.add_argument("--abilities", default="0x93,0x94", help="ability ids (default: Dash, Throw Stone)")
    parser.add_argument("--restore", action="store_true", help="put the built mod's table back")
    args = parser.parse_args()

    cfg = load_config()
    built = MOD_DIR / NXD
    installed = Path(cfg["target"]["reloaded_mods"]) / MOD_ID / NXD
    if not built.exists() or not installed.parent.exists():
        sys.exit("Build and install the mod first: python -I tools/build_mod.py --install")
    if args.restore:
        shutil.copy2(built, installed)
        print(f"Restored {installed}")
        return

    work = WORK / "remaster" / "build" / "test-formulas"
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    shutil.copy(built, work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "table.sqlite")
    db = sqlite3.connect(work / "table.sqlite")
    ids = [int(a, 0) for a in args.abilities.split(",")]
    for a in ids:
        db.execute("update OverrideAbilityActionData set Formula = ?, X = ? where Key = ?", (args.formula, args.x, a))
    db.commit()
    db.close()
    ff16tools(cfg, "sqlite-to-nxd", "-i", work / "table.sqlite", "-o", work / "out", "-t", "OverrideAbilityActionData")
    shutil.copy2(next((work / "out").rglob("*.nxd")), installed)
    print(f"Abilities {', '.join(f'0x{a:03X}' for a in ids)} now use formula 0x{args.formula:02X} with X = {args.x} "
          f"in {installed}")


if __name__ == "__main__":
    main()
