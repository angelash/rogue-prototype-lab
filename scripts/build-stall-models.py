"""Build original P0 stall props with the verified local Blender CLI.

Run from Blender: blender --background --factory-startup --python this_file.py
Optional arguments after --: --overwrite, --source-only, or --sync-only.
The meshes are presentation assets; they contain no gameplay colliders.
"""

import argparse
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REPO = Path(__file__).resolve().parents[1]
SOURCE = REPO / "sources/art/proto-013-ring-toss/3d-p0"
RUNTIME = REPO / "prototypes/proto-013-ring-toss/game/RingTossWorkshop/Assets/RingToss/Resources/Models"
NAMES = ("fan", "rebound_board", "toy_duck", "ceramic_cup", "radio")
PALETTE = {
    "blue": (0.085, 0.32, 0.45, 1.0),
    "cream": (0.91, 0.83, 0.65, 1.0),
    "ink": (0.055, 0.09, 0.12, 1.0),
    "wood": (0.32, 0.16, 0.060, 1.0),
    "ochre": (0.97, 0.43, 0.035, 1.0),
    "duck": (1.0, 0.39, 0.025, 1.0),
    "orange": (0.94, 0.115, 0.009, 1.0),
    "red": (0.57, 0.035, 0.018, 1.0),
}
MATERIALS = {}
PARTS = []
ROOT = None
DEPLOY_RUNTIME = True


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def reset(name):
    global MATERIALS, PARTS, ROOT
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.context.preferences.filepaths.save_version = 0
    MATERIALS, PARTS = {}, []
    ROOT = bpy.data.objects.new(name, None)
    ROOT.empty_display_type = "PLAIN_AXES"
    ROOT.empty_display_size = 0.07
    bpy.context.collection.objects.link(ROOT)


def mat(key):
    if key not in MATERIALS:
        material = bpy.data.materials.new("P0_" + key)
        material.diffuse_color = PALETTE[key]
        material.use_nodes = True
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = PALETTE[key]
        bsdf.inputs["Roughness"].default_value = 0.58 if key == "cream" else 0.72
        MATERIALS[key] = material
    return MATERIALS[key]


def finish(obj, name, material, smooth=False):
    obj.name = name
    obj.data.materials.append(mat(material))
    obj.parent = ROOT
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth
    PARTS.append(obj)
    return obj


def bevel(obj, amount, segments=2):
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new("SmallPhysicalEdge", "BEVEL")
    modifier.width = amount
    modifier.segments = segments
    modifier.affect = "EDGES"
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def box(name, center, size, material, edge=0.004):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if edge:
        bevel(obj, edge)
    return finish(obj, name, material)


def sphere(name, center, scale, material, segments=20, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=center)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, material, smooth=True)


def cylinder(name, center, radius, depth, material, axis=(0, 0, 1), vertices=20):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=center)
    obj = bpy.context.object
    obj.rotation_euler = Vector(axis).to_track_quat("Z", "Y").to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(obj, name, material, smooth=True)


def rod(name, start, end, radius, material, vertices=8):
    delta = Vector(end) - Vector(start)
    return cylinder(name, (Vector(start) + Vector(end)) * 0.5, radius, delta.length, material, delta, vertices)


def torus(name, center, radius, tube, material, axis=(0, 0, 1), major=24, minor=6):
    bpy.ops.mesh.primitive_torus_add(major_segments=major, minor_segments=minor,
                                   major_radius=radius, minor_radius=tube, location=center)
    obj = bpy.context.object
    obj.rotation_euler = Vector(axis).to_track_quat("Z", "Y").to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(obj, name, material, smooth=True)


