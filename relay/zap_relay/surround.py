"""Four image-anchored Orbis views around one shared panoramic seed.

This is a composite generated environment, not a geometric/stereo simulation.
Sky/ground outside the live camera fields retain the original panorama.
"""
from __future__ import annotations

import asyncio
import base64
import contextlib
import io
import math
import re
import time

import numpy as np
from PIL import Image

from .sources import ReactorSource

VIEW_FOV = 110
VIEW_WIDTH, VIEW_HEIGHT = 960, 540
VIEW_NAMES = ("front", "right", "back", "left")
PERSPECTIVE_CONTRACT = (
    "This is one fixed rectilinear wide-angle view in an immersive environment. Preserve the "
    "reference camera position, field of view, horizon and composition. No independent camera "
    "movement, pan, zoom, roll, crop, cut, text or overlays. Animate people, creatures, weather "
    "and appropriate small details naturally. Preserve the established space and identities. "
    "Only move the viewpoint when an explicit movement instruction is supplied."
)


def perspective(panorama: Image.Image, yaw: float, width=VIEW_WIDTH, height=VIEW_HEIGHT):
    """Render rays into a latitude/longitude texture; no image-generation guesswork."""
    source = np.asarray(panorama.convert("RGB"))
    x = ((np.arange(width) + .5) / width * 2 - 1) * math.tan(math.radians(VIEW_FOV / 2))
    y = (1 - (np.arange(height) + .5) / height * 2) * math.tan(math.radians(VIEW_FOV / 2)) * height / width
    xx, yy = np.meshgrid(x, y)
    longitude = np.arctan2(xx, np.ones_like(xx)) + math.radians(yaw)
    latitude = np.arctan2(yy, np.sqrt(1 + xx * xx))
    u = ((longitude / (2 * math.pi) + .5) % 1) * source.shape[1] - .5
    v = np.clip((.5 - latitude / math.pi) * source.shape[0] - .5, 0, source.shape[0] - 1)
    x0, y0 = np.floor(u).astype(int), np.floor(v).astype(int)
    fx, fy = (u - x0)[..., None], (v - y0)[..., None]
    x1, y1 = (x0 + 1) % source.shape[1], np.minimum(y0 + 1, source.shape[0] - 1)
    x0 %= source.shape[1]
    pixels = ((source[y0, x0] * (1-fx) + source[y0, x1] * fx) * (1-fy) +
              (source[y1, x0] * (1-fx) + source[y1, x1] * fx) * fy)
    return Image.fromarray(np.clip(pixels, 0, 255).astype(np.uint8))


def jpeg(image):
    output = io.BytesIO(); image.save(output, format="JPEG", quality=85)
    return output.getvalue()


