"""Names for FFT's PS1 data (abilities, jobs, items...) from FFTPatcher's lists.

FFTPatcher is GPL-3.0, so its lists aren't copied into this repo. They're
downloaded on first use into work/cache/fftpatcher/.
"""

from __future__ import annotations

import urllib.request
from pathlib import Path
from xml.etree import ElementTree

SOURCE = "https://raw.githubusercontent.com/Glain/FFTPatcher/master/PatcherLib.Resources/Resources/PSX-US/"

# kind: (file, element path, id attribute, number base)
LISTS = {
    "abilities": ("Abilities/Abilities.xml", "Ability", "value", 10),
    "ability_types": ("Abilities/Strings.xml", "Types/string", "value", 10),
    "jobs": ("Jobs.xml", "Job", "offset", 16),
    "skillsets": ("SkillSets.xml", "SkillSet", "byte", 16),
    "items": ("Items/Items.xml", "Item", "offset", 10),
    "statuses": ("StatusNames.xml", "Status", "offset", 10),
    "events": ("EventNames.xml", "Event", "value", 16),
    "special_names": ("SpecialNames.xml", "SpecialName", "byte", 16),
    "sprite_sets": ("SpriteSets.xml", "Sprite", "byte", 16),
}


class Names:
    def __init__(self, cache_dir: Path):
        self._lists = {kind: _load(cache_dir, *spec) for kind, spec in LISTS.items()}

    def get(self, kind: str, i: int) -> str:
        return self._lists[kind].get(i, "")

    def label(self, kind: str, i: int, width: int = 2) -> str:
        """'Cure (001)', or just the hex id when there's no name."""
        name = self.get(kind, i)
        return f"{name} ({i:0{width}X})" if name else f"{i:0{width}X}"


def _load(cache_dir: Path, file: str, path: str, attr: str, base: int) -> dict[int, str]:
    local = cache_dir / file
    if not local.exists():
        local.parent.mkdir(parents=True, exist_ok=True)
        print(f"Downloading FFTPatcher name list {file} ...")
        with urllib.request.urlopen(SOURCE + file, timeout=30) as response:
            local.write_bytes(response.read())
    root = ElementTree.fromstring(local.read_bytes())
    names: dict[int, str] = {}
    for node in root.iterfind(path):
        key = node.get(attr)
        if key is None:
            continue
        try:
            names[int(key, base)] = (node.get("name") or node.text or "").strip()
        except ValueError:
            continue
    return names