def fan():
    box("Fan_Base", (0, 0.02, 0.023), (0.235, 0.18, 0.046), "blue", 0.015)
    cylinder("Fan_Stand", (0, 0.025, 0.16), 0.022, 0.25, "blue")
    head_z = 0.375
    cylinder("Fan_BackHousing", (0, 0.015, head_z), 0.145, 0.095, "blue", (0, 1, 0), 24)
    torus("Fan_CageOuter", (0, -0.056, head_z), 0.164, 0.0075, "cream", (0, 1, 0), 32, 6)
    torus("Fan_CageInner", (0, -0.083, head_z), 0.085, 0.0045, "cream", (0, 1, 0))
    for i in range(8):
        a = i * math.tau / 8
        rod("Fan_Guard_%02d" % i, (0.023 * math.cos(a), -0.086, head_z + 0.023 * math.sin(a)),
            (0.159 * math.cos(a), -0.058, head_z + 0.159 * math.sin(a)), 0.0032, "cream", 6)
    rotor = bpy.data.objects.new("Fan_Blades", None)
    bpy.context.collection.objects.link(rotor)
    rotor.parent = ROOT
    rotor.location = (0, -0.065, head_z)
    rotor["rotation_axis_blender"] = "local Y"
    for i in range(3):
        outline = [(0.014, -0.005), (0.055, -0.048), (0.118, -0.037),
                   (0.13, 0.022), (0.071, 0.054), (0.032, 0.018)]
        angle = i * math.tau / 3
        points = [(x * math.cos(angle) - z * math.sin(angle),
                   x * math.sin(angle) + z * math.cos(angle)) for x, z in outline]
        verts = [(x, y, z) for y in (-0.004, 0.004) for x, z in points]
        count = len(points)
        faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
        faces.extend((j, (j + 1) % count, (j + 1) % count + count, j + count) for j in range(count))
        mesh = bpy.data.meshes.new("BladeGeometry")
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        obj = bpy.data.objects.new("Fan_Blade_%d" % i, mesh)
        bpy.context.collection.objects.link(obj)
        finish(obj, obj.name, "cream")
        obj.parent = rotor
        bevel(obj, 0.003, 1)
    cylinder("Fan_CenterCap", (0, -0.095, head_z), 0.029, 0.015, "blue", (0, 1, 0))
    cylinder("Fan_Switch", (0.065, -0.04, 0.05), 0.011, 0.009, "ink")


def rebound_board():
    box("Board_Panel", (0, 0, 0.29), (0.39, 0.04, 0.36), "wood", 0.005)
    box("Board_FrameTop", (0, -0.013, 0.476), (0.47, 0.06, 0.04), "wood", 0.006)
    box("Board_FrameBottom", (0, -0.013, 0.104), (0.47, 0.06, 0.04), "wood", 0.006)
    for x in (-0.216, 0.216):
        box("Board_FrameSide", (x, -0.013, 0.29), (0.04, 0.06, 0.37), "wood", 0.005)
        box("Board_Foot", (x, 0.032, 0.022), (0.10, 0.23, 0.044), "wood", 0.007)
        rod("Board_BackBrace", (x, 0.115, 0.04), (x, 0.018, 0.30), 0.012, "wood")
    for z in (0.205, 0.345):
        box("Board_YellowStripe", (0, -0.023, z), (0.38, 0.007, 0.033), "ochre", 0.002)
    for x in (-0.21, 0.21):
        for z in (0.12, 0.46):
            cylinder("Board_FramePin", (x, -0.046, z), 0.007, 0.004, "cream", (0, 1, 0), 8)


def toy_duck():
    sphere("Duck_Body", (0, 0.025, 0.107), (0.13, 0.155, 0.107), "duck")
    sphere("Duck_Head", (0, -0.066, 0.238), (0.08, 0.084, 0.079), "duck")
    sphere("Duck_Beak", (0, -0.151, 0.227), (0.065, 0.059, 0.021), "orange", 16, 8)
    for x in (-0.064, 0.064):
        sphere("Duck_Eye", (x, -0.116, 0.26), (0.01, 0.008, 0.011), "ink", 12, 6)
        sphere("Duck_Wing", (x * 1.74, 0.026, 0.13), (0.025, 0.095, 0.05), "duck", 16, 8)
    sphere("Duck_Tail", (0, 0.152, 0.13), (0.055, 0.06, 0.053), "duck", 16, 8)


