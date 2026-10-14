"""Back up the registered APK in a separate local Git repository, with no remote."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / "local"


def git(*args):
    result = subprocess.run(["git", "-C", str(LOCAL), *args], capture_output=True, check=True)
    return result.stdout.decode("utf8", errors="replace").strip()


def main():
    evidence = json.loads((ROOT / "evidence/preparation.json").read_text("utf8"))
    if not (LOCAL / ".git").exists():
        git("init", "--initial-branch=main")
        git("config", "core.autocrlf", "false")
        git("config", "core.compression", "1")
    if git("remote"):
        raise RuntimeError("The private input repository must have no remote.")
    ignore = LOCAL / ".gitignore"
    attributes = LOCAL / ".gitattributes"
    if not ignore.exists():
        ignore.write_text("/logs/\n__pycache__/\n*.py[cod]\n", encoding="utf8")
    if not attributes.exists():
        attributes.write_text("*.apk binary\n", encoding="utf8")
    relative = Path(evidence["working_copy"]).relative_to("local").as_posix()
    git("add", "--", ".gitignore", ".gitattributes", relative)
    if git("diff", "--cached", "--name-only"):
        git("commit", "-m", "Preserve registered Xiaoxiaoshenbing APK input")
    commit = git("rev-parse", "HEAD")
    blob = f"{commit}:{relative}"
    digest = hashlib.sha256()
    proc = subprocess.Popen(["git", "-C", str(LOCAL), "cat-file", "blob", blob], stdout=subprocess.PIPE)
    for block in iter(lambda: proc.stdout.read(1024 * 1024), b""):
        digest.update(block)
    proc.stdout.close()
    if proc.wait():
        raise RuntimeError("Git input blob read failed.")
    git("fsck", "--full")
    state = git("status", "--porcelain")
    record = {"date": "2026-10-14", "phase": "preparation-only", "repository": "local/.git", "remotes": [],
              "commit": commit, "tracked_files": len(git("ls-files").splitlines()),
              "input_path": relative, "input_blob_sha256": digest.hexdigest(), "input_sha256": evidence["apk_sha256"],
              "input_blob_matches": digest.hexdigest() == evidence["apk_sha256"], "git_fsck_pass": True,
              "working_tree_clean": not state, "excluded_generated_outputs": ["local/logs/"],
              "public_backup": "Preparation documentation/tools are in the parent workspace; publication not performed by this script.",
              "scope": "Original APK only; no decompiled code or extracted media have been generated."}
    (ROOT / "evidence/backup.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(json.dumps(record, ensure_ascii=True))
    if not record["input_blob_matches"] or not record["working_tree_clean"]:
        raise RuntimeError("Private backup verification failed.")


if __name__ == "__main__":
    main()
