from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))

import build_mod  # noqa: E402


class StockEventRepairsTests(unittest.TestCase):
    def test_missing_or_level_zero_world_unit_skips_roster_screens(self) -> None:
        # A small event with the same structural sequence as Riovanes event 281. The length table contains parameter
        # counts, as the remaster's table does; irrelevant opcodes here have no parameters.
        lengths = bytearray(256)
        lengths[0xC3] = lengths[0x78] = lengths[0xE5] = lengths[0x7A] = 2
        code = bytearray([0xC3, 0x19, 0])
        for state in range(2, 8):
            code += bytes([0x78, state, 60, 0xE5, 0x38, 0])
        code += bytes([0x7A, 0x19, 0, 0xDB])

        self.assertTrue(build_mod.guard_missing_world_unit_screens(bytes(lengths), code, "event281.e"))
        self.assertEqual(bytes.fromhex("01 AF 02 00 00 00 C1 00 16 00 02 01 01 AF 00 00 02 00"), code[3:21])
        self.assertEqual(bytes.fromhex("03 AE 01 00 FF 00 C4 01 01 00 02 01 04 04 7A"), code[-18:-3])
        once = bytes(code)
        self.assertTrue(build_mod.guard_missing_world_unit_screens(bytes(lengths), code, "event281.e"))
        self.assertEqual(once, bytes(code))

    def test_unrelated_event_is_untouched(self) -> None:
        code = bytearray([0xDB])
        self.assertFalse(build_mod.guard_missing_world_unit_screens(bytes(256), code, "event280.e"))
        self.assertEqual(bytes([0xDB]), bytes(code))


if __name__ == "__main__":
    unittest.main()