def ceramic_cup():
    segments = 24
    rings = [(0.082, 0.008), (0.088, 0.035), (0.103, 0.255), (0.103, 0.278),
             (0.087, 0.278), (0.086, 0.258), (0.073, 0.045)]
    vertices = []
    for radius, height in rings:
        vertices.extend((radius * math.cos(i * math.tau / segments),
                         radius * math.sin(i * math.tau / segments), height) for i in range(segments))
    faces = []
    for k in range(len(rings) - 1):
        for i in range(segments):
            nxt = (i + 1) % segments
            faces.append((k * segments + i, k * segments + nxt,
                          (k + 1) * segments + nxt, (k + 1) * segments + i))
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple((len(rings) - 1) * segments + i for i in range(segments)))
    mesh = bpy.data.meshes.new("HollowCupGeometry")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Cup_HollowBody", mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj, obj.name, "cream", smooth=True)
    torus("Cup_Handle", (0.116, 0, 0.154), 0.068, 0.014, "cream", (0, 1, 0))
    torus("Cup_Foot", (0, 0, 0.009), 0.074, 0.009, "cream")
    torus("Cup_OchreBand", (0, 0, 0.218), 0.1005, 0.004, "ochre")


def radio():
    box("Radio_Case", (0, 0, 0.148), (0.35, 0.145, 0.25), "red", 0.019)
    box("Radio_FrontInset", (0, -0.077, 0.148), (0.304, 0.012, 0.204), "cream", 0.009)
    box("Radio_SpeakerInset", (-0.064, -0.087, 0.14), (0.135, 0.009, 0.146), "ink", 0.006)
    for i in range(7):
        box("Radio_SpeakerBar", (-0.064, -0.095, 0.079 + i * 0.020), (0.12, 0.008, 0.006), "cream", 0.001)
    box("Radio_FrequencyWindow", (0.079, -0.091, 0.211), (0.089, 0.009, 0.034), "ink", 0.002)
    for x in (0.055, 0.075, 0.095, 0.115):
        box("Radio_DialTick", (x, -0.098, 0.212), (0.002, 0.004, 0.014), "cream", 0.0005)
    for x in (0.053, 0.112):
        cylinder("Radio_Knob", (x, -0.101, 0.125), 0.023, 0.020, "ink", (0, 1, 0), 16)
        cylinder("Radio_KnobCap", (x, -0.113, 0.125), 0.009, 0.004, "cream", (0, 1, 0), 12)
    for x in (-0.13, 0.13):
        box("Radio_Foot", (x, 0, 0.012), (0.048, 0.075, 0.024), "ink", 0.006)
        rod("Radio_HandlePost", (x, 0, 0.258), (x, 0, 0.319), 0.013, "ink")
    rod("Radio_HandleGrip", (-0.13, 0, 0.319), (0.13, 0, 0.319), 0.013, "ink")
    rod("Radio_Antenna", (0.128, 0.044, 0.264), (0.157, 0.048, 0.38), 0.0035, "cream")
    sphere("Radio_AntennaTip", (0.157, 0.048, 0.38), (0.006, 0.006, 0.006), "cream", 8, 4)


def bounds(objects):
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    low = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    high = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    return low, high


def normalize():
    low, high = bounds(PARTS)
    shift = Vector((-(low.x + high.x) * 0.5, -(low.y + high.y) * 0.5, -low.z))
    # Move root children only; the fan rotor's children inherit its movement.
    for obj in list(ROOT.children):
        obj.location += shift
    bpy.context.view_layer.update()
    return bounds(PARTS)


