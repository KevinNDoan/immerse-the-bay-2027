"""Serve an already-imported world and teaching model without loading any API keys."""
from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path

import uvicorn

from zap_relay.app import create_app
from zap_relay.config import DEFAULT_MODEL, Settings
from zap_relay.worlds import validate_plan


class SavedStudy:
    def __init__(self, plan):
        self.plan_data = validate_plan(plan)

    async def plan(self, topic, context, **kwargs):
        return copy.deepcopy(self.plan_data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--world-id", required=True, help="An existing, locally imported world ID")
    parser.add_argument("--model", default="marble-1.1")
    parser.add_argument("--port", type=int, default=8793)
    parser.add_argument("--enable-openai", action="store_true", help="Enable object requests only; never load environment provider keys")
    parser.add_argument("--study-plan", type=Path, help="An explicit saved exploration model for this demo")
    args = parser.parse_args()
    # Deliberately do not call load_settings/load_dotenv. Even unexpected video
    # requests use the local stand-in; neither provider receives credentials.
    openai_key, node_model = "", "gpt-6-luna"
    if args.enable_openai:
        from dotenv import dotenv_values
        root = Path(__file__).resolve().parents[2]
        values = {**dotenv_values(root / "relay" / ".env"), **dotenv_values(root / ".env")}
        openai_key = (values.get("OPENAI_API_KEY") or "").strip()
        node_model = values.get("NODE_MODEL") or node_model
        if not openai_key: parser.error("OPENAI_API_KEY is missing. Offline replay remains available without --enable-openai.")
    settings = Settings("standin", "", DEFAULT_MODEL, args.port, openai_key=openai_key, node_model=node_model, panorama_model="",
                        world_provider="worldlabs", worldlabs_model=args.model,
                        world_test_world_id=args.world_id, world_generation_enabled=False)
    app = create_app(settings)
    spatial = app.state.spatial
    shared = spatial._folder(spatial.pinned_key)
    if not (shared / "manifest.json").is_file() or not (shared / "plan.json").is_file():
        parser.error("This world is not fully cached. Import the existing export first; no generation was started.")
    saved = SavedStudy(json.loads((args.study_plan or shared / "plan.json").read_text()))
    spatial.study = saved
    if args.study_plan: spatial.demo_plan = saved.plan_data
    spatial.enabled = True
    print("Saved world: environment generation disabled; object AI " + ("enabled." if openai_key else "disabled."), flush=True)
    uvicorn.run(app, host="127.0.0.1", port=args.port)


if __name__ == "__main__":
    main()
