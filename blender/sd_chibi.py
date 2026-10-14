"""
일반 비율 캐릭터 모델을 SD(치비) 비율로 바꾸고 Unity용 FBX로 내보내는 Blender 스크립트.

머리는 키우고, 목·몸통·팔은 줄이고, 다리는 짧게 만든 뒤 발바닥을 원래 바닥 높이에 맞춥니다.
뼈대(rest pose)와 메시를 같이 바꾸므로 Unity에서 Humanoid로 써도 비율이 유지되고,
표정(셰이프 키)도 같이 변형되어 그대로 남습니다.

사용법 (명령 프롬프트, 한 줄로):
  blender.exe -b --python-exit-code 1 --python sd_chibi.py -- --input 모델.pmx --fbx 결과.fbx --preview 비교.png
  blender.exe -b 모델.blend --python-exit-code 1 --python sd_chibi.py -- --fbx 결과.fbx --preview 비교.png --blend 결과.blend

  --input        .pmx/.pmd(MMD Tools 필요) 또는 .fbx. 생략하면 이미 연 .blend를 씁니다.
  --fbx          Unity용 FBX 저장 경로
  --blend        변형한 결과를 .blend로도 저장
  --preview      변형 전/후를 나란히 렌더링한 PNG 저장 경로
  --head 1.45    머리 크기 배율 (1.3 자연스러움 ~ 1.8 아주 큰 머리)
  --hair 값      머리카락 뼈 배율 (생략하면 머리와 같음). 긴 머리가 너무 길어지면 1.0~1.3으로
  --neck 0.5     목 길이 배율
  --torso 0.85   몸통 배율
  --arm 0.75     팔 배율
  --leg-length 0.55  다리 길이 배율
  --leg-width 0.85   다리 굵기와 발 크기 배율
  --arms-down 각도  팔을 수평에서 이 각도만큼 내림 (T포즈 모델을 애니메이션 없이 세워 둘 때 60~70)
  --dry-run      뼈 분류 결과만 출력하고 아무것도 바꾸지 않음

뼈 이름으로 부위를 찾습니다. MMD 표준 뼈 이름(頭, 首, 上半身, 足, ひざ ...)과
영어 이름(Head, Neck, Spine, UpperLeg ...), VRM 이름(J_Bip_C_Head ...)을 알아봅니다.
처음 쓰는 모델은 --dry-run으로 분류가 맞는지 먼저 확인하세요.
"""

import argparse
import math
import os
import re
import sys

import bpy
from mathutils import Matrix
import numpy as np

# ───────── 뼈 분류 ─────────

# 부위: root(그대로), torso, neck, head, hair, arm, leg, foot, ik(가까운 뼈를 따라감)
JP_REGIONS = {
    "全ての親": "root", "操作中心": "root",
    "センター": "torso", "グルーブ": "torso", "腰": "torso", "下半身": "torso",
    "上半身": "torso", "上半身1": "torso", "上半身2": "torso", "上半身3": "torso", "腰キャンセル": "torso",
    "首": "neck",
    "頭": "head",
    "腕": "arm", "腕捩": "arm",
    "足": "leg", "ひざ": "leg",
    "足首": "foot", "足先EX": "foot", "つま先": "foot",
}

# 영어/VRM 이름. 위에서부터 먼저 맞는 것을 씁니다. (구분 기호를 뺀 소문자로 비교)
EN_RULES = [
    ("root", ("root", "armature")),  # "armature"가 "arm"(팔)으로 잡히지 않게 먼저 봅니다
    ("neck", ("neck",)),
    ("head", ("head",)),
    ("foot", ("foot", "toe", "ankle")),
    ("leg", ("upperleg", "lowerleg", "thigh", "knee", "shin", "calf", "leg")),
    ("arm", ("upperarm", "lowerarm", "forearm", "elbow", "hand", "wrist", "arm")),
    ("torso", ("hips", "hip", "pelvis", "spine", "chest", "waist", "upperbody", "lowerbody", "center", "centre")),
]

# 이 부위 아래의 뼈는 이름과 상관없이 같은 부위로 봅니다 (머리카락, 눈, 손가락 등)
LOCKED_REGIONS = {"head", "hair", "arm", "foot"}

# 머리 아래에서 이 이름이 들어간 뼈는 머리카락으로 봅니다 (--hair 배율 적용)
HAIR_PATTERN = re.compile(r"髪|もみあげ|アホ毛|hair|ahoge|bang|tail", re.IGNORECASE)