class SurroundSource:
    projection = "multiview"

    def __init__(self, api_key, model, on_video, on_audio, on_error, source_factory=ReactorSource):
        self.api_key, self.model = api_key, model
        self.on_video, self.on_audio, self.on_error = on_video, on_audio, on_error
        self.factory = source_factory
        self.sources = []
        self.frames = [None] * 4
        self.live_views = 0
        self.failures = {}
        self._active = False
        self._pump = None
        self._seq = 0
        self._changed = False
        self._last = [0.] * 4
        self._starts = []
        self._ready = asyncio.Event()
        self._started = False

    async def start(self, topic, prompt, image):
        await self.stop()
        reusable = dict(self.sources)
        self.sources = []
        self.live_views = 0; self.failures = {}; self._seq = 0
        self.frames = [None] * 4; self._last = [0.] * 4
        self._active = True
        self._ready = asyncio.Event(); self._started = False
        seed = Image.open(io.BytesIO(base64.b64decode(image))).convert("RGB") if image else None
        async def start_view(index):
            async def video(data, seq):
                if not self._active: return
                frame = await asyncio.to_thread(lambda: Image.open(io.BytesIO(data)).convert("RGB").resize((VIEW_WIDTH, VIEW_HEIGHT)))
                if not self._active: return
                self.frames[index] = frame
                self.failures.pop(index, None)
                self.live_views |= 1 << index
                self._last[index] = time.monotonic()
                self._changed = True
                self._ready.set()
            async def audio(data, seq):
                if index == 0 and self._active: await self.on_audio(data, seq)
            async def error(detail):
                self.failures[index] = detail
                self.live_views &= ~(1 << index)
                self._changed = True
                if len(self.failures) == 4: self._ready.set()
                # Keep healthy directions alive if one deployment loses capacity.
            source = reusable.get(index)
            if source is None:
                source = self.factory(self.api_key, self.model, video, audio, error,
                                      max_width=VIEW_WIDTH, resolution="1080p", audio_enabled=index == 0)
            self.sources.append((index, source))
            view_image = ""
            if seed is not None:
                view = await asyncio.to_thread(perspective, seed, index * 90)
                view_image = base64.b64encode(jpeg(view)).decode()
            await asyncio.sleep(index * .75)  # Avoid a burst of four session/token allocations.
            for attempt in range(3):
                if not self._active: return
                try:
                    self.failures.pop(index, None)
                    await source.start(topic, self._prompt(prompt, index), view_image)
                    await asyncio.sleep(.1)
                    if index in self.failures:
                        raise RuntimeError(self.failures[index])
                    return
                except asyncio.CancelledError:
                    raise
                except Exception as exc:
                    detail = str(exc).replace(self.api_key, "[redacted]")
                    match = re.search(r'retry_after_seconds[^0-9]+([0-9]+)', detail)
                    detail = re.sub(r"https?://\S+|[A-Za-z0-9_-]{40,}", "[redacted]", detail)[:500]
                    await error(type(exc).__name__ + ": " + detail)
                    await source.close()
                    if attempt < 2:
                        # A provider retry hint is useful; cap this bounded startup retry.
                        delay = min(20, max(3, int(match.group(1)) + 1)) if match else 3 + attempt * 4
                        await asyncio.sleep(delay)
        self._pump = asyncio.create_task(self._stream(), name="surround-atlas")
        self._starts = [asyncio.create_task(start_view(index)) for index in range(4)]
        deadline = time.monotonic() + 120
        while not self.live_views:
            if time.monotonic() >= deadline:
                raise RuntimeError("No Orbis views arrived within two minutes.")
            if all(task.done() for task in self._starts) and len(self.failures) == 4:
                raise RuntimeError("No Orbis views are available.")
            self._ready.clear()
            try:
                await asyncio.wait_for(self._ready.wait(), 1)
            except asyncio.TimeoutError:
                pass
        # Let healthy views and steering run while slower directions connect.
        self._started = True

    def _prompt(self, prompt, index):
        return (f"SCENE AND REQUEST: {prompt}\nVIEW: You face the {VIEW_NAMES[index]} direction of the same environment. "
                f"This view is {index * 90} degrees clockwise from the initial front direction. "
                + PERSPECTIVE_CONTRACT)

    async def steer(self, prompt):
        results = await asyncio.gather(*(source.steer(self._prompt(prompt, index))
            for index, source in self.sources if self.live_views & (1 << index)), return_exceptions=True)
        if not results or all(isinstance(result, BaseException) for result in results):
            raise RuntimeError("No live Orbis view could accept the instruction.")
        return self._seq + 24  # About two seconds at the atlas cadence; acknowledgement only.

    async def _stream(self):
        try:
            while self._active:
                if (self._started and len(self.failures) == 4 and self.live_views == 0
                        and all(task.done() for task in self._starts)):
                    raise RuntimeError("All views are unavailable.")
                for index, last in enumerate(self._last):
                    if last and time.monotonic() - last > 12:
                        self.live_views &= ~(1 << index)
                if self._changed and self.live_views:
                    self._changed = False
                    frames = list(self.frames)
                    data = await asyncio.to_thread(self._atlas, frames)
                    if self._active:
                        await self.on_video(data, self._seq); self._seq += 1
                await asyncio.sleep(1 / 12)
        except asyncio.CancelledError:
            raise
        except Exception:
            await self.on_error("Surround video could not be composed.")

    @staticmethod
    def _atlas(frames):
        atlas = Image.new("RGB", (VIEW_WIDTH * 2, VIEW_HEIGHT * 2))
        for index, frame in enumerate(frames):
            if frame is not None:
                atlas.paste(frame, ((index % 2) * VIEW_WIDTH, (index // 2) * VIEW_HEIGHT))
        return jpeg(atlas)

    async def stop(self):
        self._active = False
        starts, self._starts = self._starts, []
        for start in starts:
            if not start.done(): start.cancel()
        await asyncio.gather(*starts, return_exceptions=True)
        task, self._pump = self._pump, None
        if task is not None and task is not asyncio.current_task():
            task.cancel()
            with contextlib.suppress(asyncio.CancelledError): await task
        await asyncio.gather(*(source.stop() for _, source in self.sources), return_exceptions=True)

    async def close(self):
        await self.stop()
        sources, self.sources = self.sources, []
        await asyncio.gather(*(source.close() for _, source in sources), return_exceptions=True)
