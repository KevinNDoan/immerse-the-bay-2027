"""FastAPI application: speech and note routes, plus the world routes from world.py."""

from __future__ import annotations

import asyncio
import json
import httpx
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request, HTTPException

from .config import Settings, load_settings
from .nodes import SpeechService, NodeService, MAX_AUDIO_BYTES
from .world import World, register_world_routes

__all__ = ["World", "create_app"]


def create_app(settings: Settings | None = None) -> FastAPI:
    configured = settings or load_settings()
    world = World(configured)
    speech = SpeechService(configured.speech_model)
    nodes = NodeService(configured)
    speech_lock = asyncio.Lock()

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        app.state.world = world
        try:
            yield
        finally:
            await world.kill()

    app = FastAPI(lifespan=lifespan)
    app.state.world = world
    app.state.speech = speech
    app.state.nodes = nodes

    @app.get("/capabilities")
    async def capabilities() -> dict:
        import importlib.util
        return {"orbisMode": configured.mode, "orbisConfigured": bool(configured.api_key),
                "speechInstalled": importlib.util.find_spec("faster_whisper") is not None,
                "speechModel": configured.speech_model, "nodeAiConfigured": bool(configured.openai_key)}

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
