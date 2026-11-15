"""One resumable environment-generation/export probe. Never prints keys or signed URLs."""
import argparse
import asyncio
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dotenv import dotenv_values
from zap_relay.worldlabs import WorldLabs, download_asset


async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--topic", required=True); parser.add_argument("--context", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(); folder = args.output; folder.mkdir(parents=True, exist_ok=True)
    env = dotenv_values(Path(__file__).resolve().parents[2] / ".env")
    api = WorldLabs(env.get("WORLD_LABS_API_KEY", ""),
        allow_generation=env.get("WORLD_GENERATION_ENABLED", "false").lower() == "true")
    def save(name, value): (folder / name).write_text(json.dumps(value, indent=2))
    async def stage(file, start):
        path = folder / file
        operation = json.loads(path.read_text()) if path.exists() else await start()
        save(file, operation)
        print(file, "waiting", flush=True)
        return await api.wait(operation)
    result = await stage("generation.json", lambda: api.generate(args.topic, args.context))
    world_id = result.get("world_id") or result.get("world", {}).get("world_id")
    if not world_id: raise RuntimeError("No world ID in generation result")
    world = await api.world(world_id); save("world.json", world)
    print("Generated world", world_id, flush=True)
    assets = world.get("assets", {})
    for name, url in (("collider.glb", assets.get("mesh", {}).get("collider_mesh_url")),
                      ("pano.png", assets.get("imagery", {}).get("pano_url"))):
        if url and not (folder / name).exists(): await download_asset(url, folder / name)
    mesh = await stage("export.json", lambda: api.export(world_id))
    save("export-result.json", mesh)
    if not (folder / "world.glb").exists(): await download_asset(mesh["url"], folder / "world.glb")
    print("World and textured mesh downloaded", flush=True)

if __name__ == "__main__": asyncio.run(main())
