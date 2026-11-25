"""Offline tests for the build's English name tables (build_mod.fill_command_icons).

    python -I tools/test_name_tables.py
"""

from pathlib import Path
import sqlite3
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))

import build_mod  # noqa: E402


def tables(commands: dict[int, tuple[int, str | None]], jobs: dict[int, int]) -> sqlite3.Connection:
    """The two tables as FF16Tools exports them, cut down to the columns fill_command_icons reads."""
    db = sqlite3.connect(":memory:")
    db.execute('create table "JobCommand-en" (Key integer, IconId integer, Name text)')
    db.execute('create table "Job-en" (Key integer, "jobcommand+Id" integer)')
    db.executemany('insert into "JobCommand-en" values (?, ?, ?)', [(k, i, n) for k, (i, n) in commands.items()])
    db.executemany('insert into "Job-en" values (?, ?)', jobs.items())
    return db


def icons(db: sqlite3.Connection) -> dict[int, int]:
    return dict(db.execute('select Key, IconId from "JobCommand-en"').fetchall())


class CommandIconTests(unittest.TestCase):
    def test_named_skillsets_without_an_icon_get_one(self) -> None:
        db = tables({0x06: (2, "Items"), 0x27: (21, "Fell Sword"), 0xAC: (22, "Bio"),
                     0x52: (0, "Spell Strike"), 0x56: (0, "Combat Skill"), 0x96: (0, "Bio"), 0x97: (0, "Dark Cloud"),
                     0x90: (0, "Summon Magic"), 0x58: (0, None)},
                    {0x21: 0, 0x11: 0x27, 0x35: 0x06, 0x96: 0xAC, 0x9B: 0})
        given = build_mod.fill_command_icons(db, "JobCommand-en", "Job-en",
                                             {0x21: 0x52, 0x11: 0x56, 0x35: 0x56, 0x96: 0x96, 0x9B: 0x97})
        # Valmafra's Arc Witch had no command: a special job's icon. Combat Skill takes the Fell Sword's (the General's
        # Items icon is a generic job's, not a special one); the Reaver's list keeps its boss icon; a skillset no job
        # uses (a story unit's secondary) gets a special job's.
        self.assertEqual({0x52: 21, 0x56: 21, 0x96: 22, 0x97: 21, 0x90: 21}, given)
        self.assertEqual(21, icons(db)[0x52])
        self.assertEqual(0, icons(db)[0x58])  # unnamed rows stay as they are
        self.assertEqual(2, icons(db)[0x06])  # rows with an icon too
        self.assertEqual([], db.execute('select Key from "JobCommand-en" where IconId = 0 and Name is not null'
                                        ).fetchall())

    def test_jobs_that_disagree_get_a_special_jobs_icon(self) -> None:
        db = tables({0x27: (21, "Fell Sword"), 0xAC: (22, "Bio"), 0x50: (0, "Mixed")}, {1: 0x27, 2: 0xAC})
        self.assertEqual({0x50: 21}, build_mod.fill_command_icons(db, "JobCommand-en", "Job-en", {1: 0x50, 2: 0x50}))


if __name__ == "__main__":
    unittest.main()
