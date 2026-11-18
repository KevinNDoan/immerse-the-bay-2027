# Handoff: coordinator, 2026-11-15 09:19

Written by: Codex
Reason: brief finished

## Where things stand

- main at 28616ec, uncommitted integration work preserved. Brief: `docs/tasks/m2-living-worlds.md`.
- Owner reported the same starting-floor error for a newly generated **Exploring a Plant Cell** world after the earlier small-cell fix. This world is a botanical teaching lab; preserve its existing generated assets and user node.
- Updated APK installed on Quest 3S `340YC10G840Q5N`. Local and installed SHA256 both `1263ce8e5414e4ee617b5bcb1de9c49386bd856f9e70338099454c66c94c0a3d`.

## Finished and checked

- Verified prior APK `1c99b826...` was actually installed, so this was a new failing placement case, not a stale install.
- New world cache `7d1149e53d477b49aa1029595d28995115ce857ab9f0ada623ac81c8c54ef87e`, provider world `02feb35b-d8f3-4763-a47e-51a6008b2694`, user node `a9fef7e5f13e4bb9999c055b31c1261e`, parent `demo-biology-lab`. Metadata scale 1.8632039, ground offset 1.5620307. Actual floor near the viewpoint is +0.557m after metadata alignment; the old probe only covered -0.35..+0.4m, so it saw zero floor candidates. Increasing scale made the error worse.
- Reproduced the exact failure with real assets BEFORE editing: `/tmp/zap-plant-before-tests.xml` contains the expected starting-floor failure.
- Changed `GeneratedEnvironment.AlignStartingFloor`/`FindStandingPoint`: try estimated floor as before, then probe actual collision geometry downward from the generated camera origin, using collision bounds for the vertical range. Do this before enlarging. This respects floors above/below the estimate, preserves body/support checks, and avoids treating the outside of a low ceiling as a standing surface. Viewpoint remains tracked; visuals/collision shift together.
- Actual plant-world test now passes without enlargement and logs a -0.56m floor correction. Rendered eight views, checked floor alignment, local walking, snap turn, and floating learning-object behavior. Viewed `Builds/Verification/plant-entry/spatial-6.png`.
- PlayMode: 36 passed, 0 failed, 1 optional preview ignored in `/tmp/zap-plant-after-tests.xml`. Added regression cases for +0.56/-0.8m metadata errors and taller viewer, retaining the existing low-room enlargement/normal-room tests.
- Additional optional collision regression ran all SIX cached worlds at 1.2m and 1.9m viewer heights, with translated/rotated starting poses. Both tests passed, covering 12 placements with floor and capsule clearance assertions (`/tmp/zap-floor-matrix-tests.xml`). Uses `ZAP_FLOOR_CACHE_ROOT`; no provider calls.
- Android build succeeded (`/tmp/zap-plant-entry-build.log`), `adb install -r` succeeded, installed APK hash matched, USB reverse 8794 restored. Unity-only font/package/settings churn restored to clean pre-turn values.
- Current four-node constellation was backed up and byte-verified unchanged after install. It contains the three demo nodes plus Exploring a Plant Cell. Owner had removed the prior Inside a Cell node; do not restore older backups over this save.
- No relay changes/restart this turn, no paid generation/export. Production relay remains PID 91343, port 8794, log `/tmp/zap-cell-speed-relay.log`, 500k splats + paid user generation enabled.

## Written but not checked

- At first post-install check the Quest was at `SensorLockActivity`. One-shot `zap-enter-node.txt` queues the existing plant-cell node for startup. Asked owner to wear/wake it for a device verification; check follow-up evidence/messages before claiming a worn-headset pass.
- A subsequent 45-second check did not observe floor-alignment logs; the one-shot file still exists. Screen capture returned empty bytes, so there is no usable post-install headset image. Do not mistake `quest-after.png` or the empty `device-after.log` for device verification.
- No new full relay suite needed for this C#-only change; prior 82-test pass still applies to unchanged relay code.

## Half-done

- Implementation and installation complete. Physical device view may still await the owner wearing the headset.

## Next steps, in order

1. Once awake, confirm the app opens the saved plant-cell lab. Expect `[Environment] Aligned to reconstructed floor; corrected estimated height by -0.56m.` and 500000 Gaussians. Logs/screenshots in `Builds/Verification/plant-entry/`.
2. Have the owner retry the same orb and assess movement. Reuse saved worlds; do not spend credits on test generation.

## How to run and check

```sh
cd /Users/kevindoan/Code/immerse-the-bay
# One Unity process only. Main actual-world regression:
ZAP_ENV_PREVIEW_DIR="$PWD/Builds/Verification/plant-entry" ZAP_ENV_PREVIEW_URL=http://127.0.0.1:8794 /Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter 'GeneratedEnvironmentTests|WorldTravelTests|CrystalJourneyTests|ConstellationTests' -testResults /tmp/zap-plant-after-tests.xml -logFile /tmp/zap-plant-after-tests.log
# Saved collider matrix, no GPU/provider needed:
ZAP_FLOOR_CACHE_ROOT="$PWD/relay/.cache/geometry" /Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter GeneratedEnvironmentTests.SavedWorldCollidersProvideClearEntryAtDifferentViewerHeights -testResults /tmp/zap-floor-matrix-tests.xml -logFile /tmp/zap-floor-matrix-tests.log
```

- APK: `Builds/Quest/ImmerseTheBay.apk`; device package `com.immersethebay.vr`.
- Device evidence: `Builds/Verification/plant-entry/install.json`, before/after tests XML, floor-matrix XML, saved-world preview PNGs, before/after device logs, `quest-after.png`, `device-backup/constellations`.

## Known problems

- Worlds still need a supported clear standing position within the bounded horizontal search; this update does not bypass collision or synthesize a platform. All six current cached environments pass.
- Splat renderer/content limitations from earlier handoffs remain. No quality reduction or changes to generation prompts in this update.

## Requests for the coordinator

- None; no packages, protocol or project-settings changes.
