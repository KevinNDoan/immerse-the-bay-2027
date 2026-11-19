# Handoff: coordinator, 2026-11-15 09:00

Written by: Codex
Reason: brief finished

## Where things stand

- main at 28616ec; prior native-splat work and this update remain uncommitted. No git push requested in this follow-up.
- Brief: `docs/tasks/m2-living-worlds.md`. Owner selected faster preparation without lowering quality, constant hyperspace speed, and installation after a saved Inside a Cell entry failure.
- Latest APK installed on authorized Quest 3S `340YC10G840Q5N`. SHA256 of local APK and installed base.apk both `1c99b8263d5ceb5c5fa2c935a2bfefd36ff0b51717ed81385403678c65c9e1a7`.

## Finished and checked

- Learning planning starts after accepted/journalled generation and overlaps provider work. Independent collider/panorama/SPZ downloads run concurrently. Async siblings and planner are cancelled/drained on failure/shutdown. Existing operation polling is two seconds instead of ten. Ready manifest is published only after plan/fallback pane exists. Model and 500k quality unchanged; no HQ export in splat mode.
- Hyperspace `_Travel` advances linearly throughout the wait; existing fade/readiness behavior retained. Regression verifies equal travel increments at 2, 4.5, 5, 6, 30 and 300 seconds.
- Inside a Cell cache `6a83b64ba1988d6c71c038a37890b136ed67e1ffe30ce26f05244ff0b1640f21`, provider world `1e4b99d8-4b1c-4a33-87e9-07fcd6397c97`, node `9fa9645c4b3e4c71bb4520d6fa3fecec`. Generation was complete. Metric scale 0.3479384 yielded collider Y bounds -0.030..1.041 metres, so a standing person's capsule did not fit. The app rejected entry, producing the user's screenshot.
- Entry first tries original scale, then uniform enlargement 1.5/2/3/4× only when no clear standing point fits. Ground offset scales with it; visuals/collision share the transform, panorama retains its radius, headset remains stationary. Floor support/body clearance still required. Existing cell passes at 2×. Normal-sized synthetic room remains unchanged.
- 82 relay tests passed (`/tmp/zap-speed-relay-tests.log`); PlayMode 35 passed, 0 failed, 1 optional crystal preview ignored (`/tmp/zap-cell-speed-tests.xml`). Includes real 500k cell render, floor placement, local walking/snap turn, floating study object, travel failure recovery, speed test and scale regressions. Viewed `Builds/Verification/cell-entry/spatial-6.png`.
- Android development build succeeded (`/tmp/zap-cell-speed-build.log`); install -r succeeded and hashes verified. Unity's generated font/package/settings churn restored to pre-turn HEAD content. No new World Labs generation/export was submitted by this work.
- Preserved current four-node save: three demo worlds plus owner-created Inside a Cell child of Biology Lab. Backup under `Builds/Verification/cell-entry/device-backup`; post-install save bytes match. Do not restore old three-node-only saves.
- Relay restarted only after all cached requests had ready splat manifests. New PID 91343, exec session 6591, log `/tmp/zap-cell-speed-relay.log`, port 8794. Capabilities and existing cell status returned successfully. USB reverse restored.

## Written but not checked

- New APK's worn-headset rendering/movement is not verified yet. Device is at `com.oculus.os.vrlockscreen.SensorLockActivity`; the app process starts paused. One-shot `zap-enter-node.txt` is queued for the saved cell and has not yet been consumed.
- Fresh paid-generation latency improvement has not been benchmarked; concurrency is tested with deterministic event barriers. Provider inference remains the dominant minutes-long stage.

## Half-done

- None in implementation or installation. Physical headset evaluation awaits the owner wearing it.

## Next steps, in order

1. Owner puts on headset; app should consume the one-shot entry for Inside a Cell. Inspect fresh app logs for 500000 splats and `[Environment] Enlarged saved surroundings 2.0x` (factor can vary with current eye height).
2. Confirm the same saved cell opens, local movement feels acceptable, and long hyperspace motion stays steady. Reuse saved assets; do not generate paid test worlds.
3. If continuing, retain the new node and its parent link/learning objects. Do not revert accepted 500k quality to 100k.

## How to run and check

```sh
cd /Users/kevindoan/Code/immerse-the-bay/relay
.venv/bin/python -m pytest -q
# Already running; do not launch another listener on 8794:
.venv/bin/python -u scripts/run_world_collection.py --catalog ../Builds/Verification/demo-worlds/great-wall-biology-v1/catalog.json --port 8794 --enable-openai --enable-generation --render-mode splats --splat-quality 500k

# APK: /Users/kevindoan/Code/immerse-the-bay/Builds/Quest/ImmerseTheBay.apk
# ADB: /Applications/Unity/Hub/Editor/6000.6.9f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
# Keep reverse tcp:8794 tcp:8794; package com.immersethebay.vr
```

- Real-cell test fixture: `Builds/Verification/cell-entry/manifest.json` + `plan.json`; `ZAP_ENV_PREVIEW_DIR` points there, `ZAP_ENV_PREVIEW_URL=http://127.0.0.1:8794`.
- Unity 6000.6.9f1 PlayMode filter: `GaussianEnvironmentTests|GeneratedEnvironmentTests|WorldTravelTests|CrystalJourneyTests|ConstellationTests`. Run only one Unity process; see prior native-splat handoff for build command.

## Known problems

- Scenes with no supported, clear standing area even at 4× still reject entry. This does not invent a floor or bypass collision.
- Native Gaussian renderer limitations from prior handoff remain. Generation content is static; local objects animate. No arbitrary dynamic inhabitants.
- Paid generation remains enabled for user-created ideas per authorization; no credit check performed in this update. Secrets remain in ignored env files.

## Requests for the coordinator

- None. No package, protocol, or project-settings change requested.
