"""Bounded Unity process execution with diagnostic evidence and real exit status."""
import os
import pathlib
import signal
import subprocess
import time
import json
import threading


def _terminate(process):
    if process.poll() is not None: return
    os.killpg(process.pid, signal.SIGTERM)
    try: process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGKILL)
        process.wait()


def run(command, log, *, env=None, timeout=900, shutdown_timeout=45, capture_stdout=False, readiness_marker=None):
    # Convert harness termination into a normal unwind so owned display/audio
    # contexts clean up and a detached Unity player cannot survive its runner.
    previous = None
    if threading.current_thread() is threading.main_thread():
        previous = signal.getsignal(signal.SIGTERM)
        def interrupted(signum, _frame):
            raise SystemExit(128 + signum)
        signal.signal(signal.SIGTERM, interrupted)
    output_stream = None
    try:
        if capture_stdout: output_stream = pathlib.Path(log).open("a")
        return _run(command, log, env=env, timeout=timeout, shutdown_timeout=shutdown_timeout, output_stream=output_stream, readiness_marker=readiness_marker)
    finally:
        if output_stream is not None: output_stream.close()
        if previous is not None: signal.signal(signal.SIGTERM, previous)


def _run(command, log, *, env=None, timeout=900, shutdown_timeout=45, output_stream=None, readiness_marker=None):
    log = pathlib.Path(log)
    started = time.monotonic()
    process = subprocess.Popen(command, env=env, stdout=output_stream if output_stream is not None else subprocess.DEVNULL,
                               stderr=subprocess.STDOUT, start_new_session=True)
    readiness_seconds = None
    shutdown_started = None
    timed_out = False
    diagnostic = str(log.with_suffix(".processes.txt"))
    try:
        while True:
            content = log.read_text(errors="replace") if log.exists() else ""
            if readiness_marker is not None and readiness_seconds is None and readiness_marker in content.splitlines():
                readiness_seconds = time.monotonic() - started
            if process.poll() is not None: break
            if "Test run completed. Exiting with code" in content or "Batchmode quit successfully invoked" in content:
                if shutdown_started is None:
                    shutdown_started = time.monotonic()
            now = time.monotonic()
            if now - started > timeout or shutdown_started is not None and now - shutdown_started > shutdown_timeout:
                timed_out = True
                # Only inspect the process session this invocation created.
                snapshot = subprocess.run(["ps", "-s", str(process.pid), "-L", "-o",
                                           "pid,ppid,tid,stat,wchan:32,comm"], capture_output=True, text=True)
                pathlib.Path(diagnostic).write_text(snapshot.stdout + snapshot.stderr)
                os.killpg(process.pid, signal.SIGTERM)
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait()
                break
            time.sleep(.01 if readiness_marker is not None and readiness_seconds is None else .25)
    except BaseException as error:
        _terminate(process)
        log.with_suffix(".interrupted.json").write_text(json.dumps({
            "process_exit": process.returncode, "interrupted": True,
            "reason": type(error).__name__, "elapsed_seconds": round(time.monotonic()-started, 3)
        }, indent=2)+"\n")
        raise
    return {"command": command, "process_exit": process.returncode, "timed_out": timed_out,
            "elapsed_seconds": round(time.monotonic() - started, 3),
            "log": str(log), "timeout_diagnostics": diagnostic if timed_out else None,
            "readiness_seconds": readiness_seconds}


def player_errors(content):
    """Project/runtime diagnostics that cannot be accepted by a successful process exit."""
    markers = ("Exception:", "Script error (", "error CS", "Assertion failed", "[ClustrGalaxy.DemoJourney] FAIL",
               "[ClustrGalaxy.LiveSmoke] FAILED", "[ClustrGalaxy.UiSmoke] FAIL")
    return [line for line in content.splitlines() if any(marker in line for marker in markers)]
