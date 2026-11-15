"""Topic-independent scene preparation: a panoramic seed and a bounded study model.

OpenAI prepares the starting image and teaching geometry; Orbis remains the live
environment renderer. Blueprints contain data only, never executable model code.
"""
from __future__ import annotations

import asyncio
import base64
import hashlib
import io
import json
import math
from pathlib import Path

import httpx
from PIL import Image

from .nodes import _text

CACHE_VERSION = "orbis-surround-v2"
PANORAMA_STYLE = (
    "Create an equirectangular 360-degree by 180-degree spherical environment map for a VR skybox. "
    "This is a latitude-longitude texture, NOT a perspective photograph, fisheye or tiny planet. "
    "Full 360 longitude across the image, full 180 latitude vertically. Straight horizon at exactly "
    "50% image height. Left and right edges join seamlessly at the same rear-facing direction. "
    "Top edge is the zenith stretched across the full width, bottom edge is the nadir stretched "
    "across the full width. Camera at human eye height, level, in the center of the space. "
    "Photoreal materials, believable lighting, rich surroundings in all directions. Nearby ground "
    "is clear for standing. No text, labels, interface, frames, black borders or visible camera. "
    "The content description below supplies the scene, not the projection or camera framing. Scene: "
)
LIVE_PANORAMA = (
    "Maintain the supplied full spherical equirectangular environment-map projection: 360 degrees "
    "of longitude and 180 degrees of latitude, matching left/right seam, horizon halfway down, "
    "zenith along the top and nadir along the bottom. No perspective crop, fisheye, zoom, cut, "
    "autonomous pan, camera roll, titles or overlays. Keep the panoramic camera level at human "
    "eye height. Animate the surroundings naturally: subjects act, small details move and the "
    "environment continues living. Preserve established layout and subjects across instructions."
)


from .study_models import PLAN_SCHEMA, PLAN_INSTRUCTIONS, validate_plan, obj


