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


if __name__ == "__main__":
    unittest.main()
