# Prepared demo collection

The owner selected this content explicitly. The runtime has no Great Wall/DNA keyword branches: a catalog binds stable node IDs to separately cached environments.

| Node | Parent | Initial artifact |
|---|---|---|
| Great Wall of China | none | none; movable world pane |
| Great Wall — Watchtower Interior | Great Wall of China | none; movable world pane |
| Biology Lab — DNA | none | the inspectable model in `dna.json` |

Inside the Great Wall exterior, point at its linked tower orb and press X, or grip and throw it, to enter the tower. The tower exposes the exterior orb for the return trip. Y returns to the constellation, where the independent biology lab is available. Linked orbs restore their map positions on return.

Every node has a reading pane. The Great Wall scenes are generated interpretations, not surveyed historical reconstructions. Background reading for the pane: [UNESCO's Great Wall description](https://whc.unesco.org/en/list/438/), especially its description of a linked defensive system, the terrain and Ming masonry. The DNA representation is schematic, not a molecular simulation or a real sequence.

The exterior's exported atlas had severe speckling, and the tower's atlas swirled badly outside its windows in the actual Unity render. Their explicit `textureProjection: "panorama"` uses the existing generated panorama as a texture on the exported 3D surfaces, preserving geometry, collision and positional parallax. This offline repair spends no credits and preserves the original GLB. It cannot reconstruct the appearance behind surfaces hidden from the panorama viewpoint; large viewpoint changes can expose stretching. The lab keeps its exported atlas: its panorama comparison improved the initial view but stretched more when the viewpoint moved beside a bench.

## Budget and preparation

All three worlds and their HQ exports are complete. The owner subsequently supplied two additional funded keys and authorized finishing the tower and lab, one complete world per key. Each new account started at 7,000 credits. Generation and its HQ textured export cost 5,080 credits; the remaining 1,920 on each account must not be spent on incomplete extra worlds. This authorizes only the requested three-world collection.

Automatic generation remains disabled in `.env` and in the collection server. `probe_worldlabs.py --authorize-generation` is an explicit one-world operation; it accepts `--key-env WORLD_LABS_API_KEY_2` for another credential kept only in the ignored root `.env`. This does not enable generation in the app. The probe journals each paid submission and resumes operation IDs rather than starting duplicates. A `submitting` record without an operation ID must be reconciled with the provider before retrying.

The canonical specification is `great-wall-biology.json`. Source jobs and signed provider responses live only under ignored `Builds/Verification/demo-worlds/great-wall-biology-v1/<node-id>/`.

Import completed local exports and regenerate the catalog and playable constellation without API calls, from `relay/`:

```sh
.venv/bin/python scripts/assemble_world_collection.py \
  --spec demo/great-wall-biology.json \
  --source-root ../Builds/Verification/demo-worlds/great-wall-biology-v1 \
  --output ../Builds/Verification/demo-worlds/great-wall-biology-v1/catalog.json
```

This emits `catalog.json` and `constellation.json`. Only complete worlds are included in the playable seed; child worlds require a completed parent. Later imports preserve the IDs and correct parent links. A missing world never silently reuses another environment.

Serve the prepared collection with OpenAI questions/object creation, without loading either environment provider's keys:

```sh
.venv/bin/python scripts/run_world_collection.py \
  --catalog ../Builds/Verification/demo-worlds/great-wall-biology-v1/catalog.json \
  --port 8794 --enable-openai
```

The catalog reloads when assembled again. Back up headset saves, stop the app, install the APK, replace the current constellation with the generated seed, and clear the prior active `exploration/` saves only when the owner authorizes a fresh demo. Keep the backup outside the app. Restore USB reverse `tcp:8794 tcp:8794` after ADB/Unity restarts. The development relay override should be `http://127.0.0.1:8794`.

For an automated on-headset check, development builds consume a one-shot `zap-enter-node.txt` file in the app's persistent files directory. Put an existing node ID there before launching; the app removes the file and enters that node once. This is not active in release builds and does not create a world.
