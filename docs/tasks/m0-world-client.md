# Brief: Unity world client and dome (lane: world)

Updated 2026-11-14. Read `AGENTS.md` and `docs/protocol.md` first. Edit only `Assets/_Project/Scripts/Net/`, `Assets/_Project/Scripts/World/`, `Assets/_Project/Tests/World/` and your handoff file.

## Goal

Unity connects to the relay, shows the incoming video on a dome around the user, and sends steering text. It works against the relay's stand-in mode.

## Deliver

### `Assets/_Project/Scripts/Net/` (assembly `Zap.Net`, namespace `Zap.Net`)

- `Zap.Net.asmdef`.
- `RelayConfig.cs`: static `BaseUrl`. Default `http://127.0.0.1:8787`. If a `TextAsset` named `zap_relay_url` exists in any `Resources/` folder, use its trimmed text. Also expose the matching `ws://` URL for `/ws/world`.
- `RelayClient.cs`: one `System.Net.WebSockets.ClientWebSocket` to `/ws/world`.
  - Receive on a background task. Hand results to the main thread through a queue drained in `Update`.
  - Parse the 13-byte binary header. Keep only the newest video frame; drop older ones.
  - Events: video frame (bytes, seq), status, prompt (text, appliesAtSeq), error.
  - Methods: `Enter(nodeId, topic, prompt, imageBase64)`, `Steer(kind, text)`, `Exit()`.
  - Reconnect after a drop with backoff: 1, 2, 4, then 8 seconds.
  - Close the socket in `OnDestroy` and on application quit.
- Message classes for `JsonUtility`, one per direction, with every field in `docs/protocol.md`.

### `Assets/_Project/Scripts/World/` (assembly `Zap.World`, namespace `Zap.World`, references `Zap.Net`)

- `Zap.World.asmdef`. Reference `Unity.InputSystem` and `Zap.Net` by name.
- `Resources/ZapDome.shader`: shader name `Zap/Dome`. Unlit, one texture, `Cull Front`, URP-compatible, single-pass instanced stereo. No lighting, no fog.
- `DomeView.cs`: builds an inside-out sphere mesh in code, radius 5 m, at least 64 by 32 segments.
  - Follows the main camera's position every frame, never its rotation.
  - Hidden until a world is entered.
  - Two mapping modes, switchable at run time:
    - `Wrap`: the frame is stretched over the whole sphere as an equirectangular panorama.
    - `Front`: the frame is shown undistorted on a patch in front of the user, 110 degrees wide and 62 degrees tall; the rest of the dome is black.
  - Default mode is `Wrap`.
- `WorldStream.cs`: decodes the newest JPEG into one reused `Texture2D` with `LoadImage` on the main thread, at most once per `Update`.
- `WorldSession.cs`: the public entry point. `Enter(nodeId, topic)`, `Exit()`, `Steer(kind, text)`, a `State` property, and events for prompt and status.
- `WorldInstaller.cs`: creates a root GameObject named `[ZAP World]` with the pieces above from `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`.
- `WorldDebugKeys.cs`: Editor and development builds only, using `UnityEngine.InputSystem.Keyboard.current`.
  - `E` enters a test topic "volcanoes". `Q` exits. `M` switches mapping mode.
  - Arrow keys send `move` steers ("move forward", "move back", "move left", "move right").
  - Log every status and prompt message.

### `Assets/_Project/Tests/World/` (assembly `Zap.World.Tests`)

- A PlayMode test assembly definition that references `Zap.Net`, `Zap.World`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner` and has `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`.
- `WorldStreamPlayModeTest.cs`: one `[UnityTest]`.
  - Call `GET /health` on `RelayConfig.BaseUrl`. If it does not answer within 2 seconds, `Assert.Ignore` with a message saying the relay is not running.
  - Otherwise enter a world, wait up to 10 seconds, and assert that at least 5 video frames arrived, the dome texture is 832x480, and the state is streaming.
  - Send one steer and assert a prompt message arrives within 5 seconds.
  - Exit and assert the state returns to idle.

## Constraints

- Ignore binary messages of type 2 (audio) for now. Do not throw on them.
- No allocations per frame beyond the JPEG buffer the socket hands you.
- No scene, prefab or material files. Create the material in code from `Shader.Find("Zap/Dome")`.
- If `Shader.Find` returns null, log one clear error and keep running.
- Do not reference anything in `Assets/_Project/Scripts/Core/`. That lane is being written at the same time.

## Done when

- Every file above exists and you have re-read each for compile errors.
- Your handoff lists exactly what you could not check. You cannot compile or run Unity; the coordinator will.

## Not in this brief

- Audio playback.
- Controller input for steering.
- The blurred surround for `Front` mode.
- Entering a world by pulling a star to the face.
