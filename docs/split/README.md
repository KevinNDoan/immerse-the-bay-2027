# ZAP! work split

Updated 2026-11-14. For all three of us. Read this page, then your own page:

- Designer: [designer.md](designer.md)
- World developer: [world.md](world.md)
- Core developer: [core.md](core.md)

## Who does what

| Lane | Person | Covers | Branch |
|---|---|---|---|
| design | Designer | How nodes, lines, notes and on-screen text look: shapes, sizes, spacing, images | `design/nodes` |
| world | Second developer | Everything inside a node's world: the video, the dome, sound, steering | `world/immersion` |
| core | Lead developer | Everything else: how the constellation behaves, voice, note generation, builds, merging | `core/app` |

## The one rule

Edit only the files your lane owns. If you need a change in someone else's file, ask that person. Do not make it yourself, even if it is one line.

Almost every merge conflict comes from two people editing the same file. The table below makes sure that does not happen.

## Who owns which files

| Path | Owner |
|---|---|
| `Assets/_Project/Art/` | design |
| `Assets/_Project/Materials/`, `Assets/_Project/Prefabs/` | design |
| `docs/design-system.md` | design |
| `Assets/_Project/Scripts/World/` | world |
| `Assets/_Project/Scripts/Net/RelayClient.cs`, `RelayMessages.cs` | world |
| `Assets/_Project/Tests/World/WorldStreamPlayModeTest.cs` | world |
| `relay/zap_relay/sources.py` | world |
| `relay/zap_relay/static/`, `relay/scripts/`, `relay/tests/test_relay.py` | world |
| `my-visko-app/` (web page for trying Orbis prompts; not part of the headset app) | world |
| `Assets/_Project/Scripts/Constellation/`, `Assets/_Project/Scripts/Core/` | core |
| `Assets/_Project/Editor/`, `Assets/_Project/Scenes/` | core |
| `Assets/_Project/Scripts/Net/RelayConfig.cs`, `Assets/_Project/Scripts/Net/Resources/` | core |
| `Assets/_Project/Tests/World/ConstellationTests.cs`, `Zap.World.Tests.asmdef` | core |
| `relay/zap_relay/app.py`, `nodes.py`, `config.py`, `__main__.py` | core |
| `relay/tests/test_features.py`, `test_config.py` | core |
| `relay/pyproject.toml`, `relay/uv.lock`, `relay/.env.example`, `relay/README.md` | core |
| `ProjectSettings/`, `Packages/`, `.gitignore`, `.gitattributes`, `.mcp.json` | core |
| `AGENTS.md`, `README.md`, `docs/` (everything not listed above) | core |
| Your own page in `docs/split/` and your own files in `docs/handoffs/` | each lane |

New files go in a folder you own. New relay tests go in a new file named for your lane, for example `relay/tests/test_world_dome.py`.

## The three files that mix all our work

These are the real danger. Each one holds work from more than one lane today.

| File | What is mixed in it |
|---|---|
| `Assets/_Project/Scripts/Constellation/ConstellationController.cs` | Node behaviour and voice (core), stick and trigger steering inside a world (world), line width, text sizes and the controls card layout (design) |
| `Assets/_Project/Scripts/Constellation/TopicBall.cs` | Grabbing and throwing (core), ball size, note text size and position (design) |
| `relay/zap_relay/app.py` | The world session and its socket (world), speech and note routes (core) |

## Step 0: pull those files apart before anyone branches

**Status, 2026-11-14:** built and checked on the branch `core/step0`. It is not merged into `main` yet. Do not branch until it is.

The core developer does this first and tells the others when it is merged. Details are in [core.md](core.md).

1. **Look values move into one theme.** Every size, spacing, colour and text size now written in `TopicBall.cs`, `ConstellationController.cs` and `ZapColors.cs` moves into a single theme asset in `Assets/_Project/Art/Theme/`. The designer then changes the look by editing that asset, never the code.
2. **In-world controls move to the world lane.** Stick and trigger steering moves out of `ConstellationController.cs` into `Assets/_Project/Scripts/World/WorldControls.cs`. `ControllerActions.cs` moves to `Scripts/Core/` so both lanes can read the controller.
3. **Relay world code gets its own file.** The world session and socket code moves out of `relay/zap_relay/app.py` into `relay/zap_relay/world.py`, owned by the world lane.
4. **Shared folders are created once.** `Assets/_Project/Art/UI/`, `Art/FX/`, `Art/Fonts/`, `Art/Models/` and `Art/Theme/` are created and committed, so nobody creates the same folder twice.

**Until Step 0 is on `main`:** only the core developer edits the three mixed files. The designer and world developer can start straight away on files that are already theirs alone (image exports for the designer; `Scripts/World/` and `sources.py` for the world developer).