IK_PATTERN = re.compile(r"ＩＫ|IK|(^|[^a-z])ik([^a-z]|$)")


def base_name(name):
    n = name
    for prefix in ("_dummy_", "_shadow_"):
        if n.startswith(prefix):
            n = n[len(prefix):]
    if n[:1] in ("左", "右"):
        n = n[1:]
    for suffix in (".L", ".R", "_L", "_R", ".l", ".r", "_l", "_r"):
        if n.endswith(suffix):
            n = n[: -len(suffix)]
            break
    # MMD의 D 뼈 (足D, ひざD, 足首D)는 원래 뼈와 같은 부위입니다
    if len(n) > 1 and n.endswith("D") and not n.isascii():
        n = n[:-1]
    return n


def classify(bone, parent_region):
    if parent_region == "head" and HAIR_PATTERN.search(bone.name):
        return "hair"
    if parent_region in LOCKED_REGIONS:
        return parent_region
    if IK_PATTERN.search(bone.name):
        return "ik"
    base = base_name(bone.name)
    if base in JP_REGIONS:
        return JP_REGIONS[base]
    simple = re.sub(r"[\s_.\-]", "", base).lower()
    for region, keys in EN_RULES:
        if any(key in simple for key in keys):
            return region
    if parent_region is None or parent_region == "ik":
        return "root" if parent_region is None else "ik"
    return parent_region


# ───────── 변형 행렬 ─────────

def affine(scale3, pivot_new, pivot_old, rot=None):
    """p -> pivot_new + R S (p - pivot_old) 를 4x4 행렬로"""
    m = np.eye(4)
    rs = (rot if rot is not None else np.eye(3)) @ np.diag(scale3)
    m[:3, :3] = rs
    m[:3, 3] = pivot_new - rs @ pivot_old
    return m


def rotation_y(angle):
    c, s = math.cos(angle), math.sin(angle)
    return np.array([[c, 0.0, s], [0.0, 1.0, 0.0], [-s, 0.0, c]])


def apply(m, p):
    return m[:3, :3] @ p + m[:3, 3]


def region_scales(args):
    return {
        "root": (1.0, 1.0, 1.0),
        "torso": (args.torso,) * 3,
        "neck": (args.torso, args.torso, args.neck),
        "head": (args.head,) * 3,
        "hair": ((args.hair if args.hair else args.head),) * 3,
        "arm": (args.arm,) * 3,
        "leg": (args.leg_width, args.leg_width, args.leg_length),
        "foot": (args.leg_width,) * 3,
    }


def build_bone_deforms(arm_obj, args, z_offset=0.0):
    """뼈마다 월드 공간 변형 행렬을 만듭니다. 같은 부위의 뼈는 같은 행렬을 공유합니다."""
    mw = np.array(arm_obj.matrix_world)
    scales = region_scales(args)
    bones = arm_obj.data.bones

    def world(v):
        return (mw @ np.array([v[0], v[1], v[2], 1.0]))[:3]

    regions = {}

    def classify_all(bone, parent_region):
        regions[bone.name] = classify(bone, parent_region)
        for child in bone.children:
            classify_all(child, regions[bone.name])

    for bone in bones:
        if bone.parent is None:
            classify_all(bone, None)

    deforms = {}
    rotations = {}

    def arm_rotation(bone):
        """--arms-down: 팔이 수평에서 이 각도만큼 아래를 향하도록 어깨 관절에서 돌립니다"""
        if args.arms_down is None:
            return np.eye(3)
        d = world(bone.tail_local) - world(bone.head_local)
        current = math.degrees(math.atan2(-d[2], abs(d[0])))
        delta = math.radians(args.arms_down - current)
        return rotation_y(delta if d[0] >= 0 else -delta)

    def compute(bone):
        region = regions[bone.name]
        if region == "ik":
            # IK 뼈는 위치가 같은 (가장 가까운) 몸 뼈를 따라갑니다
            head = world(bone.head_local)
            best, best_dist = None, math.inf
            for other in bones:
                if other.name in deforms and regions[other.name] not in ("ik", "root"):
                    dist = np.linalg.norm(world(other.head_local) - head)
                    if dist < best_dist:
                        best, best_dist = other.name, dist
            parent_deform = deforms.get(bone.parent.name) if bone.parent else None
            deforms[bone.name] = deforms[best] if best else (parent_deform if parent_deform is not None else np.eye(4))
        else:
            head = world(bone.head_local)
            parent_deform = deforms.get(bone.parent.name) if bone.parent else None
            pivot_new = apply(parent_deform, head) if parent_deform is not None else head
            parent_region = regions[bone.parent.name] if bone.parent else None
            if region == "arm" and parent_region != "arm":
                rotations[bone.name] = arm_rotation(bone)
            elif parent_region == region:
                rotations[bone.name] = rotations.get(bone.parent.name, np.eye(3))
            else:
                rotations[bone.name] = np.eye(3)
            deforms[bone.name] = affine(np.array(scales[region]), pivot_new, head, rotations[bone.name])
        for child in bone.children:
            if regions[child.name] != "ik" or regions[bone.name] == "ik":
                compute(child)

    # 몸 뼈를 먼저 다 계산한 뒤에 IK 뼈를 계산합니다 (가까운 몸 뼈를 찾아야 하므로)
    roots = [b for b in bones if b.parent is None]
    for bone in roots:
        if regions[bone.name] != "ik":
            compute(bone)
    for bone in bones:
        if regions[bone.name] == "ik" and bone.name not in deforms and (bone.parent is None or regions[bone.parent.name] != "ik"):
            compute(bone)
    for bone in bones:  # 혹시 빠진 뼈가 있으면 그대로 둡니다
        deforms.setdefault(bone.name, np.eye(4))

    if z_offset:
        shift = np.eye(4)
        shift[2, 3] = z_offset
        for name in deforms:
            if regions[name] != "root":
                deforms[name] = shift @ deforms[name]
    return deforms, regions, rotations


