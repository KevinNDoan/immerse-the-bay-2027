import asyncio
import io
import json
import wave

import httpx
import numpy as np
import pytest
from fastapi.testclient import TestClient
from PIL import Image

from zap_relay.app import World, create_app
from zap_relay.config import Settings
from zap_relay.nodes import NodeService, SpeechService
from zap_relay.sources import ReactorSource


def settings(**kwargs):
    return Settings(mode="standin", api_key="", model="reactor/visko-orbis-stable", port=8787, **kwargs)


def test_steering_replaces_camera_and_event_slots():
    async def scenario():
        world = World(settings())
        try:
            await world.enter("Volcanoes", "Ocean volcanoes", "")
            await asyncio.sleep(.15)
            for index in range(30):
                await world.steer("move", f"forward {index}")
            await world.steer("event", "Add a whale")
            await world.steer("look", "pan right")
            assert "Ocean volcanoes" in world.prompt
            assert "Add a whale" in world.prompt
            assert "pan right" in world.prompt
            assert "forward" not in world.prompt
            assert len(world.prompt) < 200
            await world.steer("scene", "Revised topic: underwater chemistry")
            assert "underwater chemistry" in world.prompt
            assert "Ocean volcanoes" not in world.prompt
        finally:
            await world.kill()
    asyncio.run(scenario())


def test_kill_cancels_an_inflight_connection():
    class SlowSource:
        closed = False
        async def start(self, *args): await asyncio.sleep(600)
        async def close(self): self.closed = True
        async def stop(self): pass
    async def scenario():
        world = World(settings())
        source = SlowSource()
        world._make_source = lambda: source
        await world.begin_enter("test", "prompt", "")
        await asyncio.sleep(.01)
        await asyncio.wait_for(world.kill(), .5)
        assert source.closed
        assert world.state == "idle"
        assert world.enter_task is None
    asyncio.run(scenario())


def test_reactor_commands_batch_frames_resolution_and_restart():
    class Track:
        def on_frame(self, callback): self.callback = callback
    class FakeReactor:
        def __init__(self, **kwargs): self.tracks = {}; self.calls = []; self.closed = False
        def track(self, name): return self.tracks.setdefault(name, Track())
        def on_message(self, callback): self.message = callback
        def on_error(self, callback): self.error = callback
        async def connect(self): self.message({"type":"state", "data":{"available_resolutions":["1080p"]}})
        async def send_command(self, command, data):
            self.calls.append((command, data))
            if command == "start":
                self.tracks["main_video"].callback(np.full((2, 720, 1280, 3), 127, dtype=np.uint8))
        async def upload_file(self, *args, **kwargs): return {"file_id":"test"}
        async def disconnect(self): self.closed = True
        def close(self): self.closed = True
    async def scenario():
        frames, errors = [], []
        async def video(jpeg, seq): frames.append((jpeg, seq))
        async def audio(pcm, seq): pass
        async def error(detail): errors.append(detail)
        source = ReactorSource("private", "model", video, audio, error, max_width=640,
                               resolution="1080p", reactor_factory=FakeReactor)
        try:
            await source.start("test", "scene", "")
            reactor = source._reactor
            await asyncio.sleep(.15)
            assert ("set_resolution", {"resolution":"1080p"}) in reactor.calls
            assert not any(command == "set_audio_enabled" for command, _ in reactor.calls)
            assert frames
            assert Image.open(io.BytesIO(frames[0][0])).size == (640, 360)
            reactor.message({"type":"generation_complete", "data":{}})
            await asyncio.sleep(.1)
            assert sum(command == "start" for command, _ in reactor.calls) == 2
            reactor.message({"type":"command_error", "data":{"command":"set_prompt", "reason":"private invalid"}})
            await asyncio.sleep(.02)
            assert "[redacted]" in errors[0]
        finally:
            await source.close()
        assert reactor.closed
    asyncio.run(scenario())


def test_direct_nodes_keep_context_and_reject_bad_edits():
    service = NodeService(settings())
    async def scenario():
        result = await service.update({"action":"create", "text":"Eruptions", "parents":[{"topic":"Underwater volcanoes", "summary":"Seafloor activity"}]})
        assert "Underwater volcanoes" in result["worldPrompt"]
        edited = await service.update({"action":"edit", "text":"Rename it to Hydrothermal vents", "node":result})
        assert edited["topic"] == "Hydrothermal vents"
        with pytest.raises(ValueError): await service.update({"action":"edit", "text":"Make it simpler"})
    asyncio.run(scenario())


def test_gpt_request_contains_multiple_parents_and_requested_model(monkeypatch):
    import zap_relay.nodes as module
    requests = []
    def respond(request):
        requests.append(json.loads(request.content))
        return httpx.Response(200, json={"output":[{"type":"message", "content":[{
            "type":"output_text", "text":json.dumps({"topic":"Vent ecosystems", "summary":"Life at vents",
                "worldPrompt":"Deep-sea vents supporting chemosynthetic life", "suggestions":["Chemosynthesis"]})}]}]})
    client = httpx.AsyncClient(transport=httpx.MockTransport(respond))
    monkeypatch.setattr(module.httpx, "AsyncClient", lambda **kwargs: client)
    parents = [{"topic":"Ocean", "summary":"Marine life"}, {"topic":"Chemistry", "summary":"Energy"}]
    result = asyncio.run(NodeService(settings(openai_key="test")).update({"action":"create", "text":"Vents", "parents":parents}))
    assert result["topic"] == "Vent ecosystems"
    assert requests[0]["model"] == "gpt-6-luna"
    assert requests[0]["store"] is False
    assert json.loads(requests[0]["input"][0]["content"])["parents"] == parents


def test_speech_rejects_silence_and_malformed_wav_without_loading_model():
    service = SpeechService("unused")
    with pytest.raises(ValueError): service.transcribe(b"bad audio")
    output = io.BytesIO()
    with wave.open(output, "wb") as wav:
        wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(16000); wav.writeframes(bytes(16000))
    with pytest.raises(ValueError, match="No speech"):
        service.transcribe(output.getvalue())
    assert service._model is None


def test_voice_and_node_http_contracts_and_limits():
    app = create_app(settings())
    class Speech:
        def transcribe(self, data): return "Underwater volcanoes"
    app.state.speech = Speech()
    with TestClient(app) as client:
        assert client.post("/speech/transcribe", content=b"test").json()["text"] == "Underwater volcanoes"
        assert client.post("/speech/transcribe", content=bytes(4_000_001)).status_code == 413
        assert client.post("/nodes/update", json={"action":"create", "text":"Volcanoes"}).status_code == 200
        assert client.post("/nodes/update", json={"action":"delete", "text":"Volcanoes"}).status_code == 400
        assert "nodeAiConfigured" in client.get("/capabilities").json()
