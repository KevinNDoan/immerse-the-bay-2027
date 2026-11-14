# ZAP! planning doc

A VR study tool for Quest 3: speak a topic, throw it into a midnight void as a star, connect stars into a constellation, and step inside any star to explore a generated world of that topic.

Vendor facts were checked against the vendors' own docs on 2026-11-14. Items marked **(untested)** still need a quick trial.

## Current stage (approved 2026-11-14)

Two stages are approved. Work stops after Stage 2 for a check. In this doc, "I" is Claude, the AI assistant coordinating the build.

**Stage 1: documents, written by me**
1. `docs/planning.md`: a copy of this doc, so the team can read it.
2. `docs/design-system.md`: the brief for the designer (outline below).
3. `AGENTS.md` in the repo: the build rules every AI tool reads (who owns which folders, how to verify, when and how to write a handoff).
4. `docs/handoffs/TEMPLATE.md` and one task brief per delegated job in `docs/tasks/`.
5. Your global rules (`~/.claude/CLAUDE.md` and `~/AGENTS.md`): the lines that keep Gemini and Codex read-only are changed so both may write code, for all projects, as you chose. "Never push, merge, deploy, or delete branches unless I ask" stays.

**Stage 2: foundation and sponsor connections, built by Grok, Gemini and ChatGPT**
- The void scene with controllers, building to a headset.
- The Orbis connection end to end, working against a recorded stand-in until the key is added.
- The Satie connection: its MCP server registered for this project, ready for sign-in later.
- No accounts are created and no keys are entered. Each key has a named slot in `relay/.env.example`.

All work happens on a new branch, `zap/build`. I commit there and push that branch to GitHub (your standing go-ahead for this project); nothing is merged to `main`.

I stop after Stage 2. The constellation work (M1 onward) starts when you say so.

## Context

- **Why:** hackathon build for the Visko "Living Worlds" track, which requires an XR experience centred on Orbis with a live-streamed, user-steered world demo by Sunday. Creative Zenith (artistry) and Virtuous Reality (social good) are secondary.
- **Pitch:** Notion or Obsidian for VR, built around what only a headset can do: placing ideas in space with your hands and stepping inside a topic.
- **Starting point:** the repo is Unity's VR template with one throwable cube in `Assets/_Project/Scenes/Main.unity`. There is no code of our own yet, and nothing has run on a headset.
- **Hardware:** two Quest 3 headsets (one is enough for this scope). Development is on a Mac, so headset testing means building an APK.

## Agreed scope

**The user:** one person studying a subject on their own.

**The void**
- A fully virtual midnight purple-black space. The real room is not visible.
- Everything in it is white on black. Colour comes later.
- Tone is playful and electric, to match the name.

**Nodes**
- The star is the node. You grab the star; lines run star to star; a sticky note with the topic hangs from it.
- Hold a button and speak a topic. The new node appears in your hand, ready to throw.
- A faint line reaches from the new node toward the existing node it relates to most. Throw it near that node and the line becomes real; throw it elsewhere and it starts unconnected.
- Each node holds its topic, a short generated summary, and three suggested related topics shown as faint stars. Grab a suggested star to keep it, which also draws its line; the others fade.
- The note shows only the topic, and expands with the summary when held close.

**Controls (controllers only for now)**
- Grip grabs and throws.
- Hold trigger on a star and drag to another to draw a line. Drag across a line to cut it. A new line snaps in like a small lightning bolt, then settles to a thin glow.
- Pull the whole constellation with two hands, or select a star to bring it and its neighbours to you.
- Stretch with two hands to scale the constellation from desk size to room size.
- Positions are free. There is no numbered order.
- The constellation saves on the headset automatically.

**Worlds**
- Every node can be entered. Finding what Orbis can and cannot do is a goal of the prototype.
- Pull a node to your face to enter. One button leaves.
- A full dome replaces the void. Orbis is prompted for panoramic footage, which is wrapped around you. If that looks warped or shows a seam, the picture stays undistorted in front and the rest of the dome is a blurred extension of it.
- Steering works like game controls: left stick moves, right stick looks, one button triggers a suggested event, hold-to-talk takes any spoken request. Each becomes a text instruction to Orbis and lands about 2 seconds later, so a marker shows the pending instruction until it does.
- A node keeps the last frame of its world as its picture, shown on the expanded note. The next visit starts from that frame.
- To visit another world, leave to the constellation and enter the next node.

**My choices where you left it to me (overrule any)**
- Inside a world you see the topic title for a few seconds on entry, the pending-instruction marker, and a small controls card on the left controller. Nothing is fixed to your view.
- A thrown node slows down and hangs where it stops. There is no gravity.
- Thumbstick walking and teleport are off in the void. You move by walking and by pulling the constellation.

