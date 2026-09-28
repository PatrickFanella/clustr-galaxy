import json
import pathlib
import tempfile
import unittest
from hierarchy_smoke import evaluate_profile, load_profile


class ProfileBinding(unittest.TestCase):
    artifact = {"source_sha": "abc123"}

    def report(self, **values):
        return dict({"source_sha": "abc123", "duration_seconds": 30, "preflight_passed": True, "qualified": False}, **values)

    def test_preflight_requires_matching_source_duration_and_verdict(self):
        self.assertEqual(evaluate_profile(self.report(), self.artifact, 30), [])
        self.assertTrue(evaluate_profile(None, self.artifact, 30))
        self.assertTrue(evaluate_profile(self.report(source_sha="other"), self.artifact, 30))
        self.assertTrue(evaluate_profile(self.report(), {"source_sha": None}, 30))
        self.assertTrue(evaluate_profile(self.report(duration_seconds=600), self.artifact, 30))
        self.assertTrue(evaluate_profile(self.report(preflight_passed=False), self.artifact, 30))

    def test_full_profile_requires_qualification_not_preflight(self):
        self.assertTrue(evaluate_profile(self.report(duration_seconds=600), self.artifact, 600))
        self.assertEqual(evaluate_profile(self.report(duration_seconds=600, qualified=True), self.artifact, 600), [])

    def test_report_is_found_through_the_single_pointer(self):
        with tempfile.TemporaryDirectory() as folder:
            config = pathlib.Path(folder) / "config"
            reports = config / "unity3d/Subcult/Clustr Galaxy/ClustrGalaxy/reports"
            reports.mkdir(parents=True)
            self.assertEqual(load_profile(config), (None, None))
            target = reports / "run.json"
            target.write_text(json.dumps(self.report()))
            (reports / "latest-report-path.txt").write_text(str(target))
            self.assertEqual(load_profile(config)[1]["source_sha"], "abc123")


if __name__ == "__main__": unittest.main()
