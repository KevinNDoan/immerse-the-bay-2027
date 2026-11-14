# ZAP! relay

Python 3.12 service for local speech transcription, GPT-6 Luna notes, and Orbis video/audio.

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
- End a world immediately: `POST /world/kill`

```sh
env ORBIS_MODE=standin REACTOR_API_KEY= OPENAI_API_KEY= .venv/bin/python -m pytest -q
.venv/bin/python scripts/smoke_world.py
```

The socket smoke test starts a world in the server's configured mode and closes it in `finally`. Live mode incurs Reactor usage. See [the full setup and controls](../docs/voice-worlds.md) and [protocol](../docs/protocol.md).
