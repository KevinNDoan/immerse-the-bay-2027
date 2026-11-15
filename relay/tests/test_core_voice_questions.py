"""Voice questions must work when Unity has only a reading pane selected."""
import copy

import pytest
from fastapi.testclient import TestClient

from zap_relay.app import create_app
from zap_relay.config import Settings
from zap_relay.study_models import validate_plan
from test_surround import plan


# JsonUtility's inline classes cannot represent a null reference on the wire.
EMPTY_UNITY_MODEL = {
    "title": "", "explanation": "", "eventPrompt": "", "displayScale": 0,
    "parts": [], "steps": [],
    "experiment": {"label": "", "lowLabel": "", "highLabel": "",
                   "lowExplanation": "", "highExplanation": "", "changes": []},
}
QUESTION = "Why are there watchtowers?"
ANSWER = "Watchtowers let guards observe the surrounding landscape."


@pytest.fixture
def voice_app():
    app = create_app(Settings("standin", "", "unused", 8787))
    calls = []

    class Speech:
        def transcribe(self, wav):
            return QUESTION

    async def explore(topic, context, instruction, current, focused, objects, state):
        calls.append((topic, context, instruction, current))
        return {"action": "answer", "answer": ANSWER, "model": None}

    async def study(topic, context, instruction, current, **kwargs):
        calls.append((topic, context, instruction, current))
        return plan()

    app.state.speech = Speech()
    app.state.preparation.explore = explore
    app.state.preparation.plan = study
    return app, calls


@pytest.mark.parametrize("endpoint", ["/world/explore", "/world/study"])
@pytest.mark.parametrize("current", [None, {}, EMPTY_UNITY_MODEL])
def test_transcribed_question_accepts_absent_selected_model(voice_app, endpoint, current):
    app, calls = voice_app
    with TestClient(app) as client:
        transcript = client.post("/speech/transcribe", content=b"fake microphone audio")
        assert transcript.status_code == 200
        response = client.post(endpoint, json={
            "topic": "Great Wall", "context": "Inside a watchtower",
            "instruction": transcript.json()["text"], "current": current,
            "focused": "", "objects": [], "step": -1, "parameter": 0,
        })
    assert response.status_code == 200, response.text
    assert calls == [("Great Wall", "Inside a watchtower", QUESTION, None)]
    if endpoint == "/world/explore":
        assert response.json() == {"action": "answer", "answer": ANSWER, "model": None}


@pytest.mark.parametrize("endpoint", ["/world/explore", "/world/study"])
def test_real_selected_model_reaches_planner_unchanged(voice_app, endpoint):
    app, calls = voice_app
    current = validate_plan(plan())
    with TestClient(app) as client:
        response = client.post(endpoint, json={"topic": "Flow", "context": "A flow model",
            "instruction": "What does this path show?", "current": current, "objects": ["Flow"]})
    assert response.status_code == 200, response.text
    assert calls[0][3] == current


@pytest.mark.parametrize("endpoint", ["/world/explore", "/world/study"])
@pytest.mark.parametrize("current", [
    "invalid", [], 0, False,
    {**EMPTY_UNITY_MODEL, "title": "An incomplete model"},
    {**EMPTY_UNITY_MODEL, "parts": "invalid"},
    {**plan(), "title": ""},
    {**plan(), "parts": [{**plan()["parts"][0], "shape": "script"}]},
])
def test_nonempty_or_malformed_models_still_fail_validation(voice_app, endpoint, current):
    app, calls = voice_app
    with TestClient(app) as client:
        response = client.post(endpoint, json={"topic": "Flow", "context": "A flow model",
            "instruction": QUESTION, "current": copy.deepcopy(current)})
    assert response.status_code == 400
    assert calls == []


def test_empty_question_still_rejected(voice_app):
    app, calls = voice_app
    with TestClient(app) as client:
        response = client.post("/world/explore", json={"topic": "Great Wall",
            "context": "Inside a watchtower", "instruction": " ", "current": EMPTY_UNITY_MODEL})
    assert response.status_code == 400
    assert calls == []