class WorldPreparation:
    def __init__(self, settings, cache_dir=None):
        self.settings = settings
        self.cache_dir = Path(cache_dir or Path(__file__).resolve().parent.parent / ".cache" / "worlds")
        self._tasks = {}
        self._limit = asyncio.Semaphore(2)

    async def prepare(self, payload):
        topic = _text(payload.get("topic"), 160)
        context = _text(payload.get("context", topic), 8000)
        key = hashlib.sha256((CACHE_VERSION + self.settings.panorama_model + context).encode()).hexdigest()
        if key not in self._tasks:
            if len(self._tasks) >= 8:
                raise RuntimeError("Several worlds are preparing. Please try again shortly.")
            task = asyncio.create_task(self._prepare(topic, context, key))
            self._tasks[key] = task
            task.add_done_callback(lambda finished: self._tasks.pop(key, None))
        return await asyncio.shield(self._tasks[key])

    async def _prepare(self, topic, context, key):
        async with self._limit:
            self.cache_dir.mkdir(parents=True, exist_ok=True)
            plan_path, image_path = self.cache_dir / (key + ".json"), self.cache_dir / (key + ".jpg")
            async def model():
                if plan_path.exists():
                    try:
                        return validate_plan(json.loads(plan_path.read_text()))
                    except (ValueError, OSError):
                        pass
                plan = await self.plan(topic, context)
                plan_path.write_text(json.dumps(plan))
                return plan
            async def seed():
                if image_path.exists():
                    return image_path.read_bytes()
                if not self.settings.panorama_model:
                    raise RuntimeError("Panorama image generation is disabled.")
                image = await self.panorama(context)
                image_path.write_bytes(image)
                return image
            plan, image = await asyncio.gather(model(), seed(), return_exceptions=True)
            notices = []
            if isinstance(plan, BaseException):
                plan = None
                notices.append("The study model could not be prepared. You can still explore and speak.")
            if isinstance(image, BaseException):
                image = b""
                notices.append("Panoramic seed unavailable; trying Orbis directly.")
            return {"plan": plan, "image": base64.b64encode(image).decode(),
                    "projection": "panorama", "notice": " ".join(notices), "cacheKey": key}

    async def panorama(self, context):
        if not self.settings.openai_key:
            raise RuntimeError("OpenAI is not configured.")
        async with httpx.AsyncClient(timeout=150) as client:
            response = await client.post("https://api.openai.com/v1/images/generations",
                headers={"Authorization": "Bearer " + self.settings.openai_key},
                json={"model": self.settings.panorama_model, "prompt": PANORAMA_STYLE + context,
                      "size": "1536x768", "quality": "medium", "output_format": "jpeg", "n": 1})
        if response.status_code >= 400:
            raise RuntimeError(f"Panorama generation returned HTTP {response.status_code}.")
        try:
            image = Image.open(io.BytesIO(base64.b64decode(response.json()["data"][0]["b64_json"], validate=True)))
            if image.width * image.height > 8_500_000:
                raise ValueError("Oversized image")
            output = io.BytesIO()
            image.convert("RGB").save(output, format="JPEG", quality=90)
            return output.getvalue()
        except (KeyError, ValueError, OSError, IndexError):
            raise RuntimeError("Panorama generation returned an invalid image.") from None

    async def plan(self, topic, context, instruction="", current=None, environment_image=""):
        if not self.settings.openai_key:
            raise RuntimeError("OpenAI is not configured.")
        content = [{"type": "input_text", "text": json.dumps({"topic": topic, "context": context,
                    "instruction": instruction, "current": current})}]
        if environment_image:
            content.append({"type":"input_image", "image_url":"data:image/jpeg;base64," + environment_image,
                            "detail":"low"})
        instructions = PLAN_INSTRUCTIONS + (
            " A supplied image shows the surrounding haven. Objects float independently of its furnishings. "
            "Use clear contrast against that background. Retain the requested topic "
            "even if this reusable test environment depicts a different subject. Do not copy "
            "background furnishings into the model or claim they are interactive."
            if environment_image else "")
        async with httpx.AsyncClient(timeout=65) as client:
            response = await client.post("https://api.openai.com/v1/responses",
                headers={"Authorization": "Bearer " + self.settings.openai_key},
                json={"model": self.settings.node_model, "store": False,
                      "reasoning": {"effort": "none"}, "max_output_tokens": 12000,
                      "instructions": instructions,
                      "text": {"format": {"type": "json_schema", "name": "study_model",
                                             "strict": True, "schema": PLAN_SCHEMA}},
                      "input": [{"role":"user", "content":content}]})
        if response.status_code >= 400:
            raise RuntimeError(f"Study model returned HTTP {response.status_code}.")
        data = response.json()
        if data.get("status") in ("incomplete", "failed"):
            raise RuntimeError("Study model generation did not finish. Try again.")
        raw = "".join(part.get("text", "") for item in data.get("output", [])
                      for part in item.get("content", []) if part.get("type") == "output_text")
        try:
            return validate_plan(json.loads(raw))
        except (ValueError, TypeError, KeyError):
            raise RuntimeError("Study model was invalid. Your current model has been kept.") from None

    async def explore(self, topic, context, instruction, current=None, focused="", objects=None, state=None):
        if not self.settings.openai_key:
            raise RuntimeError("OpenAI is not configured. Existing objects still work offline.")
        schema = obj({"action": {"type": "string", "enum": ["add", "revise", "answer"]},
                      "answer": {"type": "string"}, "model": {"anyOf": [PLAN_SCHEMA, {"type": "null"}]}})
        instructions = PLAN_INSTRUCTIONS + """
You are the guide in this exploration space. Choose action from the user's intent:
add for wishing a new object into existence or comparing another representation; keep other objects.
revise for an explicit change to the selected object; preserve its identity and relevant structure.
answer for a question about the object/part or what just happened, without rebuilding geometry.
Some worlds deliberately have no learning object. When selectedObject is null, answer questions
about the world, its subject or its surroundings on the reading pane. Do not invent an object to
justify answering. Only add a model when the user actually asks for one. An ambiguous revision
with no selected object calls for a short clarification, not an unsolicited model.
For answer, model must be null. For add/revise, provide a complete model and a short helpful answer.
Answer in <=550 characters. Ground it in the current object, selected part and request, and explain
uncertainty. Do not pretend this illustration measured or simulated something it cannot.
Do not change subject merely because the reusable room or previous objects show another topic.
Never treat an ordinary question as permission to replace an object. No environment generation.
"""
        payload = {"topic": topic, "context": context, "request": instruction, "selectedObject": current,
                   "focusedPart": focused, "objectsInSpace": objects or [], "currentExplorationState": state or {}}
        async with httpx.AsyncClient(timeout=90) as client:
            response = await client.post("https://api.openai.com/v1/responses",
                headers={"Authorization": "Bearer " + self.settings.openai_key},
                json={"model": self.settings.node_model, "store": False, "reasoning": {"effort": "none"},
                      "max_output_tokens": 12000, "instructions": instructions,
                      "text": {"format": {"type": "json_schema", "name": "exploration_reply", "strict": True, "schema": schema}},
                      "input": json.dumps(payload)})
        if response.status_code >= 400:
            raise RuntimeError(f"Object exploration returned HTTP {response.status_code}.")
        data = response.json()
        if data.get("status") in ("incomplete", "failed"):
            raise RuntimeError("The object request did not finish. Please try again.")
        raw = "".join(part.get("text", "") for item in data.get("output", []) for part in item.get("content", [])
                      if part.get("type") == "output_text")
        try:
            reply = json.loads(raw)
            action = reply["action"]
            if action not in ("add", "revise", "answer"): raise ValueError()
            model = None if action == "answer" else validate_plan(reply["model"])
            return {"action": action, "answer": _text(reply.get("answer"), 800), "model": model}
        except (ValueError, TypeError, KeyError):
            raise RuntimeError("The object reply was invalid. Your objects have been kept.") from None

    async def close(self):
        tasks = list(self._tasks.values())
        for task in tasks:
            task.cancel()
        await asyncio.gather(*tasks, return_exceptions=True)
