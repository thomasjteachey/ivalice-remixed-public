"""Audit built event scripts for actor-staging mistakes.

    python -I tools/audit_event_staging.py
    python -I tools/audit_event_staging.py path/to/event123.e --strict

The ordinary run fails only on malformed scripts and unbalanced AddUnit groups.  It also reports
path-local warnings such as using an actor after RemoveUnit.  Erase and TeleportOut hide a drawable but
leave its logical actor available for off-screen positioning and animation, so those uses are valid.
``--strict`` makes warnings fail too.  Control-flow boundaries deliberately forget actor state: this
keeps branch joins and event phases from producing confident but false lifecycle claims.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path
import sys
from typing import Iterable

EIU_TOOLS = Path(__file__).resolve().parents[1] / "code" / "eiu" / "tools"
sys.path.insert(0, str(EIU_TOOLS))

from eiu_events.instructions import MARKER, REMASTER_LENGTHS  # noqa: E402


@dataclass(frozen=True)
class Finding:
    path: Path
    offset: int
    severity: str
    message: str


# Instructions whose first u16 parameter identifies the actor or group they require.  Only instructions for
# which that convention is established in the event table are included; a short list is preferable to
# guesses that make an audit noisy.
ACTOR_USES = {
    0x11: "UnitAnim", 0x1F: "Focus", 0x28: "WalkTo", 0x29: "WaitWalk", 0x2C: "FaceUnit2",
    0x2D: "RotateUnit", 0x4E: "UnitShadow",
    0x53: "FaceUnit", 0x5F: "WarpUnit", 0x64: "WaitRotateUnit", 0x68: "MirrorSprite",
    0x69: "FaceTile", 0x6F: "WaitSpriteMove", 0x70: "Jump", 0x79: "WalkToAnim",
    0x8C: "UnitAnimRotate", 0x92: "InflictStatus",
}

# A branch, label, subroutine boundary, or event end means a linear scan cannot soundly carry actor
# state onward.  Group structure is still checked across these instructions.
FLOW_BOUNDARIES = set(range(0x01, 0x10)) | {0xDB, 0xD0, 0xD2}


def actor_selector(params: bytes) -> tuple[str, int] | None:
    """Decode the event interpreter's u16 unit selector without mistaking a group for its low-byte actor id."""
    if len(params) < 2:
        return None
    selector = int.from_bytes(params[:2], "little")
    if 0 < selector < 0x100:
        return "actor", selector
    if 0x100 <= selector <= 0x103:
        return "group", selector - 0x100
    if selector == 0:
        return "all", selector
    return "special", selector


def instructions(code: bytes) -> Iterable[tuple[int, int, bytes]]:
    """Yield (file offset, opcode, parameters) for a remaster .e file."""
    at = len(MARKER) if code.startswith(MARKER) else 0
    while at < len(code):
        op = code[at]
        size = 1 + REMASTER_LENGTHS[op]
        if at + size > len(code):
            raise ValueError(f"instruction 0x{op:02X} at 0x{at:04X} is truncated")
        yield at, op, code[at + 1:at + size]
        at += size
        # Remaster files are fixed-size or padded in some sources. Nothing after the event's final END is code.
        if op == 0xDB:
            break


