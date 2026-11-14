# Brief: the void scene and controllers (lane: core)

Updated 2026-11-14. Read `AGENTS.md` first. Edit only `Assets/_Project/Scripts/Core/`, `Assets/_Project/Editor/`, `Assets/_Project/Scenes/` and your handoff file.

## Goal

The app opens in an empty, very dark purple-black space with tracked controllers and no way to walk by thumbstick. The existing Quest build menu builds it.

## Context

- The only scene today is `Assets/_Project/Scenes/Main.unity`: the template rig, a teleport floor, a pedestal and a grab cube. Leave that file untouched.
- The controller rig prefab is `Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab`. Read it to learn its child names and components.
- The build menu is `Assets/_Project/Editor/QuestBuild.cs`. Read it before changing it.
- The world lane installs itself at run time. Do not reference `Zap.Net` or `Zap.World`.

## Deliver

### `Assets/_Project/Scripts/Core/` (assembly `Zap.Core`, namespace `Zap.Core`)

- `Zap.Core.asmdef`. Reference by name only what you use (for example `Unity.XR.Interaction.Toolkit`, `Unity.XR.CoreUtils`, `Unity.InputSystem`).
- `ZapColors.cs`: static colours. Void `#07050D`, void lift `#140D26`, white `#FFFFFF`, paper `#F2F0F7`, ink `#0B0814`.
- `VoidEnvironment.cs`: installs itself from `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`, only when the active scene is named `Void`.
  - Sets the main camera to clear to solid colour `ZapColors.Void`.
  - Removes the skybox and sets flat ambient light.
  - Turns off thumbstick movement, turning, teleport, climb and jump on the rig. Keep grabbing, poking and ray interaction working.
  - Logs one line listing what it turned off, and one warning for anything it expected and did not find.

### `Assets/_Project/Editor/`

- `ZapSceneBuilder.cs`: menu item `ZAP/Build Void Scene` and a public static method `Zap.EditorTools.ZapSceneBuilder.BuildVoidScene()` that Unity can run with `-executeMethod`.
  - Creates a new empty scene.
  - Instantiates the rig prefab with `PrefabUtility.InstantiatePrefab` so it stays linked to the prefab.
  - Adds an XR Interaction Manager, and an EventSystem with the XR UI input module, matching what `Main.unity` has.
  - Adds no floor, no light and no other objects.
  - Saves to `Assets/_Project/Scenes/Void.unity`.
  - Makes `Void.unity` the only enabled scene in the build settings.
  - Exits with a non-zero code when run in batch mode and anything fails.
- `QuestBuild.cs`: make it build `Void.unity`. Keep the menu name and output path. Run `BuildVoidScene()` first if `Void.unity` does not exist.

## Constraints

- Do not write `Void.unity` yourself. The coordinator runs `BuildVoidScene()` in Unity, which creates it.
- Do not add packages or change project settings. Passthrough stays off.
- No placeholder stars, floor grid or other content. The constellation is a later brief.
- Editor code must not leak into player builds: keep it under `Assets/_Project/Editor/`.

## Done when

- Every file above exists and you have re-read each for compile errors.
- Your handoff names each rig component you turn off, with the child path where you found it in the prefab.
- Your handoff lists exactly what you could not check. You cannot compile or run Unity; the coordinator will.

## Not in this brief

- Nodes, lines, throwing, voice.
- Anything that talks to the relay.
