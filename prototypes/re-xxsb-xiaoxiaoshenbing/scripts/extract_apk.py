"""Extract the authorized APK copy; never execute or modify the input."""
import json
from pathlib import PurePosixPath
import zipfile
from common import PROJECT, APK, write, sha256


def main():
    summary = json.loads((PROJECT / "evidence/preparation.json").read_text("utf8"))
    registered = json.loads((PROJECT / "evidence/apk-files.json").read_text("utf8"))
    source = PROJECT / summary["working_copy"]
    assert sha256(source) == summary["apk_sha256"]
    rows = []
    with zipfile.ZipFile(source) as z:
        for entry in registered:
            p = PurePosixPath(entry["path"])
            assert entry["safe_path"] and not p.is_absolute() and ".." not in p.parts
            output = APK.joinpath(*p.parts)
            if entry["is_directory"]:
                output.mkdir(parents=True, exist_ok=True)
                continue
            output.parent.mkdir(parents=True, exist_ok=True)
            if output.exists():
                assert sha256(output) == entry["sha256"], f"Existing output differs: {p}"
            else:
                data = z.read(entry["path"])
                with output.open("xb") as f:
                    f.write(data)
            actual = sha256(output)
            assert actual == entry["sha256"] and output.stat().st_size == entry["size"]
            rows.append({"path": entry["path"], "bytes": entry["size"], "sha256": actual})
    write(PROJECT / "evidence/extraction.json", {"date": "2026-10-14", "authorized": True,
        "files": len(rows), "bytes": sum(r["bytes"] for r in rows), "all_sha256_and_sizes_match": True,
        "original_not_modified": True, "output": "local/apk", "records": rows})
    print(json.dumps({"files": len(rows), "bytes": sum(r["bytes"] for r in rows), "all_hashes_match": True}))


if __name__ == "__main__":
    main()
