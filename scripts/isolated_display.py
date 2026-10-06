"""Owned headless Weston session with GPU rendering and a private Xwayland display."""
import contextlib
import json
import os
import pathlib
import re
import shutil
import signal
import subprocess
import tempfile
import time


@contextlib.contextmanager
def isolated_display(evidence, width=2560, height=1440):
    evidence = pathlib.Path(evidence); evidence.mkdir(parents=True, exist_ok=True)
    local_root = pathlib.Path(__file__).resolve().parents[1] / "Builds/tools/weston"
    default_root = local_root if (local_root / "usr/bin/weston").exists() else pathlib.Path("/tmp/clustr-weston")
    root = pathlib.Path(os.environ.get("WESTON_ROOT", str(default_root)))
    executable = str(root / "usr/bin/weston") if (root / "usr/bin/weston").exists() else shutil.which("weston")
    if not executable: raise RuntimeError("Weston must be installed or supplied through WESTON_ROOT")
    runtime = pathlib.Path(tempfile.mkdtemp(prefix="clustr-gpu-"))
    env = dict(os.environ, XDG_RUNTIME_DIR=str(runtime), DRI_PRIME=os.environ.get("CLUSTR_DRI_PRIME", "pci-0000_03_00_0"))
    if (root / "usr/bin/weston").exists():
        modules = [file for folder in (root / "usr/lib").glob("*") if folder.is_dir() for file in folder.glob("*.so")]
        env.update(LD_LIBRARY_PATH=str(root / "usr/lib") + ":" + str(root / "usr/lib/weston"),
                   WESTON_MODULE_MAP=";".join(f"{file.name}={file}" for file in modules),
                   WESTON_DATA_DIR=str(root / "usr/share/weston"))
    log = (evidence / "weston.log").resolve()
    command = [executable, "--backend=headless", "--renderer=gl", "--shell=kiosk", "--xwayland", "--fake-seat",
               "--width=" + str(width), "--height=" + str(height), "--refresh-rate=120000", "--no-config",
               "--socket=clustr-galaxy", "--log=" + str(log)]
    process = subprocess.Popen(command, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
    try:
        deadline = time.monotonic() + 15
        while True:
            text = log.read_text(errors="replace") if log.exists() else ""
            display = re.search(r"xserver listening on display (:\d+)", text)
            if display: break
            if process.poll() is not None or time.monotonic() > deadline: raise RuntimeError("Isolated compositor failed: " + str(log))
            time.sleep(.1)
        renderer = re.search(r"GL renderer: (.+)", text)
        if renderer is None or any(name in renderer.group(1).lower() for name in ("llvmpipe", "softpipe", "swiftshader")):
            raise RuntimeError("Hardware compositor rendering unavailable")
        env.update(DISPLAY=display.group(1), WAYLAND_DISPLAY="clustr-galaxy")
        (evidence / "display.json").write_text(json.dumps({"renderer": renderer.group(1), "display": env["DISPLAY"],
                                                         "width": width, "height": height, "private": True}, indent=2))
        yield env
    finally:
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGTERM)
            try: process.wait(timeout=5)
            except subprocess.TimeoutExpired: os.killpg(process.pid, signal.SIGKILL); process.wait()
        shutil.rmtree(runtime)
