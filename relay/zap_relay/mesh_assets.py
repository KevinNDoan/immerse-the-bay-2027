"""Convert static, embedded glTF 2.0 meshes into bounded Quest mesh packets.

The server handles GLB parsing and texture resizing. No Unity package is required.
Unsupported compression/skins/sparse accessors fail explicitly, never guess.
"""
from __future__ import annotations

import io
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image

TYPES = {5120: "i1", 5121: "u1", 5122: "<i2", 5123: "<u2", 5125: "<u4", 5126: "<f4"}
SIZES = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


class GLB:
    def __init__(self, path):
        data = Path(path).read_bytes()
        if len(data) < 20 or len(data) > 300_000_000:
            raise ValueError("GLB size is outside the import limits.")
        magic, version, length = struct.unpack_from("<III", data)
        if magic != 0x46546C67 or version != 2 or length != len(data):
            raise ValueError("Expected a complete glTF 2.0 binary file.")
        self.doc = None; self.binary = None
        offset = 12
        while offset < length:
            size, kind = struct.unpack_from("<II", data, offset); offset += 8
            if offset + size > length: raise ValueError("Truncated GLB chunk.")
            chunk = data[offset:offset+size]; offset += size
            if kind == 0x4E4F534A: self.doc = json.loads(chunk)
            if kind == 0x004E4942: self.binary = chunk
        if self.doc is None or self.binary is None: raise ValueError("GLB is missing mesh data.")
        required = set(self.doc.get("extensionsRequired", [])) - {"KHR_materials_unlit"}
        if required or self.doc.get("skins") or self.doc.get("animations"):
            raise ValueError("This environment requires unsupported glTF extensions or animation.")
        if len(self.doc.get("buffers", [])) != 1 or self.doc["buffers"][0].get("uri"):
            raise ValueError("Environment buffers must be embedded in the GLB.")

    def view(self, index):
        view = self.doc["bufferViews"][index]
        start, length = view.get("byteOffset", 0), view["byteLength"]
        if view.get("buffer", 0) != 0 or start < 0 or length < 0 or start+length > len(self.binary):
            raise ValueError("Invalid GLB buffer view.")
        return memoryview(self.binary)[start:start+length]

    def accessor(self, index):
        item = self.doc["accessors"][index]
        if "sparse" in item or item["type"] not in SIZES or item["componentType"] not in TYPES:
            raise ValueError("Unsupported environment accessor.")
        dtype = np.dtype(TYPES[item["componentType"]]); components = SIZES[item["type"]]
        count = item["count"]
        if not 0 < count <= 6_000_000: raise ValueError("Environment has too many mesh elements.")
        view = self.doc["bufferViews"][item["bufferView"]]
        stride = view.get("byteStride", components * dtype.itemsize)
        offset = item.get("byteOffset", 0); data = self.view(item["bufferView"])
        if stride < components*dtype.itemsize or offset < 0 or offset+(count-1)*stride+components*dtype.itemsize > len(data):
            raise ValueError("Environment accessor exceeds its buffer.")
        value = np.ndarray((count, components), dtype, data, offset, (stride, dtype.itemsize)).copy()
        if item.get("normalized") and np.issubdtype(dtype, np.integer):
            value = np.maximum(-1, value.astype(np.float32) / np.iinfo(dtype).max)
        if not np.isfinite(value).all(): raise ValueError("Non-finite environment geometry.")
        return value

    def primitives(self):
        nodes = self.doc.get("nodes", [])
        scene = self.doc.get("scenes", [])[self.doc.get("scene", 0)]
        def visit(index, parent, ancestors):
            if index in ancestors or len(ancestors) > 64: raise ValueError("Invalid scene hierarchy.")
            node = nodes[index]
            transform = parent @ matrix(node)
            if "mesh" in node:
                for primitive in self.doc["meshes"][node["mesh"]]["primitives"]:
                    if primitive.get("mode", 4) != 4 or primitive.get("extensions") or primitive.get("targets"):
                        raise ValueError("Only static triangle geometry is supported.")
                    yield primitive, transform
            for child in node.get("children", []): yield from visit(child, transform, ancestors | {index})
        for node in scene.get("nodes", []): yield from visit(node, np.eye(4), set())


