#!/usr/bin/env python3
"""Run both Unity suites, preserving separate process, compiler and test evidence."""
import argparse
import datetime
import json
import os
import pathlib
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from unity_process import run
from private_audio import private_audio
from native_build import build_native


def inspect_result(xml, log, process):
    failures = []
    summary = dict(process)
    if process["process_exit"] != 0 or process["timed_out"]:
        failures.append("Unity process did not exit successfully")
    try:
        result = ET.parse(xml).getroot()
        summary["tests"] = {key: result.get(key) for key in ("result", "total", "passed", "failed", "skipped")}
        if result.get("result") != "Passed" or int(result.get("total", "0")) < 1 or int(result.get("failed", "0")) != 0:
            failures.append("Tests did not pass")
    except (OSError, ET.ParseError, ValueError) as error:
        failures.append("Missing or invalid test results: " + str(error))
    text = log.read_text(errors="replace") if log.exists() else ""
    diagnostics = [line for line in text.splitlines() if re.search(r"\b(?:error|warning) CS\d+", line)]
    summary["compiler_diagnostics"] = diagnostics
    if any("error CS" in line or "Assets/ClustrGalaxy/" in line for line in diagnostics):
        failures.append("Project compiler gate failed")
    if not log.exists(): failures.append("Missing Unity log")
    summary.update(failures=failures, passed=not failures, results=str(xml))
    return summary


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--unity", default=os.environ.get("UNITY_EDITOR", "/home/onnwee/Videos/6000.4.10f1/Editor/Unity"))
    parser.add_argument("--mode", choices=["EditMode", "PlayMode", "all"], default="all")
    args = parser.parse_args()
    root = pathlib.Path(__file__).resolve().parents[1]
    build_native(root)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    out = root / "Builds" / "validation" / stamp
    out.mkdir(parents=True)
    evidence = {"source_sha": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
                "source_dirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=root)), "suites": []}
    for mode in (["EditMode", "PlayMode"] if args.mode == "all" else [args.mode]):
        xml, log = out / (mode + ".xml"), out / (mode + ".log")
        command = [args.unity, "-batchmode", "-nographics", "-noaudio", "-projectPath", str(root), "-runTests",
                   "-testPlatform", mode, "-testResults", str(xml), "-logFile", str(log)]
        with private_audio(out / (mode + "-audio")) as env:
            summary = inspect_result(xml, log, run(command, log, env=env))
        summary["suite"] = mode
        evidence["suites"].append(summary)
        evidence["passed"] = all(item["passed"] for item in evidence["suites"])
        (out / "summary.json").write_text(json.dumps(evidence, indent=2) + "\n")
        print(f"{mode}: {'PASS' if summary['passed'] else 'FAIL'}; {xml}", flush=True)
    return 0 if evidence["passed"] else 2


if __name__ == "__main__":
    sys.exit(main())
