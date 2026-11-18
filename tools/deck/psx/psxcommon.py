"""Shared setup for scripted runs of the PS1's ReMixed, on the Deck (~/psxrun) or the PC (work/psx/run); docs/TESTING.md.

The disc is the build's patched copy (tools/remixed.py prepare; on the Deck, a copy of it); PSX_GAME overrides its
path, PSX_CORE the core (RetroArch's, by name: swanstation by default), PSX_CORES the cores folder and PSX_HOME the
run folder (states/, shots/, and system/ with the US BIOS scph5501.bin and the core's memory card).
"""
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from psxrun import Emu  # noqa: E402

DISC_NAME = "Final Fantasy Tactics ReMixed v1.2.64.23.cue"
if os.name == "nt":  # the PC: Steam's RetroArch, the repo's work folder
    ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
    HOME = os.environ.get("PSX_HOME", os.path.join(ROOT, "work", "psx", "run"))
    CORES = os.environ.get("PSX_CORES", r"C:\Program Files (x86)\Steam\steamapps\common\RetroArch\cores")
    # A state names its disc and the core takes it back only for a disc of that file name (any folder), so the
    # build's disc gets a .cue under the Deck's name: states and the memory card then move both ways.
    BUILT = os.path.join(ROOT, "work", "disc", "remixed", "FFT ReMixed-v1.2.64.23.cue")
    GAME = os.environ.get("PSX_GAME", os.path.join(os.path.dirname(BUILT), DISC_NAME))
    if not os.path.exists(GAME) and os.path.exists(BUILT):
        with open(BUILT, "rb") as cue, open(GAME, "wb") as named:
            named.write(cue.read())
    LIBRARY = "_libretro.dll"
else:  # the Deck: the Flatpak RetroArch's cores
    HOME = os.path.expanduser(os.environ.get("PSX_HOME", "~/psxrun"))
    CORES = os.path.expanduser(os.environ.get(
        "PSX_CORES", "~/.var/app/org.libretro.RetroArch/config/retroarch/cores"))
    GAME = os.path.expanduser(os.environ.get("PSX_GAME", "~/Emulation/roms/psx/" + DISC_NAME))
    LIBRARY = "_libretro.so"
CORE = os.environ.get("PSX_CORE", "swanstation")
SHOTS = os.path.join(HOME, "shots")
STATES = os.path.join(HOME, "states")
os.makedirs(SHOTS, exist_ok=True)
os.makedirs(STATES, exist_ok=True)
began = time.time()


def log(*parts):
    print(f"{time.time() - began:6.1f} s:", *parts, flush=True)


def start(state=None, card=None, **options):
    """The core with the disc loaded, and a save state (from the run folder's states/) if given."""
    if CORE == "swanstation":
        options.setdefault("swanstation_GPU_Renderer", "Software")
    emu = Emu(os.path.join(CORES, CORE + LIBRARY), os.path.join(HOME, "system"), GAME, options=options,
              card=os.path.join(HOME, card) if card else None)
    if state:
        emu.load(os.path.join(STATES, state))
    return emu


def film(emu, prefix, frames, every, buttons="", mash="", mash_every=30):
    """Run frames holding buttons (and tapping mash every mash_every frames), a shot every `every` frames
    (shots/prefix-NNNNN.png, numbered by the frame within this film)."""
    for i in range(1, frames + 1):
        held = "+".join(b for b in (buttons, mash if mash and i % mash_every < 4 else "") if b)
        if i % every == 0:
            emu.shot(os.path.join(SHOTS, f"{prefix}-{i:05d}.png"), held)
        else:
            emu.run(1, held)
