# Core developer: everything else, and merging

Updated 2026-11-14. Read [README.md](README.md) first. Your branch is `core/app`.

## Your job

- Do Step 0 so the other two can work without touching your files.
- Own how the constellation behaves: nodes, connections, voice, note generation, saving.
- Own builds, headset installs, shared settings and documents.
- Merge everyone's pull requests into `main`.

## What you own

- `Assets/_Project/Scripts/Constellation/`, `Assets/_Project/Scripts/Core/`
- `Assets/_Project/Editor/`, `Assets/_Project/Scenes/`
- `Assets/_Project/Scripts/Net/RelayConfig.cs`, `Assets/_Project/Scripts/Net/Resources/`
- `Assets/_Project/Tests/World/ConstellationTests.cs`, `Zap.World.Tests.asmdef`
- `relay/zap_relay/app.py`, `nodes.py`, `config.py`, `__main__.py`
- `relay/tests/test_features.py`, `test_config.py`
- `relay/pyproject.toml`, `relay/uv.lock`, `relay/.env.example`, `relay/README.md`
- `ProjectSettings/`, `Packages/`, `.gitignore`, `.gitattributes`, `.mcp.json`
- `AGENTS.md`, `README.md`, and `docs/` apart from the design brief and the other two split pages

## What you do not touch

- `Assets/_Project/Art/`, `Materials/`, `Prefabs/`: the designer's. You may add the `.meta` files Unity generates for the designer's images.
- `Assets/_Project/Scripts/World/`, `RelayClient.cs`, `RelayMessages.cs`, `relay/zap_relay/sources.py`, `relay/zap_relay/static/`: the world developer's. Use only the `WorldSession` members listed in the README.

## Step 0, in order

Do this on a short branch, merge it to `main`, then tell the others to branch.

### 1. Theme

- Add `Assets/_Project/Scripts/Core/ZapTheme.cs`: a `ScriptableObject` loaded once from `Resources/ZapTheme`.
- Create the asset at `Assets/_Project/Art/Theme/Resources/ZapTheme.asset` from an editor menu item, not by hand.
- Move these values into it, keeping today's numbers as the defaults:

| From | Values |
|---|---|
| `TopicBall.cs` | Ball size (0.11), selected size (0.125), note offset below the ball (0.09), topic text size (0.38), summary text size (0.24), note box (0.62 × 0.35), distance at which the summary shows (0.85), distance at which text hides (4) |
| `ConstellationController.cs` | Line width (0.0025), line and ball colour, controls card size (0.5 × 0.32), text size range (0.22 to 0.35), minimum distance from the face (0.6) |
| `ZapColors.cs` | Void, void lift, white, paper, ink |

- Add empty slots the designer will fill: node glow image, node ring image, line image, note background image, font.
- Leave grab and physics numbers (mass, damping, collider size, throw scale) in code. They are behaviour, not look.
- From here on, `ZapTheme.cs` is yours and `ZapTheme.asset` is the designer's.

### 2. In-world controls

- Move `ControllerActions.cs` to `Assets/_Project/Scripts/Core/` (namespace `Zap.Core`). Add `Unity.InputSystem` to the references in `Zap.Core.asmdef`, and add `Zap.Core` to the references in `Zap.World.asmdef`, both by name.
- Move `UpdateWorldControls`, `SendSteer`, the pending-instruction tracking (`PromptReceived`, `FrameReceived`) and the world half of the status text out of `ConstellationController.cs` into a new `Assets/_Project/Scripts/World/WorldControls.cs`.
- `ConstellationController` keeps only: `EnterWorld`, `LeaveWorld`, hiding and showing the constellation, saving the last frame.
- Let `WorldControls` raise an event with status text, and have the controls card subscribe to it.
- Hand `WorldControls.cs` to the world developer once it is merged.

### 3. Relay

- Move `class World`, `Client`, the `/ws/world` handler and its helper functions from `relay/zap_relay/app.py` to a new `relay/zap_relay/world.py`.
- `app.py` keeps `create_app`, the HTTP routes for speech, nodes and capabilities, and wires in the world routes.
- Tests must pass unchanged: `cd relay && uv run pytest`.
- Hand `world.py` to the world developer once it is merged.

### 4. Folders

- Create `Assets/_Project/Art/UI/`, `Art/FX/`, `Art/Fonts/`, `Art/Models/`, `Art/Theme/Resources/`, each with a `.gitkeep` file.
- Open Unity once so it writes the folder `.meta` files, and commit them.

### 5. Check and announce

- Unity compiles with no errors, and the four PlayMode tests pass.
- Relay tests pass.
- Play the `Void` scene: creating, grabbing, linking, entering and leaving all behave as before.
- Merge to `main` and message the others: "Step 0 is merged, branch from main now."

## Your own work after Step 0

From the last handoff, still open on your side:

1. **Suggested topics.** They are generated and saved with each node but nothing shows them. Add the three faint stars and the grab-to-keep behaviour.
2. **Cutting a connection** by dragging across the line. Today it is removed by repeating the link gesture.
3. **Wire the theme** to the designer's images as they arrive.
4. **Node preview picture**, once the three of you agree how it should work.
5. **Demo readiness:** a fresh headset build, the relay address for the venue network, a rehearsed script, a recorded backup.

## Merge duty

For each pull request:

1. `git diff --stat origin/main...<branch>` lists only files that lane owns. If not, send it back.
2. For the designer's pull requests: check out the branch, open Unity, commit any `.meta` files it creates for new images.
3. Unity compiles; relay tests pass if `relay/` changed.
4. Merge, then tell everyone to pull `main` into their branch.

Only you change scenes, `ProjectSettings/`, `Packages/`, `AGENTS.md` and `docs/protocol.md`. When someone needs a change there, they ask and you make it.
