import unittest
from full_pass import evidence_passed


class FullPassEvidenceTests(unittest.TestCase):
    def test_missing_failed_or_extra_reports_cannot_pass(self):
        process = {"process_exit": 0, "timed_out": False}
        for reports in ([], [{}], [{"passed": False}], [{"passed": True}, {"passed": True}]):
            self.assertFalse(evidence_passed(process, reports))
        self.assertTrue(evidence_passed(process, [{"passed": True}]))
        self.assertTrue(evidence_passed(process, [{"build_gate_passed": True}] * 2, expected=2))

    def test_success_report_cannot_hide_process_failure(self):
        for process in ({"process_exit": 2}, {"process_exit": 0, "timed_out": True}, {}):
            self.assertFalse(evidence_passed(process, [{"passed": True}]))
