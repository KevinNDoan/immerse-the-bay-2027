from __future__ import annotations

import io
import struct
import time

from fastapi.testclient import TestClient
from PIL import Image

from zap_relay.app import create_app
from zap_relay.config import Settings

HEADER = struct.Struct("<BIQ")


def _client() -> TestClient:
    settings = Settings(
        mode="standin",
        api_key="",
        model="reactor/visko-orbis-stable",
        port=8787,
    )
    return TestClient(create_app(settings))


def _receive_until(socket, wanted: str) -> tuple[dict, float]:
    while True:
        message = socket.receive()
        if message.get("text") is None:
            continue
        parsed = message["text"]
        import json

        data = json.loads(parsed)
        if data.get("t") == wanted:
            return data, time.monotonic()


def _receive_video(socket) -> tuple[int, bytes, float]:
    while True:
        message = socket.receive()
        packet = message.get("bytes")
        if packet is None:
            continue
        kind, seq, timestamp_us = HEADER.unpack(packet[: HEADER.size])
        assert kind == 1
        assert timestamp_us > 0
        return seq, packet[HEADER.size :], time.monotonic()


def test_health_has_documented_standin_shape() -> None:
    with _client() as client:
        response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {
        "ok": True,
        "mode": "standin",
        "state": "idle",
        "model": "",
    }


def test_world_stream_steer_timing_and_exit() -> None:
    with _client() as client, client.websocket_connect("/ws/world") as socket:
        socket.send_json(
            {"t": "enter", "nodeId": "test", "topic": "Ocean currents", "prompt": "", "image": ""}
        )
        status, _ = _receive_until(socket, "status")
        while status["state"] != "streaming":
            status, _ = _receive_until(socket, "status")

        seq0, jpeg0, _ = _receive_video(socket)
        seq1, _, _ = _receive_video(socket)
        assert seq1 > seq0
        with Image.open(io.BytesIO(jpeg0)) as image:
            assert image.size == (832, 480)
            assert image.format == "JPEG"

        socket.send_json({"t": "steer", "kind": "event", "text": "Add a whale"})
        prompt, prompt_time = _receive_until(socket, "prompt")
        assert "event: Add a whale" in prompt["text"]

        while True:
            seq, _, frame_time = _receive_video(socket)
            if seq >= prompt["appliesAtSeq"]:
                break
        assert seq == prompt["appliesAtSeq"]
        assert abs((frame_time - prompt_time) - 1.8) <= 0.3

        socket.send_json({"t": "exit"})
        status, _ = _receive_until(socket, "status")
        assert status == {"t": "status", "state": "idle", "detail": ""}
        world = client.app.state.world
        assert world.source._task is None
        assert all(peer.video_frames.empty() and peer.audio_frames.empty() for peer in world.clients)
        socket.send_json({"t": "ping"})
        pong, _ = _receive_until(socket, "pong")
        assert pong == {"t": "pong"}


def test_root_serves_judges_page_and_kill_is_idempotent() -> None:
    with _client() as client:
        response = client.get("/")
        assert response.status_code == 200
        assert "ZAP! live world" in response.text
        assert "enter-form" in response.text
        assert "startedDemo" not in response.text
        assert client.post("/world/kill").json() == {"ok": True}
