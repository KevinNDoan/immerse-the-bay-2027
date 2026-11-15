# Voice, topic balls and worlds

Current implementation on `zap/build`, 2026-11-14. The designer's skybox and Satie are deferred. The current curated demo and its preparation commands are documented in `relay/demo/README.md`.

## Run

Open `Assets/_Project/Scenes/Void.unity` in Unity 6000.6.9f1. Start the relay on the laptop:

```sh
cd relay
.venv/bin/python -m zap_relay
```

On another machine, install Python 3.12 and uv, then run `uv sync --python 3.12 --extra live --extra speech` in `relay/`. Run with `uv run python -m zap_relay`.

The root `.env` is read first, with `relay/.env` as a fallback. Exported environment variables override both. Both files are ignored by Git. Required for live use:

```dotenv
ORBIS_MODE=live
ORBIS_MODEL=reactor/visko-orbis-stable
REACTOR_API_KEY=<your key>
OPENAI_API_KEY=<your key>
NODE_MODEL=gpt-6-luna
WORLD_LABS_API_KEY=<your key>
WORLD_PROVIDER=auto
WORLD_GENERATION_ENABLED=false
WORLD_TEST_WORLD_ID=<the one existing test world>
```

Never put credentials in Unity, Resources, screenshots or commits. The headset sends recordings and note text to the laptop. Speech is transcribed locally with Faster Whisper; text and parent-note context go to OpenAI. Geometry mode loads cached World Labs assets. The optional Orbis experiment sends the world prompt and starting image to Reactor. Audio recordings are held in memory, not saved by the relay. Keep runtime generation disabled; the owner authorizes individual prepared demo worlds separately.

The local model (`base.en`, configurable with `SPEECH_MODEL`) downloads once. Warm it before a demo:

```sh
.venv/bin/python -c 'from faster_whisper import WhisperModel; WhisperModel("base.en", device="cpu", compute_type="int8")'
```

The judges' page is at `http://localhost:8787/`. Merely opening it does not create a billable world. Use Enter world or enter a ball in the headset. Close world ends the session immediately.

## Quest controls

| Action | Control |
|---|---|
| Write a new ball | Press A: a glowing orb appears immediately at the right hand. Hold while speaking, then release; inside a world, the current world becomes its parent |
| Edit a ball | Point at or grab it, then hold right B, speak the edit, release |
| Grab and place | Grip; gentle release leaves the ball floating |
| Enter a world | Deliberately throw the ball forward, or press left X with it selected |
| Cancel unfinished speech / idea | Left Y while recording, transcribing or waiting for an AI response; release the voice button before trying again |
| Return | Left Y when no voice request is pending |
| Travel to a linked idea | Parent and child orbs appear to the left inside a world. Select one and press X, or grip and throw it |
| Move/look inside a world | Left stick accelerates into walking and slides along walls; right stick snap-turns when not pointing at an object |
| Ask, add or revise an object | Hold left trigger, speak, release. A question produces an explanation; a request for another object adds it; explicit revisions change the selected object |
| Edit the current world's saved topic | Hold B; if a child ball is selected, B edits that child instead |
| Inspect an object | Grip moves/rotates; two grips resize; while pointing/holding, right stick up/down zooms and left/right adjusts its experiment |
| Move a reading pane | Grip the glass pane itself, move/rotate it, then release. It stays independently of the object and its placement is saved |
| Discover more | Right trigger advances the selected object’s exploration steps; right stick click resets its experiment |
| Reposition/pause an object | X brings the selected object and its pane in front of you when no child ball is selected; left stick click pauses animation |
| Add/remove a parent connection | Point at the parent, hold right trigger, point at the child, release; repeat to remove |
| Pull/stretch the constellation | Hold both grips in empty space and move/spread your hands |

Grant microphone access the first time prompted, then hold the button again. A recording lasts at most 20 seconds. The world pane shows listening, transcription, thinking, answers and retry details; the left-hand display holds controls and short status messages, without repeating the explanation. The question targets the object selected when recording begins. Hovering another object does not hide the answer; grabbing or deliberately using an object's controls changes the reading pane.

The orb appears at button-down, pulses while A is held, and stays at the hand while transcription and writing finish. Y cancels an unfinished request before leaving the world: microphone capture stops, the temporary orb disappears, and late responses cannot create a node or overwrite another request. Empty or failed recordings likewise remove the unfinished orb without saving a node. Cancellation discards the pending result; it does not delete a previously completed idea. Released balls slow down and float without gravity.

In the Editor, V/B hold-to-talk create/edit, N asks; Enter opens, Escape cancels a pending request or returns, arrows move/turn, Space triggers an event. The development panel also accepts typed text through the same note-generation path. The XR Interaction Simulator handles controller pose/grip simulation.

## Parent context

Creating a ball with a node selected makes that node its parent. Creating another child carries the ancestor chain forward. Connections can add multiple parents. Topics and summaries from up to six ancestors go into both GPT note generation and the Orbis entry prompt. Parent links are explicit IDs, independent of where a ball is thrown. Cycles are refused.

Prompts are rebuilt from current parent content when entering a world. Saved frames are discarded when their associated context changes, so a new context is not anchored to an obsolete picture. Edits preserve the node ID, position and parent links. Nodes, positions, relationships, summaries, suggestions and visited-world frames are saved in `Application.persistentDataPath/zap-constellation.json`, with a previous-save backup.

