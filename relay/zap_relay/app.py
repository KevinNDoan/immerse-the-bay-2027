"""FastAPI application implementing the relay protocol."""

from __future__ import annotations

import asyncio
import contextlib
import json
import struct
import time
import httpx
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from fastapi import FastAPI, WebSocket, WebSocketDisconnect, Request, HTTPException
from fastapi.responses import FileResponse, JSONResponse, StreamingResponse

from .config import Settings, load_settings
from .sources import ReactorSource, StandinSource, WorldSource
from .nodes import SpeechService, NodeService, MAX_AUDIO_BYTES

HEADER = struct.Struct("<BIQ")
STATIC_INDEX = Path(__file__).with_name("static") / "index.html"


@dataclass(eq=False)
class Client:
    websocket: WebSocket
    video_frames: asyncio.Queue[tuple[int, bytes]] = field(
        default_factory=lambda: asyncio.Queue(maxsize=1)
    )
    audio_frames: asyncio.Queue[tuple[int, bytes]] = field(
        default_factory=lambda: asyncio.Queue(maxsize=32)
    )
    controls: asyncio.Queue[dict[str, Any]] = field(default_factory=asyncio.Queue)


class World:
    def __init__(self, settings: Settings) -> None:
        self.settings = settings
        self.state = "idle"
        self.detail = ""
        self.prompt = ""
        self.topic = ""
        self.source: WorldSource | None = None
        self.clients: set[Client] = set()
        self.latest_jpeg: bytes | None = None
        self.frame_version = 0
        self.frame_changed = asyncio.Condition()
        self.lock = asyncio.Lock()
        self.close_task: asyncio.Task[None] | None = None
        self.generation = 0
        self.scene_prompt = ""
        self.camera_prompt = ""
        self.event_prompt = ""
        self.owner: Client | None = None
        self.enter_task: asyncio.Task | None = None

    async def begin_enter(self, topic: str, prompt: str, image: str) -> None:
        await self._cancel_enter()
        async def start():
            try:
                await self.enter(topic, prompt, image)
            except asyncio.CancelledError:
                raise
            except Exception:
                pass  # enter broadcasts the failure; don't leak an unobserved task exception.
        self.enter_task = asyncio.create_task(start(), name="world-enter")

    async def _cancel_enter(self):
        task, self.enter_task = self.enter_task, None
        if task is not None and task is not asyncio.current_task():
            task.cancel()
            with contextlib.suppress(asyncio.CancelledError, Exception):
                await task

    async def enter(self, topic: str, prompt: str, image: str) -> None:
        async with self.lock:
            self._cancel_delayed_close()
            self.generation += 1
            if self.source is not None and self.state in {"streaming", "connecting"}:
                await self.source.stop()
            if self.source is None:
                self.source = self._make_source()
            self.topic = topic
            self.scene_prompt = prompt or topic
            self.camera_prompt = self.event_prompt = ""
            self.prompt = self._compose_prompt()
            self.latest_jpeg = None
            for client in self.clients:
                _clear_queue(client.video_frames)
                _clear_queue(client.audio_frames)
            self.state = "connecting"
            await self._broadcast_control(self.status_message())
            try:
                await self.source.start(topic, self.prompt, image)
            except Exception as exc:
                source, self.source = self.source, None
                self.state = "error"
                self.detail = str(exc).replace(self.settings.api_key, "[redacted]") if self.settings.api_key else str(exc)
                await self._broadcast_control(self.status_message())
                if source is not None:
                    await source.close()
                raise
            self.detail = ""
            await self._broadcast_control({"t": "prompt", "text": self.prompt, "appliesAtSeq": 0})

    async def steer(self, kind: str, text: str) -> dict[str, Any]:
        async with self.lock:
            if self.source is None or self.state != "streaming":
                raise RuntimeError("no world is streaming")
            previous = (self.scene_prompt, self.camera_prompt, self.event_prompt)
            if kind == "scene":
                self.scene_prompt = text
                self.camera_prompt = self.event_prompt = ""
            elif kind in {"move", "look"}:
                self.camera_prompt = f"{kind}: {text}"
            else:
                self.event_prompt = f"{kind}: {text}"
            prompt = self._compose_prompt()
            try:
                applies_at = await self.source.steer(prompt)
            except Exception:
                self.scene_prompt, self.camera_prompt, self.event_prompt = previous
                raise
            self.prompt = prompt
            message = {"t": "prompt", "text": self.prompt, "appliesAtSeq": applies_at}
            await self._broadcast_control(message)
            return message

    def _compose_prompt(self) -> str:
        parts = [self.scene_prompt, self.camera_prompt, self.event_prompt]
        return "\n".join(part for part in parts if part) + "\na single unbroken take"

    async def exit(self) -> None:
        await self._cancel_enter()
        async with self.lock:
            self._cancel_delayed_close()
            self.generation += 1
            if self.source is not None:
                if self.state == "connecting":
                    source, self.source = self.source, None
                    await source.close()
                else:
                    try:
                        await self.source.stop()
                    except Exception:
                        source, self.source = self.source, None
                        await source.close()
            for client in self.clients:
                _clear_queue(client.video_frames)
                _clear_queue(client.audio_frames)
            self.state = "idle"
            self.detail = ""
            await self._broadcast_control(self.status_message())
            if self.settings.mode == "live" and self.source is not None:
                self.close_task = asyncio.create_task(self._close_after_delay())

    async def kill(self) -> None:
        await self._cancel_enter()
        async with self.lock:
            self._cancel_delayed_close()
            self.generation += 1
            source, self.source = self.source, None
            if source is not None:
                await source.close()
            for client in self.clients:
                _clear_queue(client.video_frames)
                _clear_queue(client.audio_frames)
            self.state = "idle"
            self.detail = ""
            await self._broadcast_control(self.status_message())

    async def add_client(self, websocket: WebSocket) -> Client:
        client = Client(websocket)
        self.clients.add(client)
        await client.controls.put(self.status_message())
        if self.prompt:
            await client.controls.put({"t": "prompt", "text": self.prompt, "appliesAtSeq": 0})
        return client

    async def remove_client(self, client: Client) -> None:
        self.clients.discard(client)

    async def on_video(self, jpeg: bytes, seq: int) -> None:
        if self.state not in {"connecting", "streaming"}:
            return
        if self.state == "connecting":
            self.state = "streaming"
            await self._broadcast_control(self.status_message())
        message = HEADER.pack(1, seq, time.monotonic_ns() // 1_000) + jpeg
        self.latest_jpeg = jpeg
        async with self.frame_changed:
            self.frame_version += 1
            self.frame_changed.notify_all()
        for client in tuple(self.clients):
            _replace_queued(client.video_frames, (self.generation, message))

    async def on_audio(self, pcm: bytes, seq: int) -> None:
        if self.state not in {"connecting", "streaming"}:
            return
        message = HEADER.pack(2, seq, time.monotonic_ns() // 1_000) + pcm
        for client in tuple(self.clients):
            _replace_queued(client.audio_frames, (self.generation, message))

    async def on_error(self, detail: str) -> None:
        async with self.lock:
            source, self.source = self.source, None
            self.state = "error"
            self.detail = detail
            await self._broadcast_control(self.status_message())
            if source is not None:
                await source.close()

    async def mjpeg(self) -> AsyncIterator[bytes]:
        seen = -1
        while True:
            async with self.frame_changed:
                await self.frame_changed.wait_for(lambda: self.frame_version != seen)
                seen = self.frame_version
                jpeg = self.latest_jpeg
            if jpeg is not None:
                yield b"--frame\r\nContent-Type: image/jpeg\r\n\r\n" + jpeg + b"\r\n"

    def health(self) -> dict[str, Any]:
        return {
            "ok": True,
            "mode": self.settings.mode,
            "state": self.state,
            "model": self.settings.model if self.settings.mode == "live" else "",
        }

    def status_message(self) -> dict[str, str]:
        return {"t": "status", "state": self.state, "detail": self.detail}

    def _make_source(self) -> WorldSource:
        args = (self.on_video, self.on_audio, self.on_error)
        if self.settings.mode == "live":
            return ReactorSource(self.settings.api_key, self.settings.model, *args,
                                 max_width=self.settings.max_width, resolution=self.settings.resolution)
        return StandinSource(*args)

    async def _broadcast_control(self, message: dict[str, Any]) -> None:
        for client in tuple(self.clients):
            await client.controls.put(message)

    async def _close_after_delay(self) -> None:
        try:
            await asyncio.sleep(45)
            async with self.lock:
                source, self.source = self.source, None
                if source is not None:
                    await source.close()
        except asyncio.CancelledError:
            pass

    def _cancel_delayed_close(self) -> None:
        if self.close_task is not None:
            self.close_task.cancel()
            self.close_task = None


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

    @app.get("/health")
    async def health() -> JSONResponse:
        return JSONResponse(world.health())

    @app.get("/")
    async def index() -> FileResponse:
        return FileResponse(STATIC_INDEX)

    @app.get("/stream.mjpg")
    async def stream() -> StreamingResponse:
        return StreamingResponse(
            world.mjpeg(), media_type="multipart/x-mixed-replace; boundary=frame"
        )

    @app.post("/world/kill")
    async def kill() -> JSONResponse:
        await world.kill()
        return JSONResponse({"ok": True})

    @app.websocket("/ws/world")
    async def websocket_endpoint(websocket: WebSocket) -> None:
        await websocket.accept()
        client = await world.add_client(websocket)
        sender = asyncio.create_task(_send_to_client(world, client), name="world-ws-sender")
        try:
            while True:
                packet = await websocket.receive()
                if packet["type"] == "websocket.disconnect":
                    break
                if packet.get("bytes") is not None:
                    await client.controls.put(_error("binary_not_allowed", "client messages must be JSON text"))
                    continue
                try:
                    message = json.loads(packet.get("text") or "")
                    await _handle_message(world, client, message)
                except (json.JSONDecodeError, TypeError, ValueError) as exc:
                    await client.controls.put(_error("bad_message", str(exc)))
                except RuntimeError as exc:
                    await client.controls.put(_error("invalid_state", str(exc)))
                except Exception:
                    await client.controls.put(_error("source_error", "World could not start. Check the relay configuration and connection."))
        except WebSocketDisconnect:
            pass
        finally:
            sender.cancel()
            with contextlib.suppress(asyncio.CancelledError, Exception):
                await sender
            await world.remove_client(client)
            if world.owner is client and world.state in {"streaming", "connecting"}:
                await world.exit()
                world.owner = None

    return app


async def _handle_message(world: World, client: Client, message: Any) -> None:
    if not isinstance(message, dict) or not isinstance(message.get("t"), str):
        raise ValueError("message must be an object with string field t")
    kind = message["t"]
    if kind == "enter":
        topic = _string(message, "topic")
        prompt = _string(message, "prompt")
        image = _string(message, "image")
        _string(message, "nodeId")
        world.owner = client
        await world.begin_enter(topic, prompt, image)
    elif kind == "steer":
        steer_kind = _string(message, "kind")
        if steer_kind not in {"move", "look", "event", "voice", "scene"}:
            raise ValueError("steer kind must be move, look, event or voice")
        await world.steer(steer_kind, _string(message, "text"))
    elif kind == "exit":
        await world.exit()
    elif kind == "ping":
        await client.controls.put({"t": "pong"})
    else:
        raise ValueError(f"unknown message type: {kind}")


async def _send_to_client(world: World, client: Client) -> None:
    try:
        while True:
            control = asyncio.create_task(client.controls.get())
            video = asyncio.create_task(client.video_frames.get())
            audio = asyncio.create_task(client.audio_frames.get())
            tasks = (control, video, audio)
            try:
                done, _ = await asyncio.wait(tasks, return_when=asyncio.FIRST_COMPLETED)
                if control in done:
                    await asyncio.wait_for(client.websocket.send_json(control.result()), 2)
                if video in done:
                    await _send_media_if_current(world, client, video.result())
                if audio in done:
                    await _send_media_if_current(world, client, audio.result())
            finally:
                for task in tasks:
                    if not task.done(): task.cancel()
                await asyncio.gather(*tasks, return_exceptions=True)
    except (TimeoutError, WebSocketDisconnect, OSError):
        with contextlib.suppress(Exception):
            await client.websocket.close()


async def _send_media_if_current(
    world: World, client: Client, media: tuple[int, bytes]
) -> None:
    generation, payload = media
    if generation == world.generation and world.state == "streaming":
        await asyncio.wait_for(client.websocket.send_bytes(payload), 2)


def _replace_queued(
    queue: asyncio.Queue[tuple[int, bytes]], value: tuple[int, bytes]
) -> None:
    if queue.full():
        with contextlib.suppress(asyncio.QueueEmpty):
            queue.get_nowait()
    queue.put_nowait(value)


def _clear_queue(queue: asyncio.Queue[tuple[int, bytes]]) -> None:
    while not queue.empty():
        with contextlib.suppress(asyncio.QueueEmpty):
            queue.get_nowait()


def _string(message: dict[str, Any], name: str) -> str:
    value = message.get(name)
    if not isinstance(value, str):
        raise ValueError(f"{name} must be a string")
    if len(value) > (3_000_000 if name == "image" else 4000):
        raise ValueError(f"{name} is too long")
    return value


def _error(code: str, detail: str) -> dict[str, str]:
    return {"t": "error", "code": code, "detail": detail}


async def _bounded_body(request: Request, limit: int) -> bytes:
    chunks = bytearray()
    async for chunk in request.stream():
        chunks.extend(chunk)
        if len(chunks) > limit:
            raise HTTPException(413, "Request is too large.")
    return bytes(chunks)
