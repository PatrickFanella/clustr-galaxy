import pathlib
import sys
import tempfile
import unittest
from unity_process import run


class ProcessEvidenceTests(unittest.TestCase):
    def test_child_stdout_and_stderr_are_retained_with_real_exit_status(self):
        with tempfile.TemporaryDirectory() as directory:
            log = pathlib.Path(directory) / "child.log"
            program = "import sys\nprint('stdout retained')\nsys.stderr.write('stderr retained\\n')\nsys.exit(7)"
            result = run([sys.executable, "-c", program], log, timeout=10, capture_stdout=True)
            self.assertEqual(result["process_exit"], 7)
            self.assertFalse(result["timed_out"])
            self.assertIn("stdout retained", log.read_text())
            self.assertIn("stderr retained", log.read_text())
