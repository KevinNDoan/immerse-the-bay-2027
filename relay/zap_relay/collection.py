"""Explicit node-to-world bindings for a prepared demo; no provider operations."""
from __future__ import annotations

import copy
import json
import re
from pathlib import Path


def validate_collection(data):
    if not isinstance(data, dict) or not isinstance(data.get("id"), str):
        raise ValueError("A collection ID is required.")
    worlds = data.get("worlds")
    if not isinstance(worlds, list) or not 1 <= len(worlds) <= 12:
        raise ValueError("A collection must contain between one and twelve worlds.")
    ids = [w.get("id") for w in worlds if isinstance(w, dict)]
    if len(ids) != len(worlds) or len(set(ids)) != len(ids) or any(not isinstance(i, str) or not re.fullmatch(r"[A-Za-z0-9_-]{1,128}", i) for i in ids):
        raise ValueError("Collection node IDs must be unique and safe.")
    by_id = {w["id"]: w for w in worlds}
    for world in worlds:
        if not isinstance(world.get("topic"), str) or not world["topic"].strip():
            raise ValueError("Every world needs a title.")
        pane = world.get("pane")
        if not isinstance(pane, dict) or not pane.get("title") or not pane.get("text"):
            raise ValueError("Every world needs a reading pane, including worlds without artifacts.")
        parents = world.get("parentIds", [])
        if not isinstance(parents, list) or len(set(parents)) != len(parents) or any(p not in by_id for p in parents):
            raise ValueError("Every parent must be another node in this collection.")
        key = world.get("cacheKey")
        if key and not re.fullmatch(r"[a-f0-9]{64}", key):
            raise ValueError("Invalid prepared-world cache key.")
        if world.get("textureProjection", "atlas") not in ("atlas", "panorama"):
            raise ValueError("Unsupported surface texture projection.")
    def visit(node, trail):
        if node in trail: raise ValueError("Parent links must not contain a cycle.")
        for parent in by_id[node].get("parentIds", []): visit(parent, trail | {node})
    for node in ids: visit(node, set())
    return data


class WorldCollection:
    def __init__(self, path):
        self.path = Path(path)
        self._stamp = None
        self._data = None
        self.data()

    def data(self):
        stamp = self.path.stat().st_mtime_ns
        if stamp != self._stamp:
            self._data = validate_collection(json.loads(self.path.read_text()))
            self._stamp = stamp
        return self._data

    def entry(self, node_id):
        return next((w for w in self.data()["worlds"] if w["id"] == node_id), None)

    def contains_key(self, key):
        return any(w.get("cacheKey") == key for w in self.data()["worlds"])

    def prepare(self, payload, spatial):
        world = self.entry(payload.get("nodeId"))
        if world is None:
            return {"state": "error", "projection": "geometry", "notice": "This idea has no prepared world in the demo. Choose a ready demo idea."}
        if not world.get("cacheKey"):
            return {"state": "error", "projection": "geometry", "notice": "This demo world is awaiting generation. No credits were spent by opening it."}
        result = spatial.status(world["cacheKey"])
        result["pane"] = copy.deepcopy(world["pane"])
        result["plan"] = copy.deepcopy(world.get("plan"))
        return result

    def seed(self, spatial):
        """Only completed worlds enter the playable constellation. Keep future links in the catalog."""
        ready = {}
        for world in self.data()["worlds"]:
            key = world.get("cacheKey")
            if key:
                try:
                    if spatial.status(key).get("state") == "ready": ready[world["id"]] = world
                except FileNotFoundError: pass
        # A child must never be published without its parent.
        while True:
            missing = [node for node,w in ready.items() if any(p not in ready for p in w.get("parentIds", []))]
            if not missing: break
            for node in missing: ready.pop(node)
        nodes = [{"id": w["id"], "topic": w["topic"], "summary": w.get("summary", ""),
                  "worldPrompt": w.get("context", w["topic"]), "parentIds": w.get("parentIds", []),
                  "position": w["position"], "image": "", "imageContext": "", "suggestions": []} for w in ready.values()]
        return {"version": 1, "nodes": nodes, "edges": [{"from": p, "to": w["id"]} for w in ready.values() for p in w.get("parentIds", [])]}
