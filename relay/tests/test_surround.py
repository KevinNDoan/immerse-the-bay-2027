import asyncio
import base64
import copy
import io
import json

import numpy as np
import pytest
from PIL import Image

from zap_relay.config import Settings
from zap_relay.surround import SurroundSource, perspective, jpeg
from zap_relay.worlds import WorldPreparation, validate_plan


def plan():
    return {"title":"Flow", "explanation":"A schematic flow model", "eventPrompt":"Show flow changing",
            "parts":[{"name":"Path", "detail":"Material travels along this route", "shape":"tube",
                "position":{"x":0,"y":0,"z":0}, "scale":{"x":.03,"y":1,"z":1},
                "rotation":{"x":0,"y":0,"z":0}, "color":"#44BBEE", "motion":"still", "speed":0,
                "points":[{"x":-.2,"y":0,"z":0},{"x":.2,"y":0,"z":0}]}]}


def test_generated_geometry_rejects_nonfinite_and_unbounded_data():
    assert validate_plan(plan())["parts"][0]["shape"] == "tube"
    invalid = plan(); invalid["parts"][0]["position"]["x"] = float("nan")
    with pytest.raises(ValueError): validate_plan(invalid)
    invalid = plan(); invalid["parts"] *= 29
    with pytest.raises(ValueError): validate_plan(invalid)
    invalid = plan(); invalid["parts"][0]["shape"] = "script"
    with pytest.raises(ValueError): validate_plan(invalid)
    invalid = plan(); invalid["parts"][0]["points"] = []
    with pytest.raises(ValueError): validate_plan(invalid)


def test_panorama_rays_point_at_the_four_matching_directions():
    source = np.zeros((360,720,3),dtype=np.uint8)
    colors = [(255,0,0),(0,255,0),(0,0,255),(255,255,0)]
    for x in range(720):
        direction = int(round((((x+.5)/720-.5)*360) / 90)) % 4
        source[:,x] = colors[direction]
    image = Image.fromarray(source)
    for index, expected in enumerate(colors):
        actual = perspective(image,index*90,width=160,height=90).getpixel((80,45))
        assert np.max(np.abs(np.array(actual,dtype=int)-expected)) <= 1


def test_prepare_deduplicates_and_caches_full_ancestor_context(tmp_path):
    async def scenario():
        settings = Settings("standin","","unused",8787)
        service = WorldPreparation(settings, tmp_path)
        calls = []
        async def create_plan(topic, context):
            calls.append(("plan",context)); await asyncio.sleep(.01); return plan()
        async def seed(context):
            calls.append(("image",context)); return jpeg(Image.new("RGB",(320,160),"blue"))
        service.plan, service.panorama = create_plan, seed
        payload = {"topic":"Flow","context":"Parent: ecosystems. Other parent: energy. Child: flow."}
        a,b = await asyncio.gather(service.prepare(payload),service.prepare(payload))
        assert a == b and len(calls) == 2 and a["image"]
        await service.prepare(payload)
        assert len(calls) == 2
        await service.prepare({**payload,"context":"Revised parent: economies. Child: flow."})
        assert len(calls) == 4
        await service.close()
    asyncio.run(scenario())


def test_image_failure_keeps_the_teaching_model(tmp_path):
    async def scenario():
        service=WorldPreparation(Settings("standin","","unused",8787),tmp_path)
        async def model(*args): return plan()
        async def seed(*args): raise RuntimeError("Image provider unavailable")
        service.plan, service.panorama = model,seed
        result=await service.prepare({"topic":"Flow"})
        assert result["plan"]["title"] == "Flow" and not result["image"]
        assert "unavailable" in result["notice"]
        await service.close()
    asyncio.run(scenario())


def test_four_views_share_context_steer_together_and_close():
    class FakeSource:
        instances=[]
        def __init__(self,key,model,video,audio,error,**options):
            self.video=video; self.options=options; self.prompts=[]; self.closed=False
            FakeSource.instances.append(self)
        async def start(self,topic,prompt,image):
            self.prompts.append(prompt)
            assert Image.open(io.BytesIO(base64.b64decode(image))).size == (960,540)
            await self.video(jpeg(Image.new("RGB",(960,540),"red")),0)
        async def steer(self,prompt): self.prompts.append(prompt); return 33
        async def stop(self): pass
        async def close(self): self.closed=True
    async def scenario():
        frames=[]
        async def video(data,seq): frames.append(data)
        async def ignore(*args): pass
        source=SurroundSource("private","model",video,ignore,ignore,source_factory=FakeSource)
        seed=base64.b64encode(jpeg(Image.new("RGB",(320,160),"blue"))).decode()
        try:
            await source.start("Child","Parent context plus child",seed)
            await asyncio.sleep(2.5)
            assert source.live_views == 15
            assert Image.open(io.BytesIO(frames[-1])).size == (1920,1080)
            assert sum(item.options["audio_enabled"] for item in FakeSource.instances) == 1
            await source.steer("Parent context plus child; change the weather")
            assert all("change the weather" in item.prompts[-1] for item in FakeSource.instances)
            await source.stop()
            await source.start("Next child","A different parent context",seed)
            await asyncio.sleep(2.5)
            assert len(FakeSource.instances) == 4, "World changes should reuse the four sessions."
            assert source.live_views == 15
        finally: await source.close()
        assert all(item.closed for item in FakeSource.instances)
    asyncio.run(scenario())


def test_failed_view_recovers_without_old_error_rejecting_delayed_frames():
    class RecoveringSource:
        instances = []
        def __init__(self, key, model, video, audio, error, **options):
            self.video = video; self.attempts = 0; self.task = None
            self.index = len(self.instances); self.instances.append(self)
        async def start(self, topic, prompt, image):
            self.attempts += 1
            if self.index == 0 and self.attempts == 1:
                raise RuntimeError('quota exceeded; retry_after_seconds: 1')
            async def deliver():
                await asyncio.sleep(.2)  # Later than the startup error check.
                await self.video(jpeg(Image.new("RGB", (960,540), "blue")), 0)
            self.task = asyncio.create_task(deliver())
        async def stop(self):
            if self.task:
                self.task.cancel()
                await asyncio.gather(self.task, return_exceptions=True)
        async def close(self): await self.stop()
    async def scenario():
        async def ignore(*args): pass
        source = SurroundSource("private", "model", ignore, ignore, ignore, source_factory=RecoveringSource)
        try:
            await source.start("Topic", "Parent context", "")
            await asyncio.sleep(3.5)
            assert source.live_views == 15
            assert not source.failures
            assert RecoveringSource.instances[0].attempts == 2
        finally:
            await source.close()
        assert all(item.task is None or item.task.done() for item in RecoveringSource.instances)
    asyncio.run(scenario())
