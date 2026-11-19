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
    def setUp(self) -> None:
        self.logs = self.enterContext(mock.patch.object(quick_test.harness, "log_lines", return_value=[]))

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

    def test_name_keeps_an_apostrophe(self) -> None:
        path = self.case("name Mandalia's join screen\nexpect unit battle_id=0x91 count=0\n")
        with mock.patch.object(quick_test.harness, "send", return_value=UNITS):
            with self.assertRaisesRegex(quick_test.CaseFailure, r"^Mandalia's join screen:2: unit battle_id=0x91"):
                quick_test.run_case(path)

    def test_expect_count_zero_wants_none(self) -> None:
        path = self.case("expect unit battle_id=0x92 count=0\nexpect unit battle_id=0x91 count=0\n")
        with mock.patch.object(quick_test.harness, "send", return_value=UNITS):
            with self.assertRaisesRegex(quick_test.CaseFailure, r"case:2: unit battle_id=0x91 count=0: "
                                                                r"expected 0, found 1"):
                quick_test.run_case(path)

    def test_log_waits_ignore_old_lines_and_consume_each_new_match_once(self) -> None:
        path = self.case('wait log "loaded event 404" timeout=1\nwait log "loaded event 404" timeout=1\n')
        self.logs.return_value = ["loaded event 404"]
        with mock.patch.object(quick_test.harness, "wait_log", side_effect=("loaded event 404", None)) as wait:
            with self.assertRaisesRegex(quick_test.CaseFailure, "log did not match"):
                quick_test.run_case(path)
        self.assertEqual([mock.call("loaded event 404", 1, before=1), mock.call("loaded event 404", 1, before=2)],
                         wait.call_args_list)


class RosterRecords(unittest.TestCase):
    def test_reads_slots_sprites_and_name_ids(self):
        size, name_at = quick_test.harness.ROSTER_SIZE, quick_test.harness.ROSTER_NAME_ID
        data = bytearray(size * 3)
        data[1] = 0xFF  # an empty slot
        data[size:size + 4] = bytes([0x82, 0x01, 0x5E, 0x00])
        data[size + name_at:size + name_at + 4] = (0x76).to_bytes(4, "little")
        found = quick_test.roster_records(bytes(data))
        self.assertEqual([(r["slot"], r["sprites"], r["job"], r["name_id"]) for r in found], [(1, 0x82, 0x5E, 0x76)])
        self.assertEqual(quick_test.find("roster", {"name_id": 0x76}, found), found)

    def test_reads_job_fields(self):
        # Lorne's record as 0x0ED leaves it: the Squire, the Chemist and the Bard open; Battle Song (the Bard's 4th), 61
        # JP of 161; an Archer's Equip Crossbow (its 3rd reaction/support/movement slot).
        size = quick_test.harness.ROSTER_SIZE
        data = bytearray(size)
        data[0:4] = bytes([0xB4, 0x01, 0x5B, 0x80])
        unlocked, learned, jp, total = quick_test.ROSTER_JOBS
        data[unlocked:unlocked + 3] = bytes([0xC0, 0x00, 0x40])
        data[learned + 3 * 17] = 0x10
        data[learned + 3 * 3 + 2] = 0x20
        data[jp + 2 * 17:jp + 2 * 17 + 2] = (61).to_bytes(2, "little")
        data[total + 2 * 17:total + 2 * 17 + 2] = (161).to_bytes(2, "little")
        data[quick_test.ROSTER_LEVELS + 8] = 0x41  # job 16 at 4, the Bard (17) at 1
        data[quick_test.ROSTER_LEVELS + 10] = 0x80  # the Dark Knight (20) at 8
        data[quick_test.ROSTER_ERRAND] = 0x80
        found = quick_test.roster_records(bytes(data))
        wanted = {"job": 0x5B, "unlocked": 0xC00040, "bard_learned": 0x100000, "bard_jp": 61, "bard_total": 161,
                  "archer_learned": 0x20, "dancer_learned": 0, "dancer_jp": 0, "bard_level": 1, "dark_knight_level": 8,
                  "errand": 0x80}
        self.assertEqual(quick_test.find("roster", wanted, found), found)
        quick_test.expect_record("roster", {"name_id": 0x69, "count": 0}, "case", 1, found)
        with self.assertRaises(quick_test.CaseFailure):
            quick_test.expect_record("roster", {"job": 0x5B, "count": 0}, "case", 1, found)


if __name__ == "__main__":
    unittest.main()
