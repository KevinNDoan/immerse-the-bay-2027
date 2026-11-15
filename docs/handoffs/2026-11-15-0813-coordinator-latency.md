# Handoff: coordinator, generation latency investigation

Written by: Codex
Reason: brief finished

## Where things stand

- main at 28616ec. Read-only investigation; no runtime changes or new paid submissions.
- Brief: docs/tasks/m2-living-worlds.md; owner asked why new worlds take so long.

## Finished and checked

- Follow-up question: can the app work without the HQ export? Inspected GeneratedEnvironment.cs: current loader requires visual mesh and collision arrays; no native splat renderer exists. Navigation uses the separate collision geometry, while study objects remain separate Unity objects. A splat renderer plus retained colliders can replace the visual mesh in principle, but simply disabling export breaks the current loader. No implementation change authorized or performed for this feasibility question.
- Reviewed spatial.py and worldlabs.py. Fresh world creation waits sequentially for generation, downloads, study planning, a separate textured mesh export, and local conversion. One job runs at a time. Ready worlds reuse cache.
- Current owner-requested Inside a Plant Cell job is f8b0125a9e7dab9dfcf6fcbbc35fbd3c89376a6f01c627e1fb7fbeb60bdc4e9e, operation b361ea5b-cddb-4a2c-ae08-d1613a6886bc. Read-only provider status returned IN_PROGRESS, no error, at initial generation stage. Submitted around 08:10 PST.
- Existing demo records show exterior export took 34 minutes and tower/lab exports about 40 minutes. Lab generation took about five minutes; GLB download/conversion much shorter than export.
- Official https://docs.worldlabs.ai/marble/export/mesh documents up to one hour for high-quality mesh generation and recommends splats for visual fidelity. Current app waits for this mesh rather than rendering native splats.

## Written but not checked

- Native splat rendering is a possible faster entry path, not implemented or profiled on Quest. Could evaluate using existing saved worlds without new generation charges.

## Half-done

- None for the explanation. Owner-requested paid world continues in the existing relay process.

## Next steps, in order

1. Explain generation/export latency clearly; do not promise instant generation.
2. If requested, evaluate native splat rendering or earlier preview with existing assets and validate Quest performance.

## How to run and check

- Read relay/.cache/geometry/f8b0125a9e7dab9dfcf6fcbbc35fbd3c89376a6f01c627e1fb7fbeb60bdc4e9e/status.json.
- Relay log: /tmp/zap-funded-world-relay.log. Do not restart or resubmit the running paid job for this investigation.

## Known problems

- Current full-mesh-first architecture adds the long provider export wait before entry. Native splat Quest performance remains unverified.

## Requests for the coordinator

- None.
