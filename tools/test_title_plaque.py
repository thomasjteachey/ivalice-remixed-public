from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import title_plaque as tp


class TitlePlaqueTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.glyphs = json.loads(tp.LAYOUT.read_text(encoding="utf-8"))["glyphs"]

    def test_every_character_is_drawn(self) -> None:
        self.assertEqual(set(self.glyphs), set(tp.CHARACTERS))

    def test_the_plaques_spacing(self) -> None:
        placed = tp.layout("IVALICE REMIXED 0.9.13", self.glyphs)
        ends = [x + self.glyphs[c]["ink"] for c, x in placed]
        gaps = [placed[k + 1][1] - ends[k] for k in range(len(placed) - 1)]
        # I-V 7, V-A kerned 2, ...E then the word gap 20 to R, digits and dots 5
        self.assertEqual(gaps[:3], [tp.GAP, 2, tp.GAP])
        self.assertEqual(gaps[6], tp.WORD_GAP)
        self.assertEqual(gaps[-5:], [tp.NUMBER_GAP] * 5)
        middle = (placed[0][1] + ends[-1]) / 2
        self.assertLessEqual(abs(middle - tp.CENTRE), 1)
        self.assertGreaterEqual(placed[0][1], tp.LEFT)
        self.assertLessEqual(ends[-1], tp.RIGHT)

    def test_a_long_version_closes_up_and_a_longer_one_is_refused(self) -> None:
        placed = tp.layout("IVALICE REMIXED 10.12.130-RC.2", self.glyphs)
        self.assertGreaterEqual(placed[0][1], tp.LEFT)
        self.assertLessEqual(placed[-1][1] + self.glyphs[placed[-1][0]]["ink"], tp.RIGHT)
        with self.assertRaises(ValueError):
            tp.layout("IVALICE REMIXED 123.456.789-BETA.10", self.glyphs)
        with self.assertRaises(ValueError):
            tp.layout("IVALICE REMIXED 0.9_1", self.glyphs)

    def test_only_the_plaque_changes(self) -> None:
        width, height, base = tp.read_png(tp.BASE)
        with tempfile.TemporaryDirectory() as folder:
            out = Path(folder) / "title.png"
            tp.draw_title("IVALICE REMIXED 0.9.13", out)
            self.assertEqual(tp.read_png(out)[:2], (width, height))
            drawn = tp.read_png(out)[2]
        row = width * 4
        top, bottom = tp.TOP * row, (tp.TOP + tp.ROWS) * row
        self.assertEqual(drawn[:top], base[:top])
        self.assertEqual(drawn[bottom:], base[bottom:])
        self.assertNotEqual(drawn[top:bottom], base[top:bottom])

    def test_png_round_trip(self) -> None:
        pixels = bytearray(range(256)) * 3
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "small.png"
            tp.write_png(path, 16, 12, pixels)
            self.assertEqual(tp.read_png(path), (16, 12, pixels))


if __name__ == "__main__":
    unittest.main()