def lighting_and_camera(low, high):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.world = bpy.data.worlds.new("PreviewWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.10, 0.145, 0.19, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.4
    ground_mat = bpy.data.materials.new("PreviewOnly_Ground")
    ground_mat.diffuse_color = (0.075, 0.11, 0.145, 1)
    ground_mat.use_nodes = True
    ground_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = ground_mat.diffuse_color
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.002))
    ground = bpy.context.object
    ground.name = "PREVIEW_ONLY_Ground"
    ground.data.materials.append(ground_mat)
    center = (low + high) * 0.5
    for name, position, power, color, size in [
        ("PREVIEW_ONLY_WarmKey", (-1.4, -2, 2.8), 170, (1, 0.76, 0.49), 2.0),
        ("PREVIEW_ONLY_CoolFill", (1.5, -0.2, 1.4), 85, (0.45, 0.7, 1), 2.0),
        ("PREVIEW_ONLY_Rim", (0.2, 1.3, 2), 140, (1, 0.67, 0.32), 1.5),
    ]:
        light = bpy.data.lights.new(name, "AREA")
        light.energy, light.color, light.shape, light.size = power, color, "DISK", size
        obj = bpy.data.objects.new(name, light)
        bpy.context.collection.objects.link(obj)
        obj.location = position
        obj.rotation_euler = (center - obj.location).to_track_quat("-Z", "Y").to_euler()
    camera_data = bpy.data.cameras.new("PREVIEW_ONLY_Camera")
    camera = bpy.data.objects.new("PREVIEW_ONLY_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    camera.location = center + Vector((0.75, -1.5, 0.68))
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(high - low) * 1.65
    camera_data.lens = 50


def export(name):
    low, high = normalize()
    ROOT["asset_id"] = "RING-P0-3D-" + name
    ROOT["source_axes"] = "Z-up; front -Y; meters"
    ROOT["purpose"] = "Visual only; no colliders; acceptance markers are external"
    ROOT["copyright"] = "Original procedural geometry created for rogue-prototype-lab"
    size = high - low
    tri_count = 0
    for obj in PARTS:
        obj.data.calc_loop_triangles()
        tri_count += len(obj.data.loop_triangles)
    if tri_count > 3000:
        raise RuntimeError(f"{name}: {tri_count} triangles exceeds the P0 3000 cap")
    if len(MATERIALS) > 3:
        raise RuntimeError(f"{name}: material count exceeds three")
    bpy.ops.object.select_all(action="DESELECT")
    ROOT.select_set(True)
    for obj in PARTS:
        obj.select_set(True)
    for obj in ROOT.children:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = ROOT
    fbx = SOURCE / (name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"MESH", "EMPTY"},
                             global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", use_space_transform=True,
                             bake_space_transform=False, use_mesh_modifiers=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
                             path_mode="AUTO", use_custom_props=True)
    if DEPLOY_RUNTIME:
        shutil.copyfile(fbx, RUNTIME / fbx.name)
    lighting_and_camera(low, high)
    preview = SOURCE / (name + "-preview.png")
    bpy.context.scene.render.filepath = str(preview)
    blend = SOURCE / (name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.render.render(write_still=True)
    return {
        "id": "RING-P0-3D-" + name, "name": name,
        "source_dimensions_xyz_m": [round(value, 5) for value in size],
        "unity_dimensions_width_height_depth_m": [round(size.x, 5), round(size.z, 5), round(size.y, 5)],
        "source_bounds_min_xyz_m": [round(value, 5) for value in low],
        "source_bounds_max_xyz_m": [round(value, 5) for value in high],
        "triangles": tri_count, "mesh_objects": len(PARTS),
        "materials": [{"name": material.name, "rgba": list(material.diffuse_color)} for material in MATERIALS.values()],
        "origin": "bottom bounding-box center; local (0,0,0)",
        "source_front": "-Y", "unity_resource_key": "Models/" + name,
        "visual_collision": "none; no gameplay physics in FBX",
        "source": {"path": blend.relative_to(REPO).as_posix(), "sha256": sha256(blend)},
        "fbx": {"path": fbx.relative_to(REPO).as_posix(), "sha256": sha256(fbx)},
        "runtime_fbx": {"path": (RUNTIME / fbx.name).relative_to(REPO).as_posix(),
                        "sha256": sha256(RUNTIME / fbx.name) if (RUNTIME / fbx.name).exists() else None,
                        "synchronized": DEPLOY_RUNTIME},
        "preview": {"path": preview.relative_to(REPO).as_posix(), "sha256": sha256(preview)},
        "review": "Generated; Blender preview/FBX re-import review is recorded separately; Unity review pending",
    }


def validate_fbx(record):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(REPO / record["fbx"]["path"]))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    low, high = bounds(meshes)
    size = high - low
    expected = record["source_dimensions_xyz_m"]
    if any(abs(size[i] - expected[i]) > 0.0001 for i in range(3)):
        raise RuntimeError(f"FBX round-trip bounds changed: {record['name']} {list(size)} != {expected}")
    root = bpy.data.objects.get(record["name"])
    if root is None or root.location.length > 0.0001:
        raise RuntimeError(f"FBX round-trip root missing or moved: {record['name']}")
    if len(meshes) != record["mesh_objects"]:
        raise RuntimeError(f"FBX round-trip mesh count changed: {record['name']}")
    record["blender_fbx_round_trip"] = {
        "result": "pass", "meshes": len(meshes),
        "reimport_dimensions_xyz_m": [round(value, 5) for value in size],
        "root_at_zero": True, "tolerance_m": 0.0001,
    }


