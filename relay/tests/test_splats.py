import asyncio
import gzip
import json
import struct

import numpy as np
import pytest
from PIL import Image

from zap_relay.config import Settings
from zap_relay.spatial import SpatialPreparation
from zap_relay.splat_assets import decode_spz
from test_spatial import triangle_glb


def fixture(version=2):
    xyz=b''.join((v & 0xffffff).to_bytes(3,'little') for v in (4096,-8192,12288))
    rotation=bytes([128,128,128]) if version==2 else struct.pack('<I',3<<30)
    return gzip.compress(struct.pack('<III4B',0x5053474e,version,1,0,12,0,0)+xyz+bytes([255,128,128,128,160,160,160])+rotation)


@pytest.mark.parametrize('version',[2,3])
def test_decode_signed_position_covariance_and_color(version):
    p=decode_spz(fixture(version))
    np.testing.assert_allclose(p[0,:4],[1,2,3,1])
    np.testing.assert_allclose(p[0,4:10],[1,0,0,1,0,1],atol=1e-6)
    assert np.all((p[0,12:15]>.5)&(p[0,12:15]<.51))
    with pytest.raises(ValueError): decode_spz(gzip.compress(gzip.decompress(fixture(version))[:-1]))


def test_splat_pipeline_never_submits_textured_export_and_reuses_cache(tmp_path,monkeypatch):
    import zap_relay.spatial as module
    class API:
        generated=0
        async def request(self,method,path):
            assert (method,path)==('GET','/credits');return {'remaining_credits':1920}
        async def generate(self,*args): self.generated+=1; return {'operation_id':'generation'}
        async def wait(self,operation): return {'world_id':'world'}
        async def world(self,*args): return {'assets':{'mesh':{'collider_mesh_url':'collider'},'imagery':{'pano_url':'pano'},'splats':{'spz_urls':{'500k':'splats'}}}}
        async def export(self,*args): raise AssertionError('Splat mode must never spend credits exporting a mesh')
    class Study:
        async def plan(self,*args,**kwargs): return {'title':'An idea'}
    async def download(url,path):
        if url=='pano': Image.new('RGB',(8,4),'blue').save(path)
        else: path.write_bytes(fixture() if url=='splats' else triangle_glb())
    async def run():
        api=API(); settings=Settings('live','','',8794,world_generation_enabled=True,world_render_mode='splats')
        service=SpatialPreparation(settings,Study(),tmp_path,api)
        monkeypatch.setattr(module,'download_asset',download)
        payload={'nodeId':'new','topic':'Idea','context':'Unrelated context'}
        job=await service.prepare(payload);await asyncio.gather(*service.tasks.values())
        ready=service.status(job['cacheKey'])
        assert ready['state']=='ready',ready
        assert ready['environment']['splats']['count']==1
        assert ready['environment']['visual']==[]
        assert service.asset(job['cacheKey'],'scene.splat').read_bytes()[:4]==b'ZSP1'
        assert not (service._folder(job['cacheKey'])/'export.json').exists()
        assert (await service.prepare(payload))['state']=='ready' and api.generated==1
        await service.close()
    asyncio.run(run())


def test_planning_overlaps_generation_downloads_overlap_and_ready_waits_for_plan(tmp_path, monkeypatch):
    import zap_relay.spatial as module

    async def run():
        planning = asyncio.Event(); finish_plan = asyncio.Event()
        downloads_started = set(); all_downloads = asyncio.Event(); imported = asyncio.Event()

        class API:
            async def request(self, *args): return {'remaining_credits': 1920}
            async def generate(self, *args): return {'operation_id': 'generation'}
            async def wait(self, operation):
                await planning.wait()  # A serial planner would deadlock here.
                return {'world_id': 'world'}
            async def world(self, *args):
                return {'assets': {'mesh': {'collider_mesh_url': 'collider'},
                    'imagery': {'pano_url': 'pano'}, 'splats': {'spz_urls': {'500k': 'splats'}}}}
            async def export(self, *args): raise AssertionError('Unexpected paid export')

        class Study:
            async def plan(self, *args):
                planning.set(); await finish_plan.wait()
                return {'title': 'Concurrent idea'}

        async def download(url, path):
            downloads_started.add(url)
            if len(downloads_started) == 3: all_downloads.set()
            await all_downloads.wait()  # All three must start independently.
            if url == 'pano': Image.new('RGB', (8, 4), 'blue').save(path)
            else: path.write_bytes(fixture() if url == 'splats' else triangle_glb())

        settings = Settings('live', '', '', 8794, world_generation_enabled=True, world_render_mode='splats')
        service = SpatialPreparation(settings, Study(), tmp_path, API())
        original_import = service.import_splats

        async def import_assets(*args, **kwargs):
            result = await original_import(*args, **kwargs)
            imported.set(); return result

        monkeypatch.setattr(module, 'download_asset', download)
        monkeypatch.setattr(service, 'import_splats', import_assets)
        try:
            job = await service.prepare({'nodeId': 'new', 'topic': 'Idea'})
            await asyncio.wait_for(imported.wait(), 3)
            assert service.status(job['cacheKey'])['state'] == 'preparing'
            assert not (service._folder(job['cacheKey']) / 'splat-manifest.json').exists()
            finish_plan.set()
            await asyncio.wait_for(asyncio.gather(*service.tasks.values()), 3)
            ready = service.status(job['cacheKey'])
            assert ready['state'] == 'ready' and ready['plan']['title'] == 'Concurrent idea'
        finally:
            await service.close()

    asyncio.run(run())


@pytest.mark.parametrize('cancel', [False, True])
def test_parallel_failure_or_shutdown_drains_downloads_and_planner(tmp_path, monkeypatch, cancel):
    import zap_relay.spatial as module

    async def run():
        planning = asyncio.Event(); planner_stopped = asyncio.Event(); all_started = asyncio.Event()
        started = set(); stopped = set()

        class API:
            async def request(self, *args): return {'remaining_credits': 1920}
            async def generate(self, *args): return {'operation_id': 'generation'}
            async def wait(self, operation):
                await planning.wait(); return {'world_id': 'world'}
            async def world(self, *args):
                return {'assets': {'mesh': {'collider_mesh_url': 'collider'},
                    'imagery': {'pano_url': 'pano'}, 'splats': {'spz_urls': {'500k': 'splats'}}}}

        class Study:
            async def plan(self, *args):
                planning.set()
                try: await asyncio.Event().wait()
                finally: planner_stopped.set()

        async def download(url, path):
            started.add(url)
            if len(started) == 3: all_started.set()
            try:
                await all_started.wait()
                if not cancel and url == 'pano': raise RuntimeError('Download failed')
                await asyncio.Event().wait()
            finally: stopped.add(url)

        settings = Settings('live', '', '', 8794, world_generation_enabled=True, world_render_mode='splats')
        service = SpatialPreparation(settings, Study(), tmp_path, API())
        monkeypatch.setattr(module, 'download_asset', download)
        try:
            job = await service.prepare({'topic': 'Idea'})
            await asyncio.wait_for(all_started.wait(), 3)
            if cancel: await service.close()
            else: await asyncio.wait_for(asyncio.gather(*service.tasks.values()), 3)
            assert planner_stopped.is_set() and stopped == started
            assert not (service._folder(job['cacheKey']) / 'splat-manifest.json').exists()
            if not cancel: assert service.status(job['cacheKey'])['state'] == 'error'
        finally: await service.close()

    asyncio.run(run())
