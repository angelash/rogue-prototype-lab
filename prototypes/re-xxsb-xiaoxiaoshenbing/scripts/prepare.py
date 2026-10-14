"""Copy and verify an APK; inspect only archive metadata and Android badging.

No payload extraction, decompilation, execution, or network access.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
from pathlib import Path, PurePosixPath
import platform
import re
import shutil
import subprocess
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[1]
EXPECTED_SHA256 = "4fb214bc2ed43a871ad2d35c4b2cae4cbe1125b649952795bd6a8e292f8fd07f"


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf8")


def run_aapt(aapt, command, apk, log):
    args = [str(aapt), *command, str(apk)]
    result = subprocess.run(args, capture_output=True, check=False)
    output = result.stdout.decode("utf8", errors="replace")
    log.write_bytes(result.stdout + result.stderr)
    if result.returncode:
        raise RuntimeError(f"aapt failed ({result.returncode}); see {log}")
    return output, {"command": args, "return_code": result.returncode, "log": log.relative_to(ROOT).as_posix()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--aapt", type=Path, default=Path("D:/androidSdk/build-tools/34.0.0/aapt.exe"))
    args = parser.parse_args()
    original = args.apk.resolve(strict=True)
    aapt = args.aapt.resolve(strict=True)
    digest_before = sha256(original)
    if digest_before != EXPECTED_SHA256:
        raise ValueError("Input differs from the registered APK; register it as a new target.")
    local = ROOT / "local"
    (local / "input").mkdir(parents=True, exist_ok=True)
    (local / "logs").mkdir(parents=True, exist_ok=True)
    copy = local / "input" / original.name
    if copy.exists():
        if sha256(copy) != digest_before:
            raise ValueError("Existing copy differs; refusing to overwrite it.")
    else:
        with original.open("rb") as source, copy.open("xb") as destination:
            shutil.copyfileobj(source, destination, 1024 * 1024)
    digest_copy = sha256(copy)
    entries, failures = [], []
    with zipfile.ZipFile(copy) as archive:
        for item in archive.infolist():
            digest = hashlib.sha256()
            crc = 0
            size = 0
            with archive.open(item) as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    size += len(block)
                    crc = zlib.crc32(block, crc)
                    digest.update(block)
            path = PurePosixPath(item.filename)
            safe = not (path.is_absolute() or ".." in path.parts or "\\" in item.filename or ":" in item.filename)
            valid = size == item.file_size and (crc & 0xffffffff) == item.CRC
            entry = {"path": item.filename, "size": size, "compressed_size": item.compress_size,
                     "crc32": f"{item.CRC:08x}", "sha256": digest.hexdigest(),
                     "compression": item.compress_type, "is_directory": item.is_dir(),
                     "safe_path": safe, "size_and_crc_match": valid}
            entries.append(entry)
            if not valid or not safe:
                failures.append(item.filename)
    names = [x["path"] for x in entries]
    duplicate_names = [n for n, count in collections.Counter(names).items() if count > 1]
    badging, badging_command = run_aapt(aapt, ["dump", "badging"], copy, local / "logs" / "aapt-badging.txt")
    permissions_text, permissions_command = run_aapt(aapt, ["dump", "permissions"], copy, local / "logs" / "aapt-permissions.txt")
    package = re.search(r"package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'", badging)
    label = re.search(r"^application-label:'([^']+)'", badging, re.M)
    main_activity = re.search(r"launchable-activity: name='([^']+)'", badging)
    if not package or not label or not main_activity:
        raise ValueError("Badging identity fields not found.")
    version = subprocess.run([str(aapt), "version"], capture_output=True, check=True).stdout.decode("utf8", errors="replace").strip()
    identity = {"package": package[1], "version_code": package[2], "version_name": package[3], "label": label[1],
                "main_activity": main_activity[1], "debuggable": "application-debuggable" in badging,
                "min_sdk": re.search(r"sdkVersion:'([^']+)'", badging)[1],
                "target_sdk": re.search(r"targetSdkVersion:'([^']+)'", badging)[1],
                "permissions": re.findall(r"uses-permission: name='([^']+)'", permissions_text)}
    libraries = [n for n in names if n.endswith(".so")]
    markers = [n for n in names if n.endswith(("global-metadata.dat", "libil2cpp.so", "libunity.so", "libmain.so"))]
    summary = {"schema": 1, "date": "2026-10-14", "timezone": "Asia/Shanghai", "phase": "preparation-only",
               "authorization": "Target ownership/decompilation/asset-extraction scope requested; no reply received.",
               "source": str(original), "working_copy": copy.relative_to(ROOT).as_posix(),
               "apk_bytes": original.stat().st_size, "apk_sha256": digest_before, "copy_sha256": digest_copy,
               "original_sha256_after": sha256(original), "original_unchanged": sha256(original) == digest_before,
               "copy_matches": digest_before == digest_copy, "android": identity,
               "backend": "Unity IL2CPP" if any(n.endswith("global-metadata.dat") for n in markers) and any(n.endswith("libil2cpp.so") for n in markers) else "unknown",
               "engine_markers": markers, "unity_exact_version": "not inspected",
               "zip_entries": len(entries), "uncompressed_bytes": sum(x["size"] for x in entries),
               "directory_entries": sum(x["is_directory"] for x in entries),
               "top_level_dex": [n for n in names if re.fullmatch(r"classes\d*\.dex", n)],
               "other_dex": [n for n in names if n.endswith(".dex") and not re.fullmatch(r"classes\d*\.dex", n)],
               "native_libraries": libraries, "native_by_abi": dict(collections.Counter(n.split("/")[1] for n in libraries)),
               "bundle_paths": [n for n in names if n.endswith(".bundle")],
               "manifest_paths": [n for n in names if n.startswith("assets/") and "manifest" in n.lower()],
               "extensions": dict(collections.Counter(PurePosixPath(n).suffix.lower() or "[no suffix]" for n in names)),
               "top_directories": dict(collections.Counter(n.split("/")[0] for n in names)),
               "verification": {"all_entries_streamed": len(entries), "failures": failures, "duplicates": duplicate_names,
                                "all_size_crc_and_safe_paths_pass": not failures and not duplicate_names},
               "tools": {"python": platform.python_version(), "aapt": str(aapt), "aapt_version": version,
                         "aapt_sha256": sha256(aapt)}, "commands": [badging_command, permissions_command],
               "not_performed": ["payload extraction", "decompilation", "IL2CPP metadata recovery", "asset object parsing",
                                 "APK install/run", "network access", "APK modification", "original-project recovery"]}
    write_json(ROOT / "evidence" / "apk-files.json", entries)
    write_json(ROOT / "evidence" / "preparation.json", summary)
    print(json.dumps({"label": identity["label"], "package": identity["package"], "apk_bytes": summary["apk_bytes"],
                      "entries": len(entries), "bundles": len(summary["bundle_paths"]), "top_level_dex": len(summary["top_level_dex"]),
                      "other_dex": len(summary["other_dex"]), "libraries": len(libraries),
                      "copy_matches": summary["copy_matches"], "original_unchanged": summary["original_unchanged"],
                      "verification": summary["verification"]}, ensure_ascii=True))
    if not summary["copy_matches"] or not summary["original_unchanged"] or not summary["verification"]["all_size_crc_and_safe_paths_pass"]:
        raise RuntimeError("Preparation verification failed.")


if __name__ == "__main__":
    main()
