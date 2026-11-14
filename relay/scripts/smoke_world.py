"""Exercise a running relay through its real socket. Always close the world."""
import argparse
import asyncio
import io
import json
from pathlib import Path
import time

import httpx
from PIL import Image
from websockets.asyncio.client import connect


async def run(url):
    async with httpx.AsyncClient(timeout=25) as http:
        health = (await http.get(url + "/health")).json()
        print("Relay:", health, flush=True)
        frames = 0
        try:
            async with connect(url.replace("http", "ws", 1) + "/ws/world", max_size=8_000_000) as ws:
                await ws.send(json.dumps({"t": "enter", "nodeId": "smoke", "topic": "Volcanoes",
                    "prompt": "A wide immersive view of an island volcano beside a blue ocean, a slow steady camera, no text, a single unbroken take", "image": ""}))
                deadline = time.monotonic() + 150
                steer_seq = None
                steered = False
                while time.monotonic() < deadline:
                    message = await asyncio.wait_for(ws.recv(), 100)
                    if isinstance(message, bytes):
                        if message[0] != 1:
                            continue
                        seq = int.from_bytes(message[1:5], "little")
                        frames += 1
                        if frames == 1:
                            with Image.open(io.BytesIO(message[13:])) as image:
                                print("First JPEG:", image.size, flush=True)
                                out = Path("../Builds/Verification"); out.mkdir(parents=True, exist_ok=True)
                                image.save(out / (health["mode"] + "-world.jpg"))
                        if frames == 5:
                            await ws.send(json.dumps({"t": "steer", "kind": "look", "text": "slowly pan right toward the ocean"}))
                            steered = True
                        if steer_seq is not None and seq >= steer_seq:
                            print("PASS: frames received after steer acknowledgement:", frames, flush=True)
                            return
                    else:
                        data = json.loads(message)
                        if data["t"] == "status":
                            print("World status:", data["state"], flush=True)
                            if data["state"] == "error":
                                raise RuntimeError(data.get("detail", "World failed"))
                        if data["t"] == "error":
                            raise RuntimeError(data.get("detail", "Relay error"))
                        if data["t"] == "prompt" and steered:
                            steer_seq = data["appliesAtSeq"]
                            print("Steering accepted; expected sequence:", steer_seq, flush=True)
                raise TimeoutError("World stream/steering timed out")
        finally:
            response = await http.post(url + "/world/kill")
            response.raise_for_status()
            print("Session cleanup:", (await http.get(url + "/health")).json()["state"], flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", default="http://127.0.0.1:8787")
    asyncio.run(run(parser.parse_args().url.rstrip("/")))
