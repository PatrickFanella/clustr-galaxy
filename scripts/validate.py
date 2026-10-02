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
import time
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


def project_assemblies(root):
    """Assembly names defined by this project's asmdef files."""
    return sorted(json.loads(path.read_text())["name"] for path in (root / "Assets/ClustrGalaxy").rglob("*.asmdef"))


def force_project_compilation(root):
    """Remove compiled project outputs so Unity must run csc and print their diagnostics.

    Incremental compilation reuses unchanged assemblies silently, so a warning-free log
    would otherwise not show that project code compiles without warnings.
    """
    removed = []
    library = root / "Library"
    folders = [library / "ScriptAssemblies", *sorted((library / "Bee/artifacts").glob("*.dag"))]
    for folder in folders:
        for name in project_assemblies(root):
            for path in folder.glob(name + ".*"):
                if path.is_file():
                    path.unlink()
                    removed.append(str(path.relative_to(root)))
    return removed


def compilation_evidence(root, since):
    """Every project assembly must have been written after `since`."""
    missing, stale = [], []
    for name in project_assemblies(root):
        path = root / "Library/ScriptAssemblies" / (name + ".dll")
        if not path.exists(): missing.append(name)
        elif path.stat().st_mtime < since: stale.append(name)
    return {"compiled": not missing and not stale, "missing": missing, "stale": stale}


def overall(suites, expected_modes, compilation):
    """Passed only once every requested suite completed and project code actually compiled."""
    complete = [suite["suite"] for suite in suites] == list(expected_modes)
    return {"complete": complete,
            "passed": complete and compilation.get("compiled") is True and all(suite["passed"] for suite in suites)}


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
    modes = ["EditMode", "PlayMode"] if args.mode == "all" else [args.mode]
    evidence = {"source_sha": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
                "source_dirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=root)),
                "requested_suites": modes, "suites": [], "complete": False, "passed": False}
    started = time.time()
    evidence["forced_recompilation"] = force_project_compilation(root)
    compilation = {"compiled": False}

    def save():
        temporary = out / "summary.pending"
        temporary.write_text(json.dumps(evidence, indent=2) + "\n")
        temporary.replace(out / "summary.json")

    save()
    for mode in modes:
        xml, log = out / (mode + ".xml"), out / (mode + ".log")
        command = [args.unity, "-batchmode", "-nographics", "-noaudio", "-projectPath", str(root), "-runTests",
                   "-testPlatform", mode, "-testResults", str(xml), "-logFile", str(log)]
        with private_audio(out / (mode + "-audio")) as env:
            summary = inspect_result(xml, log, run(command, log, env=env))
        summary["suite"] = mode
        evidence["suites"].append(summary)
        if len(evidence["suites"]) == 1:
            compilation = compilation_evidence(root, started)
            evidence["project_compilation"] = compilation
        evidence.update(overall(evidence["suites"], modes, compilation))
        save()
        print(f"{mode}: {'PASS' if summary['passed'] else 'FAIL'}; {xml}", flush=True)
    if not compilation.get("compiled"):
        print("Project scripts were not compiled in this run: " + json.dumps(compilation), flush=True)
    return 0 if evidence["passed"] else 2


if __name__ == "__main__":
    sys.exit(main())
