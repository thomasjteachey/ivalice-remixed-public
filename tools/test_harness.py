from __future__ import annotations

import os
import struct
import sys
import tempfile
import unittest
import zlib
from pathlib import Path
from unittest import mock

# harness computes its log/save locations at import time.  Harmless temporary defaults keep these parser and
# orchestration tests portable; no test opens the pipe, starts the game, or touches either directory.
os.environ.setdefault("APPDATA", str(Path(tempfile.gettempdir()) / "ivalice-remixed-test-appdata"))
os.environ.setdefault("USERPROFILE", str(Path(tempfile.gettempdir()) / "ivalice-remixed-test-profile"))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import harness  # noqa: E402


class CheckpointArgumentsTests(unittest.TestCase):
    def parse(self, *args: str):
        return harness.build_parser().parse_args(["checkpoint", *args])

    def test_existing_direct_phase_form_stays_valid(self) -> None:
        args = self.parse("34D", "7C", "5", "--messages", "30", "49")
        harness.validate_checkpoint_args(args)
        self.assertFalse(args.victory)
        self.assertEqual(5, args.phase)

    def test_victory_form_omits_phase(self) -> None:
        args = self.parse("34D", "7C", "--victory", "--advance-phase", "2", "4", "--dismiss-end-phase", "2",
                          "--messages", "30", "49", "--after-event")
        harness.validate_checkpoint_args(args)
        self.assertIsNone(args.phase)
        self.assertTrue(args.after_event)

    def test_direct_checkpoint_requires_phase(self) -> None:
        args = self.parse("34D", "7C", "--messages", "30")
        with self.assertRaisesRegex(SystemExit, "needs PHASE"):
            harness.validate_checkpoint_args(args)

    def test_victory_rejects_forced_phase(self) -> None:
        args = self.parse("34D", "7C", "5", "--victory", "--condition-phase", "5", "--messages", "30")
        with self.assertRaisesRegex(SystemExit, "omit PHASE"):
            harness.validate_checkpoint_args(args)

    def test_enemy_indexes_require_victory(self) -> None:
        args = self.parse("34D", "7C", "5", "--messages", "30", "--enemies", "3", "4")
        with self.assertRaisesRegex(SystemExit, "only valid with --victory"):
            harness.validate_checkpoint_args(args)

    def test_victory_requires_condition_phase(self) -> None:
        args = self.parse("34D", "34D", "--victory", "--messages", "30")
        with self.assertRaisesRegex(SystemExit, "needs --condition-phase"):
            harness.validate_checkpoint_args(args)


