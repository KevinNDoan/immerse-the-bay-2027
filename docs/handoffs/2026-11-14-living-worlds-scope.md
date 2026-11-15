# Handoff: general generated living worlds

Written by: Codex
Reason: corrected Orbis assessment after reviewing the owner's primary sources

## Where things stand

- Baseline is commit `4c3056b`; the earlier voice/throw/parent-context/Orbis implementation is committed by another actor. This turn has not committed or pushed anything.
- The owner wants a navigable, living space with full surroundings and interactive teaching objects. This is an experience requirement, not a mandate to replace Orbis with a particular geometry provider.
- The owner explicitly says DNA/lab/dinosaurs are examples only. Do not hardcode scene types, theme keyword switches or topic-specific builders. `docs/tasks/m2-living-worlds.md` records this corrected scope.
- No scene implementation or asset download was made before that correction. Only the new brief and this handoff were added.

## Finished and checked

- Inspected the actual renderer: `DomeView` defaults to Front, maps 110 by 62 degrees, and `ZapDome.shader` returns black for UVs outside the front patch. This explains the one-wall complaint. Merely switching to Wrap would stretch ordinary video and would not create real 360 imagery or geometry.
- Rechecked Reactor's Orbis API: text/image input, video/audio output, prompt changes at chunk boundaries. The documented commands do not expose camera poses, synchronized stereo/surround views or an object-state interface. This limits what is confirmed through our endpoint; it does not establish fundamental model impossibility.
- Reviewed the owner's Visko Orbis 1.0 paper, including formulation, memory, inference and conclusion, and the Visko site. The paper reports stateful live prompt control, 4K/24 FPS delivery and average visible response below one second in its serving configuration. It does not document a tracked VR interface. Reactor's published endpoint specifications remain 18 FPS and approximately 1.8-second chunk boundaries. Treat the serving difference as unresolved.
- The earlier categorical statement that Orbis alone cannot meet the requirement was not justified. Video output alone does not rule out navigability; viewpoint control, consistent views and responsiveness need separate verification.
- World Labs currently documents a generation API and a textured GLB mesh export API. These provide an environment-generation integration to evaluate for Unity. Generation is asynchronous and documented around five minutes; a new generated world is not an instantaneous teleport on the first request.
- Meshy documents separate text-to-3D asset generation, but its rigging API says standard humanoid characters only. Do not promise this automatically rigs arbitrary animals or makes arbitrary generated scenes alive.
- No World Labs/Meshy credentials are available. The user asked whether to obtain a key and whether Orbis alone can meet this requirement.

## Written but not checked

- No new runtime code, provider adapter, import pipeline or generic simulation has been implemented yet.
- World Labs geometry quality, exported GLB characteristics, scale, collisions, import speed and Quest 3S performance are untested in this project.

## Half-done

- The authorized generic architecture needs request/ancestor interpretation and reusable interactions. Which visual and spatial functions Orbis can supply is still under assessment; do not lock in a replacement provider before that assessment.
- No additional API key is required for the next Orbis capability investigation. World Labs remains an optional environment candidate, not a prerequisite established by this review.

## Next steps, in order

1. Validate the available Orbis path: coherent surrounding imagery, controllable viewpoints, revisiting views, prompt response and any documented stereo/depth capability. A prompted panoramic experiment would be an experiment, not proof of native VR support. Do not send vendor messages without the owner's instruction.
2. Correct the front-only presentation while keeping its limitations explicit. Ordinary video stretched or repeated over a sphere is not sufficient acceptance evidence for the requested world.
3. Build generic local interaction components and request/ancestor interpretation independently of topic names. Any procedural fallback must be labeled as such and must not be presented as realistic generation.
4. Evaluate an additional environment/asset provider only for a demonstrated gap. No accounts, payments or sign-ins may be performed by the agent; no new credential has been requested as a prerequisite by this revised assessment.

## How to run and check

- Existing setup and demo controls are in `docs/voice-worlds.md`. This scope correction made no changes to the installed APK.
- `git diff --check` is sufficient for these documentation-only changes.

## Known problems

- Merely stretching the current ordinary video does not create missing surrounding views, translational parallax or interactive objects. Whether a different Orbis configuration can provide suitable imagery is unverified.
- Fully general, realistic, animated and interactive content is not guaranteed by one API call. State generation time and supported interaction/animation boundaries honestly.
- Runtime GLB/splat loading is not currently present in the Unity project. Respect AGENTS.md's package/ProjectSettings restrictions when proposing an importer.

## Sources checked

- https://www.visko.ai/blog/papers/files/Visko_Orbis_1_0__A_Live_Model_for_Real_TimeInteractive_Long_Video_Generation.pdf (especially pages 2, 6-7, 10 and 15)
- https://www.visko.ai/#where-worlds-go-live
- https://www.visko.ai/models (the linked travel demo redirected to login; it was not tested)
- https://www.reactor.inc/models/visko-orbis-stable/api
- https://docs.reactor.inc/model-api-reference/visko-orbis-stable/overview.md
- https://www.reactor.inc/models/visko-orbis-dynamic/api
- https://docs.worldlabs.ai/api
- https://docs.worldlabs.ai/api/reference/worlds/export
- https://docs.worldlabs.ai/api/pricing
- https://docs.meshy.ai/en/api/rigging

## Requests for the coordinator

- Preserve the topic-agnostic requirement and the owner's demand for actual 3D presence. Do not reinstate curated lab/Jurassic routing.
- Keep API credentials out of tracked files and logs; no new keys have been provided for this phase.
