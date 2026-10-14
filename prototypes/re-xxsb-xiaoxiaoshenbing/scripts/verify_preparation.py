"""Check preparation evidence cross-links and coverage, without reprocessing payloads."""
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def main():
    summary = json.loads((ROOT / "evidence/preparation.json").read_text("utf8"))
    files = json.loads((ROOT / "evidence/apk-files.json").read_text("utf8"))
    backup = json.loads((ROOT / "evidence/backup.json").read_text("utf8"))
    checks = {
        "identity": summary["android"]["label"] == "小小神兵" and summary["android"]["package"] == "com.gnml.xxsb.tt1",
        "version": summary["android"]["version_name"] == "0.1.3" and summary["android"]["version_code"] == "1000300",
        "copy_and_original": len({summary["apk_sha256"], summary["copy_sha256"], summary["original_sha256_after"], backup["input_blob_sha256"]}) == 1,
        "entry_coverage": len(files) == summary["zip_entries"] == 1488,
        "size_coverage": sum(x["size"] for x in files) == summary["uncompressed_bytes"] == 370964058,
        "archive_checks": all(x["size_and_crc_match"] and x["safe_path"] for x in files) and len({x["path"] for x in files}) == len(files),
        "hash_coverage": all(re.fullmatch("[0-9a-f]{64}", x["sha256"]) for x in files),
        "dex_coverage": len(summary["top_level_dex"]) == 35 and len(summary["other_dex"]) == 1,
        "library_coverage": len(summary["native_libraries"]) == 40 and summary["native_by_abi"] == {"arm64-v8a": 20, "armeabi-v7a": 20},
        "bundle_coverage": len(summary["bundle_paths"]) == 485,
        "private_backup": backup["input_blob_matches"] and backup["git_fsck_pass"] and backup["working_tree_clean"] and not backup["remotes"],
        "scope": summary["phase"] == backup["phase"] == "preparation-only" and "decompilation" in summary["not_performed"],
    }
    output = ROOT / "evidence/verification.json"
    missing_links = []
    for path in ROOT.rglob("*.md"):
        if "local" in path.relative_to(ROOT).parts:
            continue
        for link in re.findall(r"\]\(([^)]+)\)", path.read_text("utf8")):
            if "://" in link or link.startswith("#"):
                continue
            target = (path.parent / link.split("#")[0]).resolve()
            if target != output.resolve() and not target.exists():
                missing_links.append({"file": str(path.relative_to(ROOT)), "link": link})
    checks["document_links"] = not missing_links
    result = {"date": "2026-10-14", "phase": "preparation-only", "checks": checks,
              "passed": sum(checks.values()), "total": len(checks), "missing_links": missing_links,
              "scope": "Cross-file coverage, identity, preparation checks, local input backup and document links only; no gameplay/source/asset recovery claims."}
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(json.dumps(result, ensure_ascii=True))
    if not all(checks.values()):
        raise RuntimeError("Preparation evidence check failed.")


if __name__ == "__main__":
    main()
