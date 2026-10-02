#!/usr/bin/env python3
"""Render the original exploration collection inside an owned hardware display."""
import datetime,json,os,pathlib,subprocess
from isolated_display import isolated_display
from private_audio import private_audio
from artifact_identity import identify
root=pathlib.Path(__file__).resolve().parents[1]
stamp=datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
out=root/"Builds/evidence"/(stamp+"-art-preview");out.mkdir(parents=True)
player=root/"Builds/LinuxRelease/ClustrGalaxy.x86_64"
report={"artifact":identify(player),"qualified":False}
try:
    with isolated_display(out/"display",1920,1080) as display, private_audio(out/"audio") as audio:
        env=dict(audio,**{k:v for k,v in display.items() if k in ("DISPLAY","WAYLAND_DISPLAY","XDG_RUNTIME_DIR","DRI_PRIME","LD_LIBRARY_PATH")})
        env["XDG_CONFIG_HOME"]=str(out/"config")
        p=subprocess.run([str(player),"--clustr-demo","--clustr-art-preview","-force-vulkan","-screen-width","1920","-screen-height","1080","-screen-fullscreen","0","-logFile",str(out/"player.log")],env=env,timeout=60,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        report["exit_code"]=p.returncode
        pictures=list((out/"config").rglob("exploration-assets.png"));report["images"]=[str(x) for x in pictures]
        log=(out/"player.log").read_text(errors="replace")
        report["passed"]=p.returncode==0 and len(pictures)==1 and "[ClustrGalaxy.Art] Preview captured:" in log and "Exception:" not in log
except Exception as error:report.update(passed=False,error=str(error))
(out/"summary.json").write_text(json.dumps(report,indent=2)+"\n")
print(str(out/"summary.json"));raise SystemExit(0 if report["passed"] else 1)
