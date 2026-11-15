import asyncio
import json
import struct
from dataclasses import replace

import httpx
import numpy as np
import pytest

from zap_relay.config import Settings
from zap_relay.mesh_assets import GLB, convert_glb, project_panorama
from zap_relay.spatial import SpatialPreparation
from zap_relay.worldlabs import WorldLabs


def triangle_glb():
    vertices=np.array([[0,1,0],[1,1,0],[0,1,1]],dtype='<f4').tobytes()
    indices=np.array([0,1,2],dtype='<u4').tobytes()
    binary=vertices+indices
    doc={'asset':{'version':'2.0'},'buffers':[{'byteLength':len(binary)}],
         'bufferViews':[{'buffer':0,'byteOffset':0,'byteLength':len(vertices)},
                        {'buffer':0,'byteOffset':len(vertices),'byteLength':len(indices)}],
         'accessors':[{'bufferView':0,'componentType':5126,'count':3,'type':'VEC3'},
                      {'bufferView':1,'componentType':5125,'count':3,'type':'SCALAR'}],
         'meshes':[{'primitives':[{'attributes':{'POSITION':0},'indices':1}]}],
         'nodes':[{'mesh':0,'translation':[0,0,2]}],'scenes':[{'nodes':[0]}],'scene':0}
    encoded=json.dumps(doc).encode();encoded+=b' '*((-len(encoded))%4)
    size=12+8+len(encoded)+8+len(binary)
    return struct.pack('<III',0x46546c67,2,size)+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary


def test_marble_mesh_axes_indices_and_packet_bounds(tmp_path):
    source=tmp_path/'world.glb';source.write_bytes(triangle_glb())
    result=convert_glb(source,tmp_path/'assets')
    assert result[0]['triangles']==1
    data=(tmp_path/'assets'/result[0]['mesh']).read_bytes()
    magic,vertices,indices=struct.unpack_from('<4sII',data)
    assert magic==b'ZMS1' and vertices==3 and indices==3
    xyz=np.frombuffer(data,dtype='<f4',offset=12,count=vertices*3).reshape(-1,3)
    assert np.all(xyz[:,1]==-1), 'Marble +Y-down must become Unity +Y-up.'
    assert set(xyz[:,2])=={2,3}, 'Marble forward must remain Unity forward.'
    assert len(data)==12+vertices*24+indices*4
    source.write_bytes(triangle_glb()[:-1])
    with pytest.raises(ValueError): GLB(source)


def test_generation_and_export_are_disabled_by_default():
    async def scenario():
        requests=[]
        async def handler(request):
            requests.append(request);return httpx.Response(200,json={})
        async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
            api=WorldLabs('private',client=client)
            with pytest.raises(RuntimeError,match='disabled'): await api.generate('Topic','Parent context')
            with pytest.raises(RuntimeError,match='disabled'): await api.export('existing-world')
            with pytest.raises(RuntimeError,match='disabled'): await api.request('POST','/worlds:generate',{})
            with pytest.raises(RuntimeError,match='disabled'): await api.request('POST','/worlds/existing-world:export',{})
        assert not requests
    asyncio.run(scenario())


def test_panorama_surface_projection_preserves_geometry_and_splits_back_seam():
    positions=np.array([[.1,0,-1],[-.1,0,-1],[.1,.2,-1],[0,1,0],[0,-1,0],[0,0,1]],dtype=np.float32)
    triangles=np.array([[0,1,2]],dtype=np.uint32)
    projected,uv,indices=project_panorama(positions,triangles)
    np.testing.assert_allclose(projected[indices],positions[triangles])
    assert np.ptp(uv[indices,0],axis=1).max()<.05, 'The back seam must not smear the front of the panorama.'
    assert len(projected)>len(positions)
    np.testing.assert_allclose(uv[3:6,1],[1,0,.5],atol=1e-6)
    np.testing.assert_allclose(uv[5],[.5,.5])


def test_local_texture_repair_reimports_cached_assets_without_provider_calls(tmp_path):
    from PIL import Image
    class NoCalls:
        def __getattr__(self,name): raise AssertionError('Texture repair must use existing assets only.')
    async def scenario():
        service=SpatialPreparation(Settings('standin','','model',8794),None,tmp_path,NoCalls())
        key='a'*64; folder=service._folder(key); folder.mkdir()
        for name in ('world.glb','collider.glb'): (folder/name).write_bytes(triangle_glb())
        Image.new('RGB',(64,32),(100,140,180)).save(folder/'pano.png')
        original=await service.import_assets(folder,key)
        mesh_before=(folder/'assets/visual-0.mesh').read_bytes()
        collider_before=(folder/'assets/collision-0.mesh').read_bytes()
        repaired=await service.import_assets(folder,key,'panorama')
        assert repaired['environment']['textureProjection']=='panorama'
        assert repaired['environment']['visual'][0]['texture']=='visual-0.jpg'
        assert repaired['environment']['visual'][0]['triangles']==original['environment']['visual'][0]['triangles']
        assert (folder/'assets/visual-0.mesh').read_bytes()!=mesh_before
        assert (folder/'assets/collision-0.mesh').read_bytes()==collider_before
        assert (folder/'world.glb').read_bytes()==triangle_glb()
        restored=await service.import_assets(folder,key,'atlas')
        assert restored['environment']['textureProjection']=='atlas'
        assert (folder/'assets/visual-0.mesh').read_bytes()==mesh_before
        await service.close()
    asyncio.run(scenario())