def main():
    global DEPLOY_RUNTIME
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--overwrite", action="store_true")
    route = parser.add_mutually_exclusive_group()
    route.add_argument("--source-only", action="store_true", help="Do not write Unity FBX while the editor is busy")
    route.add_argument("--sync-only", action="store_true", help="Copy the reviewed existing FBX snapshot to Unity, preserving .meta")
    args = parser.parse_args(argv)
    DEPLOY_RUNTIME = not args.source_only
    if args.sync_only:
        register_path = SOURCE / "asset-register.json"
        register = json.loads(register_path.read_text(encoding="utf-8"))
        for record in register["assets"]:
            source_path = REPO / record["fbx"]["path"]
            if source_path.name != record["name"] + ".fbx" or sha256(source_path) != record["fbx"]["sha256"]:
                raise RuntimeError("Unreviewed source changed: " + str(source_path))
        RUNTIME.mkdir(parents=True, exist_ok=True)
        for record in register["assets"]:
            source_path = REPO / record["fbx"]["path"]
            destination = RUNTIME / source_path.name
            shutil.copyfile(source_path, destination)
            record["runtime_fbx"] = {"path": destination.relative_to(REPO).as_posix(), "sha256": sha256(destination), "synchronized": True}
            if record["runtime_fbx"]["sha256"] != record["fbx"]["sha256"]:
                raise RuntimeError("Runtime copy differs: " + str(destination))
        register_path.write_text(json.dumps(register, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        readme = SOURCE / "README.md"
        readme.write_text(readme.read_text(encoding="utf-8").replace("运行副本部署：待同步。", "运行副本部署：已同字节同步并逐件核对 SHA256。"), encoding="utf-8")
        print("RUNTIME_SYNC_OK", len(register["assets"]), flush=True)
        return
    anticipated = [SOURCE / (name + extension) for name in NAMES for extension in (".blend", ".fbx", "-preview.png")]
    anticipated += [RUNTIME / (name + ".fbx") for name in NAMES]
    anticipated += [SOURCE / "asset-register.json", SOURCE / "README.md"]
    if not args.overwrite and any(path.exists() for path in anticipated):
        raise RuntimeError("Output already exists; inspect it before using explicit --overwrite")
    SOURCE.mkdir(parents=True, exist_ok=True)
    RUNTIME.mkdir(parents=True, exist_ok=True)
    records = []
    builders = (fan, rebound_board, toy_duck, ceramic_cup, radio)
    for name, builder in zip(NAMES, builders):
        reset(name)
        builder()
        record = export(name)
        validate_fbx(record)
        records.append(record)
        print("MODEL_OK", name, record["triangles"], record["unity_dimensions_width_height_depth_m"], flush=True)
    register = {
        "version": "0.1", "date": "2026-10-09", "phase": "P0 original presentation candidates",
        "creator": "Project assistant using original procedural geometry; no third-party meshes, images or textures",
        "rights": "Original project assets for use and modification in rogue-prototype-lab; no third-party attribution obligation is introduced",
        "blender": {"version": bpy.app.version_string, "build_hash": bpy.app.build_hash.decode()},
        "script": {"path": Path(__file__).relative_to(REPO).as_posix(), "sha256": sha256(Path(__file__))},
        "export": {"format": "FBX", "axis_forward": "-Z", "axis_up": "Y", "unit": "meter", "global_scale": 1,
                   "apply_scale_options": "FBX_SCALE_UNITS", "bake_space_transform": False},
        "unity_import": "Resources/Models/*.fbx; Unity ModelImporter/material fidelity and Player framing still need actual review",
        "assets": records,
    }
    (SOURCE / "asset-register.json").write_text(json.dumps(register, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# 套圈改造摊 P0 原创三维旧物", "", "版本：v0.1；2026-10-09。", "",
        "本组按用户最新的真实三维斜视地摊方向制作。全部网格与纯色材质由本项目助手使用 Blender 脚本原创构建，没有复用外部模型、纹理或生图。属于项目原创素材，可在本项目内修改、打包和发行；本记录不擅自将全项目改为 CC0。", "",
        "## 源与运行副本", "",
        "每件保留同名 `.blend`、`.fbx` 与 `-preview.png`。权威机器清单位于 `asset-register.json`，包括尺寸、三角面、材料、源/导出/预览/运行副本 SHA256 和 Blender 往返证据。预览灯、相机和地面仅在源场景中，FBX 只导出模型根节点与网格。", "",
        "Unity 副本位于 `Assets/RingToss/Resources/Models/`，运行时使用 `Resources.Load<GameObject>(\"Models/fan\")` 等键。首次 Unity 导入生成 `.meta`，后续替换须保留已有 `.meta`。本脚本不运行 Unity，不创建碰撞器或 Rigidbody。", "",
        "运行副本部署：已同字节同步并逐件核对 SHA256。" if DEPLOY_RUNTIME else "运行副本部署：待同步。", "",
        "## 尺寸与挂点", "",
        "单位米；Blender Z-up、正面 -Y；FBX 声明 Y-up、forward -Z。根为静态 Empty，底部包围盒中心为本地原点，根位置/旋转/缩放为零/零/一。Unity 实际朝向由导入与场景验收确认，不能用 Blender 回读替代 Unity 核验。", "",
        "六槽底心 XZ 为 (-1.6,3.4)、(0,3.4)、(1.6,3.4)、(-1.6,5.6)、(0,5.6)、(1.6,5.6)。模型拟放 0.1m 展示垫表面，接受平面 y=0.65m、标记环与价牌由程序另外显示。模型形体不会决定命中区，风扇效果中心与反弹平面以 Core 规则为准。", "",
    ]
    for record in records:
        width, height, depth = record["unity_dimensions_width_height_depth_m"]
        lines.append(f"1. `{record['name']}`：宽 {width:.3f}m、高 {height:.3f}m、深 {depth:.3f}m；{record['triangles']} 三角面；{len(record['materials'])} 种纯色材质；运行键 `Models/{record['name']}`。")
    lines += ["", "台扇 `Fan_Blades` 是单独可转的空节点，叶片随其转动；源旋转轴为本地 Y，导入后的本地轴须实际核对。外罩、底座保持静态。其余模型本轮没有动画；收音机天线和杯子孔腔为外观细节。反弹板正面是固定源 XZ 平面，法向 -Y，场景不能任意转向后仍画原规则方向。", "",
              "## 生成与验收", "", "使用已核实的 Blender 5.2.2 LTS CLI：", "", "```powershell", "& 'D:\\SteamLibrary\\steamapps\\common\\Blender\\blender.exe' --background --factory-startup --python scripts/build-stall-models.py", "```", "",
              "确认已有输出是本脚本产生的版本后，可在命令末尾添加 `-- --overwrite` 更新这些明确文件；脚本不会删除目录。Unity 忙碌时使用 `-- --overwrite --source-only` 只制作隔离源，结束后使用 `-- --sync-only` 将登记哈希未变的五件 FBX 同步，不改 `.meta`。几何与调色参数固定，FBX/.blend 内部保存元数据不承诺跨运行字节完全一致，因此以本次 SHA256 为快照。", "",
              "生成时检查每件三角面不超过 3000、材质不超过 3、FBX Blender 回读网格数/尺寸与底心根位置；实际预览检查见下方记录。Unity 材质、轴向/大小、六槽遮挡、阴影、性能、真实 Player 镜头和功能提示对齐仍由接入阶段检查，不标记为已通过。", "",
              "## 预览复核记录", "", "待逐件实际查看预览后填写；生成成功不等于视觉验收通过。", ""]
    (SOURCE / "README.md").write_text("\n".join(lines), encoding="utf-8")
    print("ASSET_REGISTER_OK", SOURCE / "asset-register.json", flush=True)


if __name__ == "__main__":
    main()
