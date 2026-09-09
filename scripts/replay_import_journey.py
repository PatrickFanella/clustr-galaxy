#!/usr/bin/env python3
"""Import a prior journey's Survey replay in a fresh, separately named player profile."""
import argparse
import datetime
import json
import pathlib
from artifact_identity import identify
from demo_journey import digest
from isolated_display import isolated_display
from private_audio import private_audio
from unity_process import run, player_errors


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--player", default="Builds/LinuxRelease/ClustrGalaxy.x86_64")
    args = parser.parse_args()
    source = pathlib.Path(args.source).resolve(strict=True)
    player = pathlib.Path(args.player).resolve(strict=True)
    source_hash = digest(source)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    evidence = (pathlib.Path("Builds/evidence") / (stamp + "-recipient-import")).resolve()
    evidence.mkdir(parents=True)
    config = evidence / "config"
    storage = config / "unity3d/Subcult/Clustr Galaxy/ClustrGalaxy"
    sentinel = storage / "profiles/v1/replay-recipient.json"
    sentinel.parent.mkdir(parents=True)
    sentinel.write_bytes(b"v1 recipient sentinel: never read or migrate\x00\xff")
    sentinel_hash = digest(sentinel)
    artifact = identify(player)
    log = evidence / "player.log"
    with isolated_display(evidence / "display", 1920, 1080) as display:
        with private_audio(evidence / "audio") as audio:
            env = dict(audio)
            for name in ("DISPLAY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "DRI_PRIME", "LD_LIBRARY_PATH"):
                if name in display: env[name] = display[name]
            env["XDG_CONFIG_HOME"] = str(config)
            result = run([str(player), "--clustr-demo", "--clustr-profile=replay-recipient",
                          "--clustr-demo-journey=import-only", "--clustr-import-fixture-path=" + str(source),
                          "-force-vulkan", "-screen-width", "1920", "-screen-height", "1080",
                          "-screen-fullscreen", "0", "-logFile", str(log)], log, env=env, timeout=120)
    report = storage / "demo-journey/import-only.json"
    imported = list((storage / "imported").glob("*.clustr-replay"))
    profile_files = list((storage / "profiles/v2").rglob("*.json"))
    errors = player_errors(log.read_text(errors="replace"))
    result.update(artifact=artifact, source=str(source), source_sha256=source_hash,
                  source_unchanged=digest(source) == source_hash, v1_preserved=digest(sentinel) == sentinel_hash,
                  different_profile="replay-recipient", created_progression_files=[str(p) for p in profile_files],
                  imported_sha256=[digest(p) for p in imported], runtime_errors=errors)
    result["passed"] = (result["process_exit"] == 0 and not result["timed_out"] and not errors
                        and report.is_file() and json.loads(report.read_text()).get("passed") is True
                        and result["source_unchanged"] and result["v1_preserved"] and not profile_files
                        and len(imported) == 1 and digest(imported[0]) == source_hash)
    (evidence / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
    print(f"passed={result['passed']}; {evidence}", flush=True)
    return 0 if result["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
