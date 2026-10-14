"""Full Android bytecode dumps, reconstructed Java and independent coverage checks."""
import json
from pathlib import Path
import re
import shutil
import subprocess
import time
from common import PROJECT, LOCAL, APK, write, sha256


def run(command, log):
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open("wb") as stream:
        result = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, check=False)
    return result.returncode


def main():
    repo = PROJECT.parents[1]
    source = repo / "prototypes/re-t1-codename-rebirth/local/tools/jadx"
    target = LOCAL / "tools/jadx"
    if not target.exists():
        shutil.copytree(source, target)
    jar = target / "lib/jadx-1.5.3-all.jar"
    java = Path("C:/Program Files/Java/jdk-17/bin/java.exe")
    sdk = Path("D:/androidSdk/build-tools/34.0.0")
    inputs = sorted(APK.glob("classes*.dex"))
    assert len(inputs) == 35
    normal_command = [str(java), "-Xmx3g", "-cp", str(jar), 'jadx.cli.JadxCLI', "--no-res", "-j", "4",
        "--respect-bytecode-access-modifiers", "--use-source-name-as-class-name-alias", "never", "--log-level", "warn",
        "-d", str(LOCAL / "android/java"), *[str(p) for p in inputs]]
    started = time.monotonic()
    normal_log = LOCAL / "logs/jadx-normal.txt"
    normal_exit = run(normal_command, normal_log)
    print("Normal Java reconstruction complete; exit", normal_exit, flush=True)
    expected = {r["file"]: r for r in json.loads((PROJECT / "evidence/dex-summary.json").read_text("utf8"))}
    details = []
    for dex in inputs:
        exp = expected[dex.name]
        output = LOCAL / "android/disassembly" / (dex.stem + ".txt")
        exit_code = run([str(sdk / "dexdump.exe"), "-d", "-f", str(dex)], output)
        assert exit_code == 0
        text = output.read_text("utf8", errors="replace")
        units = [int(x) for x in re.findall(r"insns size\s*:\s*(\d+) 16-bit code units", text)]
        count = len(re.findall(r"^Class #\d+\s+-", text, re.M))
        assert count == exp["header"]["classCount"] and len(units) == exp["methodBodies"] and sum(units) == exp["instructionUnits"]
        fallback_folder = LOCAL / "android/fallback" / dex.stem
        fallback_log = LOCAL / "logs" / ("jadx-fallback-" + dex.stem + ".txt")
        if count:
            fallback_command = [str(java), "-Xmx1g", "-cp", str(jar), 'jadx.cli.JadxCLI', "--no-res", "-j", "4", "--decompilation-mode", "fallback",
                "--no-inline-anonymous", "--no-move-inner-classes", "--no-debug-info", "--use-source-name-as-class-name-alias", "never",
                "--log-level", "error", "-d", str(fallback_folder), str(dex)]
            fallback_exit = run(fallback_command, fallback_log)
            fallback_status = "ok" if fallback_exit == 0 else "diagnostics"
        else:
            fallback_exit = None
            fallback_status = "empty-DEX"
        item = {"file": dex.name, "classes": count, "method_bodies": len(units), "instruction_units": sum(units),
            "sdk_counts_match_independent_parser": True, "disassembly_bytes": output.stat().st_size,
            "disassembly_sha256": sha256(output), "fallback_exit": fallback_exit, "fallback_status": fallback_status,
            "fallback_files": len(list(fallback_folder.rglob("*.java")))}
        details.append(item)
        print(dex.name, count, "classes", len(units), "methods", fallback_status, flush=True)
    original = next((LOCAL / "input").glob("*.apk"))
    manifest_log = LOCAL / "android/manifest-xmltree.txt"
    assert run([str(sdk / "aapt.exe"), "dump", "xmltree", str(original), "AndroidManifest.xml"], manifest_log) == 0
    full_manifest = manifest_log.read_text("utf8", errors="replace")
    components = []
    current = None
    for line in full_manifest.splitlines():
        element = re.search(r"E: (activity|service|receiver|provider|application|activity-alias)\b", line)
        if element:
            current = element[1]
        if current:
            name = re.search(r'A: android:name[^=]*="([^"]+)"', line)
            if name:
                components.append({"kind": current, "name": name[1]})
                current = None
    write(LOCAL / "android/components.json", components)
    normal_text = normal_log.read_text("utf8", errors="replace")
    diagnostics = [line for line in normal_text.splitlines() if "ERROR -" in line]
    write(LOCAL / "android/diagnostics.json", {"normal_exit": normal_exit, "errors": diagnostics, "full_log": "logs/jadx-normal.txt"})
    summary = {"date": "2026-10-14", "jadx": "1.5.3", "jadx_jar_sha256": sha256(jar), "java": str(java),
        "normal_exit": normal_exit, "normal_java_files": len(list((LOCAL / "android/java").rglob("*.java"))),
        "normal_error_lines": len(diagnostics), "DEX_disassembled": len(details), "classes": sum(r["classes"] for r in details),
        "method_bodies": sum(r["method_bodies"] for r in details), "instruction_units": sum(r["instruction_units"] for r in details),
        "fallback_java_files": sum(r["fallback_files"] for r in details), "all_sdk_counts_match": all(r["sdk_counts_match_independent_parser"] for r in details),
        "components": dict(__import__('collections').Counter(r['kind'] for r in components)), "files": details,
        "seconds": round(time.monotonic()-started, 2),
        "scope": "All 35 standard DEX, including duplicate definitions and empty inputs; the nonstandard embedded .dex is separately retained. Java is reconstructed, complete original instructions remain available."}
    write(PROJECT / "evidence/android-recovery.json", summary)
    print(json.dumps({k: v for k, v in summary.items() if k != "files"}, ensure_ascii=True), flush=True)


if __name__ == "__main__":
    main()
