#!/usr/bin/env python3
"""Build both configurations; fail if Unity or build evidence is incomplete."""
import os, pathlib, subprocess, sys, datetime, argparse, json, hashlib
from unity_process import run
from private_audio import private_audio
from native_build import build_native
root = pathlib.Path(__file__).resolve().parents[1]
build_native(root)
editor = os.environ.get("UNITY_EDITOR", "/home/onnwee/Videos/6000.4.10f1/Editor/Unity")
sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
if subprocess.check_output(["git", "status", "--porcelain"], cwd=root): sha += "-working"
env = dict(os.environ, CLUSTR_SOURCE_SHA=sha)
out = root / "Builds" / "build-logs" / datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
out.mkdir(parents=True)
parser = argparse.ArgumentParser()
parser.add_argument("--configuration", choices=["all", "development", "release"], default="all")
args = parser.parse_args()
methods = ["BuildLinuxBeta", "BuildLinuxRelease"] if args.configuration == "all" else ["BuildLinuxBeta" if args.configuration == "development" else "BuildLinuxRelease"]
for method in methods:
    log = out / (method + ".log")
    settings = root / "ProjectSettings/ProjectSettings.asset"
    settings_before = settings.read_bytes()
    try:
        with private_audio(out / (method + "-audio")) as audio_env:
            process = run([editor, "-batchmode", "-nographics", "-noaudio", "-quit", "-projectPath", str(root),
                "-executeMethod", "ClustrGalaxy.Editor.GalaxyLinuxBetaBuilder." + method, "-logFile", str(log)],
                log, env=dict(audio_env, CLUSTR_SOURCE_SHA=sha))
    finally:
        settings.write_bytes(settings_before)
    timed_out = process["timed_out"]
    build_succeeded = log.exists() and "[ClustrGalaxy.Build] Linux beta succeeded" in log.read_text(errors="replace")
    artifact = root / "Builds" / ("Linux" if method == "BuildLinuxBeta" else "LinuxRelease")
    hashes = {}
    if build_succeeded and artifact.exists():
        for file in sorted(artifact.rglob("*")):
            if file.is_file():
                with file.open("rb") as stream: hashes[str(file.relative_to(artifact))] = hashlib.file_digest(stream, "sha256").hexdigest()
    passed = build_succeeded and process["process_exit"] == 0 and not timed_out
    manifest = {"source_sha": sha, "method": method, "artifact": str(artifact), "log": str(log),
        "process_exit": process["process_exit"], "shutdown_timeout": timed_out, "build_completed": build_succeeded,
        "build_gate_passed": passed, "qualified": False, "sha256": hashes}
    (out / (method + ".json")).write_text(json.dumps(manifest, indent=2) + "\n")
    if not passed:
        print("Build gate failed; artifact is unqualified: " + str(log), flush=True); sys.exit(2 if timed_out else 1)
    print("Built " + method + ": " + str(log), flush=True)
