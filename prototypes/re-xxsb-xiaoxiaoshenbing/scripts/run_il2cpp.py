"""Recover IL2CPP declarations/address maps from the authorized static files."""
import json
import argparse
from pathlib import Path
import shutil
import subprocess
from common import PROJECT, LOCAL, APK, write, sha256


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--abi',choices=['arm64-v8a','armeabi-v7a'],default='arm64-v8a');options=parser.parse_args()
    repo = PROJECT.parents[1]
    source = repo / "prototypes/re-t1-codename-rebirth/local/tools/il2cppdumper"
    target = LOCAL / "tools/il2cppdumper"
    if not target.exists():
        shutil.copytree(source, target)
    config = json.loads((target / "config.json").read_text("utf8"))
    config["RequireAnyKey"] = False
    write(target / "config.json", config)
    output = LOCAL / ('il2cpp/arm64' if options.abi=='arm64-v8a' else 'il2cpp/armv7')
    output.mkdir(parents=True, exist_ok=True)
    binary = APK / 'lib' / options.abi / 'libil2cpp.so'
    metadata = APK / "assets/bin/Data/Managed/Metadata/global-metadata.dat"
    exe = target / "Il2CppDumper.exe"
    args = [str(exe), str(binary), str(metadata), str(output)]
    result = subprocess.run(args, capture_output=True, input=b"\n", check=False)
    log = LOCAL / ('logs/il2cpp-arm64.txt' if options.abi=='arm64-v8a' else 'logs/il2cpp-armv7.txt')
    log.write_bytes(result.stdout + result.stderr)
    files = list((output / "DummyDll").glob("*.dll"))
    evidence = {"date": "2026-10-14", "command": args, "exit_code": result.returncode,
        "tool_sha256": sha256(exe), "tool_source": "Existing local Il2CppDumper from the T1 tooling directory; copied without game data",
        "binary_sha256": sha256(binary), "metadata_sha256": sha256(metadata),
        "metadata_header_version": int.from_bytes(metadata.read_bytes()[4:8], "little"),
        "dummy_assemblies": len(files), "dump_cs_exists": (output / "dump.cs").exists(),
        "script_json_exists": (output / "script.json").exists(), "log": log.relative_to(PROJECT).as_posix(),
        "scope": "IL2CPP declarations and native addresses; DummyDll does not recover original method IL or author source."}
    write(PROJECT / ('evidence/il2cpp-recovery.json' if options.abi=='arm64-v8a' else 'evidence/il2cpp-recovery-armv7.json'), evidence)
    print(json.dumps(evidence, ensure_ascii=True))
    if not files or not evidence["dump_cs_exists"]:
        raise RuntimeError("IL2CPP declarations not recovered; inspect the retained log.")


if __name__ == "__main__":
    main()
