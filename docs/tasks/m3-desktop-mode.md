# Brief: desktop mode (lane: core)

Updated 2026-11-14. Read `AGENTS.md` first. Core-lane work on the branch `core/app`.

## Goal

ZAP! runs on a laptop screen with mouse and keyboard, with no headset. Same scene, same constellation code, same relay. Only how you point, move and type is new.

## Reuse, do not rewrite

- `ConstellationController` owns nodes, links, voice, saving and worlds. Desktop mode calls its public methods. It never duplicates that logic.
- `LineCutter.SweepCrosses` for cutting. `SuggestedStars` for suggestions. `DomeView` in `Front` mode for worlds. `ZapTheme` for every colour and size. uGUI and TextMeshPro for the panel. The `EventSystem` already in `Void.unity` handles the mouse.
- No new packages. No scene, prefab or material files.

## Four parts, built at the same time

- Part A: `Assets/_Project/Scripts/Core/ZapPlatform.cs` (new), `Assets/_Project/Scripts/Desktop/Zap.Desktop.asmdef` (new), `Assets/_Project/Scripts/Desktop/DesktopMode.cs` (new), `Assets/_Project/Scripts/Constellation/ConstellationController.cs`, `Assets/_Project/Scripts/Constellation/SuggestedStars.cs`.
- Part B: `Assets/_Project/Scripts/Desktop/DesktopHud.cs` (new).
- Part C: `Assets/_Project/Editor/DesktopBuild.cs` (new).
- Part D: `Assets/_Project/Scripts/Core/ControllerActions.cs`.
- Edit only your part's files. Code against the contract below; the other parts are being written at the same time.
- Do not write `.meta` files. Do not start Unity. Do not run git commands that change state. Skip the handoff file.

## Contract shared by all parts

`Zap.Core.ZapPlatform` (written by Part A):

```csharp
namespace Zap.Core
{
    public static class ZapPlatform
    {
        // True on a computer with no headset active. False on Quest and whenever an XR device is running.
        public static bool IsDesktop => !Application.isMobilePlatform && !UnityEngine.XR.XRSettings.isDeviceActive;
        // Set by the desktop panel while a text field has keyboard focus.
        public static bool IsTyping { get; set; }
    }
}
```

`Zap.Desktop` assembly: references `Zap.Core`, `Zap.Net`, `Zap.World`, `Zap.Constellation`, `Unity.InputSystem`, `Unity.TextMeshPro`, `Unity.ugui`, `Unity.XR.Interaction.Toolkit`, all by name. Namespace `Zap.Desktop`.

`ConstellationController` members desktop mode may use. The first group exists today; the second is added by Part A:

- Exists: `Current`, `Balls`, `Status`, `Busy`, `InWorld`, `IsListening`, `Select(TopicBall)` (null deselects), `SubmitText(string text, bool edit)` (creates a topic, edits the selected one, or steers when in a world), `EnterWorld(TopicBall)`, `LeaveWorld()`, `ToggleLink(TopicBall from, TopicBall to)`.
- Added: `public bool ShowControlsCard { get; set; } = true;` and `public bool CutAcross(Vector3 previousOrigin, Vector3 previousDirection, Vector3 origin, Vector3 direction)`.

`SuggestedStars`, added by Part A: `public bool TryKeep(Collider collider)`.

## Part A: desktop mode and hooks

### `ConstellationController.cs`

- `ShowControlsCard`: when false, the floating controls card (`_hud`) is inactive and `UpdateHud` does nothing else.
- `CutAcross`: move the existing loop over `_lines` that calls `LineCutter.SweepCrosses` into this method. It cuts the nearest crossed line with `CutEdge` and returns true, or returns false. The headset trigger gesture calls it with `(_cutOrigin, _cutDirection, _right.position, direction)`.
- `OnGUI`: return at once when `ZapPlatform.IsDesktop`. The desktop panel replaces that developer overlay.
- Change nothing else.

### `SuggestedStars.cs`

- `TryKeep(Collider collider)`: if the collider belongs to a star that is showing, do what gripping it does and return true. Otherwise return false.

### `DesktopMode.cs`

`public sealed class DesktopMode : MonoBehaviour`. Installs itself from `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` when the active scene is `Void` and `ZapPlatform.IsDesktop`: creates a GameObject `[ZAP Desktop]` with `DesktopMode` and `DesktopHud`.

Setup, once `ConstellationController` and `Camera.main` exist:

- Disable every `UnityEngine.InputSystem.XR.TrackedPoseDriver` on the main camera so the script owns the camera.
- Deactivate `XR Origin (XR Rig)/Camera Offset/Left Controller` and `.../Right Controller`.
- Set `controller.ShowControlsCard = false`.
- Show and unlock the cursor.
- Use `Mouse.current` and `Keyboard.current` from the Input System. `UnityEngine.Input` throws in this project.

Camera: an orbit camera around a pivot.

- Start: pivot at the average position of all balls, or `(0, 1.3, 0.6)` when there are none; distance 1.6; yaw 0; pitch 10 degrees.
- Right-drag orbits (pitch limited to plus or minus 80 degrees). Shift + right-drag pans the pivot in the camera's plane. Scroll zooms (distance 0.3 to 8).
- `F` moves the pivot to the average position of all balls.
- Ease the camera toward its target each frame; no snapping.

Constellation interactions, only when `!controller.InWorld`. Ignore a mouse press that starts over the panel (`EventSystem.current.IsPointerOverGameObject()`), and ignore keys while `ZapPlatform.IsTyping`:

