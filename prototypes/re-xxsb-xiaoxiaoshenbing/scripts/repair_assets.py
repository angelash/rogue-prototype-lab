"""Repair export bookkeeping and stripped base trees using same-build embedded trees."""
import collections
import hashlib
import json
from pathlib import Path
import sys
import UnityPy
from common import PROJECT, LOCAL, write
sys.path.insert(0, str(LOCAL / "tools/python"))
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.helpers.Tpk import get_typetree_node
from UnityPy.helpers.TypeTreeNode import TypeTreeNode
from analyze_assets import compact, pointers


def main():
    rows = json.loads((LOCAL / "assets/objects.json").read_text("utf8"))
    sources = {x["source_id"]: x for x in json.loads((LOCAL / "assets/input-containers.json").read_text("utf8"))}
    containers = json.loads((LOCAL / "assets/containers.json").read_text("utf8"))
    ext = {x["file"]: x["externals"] for x in containers}
    scripts = {(x["file"], x["path_id"]): (x.get("script_assembly"), x.get("script_namespace"), x.get("script_class"))
        for x in rows if x["type"] == "MonoScript"}
    def identity(file, pointer):
        if not pointer:
            return None
        if pointer["m_FileID"] > 0:
            if pointer["m_FileID"] > len(ext[file]):
                return None
            file = ext[file][pointer["m_FileID"]-1].replace("\\", "/").rsplit("/", 1)[-1]
        return scripts.get((file, pointer["m_PathID"]))
    needed = set()
    source_rows = collections.defaultdict(list)
    for row in rows:
        if row["type"] == "TextAsset" or (row["type"] == "MonoBehaviour" and (row["tree_status"] != "ok" or not identity(row["file"], row.get("script_pointer")))):
            source_rows[row["source_id"]].append(row)
            if row.get("base_only"):
                key = identity(row["file"], row["base_only"].get("m_Script"))
                if key:
                    needed.add(key)
    embedded = {}
    grouped = collections.defaultdict(list)
    for row in rows:
        if row["type"] == "MonoBehaviour" and row["tree_status"] == "ok" and row["source_id"].startswith("b") and not row["source_id"].startswith("base"):
            key = identity(row["file"], row.get("script_pointer"))
            if key in needed:
                grouped[row["source_id"]].append((row, key))
    for source_id, entries in grouped.items():
        if all(key in embedded for _, key in entries):
            continue
        env = UnityPy.load(str(LOCAL / sources[source_id]["path"]))
        objects = {(o.assets_file.name, o.path_id): o for o in env.objects}
        for row, key in entries:
            if key not in embedded:
                embedded[key] = objects[row["file"], row["path_id"]]._get_typetree_node()
    generator = TypeTreeGenerator("2022.3.62f3")
    generator.load_local_dll_folder(str(LOCAL / "code/hotfix-references"))
    def normalize_root(nodes, version):
        base = get_typetree_node(114, version)
        names = {n.m_Name for n in base.m_Children}
        # The generator's root omits player-header alignment on m_Enabled.
        # Preserve the build's standard MonoBehaviour header and all custom fields.
        nodes.m_Children = list(base.m_Children) + [n for n in nodes.m_Children if n.m_Name not in names and n.m_Name not in {'m_ObjectHideFlags','m_EditorHideFlags','m_EditorClassIdentifier'}]
        for child in nodes.traverse():
            if child.m_Type == 'string' and child.m_Children and child.m_Children[0].m_Type == 'Array':
                values = child.m_Children[0].m_Children
                if len(values) == 2 and values[1].m_Type == 'string':
                    # API 0.0.10 labels string[] with scalar string type even
                    # though its children describe an array of strings.
                    child.m_Type = 'vector'
        # Recreate C-backed nodes so the accelerated reader rebuilds cached
        # type dispatch after a scalar-string node becomes a vector.
        return TypeTreeNode.from_list([{'m_Level': n.m_Level, 'm_Type': n.m_Type, 'm_Name': n.m_Name,
            'm_ByteSize': n.m_ByteSize, 'm_Version': n.m_Version, 'm_MetaFlag': n.m_MetaFlag} for n in nodes.traverse()])
    repairs, texts = [], []
    for source_id, entries in source_rows.items():
        path = LOCAL / sources[source_id]["path"]
        support = LOCAL / "normalized/base/globalgamemanagers.assets"
        env = UnityPy.load(str(path), *([str(support)] if path != support and source_id.startswith("base") else []))
        env.typetree_generator = generator
        objects = {(o.assets_file.name, o.path_id): o for o in env.objects}
        for row in entries:
            obj = objects[row["file"], row["path_id"]]
            result = {"id": row["id"], "type": row["type"], "prior_status": row["tree_status"]}
            try:
                if obj.type.name == "TextAsset":
                    tree = obj.read_typetree()
                    parsed = obj.read()
                    raw = parsed.m_Script.encode("utf8", "surrogateescape") if isinstance(parsed.m_Script, str) else bytes(parsed.m_Script)
                    output = LOCAL / "assets/text" / source_id / (str(obj.path_id) + (Path(parsed.m_Name).suffix or ".bin"))
                    output.parent.mkdir(parents=True, exist_ok=True)
                    output.write_bytes(raw)
                    texts.append({"object_id": row["id"], "source_id": source_id, "name": parsed.m_Name,
                        "payload": output.relative_to(LOCAL).as_posix(), "bytes": len(raw),
                        "sha256": hashlib.sha256(raw).hexdigest(), "header": raw[:16].hex(),
                        "addresses": [address for address, pointer in env.container.items() if pointer.path_id == obj.path_id]})
                    result["method"] = "TextAsset payload retained; corrected PPtr address bookkeeping"
                else:
                    base = obj.read_typetree(nodes=get_typetree_node(114, obj.version), check_read=False)
                    key = identity(row["file"], base.get("m_Script"))
                    result["script_identity"] = key
                    if key in embedded:
                        tree = obj.read_typetree(nodes=normalize_root(embedded[key], obj.version))
                        result["method"] = "same-build embedded type tree"
                    else:
                        asm, namespace, name = key
                        if key == ('UnityEngine','UnityEngine','GUISkin'):
                            # GUISkin is a native-backed MonoBehaviour; class 142
                            # is AssetBundle and must not be used as its layout.
                            raise NotImplementedError('Builtin GUISkin custom layout unavailable; preserve the original object bytes')
                        nodes = generator.get_nodes_up(asm, ".".join(s for s in [namespace, name] if s))
                        tree = obj.read_typetree(nodes=normalize_root(nodes, obj.version))
                        result["method"] = "generated full hotfix/native-declaration tree"
                    assert identity(row["file"], tree.get("m_Script")) == key, "Decoded script pointer differs from the independently read base"
                    row["script_pointer"] = tree.get("m_Script")
                    row["script_identity"] = key
                row["tree_status"] = "ok"
                row["name"] = tree.get("m_Name", "")
                row["tree"] = f"assets/trees/{source_id}/{row['file']}/{row['path_id']}_{row['type']}.json"
                write(LOCAL / row["tree"], compact(tree))
                row.pop("error", None)
                result["status"] = "ok"
            except Exception as error:
                row.update(tree_status="failed", error=type(error).__name__ + ": " + str(error))
                result.update(status="failed", error=row["error"])
            repairs.append(result)
    # Rebuild references from the current authoritative trees, excluding failed layouts.
    changed_ids = {r['id'] for entries in source_rows.values() for r in entries}
    edges = [e for e in json.loads((LOCAL / 'assets/edges.json').read_text('utf8')) if e['source'] not in changed_ids]
    known = {(r["file"], r["path_id"]) for r in rows}
    print('Repair pass complete; rebuilding references for', len(changed_ids), 'changed objects', flush=True)
    for row in rows:
        if row["tree_status"] != "ok" or row['id'] not in changed_ids:
            continue
        tree = json.loads((LOCAL / row["tree"]).read_text("utf8"))
        if row["type"] == "MonoBehaviour":
            row["script_identity"] = identity(row["file"], tree.get("m_Script"))
        for pointer in pointers(tree):
            target = row["file"] if pointer["file_id"] == 0 else (ext[row["file"]][pointer["file_id"]-1] if 0 < pointer["file_id"] <= len(ext[row["file"]]) else None)
            basename = target.replace("\\", "/").rsplit("/", 1)[-1] if target else None
            edges.append({"source": row["id"], **pointer, "target_file": target, "resolved_in_inventory": (basename, pointer["path_id"]) in known})
    write(LOCAL / "assets/objects.json", rows)
    write(LOCAL / "assets/text-assets.json", texts)
    write(LOCAL / "assets/edges.json", edges)
    write(LOCAL / "assets/repair-index.json", repairs)
    failures = [r for r in rows if r["tree_status"] != "ok"]
    summary = {"attempted": len(repairs), "passed": sum(r["status"] == "ok" for r in repairs), "failed": sum(r["status"] != "ok" for r in repairs),
        "embedded_types_reused": len(embedded), "total_objects": len(rows), "tree_success": sum(r["tree_status"] == "ok" for r in rows),
        "remaining_tree_failures": len(failures), "text_assets": len(texts), "reference_edges": len(edges),
        "unresolved_edges": sum(not r["resolved_in_inventory"] for r in edges),
        "failures": [{k: v for k, v in r.items() if k in ["id", "type", "error", "base_only"]} for r in failures]}
    write(PROJECT / "evidence/asset-repair.json", summary)
    print(json.dumps({k: v for k, v in summary.items() if k != "failures"}, ensure_ascii=True), flush=True)


if __name__ == "__main__":
    main()
