#!/usr/bin/env python3
"""Build both configurations; fail if Unity or build evidence is incomplete."""
import os, pathlib, subprocess, sys, datetime, argparse, json, hashlib, shutil
from unity_process import run
from private_audio import private_audio
from isolated_display import isolated_display
from contextlib import nullcontext
from native_build import build_native
root = pathlib.Path(__file__).resolve().parents[1]
build_native(root)
editor = os.environ.get("UNITY_EDITOR", "/home/onnwee/Videos/6000.4.10f1/Editor/Unity")
sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
def source_status(): return subprocess.check_output(["git", "status", "--porcelain"], cwd=root, text=True)
initial_status = source_status()
if initial_status: sha += "-working"
env = dict(os.environ, CLUSTR_SOURCE_SHA=sha)
out = root / "Builds" / "build-logs" / datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
out.mkdir(parents=True)
parser = argparse.ArgumentParser()
parser.add_argument("--configuration", choices=["all", "development", "release"], default="all")
args = parser.parse_args()
methods = ["BuildLinuxBeta", "BuildLinuxRelease"] if args.configuration == "all" else ["BuildLinuxBeta" if args.configuration == "development" else "BuildLinuxRelease"]
for method in methods:
    log = out / (method + ".log")
    artifact = root / "Builds" / ("Linux" if method == "BuildLinuxBeta" else "LinuxRelease")
    # Hash only files this build produced: never inherit a previous build's leftovers.
    if artifact.exists(): shutil.rmtree(artifact)
    settings = root / "ProjectSettings/ProjectSettings.asset"
    settings_before = settings.read_bytes()
    try:
        # Texture import workers still open X even when the main Editor uses -nographics.
        # On unattended hosts, own a private display instead of using the user's desktop.
        with (isolated_display(out / (method + "-display")) if not os.environ.get("DISPLAY") else nullcontext({})) as display_env, private_audio(out / (method + "-audio")) as audio_env:
            for key in ("DISPLAY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "DRI_PRIME", "LD_LIBRARY_PATH"):
                if key in display_env: audio_env[key] = display_env[key]
            process = run([editor, "-batchmode", "-nographics", "-noaudio", "-quit", "-projectPath", str(root),
                "-executeMethod", "ClustrGalaxy.Editor.GalaxyLinuxBetaBuilder." + method, "-logFile", str(log)],
                log, env=dict(audio_env, CLUSTR_SOURCE_SHA=sha))
    finally:
        settings.write_bytes(settings_before)
    timed_out = process["timed_out"]
    build_succeeded = log.exists() and "[ClustrGalaxy.Build] Linux beta succeeded" in log.read_text(errors="replace")
    # The builder saves assets; a build that changes tracked files no longer matches its recorded source.
    source_changed = source_status() != initial_status
    hashes = {}
    if build_succeeded and artifact.exists():
        for file in sorted(artifact.rglob("*")):
            if file.is_file():
                with file.open("rb") as stream: hashes[str(file.relative_to(artifact))] = hashlib.file_digest(stream, "sha256").hexdigest()
    passed = build_succeeded and process["process_exit"] == 0 and not timed_out and not source_changed
    manifest = {"source_sha": sha, "method": method, "artifact": str(artifact), "log": str(log),
        "process_exit": process["process_exit"], "shutdown_timeout": timed_out, "build_completed": build_succeeded,
        "source_changed_by_build": source_changed, "build_gate_passed": passed, "qualified": False, "sha256": hashes}
    (out / (method + ".json")).write_text(json.dumps(manifest, indent=2) + "\n")
    if not passed:
        print("Build gate failed; artifact is unqualified: " + str(log), flush=True); sys.exit(2 if timed_out else 1)
    print("Built " + method + ": " + str(log), flush=True)
