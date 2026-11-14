# ZAP! relay

Stand-in video for the headset and the judges' page. One Reactor key switches the same server to live Orbis.

## Run

```
cd relay
uv run python -m zap_relay
```

Health: `curl -s localhost:8787/health`

Judges' page: http://localhost:8787/

Tests: `uv run pytest`

The port defaults to 8787 (`RELAY_PORT`).

## Add the key

```
cp .env.example .env
```

Put the Reactor key in `REACTOR_API_KEY`. `.env` is gitignored. The process never prints the key.

```
uv sync --extra live
```

## Switch modes

`ORBIS_MODE=standin` is the default. It needs no key and no network.

`ORBIS_MODE=live` streams Orbis. An empty `REACTOR_API_KEY` refuses to start and names that variable.

`ORBIS_MODEL` defaults to `reactor/visko-orbis-stable`.
