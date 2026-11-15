"""Resumable generated geometry jobs and local asset delivery for the Quest."""
from __future__ import annotations

import asyncio
import base64
import hashlib
import io
import json
import re
from pathlib import Path

from PIL import Image

from .mesh_assets import convert_glb
from .nodes import _text
from .worldlabs import WorldLabs, download_asset

VERSION = "spatial-v2"


class SpatialPreparation:
    def __init__(self, settings, study, cache_dir=None, api=None):
        self.settings, self.study = settings, study
        self.api = api or WorldLabs(settings.worldlabs_key, settings.worldlabs_model,
                                   allow_generation=settings.world_generation_enabled)
        self.root = Path(cache_dir or Path(__file__).resolve().parent.parent / ".cache" / "geometry")
        self.tasks = {}; self.limit = asyncio.Semaphore(1)
        self.demo_plan = None
        self.collection = None
        self.enabled = settings.mode == "live" and settings.world_provider != "orbis" and (
            settings.world_provider == "worldlabs" or bool(settings.worldlabs_key))

    def key(self, context):
        return hashlib.sha256((VERSION + self.settings.worldlabs_model + context).encode()).hexdigest()

    @property
    def pinned_key(self):
        return self.key("existing-world:" + self.settings.world_test_world_id)

    def _folder(self, key):
        if not re.fullmatch(r"[a-f0-9]{64}", key): raise ValueError("Invalid world ID.")
        return self.root / key

    def status(self, key):
        if self.collection is not None and not self.collection.contains_key(key): raise FileNotFoundError("World is not in this demo.")
        folder = self._folder(key)
        if not (folder / "status.json").exists(): raise FileNotFoundError("World job not found.")
        result = json.loads((folder / "status.json").read_text())
        result["cacheKey"] = key; result["projection"] = "geometry"
        if self.demo_plan is not None and result.get("state") == "ready": result["plan"] = self.demo_plan
        return result

    def _save(self, folder, name, value):
        temp = folder / (name + ".tmp")
        temp.write_text(json.dumps(value)); temp.replace(folder / name)

    async def prepare(self, payload):
        if self.collection is not None: return self.collection.prepare(payload, self)
        topic = _text(payload.get("topic"), 160)
        context = _text(payload.get("context", topic), 8000)
        if not topic or not context: raise ValueError("A topic and its context are required.")
        key = self.key(context); folder = self._folder(key)
        folder.mkdir(parents=True, exist_ok=True)
        if (folder / "status.json").exists() and self.status(key)["state"] == "ready":
            return self.status(key)
        if key not in self.tasks:
            if len(self.tasks) >= 4: raise RuntimeError("Several 3D worlds are preparing. Try again shortly.")
            self._save(folder, "request.json", {"topic": topic, "context": context})
            self._save(folder, "status.json", {"state": "preparing", "notice": "Preparing a 3D world. You can return while it builds."})
            task = asyncio.create_task(self._prepare(topic, context, key))
            self.tasks[key] = task
            task.add_done_callback(lambda done: self.tasks.pop(key, None))
        return self.status(key)

    async def _prepare(self, topic, context, key):
        folder = self._folder(key)
        def progress(text): self._save(folder, "status.json", {"state": "preparing", "notice": text})
        async def operation(name, create):
            path = folder / name
            if path.exists(): value = json.loads(path.read_text())
            else:
                value = await create(); self._save(folder, name, value)
            return await self.api.wait(value)
        try:
            async with self.limit:
                if self.settings.world_test_world_id:
                    shared = self._folder(self.pinned_key)
                    if not (shared / "manifest.json").exists():
                        raise RuntimeError("The single test world's textured mesh is still being prepared. Re-enter when the export is ready.")
                    manifest = json.loads((shared / "manifest.json").read_text())
                    manifest["basePath"] = "/world/assets/" + self.pinned_key + "/"
                    if (folder / "plan.json").exists(): plan = json.loads((folder / "plan.json").read_text())
                    else:
                        preview = await asyncio.to_thread(self.preview)
                        plan = await self.study.plan(topic, context, environment_image=preview.get("image", ""))
                        self._save(folder, "plan.json", plan)
                    self._save(folder, "status.json", {"state":"ready", "notice":"Ready in the saved test world.",
                        "environment":manifest, "plan":plan})
                    return
                if not self.settings.world_generation_enabled:
                    raise RuntimeError("New World Labs generation is disabled. Choose the saved test world.")
                if not (folder / "generation.json").exists():
                    credits = await self.api.request("GET", "/credits")
                    # Do not start a new world that cannot finish its first HQ export.
                    if credits.get("remaining_credits", 0) < 5080:
                        raise RuntimeError("A new textured 3D world needs about 5,080 World Labs API credits. Add credits or revisit a ready world.")
                progress("Generating the 3D environment. This can take several minutes; Y returns while it builds.")
                result = await operation("generation.json", lambda: self.api.generate(topic, context))
                world_id = result.get("world_id") or result.get("world", {}).get("world_id")
                world = await self.api.world(world_id); self._save(folder, "world.json", world)
                assets = world.get("assets") or {}
                for name, url in (("collider.glb", (assets.get("mesh") or {}).get("collider_mesh_url")),
                                  ("pano.png", (assets.get("imagery") or {}).get("pano_url"))):
                    if not url: raise RuntimeError("World Labs did not provide the required environment assets.")
                    if not (folder / name).exists(): await download_asset(url, folder / name)
                if not (folder / "plan.json").exists():
                    plan = await self.study.plan(topic, context)
                    self._save(folder, "plan.json", plan)
                progress("Building the textured 3D surfaces. The provider can take up to an hour; Y returns while it builds.")
                result = await operation("export.json", lambda: self.api.export(world_id))
                if not (folder / "world.glb").exists(): await download_asset(result["url"], folder / "world.glb")
                await self.import_assets(folder, key)
        except asyncio.CancelledError: raise
        except Exception as exc:
            # Only our fixed diagnostics leave the server; HTTP exceptions can contain signed URLs.
            detail = str(exc) if isinstance(exc, (RuntimeError, ValueError)) else "3D preparation failed. Re-enter to resume the saved job."
            detail = detail.replace(self.settings.worldlabs_key, "[redacted]") if self.settings.worldlabs_key else detail
            if "http" in detail: detail = "3D preparation failed. Re-enter to resume the saved job."
            self._save(folder, "status.json", {"state": "error", "notice": detail[:400]})

    async def import_assets(self, folder, key, texture_projection="atlas"):
        """Also used to import an explicitly requested verification job without regenerating it."""
        if texture_projection not in ("atlas", "panorama"): raise ValueError("Unsupported surface texture projection.")
        converted = folder / "assets"
        manifest_path = folder / "manifest.json"
        manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else None
        if manifest is None or manifest.get("textureProjection", "atlas") != texture_projection:
            visual = await asyncio.to_thread(convert_glb, folder / "world.glb", converted,
                panorama_path=folder / "pano.png" if texture_projection == "panorama" else None)
            collision = await asyncio.to_thread(convert_glb, folder / "collider.glb", converted, "collision", True)
            with Image.open(folder / "pano.png") as image:
                image.thumbnail((2048,1024)); image.convert("RGB").save(converted / "panorama.jpg", quality=90)
                average = image.resize((1,1)).convert("RGB").getpixel((0,0))
            scale = 1.0
            ground = 0.0
            if (folder / "world.json").exists():
                world = json.loads((folder / "world.json").read_text())
                semantics = ((world.get("assets") or {}).get("splats") or {}).get("semantics_metadata") or {}
                scale = float(semantics.get("metric_scale_factor", 1.0))
                ground = float(semantics.get("ground_plane_offset", 0.0))
                if not .05 <= scale <= 20: raise ValueError("Invalid world metric scale.")
                if not -20 <= ground <= 20: raise ValueError("Invalid world ground offset.")
            manifest = {"visual": visual, "collision": collision, "panorama": "panorama.jpg", "textureProjection":texture_projection, "metricScale":scale, "groundOffset":ground,
                        "ambient": {"r": average[0]/255, "g": average[1]/255, "b": average[2]/255, "a": 1}}
            self._save(folder, "manifest.json", manifest)
        manifest["basePath"] = "/world/assets/" + key + "/"
        plan = json.loads((folder / "plan.json").read_text()) if (folder / "plan.json").exists() else None
        self._save(folder, "status.json", {"state": "ready", "notice": "3D environment ready.",
            "environment": manifest, "plan": plan})
        return self.status(key)

    def asset(self, key, filename):
        if self.collection is not None and not self.collection.contains_key(key): raise FileNotFoundError("World is not in this demo.")
        if not re.fullmatch(r"(?:visual|collision)-[0-9]+\.(?:mesh|jpg)|panorama\.jpg", filename):
            raise ValueError("Invalid world asset.")
        path = self._folder(key) / "assets" / filename
        if not path.is_file(): raise FileNotFoundError("World asset not found.")
        return path

    def preview(self, node_id=""):
        """Read the existing world's panorama only. Never start a provider operation."""
        if self.collection is not None:
            entry = self.collection.entry(node_id)
            if not entry or not entry.get("cacheKey"): return {"image": ""}
            folder = self._folder(entry["cacheKey"])
        else:
            if not self.settings.world_test_world_id: return {"image": ""}
            folder = self._folder(self.pinned_key)
        path = folder / "pano.png"
        if not path.exists(): return {"image": ""}
        with Image.open(path) as image:
            image.thumbnail((1024,512)); output=io.BytesIO()
            image.convert("RGB").save(output,format="JPEG",quality=85)
        return {"image":base64.b64encode(output.getvalue()).decode()}

    async def close(self):
        tasks = list(self.tasks.values())
        for task in tasks: task.cancel()
        await asyncio.gather(*tasks, return_exceptions=True)