## Build and install

The headset and laptop must be on a network allowing device-to-device traffic. Set the laptop's LAN URL when building; `localhost` inside a headset means the headset itself.

```sh
ZAP_RELAY_URL=http://<laptop-ip>:8787 /Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" -buildTarget Android \
  -executeMethod ImmerseTheBay.Editor.QuestBuild.BuildDevelopment -logFile /tmp/zap-quest-build.log
```

This bakes the URL into the APK, includes microphone/Internet permissions in the generated Android manifest, and permits local HTTP for the development build. The APK is `Builds/Quest/ImmerseTheBay.apk`.

The build restores `Assets/_Project/Scripts/Net/Resources/zap_relay_url.txt` afterward, so the tracked file stays empty and one laptop's address is never committed. Without `ZAP_RELAY_URL`, the APK uses `http://127.0.0.1:8787`, which works over USB with `adb reverse` (below).

In the Unity Editor the baked address is ignored: the Editor always uses `http://127.0.0.1:8787` unless a `zap-relay-url.txt` override exists in the app's data folder. Plain HTTP is allowed in the Editor and in development builds, not in release builds (`insecureHttpOption: 1` in `ProjectSettings/ProjectSettings.asset`).

Enable developer mode, connect USB, accept USB debugging inside the headset, then install with Android SDK `adb install -r Builds/Quest/ImmerseTheBay.apk`. Launch package `com.immersethebay.vr`.

For a USB demo when Wi-Fi blocks device-to-device traffic, development builds also read `zap-relay-url.txt` from `Application.persistentDataPath` at startup. It must contain an HTTP(S) base address, with no credentials, path, query or fragment. Configure the installed app with:

```sh
adb reverse tcp:8787 tcp:8787
printf '%s\n' 'http://127.0.0.1:8787' > /tmp/zap-relay-url.txt
adb push /tmp/zap-relay-url.txt /sdcard/Android/data/com.immersethebay.vr/files/zap-relay-url.txt
adb shell am force-stop com.immersethebay.vr
adb shell monkey -p com.immersethebay.vr -c android.intent.category.LAUNCHER 1
```

Keep USB connected and reapply `adb reverse` after reconnecting or restarting ADB. To return to wireless operation, put the laptop's reachable LAN URL in the same file and restart the app. Release builds ignore this override.

## Verification

On 2026-11-14, the owner confirmed grabbing/throwing, B-button voice editing, X/Y world entry/return, and controls readability on the installed Quest 3S build. Device logs and screenshots also verified speech-to-GPT node creation, saved parent connections across an app update, and live Orbis video displayed in both eyes over USB.

Offline relay tests: `cd relay && env ORBIS_MODE=standin REACTOR_API_KEY= OPENAI_API_KEY= .venv/bin/python -m pytest -q`.

Start a stand-in relay for Unity tests with `ORBIS_MODE=standin RELAY_PORT=8787 .venv/bin/python -m zap_relay`. Unity PlayMode tests in `Zap.World.Tests` cover stream/steer/exit, grab-and-throw motion without gravity, PCM WAV encoding, multi-parent traversal, and save/load recovery. Set `ZAP_TEST_RELAY_URL` to override the target. Live test execution creates a paid Reactor session; the test closes it on success, and `POST /world/kill` is the emergency cleanup.

`relay/scripts/smoke_world.py --url http://127.0.0.1:8787` checks a real relay socket, JPEG frames and steering, and closes the session in `finally`.

## Current boundaries

- Geometry mode loads actual environment and collision meshes. Learning objects float independently of furniture, and remain where released. Up to six objects per idea are saved with their poses, exploration step and parameter under `Application.persistentDataPath/exploration/`. The one existing test world is reused for every topic under the owner's credit limit. See `docs/spatial-worlds.md`.
- The imported environment is static. Teaching objects animate and respond to manipulation/voice; arbitrary environment inhabitants are not yet independently animated or interactive.
- `WORLD_PROVIDER=orbis` retains the video experiment. It does not provide shared scene geometry, stereo depth or reliable spatial persistence; it is not the current geometry-mode background.
- Steering completion uses an estimated frame boundary, not a guarantee that every visual instruction was obeyed.
- The void and note visuals are temporary. Suggested topics are generated and saved; the planned ghost-star suggestion UI is not implemented.
- Live Stable was verified. Dynamic returned a Reactor capacity error during verification.
- Local speech currently defaults to the English `base.en` model; choose a multilingual model in `SPEECH_MODEL` for other languages.
- No Satie, multiplayer, imported notes or hand tracking work was added.

## Show the headset view on the laptop (USB)

Install once with `brew install scrcpy` and `brew install --cask android-platform-tools`. With the headset plugged in and awake:

```sh
scrcpy --crop=1832:1920:0:0 --no-audio --no-control --max-size=1280 --window-title="ZAP headset view"
```

The crop shows the left eye of a Quest 3S (screen 3664x1920). Check another headset's size with `adb shell wm size` and use half its width. The window is black while the headset is off your head. A Unity headset build restarts the USB service and closes the window; run the command again afterwards.
