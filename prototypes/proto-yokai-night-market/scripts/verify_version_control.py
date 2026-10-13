"""Check full production inputs and tests against the Git index; exclude results."""
from pathlib import Path
import hashlib
import json
import subprocess

TASK = Path(__file__).resolve().parents[1]
REPO = TASK.parents[1]
REFERENCE = REPO / "prototypes/re-crunch-time/local"
PROJECT = TASK / "local/UnityProject"


def git(*args):
    return subprocess.check_output(["git", *args], cwd=REPO)


def verify():
    entries = {}
    for row in git("ls-files", "--stage", "-z").split(b"\0"):
        if row:
            header, path = row.split(b"\t", 1)
            entries[path.decode("utf-8")] = header.split()[1].decode("ascii")
    groups = {
        "unityAssets": PROJECT / "Assets",
        "packages": PROJECT / "Packages",
        "projectSettings": PROJECT / "ProjectSettings",
        "imageExports": TASK / "local/NightArt",
        "imageGenerationSources": TASK / "fidelity/art",
        "packagingData": TASK / "fidelity/data",
        "copySkill": REPO / ".agents/skills/game-copy-polish",
        "testAndProductionTools": TASK / "scripts",
        "testSpecifications": TASK / "fidelity/test-cases",
        "referenceCodeFixtures": REFERENCE / "decompiled",
        "referenceAssetFixtures": REFERENCE / "exports",
        "referenceStructureFixtures": REFERENCE / "typetrees",
    }
    required = set()
    counts = {}
    for key, directory in groups.items():
        files = [p for p in directory.rglob("*") if p.is_file() and "__pycache__" not in p.parts]
        counts[key] = len(files)
        required.update(files)
    required.update(REFERENCE / name for name in ["behaviours.json", "content-catalog.json", "resource-map.json", "scene-hierarchy.json", "settings.json"])
    required.add(PROJECT / ".gitignore")
    missing = []
    different = []
    manifest = []
    ordered = sorted(required)
    names = [p.relative_to(REPO).as_posix() for p in ordered]
    hashes = subprocess.check_output(["git", "hash-object", "--stdin-paths"], cwd=REPO,
        input="".join(json.dumps(p, ensure_ascii=False) + "\n" for p in names).encode("utf-8"), stderr=subprocess.PIPE).decode().splitlines()
    working_blobs = dict(zip(names, hashes))
    for path in ordered:
        relative = path.relative_to(REPO).as_posix()
        blob = entries.get(relative)
        if not path.exists() or blob is None:
            missing.append(relative)
            continue
        actual = working_blobs[relative]
        if actual != blob:
            different.append(relative)
        manifest.append({"path": relative, "gitBlob": blob, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    forbidden = [p for p in entries if p.startswith("prototypes/proto-yokai-night-market/local/") and (
        "/Library/" in p or "/Temp/" in p or "/Logs/" in p or "/UserSettings/" in p or "/QAResults/" in p or "/Builds/" in p or "/Backups/" in p)]
    probes = ["local/QAResults/probe/report.json", "local/Builds/probe.exe", "local/Backups/probe.zip", "local/UnityProject/Library/probe.asset", "local/UnityProject/UserSettings/probe.asset"]
    ignored = all(subprocess.run(["git", "check-ignore", "--quiet", (TASK / p).relative_to(REPO).as_posix()], cwd=REPO).returncode == 0 for p in probes)
    out = {"passed": not missing and not different and not forbidden and ignored,
           "branch": git("branch", "--show-current").decode().strip(), "groups": counts,
           "requiredFiles": len(required), "missing": missing, "differentFromIndex": different,
           "trackedOutputsOrCaches": forbidden, "outputsIgnored": ignored, "inputs": manifest}
    target = TASK / "local/source-version-control.json"
    target.write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({k: v for k, v in out.items() if k != "inputs"}, ensure_ascii=False))
    return out["passed"]


if __name__ == "__main__":
    raise SystemExit(0 if verify() else 2)