# ───────── 메시 변형 ─────────

def skinned_meshes(arm_obj):
    result = []
    for ob in bpy.data.objects:
        if ob.type != "MESH":
            continue
        if any(m.type == "ARMATURE" and m.object == arm_obj for m in ob.modifiers):
            result.append(ob)
    return result


def vertex_affines(ob, deforms, regions, arm_obj):
    """정점마다 (뼈 가중치로 섞은) 오브젝트 공간 변형 행렬"""
    me = ob.data
    n = len(me.vertices)
    mo = np.array(ob.matrix_world)
    mo_inv = np.linalg.inv(mo)
    deform_bones = {b.name for b in arm_obj.data.bones if b.use_deform}

    group_matrix = {}
    group_is_leg = {}
    for group in ob.vertex_groups:
        if group.name in deforms and group.name in deform_bones:
            group_matrix[group.index] = mo_inv @ deforms[group.name] @ mo
            group_is_leg[group.index] = regions[group.name] in ("leg", "foot")

    vi, gi, ws = [], [], []
    for v in me.vertices:
        for g in v.groups:
            if g.group in group_matrix and g.weight > 0.0:
                vi.append(v.index)
                gi.append(g.group)
                ws.append(g.weight)
    vi = np.array(vi, dtype=np.int64)
    ws = np.array(ws)
    group_ids = sorted(group_matrix)
    lookup = {g: i for i, g in enumerate(group_ids)}
    mats = np.array([group_matrix[g] for g in group_ids]) if group_ids else np.zeros((0, 4, 4))
    leg_flags = np.array([group_is_leg[g] for g in group_ids], dtype=float) if group_ids else np.zeros(0)
    gidx = np.array([lookup[g] for g in gi], dtype=np.int64)

    a = np.zeros((n, 4, 4))
    wsum = np.zeros(n)
    leg = np.zeros(n)
    if len(vi):
        np.add.at(a, vi, ws[:, None, None] * mats[gidx])
        np.add.at(wsum, vi, ws)
        np.add.at(leg, vi, ws * leg_flags[gidx])
    weighted = wsum > 1e-8
    a[weighted] /= wsum[weighted][:, None, None]
    a[~weighted] = np.eye(4)
    leg_share = np.where(weighted, leg / np.maximum(wsum, 1e-8), 0.0)
    return a, leg_share, int((~weighted).sum())


def transform_coords(a, co):
    return np.einsum("nij,nj->ni", a[:, :3, :3], co) + a[:, :3, 3]


def read_coords(collection, n):
    co = np.empty(n * 3)
    collection.foreach_get("co", co)
    return co.reshape(n, 3)


