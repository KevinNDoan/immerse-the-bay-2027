"""Resumable generated geometry jobs and local asset delivery for the Quest."""
from __future__ import annotations

import asyncio
import base64
import hashlib
import io
import json
import os
import re
from pathlib import Path

from PIL import Image

from .mesh_assets import convert_glb
from .splat_assets import convert_spz
from .nodes import _text
from .worldlabs import WorldLabs, download_asset

VERSION = "spatial-v2"


async def parallel(*operations):
    """Join independent work, cancelling and draining siblings on failure/shutdown."""
    tasks = [asyncio.create_task(operation) for operation in operations]
    try:
        return await asyncio.gather(*tasks)
    finally:
        for task in tasks:
            if not task.done(): task.cancel()
        await asyncio.gather(*tasks, return_exceptions=True)


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
        if self.collection is not None and not self.settings.world_generation_enabled and not self.collection.contains_key(key): raise FileNotFoundError("World is not in this demo.")
        folder = self._folder(key)
        if not (folder / "status.json").exists(): raise FileNotFoundError("World job not found.")
        result = json.loads((folder / "status.json").read_text())
        splat_manifest = folder / "splat-manifest.json"
        if self.settings.world_render_mode == "splats" and splat_manifest.exists():
            result["state"] = "ready"
            result["notice"] = "3D splat environment ready."
            result["environment"] = json.loads(splat_manifest.read_text())
            result["environment"]["basePath"] = "/world/assets/" + key + "/"
            for field in ("plan", "pane"):
                path = folder / (field + ".json")
                if path.exists(): result[field] = json.loads(path.read_text())
        result["cacheKey"] = key; result["projection"] = "geometry"
        if self.demo_plan is not None and result.get("state") == "ready": result["plan"] = self.demo_plan
        return result

    def _save(self, folder, name, value):
        temp = folder / (name + ".tmp")
        temp.write_text(json.dumps(value)); temp.replace(folder / name)

    async def _prepare_plan(self, folder, topic, context, **kwargs):
        """A learning object is optional; an unavailable planner must not strand a paid world."""
        path = folder / "plan.json"
        try:
            plan = json.loads(path.read_text()) if path.exists() else await self.study.plan(topic, context, **kwargs)
        except asyncio.CancelledError:
            raise
        except Exception:
            # Provider errors can contain credentials or URLs. Keep only a fixed, useful fallback.
            plan = None
        self._save(folder, "plan.json", plan)
        pane = None
        if plan is None:
            pane = {"title": topic, "text": "Your exploration space is ready. The initial learning object could not be prepared. "
                    "Look around, or hold left trigger to ask a question or wish an object into creation."}
            self._save(folder, "pane.json", pane)
        return plan, pane

    async def prepare(self, payload):
        if self.collection is not None and (self.collection.entry(payload.get("nodeId")) is not None
                or not self.settings.world_generation_enabled):
            return self.collection.prepare(payload, self)
        topic = _text(payload.get("topic"), 160)
        context = _text(payload.get("context", topic), 8000)
        if not topic or not context: raise ValueError("A topic and its context are required.")
        key = self.key(context); folder = self._folder(key)
        node_id = _text(payload.get("nodeId", ""), 160, required=False)
        if node_id:
            links = self._node_worlds(); links[node_id] = key
            self.root.mkdir(parents=True, exist_ok=True)
            self._save(self.root, "node-worlds.json", links)
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
        plan_task = None
        def progress(text): self._save(folder, "status.json", {"state": "preparing", "notice": text})
        async def operation(name, create):
            nonlocal plan_task
            path = folder / name
            uncertain = "A World Labs submission has an uncertain outcome. Check the saved operation with the provider before retrying; no duplicate request was sent."
            if path.exists():
                try: value = json.loads(path.read_text())
                except (OSError, ValueError): raise RuntimeError(uncertain) from None
                if not isinstance(value, dict) or not value.get("operation_id"):
                    raise RuntimeError(uncertain)
            else:
                # Persist intent before spending, including across cancellation or process failure.
                # Never repeat an ambiguous POST simply because its response was lost.
                try:
                    with path.open("x") as journal:
                        json.dump({"state": "submitting"}, journal)
                        journal.flush(); os.fsync(journal.fileno())
                    value = await create()
                    if not isinstance(value, dict) or not value.get("operation_id"):
                        raise RuntimeError(uncertain)
                    self._save(folder, name, value)
                except asyncio.CancelledError:
                    raise
                except Exception:
                    raise RuntimeError(uncertain) from None
            # Only start planning once generation is accepted/journalled. It can
            # finish while the provider builds, without delaying asset preparation.
            if name == "generation.json" and plan_task is None:
                plan_task = asyncio.create_task(self._prepare_plan(folder, topic, context))
            return await self.api.wait(value)
        try:
            async with self.limit:
                if self.settings.world_test_world_id:
                    shared = self._folder(self.pinned_key)
                    if not (shared / "manifest.json").exists():
                        raise RuntimeError("The single test world's textured mesh is still being prepared. Re-enter when the export is ready.")
                    manifest = json.loads((shared / "manifest.json").read_text())
                    manifest["basePath"] = "/world/assets/" + self.pinned_key + "/"
                    preview = {"image": ""}
                    if not (folder / "plan.json").exists():
                        try: preview = await asyncio.to_thread(self.preview)
                        except (OSError, ValueError): pass
                    plan, pane = await self._prepare_plan(folder, topic, context, environment_image=preview.get("image", ""))
                    self._save(folder, "status.json", {"state":"ready", "notice":"Ready in the saved test world.",
                        "environment":manifest, "plan":plan, "pane":pane})
                    return
                if not self.settings.world_generation_enabled:
                    raise RuntimeError("New World Labs generation is disabled. Choose the saved test world.")
                if not (folder / "generation.json").exists():
                    credits = await self.api.request("GET", "/credits")
                    # Check the complete selected path before any paid submission.
                    minimum = 1580 if self.settings.world_render_mode == "splats" else 5080
                    if credits.get("remaining_credits", 0) < minimum:
                        raise RuntimeError(f"A new 3D world needs about {minimum:,} World Labs API credits in this rendering mode. Add credits or revisit a ready world.")
                progress("Generating the 3D environment. This can take several minutes; Y returns while it builds.")
                result = await operation("generation.json", lambda: self.api.generate(topic, context))
                world_id = result.get("world_id") or result.get("world", {}).get("world_id")
                world = await self.api.world(world_id); self._save(folder, "world.json", world)
                assets = world.get("assets") or {}
                downloads = [("collider.glb", (assets.get("mesh") or {}).get("collider_mesh_url")),
                             ("pano.png", (assets.get("imagery") or {}).get("pano_url"))]
                if self.settings.world_render_mode == "splats":
                    quality = self.settings.world_splat_quality
                    downloads.append(("scene-" + quality + ".spz", ((assets.get("splats") or {}).get("spz_urls") or {}).get(quality)))
                if any(not url for _, url in downloads):
                    raise RuntimeError("World Labs did not provide the required environment assets.")
                await parallel(*(download_asset(url, folder / name) for name, url in downloads if not (folder / name).exists()))
                if self.settings.world_render_mode == "splats":
                    progress("Preparing the native 3D splats and walkable surfaces…")
                    manifest = await self.import_splats(folder, key, publish=False)
                    await plan_task
                    # The manifest is the ready marker: publish only once the
                    # initial learning object (or fallback pane) is also saved.
                    self._save(folder, "splat-manifest.json", manifest)
                    return
                progress("Building the textured 3D surfaces. The provider can take up to an hour; Y returns while it builds.")
                result = await operation("export.json", lambda: self.api.export(world_id))
                if not (folder / "world.glb").exists(): await download_asset(result["url"], folder / "world.glb")
                await plan_task
                await self.import_assets(folder, key)
        except asyncio.CancelledError: raise
        except Exception as exc:
            # Only our fixed diagnostics leave the server; HTTP exceptions can contain signed URLs.
            detail = str(exc) if isinstance(exc, (RuntimeError, ValueError)) else "3D preparation failed. Re-enter to resume the saved job."
            detail = detail.replace(self.settings.worldlabs_key, "[redacted]") if self.settings.worldlabs_key else detail
            if "http" in detail: detail = "3D preparation failed. Re-enter to resume the saved job."
            self._save(folder, "status.json", {"state": "error", "notice": detail[:400]})
        finally:
            if plan_task is not None:
                if not plan_task.done(): plan_task.cancel()
                await asyncio.gather(plan_task, return_exceptions=True)

    async def import_splats(self, folder, key, publish=True):
        """Import an existing world's mobile cloud. GET/downloads only, never export."""
        world = json.loads((folder / "world.json").read_text())
        splats = (world.get("assets") or {}).get("splats") or {}
        urls = splats.get("spz_urls") or {}
        quality = self.settings.world_splat_quality
        source = folder / ("scene-" + quality + ".spz")
        if not source.exists():
            url = urls.get(quality)
            if not url: raise RuntimeError("This world has no mobile splat asset. No textured export was started.")
            await download_asset(url, source)
        converted = folder / "assets"
        cloud = await asyncio.to_thread(convert_spz, source, converted)
        collision = await asyncio.to_thread(convert_glb, folder / "collider.glb", converted, "collision", True)
        with Image.open(folder / "pano.png") as image:
            image.thumbnail((2048,1024)); image.convert("RGB").save(converted / "panorama.jpg", quality=90)
            average = image.resize((1,1)).convert("RGB").getpixel((0,0))
        semantics = splats.get("semantics_metadata") or {}
        scale = float(semantics.get("metric_scale_factor", 1))
        ground = float(semantics.get("ground_plane_offset", 0))
        if not .05 <= scale <= 20 or not -20 <= ground <= 20: raise ValueError("Invalid world metric scale.")
        fallback = json.loads((folder / "manifest.json").read_text()).get("visual",[]) if (folder / "manifest.json").exists() else []
        manifest = {"splats": cloud, "visual": fallback, "collision": collision, "panorama":"panorama.jpg",
                    "metricScale":scale,"groundOffset":ground,
                    "ambient":dict(zip(("r","g","b","a"), [v/255 for v in average]+[1]))}
        if not publish: return manifest
        self._save(folder,"splat-manifest.json",manifest)
        if not (folder / "status.json").exists(): self._save(folder,"status.json",{"state":"ready"})
        return self.status(key)

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
        pane = json.loads((folder / "pane.json").read_text()) if (folder / "pane.json").exists() else None
        self._save(folder, "status.json", {"state": "ready", "notice": "3D environment ready.",
            "environment": manifest, "plan": plan, "pane": pane})
        return self.status(key)

    def asset(self, key, filename):
        if self.collection is not None and not self.settings.world_generation_enabled and not self.collection.contains_key(key): raise FileNotFoundError("World is not in this demo.")
        if not re.fullmatch(r"(?:visual|collision)-[0-9]+\.(?:mesh|jpg)|panorama\.jpg|scene\.splat", filename):
            raise ValueError("Invalid world asset.")
        path = self._folder(key) / "assets" / filename
        if not path.is_file(): raise FileNotFoundError("World asset not found.")
        return path

    def _node_worlds(self):
        try:
            value = json.loads((self.root / "node-worlds.json").read_text())
            return value if isinstance(value, dict) else {}
        except (OSError, ValueError): return {}

    def preview(self, node_id=""):
        """Read the existing world's panorama only. Never start a provider operation."""
        if self.collection is not None:
            entry = self.collection.entry(node_id)
            key = entry.get("cacheKey") if entry else self._node_worlds().get(node_id)
            if not key: return {"image": ""}
            folder = self._folder(key)
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
