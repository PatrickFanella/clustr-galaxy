import pathlib
import tempfile
import unittest
import json
import os
from validate import inspect_result, overall, force_project_compilation, compilation_evidence


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


class SummaryContract(unittest.TestCase):
    passing = {"suite": "EditMode", "passed": True}

    def test_partial_run_is_incomplete_and_not_passed(self):
        result = overall([self.passing], ["EditMode", "PlayMode"], {"compiled": True})
        self.assertEqual(result, {"complete": False, "passed": False})

    def test_complete_run_requires_compilation_evidence(self):
        suites = [self.passing, {"suite": "PlayMode", "passed": True}]
        self.assertTrue(overall(suites, ["EditMode", "PlayMode"], {"compiled": True})["passed"])
        self.assertFalse(overall(suites, ["EditMode", "PlayMode"], {"compiled": False})["passed"])
        self.assertFalse(overall(suites, ["EditMode", "PlayMode"], {})["passed"])


class CompilationContract(unittest.TestCase):
    def project(self, folder):
        root = pathlib.Path(folder)
        (root / "Assets/ClustrGalaxy/Runtime").mkdir(parents=True)
        (root / "Assets/ClustrGalaxy/Runtime/Game.asmdef").write_text(json.dumps({"name": "ClustrGalaxy.Game"}))
        scripts, bee = root / "Library/ScriptAssemblies", root / "Library/Bee/artifacts/1.dag"
        for folder in (scripts, bee):
            folder.mkdir(parents=True)
            for name in ("ClustrGalaxy.Game.dll", "ClustrGalaxy.Game.pdb", "Unity.Package.dll"):
                (folder / name).write_text("x")
        return root, scripts

    def test_only_project_outputs_are_removed(self):
        with tempfile.TemporaryDirectory() as folder:
            root, scripts = self.project(folder)
            removed = force_project_compilation(root)
            self.assertEqual(len(removed), 4)
            self.assertTrue((scripts / "Unity.Package.dll").exists())
            self.assertFalse((scripts / "ClustrGalaxy.Game.dll").exists())

    def test_missing_or_stale_assemblies_are_not_compilation(self):
        with tempfile.TemporaryDirectory() as folder:
            root, scripts = self.project(folder)
            self.assertFalse(compilation_evidence(root, since=os.path.getmtime(scripts / "ClustrGalaxy.Game.dll") + 10)["compiled"])
            self.assertTrue(compilation_evidence(root, since=0)["compiled"])
            (scripts / "ClustrGalaxy.Game.dll").unlink()
            self.assertEqual(compilation_evidence(root, since=0)["missing"], ["ClustrGalaxy.Game"])


if __name__ == "__main__": unittest.main()
