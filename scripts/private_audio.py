"""Private null-sink audio for Linux Unity jobs; never alters desktop audio."""
import contextlib
import json
import os
import pathlib
import shutil
import signal
import subprocess
import tempfile
import time


@contextlib.contextmanager
def private_audio(evidence):
    root = pathlib.Path(tempfile.mkdtemp(prefix="clustr-audio-"))
    env = dict(os.environ, PIPEWIRE_RUNTIME_DIR=str(root), PULSE_RUNTIME_PATH=str(root / "pulse"),
               PULSE_SERVER="unix:" + str(root / "pulse/native"), PULSE_SINK="clustr-validation")
    daemon_env = dict(env, XDG_STATE_HOME=str(root / "state"))
    processes, files = [], []
    evidence = pathlib.Path(evidence)
    evidence.mkdir(parents=True, exist_ok=True)
    try:
        if shutil.which("pulseaudio"):
            (root / "pulse").mkdir()
            commands = [("pulseaudio", ["pulseaudio", "-n", "--daemonize=no", "--exit-idle-time=-1",
                        "-L", "module-native-protocol-unix socket=" + str(root / "pulse/native") + " auth-anonymous=1",
                        "-L", "module-null-sink sink_name=clustr-validation"])]
        else:
            for binary in ("pipewire", "pipewire-pulse", "wireplumber", "pactl"):
                if not shutil.which(binary): raise RuntimeError("Private audio dependency unavailable: " + binary)
            config = root / "wireplumber.conf"
            config.write_text(pathlib.Path("/usr/share/wireplumber/wireplumber.conf").read_text() +
                "\nwireplumber.profiles = { clustr-test = { inherits = [ base ] metadata.sm-settings = required "
                "metadata.sm-objects = required policy.standard = required } }\n")
            commands = [("pipewire", ["pipewire"]), ("pulse", ["pipewire-pulse"]),
                        ("policy", ["wireplumber", "-c", str(config), "-p", "clustr-test"])]
        for name, command in commands:
            log = (evidence / (name + ".log")).open("w"); files.append(log)
            processes.append(subprocess.Popen(command, env=daemon_env, stdout=log, stderr=log, start_new_session=True))
            time.sleep(.4)
        deadline = time.monotonic() + 10
        while not (root / "pulse/native").exists():
            if any(process.poll() is not None for process in processes) or time.monotonic() > deadline:
                raise RuntimeError("Private audio startup failed; inspect " + str(evidence))
            time.sleep(.1)
        if not shutil.which("pulseaudio"):
            subprocess.run(["pactl", "load-module", "module-null-sink", "sink_name=clustr-validation"],
                           env=env, check=True, stdout=subprocess.DEVNULL, timeout=10)
        (evidence / "audio.json").write_text(json.dumps({"backend": commands[0][0], "sink": "clustr-validation",
                                                        "hardware_monitors": False}, indent=2))
        yield env
    finally:
        for process in reversed(processes):
            if process.poll() is not None: continue
            os.killpg(process.pid, signal.SIGTERM)
            try: process.wait(timeout=3)
            except subprocess.TimeoutExpired: os.killpg(process.pid, signal.SIGKILL); process.wait()
        for file in files: file.close()
        shutil.rmtree(root)