def world_z(ob, co):
    mw = np.array(ob.matrix_world)
    return (co @ mw[:3, :3].T + mw[:3, 3])[:, 2]


def feet_floor(meshes, coords_of, leg_shares):
    """다리 정점 중 가장 낮은 높이 (없으면 전체에서 가장 낮은 높이)"""
    lows_leg, lows_all = [], []
    for ob in meshes:
        z = world_z(ob, coords_of(ob))
        if len(z) == 0:
            continue
        lows_all.append(z.min())
        mask = leg_shares[ob.name] > 0.5
        if mask.any():
            lows_leg.append(z[mask].min())
    if lows_leg:
        return min(lows_leg)
    return min(lows_all) if lows_all else 0.0


def deform_mesh(ob, a):
    me = ob.data
    n = len(me.vertices)
    if me.shape_keys:
        for key in me.shape_keys.key_blocks:
            co = transform_coords(a, read_coords(key.data, n))
            key.data.foreach_set("co", co.ravel())
    co = transform_coords(a, read_coords(me.vertices, n))
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def apply_to_armature(arm_obj, deforms, rotations):
    mw = np.array(arm_obj.matrix_world)
    mw_inv = np.linalg.inv(mw)

    def to_local(p):
        return (mw_inv @ np.append(p, 1.0))[:3]

    new_positions = {}
    for bone in arm_obj.data.bones:
        m = deforms[bone.name]
        head = apply(m, (mw @ np.append(np.array(bone.head_local), 1.0))[:3])
        tail = apply(m, (mw @ np.append(np.array(bone.tail_local), 1.0))[:3])
        new_positions[bone.name] = (to_local(head), to_local(tail))

    select_only(arm_obj)
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = arm_obj.data.edit_bones
    basis = mw[:3, :3]
    basis_inv = np.linalg.inv(basis)
    for name, (head, tail) in new_positions.items():
        eb = edit_bones.get(name)
        if eb is None:
            continue
        rot = rotations.get(name)
        if rot is not None and not np.allclose(rot, np.eye(3)):
            # 돌린 뼈는 비틀림(roll)까지 같이 돌아가도록 행렬로 바꿉니다
            matrix = np.array(eb.matrix)
            matrix[:3, :3] = (basis_inv @ rot @ basis) @ matrix[:3, :3]
            matrix[:3, 3] = head
            eb.matrix = Matrix(matrix.tolist())
            eb.length = float(np.linalg.norm(tail - head))
        else:
            eb.head = head.tolist()
            eb.tail = tail.tolist()
    bpy.ops.object.mode_set(mode="OBJECT")


# ───────── 불러오기, 내보내기, 미리보기 ─────────

def select_only(*objects):
    for ob in bpy.context.view_layer.objects:
        ob.select_set(False)
    for ob in objects:
        ob.hide_set(False)
        ob.hide_viewport = False
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def import_model(path):
    # 시작 화면의 기본 큐브·카메라·조명이 미리보기를 가리지 않게 비웁니다
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    ext = os.path.splitext(path)[1].lower()
    if ext in (".pmx", ".pmd"):
        if not hasattr(bpy.ops, "mmd_tools") or not hasattr(bpy.ops.mmd_tools, "import_model"):
            sys.exit("PMX를 불러오려면 MMD Tools 확장이 필요합니다. Edit > Preferences > Get Extensions에서 설치하세요.")
        bpy.ops.mmd_tools.import_model(filepath=path, types={"MESH", "ARMATURE", "MORPHS"})
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        sys.exit(f"지원하지 않는 형식입니다: {ext} (.pmx, .pmd, .fbx)")


def find_armature():
    candidates = [ob for ob in bpy.data.objects if ob.type == "ARMATURE"]
    if not candidates:
        sys.exit("아마추어(뼈대)가 있는 모델을 찾지 못했습니다.")
    return max(candidates, key=lambda arm: len(skinned_meshes(arm)))


def mesh_bounds(meshes):
    lo = np.full(3, math.inf)
    hi = np.full(3, -math.inf)
    for ob in meshes:
        co = read_coords(ob.data.vertices, len(ob.data.vertices))
        if len(co) == 0:
            continue
        mw = np.array(ob.matrix_world)
        world = co @ mw[:3, :3].T + mw[:3, 3]
        lo = np.minimum(lo, world.min(axis=0))
        hi = np.maximum(hi, world.max(axis=0))
    return lo, hi


