"""Cache an already-generated world for the one-world test. Makes no provider calls."""
import argparse
import asyncio
import json
import shutil
import sys
import time
from pathlib import Path

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from zap_relay.config import load_settings
from zap_relay.spatial import SpatialPreparation
from zap_relay.worlds import WorldPreparation


async def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--wait',action='store_true')
    args=parser.parse_args();source=args.source
    settings=load_settings()
    if not settings.world_test_world_id: raise SystemExit('Pin the existing test world in local configuration first.')
    world=json.loads((source/'world.json').read_text())
    if world.get('world_id') != settings.world_test_world_id:
        raise SystemExit('The source does not match the configured test world.')
    deadline=time.monotonic()+4200
    while not (source/'world.glb').exists():
        if not args.wait or time.monotonic()>deadline: raise SystemExit('The existing mesh export is not ready yet.')
        await asyncio.sleep(10)
    study=WorldPreparation(settings); service=SpatialPreparation(settings,study)
    folder=service._folder(service.pinned_key);folder.mkdir(parents=True,exist_ok=True)
    for name in ('world.json','generation.json','export.json','world.glb','collider.glb','pano.png','plan.json'):
        if (source/name).exists(): shutil.copy2(source/name,folder/name)
    result=await service.import_assets(folder,service.pinned_key)
    (source/'manifest.json').write_text(json.dumps(result['environment']))
    print('Existing world ready in local cache:',result['cacheKey'],flush=True)
    print('Triangles:',sum(x['triangles'] for x in result['environment']['visual']),flush=True)
    await service.close();await study.close()

if __name__=='__main__':asyncio.run(main())
