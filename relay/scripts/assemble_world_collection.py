"""Import completed local exports and build a linked playable demo. Makes no API calls."""
import argparse
import asyncio
import copy
import json
import shutil
from pathlib import Path

from zap_relay.collection import WorldCollection, validate_collection
from zap_relay.config import DEFAULT_MODEL, Settings
from zap_relay.spatial import SpatialPreparation
from zap_relay.study_models import validate_plan


async def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--spec',type=Path,required=True)
    parser.add_argument('--source-root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    spec=validate_collection(json.loads(args.spec.read_text())); catalog=copy.deepcopy(spec)
    service=SpatialPreparation(Settings('standin','',DEFAULT_MODEL,8794),None)
    for entry in catalog['worlds']:
        source=args.source_root/entry['id']; entry['plan']=None
        if entry.get('planFile'):
            entry['plan']=validate_plan(json.loads((args.spec.parent/entry['planFile']).read_text()))
        if not all((source/name).is_file() for name in ('world.json','world.glb','collider.glb','pano.png')):
            entry['state']='pending'; continue
        world=json.loads((source/'world.json').read_text())
        entry['worldId']=world['world_id']; entry['cacheKey']=service.key('existing-world:'+world['world_id'])
        folder=service._folder(entry['cacheKey']); folder.mkdir(parents=True,exist_ok=True)
        for name in ('world.json','world.glb','collider.glb','pano.png','generation.json','export.json'):
            if (source/name).exists() and not (folder/name).exists(): shutil.copy2(source/name,folder/name)
        (folder/'plan.json').write_text(json.dumps(entry['plan']))
        result=await service.import_assets(folder,entry['cacheKey'],entry.get('textureProjection','atlas'))
        entry['state']=result['state']
        print(entry['id'],result['state'],sum(m['triangles'] for m in result['environment']['visual']),'triangles',flush=True)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    temp=args.output.with_suffix('.tmp'); temp.write_text(json.dumps(catalog,indent=2)); temp.replace(args.output)
    collection=WorldCollection(args.output); service.collection=collection
    seed=collection.seed(service)
    args.output.with_name('constellation.json').write_text(json.dumps(seed,indent=2))
    print('Playable worlds:',len(seed['nodes']),'Parent links:',len(seed['edges']),flush=True)
    await service.close()


if __name__=='__main__': asyncio.run(main())