def render_front(path, bounds):
    """정면(-Y 쪽)에서 직교 카메라로 렌더링합니다. 전/후 비교를 위해 같은 틀(bounds)을 씁니다."""
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "TEXTURE"
    except TypeError:
        pass
    scene.render.resolution_x = 600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"

    lo, hi = bounds
    center = (lo + hi) / 2
    height = hi[2] - lo[2]
    width = hi[0] - lo[0]

    cam_data = bpy.data.cameras.get("SDPreviewCam") or bpy.data.cameras.new("SDPreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = max(height, width * 900 / 600) * 1.1
    cam = bpy.data.objects.get("SDPreviewCam") or bpy.data.objects.new("SDPreviewCam", cam_data)
    if cam.name not in scene.collection.objects:
        scene.collection.objects.link(cam)
    cam.location = (center[0], lo[1] - max(height, 1.0) * 3, center[2])
    cam.rotation_euler = (math.radians(90), 0.0, 0.0)
    cam_data.clip_end = max(height, 1.0) * 10
    scene.camera = cam

    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def combine_side_by_side(left_path, right_path, out_path):
    left = bpy.data.images.load(left_path)
    right = bpy.data.images.load(right_path)
    w, h = left.size
    a = np.array(left.pixels[:]).reshape(h, w, 4)
    b = np.array(right.pixels[:]).reshape(h, w, 4)
    gap = np.ones((h, 8, 4))
    joined = np.concatenate([a, gap, b], axis=1)
    out = bpy.data.images.new("SDPreview", width=joined.shape[1], height=h, alpha=True)
    out.pixels.foreach_set(joined.astype(np.float32).ravel())
    out.filepath_raw = out_path
    out.file_format = "PNG"
    out.save()
    for image in (left, right, out):
        bpy.data.images.remove(image)


def prepare_materials_for_fbx(meshes):
    """
    MMD Tools의 셰이더(노드 그룹)는 FBX 내보내기가 읽지 못해서 텍스처가 빠집니다.
    기본 텍스처와 색, 투명도를 Principled BSDF에 연결해서 Unity가 찾을 수 있게 합니다.
    """
    done = set()
    for ob in meshes:
        for slot in ob.material_slots:
            mat = slot.material
            if mat is None or mat.name in done or mat.node_tree is None:
                continue
            done.add(mat.name)
            nodes, links = mat.node_tree.nodes, mat.node_tree.links
            output = next((n for n in nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output), None)
            if output is None:
                output = nodes.new("ShaderNodeOutputMaterial")
            surface = output.inputs["Surface"]
            if surface.links and surface.links[0].from_node.type == "BSDF_PRINCIPLED":
                continue

            mmd = getattr(mat, "mmd_material", None)
            color = tuple(mmd.diffuse_color) if mmd else tuple(mat.diffuse_color[:3])
            alpha = float(mmd.alpha) if mmd else float(mat.diffuse_color[3])
            bsdf = nodes.new("ShaderNodeBsdfPrincipled")
            texture = nodes.get("mmd_base_tex")
            if texture is not None and texture.type == "TEX_IMAGE" and texture.image is not None:
                links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
            else:
                bsdf.inputs["Base Color"].default_value = (*color, 1.0)
            # 표정용 재질(눈물, 홍조 등)은 MMD에서 알파 0으로 숨겨져 있습니다
            bsdf.inputs["Alpha"].default_value = alpha
            bsdf.inputs["Roughness"].default_value = 1.0
            for link in list(surface.links):
                links.remove(link)
            links.new(bsdf.outputs["BSDF"], surface)


def export_fbx(arm_obj, meshes, path):
    prepare_materials_for_fbx(meshes)
    select_only(arm_obj, *meshes)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        # 아마추어 외 모디파이어를 적용하면 셰이프 키(표정)가 빠지므로 끕니다
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )


# ───────── 실행 ─────────

def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="sd_chibi.py")
    p.add_argument("--input")
    p.add_argument("--fbx")
    p.add_argument("--blend")
    p.add_argument("--preview")
    p.add_argument("--head", type=float, default=1.45)
    p.add_argument("--hair", type=float, default=None)
    p.add_argument("--neck", type=float, default=0.5)
    p.add_argument("--torso", type=float, default=0.85)
    p.add_argument("--arm", type=float, default=0.75)
    p.add_argument("--leg-length", type=float, default=0.55)
    p.add_argument("--leg-width", type=float, default=0.85)
    p.add_argument("--arms-down", type=float, default=None)
    p.add_argument("--dry-run", action="store_true")
    return p.parse_args(argv)


