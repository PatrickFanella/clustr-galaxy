#!/usr/bin/env python3
"""Measure real editor/observatory UI separately, using a copied acceptance fixture profile."""
import argparse
import datetime
import json
import pathlib
import shutil
from artifact_identity import identify
from demo_journey import digest
from isolated_display import isolated_display
from private_audio import private_audio
from unity_process import run, player_errors


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile-from", required=True, help="Completed demo journey's ClustrGalaxy/profiles/v2 directory")
    parser.add_argument("--window-system", choices=("x11", "wayland"), default="x11")
    parser.add_argument("--player", default="Builds/LinuxRelease/ClustrGalaxy.x86_64")
    parser.add_argument("--single-threaded-rendering", action="store_true", help="Diagnostic Unity -force-gfx-direct; recorded separately from default rendering")
    args = parser.parse_args()
    source = pathlib.Path(args.profile_from).resolve(strict=True)
    if source.name != "v2" or source.parent.name != "profiles": raise ValueError("Choose only the completed fixture's v2 profile directory")
    player = pathlib.Path(args.player).resolve(strict=True)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    evidence = (pathlib.Path("Builds/evidence") / (stamp + "-ui-performance")).resolve()
    config = evidence / "config"
    storage = config / "unity3d/Subcult/Clustr Galaxy/ClustrGalaxy"
    source_hashes = {str(p.relative_to(source)): digest(p) for p in source.rglob("*.json")}
    shutil.copytree(source, storage / "profiles/v2")
    log = evidence / "player.log"
    artifact = identify(player)
    with isolated_display(evidence / "display", 2560, 1440) as display:
        with private_audio(evidence / "audio") as audio:
            env = dict(audio)
            for name in ("DISPLAY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "DRI_PRIME", "LD_LIBRARY_PATH"):
                if name in display: env[name] = display[name]
            env["XDG_CONFIG_HOME"] = str(config)
            result = run([str(player), "--clustr-demo", "--clustr-demo-journey=ui-performance", "--clustr-ui-performance",
                          "-force-vulkan", "-screen-width", "2560", "-screen-height", "1440",
                          "-screen-fullscreen", "0", "-logFile", str(log)] + (["-force-wayland"] if args.window_system == "wayland" else []) + (["-force-gfx-direct"] if args.single_threaded_rendering else []), log, env=env, timeout=180)
    reports = [json.loads(p.read_text()) for p in (storage / "ui-performance").glob("*/*.json")]
    errors = player_errors(log.read_text(errors="replace"))
    unchanged = source_hashes == {str(p.relative_to(source)): digest(p) for p in source.rglob("*.json")}
    restored = source_hashes == {str(p.relative_to(storage / "profiles/v2")): digest(p) for p in (storage / "profiles/v2").rglob("*.json")}
    result.update(artifact=artifact, window_system=args.window_system, single_threaded_rendering_requested=args.single_threaded_rendering, seeded_fixture_progress=True, source_profile_unchanged=unchanged,
                  copied_profile_unchanged=restored, runtime_errors=errors, reports=reports,
                  release_qualified=False)
    result["functional_passed"] = len(reports) == 2 and not errors and unchanged and restored and all(r["functional_passed"] for r in reports)
    result["passed"] = result["process_exit"] == 0 and result["functional_passed"] and all(r["measurements_passed"] for r in reports)
    (evidence / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
    print(f"functional={result['functional_passed']}; measurements={result['passed']}; {evidence}", flush=True)
    return 0 if result["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
