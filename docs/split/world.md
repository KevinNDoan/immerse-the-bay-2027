# World developer: immersion inside a node

Updated 2026-11-14. Read [README.md](README.md) first. Your branch is `world/immersion`.

## Your job

Make stepping into a node feel immersive: how the Orbis video is shown around the user, how it sounds, how it is steered, and how the user gets in and out.

## What you own

Unity
- `Assets/_Project/Scripts/World/` (dome, stream, session, audio, debug keys, the dome shader)
- `Assets/_Project/Scripts/Net/RelayClient.cs` and `RelayMessages.cs`
- `Assets/_Project/Tests/World/WorldStreamPlayModeTest.cs`
- After Step 0: `Assets/_Project/Scripts/World/WorldControls.cs` (stick, trigger and voice steering inside a world)

Laptop relay
- `relay/zap_relay/sources.py` (the Orbis connection and the stand-in picture)
- `relay/zap_relay/static/` (the judges' page), `relay/scripts/`, `relay/tests/test_relay.py`
- After Step 0: `relay/zap_relay/world.py` (the world session and its socket)

Other
- `my-visko-app/`: a web page for trying Orbis prompts. Not part of the headset app.
- This page

## What you do not touch

- `Assets/_Project/Scripts/Constellation/` and `Scripts/Core/`. Before Step 0, in-world steering still lives in `ConstellationController.cs`; send changes to the core developer until it moves.
- `relay/zap_relay/app.py`, `nodes.py`, `config.py`. Before Step 0 the world session is still in `app.py`; same rule.
- Scenes, `ProjectSettings/`, `Packages/`, `docs/protocol.md`.
- Anything under `Assets/_Project/Art/`. If you need an image, ask the designer.

## What must stay stable

The constellation opens and steers worlds through these and nothing else. Add to them freely. Do not rename or remove any without telling the core developer first.

- `WorldSession.Enter(nodeId, topic, prompt, imageBase64)`, `Exit()`, `Steer(kind, text)`
- `WorldSession.IsEntered`, `State`, and the events `OnStatus`, `OnPrompt`, `OnError`
- `WorldStream.Texture`, `WorldStream.OnFrameDecoded`
- `WorldAudio.SetMuted(bool)`
- `WorldControls.Send(kind, text)`: the constellation uses it for spoken requests and for the new scene after a note edit
- `WorldControls.StatusChanged`: the text the controls card shows while in a world

On the relay side, the message shapes in `docs/protocol.md` must keep working. If you need a new message or field on `/ws/world`, send the core developer the exact wording and they add it to the file.

## Where things stand

Checked on a Quest 3S:
- Live Orbis video shows in both eyes. It is one flat picture drawn for both eyes, not real depth.
- Entering with X, leaving with Y, and steering by stick and by voice work.
- The relay receives 1280 × 720 frames from the Stable model and sends them on as JPEGs.

Not yet checked:
- Whether world sound is audible in the headset. It reaches the headset but nobody has listened.
- Frame rate in the headset, and how long each frame takes to decode.
- How responsive steering feels. The "world updated" message is an estimate of the delay, not proof the picture changed.

Known limits:
- Orbis gives flat video only. The picture is shown on a patch in front of the user (`Front` mode, 110° by 62°); the rest of the dome is black. `Wrap` mode stretches it all the way round and looks distorted with normal footage.
- The Dynamic model was refused for lack of capacity when last tried. Stable is the default.
- A steer takes about two seconds to show.

## Work to pick from, most valuable first

1. **Listen for sound** in the headset and fix it if silent.
2. **Measure** frame rate and decode time on the headset. Tune `RELAY_MAX_WIDTH` and JPEG quality for the best picture that stays smooth.
3. **Fill the view.** Options to try: a larger or curved front patch; a blurred, colour-matched extension of the picture over the rest of the dome; prompts that ask Orbis for panoramic footage in `Wrap` mode.
4. **Add depth.** Options to try: a slight curve and distance to the picture, gentle movement of the picture against head motion, darkened edges.
5. **Entry and exit.** A transition from the node into the world and back, in place of the current instant switch.
6. **Steering feel.** A visible marker for an instruction that is on its way. The designer supplies its images (piece 5.8 in the design brief).
7. **Try the Dynamic model again** and compare it with Stable.

## Running and checking

```sh
# Relay tests (no keys needed)
cd relay && env ORBIS_MODE=standin REACTOR_API_KEY= OPENAI_API_KEY= uv run pytest -q

# Relay
cd relay && uv run python -m zap_relay
```

- In the Unity Editor: open the `Void` scene, press Play. `Enter` opens a world, arrow keys steer, `Space` triggers an event, `Escape` returns.
- Headset build and install steps are in `docs/voice-worlds.md`.
- A live Orbis session costs credits for as long as it is open. Close it with the Close world button on the judges' page, or `curl -X POST localhost:8787/world/kill`.

## Before you open a pull request

- Only files listed under "What you own" are changed.
- Relay tests pass.
- Unity opens with no red errors in the Console.
- Nothing in the "must stay stable" list was renamed or removed.
