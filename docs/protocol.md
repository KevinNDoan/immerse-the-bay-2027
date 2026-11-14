# Relay protocol v0

Updated 2026-11-14. Agent-facing. This is the contract between `relay/` and `Assets/_Project/Scripts/Net/`. Only the coordinator changes it.

## Transport

- The relay listens on `0.0.0.0:8787`. The headset reaches it over the local network.
- No TLS and no auth in v0.
- Unity takes the base URL from `Zap.Net.RelayConfig.BaseUrl`. Default `http://127.0.0.1:8787`.

## HTTP

- `GET /health` returns 200 `{"ok":true,"mode":"standin"|"live","state":"idle"|"connecting"|"streaming"|"error","model":"<model name or empty>"}`.
- `GET /` returns the judges' page: the live picture, the prompt in force, and a text box that sends a steer.
- `GET /stream.mjpg` returns the current video as multipart MJPEG.
- `POST /world/kill` closes any open Orbis session and returns 200 `{"ok":true}`.

## WebSocket `/ws/world`

- One socket per headset.
- Server to client: binary messages and JSON text messages.
- Client to server: JSON text messages only.

### Binary messages (server to client)

- 13-byte little-endian header, then the payload: `u8 type`, `u32 seq`, `u64 ts_us`.
- `type` 1: one video frame as a JPEG.
- `type` 2: audio as PCM, signed 16-bit little-endian, 48000 Hz, mono.
- `seq` starts at 0 on `enter` and rises by 1 per message, counted separately for each type.
- `ts_us` is the relay's monotonic clock in microseconds.

### JSON messages (client to server)

- `{"t":"enter","nodeId":"<id>","topic":"<text>","prompt":"<text, may be empty>","image":"<base64 JPEG, may be empty>"}`
- `{"t":"steer","kind":"move"|"look"|"event"|"voice","text":"<instruction>"}`
- `{"t":"exit"}`
- `{"t":"ping"}`

### JSON messages (server to client)

- `{"t":"status","state":"idle"|"connecting"|"streaming"|"error","detail":"<text>"}`
- `{"t":"prompt","text":"<full prompt now in force>","appliesAtSeq":<video seq>}`
- `{"t":"pong"}`
- `{"t":"error","code":"<short code>","detail":"<text>"}`

## Behaviour

- `enter` with an empty `prompt`: the relay uses `topic` as the prompt.
- `enter` in live mode: open an Orbis session, send the image if present, send the prompt, start.
- `enter` in stand-in mode: start generated frames.
- `enter` while already streaming: end the current world first.
- `steer`: the relay composes the new full prompt, applies it, and replies with `prompt`. `appliesAtSeq` is the first video frame expected to show the change.
- `exit`: stop sending frames and send `status` idle. In live mode, close the Orbis session after 45 seconds without a new `enter`.
- Socket closed by the client: treat as `exit`.
- Video target: 832x480, 18 frames per second, JPEG quality 80.
- The relay sends only the newest frame. If a socket is slow, drop frames; never queue them.
- Stand-in frames show the topic, the frame number and the prompt in force, and move visibly from frame to frame. A steer shows up 1.8 seconds after it is received, to mimic Orbis.
