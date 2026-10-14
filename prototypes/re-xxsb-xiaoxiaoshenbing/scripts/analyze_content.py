"""Decode all merged QConfig@v2 tables from the observed managed reader schema."""
import collections
import hashlib
import json
from pathlib import Path
import UnityPy
from common import PROJECT, LOCAL, APK, write


def main():
    manifest = json.loads((APK / "assets/PackageManifest_DefaultPackage_35156.json").read_text("utf8"))
    asset = next(a for a in manifest["AssetList"] if a["Address"] == "config_0")
    bundle = manifest["BundleList"][asset["BundleID"]]
    stem, ext = bundle["BundleName"].rsplit(".", 1)
    source = APK / "assets" / f"{stem}_{bundle['FileHash']}.{ext}"
    obj = next(o for o in UnityPy.load(str(source)).objects if o.type.name == "TextAsset")
    parsed = obj.read()
    raw = parsed.m_Script.encode("utf8", "surrogateescape") if isinstance(parsed.m_Script, str) else bytes(parsed.m_Script)
    out = LOCAL / "content/tables"
    out.mkdir(parents=True, exist_ok=True)
    (LOCAL / "content/config_0.original.json").write_bytes(raw)
    merged = json.loads(raw)
    schemas, failures, resolutions = [], [], []
    for name, data in merged.items():
        try:
            assert name.startswith("data_") and "/" not in name
            if isinstance(data, dict):
                table = {"name": name, "kind": "KV", "columns": [], "row_count": len(data), "rows": data}
            elif isinstance(data, list) and len(data) >= 2 and isinstance(data[0], dict) and isinstance(data[1], dict):
                assert all(v == {} for v in data[:-1]) and data[-1] == ['QConfig@v2', [], [], []]
                table = {'name': name, 'kind': 'EmptyHeaderPlaceholder', 'columns': [], 'row_count': 0, 'rows': [],
                    'original_empty_objects': len(data)-1, 'runtime_parse_behavior': 'ParseFromJsonArray returns without rows because the two headers are objects, not arrays; all original empty objects preserved.'}
            else:
                assert isinstance(data, list) and len(data) >= 2 and isinstance(data[0], list) and isinstance(data[1], list)
                names, types = data[:2]
                assert len(names) == len(types) and len(names) == len(set(names))
                tail = data[-1]
                v2 = isinstance(tail, list) and bool(tail) and tail[0] == "QConfig@v2"
                pools = tail[1:4] if v2 else [[], [], []]
                assert len(pools) == 3 and all(isinstance(p, list) for p in pools)
                scalar = {"string", "datetime", "s", "t", "int", "float", "i", "f", "long", "l", "double", "d"}
                type_masks = [0 if t.split("@")[0] in scalar else 0x30000000 for t in types]
                rows = []
                key_columns = [i for i, t in enumerate(types) if t.split("@")[1:2] == ["ukey"]]
                assert len(key_columns) <= 1
                key_index = key_columns[0] if key_columns else None
                for row_index, values in enumerate(data[2:-1] if v2 else data[2:]):
                    assert isinstance(values, list) and len(values) <= len(names)
                    row = {}
                    def value_for(field, value):
                        index = names.index(field) if field in names else None
                        mask = type_masks[index] if index is not None else 0
                        if v2 and mask and isinstance(value, int) and not isinstance(value, bool):
                            if value == -1:
                                return None
                            pool_index = value ^ mask
                            assert 0 <= pool_index < len(pools[2]), (name, row_index, field, value)
                            resolutions.append({"table": name, "row": row_index, "column": field, "pool": "object", "index": pool_index})
                            return pools[2][pool_index]
                        return value
                    for column_index, value in enumerate(values):
                        if isinstance(value, dict):
                            for field, child in value.items():
                                if child is not None:
                                    resolved = value_for(field, child)
                                    if resolved is not None:
                                        row[field] = resolved
                        elif value is not None:
                            resolved = value_for(names[column_index], value)
                            if resolved is not None:
                                row[names[column_index]] = resolved
                    rows.append(row)
                keys = [row.get(names[key_index]) for row in rows] if key_index is not None else []
                assert not keys or (None not in keys and len(set(str(k) for k in keys)) == len(keys)), (name, "duplicate/missing primary key")
                table = {"name": name, "kind": "Rows", "columns": [{"name": n, "type": t} for n, t in zip(names, types)],
                    "key_column": names[key_index] if key_index is not None else None, "row_count": len(rows), "rows": rows,
                    "format": "QConfig@v2" if v2 else "unpooled array", "pool_counts": [len(p) for p in pools]}
            write(out / (name + ".json"), table)
            schemas.append({k: v for k, v in table.items() if k != "rows"})
        except Exception as error:
            failures.append({"table": name, "error": type(error).__name__ + ": " + str(error)})
    write(LOCAL / "content/schemas.json", schemas)
    write(LOCAL / "content/pool-resolution.json", resolutions)
    write(LOCAL / "content/errors.json", failures)
    languages = next((s for s in schemas if s["name"] == "data_language"), None)
    language = json.loads((out / "data_language.json").read_text("utf8"))["rows"]
    language_map = {str(r["id"]): r["content"] for r in language}
    write(LOCAL / "content/language-map.json", language_map)
    summary = {"source_bytes": len(raw), "source_sha256": hashlib.sha256(raw).hexdigest(), "merged_entries": len(merged),
        "decoded_tables": len(schemas), "row_tables": sum(s["kind"] == "Rows" for s in schemas), "KV_tables": sum(s["kind"] == "KV" for s in schemas),
        "empty_header_placeholders": [s for s in schemas if s['kind']=='EmptyHeaderPlaceholder'],
        "row_records": sum(s["row_count"] for s in schemas if s["kind"] == "Rows"), "KV_entries": sum(s["row_count"] for s in schemas if s["kind"] == "KV"),
        "columns": sum(len(s["columns"]) for s in schemas), "object_pool_references_resolved": len(resolutions),
        "all_primary_keys_unique": not failures, "language_rows": languages["row_count"] if languages else 0,
        "table_rows": {s["name"]: s["row_count"] for s in schemas}, "failures": failures,
        "scope": "All client APK merged-table contents; follows JsonConfigDataSource.ParseFromJsonArray/ParseOneRow/GetResolvedValue. Null omissions preserved; no server config or live update claims."}
    write(PROJECT / "evidence/content-summary.json", summary)
    print(json.dumps({k: v for k, v in summary.items() if k != "table_rows"}, ensure_ascii=True))
    if failures:
        raise RuntimeError("Some configuration tables remain unparsed.")


if __name__ == "__main__":
    main()