## Agreements between lanes

These are the points where one lane's work touches another's. Keep them stable and say so before changing them.

**Look (design and core).** The theme asset is the meeting point. The designer sets values and supplies images; the core developer makes the code read them. If the designer needs something the theme does not have, such as a new state for a node, they ask the core developer to add it.

**World (core and world).** The constellation opens and closes worlds through these members of `WorldSession`, and nothing else:

- `Enter(nodeId, topic, prompt, imageBase64)`, `Exit()`, `Steer(kind, text)`
- `IsEntered`, `State`
- events `OnStatus`, `OnPrompt`, `OnError`
- `WorldStream.Texture` and `WorldStream.OnFrameDecoded`
- `WorldAudio.SetMuted(bool)`
- `WorldControls.Send(kind, text)` and the event `WorldControls.StatusChanged`

The world developer may add to this list freely, but does not rename or remove any of it without telling the core developer first. The core developer does not reach into any other part of `Scripts/World/`.

**Relay (core and world).** `docs/protocol.md` is the contract between the headset and the laptop. The world developer decides the `/ws/world`, `/stream.mjpg` and `/world/kill` parts; the core developer decides `/speech`, `/nodes` and `/capabilities`. Only the core developer edits the file, so the world developer sends the wording to add.

**A picture inside a node (all three).** If a node should show its world, the world lane supplies the image, the design lane decides where and how it appears on the node, and the core lane connects the two. Agree it in chat before anyone builds it.

## Unity habits that prevent most conflicts

- Use Unity **6000.6.9f1** exactly. A different version rewrites project files.
- Run `git lfs install` once after cloning. Images, fonts, models and sound are stored with Git LFS.
- Commit every new file together with its `.meta` file. Never commit a `.meta` on its own, and never delete one.
- Do not create a folder that someone else might also create. Ask the core developer to add it.
- Do not open and save scenes. Everything installs itself from code, so scenes rarely need to change. Only the core developer saves `Void.unity`.
- Unity changes some files by itself. Do not commit them. Before each commit, undo them:

```sh
git checkout -- "Assets/Settings/Project Configuration/UniversalRenderPipelineGlobalSettings.asset" ProjectSettings Packages
```

- Look at `git status` before every commit and add files by path. Do not use `git add -A` or `git add .`.
- Turn off any editor feature that commits and pushes on its own.
- One Unity Editor open per copy of the project.

## Testing in the Unity Editor on your own laptop

Two things in the project are set for the demo laptop and stop the Editor reaching a relay on your machine. Both fixes are local; do not commit either.

- **Relay address.** The project carries the demo laptop's network address. To use a relay on your own machine, create a text file named `zap-relay-url.txt` containing `http://127.0.0.1:8787` in the app's data folder. On a Mac that is `~/Library/Application Support/Immerse the Bay/Immerse the Bay/`.
- **Plain HTTP.** The Editor refuses `http://` addresses by default. In Unity, open Edit > Project Settings > Player > Other Settings and set "Allow downloads over HTTP" to "Always allowed". This changes `ProjectSettings/ProjectSettings.asset`, so undo it before you commit, with the command above.

Start the relay without keys for testing, so it costs nothing:

```sh
cd relay && env ORBIS_MODE=standin REACTOR_API_KEY= OPENAI_API_KEY= uv run python -m zap_relay
```

## Branches and merging

1. Wait for Step 0, then branch from `main` using your branch name from the table at the top.
2. Commit small and push your branch often.
3. Bring `main` into your branch at least every two hours:

```sh
git fetch origin && git merge origin/main
```

4. Merge into `main` only through a pull request. The core developer merges it.
5. Before opening a pull request:
   - Unity opens the project with no red errors in the Console.
   - If you touched `relay/`: `cd relay && uv run pytest` passes.
   - `git diff --stat origin/main` lists only files your lane owns.
6. If a conflict shows up in a file you do not own, stop and ask its owner. Do not resolve it yourself.
7. Nobody pushes straight to `main`.

## Keys

Keys live in `.env` at the repo root or in `relay/.env`. Git ignores both. Never paste a key into code, a document, a screenshot or a commit.

## AI tools

Every AI coding tool reads `AGENTS.md` at the repo root, which points here. Tell your tool which lane you are.

What has worked when running tools unattended on this project:

- **Codex** handles large, multi-file jobs.
- **Gemini** (Antigravity) works when told to use file tools only; it cannot run shell commands unattended.
- **Grok** (through the Cursor agent) works on small, single-file jobs with the model `grok-4.7-medium-fast`. On the `high` setting it stalled on a large job. Give it one file and a precise description. If you run low on usage, have it write a handoff in `docs/handoffs/` from `docs/handoffs/TEMPLATE.md`, so the next person or tool can continue.
