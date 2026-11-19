# Handoff: coordinator, 2026-11-15 09:47

Written by: Codex
Reason: brief finished

## Where things stand

- main at 28616ec with prior uncommitted work. Brief: `docs/tasks/m2-living-worlds.md`; direct owner request to use another saved API key after exhausting the current one.
- No application code or APK changes. Existing floor-fixed build remains installed.
- Production relay PID **94892**, exec session **3660**, port 8794, log `/tmp/zap-funded-key-relay.log`. Same 500k splat mode with OpenAI and authorized user generation enabled.

## Finished and checked

- Read-only World Labs balance calls found original active key at 340 credits; saved slots 2 and 3 each at 1,920.
- Swapped root `.env` active key with slot 2, preserving both credentials. `.env` is gitignored. Restarted only after confirming no incomplete jobs.
- Resumed the owner's failed **Forest Exploration** request, which had been rejected before any paid submission for insufficient balance. Same node `789268ddf3c147a7916de20d062bd0db`, cache `d1c6727bbaeee633dc02011dafa409a90169d6141a7495c4cf3a0494afb4caf2`, exact saved context. One generation operation accepted/journalled: `72ca4247-2c2f-4fd1-8a01-95bc8965b5d7`.
- Relay subsequently reported a generic preparation error while waiting; a read-only provider check showed that same operation complete. Resumed the journalled job through `/world/prepare`; no second generation or export was submitted. Assets downloaded/imported successfully, status **ready**, 500000 splats, collision, panorama and fallback pane present.
- The first backup then had 340 credits. Confirmed the other backup still had 1,920, swapped active key with slot 3, and restarted again after all jobs completed so the next user request has a funded account.
- Final root `.env` balance mapping: **WORLD_LABS_API_KEY = 1,920 credits**, **WORLD_LABS_API_KEY_2 = 340**, **WORLD_LABS_API_KEY_3 = 340**. These are remaining balances at verification, not key identities. All values retained locally; no keys in tracked files or logs.
- Final `/capabilities` reports World Labs, speech installed, OpenAI configured. Forest `/world/jobs/...` reports ready with 500k splats and pane. No Quest reinstall needed for server credential changes.

## Written but not checked

- Forest entry/rendering has not been tested in the headset or Unity. It is prepared and cached for the owner to enter.
- Its initial AI learning plan fell back to null with an explanatory pane; no artifact is required for this world. Existing AI configuration was not changed.

## Half-done

- None for key switching or preparing the previously blocked request.

## Next steps, in order

1. Owner re-enters the saved Forest Exploration orb. This should use the cached world without spending again.
2. Active account has enough for one further normal generation at the current 1,580-credit check. Both other saved accounts are below that threshold; do not promise unlimited access or silently regenerate completed worlds.

## How to run and check

```sh
cd /Users/kevindoan/Code/immerse-the-bay/relay
# Already running; do not start a duplicate listener:
.venv/bin/python -u scripts/run_world_collection.py --catalog ../Builds/Verification/demo-worlds/great-wall-biology-v1/catalog.json --port 8794 --enable-openai --enable-generation --render-mode splats --splat-quality 500k
```

- Final relay log `/tmp/zap-funded-key-relay.log`; intermediate resumed-job log `/tmp/zap-backup-key-relay.log`.
- Read root ignored `.env` through dotenv; never print credentials. Account switching must wait for pending operations associated with the old account to complete.

## Known problems

- Automatic key rotation is not implemented; this was an authorized manual switch preserving all stored keys.
- The generic transient preparation error's exact exception was not captured. Saved-operation resume completed successfully; no code change warranted in this credential task.

## Requests for the coordinator

- None.