def report_regions(regions):
    by_region = {}
    for name, region in regions.items():
        by_region.setdefault(region, []).append(name)
    print("\n[뼈 분류]")
    for region in ("root", "torso", "neck", "head", "hair", "arm", "leg", "foot", "ik"):
        names = by_region.get(region, [])
        sample = ", ".join(names[:8]) + (" ..." if len(names) > 8 else "")
        print(f"  {region:6s} {len(names):4d}개  {sample}")
    for needed in ("head", "torso", "leg"):
        if not by_region.get(needed):
            print(f"  ! '{needed}' 부위를 찾지 못했습니다. 뼈 이름을 확인하세요.")


def head_count(meshes, regions, arm_obj):
    """대략적인 등신: 전체 키 / (머리 꼭대기 - 머리 뼈 시작 높이 x 1.15)"""
    lo, hi = mesh_bounds(meshes)
    mw = np.array(arm_obj.matrix_world)
    heads = [b for b in arm_obj.data.bones if regions[b.name] == "head" and (b.parent is None or regions[b.parent.name] != "head")]
    if not heads:
        return None
    head_z = (mw @ np.append(np.array(heads[0].head_local), 1.0))[2]
    head_size = (hi[2] - head_z) * 1.15  # 턱이 목 관절보다 조금 아래에 있으므로
    return (hi[2] - lo[2]) / max(head_size, 1e-6), hi[2] - lo[2]


def main():
    args = parse_args()
    if args.input:
        import_model(os.path.abspath(args.input))

    arm_obj = find_armature()
    meshes = skinned_meshes(arm_obj)
    print(f"\n아마추어: {arm_obj.name}, 메시 {len(meshes)}개: {', '.join(m.name for m in meshes)}")
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    deforms, regions, _ = build_bone_deforms(arm_obj, args)
    report_regions(regions)
    if args.dry_run:
        return

    before = head_count(meshes, regions, arm_obj)
    bounds_before = mesh_bounds(meshes)
    preview_dir = os.path.dirname(os.path.abspath(args.preview)) if args.preview else None
    if args.preview:
        render_front(os.path.join(preview_dir, "_sd_before.png"), bounds_before)

    # 1차: 바닥 높이 차이 계산 → 2차: 그만큼 내려서 실제로 적용
    affines = {}
    leg_shares = {}
    for ob in meshes:
        affines[ob.name], leg_shares[ob.name], unweighted = vertex_affines(ob, deforms, regions, arm_obj)
        if unweighted:
            print(f"  ! {ob.name}: 뼈 가중치가 없는 정점 {unweighted}개는 그대로 둡니다.")
    floor_before = feet_floor(meshes, lambda ob: read_coords(ob.data.vertices, len(ob.data.vertices)), leg_shares)
    floor_after = feet_floor(
        meshes, lambda ob: transform_coords(affines[ob.name], read_coords(ob.data.vertices, len(ob.data.vertices))), leg_shares
    )
    z_offset = floor_before - floor_after

    deforms, regions, rotations = build_bone_deforms(arm_obj, args, z_offset)
    for ob in meshes:
        a, _, _ = vertex_affines(ob, deforms, regions, arm_obj)
        deform_mesh(ob, a)
    apply_to_armature(arm_obj, deforms, rotations)

    after = head_count(meshes, regions, arm_obj)
    if before and after:
        print(f"\n[결과] 키 {before[1]:.3f} → {after[1]:.3f} (x{after[1] / before[1]:.2f}),"
              f" 대략 {before[0]:.1f}등신 → {after[0]:.1f}등신")

    if args.preview:
        render_front(os.path.join(preview_dir, "_sd_after.png"), bounds_before)
        combine_side_by_side(
            os.path.join(preview_dir, "_sd_before.png"),
            os.path.join(preview_dir, "_sd_after.png"),
            os.path.abspath(args.preview),
        )
        print(f"미리보기: {os.path.abspath(args.preview)}")
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
        print(f"Blend 저장: {os.path.abspath(args.blend)}")
    if args.fbx:
        export_fbx(arm_obj, meshes, os.path.abspath(args.fbx))
        print(f"FBX 저장: {os.path.abspath(args.fbx)}")


if __name__ == "__main__":
    main()
