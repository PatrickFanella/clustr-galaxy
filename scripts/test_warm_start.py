import math
import pathlib
import sys
import tempfile
import unittest
from unity_process import run
from warm_start_smoke import evaluate


class WarmStartTests(unittest.TestCase):
    def setUp(self):
        self.process = dict(process_exit=0, timed_out=False, readiness_seconds=2.9)
        self.ready = dict(passed=True, offline=True, chart_visible=True, source_sha="abc", build_id="build",
                          width=2560, height=1440, resident_nodes=300, revision_id=243, catalog_id=14)

    def verdict(self, size=(2560, 1440), requests=0, unchanged=True):
        return evaluate(self.process, self.ready, {"source_sha": "abc"}, size, requests, unchanged)

    def test_complete_observed_start(self): self.assertEqual(self.verdict(), [])

    def test_missing_invalid_and_slow_measurements_fail(self):
        for seconds in (None, True, 0, -1, math.nan, math.inf, 3.001):
            with self.subTest(seconds=seconds):
                self.process["readiness_seconds"] = seconds; self.assertTrue(self.verdict())

    def test_failed_process_missing_chart_and_wrong_identity_fail(self):
        self.process["process_exit"] = 2; self.assertTrue(self.verdict()); self.process["process_exit"] = 0
        for key, value in (("passed", False), ("offline", False), ("chart_visible", False), ("source_sha", "other"), ("build_id", ""), ("resident_nodes", 0)):
            with self.subTest(key=key):
                old = self.ready[key]; self.ready[key] = value; self.assertTrue(self.verdict()); self.ready[key] = old

    def test_capture_network_and_source_mutation_fail(self):
        self.assertTrue(self.verdict(size=None)); self.assertTrue(self.verdict(size=(1280, 720)))
        self.assertTrue(self.verdict(requests=1)); self.assertTrue(self.verdict(unchanged=False))

    def test_process_observer_requires_exact_marker_and_handles_fast_exit(self):
        with tempfile.TemporaryDirectory() as directory:
            for content, available in (("prefix READY", False), ("READY", True)):
                log = pathlib.Path(directory) / ("ready" if available else "not-ready")
                result = run([sys.executable, "-c", "print(" + repr(content) + ")"], log,
                             timeout=10, capture_stdout=True, readiness_marker="READY")
                self.assertEqual(result["process_exit"], 0)
                self.assertEqual(result["readiness_seconds"] is not None, available)
