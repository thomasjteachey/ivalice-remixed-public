from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))

import build_mod  # noqa: E402


class StockEventRepairsTests(unittest.TestCase):
    def stock(self) -> bytearray:
        # A small event with the same sequence as Riovanes event 281: the unit lookup, the six battle-end screens with
        # their waits, DismissUnit, END.
        code = bytearray([0xC3, 0x19, 0])
        for state in range(2, 8):
            code += bytes([0x78, state, 60, 0xE5, 0x38, 0])
        code += bytes([0x7A, 0x19, 0, 0xDB])
        return code

    def test_an_earlier_builds_guard_comes_out(self) -> None:
        # 0.9.32 to 0.9.38 put upgrade instructions around the screens of a script the remaster's own interpreter
        # runs, which ends an event at an opcode below 0x10: the scene stopped at the guard's first byte.
        before, after = build_mod.STRIPPED_SCREEN_GUARDS["event281.e"]
        stock = self.stock()
        code = bytearray(stock)
        code[-4:-4] = after
        code[3:3] = before
        self.assertTrue(build_mod.strip_world_unit_screen_guard(code, "event281.e"))
        self.assertEqual(bytes(stock), bytes(code))
        self.assertFalse(build_mod.strip_world_unit_screen_guard(code, "event281.e"))
        self.assertEqual(bytes(stock), bytes(code))

    def test_half_a_guard_stops_the_build(self) -> None:
        before, _ = build_mod.STRIPPED_SCREEN_GUARDS["event281.e"]
        code = self.stock()
        code[3:3] = before
        with self.assertRaises(ValueError):
            build_mod.strip_world_unit_screen_guard(code, "event281.e")

    def test_unrelated_event_is_untouched(self) -> None:
        before, after = build_mod.STRIPPED_SCREEN_GUARDS["event281.e"]
        code = bytearray(before + after + bytes([0xDB]))
        self.assertFalse(build_mod.strip_world_unit_screen_guard(code, "event280.e"))
        self.assertEqual(before + after + bytes([0xDB]), bytes(code))


class MapMovementTests(unittest.TestCase):
    """The redrawn maps' moving parts in ReMixed's scenes (MAP_MOVEMENT_SCENES): made-up events on made-up maps."""
    LENGTHS = bytes(256)[:0x55] + bytes([2]) + bytes(256)[0x56:]  # only 0x55 has parameters (two bytes)
    THEIRS = next(iter(build_mod.REMIXED_SCENARIOS))
    OURS = build_mod.REMIXED_SCENARIOS[THEIRS]

    def rows(self, code: bytes, where: int, scenes: dict) -> dict[int, int]:
        records = {theirs: bytes([0, 0, 99]) for theirs in build_mod.REMIXED_SCENARIOS}
        records[self.THEIRS] = bytes([0, 0, where])
        remaster_maps = {scene: 200 for scene in build_mod.REMASTER_MAP_MOVEMENTS}
        remaster_maps[0x1B2] = 1
        return dict(build_mod.map_movement_rows(lambda n: code if n == self.THEIRS else bytes([0xDB]), self.LENGTHS,
                                                records, remaster_maps, scenes))

    def test_field_objects_are_read_from_the_code(self) -> None:
        self.assertEqual({1, 4}, build_mod.field_objects(bytes([0x55, 1, 0, 0x57, 0x55, 4, 0, 0xDB]), self.LENGTHS))

    def test_a_scene_takes_its_stand_in_and_the_rest_none(self) -> None:
        rows = self.rows(bytes([0x55, 1, 0, 0xDB]), 1, {self.THEIRS: 0x1B2})
        self.assertEqual(0x1B2, rows[self.OURS])
        self.assertTrue(all(scene == 0 for ours, scene in rows.items() if ours != self.OURS))
        self.assertTrue(set(build_mod.REMIXED_SCENARIOS.values()) <= set(rows))

    def test_a_moving_part_on_a_listed_map_needs_its_scene(self) -> None:
        with self.assertRaises(SystemExit):
            self.rows(bytes([0x55, 1, 0, 0xDB]), 1, {})
        self.assertEqual(0, self.rows(bytes([0x55, 1, 0, 0xDB]), 2, {})[self.OURS])  # no listed scene on map 2
        self.assertEqual(0, self.rows(bytes([0x55, 1, 0, 0xDB]), 1, {self.THEIRS: None})[self.OURS])

    def test_a_stand_in_on_another_map_or_for_other_objects_stops_the_build(self) -> None:
        with self.assertRaises(SystemExit):
            self.rows(bytes([0x55, 1, 0, 0xDB]), 2, {self.THEIRS: 0x1B2})
        with self.assertRaises(SystemExit):
            self.rows(bytes([0x55, 3, 0, 0xDB]), 1, {self.THEIRS: 0x1B2})

    def test_the_remasters_own_instruction_is_a_routine_call_after_its_object(self) -> None:
        call = build_mod.map_animation_call(5, 0)
        self.assertEqual(bytes.fromhex("ae 7e 0b 05 00 ae 7f 0b 00 00 cf c8 00 00 00"), call)
        self.assertIn((bytes([0x55, 6, 0]), call), build_mod.EVENT_CODE_INSERTS[0x9D])
        for event in build_mod.MAP_ANIMATION_CALLS:
            self.assertIn(event, build_mod.MAP_MOVEMENT_SCENES)

    def test_zeakdens_door_is_shut_from_the_start_and_opened_after_its_object(self) -> None:
        shut = build_mod.map_animation_call(0, build_mod.MAP_ANIMATION_AT_END)
        opened = build_mod.map_animation_call(0, 1)
        self.assertEqual(bytes.fromhex("ae 7e 0b 00 00 ae 7f 0b 02 00 cf c8 00 00 00"), shut)
        self.assertIn((bytes([0x0E, 0x00]), shut), build_mod.EVENT_CODE_INSERTS[0x102])
        self.assertIn((bytes([0x55, 0, 0]), opened), build_mod.EVENT_CODE_INSERTS[0x102])
        self.assertIn((bytes([0x55, 0, 0]), opened), build_mod.EVENT_CODE_INSERTS[0x103])
        # Each is found in ReMixed's code by the first instruction of those bytes: the event's own, not the calls'.
        lengths = bytearray(256)
        lengths[0x55], lengths[0xAE], lengths[0xCF], lengths[0x0D] = 2, 4, 4, 1
        code = bytes([0x0D, 0xFF, 0x0E, 0x00, 0x55, 0, 0, 0x57, 0xDB])
        out = build_mod.insert_code(code, bytes(lengths), [(bytes([0x0E, 0x00]), shut), (bytes([0x55, 0, 0]), opened)])
        self.assertEqual(bytes([0x0D, 0xFF, 0x0E, 0x00]) + shut + bytes([0x55, 0, 0]) + opened + bytes([0x57, 0xDB]), out)


if __name__ == "__main__":
    unittest.main()
