import pathlib
import tempfile
import unittest
from validate import inspect_result


class ValidationContract(unittest.TestCase):
    def inspect(self, xml, log="", exit_code=0, timeout=False):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            results, output = root / "tests.xml", root / "unity.log"
            if xml is not None: results.write_text(xml)
            output.write_text(log)
            return inspect_result(results, output, {"process_exit": exit_code, "timed_out": timeout})

    good = '<test-run result="Passed" total="2" passed="2" failed="0"/>'

    def test_success_requires_process_and_test_evidence(self):
        self.assertTrue(self.inspect(self.good)["passed"])
        self.assertFalse(self.inspect(self.good, exit_code=1)["passed"])
        self.assertFalse(self.inspect(self.good, timeout=True)["passed"])

    def test_missing_malformed_empty_and_failed_results_are_failures(self):
        for xml in (None, "broken", '<test-run result="Passed" total="0"/>',
                    '<test-run result="Failed" total="2" failed="1"/>'):
            self.assertFalse(self.inspect(xml)["passed"])

    def test_project_warnings_and_any_compile_error_fail(self):
        for line in ('Assets/ClustrGalaxy/Test.cs(1): warning CS1234: warning',
                     'Library/PackageCache/Test.cs(1): error CS1234: broken'):
            self.assertFalse(self.inspect(self.good, line)["passed"])
        result = self.inspect(self.good, 'Library/PackageCache/Test.cs(1): warning CS1234: warning')
        self.assertTrue(result["passed"])
        self.assertEqual(len(result["compiler_diagnostics"]), 1)


if __name__ == "__main__": unittest.main()
