import asyncio
import copy
import json

import httpx
import pytest
from fastapi.testclient import TestClient

from zap_relay.app import create_app
from zap_relay.config import Settings
from zap_relay.object_actions import validate_inventory, validate_reply
from zap_relay.worlds import WorldPreparation
from test_surround import plan


def model(title):
    return {**plan(), "title": title}


def inventory():
    return [{"id": "dna", "model": model("DNA")}, {"id": "cell", "model": model("Cell")}]


def action(kind, target="", title=None):
    return {"action": kind, "targetId": target, "model": model(title) if title else None}


def reply(*actions):
    return {"action": "apply", "answer": "Your requested objects are updated.", "actions": list(actions)}


def test_combined_add_remove_at_capacity_and_named_revision():
    objects = inventory() + [{"id": str(i), "model": model(str(i))} for i in range(4)]
    result = validate_reply(reply(action("add", title="RNA"), action("remove", "dna")), objects)
    assert result["actions"][0]["model"]["title"] == "RNA"
    assert result["actions"][1] == action("remove", "dna")
    result = validate_reply(reply(action("revise", "cell", "Cell with a nucleus")), objects)
    assert result["actions"][0]["targetId"] == "cell"
    with pytest.raises(ValueError, match="six"):
        validate_reply(reply(action("add", title="RNA")), objects)


@pytest.mark.parametrize("bad", [
    reply(action("remove", "unknown")),
    reply(action("remove", "dna"), action("revise", "dna", "RNA")),
    reply(action("add", "dna", "RNA")),
    reply(action("remove", "dna", "RNA")),
    reply(action("revise", "dna")),
    reply(action("execute", "dna")),
    reply(),
    {"action": "answer", "answer": "Done", "actions": [action("remove", "dna")]},
])
def test_invalid_actions_do_not_pass_to_client(bad):
    with pytest.raises(ValueError):
        validate_reply(bad, inventory())


def test_inventory_identity_and_geometry_are_validated():
    objects, selected = validate_inventory(inventory(), "cell")
    assert selected == "cell" and objects[0]["model"]["title"] == "DNA"
    for items, selected in [(inventory(), "missing"), (inventory() * 2, ""), ([{"id": "dna", "model": {}}], "")]:
        with pytest.raises(ValueError):
            validate_inventory(items, selected)


def test_http_object_actions_use_named_inventory_and_preserve_legacy_clients(monkeypatch):
    app = create_app(Settings("standin", "", "unused", 8787))
    calls = []

    async def explore(*args, **kwargs):
        calls.append((args, kwargs))
        return {"action": "answer", "answer": "An explanation.", "model": None, "actions": []}

    app.state.preparation.explore = explore
    payload = {"topic": "Biology", "context": "A lab", "instruction": "Create RNA and delete DNA",
        "current": {"title": "", "parts": []}, "objectActions": True,
        "selectedId": "cell", "objectStates": inventory(), "objects": ["DNA", "Cell"]}
    with TestClient(app) as client:
        assert client.post("/world/explore", json=payload).status_code == 200
        assert calls[-1][0][3]["title"] == "Cell"
        assert calls[-1][1]["selected_id"] == "cell"
        assert calls[-1][1]["scene_objects"][0]["id"] == "dna"
        payload["selectedId"] = "missing"
        assert client.post("/world/explore", json=payload).status_code == 400
        assert len(calls) == 1
        payload["objectActions"] = False
        assert client.post("/world/explore", json=payload).status_code == 200
        assert calls[-1][1] == {}


def test_planner_executes_compound_request_and_keeps_conceptual_answers(monkeypatch):
    requests = []
    responses = iter([reply(action("add", title="RNA"), action("remove", "dna")),
        {"action": "answer", "answer": "DNA and RNA differ in their sugars and bases.", "actions": []}])

    class Client:
        def __init__(self, **kwargs): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def post(self, url, **kwargs):
            requests.append(kwargs["json"])
            return httpx.Response(200, json={"output": [{"content": [{"type": "output_text",
                "text": json.dumps(next(responses))}]}]})

    monkeypatch.setattr("zap_relay.worlds.httpx.AsyncClient", Client)

    async def scenario():
        service = WorldPreparation(Settings("standin", "", "unused", 8787, openai_key="test"))
        objects, selected = validate_inventory(inventory(), "cell")
        original = copy.deepcopy(objects)
        result = await service.explore("Biology", "A lab", "Could you create RNA and delete the DNA?",
            scene_objects=objects, selected_id=selected)
        assert [item["action"] for item in result["actions"]] == ["add", "remove"]
        answer = await service.explore("Biology", "A lab", "How are DNA and RNA different?",
            scene_objects=objects, selected_id=selected)
        assert answer["action"] == "answer" and answer["actions"] == []
        assert objects == original

    asyncio.run(scenario())
    sent = json.loads(requests[0]["input"])
    assert sent["selectedObjectId"] == "cell" and sent["objectsInSpace"][0]["id"] == "dna"
    assert "could you" in requests[0]["instructions"]
    assert requests[0]["text"]["format"]["schema"]["properties"]["action"]["enum"] == ["apply", "answer"]
