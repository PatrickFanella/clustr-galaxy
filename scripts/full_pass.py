#!/usr/bin/env python3
"""Serialize the automated pre-human matrix and retain every child evidence report.

This is an evidence index, not permission to replace missing release/human gates.
"""
import argparse
import datetime
import json
import pathlib
import subprocess
import sys
from artifact_identity import identify
from unity_process import run

ROOT = pathlib.Path(__file__).resolve().parents[1]


def evidence_passed(process, reports, expected=1):
    return (process.get("process_exit") == 0 and not process.get("timed_out", False)
            and len(reports) == expected
            and all(value.get("passed", value.get("build_gate_passed")) is True for value in reports))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile-seconds", type=int, choices=(30, 600), default=600)
    parser.add_argument("--no-live", action="store_true", help="Omit public reads; leaves the full pass incomplete")
    args = parser.parse_args()
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    output = ROOT / "Builds/evidence" / (stamp + "-full-pass")
    output.mkdir(parents=True)
    report = dict(source_sha=subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
                  source_dirty=bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT)),
                  steps=[], passed=False, human_accepted=False, release_qualified=False,
                  external_gates=["Human controller and enjoyment acceptance", "Clean-source Arch verification",
                                  "Licensed Ubuntu 24.04 build and runtime", "Licensed Gitea runner activation"])

    def save():
        temporary = output / "summary.pending"
        temporary.write_text(json.dumps(report, indent=2) + "\n")
        temporary.replace(output / "summary.json")

    def step(name, script, arguments=(), pattern="Builds/evidence/*/summary.json", expected=1):
        before = set(ROOT.glob(pattern))
        command = [sys.executable, str(ROOT / "scripts" / script), *arguments]
        print("START " + name, flush=True)
        (output / (name + ".log")).write_text("Child evidence paths and process result are recorded in summary.json.\n")
        result = run(command, output / (name + ".log"), timeout=2400)
        paths = sorted(set(ROOT.glob(pattern)) - before)
        values = []
        for path in paths:
            try: values.append(json.loads(path.read_text()))
            except (OSError, ValueError): values.append({"passed": False})
        result.update(name=name, reports=[str(p) for p in paths], passed=evidence_passed(result, values, expected))
        if "release_artifact" in report:
            result["artifact_unchanged"] = identify(ROOT / "Builds/LinuxRelease/ClustrGalaxy.x86_64")["tree_sha256"] == report["release_artifact"]["tree_sha256"]
            result["passed"] &= result["artifact_unchanged"]
        report["steps"].append(result)
        save()
        print(("PASS " if result["passed"] else "FAIL ") + name + ": " + ", ".join(result["reports"]), flush=True)
        return paths if result["passed"] else []

    save()
    checks = run([sys.executable, "-m", "unittest", "discover", "-s", "scripts", "-p", "test_*.py"], output / "python-tests.log", timeout=120)
    checks.update(name="python-tests", passed=checks["process_exit"] == 0 and not checks["timed_out"])
    report["steps"].append(checks); save()
    if not checks["passed"]: return 2
    if not step("validation", "validate.py", pattern="Builds/validation/*/summary.json"):
        report["stopped_reason"] = "Compilation or correctness gate failed"; save(); return 2
    if not step("build", "build.py", pattern="Builds/build-logs/*/BuildLinux*.json", expected=2):
        report["stopped_reason"] = "Fresh artifact build failed"; save(); return 2
    for name, folder in (("release_artifact", "LinuxRelease"), ("development_artifact", "Linux")):
        identity = identify(ROOT / "Builds" / folder / "ClustrGalaxy.x86_64")
        report[name] = {k: v for k, v in identity.items() if k != "files_sha256"}
    save()
    step("controller-smoke", "ui_smoke.py", ["--player", "Builds/LinuxRelease/ClustrGalaxy.x86_64"])
    demo = step("campaign-1440", "demo_journey.py", ["--campaign", "--flight-activities", "--expeditions", "--resolution", "2560x1440"])
    for resolution in ("1280x720", "1920x1080", "2560x1440"):
        step("visual-" + resolution, "visual_smoke.py", ["--resolution", resolution])
        step("simulation-" + resolution, "simulation_journey.py", ["--resolution", resolution])
        if resolution != "2560x1440":
            step("creation-restart-" + resolution, "demo_journey.py", ["--resolution", resolution])
    if demo:
        storage = demo[0].parent / "config/unity3d/Subcult/Clustr Galaxy/ClustrGalaxy"
        replays = sorted((storage / "replays").glob("*.clustr-replay"))
        if replays: step("recipient-import", "replay_import_journey.py", ["--source", str(replays[0])])
        else:
            report["steps"].append(dict(name="recipient-import", passed=False, reason="Missing exported replay")); save()
        step("cockpit-interactions", "interaction_profile.py", ["--profile-from", str(storage / "profiles/v2"), "--window-system", "wayland", "--single-threaded-rendering"])
    graphics = ["--window-system", "wayland", "--single-threaded-rendering"]
    step("hierarchy-offline-jumps", "hierarchy_smoke.py", ["--jump", "--offline-restart", *graphics])
    step("development-hierarchy", "hierarchy_smoke.py", ["--player", "Builds/Linux/ClustrGalaxy.x86_64", *graphics])
    step("active-stress", "hierarchy_smoke.py", ["--stress", "--active-features", "--profile-seconds", str(args.profile_seconds), *graphics])
    if not args.no_live:
        live = step("public-hierarchy-jumps", "hierarchy_smoke.py", ["--live", "--jump", *graphics])
        if live:
            step("public-performance", "hierarchy_smoke.py", ["--live", "--active-features", "--profile-seconds", str(args.profile_seconds), *graphics])
        else:
            report["steps"].append(dict(name="public-performance", passed=False, reason="Public hierarchy prerequisite failed; no repeat provider traversal"))
    report["automated_passed"] = all(s["passed"] for s in report["steps"]) and not args.no_live and args.profile_seconds == 600
    report["passed"] = report["automated_passed"]
    save()
    print("Full automated pass: " + str(report["passed"]) + "; " + str(output / "summary.json"), flush=True)
    return 0 if report["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
