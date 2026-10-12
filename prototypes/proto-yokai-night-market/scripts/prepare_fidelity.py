"""Create the LOCAL ONLY Unity restoration from the authorized read-only analysis.

No proprietary data is embedded in this tool. It refuses to write outside local/.
The input installation is never mutated. Run from the repository with Python 3.
"""
from __future__ import annotations
import argparse, hashlib, json, re, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
SOURCE = REPO / "prototypes/re-crunch-time"
LOCAL = ROOT / "local"
PROJECT = LOCAL / "UnityProject"

def read(p): return json.loads(p.read_text(encoding="utf-8"))
def write(p, v):
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(v if isinstance(v, str) else json.dumps(v, ensure_ascii=False, indent=2), encoding="utf-8", newline="\n")
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def safe_id(v): return re.sub(r"[^A-Za-z0-9_-]", "_", v)

def prepare():
    assert PROJECT.resolve().is_relative_to(LOCAL.resolve())
    objects = read(SOURCE / "evidence/objects.json")
    containers = read(SOURCE / "evidence/containers.json")
    behaviours = {x["id"]: x["class"] for x in read(SOURCE / "local/behaviours.json")}
    deps = read(SOURCE / "evidence/dependencies.json")
    trees = {}
    copied = []
    for o in objects:
        folder = safe_id(o["container"])
        path = SOURCE / "local/typetrees" / folder / (str(o["pathID"]) + "_" + o["type"] + ".json")
        # Analysis uses spaces in container directory names except unity_builtin_extra.
        if not path.exists():
            path = SOURCE / "local/typetrees" / o["container"] / (str(o["pathID"]) + "_" + o["type"] + ".json")
        if not path.exists():
            path = next((SOURCE / "local/typetrees").glob("*/" + str(o["pathID"]) + "_" + o["type"] + ".json"), None)
        if path is None: raise FileNotFoundError(o["id"])
        trees[o["id"]] = read(path)
        o["tree"] = trees[o["id"]]
        if o["id"] in behaviours: o["scriptClass"] = behaviours[o["id"]]
        if o["type"] in ("Texture2D", "Font", "AudioClip", "Mesh", "TextAsset") and o["exportStatus"] == "ok":
            origin = SOURCE / o["exportFiles"][0]["path"]
            extension = origin.suffix
            target = PROJECT / "Assets/Recovery/Media" / (safe_id(o["id"]) + extension)
            target.parent.mkdir(parents=True, exist_ok=True)
            if not target.exists() or digest(target) != digest(origin): shutil.copyfile(origin, target)
            o["assetPath"] = target.relative_to(PROJECT).as_posix()
            copied.append({"id":o["id"], "path":o["assetPath"], "sha256":digest(target)})
    manifest = {"objects": objects, "containers":containers, "dependencies":deps,
                "scene":read(SOURCE / "local/scene-hierarchy.json"),
                "resourceMap":read(SOURCE / "local/resource-map.json")}
    write(PROJECT / "Assets/Editor/RecoverySource/manifest.json", manifest)
    adaptations = []
    for source in sorted((SOURCE / "local/decompiled").glob("*.cs")):
        text = source.read_text(encoding="utf-8-sig")
        if source.name == "AssemblyInfo.cs": continue
        before = text
        text = text.replace("base.collider2D", "GetComponent<Collider2D>()")
        text = text.replace("Screen.showCursor", "Cursor.visible")
        text = text.replace(".collider2D", ".GetComponent<Collider2D>()")
        text = text.replace("component.castShadows = false;", "component.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;")
        # Unity 4 scene-loaded query was removed; synchronous SceneManager.LoadScene keeps the original phase gate.
        text = text.replace("Application.isLoadingLevel", "LegacyEngine.IsLoadingLevel")
        text = text.replace("Application.LoadLevel(Application.loadedLevel)", "UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex)")
        target = PROJECT / "Assets/Recovery/Scripts" / source.name
        write(target, text)
        adaptations.append({"file":source.name,"referenceSHA256":digest(source),"adaptedSHA256":digest(target),"changed":before!=text})
    write(PROJECT / "Assets/Recovery/Scripts/LegacyEngine.cs", """// Unity version bridge. No game rules are changed here.
public static class LegacyEngine { public static bool IsLoadingLevel => false; }
""")
    write(PROJECT / "Assets/Recovery/Scripts/LegacyObjectSlot.cs", "using UnityEngine;\npublic sealed class LegacyObjectSlot : MonoBehaviour { public ScriptableObject Value; }\n")
    for source in (ROOT / "scripts/unity").glob("*.cs"):
        target = PROJECT / "Assets/Editor" / source.name
        write(target, source.read_text(encoding="utf-8"))
    for source in (ROOT / "scripts/runtime").glob("*.cs"):
        write(PROJECT / "Assets/Recovery/Validation" / source.name, source.read_text(encoding="utf-8"))
    dll = Path("D:/Program files/2022.3.62f3c1/Editor/Data/Managed/Newtonsoft.Json.dll")
    target = PROJECT / "Assets/Editor/Lib/Newtonsoft.Json.dll"
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(dll, target)
    write(PROJECT / "Packages/manifest.json", {"dependencies":{
        "com.unity.modules.audio":"1.0.0", "com.unity.modules.physics":"1.0.0",
        "com.unity.modules.physics2d":"1.0.0", "com.unity.modules.particlesystem":"1.0.0",
        "com.unity.modules.jsonserialize":"1.0.0", "com.unity.modules.imageconversion":"1.0.0",
        "com.unity.modules.screencapture":"1.0.0", "com.unity.modules.imgui":"1.0.0"}})
    write(PROJECT / "ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 2022.3.62f3c1\nm_EditorVersionWithRevision: 2022.3.62f3c1 (1623fc0bbb97)\n")
    axes = trees["mainData:2"]["m_Axes"]
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!13 &1", "InputManager:", "  m_ObjectHideFlags: 0", "  serializedVersion: 2", "  m_Axes:"]
    for axis in axes:
        out.append("  - serializedVersion: 3")
        for key, value in axis.items():
            if isinstance(value, bool): value = int(value)
            out.append("    " + key + ": " + str(value))
    write(PROJECT / "ProjectSettings/InputManager.asset", "\n".join(out)+"\n")
    write(LOCAL / "preparation.json", {"sourceCopyOnly":True,"copiedAssets":copied,"codeAdaptations":adaptations})
    image_rows = []
    for o in objects:
        if o["type"] == "Texture2D" and o["exportStatus"] == "ok" and o["container"] != "unity default resources":
            image_rows.append({"id":o["id"],"name":o["name"],"width":o["width"],"height":o["height"],
                               "sourceHash":o["sha256"],"replacement":"","status":"reference_only"})
    write(ROOT / "fidelity/image-specs.json", {"version":2,"textures":image_rows,"count":len(image_rows)})
    write(LOCAL / "README.md", "# 本机恢复工程\n\nUnityProject 为用户安装快照的本机互操作与夜市包装工作副本。含第三方实现及参考媒体，不得推送公开仓库。当前制作阶段、替换和验证状态见 ../DEVELOPMENT.md 与 ../fidelity/。\n")
    print(json.dumps({"project":str(PROJECT),"objectCount":len(objects),"sceneObjects":len(manifest["scene"]),"scripts":len(adaptations),"copiedAssets":len(copied),"gameTextures":len(image_rows)},ensure_ascii=False))

if __name__ == "__main__": prepare()
