# Handoff: coordinator, 2026-11-15 09:54

Written by: Codex
Reason: blocked

## Where things stand

- main at 28616ec; existing uncommitted integration work preserved. Brief: `docs/tasks/m2-living-worlds.md`, plus direct requests to verify the newest Quest installation, show the forest, and use a newly supplied primary World Labs key.
- Forest entry and latest-build verification are complete. Promoting the newly supplied key is blocked by provider authentication rejection.

## Finished and checked

- Local APK and installed APK SHA256 both `1263ce8e5414e4ee617b5bcb1de9c49386bd856f9e70338099454c66c94c0a3d`; no newer runtime changes exist. No redundant rebuild/reinstall needed.
- Current Quest save contains the three demo nodes plus Forest Exploration, linked to Biology Lab. Backed up to `Builds/Verification/forest-entry/save-before.json`; no save edits/reset.
- USB transport had reconnected and lost reverse forwarding. Restored `tcp:8794 -> tcp:8794` and verified the saved forest context exactly matches its completed cache. Queued its existing node via one-shot development entry.
- Initial device was sensor-locked; owner then confirmed wearing it. First snapshot was back in the map. Reopened with owner asked to leave controllers idle; now verified the actual forest rendering in both eyes, with its pane, in `Builds/Verification/forest-entry/reopen.png`. Logs `reopen.log` show 500000 Gaussians loaded. One-shot consumed. App PID 17768 at successful verification.
- No extra generation or credits spent to show the forest. Cache/node IDs and active provider process remain as in the 09:47 handoff.
- New user-supplied candidate key tested with read-only GET `https://api.worldlabs.ai/marble/v1/credits` and `WLT-Api-Key`: returned **401 Unauthorized**. Stored candidate only in ignored root `.env` as `WORLD_LABS_API_KEY_CANDIDATE`. Did NOT replace the working `WORLD_LABS_API_KEY`, restart relay, or put credentials in tracked files.
- Working primary remains the previously selected funded account (last verified 1,920 credits); backup slots 2/3 each last verified 340. Relay PID 94892/session 3660, `/tmp/zap-funded-key-relay.log`, port 8794 unchanged.

## Written but not checked

- Newly supplied key cannot authenticate to the documented API; its service/account origin is unknown. Await a valid replacement/API Platform key.
- User's subjective forest movement/visual quality has not been assessed. Device entry and stereo rendering are verified.

## Half-done

- Primary-key promotion requires a valid credential. Existing working service remains usable.

## Next steps, in order

1. Ask owner for a valid World Labs API Platform key, preferably saved in root ignored `.env` candidate slot. Validate read-only before promotion; preserve working credentials.
2. Do not regenerate the forest; it is cached and has now been seen on the headset.

## How to run and check

- See 09:47 handoff for running relay command, cache IDs and account mapping. No relay changes in this turn.
- Quest ADB: `/Applications/Unity/Hub/Editor/6000.6.9f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb -s 340YC10G840Q5N`.
- Device evidence: `Builds/Verification/forest-entry/reopen.png`, `reopen.log`. Earlier `quest.png` / `quest-current.png` show the map, not successful forest entry.

## Known problems

- USB reconnection drops reverse forwarding. Sensor/app pause currently returns to the map via `OnApplicationPause`; no behavior change made here.
- Forest has a fallback learning pane because the initial optional learning-object planner was unavailable. User can still request objects by voice; that path was not retested in this turn.

## Requests for the coordinator

- Valid replacement credential required to fulfill the requested new primary-key activation. No package/settings/protocol changes.