- Pick with `Physics.Raycast` from the camera through the mouse, range 20. A hit whose parents include a `TopicBall` is that ball. Otherwise pass the collider to `SuggestedStars.TryKeep`.
- Left press on a ball: `controller.Select(ball)`.
- Left drag on a ball (moved more than 4 pixels): move the ball on the plane that faces the camera through the ball's starting position. Keep its `Body.linearVelocity` at zero while dragging.
- Shift + left drag from one ball to another: on release over a different ball, `controller.ToggleLink(start, end)`. While dragging, show one `LineRenderer` from the start ball to the mouse point (material from `Shader.Find("Zap/Solid")`, colour and width from the theme). Destroy that material in `OnDestroy`.
- Alt + left drag starting in empty space: each frame call `controller.CutAcross(camera position, previous mouse ray direction, camera position, current mouse ray direction)`. Stop after the first cut until the button is released.
- Left press on empty space with no modifier: `controller.Select(null)`.
- Double-click a ball (two presses within 0.35 seconds): `controller.EnterWorld(ball)`.

World view, while `controller.InWorld`:

- On entering: remember the camera's position, rotation and field of view. Set rotation to identity and vertical field of view to 62, so the dome's front picture fills the window. Stop all orbit and picking.
- On leaving: restore them.
- Keyboard steering is handled elsewhere. Add nothing for it.

No allocations per frame once running.

## Part B: `DesktopHud.cs`

`public sealed class DesktopHud : MonoBehaviour`, on the same GameObject as `DesktopMode`. Builds a screen-space panel in code and finds the controller with `FindAnyObjectByType<ConstellationController>()` (it may appear a few frames late; wait for it).

- Canvas: `ScreenSpaceOverlay`, `CanvasScaler` scaling with screen size at 1920 by 1080, match 0.5, plus a `GraphicRaycaster`. If no `EventSystem` exists, log one error; do not create one.
- Every text is TextMeshProUGUI, white, using `ZapTheme.Current.font` when it is set. Panels are plain `Image` rectangles coloured `ZapTheme.Current.voidColor` at 85 percent opacity. No sprites.
- Pieces:
  - Top left: the word `ZAP!` (36 px). In a world, the current topic beside it and a `Back` button that calls `controller.LeaveWorld()`.
  - Bottom centre: a text field 720 wide and 56 tall. Placeholder `Type a topic and press Enter`; in a world, `Ask to see something and press Enter`. On submit with non-empty text: `controller.SubmitText(text, false)`, clear the field, keep focus.
  - Just above it: one line showing `controller.Status` (20 px). While `controller.IsListening`, show `Listening…` instead.
  - Right side, 360 wide, shown when `controller.Current != null && !controller.InWorld`: the topic (28 px), the summary (18 px, wrapped), a button `Enter world` calling `controller.EnterWorld(controller.Current)`, and a button `Edit`. `Edit` reveals a second text field; on submit, `controller.SubmitText(text, true)` and hide it.
  - Bottom left, 14 px at 70 percent white. In the constellation: `Click: select · Drag: move · Shift-drag: link · Option-drag: cut · Double-click: enter · Right-drag: orbit · Scroll: zoom · Hold V: speak`. In a world: `Arrows: move and look · Space: event · Esc: back · Hold V: speak`.
- Buttons and text fields are not interactable while `controller.Busy`.
- Every frame: `ZapPlatform.IsTyping` = true when either text field has focus. Set it false in `OnDisable`.
- Build each `TMP_InputField` fully in code: a root with `Image` and `TMP_InputField`; a child `Text Area` with `RectMask2D`; under it a `Placeholder` and a `Text`, both TextMeshProUGUI; then set `textViewport`, `textComponent`, `placeholder`, `lineType = SingleLine`, and listen to `onSubmit`.
- Update texts only when their value changes. No allocations per frame.

## Part C: `DesktopBuild.cs`

Namespace `ImmerseTheBay.Editor`, static class `DesktopBuild`, modelled on `QuestBuild.cs`.

- Menu item `Immerse the Bay/Build Desktop App (Mac)` and `public static void BuildMac()`, runnable with `-executeMethod`.
- Throw `BuildFailedException` unless the active build target is `BuildTarget.StandaloneOSX`.
- Build `Void.unity` only (use `Zap.EditorTools.ZapSceneBuilder.VoidScenePath`; build the scene first if it is missing, as `QuestBuild` does) to `Builds/Desktop/ZAP.app`, with `BuildOptions.Development`. Development is required: plain HTTP to the relay is only allowed in development builds.
- Throw `BuildFailedException` when the build does not succeed. Log the output path.

## Part D: `ControllerActions.cs`

- The keyboard block currently runs when `keyboard != null && (Application.isEditor || Debug.isDebugBuild)`. Make it run when `keyboard != null && !ZapPlatform.IsTyping && (Application.isEditor || Debug.isDebugBuild || ZapPlatform.IsDesktop)`.
- Reason for a comment: typing a topic must not trigger the V, B, Enter, Escape, Space and arrow shortcuts.
- Change nothing else.

## Done when

- Every file in your part exists and you have re-read it for compile errors.
- Reply with a short summary listing anything you could not check. You cannot compile or run Unity; the coordinator will, and will look at the running app.

## Not in this brief

- The home screen and multiple constellations. That is the next brief.
- Deleting a node.
- A Windows build.
