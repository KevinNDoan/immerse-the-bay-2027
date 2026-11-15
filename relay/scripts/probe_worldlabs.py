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
    parser.add_argument("--authorize-generation", action="store_true", help="Explicitly allow this one generation and its HQ export; does not enable the relay")
    parser.add_argument("--key-env", default="WORLD_LABS_API_KEY", help="Name of a credential in the ignored root environment file")
    args = parser.parse_args(); folder = args.output; folder.mkdir(parents=True, exist_ok=True)
    env = dotenv_values(Path(__file__).resolve().parents[2] / ".env")
    api = WorldLabs(env.get(args.key_env, ""), allow_generation=args.authorize_generation)
    def save(name, value):
        temporary=folder/(name+'.tmp'); temporary.write_text(json.dumps(value, indent=2)); temporary.replace(folder/name)
    if not (folder/'generation.json').exists():
        credits=await api.request('GET','/credits')
        if credits.get('remaining_credits',0)<5080:
            raise RuntimeError('At least 5,080 credits are required to start a complete textured world.')
    async def stage(file, start):
        path = folder / file
        if path.exists():
            operation=json.loads(path.read_text())
            if not operation.get('operation_id'):
                raise RuntimeError('A previous submission has an uncertain outcome. Inspect the provider before retrying; no duplicate was sent.')
        else:
            # Record the intent before spending. Never repeat an uncertain paid POST.
            save(file,{'state':'submitting','key_env':args.key_env})
            operation=await start(); save(file,operation)
        print(file, "waiting", flush=True)
        result=await api.wait(operation)
        save(file.replace('.json','-result.json'),result)
        return result
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
