#!/usr/bin/env python3
"""Exercise rendered hierarchy against an owned local fixture or bounded public reads."""
import argparse
import datetime
import hashlib
import json
import pathlib
import threading
from http.server import ThreadingHTTPServer
from isolated_display import isolated_display
from private_audio import private_audio
from provider_fixture import Fixture
from unity_process import run, player_errors
from artifact_identity import identify


def evaluate_profile(report, artifact, requested_seconds):
    """Bind a profile verdict to the artifact under test; 30 s runs are preflights only."""
    if report is None: return ["Beta profile report missing"]
    failures = []
    if not artifact.get("source_sha") or report.get("source_sha") != artifact["source_sha"]:
        failures.append("Report source does not match the tested artifact")
    if report.get("duration_seconds") != requested_seconds:
        failures.append("Report duration does not match the requested profile")
    verdict = report.get("qualified") if requested_seconds == 600 else report.get("preflight_passed")
    if verdict is not True: failures.append("Report verdict did not pass")
    return failures


def load_profile(config):
    """The single report named by the player's latest-report pointer under this evidence config."""
    pointers = list(config.rglob("latest-report-path.txt")) if config.exists() else []
    if len(pointers) != 1: return None, None
    path = pathlib.Path(pointers[0].read_text().strip())
    try: return path, json.loads(path.read_text())
    except (OSError, ValueError): return path, None


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--player",default="Builds/LinuxRelease/ClustrGalaxy.x86_64")
    parser.add_argument("--window-system", choices=("x11", "wayland"), default="x11")
    parser.add_argument("--single-threaded-rendering", action="store_true", help="Diagnostic Unity -force-gfx-direct; recorded separately from default rendering")
    parser.add_argument("--live",action="store_true")
    parser.add_argument("--jump",action="store_true")
    parser.add_argument("--offline-restart",action="store_true")
    parser.add_argument("--stress",action="store_true")
    parser.add_argument("--active-features", action="store_true")
    parser.add_argument("--profile-seconds",type=int,choices=(30,600))
    args=parser.parse_args()
    if args.active_features and not args.profile_seconds: parser.error("--active-features requires --profile-seconds")
    if args.offline_restart and (not args.jump or args.live): parser.error("Offline restart requires a local fixture jump journey")
    if args.jump and args.profile_seconds: parser.error("Jump journeys and performance sampling are separate")
    if args.live and args.stress: parser.error("Public stress reads are not allowed")
    player=pathlib.Path(args.player).resolve()
    stamp=datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    evidence=(pathlib.Path("Builds/evidence") / (stamp+"-hierarchy")).resolve();evidence.mkdir(parents=True)
    artifact=identify(player)
    command=[str(player),"--clustr-live-smoke","-force-vulkan","-screen-width","2560","-screen-height","1440",
             "-screen-fullscreen","0","-logFile",str(evidence/"player.log")]
    if args.window_system == "wayland": command.append("-force-wayland")
    if args.single_threaded_rendering: command.append("-force-gfx-direct")
    if args.profile_seconds: command.append("--clustr-beta-profile="+str(args.profile_seconds))
    if args.active_features: command.append("--clustr-profile-active-features")
    if args.jump: command.append("--clustr-jump-smoke")
    server=None
    try:
        if not args.live:
            class Handler(Fixture):
                stress=args.stress
                calls=0
            server=ThreadingHTTPServer(("127.0.0.1",0),Handler)
            threading.Thread(target=server.serve_forever,daemon=True).start()
            command.append("--clustr-fixture-url=http://127.0.0.1:"+str(server.server_port)+"/api")
        offline_result=None
        with isolated_display(evidence/"display") as display:
            with private_audio(evidence/"audio") as audio:
                env=dict(audio)
                for name in ("DISPLAY","WAYLAND_DISPLAY","XDG_RUNTIME_DIR","DRI_PRIME","LD_LIBRARY_PATH"):
                    if name in display: env[name]=display[name]
                env["XDG_CONFIG_HOME"]=str(evidence/"config")
                result=run(command,evidence/"player.log",env=env,timeout=(args.profile_seconds or 0)+240)
                if args.offline_restart and result["process_exit"] == 0:
                    calls_before=Handler.calls
                    offline_log=evidence/"offline-player.log"
                    offline_command=[str(offline_log) if value==str(evidence/"player.log") else value for value in command]+["--clustr-offline"]
                    offline_result=run(offline_command,offline_log,env=env,timeout=240)
                    offline_text=offline_log.read_text(errors="replace")
                    offline_result["network_requests"]=Handler.calls-calls_before
                    offline_result["passed"]=(offline_result["process_exit"]==0 and not offline_result["timed_out"] and
                        offline_result["network_requests"]==0 and "[ClustrGalaxy.LiveSmoke] JUMP COMPLETE" in offline_text and
                        "[ClustrGalaxy.LiveSmoke] FAILED" not in offline_text and not player_errors(offline_text))
        log=(evidence/"player.log").read_text(errors="replace")
        profile_path, profile_failures = None, []
        if args.profile_seconds:
            profile_path, profile = load_profile(evidence/"config")
            profile_failures = evaluate_profile(profile, artifact, args.profile_seconds)
        with player.open("rb") as stream: checksum=hashlib.file_digest(stream,"sha256").hexdigest()
        result.update(player_sha256=checksum,artifact=artifact,window_system=args.window_system,single_threaded_rendering_requested=args.single_threaded_rendering,scenario="live-hierarchy" if args.live else "fixture-stress" if args.stress else "fixture-hierarchy",
                      active_features=args.active_features, offline_restart_requested=args.offline_restart, offline_restart=offline_result, jump_requested=args.jump, profile_seconds=args.profile_seconds,
                      fixture_calls=None if args.live else Handler.calls,
                      profile_report=str(profile_path) if profile_path else None, profile_failures=profile_failures,
                      passed=not profile_failures and (not args.offline_restart or offline_result is not None and offline_result["passed"]) and result["process_exit"]==0 and not result["timed_out"] and "[ClustrGalaxy.LiveSmoke] COMPLETE" in log
                      and (not args.jump or "[ClustrGalaxy.LiveSmoke] JUMP COMPLETE" in log)
                      and "[ClustrGalaxy.LiveSmoke] FAILED" not in log and not player_errors(log))
        (evidence/"summary.json").write_text(json.dumps(result,indent=2)+"\n")
        print(f"passed={result['passed']}; {evidence}",flush=True)
        return 0 if result["passed"] else 2
    finally:
        if server is not None: server.shutdown();server.server_close()


if __name__ == "__main__": raise SystemExit(main())
