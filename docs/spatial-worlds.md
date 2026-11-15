# Cohesive 3D world prototype

## Scope and budget

The owner rejected the detached video-background/teaching-overlay experience and authorized a World Labs experiment. After one world and one HQ export were started, the owner explicitly instructed us to reuse **only that world** for all further tests. The replay runner pins that world in its command and never loads a World Labs key. The normal relay must keep `WORLD_GENERATION_ENABLED=false`. Both generation and export methods reject new jobs by default. Do not enable new jobs without a new user instruction.

The pinned world is a test environment, not a topic keyword preset. Every request carries its current idea and optional exploration history into GPT's learning-object generation. A clear new idea takes priority and may be unrelated to previous ideas; context only helps clarify ambiguity or requested connections. Different topics reuse the same physical test environment to avoid World Labs charges. No lab/DNA/dinosaur routing exists. The product goal is a knowledge graph of distinct worlds; automatic similarity grouping is not implemented yet.

## What changed

- A World Labs textured mesh and its collider share one transform. Head position, stereo rendering, occlusion and local walking operate on geometry.
- The relay converts static embedded GLB into bounded mesh packets and resizes textures to at most 4096 pixels. Unity loads those packets at runtime; no new Unity package or project-setting change is needed.
- Marble's raw OpenCV axes are converted to Unity axes, with its metric scale applied to visual and collision meshes together. Ground metadata aligns the floor. Entry searches for clear standing space and an accessible supporting surface instead of trusting a generated camera position that may overlap furniture. A distant panorama covers the sky and gaps outside reconstructed geometry.
- Learning objects float freely as representations for experimentation. They use the scene depth buffer but have no gravity or furniture-placement requirement. A translucent glass pane explains the selected part, discovery step or experiment state. Each pane can be grabbed and placed independently; placement and the latest answer persist per object. The environment is a haven, not a constraint on the model’s physics.
- The same bounded geometry vocabulary builds all objects: spheres, boxes, cylinders, rings, tubes, smooth helices, repeated structures and exact connectors between paths. Up to five discovery states and one continuous qualitative experiment attach explanations to visible changes. Objects can be held, rotated, zoomed, paused and revised; voice can add another object or answer without replacing it. Up to six objects and their exploration state persist per idea. These are schematic representations, not arbitrary photorealistic assets or validated scientific simulations.
- New ideas can be spoken and thrown from inside a world. They retain the current world's ancestry and appear in the saved constellation when returning.
- Crystal-ball previews and the hyperspace journey are integrated from the coordinated visual-design chat. The journey waits for loaded geometry or the first decoded video frame, supports Y cancellation, shows elapsed time and has a bounded client wait.

## HTTP extension

The existing socket protocol remains available for the Orbis experiment. The following HTTP routes support geometry without sending provider credentials or signed asset URLs to Unity:

| Route | Behavior |
|---|---|
| `POST /world/prepare` | `{topic, context}`; returns `projection: geometry`, `state: preparing/ready/error`, `cacheKey`, and `notice` |
| `GET /world/jobs/{cacheKey}` | Poll the saved job; ready responses include `plan` and `environment` |
| `GET /world/assets/{cacheKey}/{filename}` | Local converted mesh or JPEG; IDs/filenames are constrained |
| `GET /world/preview` | Base64 preview of the existing pinned panorama; never creates a provider job |
| `POST /world/study` | Legacy revision endpoint: `{topic, context, instruction, current}` returns a validated model |
| `POST /world/explore` | `{topic, context, instruction, current, focused, objects, step, parameter}`; returns `{action: add/revise/answer, answer, model}`. Questions keep geometry; additions preserve existing objects |

`environment` contains `basePath`, `visual[]`, `collision[]`, `panorama`, `metricScale`, `groundOffset`, and `ambient`. Each mesh entry contains `mesh`, `texture`, `vertices`, and `triangles`.

Mesh packets are little-endian: `ZMS1`, uint32 vertex count, uint32 index count, float32 XYZ per vertex, float32 UV per vertex, RGBA8 per vertex, then uint32 indices. Counts, finite coordinates, array lengths and index bounds are checked before Unity mesh construction. Visual and collision triangle counts are bounded. GLB compression, skins, animation, sparse accessors and unsupported required extensions fail explicitly.

