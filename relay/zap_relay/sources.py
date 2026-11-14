"""Video/audio sources for the stand-in and live Orbis modes."""

from __future__ import annotations

import asyncio
import base64
import io
import math
import time
import contextlib
import threading
from collections.abc import Awaitable, Callable
from typing import Protocol

import numpy as np
from PIL import Image, ImageDraw, ImageFont

WIDTH = 832
HEIGHT = 480
FPS = 18
STEER_DELAY_SECONDS = 1.8

VideoCallback = Callable[[bytes, int], Awaitable[None]]
AudioCallback = Callable[[bytes, int], Awaitable[None]]
ErrorCallback = Callable[[str], Awaitable[None]]


class WorldSource(Protocol):
    """The operations the relay needs from either media source."""

    async def start(self, topic: str, prompt: str, image: str) -> None: ...

    async def steer(self, prompt: str) -> int: ...

    async def stop(self) -> None: ...

    async def close(self) -> None: ...


class StandinSource:
    """Generate a visibly moving 832x480 JPEG stream without network access."""

    def __init__(
        self,
        on_video: VideoCallback,
        on_audio: AudioCallback,
        on_error: ErrorCallback,
    ) -> None:
        self._on_video = on_video
        self._topic = ""
        self._prompt = ""
        self._pending_prompt: tuple[str, int] | None = None
        self._next_seq = 0
        self._task: asyncio.Task[None] | None = None

    async def start(self, topic: str, prompt: str, image: str) -> None:
        await self.stop()
        self._topic = topic
        self._prompt = prompt
        self._pending_prompt = None
        self._next_seq = 0
        self._task = asyncio.create_task(self._run(), name="stand-in-frames")

    async def steer(self, prompt: str) -> int:
        applies_at = self._next_seq + math.ceil(STEER_DELAY_SECONDS * FPS)
        self._pending_prompt = (prompt, applies_at)
        return applies_at

    async def stop(self) -> None:
        task, self._task = self._task, None
        if task is not None:
            task.cancel()
            try:
                await task
            except asyncio.CancelledError:
                pass

    async def close(self) -> None:
        await self.stop()

    async def _run(self) -> None:
        interval = 1.0 / FPS
        deadline = time.monotonic()
        while True:
            seq = self._next_seq
            pending = self._pending_prompt
            if pending is not None and seq >= pending[1]:
                self._prompt = pending[0]
                self._pending_prompt = None
            jpeg = await asyncio.to_thread(self._draw_frame, seq)
            await self._on_video(jpeg, seq)
            self._next_seq += 1
            deadline += interval
            await asyncio.sleep(max(0.0, deadline - time.monotonic()))

    def _draw_frame(self, seq: int) -> bytes:
        image = Image.new("RGB", (WIDTH, HEIGHT), (7, 7, 12))
        draw = ImageDraw.Draw(image)
        font = ImageFont.load_default(size=24)
        small = ImageFont.load_default(size=17)
        x = 80 + int((WIDTH - 160) * ((math.sin(seq / 13.0) + 1.0) / 2.0))
        y = 85 + int(45 * math.cos(seq / 9.0))
        radius = 28 + int(6 * math.sin(seq / 5.0))
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill="white")
        draw.line((WIDTH // 2, HEIGHT - 58, x, y), fill=(105, 105, 120), width=2)
        draw.text((38, 35), self._topic or "Untitled world", fill="white", font=font)
        draw.text((38, 78), f"Frame {seq}", fill=(175, 175, 190), font=small)
        wrapped = _wrap(self._prompt, 72)
        draw.multiline_text((38, 300), wrapped, fill="white", font=small, spacing=7)
        output = io.BytesIO()
        image.save(output, format="JPEG", quality=80)
        return output.getvalue()


class ReactorSource:
    """Bounded latest-frame bridge; command shapes follow the Orbis schema."""

    def __init__(self, api_key, model, on_video, on_audio, on_error,
                 max_width=1280, resolution="", reactor_factory=None):
        self._api_key, self._model = api_key, model
        self._on_video, self._on_audio, self._on_error = on_video, on_audio, on_error
        self._max_width, self._resolution = max_width, resolution
        self._factory = reactor_factory
        self._reactor = None
        self._loop = None
        self._video_seq = self._audio_seq = 0
        self._active = False
        self._pending = None
        self._pending_audio = []
        self._frame_lock = threading.Lock()
        self._pump = None
        self._restart = None
        self.available_resolutions = []

    async def start(self, topic: str, prompt: str, image: str) -> None:
        self._loop = asyncio.get_running_loop()
        self._video_seq = self._audio_seq = 0
        if self._reactor is None:
            if self._factory is None:
                from reactor_sdk import Reactor
                self._factory = Reactor
            reactor = self._factory(model_name=self._model, api_key=self._api_key)
            self._reactor = reactor
            reactor.track("main_video").on_frame(self._receive_video)
            reactor.track("main_audio").on_frame(self._receive_audio)
            reactor.on_error(self._receive_error)
            reactor.on_message(self._receive_message)
            await asyncio.wait_for(reactor.connect(), 90)
        else:
            reactor = self._reactor
            await reactor.send_command("reset", {})
        if self._resolution:
            if self.available_resolutions and self._resolution not in self.available_resolutions:
                raise ValueError("ORBIS_RESOLUTION is not offered by this deployment")
            await reactor.send_command("set_resolution", {"resolution": self._resolution})
        # Sound is enabled by the model's default; do not send an unnecessary toggle.
        if image:
            image_bytes = base64.b64decode(image, validate=True)
            uploaded = await reactor.upload_file(image_bytes, name="initial.jpg", mime_type="image/jpeg")
            await reactor.send_command("set_image", {"image": uploaded})
        await reactor.send_command("set_prompt", {"prompt": prompt})
        self._active = True
        with self._frame_lock:
            self._pending = None
            self._pending_audio.clear()
        self._pump = asyncio.create_task(self._pump_media())
        await reactor.send_command("start", {})

    async def steer(self, prompt: str) -> int:
        if self._reactor is None or not self._active:
            raise RuntimeError("Orbis session is not connected")
        applies_at = self._video_seq + math.ceil(STEER_DELAY_SECONDS * FPS)
        await self._reactor.send_command("set_prompt", {"prompt": prompt})
        return applies_at

    async def stop(self) -> None:
        was_active = self._active
        self._active = False
        for task in (self._pump, self._restart):
            if task is not None and task is not asyncio.current_task():
                task.cancel()
                with contextlib.suppress(asyncio.CancelledError):
                    await task
        self._pump = self._restart = None
        with self._frame_lock:
            self._pending = None
            self._pending_audio.clear()
        if self._reactor is not None and was_active:
            await asyncio.wait_for(self._reactor.send_command("pause", {}), 10)

    async def close(self) -> None:
        with contextlib.suppress(Exception):
            await self.stop()
        reactor, self._reactor = self._reactor, None
        if reactor is not None:
            try:
                await asyncio.wait_for(reactor.disconnect(), 15)
            finally:
                reactor.close()

    def _receive_video(self, frame, *args):
        if not self._active:
            return
        frame = np.asarray(frame)
        if frame.ndim == 4:
            if not len(frame):
                return
            frame = frame[-1]  # Batch delivery: newest only, never queue old video.
        if frame.ndim != 3 or frame.shape[-1] != 3:
            self._receive_error(ValueError("Orbis returned an unsupported RGB frame shape"))
            return
        with self._frame_lock:
            self._pending = frame.copy()

    def _receive_audio(self, frame, sample_rate, channels):
        if not self._active:
            return
        if sample_rate != 48000 or channels != 1:
            self._receive_error(ValueError("Orbis audio must be 48000 Hz mono PCM"))
            return
        with self._frame_lock:
            self._pending_audio.append(np.asarray(frame).astype("<i2", copy=False).tobytes())
            self._pending_audio = self._pending_audio[-16:]

    async def _pump_media(self):
        last_frame = time.monotonic()
        try:
            while self._active:
                with self._frame_lock:
                    frame, self._pending = self._pending, None
                    audio, self._pending_audio = self._pending_audio, []
                if frame is not None:
                    jpeg = await asyncio.to_thread(self._encode, frame)
                    if not self._active:
                        return
                    await self._on_video(jpeg, self._video_seq)
                    self._video_seq += 1
                    last_frame = time.monotonic()
                for pcm in audio:
                    await self._on_audio(pcm, self._audio_seq)
                    self._audio_seq += 1
                if time.monotonic() - last_frame > 90:
                    raise RuntimeError("No Orbis video arrived for 90 seconds. Check capacity and the Reactor key.")
                await asyncio.sleep(1 / FPS)
        except asyncio.CancelledError:
            raise
        except Exception as exc:
            self._receive_error(exc)

    def _encode(self, frame):
        image = Image.fromarray(frame)
        if image.width > self._max_width:
            image = image.resize((self._max_width, round(image.height * self._max_width / image.width)), Image.Resampling.BILINEAR)
        output = io.BytesIO()
        image.save(output, format="JPEG", quality=80)
        return output.getvalue()

    def _receive_message(self, message):
        kind, data = message.get("type"), message.get("data") or {}
        if kind == "state":
            self.available_resolutions = data.get("available_resolutions", [])
        elif kind == "command_error":
            self._receive_error(RuntimeError(f"Orbis rejected {data.get('command', 'command')}: {data.get('reason', 'unknown error')}"))
        elif kind == "generation_complete" and self._active and self._loop:
            self._loop.call_soon_threadsafe(self._queue_restart)

    def _queue_restart(self):
        if self._active and (self._restart is None or self._restart.done()):
            self._restart = asyncio.create_task(self._restart_generation())

    async def _restart_generation(self):
        try:
            if self._active and self._reactor:
                await self._reactor.send_command("start", {})
        except Exception as exc:
            self._receive_error(exc)

    def _receive_error(self, error):
        if self._loop and not self._loop.is_closed():
            # SDK errors may contain credential-bearing URLs; redact the known key.
            detail = str(error)
            if self._api_key:
                detail = detail.replace(self._api_key, "[redacted]")
            asyncio.run_coroutine_threadsafe(self._on_error(detail), self._loop)


def _wrap(text: str, width: int) -> str:
    words = text.split()
    lines: list[str] = []
    current: list[str] = []
    for word in words:
        if current and len(" ".join((*current, word))) > width:
            lines.append(" ".join(current))
            current = []
        current.append(word)
    if current:
        lines.append(" ".join(current))
    return "\n".join(lines[:5])