def audit(code: bytes, path: Path = Path("<memory>")) -> list[Finding]:
    """Return structural errors and conservative, straight-line actor lifecycle warnings."""
    found: list[Finding] = []
    state: dict[int, str] = {}
    add_group: int | None = None
    try:
        stream = list(instructions(code))
    except ValueError as error:
        return [Finding(path, 0, "error", str(error))]

    for offset, op, params in stream:
        if op == 0x49:  # AddUnitStart
            if add_group is not None:
                found.append(Finding(path, offset, "error",
                                     f"nested AddUnitStart (group opened at 0x{add_group:04X})"))
            else:
                add_group = offset
        elif op == 0x4A:  # AddUnitEnd
            if add_group is None:
                found.append(Finding(path, offset, "error", "AddUnitEnd without AddUnitStart"))
            else:
                add_group = None
        elif op == 0x4B and add_group is not None:  # WaitAddUnitEnd
            found.append(Finding(path, offset, "error", "WaitAddUnitEnd before AddUnitEnd"))

        if op == 0x0D and params == b"\xFF":  # GoTo the event's terminal label
            for actor, actor_state in state.items():
                if actor_state == "staged hidden":
                    found.append(Finding(path, offset, "warning",
                                         f"actor 0x{actor:02X} reaches event exit still staged hidden by AddUnit"))
        if op in FLOW_BOUNDARIES:
            state.clear()
            continue
        selected = actor_selector(params)
        if selected is None:
            continue
        kind, actor = selected
        # Group/all/special selectors do not name one lifecycle record. Carrying their low byte as an actor made
        # selector 0x0101 look like actor 1 and produced unsound use-after-remove warnings.
        if kind != "actor":
            continue
        previous = state.get(actor)
        if op == 0x3D:  # RemoveUnit definitely destroys the logical actor.
            if previous == "removed":
                found.append(Finding(path, offset, "warning",
                                     f"actor 0x{actor:02X} is removed twice on one path"))
            state[actor] = "removed"
        elif op == 0x99:
            # BlueRemoveUnit is conditional on the unit's team. ReMixed deliberately follows it with operations on
            # the same selector: a matching-side unit was removed, while a different-side unit remains. Without ENTD
            # and runtime team state this scan cannot soundly mark the actor removed.
            pass
        elif op == 0x46:  # Erase
            if previous in ("hidden", "removed"):
                found.append(Finding(path, offset, "warning",
                                     f"actor 0x{actor:02X} is erased while {previous}"))
            state[actor] = "hidden"
        elif op == 0x45:  # AddUnit: its third parameter stages the actor hidden until Draw when nonzero
            state[actor] = "staged hidden" if len(params) >= 3 and params[2] else "visible"
        elif op in (0x44, 0x47, 0x98):  # Draw / AddGhostUnit / TeleportIn
            state[actor] = "visible"
        elif op == 0x94:  # TeleportOut
            state[actor] = "hidden"
        # Erase/TeleportOut only hide the drawable. Events routinely position, rotate, animate, or wait on that logical
        # actor before a later Draw/TeleportIn (sometimes across a label), so only a definite RemoveUnit is unsafe.
        elif op in ACTOR_USES and previous == "removed":
            found.append(Finding(path, offset, "warning",
                                 f"{ACTOR_USES[op]} uses actor 0x{actor:02X} after it was {previous}"))

    if add_group is not None:
        found.append(Finding(path, add_group, "error", "AddUnitStart has no AddUnitEnd"))
    return found


def event_files(paths: list[Path]) -> list[Path]:
    if not paths:
        paths = [Path("mod/fftivc.gameplay.ivaliceremixed/FFTIVC/data/enhanced/script/enhanced")]
    files: list[Path] = []
    for path in paths:
        files.extend(sorted(path.rglob("*.e")) if path.is_dir() else [path])
    # The mod directory also contains a few copied native remaster events. They do not use the Event Instruction
    # Upgrade marker and may have native trailing data; this audit is for the converted/upgraded scripts we control.
    return [path for path in files if path.read_bytes().startswith(MARKER)]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("paths", nargs="*", type=Path, help=".e files or directories (default: built enhanced events)")
    parser.add_argument("--strict", action="store_true", help="return failure for lifecycle warnings too")
    args = parser.parse_args()
    files = event_files(args.paths)
    if not files:
        parser.error("no .e files found; build the enhanced mod or name an event file")
    findings = [finding for path in files for finding in audit(path.read_bytes(), path)]
    for finding in findings:
        print(f"{finding.path}:0x{finding.offset:04X}: {finding.severity}: {finding.message}")
    errors = sum(f.severity == "error" for f in findings)
    warnings = len(findings) - errors
    print(f"Audited {len(files)} event file(s): {errors} error(s), {warnings} warning(s).")
    return 1 if errors or (args.strict and warnings) else 0


if __name__ == "__main__":
    raise SystemExit(main())