Completed geometry and request-specific plans are cached. Normal job mode records provider operation IDs so a restart can resume polling instead of launching a duplicate. Current pinned test mode only reads existing geometry and generates topic-specific plans through OpenAI; it cannot silently create another World Labs environment.

## Orbis findings and retained experiment

The available endpoint supplies prompt/image inputs and generated video/audio. The tested text-only panoramic prompt produced a distorted perspective image. An equirectangular seed began correctly but drifted into perspective framing. Four image-seeded streams filled the cardinal directions, but they did not share reliable geometry or object state. Some startup views also hit Reactor's 10-session/minute limit. Session reuse, bounded retry, partial-view fallback and cleanup remain available for comparison.

The experimental live atlas uses 960×540 tiles, front/right on the top row and back/left on the bottom row, emitted at up to 12 FPS. Optional status fields `projection: multiview` and `liveViews` (four-bit availability mask) drive the renderer. Ceiling/floor outside the live fields retain the seed. This is not the geometry mode's environment renderer.

## Verification and limitations

The public World Labs library export is used only as an ignored renderer fixture under `Builds/Verification/worldlabs-import`, with provenance in `SOURCE.md`; it is not shipped as a fallback or routed by topic. GPU previews verify orientation, supporting-surface placement and translational parallax. The paid test world has its own separate verification directory. See the newest coordinator handoff for final build/install and live-export results.

The single paid export completed and was imported into the local pinned cache on November 14. Its visual mesh has 597,297 triangles and its collider has 260,792. Live HTTP checks for a rhythmic-ratio model and a lever model returned different teaching objects with the same environment asset path. The World Labs balance remained 1,920 credits before and after both requests. A follow-up instruction revised and reduced the rhythmic model through the existing study endpoint. These were request data used for testing, not topic-specific runtime branches.

The environment mesh is static. Local teaching objects animate, but arbitrary inhabitants do not yet walk, react or become individually manipulable. Close inspection can expose reconstructed-surface holes or softness. Stick movement uses mesh collision and ground checks; physical head movement is still controlled by the headset. New environment generation plus HQ export can take minutes to an hour, so generation is not an instant teleport. Cached loading is substantially faster.

Sources checked: [World Labs export API](https://docs.worldlabs.ai/api/reference/worlds/export), [mesh export limits](https://docs.worldlabs.ai/marble/export/mesh), [Marble coordinate conventions](https://docs.worldlabs.ai/api/rendering-spz), [glTF 2.0](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html), [Orbis API](https://docs.reactor.inc/model-api-reference/visko-orbis-stable/overview).

## Exploration model contract

`relay/zap_relay/study_models.py` defines and validates the strict data schema. Geometry is bounded to 28 part groups and 160 repeated components. `helix` defines radius/height/turns/phase; `links` samples two actual curve paths, with optional half-connectors for distinct colors. Steps reference part indices and apply independent position/rotation/scale/visibility changes from the baseline. The experiment interpolates bounded transforms from 0 to 1. It is a qualitative illustration, with no generated executable code or physics-solver claims. Current state and focused part accompany follow-up questions. Generated explanations can still be wrong; checked fixtures do not establish educational correctness for every future request.

Movement now uses a graded deadzone, bounded acceleration and frame time, contact sliding, small-gap floor tolerance, and restrained vertical correction. It continues while speech or AI requests are pending. Snap turning remains a comfort choice. Actual headset frame-time and comfort must be assessed separately from movement logic tests.

The world pane owns learning content and request feedback; the wrist display owns controls and short status. A voice question snapshots its selected object, focused part and exploration state at recording start. Responses return to that object, even if the pointer moves during transcription. Hover updates action targeting without switching the visible reading pane; deliberate grip/experiment/discovery actions switch it. Cancel invalidates the request, aborts the client HTTP connection and suppresses delayed mutations. A provider call already started may still finish on the laptop, but its cancelled result is not applied.

The pane shader uses translucent tinted glass, rounded illuminated edges and a subtle sheen, with stereo instancing and no HDR or scene-color copy. It is not a blurred refraction effect. Positioned panes retain their orientation rather than turning toward the head after release. X resets the selected object and pane to a comfortable starting placement.
