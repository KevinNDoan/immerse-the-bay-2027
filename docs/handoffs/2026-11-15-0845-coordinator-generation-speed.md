# Handoff: coordinator, generation speed options

Written by: Codex
Reason: brief finished

## Where things stand

- main at 28616ec with prior native-splat work uncommitted. No code/config or relay restart in this follow-up.
- Brief: docs/tasks/m2-living-worlds.md; owner asks whether fresh generation can be sped up further.

## Finished and checked

- Inspected live pipeline. World generation completes before collider/panorama downloads and study planning; those are currently sequential. World Labs polling sleeps ten seconds. These are possible quality-preserving overhead improvements, not yet implemented.
- Prior plant-cell request began 08:10:02, generation submitted 08:10:10, world metadata saved 08:15:13, learning plan saved 08:15:30. The roughly five-minute provider stage dominates.
- Latest local request Inside a Cell began 08:41:57 and was still preparing at inspection. Do not restart it or create another test generation.
- Official model list https://docs.worldlabs.ai/api/models includes marble-1.0-draft. Official pricing https://docs.worldlabs.ai/api/pricing distinguishes draft generation and regular generation. No authoritative draft latency was obtained; do not promise a particular generation time or quality. No paid benchmark performed.

## Written but not checked

- Parallel planning/downloads, quicker completion polling and an optional draft-model comparison remain proposals.

## Half-done

- None for the question. Existing production relay and headset build remain unchanged.

## Next steps, in order

1. Explain that local overhead can be trimmed but the provider generation time dominates. Preserve the accepted 500k renderer quality.
2. If implementing optimizations, retain cancellation/journal safety and avoid speculative paid worlds.

## How to run and check

- Follow 2026-11-15-0843-coordinator-native-splats.md for the running relay, installed build and validation.

## Known problems

- Native splat rendering removes HQ export latency, not initial provider generation latency. Reducing rendered point count will not accelerate model inference.

## Requests for the coordinator

- None.
