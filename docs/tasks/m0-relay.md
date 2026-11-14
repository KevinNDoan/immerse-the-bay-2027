# Brief: relay with Orbis connection (lane: relay)

Updated 2026-11-14. Read `AGENTS.md` and `docs/protocol.md` first. Edit only `relay/` and your handoff file.

## Goal

A Python service that implements `docs/protocol.md` in full. It runs in stand-in mode with no keys, and is wired to Orbis so that adding one key switches it to live video.

## Deliver

- `relay/pyproject.toml`: Python 3.12, managed with `uv`. Dependencies: `fastapi`, `uvicorn[standard]`, `pillow`, `numpy`, `python-dotenv`. Put `reactor-sdk>=1.14.0` in an optional extra named `live`. Put `pytest`, `httpx` and a WebSocket test client in a dev group.
- `relay/zap_relay/__main__.py`: starts the server on `RELAY_PORT` (default 8787).
- `relay/zap_relay/config.py`: reads settings from the environment and `relay/.env`.
- `relay/zap_relay/app.py`: the HTTP routes and `/ws/world`.
- `relay/zap_relay/sources.py`: one interface, two implementations.
  - `StandinSource`: generated frames as described in the protocol.
  - `ReactorSource`: Orbis through `reactor-sdk`.
- `relay/zap_relay/static/index.html`: the judges' page. Plain HTML and JavaScript, no build step, white on near-black.
- `relay/.env.example`: every key with an empty value, one comment line each.
- `relay/.gitignore`: ignores `.env`, `.venv`, caches.
- `relay/README.md`: how to run, how to add the key, how to switch modes. Under 40 lines.
- `relay/tests/`: tests listed under Done when.

## Settings

- `ORBIS_MODE`: `standin` (default) or `live`.
- `REACTOR_API_KEY`: empty by default. If `ORBIS_MODE=live` and it is empty, refuse to start with a one-line message naming the variable.
- `ORBIS_MODEL`: default `reactor/visko-orbis-stable`.
- `RELAY_PORT`: default 8787.

## Orbis facts (from Reactor's docs, checked 2026-11-14)

- Docs: https://docs.reactor.inc/sdk-reference/python/reactor.md , https://docs.reactor.inc/sdk-reference/python/track.md , https://docs.reactor.inc/model-api-reference/visko-orbis-stable/schema.md
- Fetch those pages if your tool can. If it cannot, build from the facts below.
- Client: `Reactor(model_name="reactor/visko-orbis-stable", api_key=...)`, then `connect()`.
- Tracks: `main_video` and `main_audio`.
- Video frames arrive through `@track.on_frame` as an `(H, W, 3)` `uint8` RGB NumPy array.
- Audio arrives as `(samples, channels)` `int16` plus `sample_rate` (48000, mono).
- Commands: `set_image` (16:9 image), `set_prompt`, `start`, `pause`, `resume`, `reset`, `set_seed`, `set_resolution`, `set_audio_prompt`, `set_audio_enabled`.
- A new prompt takes effect at the next chunk boundary. A chunk is 33 frames, 1.833 seconds.
- Generated size is 832x480 at 18 frames per second.
- Billing runs while a session is open. Always close the session on `exit` timeout, `POST /world/kill` and shutdown.

## Rules for the live path

- Import `reactor-sdk` only inside `ReactorSource`, so stand-in mode works without it installed.
- You cannot test live mode. Put the doc URL in a comment above each SDK call.
- Mark every call whose name or arguments you could not confirm in the docs with `# UNVERIFIED`, and list them in your handoff.
- Never print or log the key.

## Done when

- `cd relay && uv run pytest` passes with no keys and no network. Tests cover:
  - `GET /health` returns the documented shape with `mode` `standin`.
  - `enter` on `/ws/world` produces a `status` streaming message and binary video messages with a correct 13-byte header, rising `seq`, and a payload that decodes as an 832x480 JPEG.
  - `steer` produces a `prompt` message, and the frame at `appliesAtSeq` arrives 1.8 seconds later, within 0.3 seconds.
  - `exit` stops frames and sends `status` idle.
  - `ORBIS_MODE=live` with an empty key refuses to start.
- `uv run python -m zap_relay` starts, and `curl -s localhost:8787/health` answers.
- The judges' page at `http://localhost:8787/` shows the moving stand-in picture and a typed steer changes the prompt shown.
- A handoff file exists, written from `docs/handoffs/TEMPLATE.md`.

## Not in this brief

- Speech-to-text and LLM endpoints.
- Audio in stand-in mode. Send no type 2 messages there.
