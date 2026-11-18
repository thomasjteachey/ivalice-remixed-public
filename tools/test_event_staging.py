from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))

from audit_event_staging import audit  # noqa: E402


class EventStagingAuditTests(unittest.TestCase):
    def test_hidden_actor_can_be_staged_before_draw(self) -> None:
        # Erase hides the drawable, but Warp/UnitAnim still operate on its logical actor before Draw.
        code = bytes.fromhex("46 84 00 5f 84 00 03 11 00 00 11 84 00 00 00 00 00 44 84 00")
        self.assertEqual([], audit(code))

    def test_reports_removed_actor_use(self) -> None:
        code = bytes.fromhex("3d 84 00 11 84 00 00 00 00 00")
        findings = audit(code)
        self.assertEqual(["UnitAnim uses actor 0x84 after it was removed"],
                         [f.message for f in findings])

    def test_blue_remove_is_conditional(self) -> None:
        # BlueRemoveUnit removes only one team; the same selector can validly name a surviving other-team actor.
        self.assertEqual([], audit(bytes.fromhex("99 ff 00 11 ff 00 00 00 00 00")))

    def test_group_selector_is_not_actor_low_byte(self) -> None:
        self.assertEqual([], audit(bytes.fromhex("3d 01 00 11 01 01 00 00 00 00")))

    def test_flow_boundary_avoids_false_path_join(self) -> None:
        # A label may be entered without taking the preceding RemoveUnit.
        code = bytes.fromhex("3d 84 00 0e 01 11 84 00 00 00 00 00")
        self.assertEqual([], audit(code))

    def test_add_unit_group_must_close_before_wait(self) -> None:
        findings = audit(bytes.fromhex("49 45 84 00 01 4b"))
        self.assertEqual(["WaitAddUnitEnd before AddUnitEnd", "AddUnitStart has no AddUnitEnd"],
                         [f.message for f in findings])

    def test_balanced_group_is_clean(self) -> None:
        self.assertEqual([], audit(bytes.fromhex("49 45 84 00 01 4a 4b")))

    def test_hidden_add_must_be_drawn_before_event_exit(self) -> None:
        findings = audit(bytes.fromhex("49 45 84 00 01 4a 4b 0d ff"))
        self.assertEqual(["actor 0x84 reaches event exit still staged hidden by AddUnit"],
                         [f.message for f in findings])

    def test_draw_resolves_hidden_add_before_event_exit(self) -> None:
        self.assertEqual([], audit(bytes.fromhex("49 45 84 00 01 4a 4b 44 84 00 0d ff")))

    def test_immediate_visible_add_is_clean_at_event_exit(self) -> None:
        self.assertEqual([], audit(bytes.fromhex("49 45 84 00 00 4a 4b 0d ff")))

    def test_truncated_instruction_is_an_error(self) -> None:
        findings = audit(bytes.fromhex("11 84"))
        self.assertEqual("error", findings[0].severity)
        self.assertIn("truncated", findings[0].message)


if __name__ == "__main__":
    unittest.main()

