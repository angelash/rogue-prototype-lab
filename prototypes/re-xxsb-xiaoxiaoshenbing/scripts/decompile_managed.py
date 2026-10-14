"""Decompile every recovered managed image and keep all tool diagnostics."""
import json
from pathlib import Path
import shutil
import subprocess
import time
from common import PROJECT, LOCAL, write


def main():
    exe = LOCAL / "tools/ilspy/ilspycmd.exe"
    if not exe.is_file():
        raise FileNotFoundError(f"Missing recorded ILSpy tool cache: {exe}")
    records = json.loads((LOCAL / "code/hotfix-index.json").read_text("utf8"))
    hotrefs = LOCAL / "code/decompile-hotfix-references"
    hotrefs.mkdir(parents=True, exist_ok=True)
    for p in (LOCAL / "il2cpp/arm64/DummyDll").glob("*.dll"):
        shutil.copyfile(p, hotrefs / p.name)
    for r in records:
        if r["kind"] == "hotfix DLL" and r["status"] == "ok":
            shutil.copyfile(LOCAL / r["canonical"], hotrefs / (r["assembly"] + ".dll"))
    results = []
    for r in records:
        if r["status"] != "ok":
            continue
        folder = LOCAL / "code/decompiled" / r["assembly"]
        refs = hotrefs if r["kind"] == "hotfix DLL" else LOCAL / "code/references"
        command = [str(exe), "--disable-updatecheck", "-p", "--nested-directories", "-r", str(refs), "-o", str(folder), str(LOCAL / r["canonical"])]
        started = time.monotonic()
        result = subprocess.run(command, capture_output=True, check=False)
        log = LOCAL / "logs" / ("ilspy-" + r["assembly"] + ".txt")
        log.write_bytes(result.stdout + result.stderr)
        source_files = list(folder.rglob("*.cs"))
        item = {"assembly": r["assembly"], "kind": r["kind"], "exit_code": result.returncode, "source_files": len(source_files),
            "bytes": sum(p.stat().st_size for p in source_files), "log_bytes": log.stat().st_size,
            "log": log.relative_to(LOCAL).as_posix(), "seconds": round(time.monotonic()-started, 2)}
        results.append(item)
        print(json.dumps(item, ensure_ascii=True), flush=True)
    write(PROJECT / "evidence/managed-decompilation.json", {"assemblies": len(results), "source_files": sum(r["source_files"] for r in results),
        "failed_exit_codes": sum(r["exit_code"] != 0 for r in results), "files": results,
        "scope": "Reconstructed C# for reading; all original managed bytes and IL retained. This does not prove recompilation or restore original author source."})


if __name__ == "__main__":
    main()
