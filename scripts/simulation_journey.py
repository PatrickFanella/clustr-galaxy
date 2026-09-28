#!/usr/bin/env python3
"""Fly a real departure, drive cancellation and moving-planet cruise in an isolated player."""
import argparse, datetime, json, pathlib, struct, math
from artifact_identity import identify
from isolated_display import isolated_display
from private_audio import private_audio
from unity_process import run, player_errors

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--resolution",choices=("1280x720","1920x1080","2560x1440"),default="1920x1080")
    args=parser.parse_args();width,height=map(int,args.resolution.split("x"))
    player=pathlib.Path("Builds/LinuxRelease/ClustrGalaxy.x86_64").resolve()
    stamp=datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    output=(pathlib.Path("Builds/evidence")/(stamp+"-simulation-journey")).resolve();output.mkdir(parents=True)
    config=output/"config";artifact=identify(player);log=output/"player.log"
    with isolated_display(output/"display",width,height) as display:
        with private_audio(output/"audio") as audio:
            env=dict(audio)
            for name in ("DISPLAY","WAYLAND_DISPLAY","XDG_RUNTIME_DIR","DRI_PRIME","LD_LIBRARY_PATH"):
                if name in display:env[name]=display[name]
            env["XDG_CONFIG_HOME"]=str(config)
            result=run([str(player),"--clustr-demo","--clustr-simulation-journey","-force-vulkan","-screen-width",str(width),"-screen-height",str(height),"-screen-fullscreen","0","-logFile",str(log)],log,env=env,timeout=120)
    folder=config/"unity3d/Subcult/Clustr Galaxy/ClustrGalaxy/simulation-journey"
    report=json.loads((folder/"summary.json").read_text()) if (folder/"summary.json").exists() else {}
    captures={}
    for name in ("arrival","chart"):
        path=folder/(name+".png")
        if path.exists():
            data=path.read_bytes()[:24];captures[name]={"path":str(path),"dimensions":list(struct.unpack(">II",data[16:24])) if data[:8]==b"\x89PNG\r\n\x1a\n" else None}
    trajectory_path=folder/"trajectory.json"
    try:
        poses=json.loads(trajectory_path.read_text())["poses"]
        trajectory_valid=len(poses)==report.get("samples") and len(poses)>=100 and all(
            all(isinstance(p.get(key),(int,float)) and math.isfinite(p[key]) for key in ("seconds","x","y","z","clearance","speed"))
            and p["clearance"]>0 and p["speed"]>=0 and (i==0 or p["seconds"]>poses[i-1]["seconds"]) for i,p in enumerate(poses))
    except (OSError,ValueError,KeyError,TypeError): trajectory_valid=False
    errors=player_errors(log.read_text(errors="replace"))
    result.update(trajectory=str(trajectory_path),trajectory_valid=trajectory_valid,artifact=artifact,report=report,captures=captures,runtime_errors=errors,player_accepted=False,release_qualified=False)
    result["passed"]=trajectory_valid and result["process_exit"]==0 and not result["timed_out"] and report.get("passed") is True and not errors and len(captures)==2 and all(x["dimensions"]==[width,height] for x in captures.values())
    (output/"summary.json").write_text(json.dumps(result,indent=2)+"\n")
    print(f"passed={result['passed']}; {output}",flush=True)
    return 0 if result["passed"] else 2
if __name__=="__main__":raise SystemExit(main())
