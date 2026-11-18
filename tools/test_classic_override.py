"""Build a tiny test mod: does the classic build read OverrideAbilityActionData?

    python -I tools/test_classic_override.py [--install]

Classic doesn't ship this table. The test mod adds it to classic's data with
two obvious changes and nothing else:

    Throw Stone (0x94)  range 4 -> 10
    Cure (0x01)         MP cost 6 -> 1

Launch FFT_classic.exe through Reloaded-II with the mod loader and this mod
enabled. If Throw Stone reaches 10 panels, classic reads the table and the
full mod can target both builds.
"""

from __future__ import annotations

import argparse
import json
import shutil
import sqlite3
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from build_mod import ff16tools, install  # noqa: E402
from remixed import ROOT, WORK, load_config  # noqa: E402

TEST_ID = "fftivc.test.classicoverride"
TEST_DIR = WORK / "tests" / TEST_ID
CHANGES = {0x94: {"Range": 10}, 0x01: {"MPCost": 1}}  # ability id -> column -> value


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--install", action="store_true", help="copy into the Reloaded-II Mods folder")
    args = parser.parse_args()
    cfg = load_config()

    base = WORK / "remaster" / "extract" / "enhanced" / "nxd" / "overrideabilityactiondata.nxd"
    if not base.exists():
        sys.exit("Run `python -I tools/build_mod.py` once first (it extracts the table's layout).")
    work = WORK / "tests" / "classic-override-build"
    shutil.rmtree(work, ignore_errors=True)
    (work / "in").mkdir(parents=True)
    shutil.copy(base, work / "in")
    ff16tools(cfg, "nxd-to-sqlite", "-i", work / "in", "-o", work / "table.sqlite")

    db = sqlite3.connect(work / "table.sqlite")
    # Start from a table that changes nothing (the enhanced rows are enhanced's own balance).
    db.execute("update OverrideAbilityActionData set Flags12 = '[]', Flags34 = '[]', Range = -1, EffectArea = -1, "
               "Vertical = -1, Element = -1, Formula = -1, X = -1, Y = -1, InflictStatus = -1, CT = -1, MPCost = -1")
    for ability, columns in CHANGES.items():
        for column, value in columns.items():
            db.execute(f"update OverrideAbilityActionData set {column} = ? where Key = ?", (value, ability))
    db.commit()
    db.close()
    ff16tools(cfg, "sqlite-to-nxd", "-i", work / "table.sqlite", "-o", work / "out", "-t", "OverrideAbilityActionData")

    shutil.rmtree(TEST_DIR, ignore_errors=True)
    target = TEST_DIR / "FFTIVC" / "data" / "classic" / "nxd" / "overrideabilityactiondata.nxd"
    target.parent.mkdir(parents=True)
    shutil.copy(next((work / "out").rglob("*.nxd")), target)
    config = {
        "ModId": TEST_ID, "ModName": "Classic override test (Ivalice ReMixed)", "ModAuthor": "Ivalice ReMixed port",
        "ModVersion": "0.0.1", "ModDescription": "Test only: Throw Stone range 10, Cure MP 1 on the classic build.",
        "ModDll": "", "ModIcon": "", "ModR2RManagedDll32": "", "ModR2RManagedDll64": "", "ModNativeDll32": "",
        "ModNativeDll64": "", "Tags": [], "CanUnload": True, "HasExports": False, "IsLibrary": False,
        "ReleaseMetadataFileName": f"{TEST_ID}.ReleaseMetadata.json", "IsUniversalMod": False,
        "ModDependencies": ["fftivc.utility.modloader"], "OptionalDependencies": [],
        "SupportedAppId": ["fft_classic.exe"],
    }
    (TEST_DIR / "ModConfig.json").write_text(json.dumps(config, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {TEST_DIR.relative_to(ROOT)}")

    if args.install:
        mods = cfg.get("target", {}).get("reloaded_mods")
        if not mods or not Path(mods).is_dir():
            sys.exit("Set target.reloaded_mods in local.toml to Reloaded-II's Mods folder.")
        install(TEST_DIR, Path(mods) / TEST_ID)


if __name__ == "__main__":
    main()
