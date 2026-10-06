#!/usr/bin/env python3
"""Launch a visible demo with owned storage and count only the selected gamepad's events."""
import argparse, datetime, hashlib, json, os, pathlib, shutil, struct, subprocess, time
from artifact_identity import identify

def profile_hashes(source):
    files = sorted(source.rglob("*.json"))
    if any(p.is_symlink() for p in source.rglob("*")):
        raise ValueError("Fixture profiles must not contain symbolic links")
    return {str(p.relative_to(source)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}


def seed_profile(source, config):
    source = pathlib.Path(source).resolve(strict=True)
    if source.name != "v2" or source.parent.name != "profiles" or not source.is_dir():
        raise ValueError("Choose the completed fixture's ClustrGalaxy/profiles/v2 directory")
    hashes = profile_hashes(source)
    if not hashes: raise ValueError("Fixture profile has no saved progress")
    destination = config / "unity3d/Subcult/Clustr Galaxy/ClustrGalaxy/profiles/v2"
    for relative in hashes:
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / relative, target)
    return source, hashes


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--player",default="Builds/LinuxRelease/ClustrGalaxy.x86_64")
    parser.add_argument("--controller",required=True)
    parser.add_argument("--profile-from", help="Optional completed fixture profiles/v2 directory; copies progress only, not media")
    parser.add_argument("--resolution", choices=("1280x720", "1920x1080", "2560x1440"), default="1280x720")
    parser.add_argument("--window-system",choices=("x11","wayland"),default="x11")
    args=parser.parse_args()
    player=pathlib.Path(args.player).resolve()
    stamp=datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    evidence=(pathlib.Path("Builds/evidence")/(stamp+"-manual-controller")).resolve();evidence.mkdir(parents=True)
    artifact=identify(player)
    source = None; source_hashes = {}
    if args.profile_from: source, source_hashes = seed_profile(args.profile_from, evidence / "config")
    width, height = args.resolution.split("x")
    env=dict(os.environ,XDG_CONFIG_HOME=str(evidence/"config"))
    command=[str(player),"--clustr-demo","-force-vulkan","-screen-width",width,"-screen-height",height,"-screen-fullscreen","0","-logFile",str(evidence/"player.log")]
    if args.window_system=="wayland":command.append("-force-wayland")
    report={"window_system":args.window_system,"resolution":args.resolution,"seeded_fixture_progress":source is not None,"source_profile":str(source) if source else None,"source_profile_sha256":source_hashes,"artifact":artifact,"command":command,"controller_path":args.controller,"physical_button_events":0,"physical_axis_events":0,"human_acceptance":"pending","qualified":False}
    fd=None
    try:fd=os.open(args.controller,os.O_RDONLY|os.O_NONBLOCK)
    except OSError as error:report["event_capture_error"]=str(error)
    (evidence/"session.json").write_text(json.dumps(report,indent=2)+"\n")
    process=subprocess.Popen(command,env=env)
    print("Visible manual player started: "+str(evidence),flush=True)
    packet=struct.Struct("llHHi");pending=b""
    try:
        while process.poll() is None:
            if fd is not None:
                try:pending+=os.read(fd,packet.size*64)
                except BlockingIOError:pass
                except OSError as error:report["event_capture_error"]=str(error);os.close(fd);fd=None
                while len(pending)>=packet.size:
                    _,_,kind,code,value=packet.unpack(pending[:packet.size]);pending=pending[packet.size:]
                    if kind==1 and value==1:report["physical_button_events"]+=1
                    elif kind==3:report["physical_axis_events"]+=1
            time.sleep(.1)
    finally:
        if fd is not None:os.close(fd)
        report["process_exit"]=process.poll()
        if source is not None: report["source_profile_unchanged"] = profile_hashes(source) == source_hashes
        report["physical_input_observed"]=report["physical_button_events"]>0 and report["physical_axis_events"]>0
        (evidence/"session.json").write_text(json.dumps(report,indent=2)+"\n")
        print("Manual session ended; player assessment still required: "+str(evidence),flush=True)
    return 0 if process.returncode==0 else 2
if __name__=="__main__":raise SystemExit(main())
