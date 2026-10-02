import json
import os
import pathlib
import signal
import subprocess
import sys
import tempfile
import time
import unittest


class ProcessCleanupTests(unittest.TestCase):
    def test_terminating_harness_reaps_its_owned_player(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            pid_file, log = root / "player.pid", root / "player.log"
            player = [sys.executable, "-c", "import os,pathlib,time;pathlib.Path(" +
                      repr(str(pid_file)) + ").write_text(str(os.getpid()));time.sleep(30)"]
            code = "from unity_process import run;run(" + repr(player) + "," + repr(str(log)) + ")"
            env = dict(os.environ, PYTHONPATH=str(pathlib.Path(__file__).resolve().parent))
            parent = subprocess.Popen([sys.executable, "-c", code], env=env,
                                      stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            try:
                deadline = time.monotonic() + 5
                while not pid_file.exists() and time.monotonic() < deadline: time.sleep(.02)
                self.assertTrue(pid_file.exists(), "Owned test player did not start")
                player_pid = int(pid_file.read_text())
                parent.send_signal(signal.SIGTERM)
                self.assertEqual(parent.wait(timeout=8), 143)
                with self.assertRaises(ProcessLookupError): os.kill(player_pid, 0)
                self.assertTrue(json.loads(log.with_suffix(".interrupted.json").read_text())["interrupted"])
            finally:
                if parent.poll() is None: parent.kill(); parent.wait()

    def test_normal_exit_reaps_helpers_left_in_the_session(self):
        from unity_process import run
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            pid_file, log = root / "helper.pid", root / "player.log"
            helper = ("import signal,time;signal.signal(signal.SIGTERM,signal.SIG_IGN);time.sleep(30)")
            leader = ("import subprocess,sys,pathlib;p=subprocess.Popen([sys.executable,'-c'," + repr(helper) +
                      "]);pathlib.Path(" + repr(str(pid_file)) + ").write_text(str(p.pid))")
            result = run([sys.executable, "-c", leader], log, timeout=10)
            self.assertEqual(result["process_exit"], 0)
            helper_pid = int(pid_file.read_text())
            deadline = time.monotonic() + 2
            while time.monotonic() < deadline:
                try: os.kill(helper_pid, 0)
                except ProcessLookupError: break
                try: os.waitpid(helper_pid, os.WNOHANG)
                except ChildProcessError: pass
                time.sleep(.05)
            with self.assertRaises(ProcessLookupError): os.kill(helper_pid, 0)