class CheckpointOrchestrationTests(unittest.TestCase):
    def parse(self, *args: str):
        return harness.build_parser().parse_args(["checkpoint", *args])

    @staticmethod
    def opening() -> dict:
        return {"world": 10, "event": 0x34D, "phase": 0, "scenario": 0x34D, "pc": 0x456, "op": 0xDB,
                "text": "running event 124"}

    def common_patches(self):
        return (mock.patch.object(harness, "on_world_map", return_value=True),
                mock.patch.object(harness, "count_log", return_value=0),
                mock.patch.object(harness, "wait_log", return_value=None),
                mock.patch.object(harness, "formation", return_value=True),
                mock.patch.object(harness, "advance", return_value=self.opening()),
                mock.patch.object(harness, "log_lines", return_value=[]),
                mock.patch.object(harness, "capture_event_messages", return_value=[]))

    def test_victory_uses_defeat_and_condition_wait_without_forcing_phase(self) -> None:
        args = self.parse("34D", "7C", "--victory", "--advance-phase", "2", "4", "--dismiss-end-phase", "2",
                          "--messages", "30", "--after-event")
        patches = self.common_patches()
        with patches[0], patches[1], patches[2], patches[3], patches[4], patches[5], patches[6] as capture, \
                mock.patch.object(harness, "send", return_value="carrier started") as send, \
                mock.patch.object(harness, "defeat", return_value=[3, 4]) as defeat, \
                mock.patch.object(harness, "wait_condition_event", return_value=("condition loaded", 0)) as waited, \
                mock.patch.object(harness, "capture_after_event") as after:
            harness.cmd_checkpoint(args)

        defeat.assert_called_once_with(None)
        waited.assert_called_once_with(0x7C, None, args.timeout, 0, self.opening(), {2, 4}, {2}, args.every, args.prefix)
        capture.assert_called_once()
        after.assert_called_once_with(0x7C, args.timeout, args.every, None)
        self.assertEqual([mock.call("scenario 34D")], send.call_args_list)

    def test_direct_phase_still_forces_phase_and_never_defeats_units(self) -> None:
        args = self.parse("34D", "7C", "5", "--messages", "30")
        patches = self.common_patches()
        with patches[0], patches[1], patches[2], patches[3], patches[4], patches[5], patches[6] as capture, \
                mock.patch.object(harness, "send", side_effect=("carrier started", "phase queued")) as send, \
                mock.patch.object(harness, "defeat") as defeat, \
                mock.patch.object(harness, "wait_condition_event") as waited:
            harness.cmd_checkpoint(args)

        defeat.assert_not_called()
        waited.assert_not_called()
        capture.assert_called_once()
        self.assertEqual([mock.call("scenario 34D"), mock.call("event 7C 5")], send.call_args_list)


class VictoryTransitionTests(unittest.TestCase):
    @staticmethod
    def state(pc: int, op: int = 0xDB, world: int = 10, phase: int = 0, event: int = 0x34D) -> dict:
        return {"world": world, "event": event, "phase": phase, "scenario": event, "pc": pc, "op": op,
                "text": f"event at 0x{pc:X}"}

    def test_same_event_number_is_not_enough_until_opening_end_changes(self) -> None:
        opening = self.state(0x456)
        wrong_phase = self.state(0x500, 0x22, phase=3)
        closing = self.state(0x600, 0x22, phase=5)
        with mock.patch.object(harness, "log_lines", return_value=[]), \
                mock.patch.object(harness, "game_state", side_effect=(opening, wrong_phase, closing)) as state, \
                mock.patch.object(harness, "nap"), mock.patch.object(harness, "send") as send:
            result, capture_seen = harness.wait_condition_event(0x34D, 5, 1, 0, opening)
        self.assertEqual(closing["text"], result)
        self.assertEqual(0, capture_seen)
        self.assertEqual(3, state.call_count)
        send.assert_not_called()

    def test_only_allowlisted_intervening_event_gets_input(self) -> None:
        opening = self.state(0x456)
        other = self.state(0x500, 0x22, phase=3)
        allowed = self.state(0x510, 0xE5, phase=4)
        closing = self.state(0x600, 0x22, event=0x7C)
        with mock.patch.object(harness, "log_lines", return_value=[]), \
                mock.patch.object(harness, "game_state", side_effect=(other, allowed, closing)), \
                mock.patch.object(harness, "nap"), mock.patch.object(harness, "send") as send:
            harness.wait_condition_event(0x7C, None, 1, 0, opening, {4}, set(), 0)
        send.assert_called_once_with("key RETURN")

    def test_allowlisted_end_notice_gets_one_input(self) -> None:
        opening = self.state(0x456)
        notice = self.state(0x510, phase=2)
        closing = self.state(0x600, 0x22, event=0x7C)
        with mock.patch.object(harness, "log_lines", return_value=[]), \
                mock.patch.object(harness, "game_state", side_effect=(notice, notice, closing)), \
                mock.patch.object(harness, "nap"), mock.patch.object(harness, "send") as send, \
                mock.patch.object(harness, "shot") as shot:
            harness.wait_condition_event(0x7C, None, 1, 0, opening, set(), {2}, 0)
        send.assert_called_once_with("key RETURN")
        shot.assert_called_once()

    def test_after_event_never_presses_once_world_frames_move(self) -> None:
        first = self.state(0x500)
        world = self.state(0x500, world=16)
        with mock.patch.object(harness, "game_state", side_effect=(first, world)), \
                mock.patch.object(harness, "send") as send, mock.patch.object(harness, "shot") as shot:
            path = harness.capture_after_event(0x34D, 1, 0.01)
        send.assert_not_called()
        shot.assert_called_once()
        self.assertEqual(harness.SHOTS / "checkpoint-event-34D-after-event.png", path)

    def test_after_event_never_confirms_a_stable_end_instruction(self) -> None:
        ended = self.state(0x600)
        with mock.patch.object(harness, "game_state", side_effect=(ended, ended, ended)), \
                mock.patch.object(harness, "nap"), mock.patch.object(harness, "send") as send, \
                mock.patch.object(harness, "shot"):
            harness.capture_after_event(0x34D, 1, 0.01)
        send.assert_not_called()


