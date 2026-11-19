"""World Labs API adapter. Credentials stay on the relay, never in asset requests."""
from __future__ import annotations

import asyncio
import time
from urllib.parse import urlparse

import httpx

API = "https://api.worldlabs.ai/marble/v1"


class WorldLabs:
    def __init__(self, key, model="marble-1.1", client=None, allow_generation=False):
        self.key, self.model, self.client = key, model, client
        self.allow_generation = allow_generation

    async def request(self, method, path, payload=None):
        if method.upper() == "POST" and path.endswith((":generate", ":export")) and not self.allow_generation:
            raise RuntimeError("New World Labs generation and exports are disabled.")
        if not self.key:
            raise RuntimeError("Add WORLD_LABS_API_KEY to the relay's local .env.")
        async def send(client):
            response = await client.request(method, API + path,
                headers={"WLT-Api-Key": self.key}, json=payload, timeout=90)
            if response.status_code in (401, 403):
                raise RuntimeError("World Labs rejected the API key. Check WORLD_LABS_API_KEY.")
            if response.status_code == 402:
                raise RuntimeError("World Labs API credits are insufficient. Check the API Platform balance.")
            if response.status_code >= 400:
                raise RuntimeError(f"World Labs returned HTTP {response.status_code}. Please try again later.")
            return response.json()
        if self.client is not None: return await send(self.client)
        async with httpx.AsyncClient() as client: return await send(client)

    async def generate(self, topic, context):
        if not self.allow_generation:
            raise RuntimeError("New World Labs generation is disabled. Reuse the existing test world.")
        prompt = (
            "Create one cohesive, photorealistic place expressing the current idea. Earlier ideas are optional "
            "exploration history, useful for ambiguity or an explicitly requested connection. A clear new topic "
            "can be unrelated and have its own setting; do not force a blend of every preceding idea. "
            "This is an actual environment to explore from human eye height, with "
            "consistent scale, materials and lighting in every direction. Choose a natural setting "
            "for the subject, not a generic exhibition hall. The starting viewpoint is in a clear "
            "walkable area. Leave a nearby accessible surface or clear patch of ground for a small "
            "hands-on learning object. Keep near-field surfaces well defined and accessible. "
            "No floating interface, text captions, diagram overlays, portraits or close foreground "
            "people. Preserve the requested time and place. Request and ancestry: " + context
        )
        return await self.request("POST", "/worlds:generate", {
            "display_name": topic[:64], "model": self.model,
            "permission": {"public": False},
            "world_prompt": {"type": "text", "text_prompt": prompt}})

    async def operation(self, operation_id):
        return await self.request("GET", "/operations/" + safe_id(operation_id))

    async def world(self, world_id):
        return await self.request("GET", "/worlds/" + safe_id(world_id))

    async def export(self, world_id):
        if not self.allow_generation:
            raise RuntimeError("New World Labs exports are disabled. Resume the already-started export.")
        return await self.request("POST", "/worlds/" + safe_id(world_id) + ":export", {
            "asset_type": "mesh", "format": "glb", "mesh_variant": "textured"})

    async def wait(self, operation, progress=None, timeout=4200):
        deadline = time.monotonic() + timeout
        while True:
            if operation.get("error"):
                raise RuntimeError("World Labs could not finish the generation. Check its API Platform operation.")
            if operation.get("done"):
                return operation.get("response") or {}
            if time.monotonic() > deadline:
                raise RuntimeError("World Labs is still preparing this world. Re-enter to resume waiting.")
            if progress: progress(operation)
            await asyncio.sleep(2)
            operation = await self.operation(operation["operation_id"])


def safe_id(value):
    if not isinstance(value, str) or not 1 <= len(value) <= 128 or any(
        not (c.isalnum() or c in "_-") for c in value):
        raise ValueError("Invalid World Labs resource ID.")
    return value


async def download_asset(url, target, max_bytes=300_000_000):
    parsed = urlparse(url)
    host = parsed.hostname or ""
    allowed = ("wlt-ai-cdn.art", "storage.googleapis.com", "worldlabs.ai")
    if parsed.scheme != "https" or parsed.username or not any(host == h or host.endswith("." + h) for h in allowed):
        raise RuntimeError("World Labs returned an unsupported asset host.")
    async with httpx.AsyncClient(timeout=120, follow_redirects=False) as client:
        async with client.stream("GET", url) as response:
            if response.status_code != 200:
                raise RuntimeError(f"World asset download returned HTTP {response.status_code}.")
            total = 0
            temporary = target.with_suffix(target.suffix + ".download")
            try:
                with temporary.open("wb") as output:
                    async for chunk in response.aiter_bytes():
                        total += len(chunk)
                        if total > max_bytes: raise RuntimeError("Generated world asset exceeds the Quest import limit.")
                        output.write(chunk)
                temporary.replace(target)
            finally:
                temporary.unlink(missing_ok=True)
