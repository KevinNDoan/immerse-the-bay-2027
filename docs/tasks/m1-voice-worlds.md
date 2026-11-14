# Brief: voice, throwable topic balls, and live worlds

Authorized by the owner on 2026-11-14 after the Stage 2 review. This supersedes the M0 stop gate.

- Implement both hold-to-talk actions: create/write a new topic ball and edit the selected/current node.
- Preserve controller grabbing and throwing; thrown balls decelerate and float. Save node content and placement.
- Connect nodes to Orbis entry, steering and exit. Repair and test the relay before claiming live readiness.
- Keep the void inexpensive and temporary; the designer is supplying a skybox. Defer Satie and ornamental work.
- Codex owns integration across core, constellation, world, network, relay, tests, editor build tooling and documentation for this brief.
- Run relay tests, Unity compilation, PlayMode tests and the Quest build. Close the interactive Editor gracefully first; never open the project in two Unity processes.
- Keep credentials in the owner-selected ignored root .env or fallback relay/.env only. No credentials or cloud sessions may be fabricated. Report unavailable keys and on-headset checks explicitly.
- Do not commit or push unless requested. Write a handoff with results and remaining limitations.
