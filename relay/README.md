# ZAP! relay

Python 3.12 service for local speech, GPT-6 Luna notes and learning objects, cached World Labs geometry, and the optional Orbis video experiment.

```sh
uv sync --python 3.12 --extra live --extra speech
uv run python -m zap_relay
```

On this configured checkout, `.venv/bin/python -m zap_relay` also works.

The root `.env` is read first, then `relay/.env`; process environment variables take priority. Copy `.env.example` for the available settings. Stand-in mode needs no credentials. Live Orbis needs `REACTOR_API_KEY`; AI notes need `OPENAI_API_KEY`. The default node model is `gpt-6-luna`. Missing OpenAI credentials use clearly identified direct text edits instead of pretending to run AI.

Local speech uses Faster Whisper (`SPEECH_MODEL=base.en`), downloaded on first use. Audio must be a mono, 16-bit PCM WAV, 0.3–20 seconds. Recordings are not saved.

- Judges' page: http://localhost:8787/ (does not auto-start a session)
- Health: `GET /health`; capability/configuration flags: `GET /capabilities`
- Speech: `POST /speech/transcribe` (WAV body)
- Create/edit: `POST /nodes/update` (JSON, including optional parent notes)
- Prepare/load a world: `POST /world/prepare`; poll geometry jobs at `GET /world/jobs/{cacheKey}`
- Crystal preview: `GET /world/preview` reads the existing panorama without starting generation
- Geometry: `GET /world/assets/{cacheKey}/{filename}` serves converted meshes and textures
- Revise the interactive learning object: `POST /world/study`
- End a world immediately: `POST /world/kill`

```sh
env ORBIS_MODE=standin REACTOR_API_KEY= OPENAI_API_KEY= WORLD_LABS_API_KEY= .venv/bin/python -m pytest -q
.venv/bin/python scripts/smoke_world.py
```

The socket smoke test starts a world in the server's configured mode and closes it in `finally`. Live mode incurs Reactor usage. See [the full setup and controls](../docs/voice-worlds.md) and [protocol](../docs/protocol.md).

With a World Labs key, `WORLD_PROVIDER=auto` uses geometry for headset entry. `WORLD_PROVIDER=orbis` explicitly selects the earlier video experiment. `WORLD_GENERATION_ENABLED=false` is the default and must stay false for the owner's current one-world test budget. `WORLD_TEST_WORLD_ID` pins the already-generated test environment; other topics reuse its geometry and receive their own context-aware learning objects. Geometry is cached under ignored `.cache/geometry/`; do not delete this cache during the demo. The preview endpoint never creates a paid job. See [spatial world details](../docs/spatial-worlds.md).

For a headset demo that cannot use provider credits, run the saved-world replay service:

```sh
.venv/bin/python scripts/run_saved_world_demo.py --world-id YOUR_CACHED_WORLD_ID
```

It listens on localhost port 8793, loads no `.env` or provider keys, and requires the existing imported geometry and plan. Cached topic plans are reused; new topics receive the saved demonstration model. AI model revisions are unavailable in this mode. Forward USB port 8793 and set the development headset URL to `http://127.0.0.1:8793`. Keep the relay and USB connection available while testing. The normal relay on 8787 can remain running separately.