def matrix(node):
    if "matrix" in node:
        result = np.array(node["matrix"], dtype=np.float64).reshape(4,4, order="F")
    else:
        x,y,z,w = node.get("rotation", (0,0,0,1))
        norm = np.linalg.norm((x,y,z,w))
        if norm < .00001: raise ValueError("Invalid mesh rotation.")
        x,y,z,w = np.array((x,y,z,w)) / norm
        result = np.eye(4)
        result[:3,:3] = [[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                           [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                           [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
        result[:3,:3] *= node.get("scale", (1,1,1))
        result[:3,3] = node.get("translation", (0,0,0))
    if not np.isfinite(result).all(): raise ValueError("Invalid mesh transform.")
    return result


def project_panorama(positions, triangles):
    """Texture real surfaces from the generated panorama's original viewpoint.

    Split longitude seams so triangles never interpolate across the whole image.
    This cannot recover the appearance of surfaces hidden from that viewpoint.
    """
    radius = np.maximum(np.linalg.norm(positions, axis=1), 1e-8)
    uv = np.column_stack((np.arctan2(positions[:,0], positions[:,2])/(2*np.pi)+.5,
                          np.arcsin(np.clip(positions[:,1]/radius, -1, 1))/np.pi+.5))
    seam = np.ptp(uv[triangles,0], axis=1) > .5
    crossing = triangles[seam].copy()
    low = uv[crossing,0] < .5
    duplicates = np.unique(crossing[low])
    remap = np.arange(len(positions), dtype=np.uint32)
    remap[duplicates] = np.arange(len(duplicates), dtype=np.uint32) + len(positions)
    crossing[low] = remap[crossing[low]]
    triangles = triangles.copy(); triangles[seam] = crossing
    extra_uv = uv[duplicates].copy(); extra_uv[:,0] += 1
    return np.concatenate((positions, positions[duplicates])), np.concatenate((uv, extra_uv)), triangles


def convert_glb(path, output_dir, prefix="visual", collider=False, marble_frame=True, panorama_path=None):
    glb = GLB(path); output_dir = Path(output_dir); output_dir.mkdir(parents=True, exist_ok=True)
    projected_texture = ""
    if panorama_path is not None and not collider:
        projected_texture = f"{prefix}-0.jpg"
        with Image.open(panorama_path) as image:
            image.thumbnail((4096,2048), Image.Resampling.LANCZOS)
            image.convert("RGB").save(output_dir / projected_texture, quality=94)
    parts = []; total_triangles = 0
    for index, (primitive, transform) in enumerate(glb.primitives()):
        if index >= 64: raise ValueError("Too many environment primitives.")
        attrs = primitive["attributes"]
        positions = glb.accessor(attrs["POSITION"]).astype(np.float32)
        if positions.shape[1] != 3 or len(positions) > 2_000_000: raise ValueError("Unsupported position data.")
        positions = positions @ transform[:3,:3].T + transform[:3,3]
        # Marble exports use its raw OpenCV frame (+Y down, +Z forward), even inside GLB.
        # Equivalent to the vendor viewer's 180-degree X rotation, then glTF -> Unity Z flip.
        positions[:,1 if marble_frame else 2] *= -1
        if np.abs(positions).max() > 10_000: raise ValueError("Environment scale is invalid.")
        indices = glb.accessor(primitive["indices"]).reshape(-1) if "indices" in primitive else np.arange(len(positions))
        if len(indices) % 3 or indices.max() >= len(positions) or indices.min() < 0:
            raise ValueError("Invalid environment triangle indices.")
        triangles = indices.astype(np.uint32).reshape(-1,3)
        if np.linalg.det(transform[:3,:3]) >= 0: triangles = triangles[:,[0,2,1]]
        total_triangles += len(triangles)
        if total_triangles > (400_000 if collider else 1_500_000):
            raise ValueError("Environment exceeds the Quest triangle budget.")
        uv = np.zeros((len(positions),2), dtype=np.float32)
        if not collider and not projected_texture and "TEXCOORD_0" in attrs:
            uv = glb.accessor(attrs["TEXCOORD_0"]).astype(np.float32)
            uv[:,1] = 1 - uv[:,1]  # Embedded image origin is top-left in glTF.
        colors = np.ones((len(positions),4), dtype=np.float32)
        if not collider and not projected_texture and "COLOR_0" in attrs:
            source_colors = glb.accessor(attrs["COLOR_0"])
            colors[:,:source_colors.shape[1]] = source_colors
        material = glb.doc.get("materials", [])[primitive["material"]] if "material" in primitive else {}
        pbr = material.get("pbrMetallicRoughness", {})
        if not projected_texture: colors *= pbr.get("baseColorFactor", (1,1,1,1))
        texture_name = ""
        if projected_texture:
            positions, uv, triangles = project_panorama(positions, triangles)
            colors = np.ones((len(positions),4), dtype=np.float32)
            texture_name = projected_texture
        elif not collider and "baseColorTexture" in pbr:
            tex = pbr["baseColorTexture"]
            if tex.get("texCoord", 0) != 0 or tex.get("extensions"):
                raise ValueError("Unsupported environment texture transform.")
            image_index = glb.doc["textures"][tex["index"]]["source"]
            image = glb.doc["images"][image_index]
            if "bufferView" not in image: raise ValueError("Environment textures must be embedded.")
            with Image.open(io.BytesIO(glb.view(image["bufferView"]))) as decoded:
                decoded.thumbnail((4096,4096), Image.Resampling.LANCZOS)
                texture_name = f"{prefix}-{index}.jpg"
                decoded.convert("RGB").save(output_dir / texture_name, quality=90)
        # The HQ exporter duplicates each triangle's vertices. Weld only identical attributes,
        # preserving texture seams and color boundaries while reducing Quest memory.
        attributes = np.column_stack((positions,uv,colors)).astype("<f4")
        if not np.isfinite(attributes).all(): raise ValueError("Non-finite environment attributes.")
        unique, inverse = np.unique(attributes, axis=0, return_inverse=True)
        triangles = inverse[triangles].astype("<u4")
        vertex_colors = np.round(np.clip(unique[:,5:9],0,1)*255).astype(np.uint8)
        packet = bytearray(struct.pack("<4sII", b"ZMS1", len(unique), triangles.size))
        packet.extend(unique[:,:3].astype("<f4").tobytes())
        packet.extend(unique[:,3:5].astype("<f4").tobytes())
        packet.extend(vertex_colors.tobytes()); packet.extend(triangles.tobytes())
        filename = f"{prefix}-{index}.mesh"
        (output_dir / filename).write_bytes(packet)
        parts.append({"mesh": filename, "texture": texture_name,
            "vertices": len(unique), "triangles": len(triangles)})
    if not parts: raise ValueError("No renderable environment geometry.")
    return parts
