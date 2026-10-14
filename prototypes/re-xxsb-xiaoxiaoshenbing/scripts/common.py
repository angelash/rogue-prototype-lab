import hashlib
import json
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
LOCAL = PROJECT / "local"
APK = LOCAL / "apk"


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=True, indent=2) + "\n", encoding="utf8", newline="\n")


def sha256(path):
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()
