"""Inventory all Unity containers, full type trees, references and TextAsset bytes."""
import collections
import hashlib
import json
from pathlib import Path
import shutil
import sys
import time
from common import PROJECT, LOCAL, APK, write, sha256

python_tools = LOCAL / "tools/python"
if not (python_tools / "TypeTreeGeneratorAPI").exists():
    cached = PROJECT.parents[1] / "prototypes/re-t1-codename-rebirth/local/tools/python"
    python_tools.mkdir(parents=True, exist_ok=True)
    shutil.copytree(cached / "TypeTreeGeneratorAPI", python_tools / "TypeTreeGeneratorAPI")
sys.path.insert(0, str(python_tools))
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.helpers.Tpk import get_typetree_node


def compact(value):
    if isinstance(value, bytes):
        return {"$bytes": len(value), "sha256": hashlib.sha256(value).hexdigest()}
    if isinstance(value, dict):
        return {str(k): compact(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [compact(v) for v in value]
    return value


def pointers(value, path=""):
    if isinstance(value, dict):
        if "m_FileID" in value and "m_PathID" in value:
            if value["m_PathID"]:
                yield {"property": path, "file_id": value["m_FileID"], "path_id": value["m_PathID"]}
        else:
            for key, child in value.items():
                yield from pointers(child, path + "." + str(key))
    elif isinstance(value, (list, tuple)):
        for index, child in enumerate(value):
            yield from pointers(child, path + f"[{index}]")


def main():
    started = time.monotonic()
    references = LOCAL / "code/hotfix-references"
    references.mkdir(parents=True, exist_ok=True)
    for source in (LOCAL / "il2cpp/arm64/DummyDll").glob("*.dll"):
        shutil.copyfile(source, references / source.name)
    hotfix = json.loads((LOCAL / "code/hotfix-index.json").read_text("utf8"))
    for record in hotfix:
        if record["kind"] == "hotfix DLL" and record["status"] == "ok":
            shutil.copyfile(LOCAL / record["canonical"], references / (record["assembly"] + ".dll"))
    generator = TypeTreeGenerator("2022.3.62f3")
    generator.load_local_dll_folder(str(references))
    manifests = json.loads((LOCAL / "manifest-bundles.json").read_text("utf8"))
    bundle_ids = {b["filename"]: b["id"] for b in manifests}
    candidates = [("b" + str(bundle_ids[p.name]).zfill(4) if p.name in bundle_ids else "extra-" + p.stem[:16], p)
        for p in sorted((APK / "assets").glob("*.bundle"))]
    data = APK / "assets/bin/Data"
    merged = LOCAL / "normalized/base"
    merged.mkdir(parents=True, exist_ok=True)
    for p in sorted(data.iterdir()):
        if not p.is_file() or p.name in ["boot.config", "unity_app_guid"] or p.suffix == ".json":
            continue
        if ".split" in p.name:
            if not p.name.endswith(".split0"):
                continue
            name = p.name.rsplit(".split", 1)[0]
            splits = sorted(data.glob(name + ".split*"), key=lambda x: int(x.name.rsplit(".split", 1)[1]))
            target = merged / name
            target.write_bytes(b"".join(x.read_bytes() for x in splits))
            candidates.append(("base-" + name, target))
        else:
            candidates.append(("base-" + p.name, p))
    for p in sorted((data / "Resources").glob("*")):
        if p.is_file():
            candidates.append(("base-res-" + p.name, p))
    objects, containers, texts, failures, edges, candidate_records = [], [], [], [], [], []
    (LOCAL / "assets/trees").mkdir(parents=True, exist_ok=True)
    for ci, (source_id, path) in enumerate(candidates):
        relative = path.relative_to(LOCAL).as_posix()
        row = {"source_id": source_id, "path": relative, "bytes": path.stat().st_size, "sha256": sha256(path)}
        try:
            env = UnityPy.load(str(path))
            env.typetree_generator = generator
            assets = list(env.assets)
            row.update(status="ok" if assets else "not-serialized-container", serialized_files=len(assets), objects=len(list(env.objects)))
            for asset in assets:
                container = {"source_id": source_id, "file": asset.name, "unity_version": asset.unity_version,
                    "objects": len(asset.objects), "externals": [x.path for x in asset.externals]}
                containers.append(container)
                for obj in asset.objects.values():
                    identifier = source_id + "/" + asset.name + "/" + str(obj.path_id)
                    record = {"id": identifier, "source_id": source_id, "file": asset.name, "path_id": obj.path_id,
                        "type": obj.type.name, "bytes": obj.byte_size, "name": "", "tree_status": "pending"}
                    try:
                        tree = obj.read_typetree()
                        record.update(tree_status="ok", name=tree.get("m_Name", ""))
                        record["tree"] = f"assets/trees/{source_id}/{asset.name}/{obj.path_id}_{obj.type.name}.json"
                        write(LOCAL / record["tree"], compact(tree))
                        for pointer in pointers(tree):
                            target_file = asset.name if pointer["file_id"] == 0 else (
                                asset.externals[pointer["file_id"]-1].path if 0 < pointer["file_id"] <= len(asset.externals) else None)
                            edges.append({"source": identifier, **pointer, "target_file": target_file})
                        if obj.type.name == "MonoScript":
                            record.update(script_class=tree.get("m_ClassName"), script_namespace=tree.get("m_Namespace"),
                                script_assembly=tree.get("m_AssemblyName"))
                        if obj.type.name == "MonoBehaviour":
                            record["script_pointer"] = tree.get("m_Script")
                        if obj.type.name == "TextAsset":
                            parsed = obj.read()
                            raw = parsed.m_Script.encode("utf8", "surrogateescape") if isinstance(parsed.m_Script, str) else bytes(parsed.m_Script)
                            suffix = Path(parsed.m_Name).suffix or ".bin"
                            output = LOCAL / "assets/text" / source_id / (str(obj.path_id) + suffix)
                            output.parent.mkdir(parents=True, exist_ok=True)
                            output.write_bytes(raw)
                            text_record = {"object_id": identifier, "source_id": source_id, "name": parsed.m_Name,
                                "payload": output.relative_to(LOCAL).as_posix(), "bytes": len(raw),
                                "sha256": hashlib.sha256(raw).hexdigest(), "header": raw[:16].hex(), "addresses": []}
                            for address, pointer in env.container.items():
                                if pointer.path_id == obj.path_id:
                                    text_record["addresses"].append(address)
                            texts.append(text_record)
                    except Exception as error:
                        record.update(tree_status="failed", error=type(error).__name__ + ": " + str(error))
                        failures.append({"id": identifier, "type": obj.type.name, "error": record["error"]})
                        if obj.type.name == "MonoBehaviour":
                            try:
                                record["base_only"] = compact(obj.read_typetree(nodes=get_typetree_node(114, obj.version), check_read=False))
                            except Exception as fallback:
                                record["base_error"] = str(fallback)
                    objects.append(record)
        except Exception as error:
            row.update(status="failed", error=type(error).__name__ + ": " + str(error))
            failures.append({"source_id": source_id, "stage": "container", "error": row["error"]})
        candidate_records.append(row)
        if (ci+1) % 50 == 0:
            print(f"containers {ci+1}/{len(candidates)}, objects {len(objects)}, failures {len(failures)}", flush=True)
    lookup = {(r["file"], r["path_id"]) for r in objects}
    for edge in edges:
        filename = edge["target_file"].replace("\\", "/").rsplit("/", 1)[-1] if edge["target_file"] else None
        edge["resolved_in_inventory"] = (filename, edge["path_id"]) in lookup
    write(LOCAL / "assets/objects.json", objects)
    write(LOCAL / "assets/containers.json", containers)
    write(LOCAL / "assets/text-assets.json", texts)
    write(LOCAL / "assets/edges.json", edges)
    write(LOCAL / "assets/errors.json", failures)
    write(LOCAL / "assets/input-containers.json", candidate_records)
    summary = {"tool": "UnityPy " + UnityPy.__version__, "generator": "TypeTreeGeneratorAPI, full hotfix + IL2CPP declaration references",
        "input_candidates": len(candidates), "bundle_inputs": sum(p.suffix == ".bundle" for _, p in candidates),
        "bundle_containers_ok": sum(r["status"] == "ok" and r["source_id"].startswith(("b", "extra")) and not r["source_id"].startswith("base-") for r in candidate_records),
        "serialized_files": len(containers), "objects": len(objects), "types": dict(collections.Counter(r["type"] for r in objects).most_common()),
        "unity_versions": dict(collections.Counter(r["unity_version"] for r in containers)),
        "tree_success": sum(r["tree_status"] == "ok" for r in objects), "tree_failures": sum(r["tree_status"] == "failed" for r in objects),
        "text_assets": len(texts), "reference_edges": len(edges), "resolved_edges": sum(e["resolved_in_inventory"] for e in edges),
        "failed_inputs": [r for r in candidate_records if r["status"] == "failed"], "failures": failures,
        "seconds": round(time.monotonic()-started, 2), "scope": "All APK bundles and base serialized containers; type trees and payload bytes, no original Unity project reconstruction or runtime claim."}
    write(PROJECT / "evidence/asset-summary.json", summary)
    print(json.dumps({k: v for k, v in summary.items() if k not in ["failures", "failed_inputs"]}, ensure_ascii=True))


if __name__ == "__main__":
    main()
