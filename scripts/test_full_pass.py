import unittest
from full_pass import evidence_passed, host_identity, is_arch_family


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


class HostIdentityTests(unittest.TestCase):
    def test_arch_and_explicit_arch_derivative(self):
        for text in ('ID=arch', 'ID=omarchy\nID_LIKE="arch"', "ID=derivative\nID_LIKE='arch linux'"):
            self.assertTrue(is_arch_family(*host_identity(text)))
        self.assertEqual(host_identity('ID=omarchy\nID_LIKE=arch'), ('omarchy', ['arch']))

    def test_unrelated_missing_and_substring_do_not_qualify(self):
        for text in ('', 'ID=ubuntu\nID_LIKE=debian', 'ID=archlinux', 'ID=other\nID_LIKE=archipelago'):
            self.assertFalse(is_arch_family(*host_identity(text)))
