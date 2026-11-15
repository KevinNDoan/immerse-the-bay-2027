import copy
import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient
from PIL import Image

from zap_relay.app import create_app
from zap_relay.collection import WorldCollection, validate_collection
from zap_relay.config import Settings


def test_requested_demo_has_exact_parent_links_and_optional_artifacts():
    spec=validate_collection(json.loads((Path(__file__).parents[1]/'demo/great-wall-biology.json').read_text()))
    worlds={w['id']:w for w in spec['worlds']}
    assert len(worlds)==3
    assert worlds['demo-great-wall-exterior']['parentIds']==[]
    assert worlds['demo-great-wall-tower']['parentIds']==['demo-great-wall-exterior']
    assert worlds['demo-biology-lab']['parentIds']==[]
    assert worlds['demo-great-wall-exterior']['planFile'] is None
    assert worlds['demo-great-wall-tower']['planFile'] is None
    assert worlds['demo-biology-lab']['planFile']=='dna.json'
    cycle=copy.deepcopy(spec)
    cycle['worlds'][0]['parentIds']=['demo-great-wall-tower']
    with pytest.raises(ValueError,match='cycle'): validate_collection(cycle)


def test_collection_routes_node_ids_to_distinct_worlds_and_never_generates(tmp_path):
    spec=json.loads((Path(__file__).parents[1]/'demo/great-wall-biology.json').read_text())
    app=create_app(Settings('standin','','model',8794))
    spatial=app.state.spatial; spatial.root=tmp_path/'geometry'; spatial.enabled=True
    class NoCalls:
        def __getattr__(self,name): raise AssertionError('Demo must never make a provider request: '+name)
    spatial.api=NoCalls(); spatial.study=NoCalls()
    for index,world in enumerate(spec['worlds'][:2]):
        world['cacheKey']=str(index+1)*64; world['plan']=None
        folder=spatial._folder(world['cacheKey']); (folder/'assets').mkdir(parents=True)
        (folder/'status.json').write_text(json.dumps({'state':'ready','projection':'geometry',
            'environment':{'basePath':'/world/assets/'+world['cacheKey']+'/'},'plan':None}))
        (folder/'assets/visual-0.mesh').write_bytes(bytes([index]))
        Image.new('RGB',(32,16),'red' if index==0 else 'blue').save(folder/'pano.png')
    path=tmp_path/'catalog.json'; path.write_text(json.dumps(spec))
    spatial.collection=WorldCollection(path)
    with TestClient(app) as client:
        exterior=client.post('/world/prepare',json={'nodeId':spec['worlds'][0]['id'],'topic':'Identical title'}).json()
        tower=client.post('/world/prepare',json={'nodeId':spec['worlds'][1]['id'],'topic':'Identical title'}).json()
        assert exterior['cacheKey']!=tower['cacheKey']
        assert exterior['pane']==spec['worlds'][0]['pane'] and exterior['plan'] is None
        assert tower['pane']==spec['worlds'][1]['pane'] and tower['plan'] is None
        preview_a=client.get('/world/preview',params={'nodeId':spec['worlds'][0]['id']}).json()['image']
        preview_b=client.get('/world/preview',params={'nodeId':spec['worlds'][1]['id']}).json()['image']
        assert preview_a and preview_b and preview_a!=preview_b
        assert client.post('/world/prepare',json={'nodeId':'old-demo-node'}).json()['state']=='error'
        assert client.post('/world/prepare',json={'nodeId':spec['worlds'][2]['id']}).json()['state']=='error'
        assert client.get('/world/preview',params={'nodeId':'old-demo-node'}).json()=={'image':''}
        assert client.get('/world/assets/'+'f'*64+'/visual-0.mesh').status_code==404
        seed=spatial.collection.seed(spatial)
        assert len(seed['nodes'])==2
        assert seed['edges']==[{'from':'demo-great-wall-exterior','to':'demo-great-wall-tower'}]
        assert not spatial.tasks
