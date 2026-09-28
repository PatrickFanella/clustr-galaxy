#!/usr/bin/env python3
"""Run real-player virtual-controller smoke in owned GPU/audio sessions."""
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


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--player", default="Builds/Linux/ClustrGalaxy.x86_64")
    args = parser.parse_args()
    player = pathlib.Path(args.player).resolve()
    if not player.is_file(): raise FileNotFoundError(player)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    output = pathlib.Path("Builds/evidence") / (stamp + "-ui-smoke")
    output.mkdir(parents=True)
    artifact = identify(player)
    results = []
    resolutions = ((1280,720),(1920,1080),(2560,1440))
    with player.open("rb") as stream: checksum = hashlib.file_digest(stream, "sha256").hexdigest()
    for width, height in resolutions:
        evidence = (output / f"{width}x{height}").resolve()
        evidence.mkdir()
        with isolated_display(evidence / "display", width, height) as display:
            with private_audio(evidence / "audio") as audio:
                env = dict(audio)
                for name in ("DISPLAY","WAYLAND_DISPLAY","XDG_RUNTIME_DIR","DRI_PRIME","LD_LIBRARY_PATH"):
                    if name in display: env[name] = display[name]
                env["XDG_CONFIG_HOME"] = str(evidence / "config")
                log = evidence / "player.log"
                result = run([str(player),"--clustr-demo","--clustr-ui-smoke","-force-vulkan",
                              "-screen-width",str(width),"-screen-height",str(height),"-screen-fullscreen","0",
                              "-logFile",str(log)],log,env=env,timeout=90)
        captures = list(evidence.rglob("ui-smoke/result.json"))
        screenshots = list(evidence.rglob("ui-smoke/actions.png"))
        text = log.read_text(errors="replace") if log.exists() else ""
        dimensions = None
        if len(screenshots) == 1:
            with screenshots[0].open("rb") as stream:
                header = stream.read(24)
            if len(header) == 24 and header[:8] == b"\x89PNG\r\n\x1a\n": dimensions = struct.unpack(">II",header[16:24])
        result.update(width=width,height=height,real_controller=False,
                      passed=result["process_exit"]==0 and not result["timed_out"] and len(captures)==1 and len(screenshots)==1
                      and dimensions == (width,height)
                      and json.loads(captures[0].read_text()).get("passed") is True
                      and not player_errors(text) and "[ClustrGalaxy.UiSmoke] FAIL" not in text,
                      screenshot_dimensions=dimensions, screenshots=[str(path) for path in screenshots])
        results.append(result)
        # Interim summaries stay incomplete; only the final resolution can make the run pass.
        complete = len(results) == len(resolutions)
        (output / "summary.json").write_text(json.dumps(dict(player=str(player),player_sha256=checksum,
              artifact=artifact,scenarios=results,complete=complete,
              passed=complete and all(item["passed"] for item in results)),indent=2)+"\n")
        print(f"{width}x{height}: {result['passed']}; {evidence}",flush=True)
    return 0 if len(results) == len(resolutions) and all(item["passed"] for item in results) else 2


if __name__ == "__main__":
    raise SystemExit(main())
