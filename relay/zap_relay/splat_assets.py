"""Bounded SPZ v2/v3 decoding for the Quest renderer; no paid export involved.

Format reference: Niantic Labs SPZ (MIT), github.com/nianticlabs/spz.
World Labs' assets use its raw RDF frame, like its collider GLB: flip Y for Unity.
Only DC color is needed for the provider's mobile (SH degree zero) variants.
"""
import gzip
import io
import struct
from pathlib import Path

import numpy as np

MAX_SPLATS = 500_000


def decode_spz(data):
    with gzip.GzipFile(fileobj=io.BytesIO(data)) as stream:
        raw = stream.read(40_000_001)
    if len(raw) < 16 or len(raw) > 40_000_000:
        raise ValueError("Invalid splat file size.")
    magic, version, count, degree, bits, flags, reserved = struct.unpack_from('<III4B', raw)
    if magic != 0x5053474e or version not in (2, 3) or not 1 <= count <= MAX_SPLATS or degree > 3 or bits > 24 or flags & ~1 or reserved:
        raise ValueError("Unsupported splat format or mobile point budget.")
    stride = 19 if version == 2 else 20
    if len(raw) != 16 + count * (stride + ((degree + 1) ** 2 - 1) * 3):
        raise ValueError("Truncated splat attributes.")
    offset = 16
    def take(width, dtype=np.uint8):
        nonlocal offset
        value = np.frombuffer(raw, dtype=dtype, count=count * width, offset=offset).reshape(count, width)
        offset += value.nbytes
        return value
    packed = take(9).astype(np.int32).reshape(count, 3, 3)
    xyz = packed[:,:,0] | (packed[:,:,1] << 8) | (packed[:,:,2] << 16)
    xyz = ((xyz ^ 0x800000) - 0x800000).astype(np.float32) / (1 << bits)
    alpha = take(1).astype(np.float32) / 255
    rgb = np.clip(.5 + .28209479177387814 * (take(3).astype(np.float32) / 255 - .5) / .15, 0, 1)
    scales = np.exp(take(3).astype(np.float32) / 16 - 10)
    q = np.zeros((count,4), np.float32)
    if version == 2:
        q[:,:3] = take(3).astype(np.float32) / 127.5 - 1
        q[:,3] = np.sqrt(np.maximum(0, 1 - np.sum(q[:,:3]**2, axis=1)))
    else:
        encoded = take(1, '<u4').reshape(-1).copy()
        largest = encoded >> 30
        for i in range(3, -1, -1):
            mask = largest != i
            q[mask,i] = (encoded[mask] & 511) / (511 * np.sqrt(2)) * np.where(encoded[mask] & 512, -1, 1)
            encoded[mask] >>= 10
        q[np.arange(count),largest] = np.sqrt(np.maximum(0, 1 - np.sum(q*q,axis=1)))
    q /= np.maximum(np.linalg.norm(q,axis=1,keepdims=True), 1e-8)
    x,y,z,w = q.T
    rotation = np.stack([1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w),
                         2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w),
                         2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)],axis=1).reshape(-1,3,3)
    axes = rotation * scales[:,None,:]
    axes[:,1,:] *= -1
    covariance = axes @ axes.transpose(0,2,1)
    xyz[:,1] *= -1
    result = np.zeros((count,16),dtype='<f4')
    result[:,:3] = xyz; result[:,3:4] = alpha
    result[:,4:8] = covariance[:,(0,0,0,1),(0,1,2,1)]
    result[:,8:10] = covariance[:,(1,2),(2,2)]
    result[:,12:15] = rgb
    if not np.isfinite(result).all() or np.abs(xyz).max() > 10000:
        raise ValueError("Invalid splat geometry.")
    return result


def convert_spz(source, output_dir):
    points = decode_spz(Path(source).read_bytes())
    output_dir = Path(output_dir); output_dir.mkdir(parents=True, exist_ok=True)
    path = output_dir / 'scene.splat'
    temporary = path.with_suffix('.tmp')
    temporary.write_bytes(struct.pack('<4sI', b'ZSP1', len(points)) + points.tobytes())
    temporary.replace(path)
    return {'file': path.name, 'count': len(points)}