class DispatchHarnessTests(unittest.TestCase):
    JOBS = ("Jobs (-), 4 row(s): 4 Job\n"
            "1: Job | Jobs | Mining | key 195\n"
            "2: Job | Jobs | Harvesting | key 196\n"
            "3: Job | Jobs | Guarding | key 197\n"
            "4: Job | Jobs | Hunting | key 198")

    def parse(self, *args: str):
        return harness.build_parser().parse_args(["dispatch", *args])

    def test_parser_selects_one_catalog_and_capture(self) -> None:
        args = self.parse("jobs", "--story", "12", "--month", "6", "--file", "jobs.png")
        self.assertEqual("jobs", args.kind)
        self.assertEqual(12, args.story)
        self.assertEqual(6, args.month)
        self.assertEqual(Path("jobs.png"), args.file)
        self.assertFalse(args.leave_open)

    def test_stable_tavern_opens_kind_without_world_navigation(self) -> None:
        args = self.parse("jobs", "--leave-open")
        with mock.patch.object(harness, "send", side_effect=(
                "queued Dispatch Missions (Job, story 16, month 1)",
                self.JOBS)) as send, \
                mock.patch.object(harness, "count_log", return_value=0), \
                mock.patch.object(harness, "wait_log", return_value="opened"), \
                mock.patch.object(harness, "wait_dispatch_view"), mock.patch.object(harness, "nap"), \
                mock.patch.object(harness, "shot") as shot:
            harness.cmd_dispatch(args)
        self.assertEqual([mock.call("dispatch jobs 16 1"), mock.call("dispatchinfo")], send.call_args_list)
        shot.assert_called_once_with(args)

    def test_eagrose_fixture_enters_tavern_once(self) -> None:
        args = self.parse("contests", "--leave-open")
        replies = iter(("error: dispatch requires the stable Tavern menu", "ok", "ok", "ok", "ok", "queued",
                        "Contests (-), 1 row(s): 1 Contest\n1: Contest | Contests | Test | key 199"))
        with mock.patch.object(harness, "send", side_effect=lambda *_: next(replies)) as send, \
                mock.patch.object(harness, "wait_world_ready",
                                  return_value="world depth 1, top state 0, party location 2, autopilot done true"), \
                mock.patch.object(harness, "wait_world_state", side_effect=("town", "tavern")), \
                mock.patch.object(harness, "count_log", return_value=0), \
                mock.patch.object(harness, "wait_log", return_value="opened"), \
                mock.patch.object(harness, "wait_dispatch_view"), \
                mock.patch.object(harness, "nap"), mock.patch.object(harness, "shot"):
            harness.cmd_dispatch(args)
        self.assertEqual(["key C", "key RETURN", "key S", "key RETURN"],
                         [call.args[0] for call in send.call_args_list[1:5]])
        self.assertEqual("dispatch contests 16 1", send.call_args_list[-2].args[0])

    def test_disposable_save_teleport_updates_all_three_locations_and_crc(self) -> None:
        data = bytearray(harness.SAVE_HEADER + harness.SAVE_SLOTS * harness.SAVE_SLOT)
        struct.pack_into("<I", data, 0, harness.SAVE_HEADER)
        base = harness.SAVE_HEADER
        data[base:base + 2] = b"SC"
        struct.pack_into("<I", data, 4, zlib.crc32(data[harness.SAVE_HEADER:]))
        self.assertEqual(1, harness.patch_save_data_location(data, 2))
        self.assertEqual(2, struct.unpack_from("<I", data, base + harness.SAVE_EVENTS + 0x31 * 4)[0])
        self.assertEqual(2, data[base + harness.SAVE_WORLD_LOCATION_SUMMARY])
        self.assertEqual(2, struct.unpack_from("<I", data, base + harness.SAVE_WORLD_LOCATION)[0])
        self.assertEqual(struct.unpack_from("<I", data, 4)[0], zlib.crc32(data[harness.SAVE_HEADER:]))

    def test_capture_closes_dispatch_by_default(self) -> None:
        args = self.parse("jobs")
        with mock.patch.object(harness, "send", side_effect=(
                "queued", self.JOBS, "ok")) as send, \
                mock.patch.object(harness, "count_log", return_value=0), \
                mock.patch.object(harness, "wait_log", return_value="opened"), \
                mock.patch.object(harness, "wait_dispatch_view") as view, \
                mock.patch.object(harness, "nap"), mock.patch.object(harness, "shot"):
            harness.cmd_dispatch(args)
        self.assertEqual("key ESCAPE", send.call_args_list[-1].args[0])
        self.assertEqual([mock.call(True, 20.0), mock.call(False, 20.0)], view.call_args_list)

    def test_expected_rows_follow_native_hierarchy(self) -> None:
        self.assertEqual({"Menu": 5}, dict(+harness.expected_dispatch_counts(0, "all")))
        self.assertEqual({"Menu": 6}, dict(+harness.expected_dispatch_counts(16, "propositions")))
        self.assertEqual({"Job": 4}, dict(+harness.expected_dispatch_counts(16, "jobs")))

    def test_catalog_validation_rejects_duplicate_keys(self) -> None:
        info = ("Jobs (-), 4 row(s): 4 Job\n1: Job | Jobs | One | key 195\n2: Job | Jobs | Two | key 195\n"
                "3: Job | Jobs | Three | key 197\n4: Job | Jobs | Four | key 198")
        with self.assertRaisesRegex(RuntimeError, "duplicate"):
            harness.validate_dispatch_info(info, "jobs", 3)

    def test_page_waits_for_native_rows_and_input(self) -> None:
        header = "PropositionKinds (-), 6 row(s): 6 Menu\n"
        stale = header + "profit-main: rows 5, focus 0, state 4, gate 0, pending -1"
        animating = header + "profit-main: rows 6, focus 0, state 4, gate 1, pending -1"
        ready = header + "profit-main: rows 6, focus 0, state 4, gate 0, pending -1"
        with mock.patch.object(harness, "send", side_effect=(stale, animating, ready)) as send, \
                mock.patch.object(harness, "nap"):
            self.assertEqual(ready, harness.wait_dispatch_page("PropositionKinds (-)", 1))
        self.assertEqual(3, send.call_count)

    def test_navigation_stops_at_a_failed_transition(self) -> None:
        with mock.patch.object(harness, "wait_dispatch_page", side_effect=("main", RuntimeError("stuck"))), \
                mock.patch.object(harness, "send") as send:
            with self.assertRaisesRegex(RuntimeError, "cycle 1/10 failed: stuck"):
                harness.check_dispatch_navigation(10, 16, 1)
        send.assert_called_once_with("key RETURN", timeout=1)


if __name__ == "__main__":
    unittest.main()
