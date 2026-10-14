"""Export supported media; original serialized bundles remain authoritative."""
import collections
import gzip
import hashlib
import json
from pathlib import Path
import re
import time
import UnityPy
from PIL import Image, ImageDraw
from UnityPy.helpers.ResourceReader import get_resource_data
from common import PROJECT, LOCAL, write


def main():
    started = time.monotonic()
    objects = json.loads((LOCAL / "assets/objects.json").read_text("utf8"))
    candidates = {r["source_id"]: r for r in json.loads((LOCAL / "assets/input-containers.json").read_text("utf8"))}
    containers = json.loads((LOCAL / "assets/containers.json").read_text("utf8"))
    cab_sources = {c["file"]: c["source_id"] for c in containers}
    grouped = collections.defaultdict(list)
    for obj in objects:
        if obj["type"] in ["Texture2D", "Sprite", "Mesh", "AudioClip", "AnimationClip", "Avatar", "Shader", "ComputeShader", "Font"]:
            grouped[obj["source_id"]].append(obj)
    outputs, failures, totals = [], [], collections.Counter()
    output_root = LOCAL / "media"
    output_root.mkdir(exist_ok=True)
    for group_index, (source_id, rows) in enumerate(grouped.items()):
        env = UnityPy.load(str(LOCAL / candidates[source_id]["path"]))
        loaded = {source_id}
        pending = [c for c in containers if c["source_id"] == source_id]
        while pending:
            container = pending.pop()
            for external in container["externals"]:
                name = external.replace("\\", "/").rsplit("/", 1)[-1]
                dep = cab_sources.get(name)
                if dep and dep not in loaded:
                    env.load_file(str(LOCAL / candidates[dep]["path"]))
                    loaded.add(dep)
                    pending.extend(c for c in containers if c["source_id"] == dep)
        reader_map = {(o.assets_file.name, o.path_id): o for o in env.objects}
        for row in rows:
            obj = reader_map[row["file"], row["path_id"]]
            kind = row["type"]
            record = {"object_id": row["id"], "source_id": source_id, "path_id": row["path_id"], "type": kind, "name": row["name"], "files": []}
            prefix = source_id.replace(" ", "_") + "_" + str(row["path_id"]) + "_" + re.sub(r"[^\w.-]", "_", row["name"])[:70]
            folder = output_root / kind
            folder.mkdir(exist_ok=True)
            try:
                parsed = obj.read()
                targets = []
                if kind in ["Texture2D", "Sprite"]:
                    if kind == "Texture2D" and (parsed.m_Width == 0 or parsed.m_Height == 0 or (not parsed.image_data and not getattr(parsed, "m_StreamData", None))):
                        record["status"] = "empty-runtime-texture"
                        record.update(width=parsed.m_Width, height=parsed.m_Height)
                        outputs.append(record)
                        continue
                    image = parsed.image
                    destination = folder / (prefix + ".png")
                    image.save(destination)
                    with Image.open(destination) as checked:
                        checked.verify()
                    record.update(width=image.width, height=image.height)
                    targets.append(destination)
                elif kind == "Mesh":
                    text = parsed.export()
                    assert text and any(l.startswith("v ") for l in text.splitlines()), "No OBJ vertices"
                    destination = folder / (prefix + ".obj.gz")
                    destination.write_bytes(gzip.compress(text.encode("utf8"), mtime=0))
                    record.update(vertices=sum(l.startswith("v ") for l in text.splitlines()), faces=sum(l.startswith("f ") for l in text.splitlines()))
                    targets.append(destination)
                elif kind == "AudioClip":
                    raw = bytes(parsed.m_AudioData) if parsed.m_AudioData else get_resource_data(parsed.m_Resource.m_Source, obj.assets_file, parsed.m_Resource.m_Offset, parsed.m_Resource.m_Size)
                    original = folder / (prefix + ".native")
                    original.write_bytes(raw)
                    targets.append(original)
                    record.update(channels=parsed.m_Channels, frequency=parsed.m_Frequency, duration=parsed.m_Length, original_header=raw[:16].hex())
                    samples = parsed.samples
                    assert samples, "No decoded samples"
                    for sample_index, (name, data) in enumerate(samples.items()):
                        destination = folder / (prefix + f"_{sample_index}" + (Path(name).suffix or ".wav"))
                        destination.write_bytes(data)
                        targets.append(destination)
                elif kind in ["Shader", "ComputeShader"]:
                    original = folder / (prefix + ".serialized.bin.gz")
                    original.write_bytes(gzip.compress(obj.get_raw_data(), mtime=0))
                    targets.append(original)
                    if kind == "Shader":
                        try:
                            exported = parsed.export()
                            destination = folder / (prefix + ".shader.txt")
                            destination.write_text(exported, encoding="utf8")
                            targets.append(destination)
                        except Exception as error:
                            record["text_export_limitation"] = type(error).__name__ + ": " + str(error)
                else:
                    destination = folder / (prefix + ".serialized.bin.gz")
                    destination.write_bytes(gzip.compress(obj.get_raw_data(), mtime=0))
                    assert gzip.decompress(destination.read_bytes()) == obj.get_raw_data()
                    targets.append(destination)
                    record["format_scope"] = "Unity serialized object bytes; source bundles/type trees retain bindings and complete metadata, not an authoring FBX/TTF claim."
                for target in targets:
                    record["files"].append({"path": target.relative_to(LOCAL).as_posix(), "bytes": target.stat().st_size,
                        "sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
                record["status"] = "ok"
                totals[kind] += 1
            except Exception as error:
                record.update(status="failed", error=type(error).__name__ + ": " + str(error))
                failures.append(record.copy())
            outputs.append(record)
        if (group_index+1) % 50 == 0:
            print(f"media groups {group_index+1}/{len(grouped)}: {dict(totals)}, failures {len(failures)}", flush=True)
    write(output_root / "index.json", outputs)
    write(output_root / "errors.json", failures)
    selected = [r for r in outputs if r["type"] == "Sprite" and r["status"] == "ok"][:24]
    if not selected:
        selected = [r for r in outputs if r["type"] == "Texture2D" and r["status"] == "ok"][:24]
    if selected:
        sheet = Image.new("RGB", (1200, 240*((len(selected)+5)//6)), (32, 34, 39))
        draw = ImageDraw.Draw(sheet)
        for i, item in enumerate(selected):
            img = Image.open(LOCAL / item["files"][0]["path"]).convert("RGBA")
            img.thumbnail((184, 192))
            x = (i % 6)*200 + (200-img.width)//2
            y = (i // 6)*240 + 8
            sheet.paste(img, (x, y), img)
            draw.text(((i % 6)*200+8, (i // 6)*240+206), item["name"].encode("ascii", "replace").decode()[:28], fill="white")
            draw.text(((i % 6)*200+8, (i // 6)*240+222), item["source_id"] + " / " + str(item["path_id"])[:13], fill=(170, 180, 190))
        sheet.save(output_root / "contact-sheet.png")
        write(output_root / "contact-sheet-index.json", selected)
    summary = {"attempted_objects": len(outputs), "passed": dict(totals), "empty_runtime_textures": sum(r["status"] == "empty-runtime-texture" for r in outputs),
        "failed": len(failures), "exported_bytes": sum(f["bytes"] for r in outputs for f in r["files"]), "seconds": round(time.monotonic()-started, 2),
        "scope": "PNG images/sprites, static OBJ meshes, decoded audio, raw serialized animations/avatars/shaders/fonts. Original containers retain skinning/materials/scene transforms; no gameplay or listening claim."}
    write(PROJECT / "evidence/media-export.json", summary)
    print(json.dumps(summary, ensure_ascii=True), flush=True)


if __name__ == "__main__":
    main()
