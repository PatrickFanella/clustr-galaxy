#!/usr/bin/env python3
"""Measure a separate offline process through rendered overview capture, including startup.

The monotonic timer starts before Popen and ends when the post-capture READY line
is observed. Log polling and screenshot writing make this a conservative bound.
Compositor/audio setup and copying an existing cache occur before the measurement.
"""
import argparse
import datetime
import hashlib
import json
import math
import pathlib
import shutil
import struct
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from artifact_identity import identify
from isolated_display import isolated_display
from private_audio import private_audio
from unity_process import run, player_errors


def evaluate(process, ready, identity, image_size, network_requests, source_cache_unchanged):
    failures = []
    seconds = process.get("readiness_seconds")
    if process.get("process_exit") != 0 or process.get("timed_out"): failures.append("Player failed or timed out")
    if isinstance(seconds, bool) or not isinstance(seconds, (float, int)) or not math.isfinite(seconds) or seconds <= 0 or seconds > 3:
        failures.append("Rendered cached overview unavailable within three seconds of process launch")
    if ready.get("passed") is not True or ready.get("offline") is not True or ready.get("chart_visible") is not True:
        failures.append("Verified offline overview did not reach the chart")
    if not identity.get("source_sha") or ready.get("source_sha") != identity["source_sha"] or not ready.get("build_id"):
        failures.append("Artifact/source identity missing or mismatched")
    if (ready.get("width"), ready.get("height")) != (2560, 1440) or image_size != (2560, 1440):
        failures.append("Required 1440p rendered evidence missing")
    if ready.get("resident_nodes", 0) <= 0 or ready.get("revision_id", 0) <= 0 or ready.get("catalog_id", 0) <= 0:
        failures.append("Snapshot identity or rendered population unavailable")
    if network_requests not in (None, 0): failures.append("Offline fixture sent a network request")
    if not source_cache_unchanged: failures.append("Source cache changed")
    return failures


def cache_hashes(root):
    result = {}
    for path in sorted(root.rglob("*")):
        if path.is_file():
            with path.open("rb") as stream: result[str(path.relative_to(root))] = hashlib.file_digest(stream, "sha256").hexdigest()
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", default="Builds/LinuxRelease/ClustrGalaxy.x86_64")
    parser.add_argument("--cache-from", type=pathlib.Path, required=True, help="Existing v2 cache directory; copied, never written")
    parser.add_argument("--fixture", action="store_true")
    args = parser.parse_args()
    if not args.cache_from.is_dir(): parser.error("--cache-from must be an existing v2 cache directory")
    player = pathlib.Path(args.player).resolve()
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    evidence = (pathlib.Path("Builds/evidence") / (stamp + "-warm-start")).resolve(); evidence.mkdir(parents=True)
    identity = identify(player)
    config = evidence / "config"
    storage = config / "unity3d/Subcult/Clustr Galaxy/ClustrGalaxy"
    source_hashes = cache_hashes(args.cache_from)
    shutil.copytree(args.cache_from, storage / ("fixture-cache/v2" if args.fixture else "cache/v2"))
    log = evidence / "player.log"
    command = [str(player), "--clustr-offline", "--clustr-warm-start-smoke", "-force-vulkan", "-force-wayland",
               "-force-gfx-direct", "-screen-width", "2560", "-screen-height", "1440", "-screen-fullscreen", "0", "-logFile", str(log)]
    class Sentinel(BaseHTTPRequestHandler):
        calls = 0
        def do_GET(self):
            type(self).calls += 1; self.send_error(503)
        def log_message(self, *_): pass
    server = None
    try:
        if args.fixture:
            server = ThreadingHTTPServer(("127.0.0.1", 0), Sentinel)
            threading.Thread(target=server.serve_forever, daemon=True).start()
            command.append("--clustr-fixture-url=http://127.0.0.1:" + str(server.server_port) + "/api")
        with isolated_display(evidence / "display") as display, private_audio(evidence / "audio") as audio:
            env = dict(audio)
            for name in ("DISPLAY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "DRI_PRIME", "LD_LIBRARY_PATH"):
                if name in display: env[name] = display[name]
            env["XDG_CONFIG_HOME"] = str(config)
            process = run(command, log, env=env, timeout=25, readiness_marker="[ClustrGalaxy.WarmStart] READY")
        ready_path = storage / "warm-start/summary.json"
        try: ready = json.loads(ready_path.read_text())
        except (OSError, ValueError): ready = {}
        image_size = None
        try:
            with (storage / "warm-start/overview.png").open("rb") as stream: header = stream.read(24)
            if header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR": image_size = struct.unpack(">II", header[16:24])
        except (OSError, struct.error): pass
        network_requests = Sentinel.calls if args.fixture else None
        unchanged = source_hashes == cache_hashes(args.cache_from)
        failures = evaluate(process, ready, identity, image_size, network_requests, unchanged)
        failures.extend(player_errors(log.read_text(errors="replace") if log.exists() else "Missing player log"))
        result = dict(process, passed=not failures, failures=failures, artifact=identity, player_report=ready,
                      measurement_scope="process launch through completed chart/HUD frames and saved screenshot; monotonic observer bound",
                      source_cache_sha256=source_hashes, source_cache_unchanged=unchanged,
                      fixture=args.fixture, network_requests=network_requests,
                      network_measurement="fixture sentinel" if args.fixture else "not externally measured; offline source contract enforced",
                      screenshot=str(storage / "warm-start/overview.png"))
        (evidence / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
        print("passed=" + str(result["passed"]) + "; " + str(evidence), flush=True)
        return 0 if result["passed"] else 2
    finally:
        if server is not None: server.shutdown(); server.server_close()


if __name__ == "__main__": raise SystemExit(main())
