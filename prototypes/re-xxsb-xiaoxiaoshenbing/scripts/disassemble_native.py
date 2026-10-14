"""Retain complete ARM64 executable-section disassembly, without executing binaries."""
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import sys
import time
from common import PROJECT, LOCAL, APK, write, sha256

python_tools = LOCAL / "tools/python"
cached = PROJECT.parents[1] / "prototypes/re-t1-codename-rebirth/local/tools/python"
if not (python_tools / "capstone").exists():
    shutil.copytree(cached / "capstone", python_tools / "capstone")
sys.path.insert(0, str(python_tools))
from capstone import Cs, CS_ARCH_ARM64, CS_MODE_ARM, __version__


def main():
    started = time.monotonic()
    summary = json.loads((PROJECT / "evidence/native-summary.json").read_text("utf8"))
    methods = json.loads((LOCAL / "il2cpp/arm64/script.json").read_text("utf8"))["ScriptMethod"]
    named = {}
    for method in methods:
        named.setdefault(method["Address"], []).append(method["Name"])
    records = []
    output = LOCAL / "native/arm64"
    output.mkdir(parents=True, exist_ok=True)
    for lib in summary:
        if lib["bitness"] != 64:
            continue
        path = APK / lib["file"]
        metadata = json.loads((LOCAL / "elf/arm64-v8a" / (path.name + ".json")).read_text("utf8"))
        code_sections = [s for s in metadata["sections"] if s["flags"] & 4 and s["bytes"] and s["type"] != 8]
        binary = path.read_bytes()
        destination = output / (path.name + ".asm.gz")
        engine = Cs(CS_ARCH_ARM64, CS_MODE_ARM)
        engine.skipdata = True
        sections = []
        with gzip.open(destination, "wt", encoding="utf8", compresslevel=1) as stream:
            stream.write("; Linear ARM64 executable-section disassembly; symbol names are evidence, ranges do not restore author functions.\n")
            for section in code_sections:
                data = binary[section["offset"]:section["offset"]+section["bytes"]]
                stream.write(f"\n; SECTION {section['name']} VA=0x{section['address']:x} FILE=0x{section['offset']:x} BYTES={section['bytes']}\n")
                consumed = instructions = skipbytes = labels = 0
                for address, size, mnemonic, operands in engine.disasm_lite(data, section["address"]):
                    if path.name == "libil2cpp.so" and address in named:
                        stream.write("; " + " | ".join(named[address]) + "\n")
                        labels += len(named[address])
                    stream.write(f"{address:016x}  {mnemonic} {operands}\n")
                    consumed += size
                    if mnemonic == ".byte":
                        skipbytes += size
                    else:
                        instructions += 1
                assert consumed == len(data), (path.name, section["name"], consumed, len(data))
                sections.append({"name": section["name"], "virtual_address": section["address"], "file_offset": section["offset"],
                    "bytes": len(data), "disassembled_bytes": consumed, "instruction_count": instructions,
                    "skip_data_bytes": skipbytes, "mapped_IL2CPP_names": labels})
        item = {"file": lib["file"], "input_sha256": sha256(path), "output": destination.relative_to(LOCAL).as_posix(),
            "output_sha256": sha256(destination), "output_bytes": destination.stat().st_size, "sections": sections}
        records.append(item)
        print(path.name, sum(s["instruction_count"] for s in sections), "instructions", flush=True)
    evidence = {"capstone": __version__, "libraries": len(records), "executable_sections": sum(len(r["sections"]) for r in records),
        "executable_bytes": sum(s["bytes"] for r in records for s in r["sections"]),
        "instruction_count": sum(s["instruction_count"] for r in records for s in r["sections"]),
        "skip_data_bytes": sum(s["skip_data_bytes"] for r in records for s in r["sections"]),
        "IL2CPP_method_names": len(methods), "unique_IL2CPP_addresses": len(named),
        "labeled_IL2CPP_method_names": sum(s["mapped_IL2CPP_names"] for r in records for s in r["sections"]),
        "files": records, "seconds": round(time.monotonic()-started, 2),
        "scope": "All ARM64 ELF executable sections, including unmapped ranges; linear machine instructions/data markers, not reconstructed C++ or complete high-level algorithms. ARMv7 originals and ELF metadata are separately preserved."}
    write(PROJECT / "evidence/native-disassembly.json", evidence)
    print(json.dumps({k: v for k, v in evidence.items() if k != "files"}, ensure_ascii=True))


if __name__ == "__main__":
    main()
