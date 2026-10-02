"""Identify the complete player tree, including managed code and presentation assets."""
import hashlib
import json
import pathlib


def identify(player):
    files = {}
    for path in sorted(player.parent.rglob("*")):
        if path.is_file():
            with path.open("rb") as stream:
                files[path.relative_to(player.parent).as_posix()] = hashlib.file_digest(stream, "sha256").hexdigest()
    serialized = json.dumps(files, sort_keys=True, separators=(",", ":")).encode()
    identity = {"tree_sha256": hashlib.sha256(serialized).hexdigest(), "files_sha256": files,
                "source_sha": None, "build_manifest": None}
    logs = pathlib.Path(__file__).resolve().parents[1] / "Builds/build-logs"
    for manifest in sorted(logs.glob("*/BuildLinux*.json"), reverse=True):
        try:
            value = json.loads(manifest.read_text())
        except (OSError, ValueError):
            continue
        if value.get("build_gate_passed") is True and value.get("sha256") == files:
            identity.update(source_sha=value.get("source_sha"), build_manifest=str(manifest))
            break
    return identity
