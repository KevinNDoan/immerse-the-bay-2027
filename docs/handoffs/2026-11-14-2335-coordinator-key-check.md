# Handoff: coordinator, replacement credential check

Written by: Codex
Reason: blocked on valid World Labs API credential

## Where things stand

- Existing workspace changes preserved; no commits, builds, or installations this turn.
- Brief: user supplied a replacement World Labs key after enabling paid generation.

## Finished and checked

- Stored the supplied replacement as WORLD_LABS_API_KEY in the ignored root .env without printing the credential.
- Read-only GET https://api.worldlabs.ai/marble/v1/credits with WLT-Api-Key returned HTTP 401 Unauthorized. Verified via adapter and direct status-only HTTP request.
- Did not restart the running paid-enabled collection relay PID64376/port8794 with a rejected key. That process still has the previous valid but underfunded credential in memory.
- No paid generation/export or test world requested.

## Written but not checked

- Replacement credential cannot be activated or balance checked because authentication failed.

## Half-done

- Root .env currently contains the rejected replacement. A valid API Platform credential is needed before restarting the relay.

## Next steps, in order

1. Request a valid World Labs API Platform key, save it locally, and verify GET /credits.
2. Reload the relay only after authentication succeeds; preserve prepared-world catalog and saves.

## How to run and check

- Use relay/zap_relay/worldlabs.py WorldLabs.request('GET', '/credits'); never print credentials or response bodies containing account data.

## Known problems

- New credential returns 401. Previous accounts had 1,920 credits each, below the 5,080 complete-world threshold.

## Requests for the coordinator

- Valid replacement key from the user. No extra spend authorization needed for user-initiated new worlds.
