# Brief: navigable, living learning spaces

The owner explicitly selected fully navigable 3D scenes and living behavior on 2026-11-14, after clarifying the difference between a panorama and real geometry. The owner then clarified that DNA/lab/dinosaurs are examples only: the implementation must generalize to arbitrary requests and must not hardcode these topics.

The owner's subsequent clarification defines the product as a knowledge graph of worlds: each idea has its own world, similar ideas should group together, and exploration may jump across loosely related or unrelated subjects. Preserve prior context as optional background, not a requirement that each new idea be a subtopic. Automatic semantic grouping and associative graph links are a separate outstanding implementation item; do not claim that lineage links provide them.

The owner further emphasized learning through interacting with and exploring the whole idea-world. A static room containing one generic inspectable model is insufficient. Plan meaningful discoverable elements, topic-appropriate actions, observable consequences and contextual explanations across the space, using reusable interaction behaviors rather than hardcoded subjects. Preserve discoveries on revisit and support branching to other idea-worlds. These capabilities remain outstanding; current grab/resize/spread/voice-revision behavior must not be described as full world interactivity.

- Replace the main video-wall experience with convincing surroundings in every direction and responsive exploration. Evaluate Orbis against these requirements before prescribing another environment provider.
- Generate scene structure, objects and behavior from each request plus parent context through a shared pipeline. Do not implement a lab/Jurassic switch or topic-specific scene constructors.
- Preserve speech creation/editing, parent-node context, saved constellations and Orbis as a central part of the experience. The owner supplied Visko's Orbis 1.0 paper and challenged the proposed provider switch. Do not equate video output with an inability to support a navigable experience, or claim that an undocumented VR capability is available.
- Add local locomotion with collision and snap turning; restore the constellation viewpoint on exit.
- Keep head tracking and direct object manipulation responsive independently of prompt-generation delay. Validate coherent surrounding views, viewpoint control, spatial persistence and stereo/depth support separately; filling the dome with stretched video does not establish these capabilities. Validate generated scene plans before instantiation and state provider/asset limitations explicitly.
- Do not require World Labs or Meshy credentials before assessing the existing Orbis access. Those providers remain optional candidates if a verified capability gap warrants them.
- Codex owns integration across Core, Constellation, World, Net, Editor, tests, related Resources/art assets and docs for this brief.
- Unity may run for import, compilation, meaningful PlayMode checks, rendered previews and Quest builds. Never open the project in two Unity processes.
- Use freely licensed assets with provenance. Do not purchase assets or create/sign into accounts. Report remaining realism limitations honestly.
- Generate all serialized Unity assets and metadata with Unity; do not hand-edit them. Do not change packages or shared ProjectSettings.
- Build and install on the authorized headset when connected. No commits or pushes without a request.