**Deferred**
- Seeing the real room (passthrough).
- Hand tracking.
- Two-person use and tossing nodes between people.
- Importing existing notes (Obsidian folders).
- Typing on a keyboard and other input options.
- Colour.
- Sound content. The Satie connection is set up now; sounds are authored once access arrives.
- Accounts and keys for Orbis, Satie, Claude and speech-to-text. The connections are built first and keys are slotted in later.
- Doorways from one world straight into a connected one.
- Mini-games and teacher-made course maps.

## Sponsor tech: what it actually is

**Orbis (Visko), accessed through Reactor**
- A real-time generative video model. It streams flat, mono 16:9 video (generated at 832×480, 18 fps, upscalable to 4K) and a mono audio track.
- It accepts text prompts only. A new prompt takes effect about 1.8 seconds later. There is no camera, pose or controller input, and no dome or 3D output.
- It can start from a 16:9 image. A run lasts up to about 61 minutes. An account can hold 5 sessions at once.
- Cost is about $0.58 per minute (Stable) or $1.25 per minute (Dynamic), billed while a session is open. You have plenty of credits and the sponsor encourages heavy use, so sessions are not rationed.
- SDKs: JavaScript, Python and native Android. There is no Unity SDK.

**Satie (Attractor Labs)**: connection now, sound later
- A hosted, web-first service for authoring spatial sound scenes, in private beta. A coding agent drives it through its MCP server at `https://www.satie.world/mcp`; sign-in is with GitHub or Google.
- There is no Unity package, and its Unity export is untested by its own account.
- Now: the MCP server is registered in the project's `.mcp.json`, so signing in is the only step left. Satie's guide also tells agents to add hooks to Claude settings; that is not done.
- Later: Satie makes the sounds ahead of time and Unity plays them.

## Tech stack

| Layer | Choice | Status |
|---|---|---|
| Engine | Unity 6000.6.9f1, URP 17.6 | In repo |
| XR runtime | OpenXR 1.18 with Unity OpenXR: Meta 2.6.1 | In repo |
| Interaction | XR Interaction Toolkit 3.6.1, controllers | In repo |
| Text | TextMeshPro with Inter fonts | In repo |
| Relay | One small Python service on a laptop. It holds the keys and does four jobs: Orbis, speech-to-text, the LLM calls, and a web page showing the live stream for judges | To build |
| Orbis into the headset | The relay receives frames and audio through Reactor's `reactor-sdk` and forwards them to the headset over Wi-Fi; steering text goes back the same way | To build. Frame delivery confirmed in Reactor's docs; decode speed on Quest **(untested)** |
| Orbis, plan B | Reactor's native Android SDK inside the Unity app | **(untested)** |
| Dome | An inside-out sphere around the user showing the stream | To build. Whether Orbis panoramas wrap cleanly is **(untested)** |
| Voice | Hold-to-talk on the controller; audio goes to the relay, which transcribes it with a local speech model | To build. Local speed **(untested)**; a cloud transcription service is the fallback |
| LLM | Claude API on the relay. `claude-haiku-5-5` writes the summary, the three suggestions and the suggested connection when a node is created, and turns steering into Orbis instructions. `claude-opus-5-5` writes each node's world prompt in the background | To build |
| Saving | A small JSON file on the headset | To build |
| Build | Existing menu item "Immerse the Bay → Build Quest Development APK" (`Assets/_Project/Editor/QuestBuild.cs`) | In repo |

