"""Download/convert native mobile splats from already generated worlds. Never generates or exports."""
import argparse
import asyncio
import json
from pathlib import Path

from zap_relay.config import Settings
from zap_relay.spatial import SpatialPreparation


async def run(args):
    service=SpatialPreparation(Settings('standin','','',8794,world_render_mode='splats',world_splat_quality=args.quality),None)
    keys=list(args.cache_key)
    if args.catalog:
        keys += [w['cacheKey'] for w in json.loads(args.catalog.read_text())['worlds'] if w.get('cacheKey')]
    for key in dict.fromkeys(keys):
        result=await service.import_splats(service._folder(key),key)
        print(key, result['state'], result['environment']['splats'], flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--catalog',type=Path)
    parser.add_argument('--cache-key',action='append',default=[])
    parser.add_argument('--quality',choices=('100k','150k','500k'),default='500k')
    asyncio.run(run(parser.parse_args()))
