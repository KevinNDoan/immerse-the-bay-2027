"""Video/audio sources for the stand-in and live Orbis modes."""

from __future__ import annotations

import asyncio
import base64
import io
import math
import time
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
    """Receive Orbis media through reactor-sdk 1.14.x."""

    def __init__(
        self,
        api_key: str,
        model: str,
        on_video: VideoCallback,
        on_audio: AudioCallback,
        on_error: ErrorCallback,
    ) -> None:
        self._api_key = api_key
        self._model = model
        self._on_video = on_video
        self._on_audio = on_audio
        self._on_error = on_error
        self._reactor: object | None = None
        self._loop: asyncio.AbstractEventLoop | None = None
        self._video_seq = 0
        self._audio_seq = 0

    async def start(self, topic: str, prompt: str, image: str) -> None:
        self._loop = asyncio.get_running_loop()
        self._video_seq = 0
        self._audio_seq = 0
        if self._reactor is None:
            from reactor_sdk import Reactor

            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            reactor = Reactor(model_name=self._model, api_key=self._api_key)
            self._reactor = reactor
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            video = reactor.track("main_video")
            # https://docs.reactor.inc/sdk-reference/python/track.md
            video.on_frame(self._receive_video)
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            audio = reactor.track("main_audio")
            # https://docs.reactor.inc/sdk-reference/python/track.md
            audio.on_frame(self._receive_audio)
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            reactor.on_error(self._receive_error)
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            await reactor.connect()
        else:
            reactor = self._reactor
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            await reactor.send_command("reset", {})  # type: ignore[attr-defined]  # UNVERIFIED

        # https://docs.reactor.inc/sdk-reference/python/reactor.md
        await reactor.send_command(  # type: ignore[attr-defined]
            "set_resolution", {"width": WIDTH, "height": HEIGHT}
        )  # UNVERIFIED
        # https://docs.reactor.inc/sdk-reference/python/reactor.md
        await reactor.send_command(  # type: ignore[attr-defined]
            "set_audio_enabled", {"enabled": True}
        )  # UNVERIFIED
        if image:
            image_bytes = base64.b64decode(image, validate=True)
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            uploaded = await reactor.upload_file(  # type: ignore[attr-defined]
                image_bytes, name="initial.jpg", mime_type="image/jpeg"
            )
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            await reactor.send_command("set_image", {"image": uploaded})  # type: ignore[attr-defined]
        # https://docs.reactor.inc/sdk-reference/python/reactor.md
        await reactor.send_command("set_prompt", {"prompt": prompt})  # type: ignore[attr-defined]
        # https://docs.reactor.inc/sdk-reference/python/reactor.md
        await reactor.send_command("start", {})  # type: ignore[attr-defined]  # UNVERIFIED

    async def steer(self, prompt: str) -> int:
        reactor = self._require_reactor()
        applies_at = self._video_seq + 33
        # https://docs.reactor.inc/sdk-reference/python/reactor.md
        await reactor.send_command("set_prompt", {"prompt": prompt})  # type: ignore[attr-defined]
        return applies_at

    async def stop(self) -> None:
        if self._reactor is not None:
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            await self._reactor.send_command("pause", {})  # type: ignore[attr-defined]  # UNVERIFIED

    async def close(self) -> None:
        reactor, self._reactor = self._reactor, None
        if reactor is None:
            return
        try:
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            await reactor.disconnect()  # type: ignore[attr-defined]
        finally:
            # https://docs.reactor.inc/sdk-reference/python/reactor.md
            reactor.close()  # type: ignore[attr-defined]

    def _require_reactor(self) -> object:
        if self._reactor is None:
            raise RuntimeError("Orbis session is not connected")
        return self._reactor

    def _receive_video(self, frame: np.ndarray, *args: object) -> None:
        output = io.BytesIO()
        Image.fromarray(frame, mode="RGB").save(output, format="JPEG", quality=80)
        seq = self._video_seq
        self._video_seq += 1
        self._schedule(self._on_video(output.getvalue(), seq))

    def _receive_audio(self, frame: np.ndarray, sample_rate: int, channels: int) -> None:
        if sample_rate != 48_000 or channels != 1:
            self._schedule(self._on_error(f"unexpected Orbis audio format: {sample_rate} Hz/{channels}ch"))
            return
        seq = self._audio_seq
        self._audio_seq += 1
        self._schedule(self._on_audio(frame.astype("<i2", copy=False).tobytes(), seq))

    def _receive_error(self, error: Exception) -> None:
        self._schedule(self._on_error(str(error)))

    def _schedule(self, awaitable: Awaitable[None]) -> None:
        if self._loop is None:
            return
        asyncio.run_coroutine_threadsafe(awaitable, self._loop)


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
