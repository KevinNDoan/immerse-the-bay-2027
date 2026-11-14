# Voice, topic balls and worlds

Current implementation on `zap/build`, 2026-11-14. The designer's skybox and Satie are deferred.

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
```

Never put credentials in Unity, Resources, screenshots or commits. The headset sends recordings and note text to the laptop. Speech is transcribed locally with Faster Whisper; text and parent-note context go to OpenAI. The world prompt and optional last frame go to Reactor. Audio recordings are held in memory, not saved by the relay.

The local model (`base.en`, configurable with `SPEECH_MODEL`) downloads once. Warm it before a demo:

```sh
.venv/bin/python -c 'from faster_whisper import WhisperModel; WhisperModel("base.en", device="cpu", compute_type="int8")'
```

The judges' page is at `http://localhost:8787/`. Merely opening it does not create a billable world. Use Enter world or enter a ball in the headset. Close world ends the session immediately.

## Quest controls

| Action | Control |
|---|---|
| Write a new ball | Hold right A, speak a topic, release |
| Edit a ball | Point at or grab it, then hold right B, speak the edit, release |
| Grab and throw | Grip; move your hand and release grip |
| Enter a world | Left X with a ball selected, or hold a grabbed ball near your face briefly |
| Return | Left Y |
| Move/look inside a world | Left/right sticks; instructions are throttled to the generation cadence |
| Steer a world by voice | Hold A while inside the world |
| Edit the current world's saved topic | Hold B; the updated context is also sent to Orbis |
| Trigger an event | Right trigger inside the world |
| Add/remove a parent connection | Point at the parent, hold right trigger, point at the child, release; repeat to remove |
| Pull/stretch the constellation | Hold both grips in empty space and move/spread your hands |

Grant microphone access the first time prompted, then hold the button again. A recording lasts at most 20 seconds. Processing and errors are shown on the controls card. The ball appears just in front of the right controller after generation, ready to grab. Released balls slow down and float without gravity.

In the Editor, V/B hold-to-talk create/edit; Enter opens, Escape returns, arrows steer, Space triggers an event. The development panel also accepts typed text through the same note-generation path. The XR Interaction Simulator handles controller pose/grip simulation.

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

This generates the URL TextAsset through the Editor, includes microphone/Internet permissions in the generated Android manifest, and permits local HTTP for the development build. Shared HTTP player settings are restored afterward. The APK is `Builds/Quest/ImmerseTheBay.apk`.

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

- This is generated mono video shown inside a dome, not navigable 3D geometry. Front mapping is the default; panorama wrapping remains experimental.
- Steering completion uses an estimated frame boundary, not a guarantee that every visual instruction was obeyed.
- The void and note visuals are temporary. Suggested topics are generated and saved; the planned ghost-star suggestion UI is not implemented.
- Live Stable was verified. Dynamic returned a Reactor capacity error during verification.
- Local speech currently defaults to the English `base.en` model; choose a multilingual model in `SPEECH_MODEL` for other languages.
- No Satie, multiplayer, imported notes or hand tracking work was added.
