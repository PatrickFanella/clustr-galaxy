#!/usr/bin/env python3
"""Create and restart a demo profile in two real players; preserve owned v1 sentinels."""
import argparse
import datetime
import hashlib
import json
import pathlib
import struct
from isolated_display import isolated_display
from private_audio import private_audio
from unity_process import run, player_errors
from artifact_identity import identify


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--player", default="Builds/LinuxRelease/ClustrGalaxy.x86_64")
    parser.add_argument("--resolution", choices=("1280x720", "1920x1080", "2560x1440"), default="2560x1440")
    parser.add_argument("--flight-activities", action="store_true")
    parser.add_argument("--campaign", action="store_true")
    parser.add_argument("--expeditions", action="store_true")
    args = parser.parse_args()
    if args.expeditions and not (args.campaign and args.flight_activities): parser.error("--expeditions requires --campaign and --flight-activities")
    width, height = map(int, args.resolution.split("x"))
    player = pathlib.Path(args.player).resolve()
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    evidence = (pathlib.Path("Builds/evidence") / (stamp + "-demo-journey")).resolve()
    config = evidence / "config"
    storage = config / "unity3d/Subcult/Clustr Galaxy"
    sentinels = {}
    for relative in ("ClustrGalaxy/profiles/v1/offline-pilot/demo/9-3.json", "ClustrGalaxy/cache/v1/sentinel.json",
                     "profiles/v1/pilot.json"):
        path = storage / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"v1 sentinel: not valid JSON; never read or migrate\x00\xff\n")
        sentinels[relative] = digest(path)
    artifact = identify(player)
    results = []
    first_profiles = None
    first_postcards = None
    first_replays = None
    first_images = None
    first_packages = None
    for phase in (("create", "resume", "restart") if args.expeditions else ("create", "restart")):
        output = evidence / phase
        output.mkdir()
        with isolated_display(output / "display", width, height) as display:
            with private_audio(output / "audio") as audio:
                env = dict(audio)
                for name in ("DISPLAY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "DRI_PRIME", "LD_LIBRARY_PATH"):
                    if name in display: env[name] = display[name]
                env["XDG_CONFIG_HOME"] = str(config)
                log = output / "player.log"
                result = run([str(player), "--clustr-demo", "--clustr-demo-journey=" + phase,
                    "-force-vulkan", "-screen-width", str(width), "-screen-height", str(height), "-screen-fullscreen", "0",
                    "-logFile", str(log)] + (["--clustr-expedition-journey"] if args.expeditions else []) + (["--clustr-flight-journey"] if args.flight_activities else []) + (["--clustr-campaign-journey"] if args.campaign else []), log, env=env, timeout=480 if args.campaign else 300 if args.flight_activities else 120)
        report = storage / "ClustrGalaxy/demo-journey" / (phase + ".json")
        screenshot = report.with_suffix(".png")
        dimensions = None
        if screenshot.is_file():
            with screenshot.open("rb") as stream:
                header = stream.read(24)
            if len(header) == 24 and header[:8] == b"\x89PNG\r\n\x1a\n":
                dimensions = struct.unpack(">II", header[16:24])
        text = log.read_text(errors="replace")
        preserved = all((storage / relative).is_file() and digest(storage / relative) == checksum
                        for relative, checksum in sentinels.items())
        profiles = {str(path.relative_to(storage)): digest(path) for path in (storage / "ClustrGalaxy/profiles/v2").rglob("*.json")}
        profile_unchanged = bool(profiles) and (phase == "create" or profiles == first_profiles)
        if phase == "resume":
            # Only activity companions may change when the paused trip is deliberately resumed.
            profile_unchanged = bool(profiles) and set(first_profiles)<=set(profiles) and all(".activities-" in name or checksum==first_profiles.get(name) for name,checksum in profiles.items())
            first_profiles = profiles
        if phase == "create": first_profiles = profiles
        postcards = {}
        postcard_dimensions_valid = True
        for path in (storage / "ClustrGalaxy/exports").glob("*-postcard.png"):
            with path.open("rb") as stream: header = stream.read(24)
            postcard_dimensions_valid = postcard_dimensions_valid and len(header) == 24 and header[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II",header[16:24]) == (1920,1080)
            postcards[str(path.relative_to(storage))] = digest(path)
        postcard_preserved = len(postcards) == (3 if args.campaign else 1) and (phase == "create" or postcards == first_postcards)
        if phase == "create": first_postcards = postcards
        replays = {str(path.relative_to(storage)): digest(path) for path in (storage / "ClustrGalaxy/replays").glob("*.clustr-replay")}
        replay_preserved = len(replays) == 1 and (phase == "create" or replays == first_replays)
        if phase == "create": first_replays = replays
        images = {str(path.relative_to(storage)): digest(path) for path in (storage / "ClustrGalaxy/images").rglob("*") if path.is_file()}
        image_preserved = not args.campaign or (len(images) == 2 and (phase == "create" or images == first_images))
        if phase == "create": first_images = images
        packages = {str(path.relative_to(storage)): digest(path) for folder in ("replays", "ghosts", "imported") for path in (storage / "ClustrGalaxy" / folder).glob("*.clustr-replay")}
        packages_preserved = bool(packages) and (phase == "create" or packages == first_packages)
        if phase == "create": first_packages = packages
        passed = (result["process_exit"] == 0 and not result["timed_out"] and report.is_file()
                  and json.loads(report.read_text()).get("passed") is True and preserved and profile_unchanged
                  and replay_preserved and packages_preserved and image_preserved and dimensions == (width, height) and postcard_dimensions_valid and postcard_preserved
                  and not player_errors(text) and "[ClustrGalaxy.DemoJourney] FAIL" not in text)
        result.update(phase=phase, passed=passed, all_replay_package_sha256=packages, all_packages_preserved_on_restart=packages_preserved, image_sha256=images, images_preserved_on_restart=image_preserved, replay_sha256=replays, replay_preserved_on_restart=replay_preserved, postcard_sha256=postcards, postcard_dimensions_valid=postcard_dimensions_valid, postcard_preserved_on_restart=postcard_preserved, screenshot=str(screenshot), screenshot_dimensions=dimensions, v1_sentinels_preserved=preserved,
                      profile_unchanged_on_restart=profile_unchanged, profile_sha256=profiles)
        results.append(result)
        (evidence / "summary.json").write_text(json.dumps(dict(player=str(player), player_sha256=digest(player),
            artifact=artifact, campaign=args.campaign, expeditions=args.expeditions, flight_activities=args.flight_activities, resolution=args.resolution, real_controller=False, virtual_controller_focus_traversal=True, range_uses_fixture_setup=True, scenarios=results,
            v1_sentinel_sha256=sentinels, passed=len(results) == (3 if args.expeditions else 2) and all(item["passed"] for item in results)), indent=2) + "\n")
        print(f"{phase}: {passed}; {evidence}", flush=True)
        if not passed: return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