def test_different_topics_reuse_one_environment_without_any_worldlabs_calls(tmp_path):
    class NoWorldCalls:
        def __getattr__(self,name): raise AssertionError('World Labs must not be contacted: '+name)
    class Study:
        calls=[]
        async def plan(self,topic,context,**kwargs):
            self.calls.append(context);return {'title':topic,'parts':[]}
    async def scenario():
        settings=Settings('live','orbis','model',8787,worldlabs_key='private',world_test_world_id='fixed-world')
        study=Study();service=SpatialPreparation(settings,study,tmp_path,NoWorldCalls())
        shared=service._folder(service.pinned_key);shared.mkdir(parents=True)
        (shared/'manifest.json').write_text(json.dumps({'visual':[{'mesh':'visual-0.mesh'}], 'collision':[]}))
        a=await service.prepare({'topic':'Optics','context':'Parent physics, child optics'})
        b=await service.prepare({'topic':'Trade','context':'Parent history, child trade'})
        await asyncio.gather(*service.tasks.values())
        a=service.status(a['cacheKey']);b=service.status(b['cacheKey'])
        assert a['state']==b['state']=='ready'
        assert a['environment']==b['environment']
        assert a['plan']['title']=='Optics' and b['plan']['title']=='Trade'
        await service.prepare({'topic':'Optics','context':'Parent physics, child optics'})
        assert len(study.calls)==2
        with pytest.raises(ValueError): service.asset('../outside','visual-0.mesh')
        with pytest.raises(ValueError): service.asset(service.pinned_key,'../../.env')
        await service.close()
    asyncio.run(scenario())


def test_missing_pinned_world_does_not_launch_a_replacement(tmp_path):
    class NoCalls:
        def __getattr__(self,name): raise AssertionError('No generation authorized')
    async def scenario():
        settings=Settings('live','','model',8787,worldlabs_key='private',world_test_world_id='fixed-world')
        service=SpatialPreparation(settings,NoCalls(),tmp_path,NoCalls())
        result=await service.prepare({'topic':'Request','context':'An unrelated request'})
        await asyncio.gather(*service.tasks.values())
        result=service.status(result['cacheKey'])
        assert result['state']=='error' and 'test world' in result['notice']
        await service.close()
    asyncio.run(scenario())


def test_http_flow_serves_one_cached_world_and_read_only_preview(tmp_path):
    from fastapi.testclient import TestClient
    from PIL import Image
    from zap_relay.app import create_app
    settings=Settings('live','orbis','model',8787,worldlabs_key='private',world_test_world_id='fixed-world')
    app=create_app(settings);service=app.state.spatial;service.root=tmp_path
    class NoWorldCalls:
        def __getattr__(self,name): raise AssertionError('No new provider request authorized')
    service.api=NoWorldCalls()
    async def study(topic,context,**kwargs): return {'title':topic,'parts':[]}
    service.study.plan=study
    shared=service._folder(service.pinned_key);(shared/'assets').mkdir(parents=True)
    (shared/'manifest.json').write_text(json.dumps({'visual':[{'mesh':'visual-0.mesh'}],'collision':[]}))
    (shared/'assets'/'visual-0.mesh').write_bytes(b'local cached geometry')
    Image.new('RGB',(32,16),'blue').save(shared/'pano.png')
    with TestClient(app) as client:
        preview=client.get('/world/preview')
        assert preview.status_code==200 and preview.json()['image']
        assert not service.tasks, 'Reading a preview must never prepare a new world.'
        job=client.post('/world/prepare',json={'topic':'Requested idea','context':'Inherited parent notes'}).json()
        for _ in range(20):
            result=client.get('/world/jobs/'+job['cacheKey']).json()
            if result['state']=='ready':break
        assert result['state']=='ready'
        assert result['plan']['title']=='Requested idea'
        assert 'private' not in json.dumps(result)
        path=result['environment']['basePath']+'visual-0.mesh'
        assert client.get(path).content==b'local cached geometry'
        assert client.get(result['environment']['basePath']+'secret.env').status_code==400
        assert client.get('/world/jobs/invalid').status_code==400
