"""Extract all listed hotfix/AOT metadata TextAssets, preserving original bytes."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import UnityPy
from common import PROJECT, LOCAL, APK, write, sha256


def main():
    repo = PROJECT.parents[1]
    tools = LOCAL / "tools/metadata"
    if not tools.exists():
        shutil.copytree(repo / "prototypes/re-t1-codename-rebirth/local/tools/metadata", tools)
    manifest = json.loads((APK / "assets/PackageManifest_DefaultPackage_35156.json").read_text("utf8"))
    output = LOCAL / "code/input-hotfixes"
    index = LOCAL / "code/index"
    references = LOCAL / "code/references"
    for folder in [output, index, references]:
        folder.mkdir(parents=True, exist_ok=True)
    for source in (LOCAL / "il2cpp/arm64/DummyDll").glob("*.dll"):
        shutil.copyfile(source, references / source.name)
    records = []
    for asset in manifest["AssetList"]:
        if "/Hotfixes/" not in asset["AssetPath"]:
            continue
        bundle = manifest["BundleList"][asset["BundleID"]]
        stem, ext = bundle["BundleName"].rsplit(".", 1)
        filename = f"{stem}_{bundle['FileHash']}.{ext}"
        env = UnityPy.load(str(APK / "assets" / filename))
        text_assets = [o for o in env.objects if o.type.name == "TextAsset"]
        assert len(text_assets) == 1
        obj = text_assets[0]
        text = obj.read()
        raw = text.m_Script.encode("utf8", "surrogateescape") if isinstance(text.m_Script, str) else bytes(text.m_Script)
        rawpath = output / (Path(asset["AssetPath"]).name)
        rawpath.write_bytes(raw)
        record = {"address": asset["Address"], "asset_path": asset["AssetPath"], "bundle_id": asset["BundleID"],
            "bundle": filename, "object_path_id": obj.path_id, "object_name": text.m_Name,
            "bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest(), "header": raw[:16].hex(),
            "input": rawpath.relative_to(LOCAL).as_posix(), "kind": "AOT supplement" if ".aotmeta" in rawpath.name else "hotfix DLL"}
        if not raw.startswith(b"MZ"):
            record["status"] = "nonstandard-payload"
            records.append(record)
            continue
        outindex = index / (asset["Address"] + ".json")
        command = [str(tools / "MetadataIndex.exe"), str(rawpath), str(outindex)]
        result = subprocess.run(command, capture_output=True, check=False)
        (LOCAL / "logs" / ("metadata-" + asset["Address"] + ".txt")).write_bytes(result.stdout + result.stderr)
        if result.returncode:
            record.update(status="metadata-index-failed", exit_code=result.returncode)
        else:
            metadata = json.loads(outindex.read_text("utf8"))
            canonical = references / (metadata["assembly"] + ".dll")
            canonical.write_bytes(raw)
            methods = [m for t in metadata["types"] for m in t["methods"]]
            record.update(status="ok", assembly=metadata["assembly"], types=metadata["typeCount"],
                methods=metadata["methodCount"], fields=metadata["fieldCount"],
                il_method_bodies=sum(m["rva"] != 0 for m in methods), il_bytes=sum(m["ilLength"] for m in methods),
                il_failures=len(metadata["failures"]), index=outindex.relative_to(LOCAL).as_posix(),
                canonical=canonical.relative_to(LOCAL).as_posix(), references=metadata["references"])
        records.append(record)
        print(asset["Address"], record["status"], record.get("types"), record.get("il_method_bodies"), flush=True)
    write(LOCAL / "code/hotfix-index.json", records)
    summary = {"payloads": len(records), "standard_PE": sum(r["header"].startswith("4d5a") for r in records),
        "hotfix_DLLs": sum(r["kind"] == "hotfix DLL" for r in records), "AOT_supplements": sum(r["kind"] == "AOT supplement" for r in records),
        "successful_metadata": sum(r["status"] == "ok" for r in records), "types": sum(r.get("types", 0) for r in records),
        "methods": sum(r.get("methods", 0) for r in records), "IL_method_bodies": sum(r.get("il_method_bodies", 0) for r in records),
        "IL_bytes": sum(r.get("il_bytes", 0) for r in records), "IL_decode_failures": sum(r.get("il_failures", 0) for r in records),
        "files": [{k: v for k, v in r.items() if k not in ["references"]} for r in records],
        "scope": "Actual original managed images and IL indexes; AOT supplements may retain only partial IL, independent from IL2CPP DummyDll declarations."}
    write(PROJECT / "evidence/managed-recovery.json", summary)
    print(json.dumps({k: v for k, v in summary.items() if k != "files"}, ensure_ascii=True))


if __name__ == "__main__":
    main()
