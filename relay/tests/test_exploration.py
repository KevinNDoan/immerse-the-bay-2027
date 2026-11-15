import asyncio
import copy
import json

import httpx
import pytest

from zap_relay.config import Settings
from zap_relay.worlds import WorldPreparation
from zap_relay.study_models import validate_plan
from test_surround import plan


def change(part=0):
    return {"part": part, "offset": {"x": .2, "y": 0, "z": 0}, "rotation": {"x": 0, "y": 0, "z": 0},
            "scale": {"x": 1, "y": 1, "z": 1}, "visible": True}


def test_legacy_models_load_and_exploration_references_are_bounded():
    original = plan()
    clean = validate_plan(original)
    assert clean["steps"] == [] and clean["experiment"]["label"] == ""
    assert clean["parts"][0]["repeat"]["count"] == 1
    original["steps"] = [{"title": "Move the path", "explanation": "Follow the connection", "focus": [0], "changes": [change()]}]
    assert validate_plan(original)["steps"][0]["changes"][0]["offset"]["x"] == .2
    for invalid in [change(4), {**change(), "visible": "false"}]:
        sample = copy.deepcopy(original); sample["steps"][0]["changes"] = [invalid]
        with pytest.raises(ValueError): validate_plan(sample)
    sample = copy.deepcopy(original); sample["steps"][0]["changes"] = [change(), change()]
    with pytest.raises(ValueError): validate_plan(sample)
    sample = copy.deepcopy(original); sample["parts"][0]["shape"] = "helix"
    sample["parts"][0]["repeat"] = {"count": 32, "angle": 0, "step": {"x": 0, "y": 0, "z": 0}}
    with pytest.raises(ValueError): validate_plan(sample)


def test_questions_do_not_replace_models_and_creation_returns_validated_data(monkeypatch):
    replies = iter([{"action": "answer", "answer": "The path connects the two endpoints.", "model": None},
                    {"action": "add", "answer": "Here is another object.", "model": plan()}])
    requests = []
    class Client:
        def __init__(self, **kwargs): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def post(self, url, **kwargs):
            requests.append((url, kwargs["json"]))
            return httpx.Response(200, json={"status": "completed", "output": [{"content": [{"type": "output_text", "text": json.dumps(next(replies))}]}]})
    monkeypatch.setattr("zap_relay.worlds.httpx.AsyncClient", Client)
    async def scenario():
        service = WorldPreparation(Settings("standin", "", "unused", 8787, openai_key="test"))
        current = plan(); before = copy.deepcopy(current)
        answer = await service.explore("Flow", "Any subject", "What is this path?", current, "Path", ["Flow"])
        assert answer["action"] == "answer" and answer["model"] is None and current == before
        added = await service.explore("Flow", "Any subject", "Add a different object", current)
        assert added["action"] == "add" and added["model"]["parts"]
        assert all(url == "https://api.openai.com/v1/responses" for url, _ in requests)
        assert all(body["model"] == "gpt-6-luna" and body["store"] is False for _, body in requests)
    asyncio.run(scenario())
