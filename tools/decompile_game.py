"""Decompile the whole remaster exe with Ghidra, for reading and searching its code as C.

    python -I tools/decompile_game.py [--ghidra C:/Projects/Gamedev/tools/ghidra_12.2_PUBLIC] [--memory 8G]

Imports FFT_enhanced.exe into a Ghidra project under work/ghidra/project, runs Ghidra's full auto-analysis, then
tools/ghidra/ExportDecompiled.java writes every function as C into work/ghidra/decompiled/<rva>.c (64 KB of code per
file; each function starts with "// ==== <rva> <name>"). Ghidra's names are FUN_<address> with the image base
0x140000000, so the image-relative address the docs use is the name's address minus 0x140000000. Takes a while (the
analysis and the decompile each run for a good part of an hour); the log goes to work/ghidra/decompile.log. Running it
again starts over (the project and the output are replaced). Needs Ghidra 12.2 and Java 25 (JAVA_HOME or on PATH).
"""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))

from remixed import load_config  # noqa: E402

DEFAULT_GHIDRA = Path("C:/Projects/Gamedev/tools/ghidra_12.2_PUBLIC")


def game_exe() -> Path:
    game = load_config().get("target", {}).get("tic_dir")
    if game:
        return Path(game) / "FFT_enhanced.exe"
    return Path("D:/SteamLibrary/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/FFT_enhanced.exe")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--ghidra", type=Path, default=DEFAULT_GHIDRA)
    parser.add_argument("--memory", default="8G", help="Ghidra's heap (default 8G)")
    parser.add_argument("--exe", type=Path, default=None)
    args = parser.parse_args()
    exe = args.exe or game_exe()
    headless = args.ghidra / "support" / "analyzeHeadless.bat"
    if not headless.exists():
        sys.exit(f"No Ghidra at {args.ghidra} (support/analyzeHeadless.bat).")
    if not exe.exists():
        sys.exit(f"No exe at {exe}.")
    work = ROOT / "work" / "ghidra"
    project, out = work / "project", work / "decompiled"
    for folder in (project, out):
        shutil.rmtree(folder, ignore_errors=True)
        folder.mkdir(parents=True)
    env = dict(os.environ, GHIDRA_HEADLESS_MAXMEM=args.memory)
    if "JAVA_HOME" not in env and shutil.which("java"):
        env["JAVA_HOME"] = str(Path(shutil.which("java")).resolve().parent.parent)  # Ghidra would ask for it otherwise
    command = [str(headless), str(project), "fft", "-import", str(exe), "-overwrite",
               "-scriptPath", str(ROOT / "tools" / "ghidra"), "-postScript", "ExportDecompiled.java", str(out)]
    with open(work / "decompile.log", "w", encoding="utf-8") as log:
        result = subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT)
    files = sorted(out.glob("*.c"))
    print(f"exit {result.returncode}; {len(files)} files in {out} "
          f"({sum(f.stat().st_size for f in files) / 1e6:.0f} MB); log in {work / 'decompile.log'}")


if __name__ == "__main__":
    main()
