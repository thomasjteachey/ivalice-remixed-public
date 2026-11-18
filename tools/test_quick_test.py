from __future__ import annotations

import tempfile
import unittest
import sys
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))

import quick_test


UNITS = """autopilot: the battle units at 0x1234
unit 6: sprites 0xB5 id 0x06 job 0x5C team 0x04 skillsets 0x17 0x00 level 20 graphic 0x83 palette 0x00 battle id 0x90 name 0x69
unit 9: sprites 0xB4 id 0x09 job 0x5B team 0x04 skillsets 0x16 0x00 level 20 graphic 0x38 palette 0x00 battle id 0x91 name 0x6A"""

ENTRIES = """scenario 0x79, the mod's private entry at 0x1234
entry 7: sprites 0x17 flags 0x8B name 0x17 job 0x0B secondary 0x00 team 0x84 x 0x01 y 0x06 battle id 0x17
entry 8: sprites 0x1E flags 0x4B name 0x1E job 0x0B secondary 0x0A team 0x84 x 0x00 y 0x02 battle id 0x1E"""


class QuickCaseTests(unittest.TestCase):
    def case(self, text: str) -> Path:
        folder = Path(tempfile.mkdtemp())
        path = folder / "case.txt"
        path.write_text(text, encoding="utf-8")
        return path

    def test_live_unit_fields_pass(self) -> None:
        path = self.case("scenario 0x194\nwait unit battle_id=0x91 timeout=1\n"
                         "expect unit battle_id=0x91 job=0x5b graphic=0x38\n")
        with mock.patch.object(quick_test.harness, "on_world_map", return_value=True), \
                mock.patch.object(quick_test.harness, "send", side_effect=lambda command: UNITS if command == "units" else "ok"):
            quick_test.run_case(path)

    def test_entry_flags_pass(self) -> None:
        path = self.case("expect entry battle_id=0x17 flags=0x8b sprites=0x17\n")
        with mock.patch.object(quick_test.harness, "send", return_value=ENTRIES):
            quick_test.run_case(path)

    def test_wrong_expectation_fails(self) -> None:
        path = self.case("expect unit battle_id=0x91 graphic=0x5b\n")
        with mock.patch.object(quick_test.harness, "send", return_value=UNITS):
            with self.assertRaisesRegex(quick_test.CaseFailure, "found 0"):
                quick_test.run_case(path)


if __name__ == "__main__":
    unittest.main()