Ruled out: WebRTC inside Unity (the Unity package does not list this Unity version or Quest, and Reactor's connection protocol is undocumented). Rebuilding as a web app was considered and declined.

## Resources

**Hardware**
- One Quest 3 in developer mode with a USB-C cable (a second is spare).
- A Mac for Unity and the relay.
- A travel router or phone hotspot, because venue Wi-Fi often blocks device-to-device traffic.

**Accounts and keys**
- Visko team access key from the Visko desk, and the Orbis credit pool.
- Claude API key.
- Meta developer account for developer mode.

**Reference docs**
- Orbis: https://docs.reactor.inc/model-api-reference/visko-orbis-stable/overview.md (schema, tutorial and prompt guide are alongside it)
- Reactor Python SDK: https://docs.reactor.inc/sdk-reference/python/track.md
- Reactor pricing: https://api.reactor.inc/pricing
- Satie: https://www.satie.world/

**AI tools on this Mac (checked 2026-11-14)**
- ChatGPT: Codex command-line tool, signed in.
- Gemini: Antigravity command-line tool. Only Gemini 3.8 Flash is on offer right now; the stronger Gemini 4 model is not in its list.
- Grok: through the Cursor command-line agent (Grok 4.7), on your Cursor plan. Your Grok app can't be driven from here, but every task brief is a self-contained file you can paste into it.

## How the build is run

**Who does what.** Each tool owns its own folders, and no two ever edit the same file.

| Who | Job | Owns |
|---|---|---|
| Claude (me) | Planning docs, task briefs, reviewing everything the others build, integration, monitoring, handoffs. I build a lane myself only when its tool has run out of usage | `docs/`, `AGENTS.md`, `.mcp.json`, plus any lane I take over |
| ChatGPT (Codex) | The void, controllers, then the constellation | `Assets/_Project/Scripts/Core/`, `.../Constellation/`, `Assets/_Project/Editor/`, `Assets/_Project/Scenes/` |
| Gemini (Antigravity) | The Unity side of worlds: relay client, stream playback, dome, steering | `Assets/_Project/Scripts/World/`, `.../Net/` |
| Grok (Cursor agent) | The relay: Orbis connection, stand-in mode, speech and LLM endpoints, judges' page | `relay/` |

**How a job runs**
1. I write a brief in `docs/tasks/`: goal, folders it may touch, what done looks like, how to check it.
2. The tool runs that one brief in the background, with edits allowed only inside the project. Permission checks are never switched off.
3. I review the result myself: I read the change against the brief, confirm the code compiles (Unity run from the command line, one run at a time) and that the relay answers. Problems go back to the tool that wrote it, for at most two rounds.
4. I commit to `zap/build` and push.

**Scenes.** No tool hand-edits Unity scene or prefab files. Scenes are assembled by an editor script or at startup, so changes stay in code that can be reviewed.

**Watching usage**
- After every job I read the tool's output for limit or quota warnings, and before each new job I check its status where the tool offers one. Whether each tool reports remaining usage as a number is **(untested)**; the fallback is its own warning.
- When a tool is close to its limit or hits it, I stop giving it work, write a handoff recording where its lane stands, push, tell you, and then build the rest of that lane myself.
- If my own usage runs low, I write a handoff for a teammate and stop.

**Handoffs**
- One markdown file per handoff in `docs/handoffs/`, named by date and lane, committed and pushed on `zap/build`.
- It states: the branch and commit, what is finished and checked, what is half-done and in which files, the next steps in order, how to run and check the work, known problems, and the brief it belongs to.
- `AGENTS.md` tells every AI tool to write one before it stops or when its user says usage is low. Teammates have one plan each, so the same rule covers them: their tool writes the handoff, and the next person's tool starts by reading the newest one.

## Building blocks already in the repo

| Need | Reuse | Path |
|---|---|---|
| Controller rig | XR Origin (XR Rig), already in the main scene | `Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/` |
| Grab and throw | `XRGrabInteractable` with throw-on-detach, already tuned on the Grab Cube | `Assets/_Project/Scenes/Main.unity` |
| Star body | Sphere Interactable | `Assets/VRTemplateAssets/Prefabs/Interactables/` |
| Hover and grab feedback | `HighlightInteractionAffordance`, affordance themes | Starter Assets, `Assets/VRTemplateAssets/Themes/` |
| Notes that face you | `Billboard` | `Assets/Samples/XR Interaction Toolkit/3.5.1/Hands Interaction Demo/` |
| Controls card that follows the controller | XRI's `LazyFollow` | XR Interaction Toolkit package |
| Fonts | Inter SDF | `Assets/VRTemplateAssets/Fonts/Inter/` |
| Testing without a headset | XR Interaction Simulator | Imported |
| Lines | `BezierCurve.cs` as a reference only; it can't be driven from code, so lines need their own renderer | `Assets/VRTemplateAssets/Scripts/` |

**Must be built:** the void scene, node and line code, voice capture, the relay, the dome and stream playback, steering, saving.

## Path to a polished product: the design system

The designer works in Figma for UI; 3D stays simple (shapes made in Unity, or free asset-store models). `docs/design-system.md` is their brief. It will contain:

1. **What ZAP! is**, in one paragraph, and its two spaces (the void and a world).
2. **Fixed rules:** white on black; playful and electric; how big and how far things are in a headset; minimum text size; nothing fixed to the user's view.
3. **Starting values the designer can overrule:** the blacks and whites, type scale (Inter), spacing, corner radius, line weights.
4. **Every piece to design, with its states:**
   - Sticky note: compact, expanded with summary, expanded with picture.
   - Star: idle, pointed at, grabbed, suggested, visited.
   - Line: being drawn, settled, suggested, cut.
   - Voice indicator: listening, thinking.
   - In a world: title card, pending-instruction marker, controls card.
   - Messages: connecting, error.
5. **Motion notes** for the lightning-bolt line and the entry into a world, as direction for developers.
6. **Figma handoff rules:** frame sizes, naming, export format, and what stays live text in Unity instead of being baked into images.
7. **3D notes:** which shapes are built in Unity and what the designer supplies for them.
8. **Open questions for the designer** and the deferred list (colour, sound, hands).

In Unity, the same values live in one shared theme asset so every screen draws from one source.

## What makes it enticing

- **You make ideas with your hands.** Say a topic, throw it, zap a line between two stars.
- **It studies with you.** Each node arrives with a summary and three directions to go next.
- **Every topic is a place.** Step inside and steer a living world instead of reading about it.
- **Your constellation is yours.** It persists, and visited stars carry a picture of where you went.
- **Track fit:** Orbis is the centre of the experience and is steered live (Living Worlds); the product story is a spatial study tool; self-directed learning carries the social-good angle.

## Milestones (each ends in a build you can demo)

- **M0 Foundation and connections (Stage 2, then I stop):**
  - ChatGPT: the void scene with controllers, building to a headset.
  - Grok: the relay, with the Orbis connection, a stand-in mode that plays a recorded clip, a web page showing the stream, and `relay/.env.example` naming every key.
  - Gemini: the Unity client that connects to the relay, shows the stream on a dome, and sends steering text.
  - Me: Satie's MCP server in `.mcp.json`.
  - Once the Orbis key is in: Stable against Dynamic, and whether panoramic prompts wrap cleanly.
- **M1 Constellation:** speak a topic, node appears in hand, throw, it hangs; grab and rearrange; draw and cut lines; pull and stretch the constellation; save.
- **M2 Generative nodes:** summary on the expanded note; three suggested stars; suggested connection.
- **M3 Worlds:** pull to face, dome, game-style steering with the pending marker, leave, last frame kept; live-stream page for judges.
- **M4 Look:** the designer's Figma assets applied through the theme; the lightning-bolt line; entry and exit transitions; demo script.

## Verification

- **Stage 1:** the docs exist in the repo and `docs/design-system.md` reads as one self-contained file you can send.
- **Stage 2, without any key:** Unity compiles from the command line with no errors; the relay starts in stand-in mode and its web page shows the clip; in the Editor, the dome shows that same clip and a steering message reaches the relay's log; the build menu produces an APK.
- **Stage 2, on the headset (needs a person):** the void appears and the controllers are tracked.
- **In the Editor:** XR Interaction Simulator for creating, throwing, connecting and saving nodes, with typed text standing in for voice.
- **Relay alone:** its web page shows the Orbis stream and accepts steering text before the headset is involved.
- **On the headset:** text readable at arm's length; a spoken topic becomes a node within a few seconds; the dome plays smoothly; a stick push changes the world in about 2 seconds; the constellation is still there after restarting the app.
- **Demo rehearsal:** a full run on the network we will demo on, with a recorded backup video.

## Risks

| Risk | Mitigation |
|---|---|
| Orbis panoramas look warped or show a seam in the dome | Undistorted picture in front with a blurred extension around it |
| Low-resolution video looks soft when wrapped around the user | Request upscaled output; compare Stable and Dynamic in M0 |
| The 2-second steering lag feels broken | Pending-instruction marker; stick pushes send short camera instructions without an LLM call |
| Video frames decode too slowly on the headset | Lower resolution or frame rate; then Reactor's Android SDK |
| Venue Wi-Fi blocks headset-to-laptop traffic | Our own router or hotspot; USB as a last resort |
| Local speech-to-text is too slow | Cloud transcription service |
| Reactor outage during the demo | Recorded clip that plays in the dome |
| Nothing has run on a headset yet | M0 puts a build on the device first |
| No AI tool can see inside the headset or the Unity Editor | Compile checks from the command line; a person tests each milestone on the device |
| Three tools writing in one project collide | Fixed folder ownership; one Unity run at a time; I integrate and commit |
| A tool runs out of usage mid-job | Small jobs with a brief each; handoff file; I take over the lane |
| Gemini is limited to the Flash model right now | Give it the most self-contained lane; I review its work closely |
| The Orbis connection can't be proven without a key | Built against Reactor's documented interface with a stand-in; first live test when the key arrives |

## Open items

- Ask at the Visko desk: does "live-streamed" mean the world streams live, or that the demo is broadcast to an audience?
- Demo story and which topics to show.
- Keys to add when you have them: Visko/Reactor, Claude, and Satie sign-in.
