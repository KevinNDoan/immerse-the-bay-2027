# Brief: start in front of the user (lane: core)

Updated 2026-11-14. Read `AGENTS.md` first. Core-lane work on the branch `core/app`.

## Changed after review (2026-11-14)

- `HomeFrame` gained `public static bool Hold { get; set; }`. While it is true the rig is not moved. `ConstellationController.Update` sets it while a ball is held, during a two-grip pull, and during a cut sweep.
- Alignment waits for a head pose that changed since the request, so a pose left over from before a pause is not used.
- Alignment waits until the head is not looking almost straight up or down.
- After 1.5 seconds with no usable pose the rig is aligned anyway, then aligned once more when a usable pose arrives. In the Editor simulator this shows as one snap the first time the simulated head moves.
- Input subsystems are looked up again every second, so recentring keeps working after the XR session restarts.
- Where the sections below disagree with this list, this list wins.

## Goal

When ZAP! starts in the headset, the controls card and the workspace are straight ahead of the person, the way the Quest's own onboarding card is. Today the card rides on the left controller and the constellation sits wherever the tracking space happens to point, so people have to turn around to find things.

- At start, the rig is turned and moved so the person stands over the world origin facing world +Z. This is the home frame.
- The controls card is fixed in the world, straight ahead in the home frame. It no longer follows a controller or the head.
- The home frame is set again when the headset is put back on and when the person recentres with the Meta button.

## Reuse, do not rewrite

- The existing controls card (`_hud` in `ConstellationController`) stays the one card. Only where it sits changes.
- The rig is the scene object `XR Origin (XR Rig)`; the head is `Camera.main`. Move the rig's Transform. Add no package and no assembly reference to `Unity.XR.CoreUtils`.
- `ZapTheme` for every size and distance.

## Two parts, built at the same time

- Part A: `Assets/_Project/Scripts/Core/HomeFrame.cs` (new).
- Part B: `Assets/_Project/Scripts/Constellation/ConstellationController.cs`, `Assets/_Project/Scripts/Core/ZapTheme.cs`, `Assets/_Project/Tests/World/ConstellationTests.cs`, `Assets/_Project/Tests/World/Zap.World.Tests.asmdef`.
- Edit only your part's files. Code against the contract below; the other part is being written at the same time.
- Do not write `.meta` files. Do not start Unity. Do not run git commands that change state. Skip the handoff file.

## Contract shared by both parts

`Zap.Core.HomeFrame` (Part A):

```csharp
namespace Zap.Core
{
    public sealed class HomeFrame : MonoBehaviour
    {
        // 0 until the rig has been aligned once; counts up on every later alignment.
        public static int Version { get; private set; }
        // World height of the head at the last alignment. 1.5 before the first one.
        public static float HeadHeight { get; private set; }
        // Turns `rig` about the vertical axis through `head` and slides it, so `head` ends over x = 0, z = 0 facing world +Z.
        // Yaw only. Height, pitch and roll are untouched.
        public static void Align(Transform rig, Transform head);
        // Asks for a fresh alignment on the next frames.
        public static void Request();
    }
}
```

After an alignment the home frame is the world frame: straight ahead is +Z, the person is at x = 0, z = 0.

## Part A: `HomeFrame.cs`

### `Align(Transform rig, Transform head)`

- Return at once when either argument is null.
- Take the head's forward projected on the horizontal plane. If its squared length is under `1e-4`, skip the rotation.
- Otherwise `rig.RotateAround(head.position, Vector3.up, -yaw)` where `yaw` is the signed angle from `Vector3.forward` to that projected forward about `Vector3.up`.
- Then move the rig by `-(head.position.x, 0, head.position.z)`.

### The component

- Installs itself from `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` when the active scene is `Void`, `!ZapPlatform.IsDesktop`, and no `HomeFrame` exists: `new GameObject("[ZAP Home Frame]").AddComponent<HomeFrame>()`.
- Reset the statics from `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`: `Version = 0`, `HeadHeight = 1.5f`, instance null. The Editor can keep statics between Play sessions.
- A pending request starts in `Awake` and whenever `Request()` is called. Record `Time.unscaledTime` when it starts.
- In `Update`, while a request is pending:
  - Find the rig with `GameObject.Find("XR Origin (XR Rig)")` and the head with `Camera.main`. Cache both; look again only while one is null. Return if either is missing.
  - The head pose counts as tracked when `head.localPosition.sqrMagnitude > 1e-6f` or `head.localRotation != Quaternion.identity`.
  - Align when at least 0.3 seconds have passed since the request and the pose is tracked, or when 1.5 seconds have passed regardless.
  - On aligning: `Align(rig, head)`, set `HeadHeight = head.position.y`, add one to `Version`, clear the request.
- `OnApplicationPause(bool paused)`: when `paused` is false, `Request()`. The headset going back on a head fires this.
- Recentre with the Meta button: subscribe to `UnityEngine.XR.XRInputSubsystem.trackingOriginUpdated` on every input subsystem and call `Request()` from the handler.
  - The subsystems may not exist in `Awake`. Until at least one is subscribed, try once per second in `Update` with `SubsystemManager.GetSubsystems(list)` on one reused `List<XRInputSubsystem>`.
  - Unsubscribe in `OnDestroy`.
- No allocations per frame once running.
- Do not call `ZapPlatform.IsDesktop` from a field initializer or constructor. Unity throws.

## Part B: card, theme, test

### `ZapTheme.cs`

- Under `Controls card`, add `public float controlsDistance = 1.1f;` with tooltip `How far ahead of the person the card sits, in metres.` and `public float controlsDropBelowEyes = .3f;` with tooltip `How far below eye height the centre of the card sits, in metres.`.
- Delete `controlsMinDistanceFromFace` and its tooltip. Nothing reads it after this brief.

### `ConstellationController.cs`

- `BuildHud`: replace the camera-relative starting position with `position = (0, 1.2, theme.controlsDistance)` and `rotation = Quaternion.identity`.
- `UpdateHud`: delete the block that moves the card to the left controller and the line that turns it toward the camera each frame.
- Add `private int _hudFrame;`. In `UpdateHud`, when `HomeFrame.Version != _hudFrame`: set the card's position to `(0, HomeFrame.HeadHeight - theme.controlsDropBelowEyes, theme.controlsDistance)`, its rotation to `Quaternion.identity`, and `_hudFrame = HomeFrame.Version`.
- The card's text, sizes and colours stay exactly as they are.
- Change nothing else. `_left` is still needed elsewhere.

### `Zap.World.Tests.asmdef`

- Add `"Zap.Core"` to `references`.

### `ConstellationTests.cs`

Add one `[Test]`, `HomeFrameAlignPutsHeadOverOriginFacingForward`:

- Rig GameObject at `(1, 0, 2)` with yaw 30 degrees. Head GameObject as its child at local position `(0.3, 1.6, -0.2)` and local Euler rotation `(10, 70, 5)`.
- Call `HomeFrame.Align(rig.transform, head.transform)`.
- Assert the head's world x and z are within `1e-3` of 0, its world y is within `1e-3` of 1.6, and its forward projected on the horizontal plane and normalised is within `1e-3` of `(0, 0, 1)`.
- Destroy both objects with `Object.DestroyImmediate`.

## Done when

- Every file in your part exists and you have re-read it for compile errors.
- Reply with a short summary listing anything you could not check. The coordinator compiles, runs the tests, and looks at the running app.

## Not in this brief

- Moving saved topics. Topics saved before this change keep their old positions and may sit off to one side the first time.
- A button to recentre from inside the app.
- Any change to the card's look or wording.
