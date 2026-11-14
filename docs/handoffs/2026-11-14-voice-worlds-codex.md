# Handoff: integration, 2026-11-14

Written by: Codex
Reason: core implementation deployed; owner confirmed the headset interaction checks

## Where things stand

- Branch: `zap/build`, based on `26db301`, uncommitted changes in the working tree. No commit or push was requested or performed.
- Brief: `docs/tasks/m1-voice-worlds.md`; the owner explicitly approved proceeding beyond M0.
- Owner selected GPT-6 Luna, supplied Reactor/OpenAI keys, and requested parent-aware child worlds. Keys are in the ignored root `.env`, mode live, verified Stable model. Do not print them.
- The designer owns the eventual skybox. Satie and visual polish remain deferred.

## Finished and checked

- Voice capture with hold/release, microphone permission flow, visible state and errors, a 20-second limit, and mono PCM WAV encoding.
- Local Faster Whisper transcription; GPT-6 Luna note creation, semantic editing, summaries, suggestions and world prompts.
- Parent IDs and multi-parent ancestry included in GPT and Orbis prompts; cycle prevention; context rebuilt from current parent notes on entry; stale initial images invalidated when context changes.
- Topic balls using XRGrabInteractable, no gravity, throwing with motion-derived velocity and damping; notes, selected state, parent connections, two-hand movement/scaling, JSON save/load with backup.
- Controller world entry/exit, stick and voice steering, in-world node editing, estimated pending-instruction state, JPEG decoding, streamed PCM audio, last-frame capture.
- Repaired Orbis command payloads, bounded video/audio buffering, variable frame sizes and batches, prompt slots, generation restart/error events, cancellation of pending connects, owner disconnect handling and a judges' page that never starts a world merely by being opened.
- Fixed Resources.Load being reached from the Unity WebSocket background thread. It is resolved on the main thread first.
- Relay: **11 offline tests passed**. Includes SDK adapter contract, resizing/batches/restart, parent-context GPT request, voice validation, bounded steering, HTTP contracts and cancelling an in-flight connect.
- Unity: **4 PlayMode tests passed**: actual XRI grab/release velocity and floating behavior, multi-parent/save recovery, WAV encoding, and stand-in stream/steer/exit. A separate **live Orbis Unity stream/steer/exit test passed** (9.5 seconds).
- Real socket smoke test against Orbis Stable received 1280x720 JPEGs, accepted steering, received 39 frames through its expected application boundary, and ended the session. Dynamic returned HTTP 429/no available capacity, so Stable is the configured default.
- Real synthetic voice WAVs were sent through the HTTP speech endpoint and GPT. Creation produced “How Underwater Volcanoes Form” under the parent “Ocean ecosystems”; the generated scene prompt included seafloor habitat context. A second WAV successfully edited the topic to “Underwater Volcanoes”. Local transcription measured approximately 1.2 seconds cold in the HTTP test and 0.4 seconds warm in a direct test.
- Final Quest development APK built successfully: `Builds/Quest/ImmerseTheBay.apk` (~76 MB), ARM64 IL2CPP. Verified the manifest includes Internet, RECORD_AUDIO and development cleartext LAN traffic. The generated relay URL is `http://10.8.181.135:8787` (this laptop's LAN IP at build time).
- Generated Unity .meta files were created by Unity, not hand-edited. Shared player HTTP settings are restored after the build. No Unity packages changed.
- The attached headset identifies as Quest 3S. USB debugging was authorized after a fresh Mac pairing key brought up the missing prompt. The new key remains active; original `adbkey` and `adbkey.pub` files are preserved under `/Users/kevindoan/.android/zap-auth-7yrd3l8z/` with `.original` suffixes. No headset authorization was bypassed.
- Installed and launched `com.immersethebay.vr` on the headset. Wi-Fi HTTP to the laptop timed out; `adb reverse tcp:8787 tcp:8787` returned a successful live-relay health check over USB.
- Added a development-only `zap-relay-url.txt` override in the app's persistent data directory. The headset currently uses `http://127.0.0.1:8787` over USB. Runtime logs confirm this override and the relay confirms the headset WebSocket connection.
- Actual headset speech requests returned HTTP 200 for transcription and GPT node generation. The headset save contains two nodes with summaries and one parent connection.
- After the final update, the owner created another topic and entered a world. The live relay reported `streaming`; `Builds/Verification/quest-live-world.png` confirms Orbis video displayed in both headset eyes. The tree-world scene includes its dinosaur ancestry context. The source is still mono video rendered to both eyes, not stereo-generated geometry.
- Headset screenshot `Builds/Verification/quest-startup.png` exposed oversized text. Reduced control and ball-note text to physical-world sizes, constrained controls to at least 0.6 m from the face, rebuilt, installed and launched the update successfully. The interactive Unity Editor was closed cleanly.
- Owner confirmed the final headset check with “It does work” after being asked to grip/throw a ball, hold B to edit it, enter with X and return with Y, and check controls readability. These core interactions are now user-verified on the Quest 3S.

## Written but not checked

- Steering responsiveness and measured frame rate have not received a separate on-headset check. Core grabbing/throwing, B-button voice editing, world entry/return and controls readability are owner-confirmed. Speech creation, persistence and live Orbis rendering also have device-side evidence.
- Live audio reached the relay protocol, but audible output on the headset has not been listened to.

## Half-done

- `Data.suggestions` is generated/persisted, but the original plan's suggested ghost-star UI is not implemented.
- The original plan's drag-across-line cutting is represented by redoing the same parent-to-child trigger gesture to remove a connection. Full visual interaction polish remains later work.

## Next steps, in order

1. Keep the relay running and USB connected for the current demo. Reapply `adb reverse tcp:8787 tcp:8787` after reconnecting USB or restarting ADB. Allow a few seconds for authorization to settle after Unity restarts ADB. The development URL file can switch networks without an APK rebuild.
2. Future QA can measure frame rate, listen to streamed audio, and assess physical steering responsiveness; do not report these as checked yet.
3. Add the designer's skybox and polish later; keep Satie deferred.

## How to run and check

```sh
cd relay
.venv/bin/python -m zap_relay
# The current session already has this server running on port 8787, live Stable.
# Its world session is controlled by the headset; the owner was using it at handoff.
env ORBIS_MODE=standin REACTOR_API_KEY= OPENAI_API_KEY= .venv/bin/python -m pytest -q
```

Full controls, configuration, privacy/data flow and build commands are in `docs/voice-worlds.md`.

Verification logs on this machine: `/tmp/zap-playmode-results.xml`, `/tmp/zap-live-unity.xml`, `/tmp/zap-quest-build-readable.log`, `/tmp/zap-relay.log`, `/tmp/zap-quest-app-usb.log`. Live stream sample: `Builds/Verification/live-world.jpg`.

## Known problems

- Orbis Dynamic was capacity-blocked. Stable is verified, but capacity is a service-side condition and can change.
- Without a headset runtime on macOS, batch PlayMode tests log OpenXR runtime-unavailable messages; the tests themselves passed.
- Local speech defaults to an English model. PyAV 19 removed an argument used by Faster Whisper's file decoder; this code bypasses that incompatibility by validating WAV and feeding normalized PCM samples directly.
- `appliesAtSeq` estimates the model's delay; it is not proof that a particular visual change happened.
- The SDK bridge emits flat mono video, not 3D geometry. Front dome mapping is the usable default.

## Requests for the coordinator

- Preserve the working USB demo configuration. Core headset interaction checks are owner-confirmed; remaining optional QA is listed above. Do not expose keys in logs or commits.
