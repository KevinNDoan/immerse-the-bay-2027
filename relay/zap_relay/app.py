"""FastAPI application: speech, node and spatial preparation routes, plus world transport."""

from __future__ import annotations

import asyncio
import json
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

import httpx
from fastapi import FastAPI, Request, HTTPException
from fastapi.responses import FileResponse

from .config import Settings, load_settings
from .nodes import SpeechService, NodeService, MAX_AUDIO_BYTES, _text
from .spatial import SpatialPreparation
from .world import World, register_world_routes
from .worlds import WorldPreparation, validate_plan

__all__ = ["World", "create_app"]


def create_app(settings: Settings | None = None) -> FastAPI:
    configured = settings or load_settings()
    world = World(configured)
    speech = SpeechService(configured.speech_model)
    nodes = NodeService(configured)
    preparation = WorldPreparation(configured)
    spatial = SpatialPreparation(configured, preparation)
    speech_lock = asyncio.Lock()

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        app.state.world = world
        try:
            yield
        finally:
            await world.kill()
            await preparation.close()
            await spatial.close()

    app = FastAPI(lifespan=lifespan)
    app.state.world = world
    app.state.speech = speech
    app.state.nodes = nodes
    app.state.preparation = preparation
    app.state.spatial = spatial

    @app.post("/world/prepare")
    async def prepare_world(request: Request) -> dict:
        try:
            payload = json.loads(await _bounded_body(request, 32000))
            if not isinstance(payload, dict):
                raise ValueError("Expected a JSON object.")
            return await (spatial.prepare(payload) if spatial.enabled else preparation.prepare(payload))
        except ValueError as exc:
            raise HTTPException(400, str(exc)) from None
        except (RuntimeError, httpx.HTTPError):
            raise HTTPException(503, "World preparation is unavailable. Please try again.") from None

    @app.get("/world/jobs/{key}")
    async def world_job(key: str) -> dict:
        try: return spatial.status(key)
        except ValueError: raise HTTPException(400, "Invalid world job.") from None
        except FileNotFoundError: raise HTTPException(404, "World job not found.") from None

    @app.get("/world/preview")
    async def world_preview(nodeId: str = "") -> dict:
        return await asyncio.to_thread(spatial.preview, nodeId)

    @app.get("/world/assets/{key}/{filename}")
    async def world_asset(key: str, filename: str):
        try: return FileResponse(spatial.asset(key, filename))
        except ValueError: raise HTTPException(400, "Invalid world asset.") from None
        except FileNotFoundError: raise HTTPException(404, "World asset not found.") from None

    @app.post("/world/study")
    async def update_study(request: Request) -> dict:
        try:
            payload = json.loads(await _bounded_body(request, 64000))
            if not isinstance(payload, dict):
                raise ValueError("Expected a JSON object.")
            current = payload.get("current")
            if current is not None:
                current = validate_plan(current)
            preview = await asyncio.to_thread(spatial.preview) if spatial.enabled else {}
            return await preparation.plan(_text(payload.get("topic"), 160),
                _text(payload.get("context"), 8000), _text(payload.get("instruction"), 2000), current,
                environment_image=preview.get("image", ""))
        except ValueError as exc:
            raise HTTPException(400, str(exc)) from None
        except (RuntimeError, httpx.HTTPError):
            raise HTTPException(503, "The study model could not be updated. Your current model is still available.") from None

    @app.get("/capabilities")
    async def capabilities() -> dict:
        import importlib.util
        return {"orbisMode": configured.mode, "orbisConfigured": bool(configured.api_key),
                "worldProvider": "worldlabs" if spatial.enabled else "orbis",
                "speechInstalled": importlib.util.find_spec("faster_whisper") is not None,
                "speechModel": configured.speech_model, "nodeAiConfigured": bool(configured.openai_key)}

    @app.post("/world/explore")
    async def explore_objects(request: Request) -> dict:
        try:
            payload = json.loads(await _bounded_body(request, 160000))
            if not isinstance(payload, dict): raise ValueError("Expected a JSON object.")
            current = payload.get("current")
            if current is not None: current = validate_plan(current)
            objects = payload.get("objects", [])
            if not isinstance(objects, list) or len(objects) > 6: raise ValueError("Invalid object collection.")
            from .study_models import number
            state = {"step": int(number(payload.get("step", -1), -1, 4)), "parameter": number(payload.get("parameter", 0), 0, 1)}
            result = await preparation.explore(_text(payload.get("topic"), 160), _text(payload.get("context"), 8000),
                _text(payload.get("instruction"), 2000), current, _text(payload.get("focused", ""), 150, required=False),
                [_text(title, 120) for title in objects], state)
            if result["action"] == "add" and len(objects) >= 6:
                return {"action": "answer", "answer": "This space has six objects. You can keep exploring or ask me to change the selected object.", "model": None}
            return result
        except ValueError as exc:
            raise HTTPException(400, str(exc)) from None
        except (RuntimeError, httpx.HTTPError):
            raise HTTPException(503, "That object request could not finish. Check OpenAI on the laptop or try again; your existing objects are kept.") from None

    @app.post("/speech/transcribe")
    async def transcribe(request: Request) -> dict:
        wav = await _bounded_body(request, MAX_AUDIO_BYTES)
        if speech_lock.locked():
            raise HTTPException(429, "Another recording is processing. Try again in a moment.")
        async with speech_lock:
            try:
                return {"text": await asyncio.to_thread(app.state.speech.transcribe, wav)}
            except ValueError as exc:
                raise HTTPException(400, str(exc)) from None
            except Exception:
                raise HTTPException(503, "Local speech is unavailable. Install the speech extra and warm the model using the relay README.") from None

    @app.post("/nodes/update")
    async def update_node(request: Request) -> dict:
        try:
            payload = json.loads(await _bounded_body(request, 16000))
            if not isinstance(payload, dict):
                raise ValueError("Expected a JSON object.")
            return await app.state.nodes.update(payload)
        except ValueError as exc:
            raise HTTPException(400, str(exc)) from None
        except RuntimeError as exc:
            raise HTTPException(503, str(exc)) from None
        except httpx.HTTPError:
            raise HTTPException(503, "Node AI could not be reached. Your note was not changed.") from None

    register_world_routes(app, world)
    return app


async def _bounded_body(request: Request, limit: int) -> bytes:
    chunks = bytearray()
    async for chunk in request.stream():
        chunks.extend(chunk)
        if len(chunks) > limit:
            raise HTTPException(413, "Request is too large.")
    return bytes(chunks)
