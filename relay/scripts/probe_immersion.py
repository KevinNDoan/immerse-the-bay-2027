"""Bounded live Orbis projection/steering experiment. Never prints credentials."""
import argparse
import asyncio
import base64
import json
import logging
import time
from pathlib import Path

from zap_relay.config import load_settings
from zap_relay.sources import ReactorSource
from zap_relay.surround import SurroundSource


async def run(args):
    settings = load_settings()
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    first = asyncio.Event()
    result = {"model": args.model or settings.model, "prompt": args.prompt,
              "passthrough": args.passthrough, "frames": 0, "errors": []}
    start = time.monotonic()
    last_save = -10

    async def video(jpeg, seq):
        nonlocal last_save
        if not first.is_set():
            result["first_frame_seconds"] = round(time.monotonic() - start, 2)
            first.set()
        result["frames"] += 1
        elapsed = time.monotonic() - start - result["first_frame_seconds"]
        if elapsed - last_save >= 3:
            (output / f"frame-{seq:04}.jpg").write_bytes(jpeg)
            last_save = elapsed

    async def audio(*_):
        pass

    async def error(detail):
        result["errors"].append(detail.replace(settings.api_key, "[redacted]"))

    class ProbeSource(ReactorSource):
        async def start(self, topic, prompt, image):
            # The SDK wire schema documents verbatim prompting for Stable.
            from reactor_sdk import Reactor
            def factory(**kw):
                reactor = Reactor(**kw)
                original = reactor.send_command
                async def send(command, data):
                    if command == "set_prompt" and args.passthrough:
                        data = {**data, "passthrough": True}
                    return await original(command, data)
                reactor.send_command = send
                return reactor
            self._factory = factory
            await super().start(topic, prompt, image)

    source = (SurroundSource(settings.api_key, result["model"], video, audio, error) if args.surround else
              ProbeSource(settings.api_key, result["model"], video, audio, error,
                          max_width=1920, resolution="1080p"))
    try:
        image = base64.b64encode(Path(args.image).read_bytes()).decode() if args.image else ""
        await asyncio.wait_for(source.start("Projection experiment", args.prompt, image), 110)
        await asyncio.wait_for(first.wait(), 90)
        await asyncio.sleep(args.seconds / 2)
        if args.steer:
            result["steer_at_seconds"] = round(time.monotonic() - start, 2)
            result["steer_expected_seq"] = await source.steer(args.prompt + " " + args.steer)
        await asyncio.sleep(args.seconds / 2)
    except Exception as exc:
        result["errors"].append(str(exc).replace(settings.api_key, "[redacted]"))
    finally:
        if args.surround:
            result["live_views"] = source.live_views
            result["view_failures"] = source.failures
        await source.close()
        result["total_seconds"] = round(time.monotonic() - start, 2)
        (output / "result.json").write_text(json.dumps(result, indent=2))
        print(json.dumps({k: v for k, v in result.items() if k != "prompt"}))


if __name__ == "__main__":
    logging.basicConfig(level=logging.CRITICAL)
    parser = argparse.ArgumentParser()
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--steer", default="")
    parser.add_argument("--image", default="")
    parser.add_argument("--model", default="")
    parser.add_argument("--output", required=True)
    parser.add_argument("--passthrough", action="store_true")
    parser.add_argument("--surround", action="store_true")
    parser.add_argument("--seconds", type=float, default=18)
    args = parser.parse_args()
    if not 6 <= args.seconds <= 45:
        parser.error("--seconds must be between 6 and 45")
    asyncio.run(run(args))
