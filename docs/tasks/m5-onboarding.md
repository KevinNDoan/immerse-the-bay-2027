# Brief: upright card, hints on the controllers, tutorial (lane: core)

Updated 2026-11-14. Read `AGENTS.md`, `docs/tasks/m4-start-in-front.md` and `docs/tasks/m4-controls-card.md` first. Core-lane work on the branch `core/app`.

## Changed after review (2026-11-14)

- Steps 3 and 4 swapped: step 3 is `Cut the link`, step 4 is `Link them`. A new topic already links to the selected one, so linking first would have removed that link and stalled the tutorial. Step 2's instruction says so.
- Step 3 is skipped when no link exists as it begins. Step 4 is done when the link count changes in either direction.
- Skip needs the left stick held for 0.7 seconds and is ignored inside a world. A single click was too easy to trigger by accident. A stick already held at start does not count.
- Titles are `<n> of 8 · <title>`. The tutorial clears the status line when it starts.
- `PlayerPrefs.Save()` follows every write.
- `ControllerHints` restores a renderer's own property block when a highlight ends, and searches only for parts a shown hint uses. `hintTextSize` default is .12.
- `HomeFrame`: the 8 second wait for presence is measured from when presence was first seen false and cannot be extended by later requests. Subscribed input subsystems are tracked in their own list.
- Where the sections below disagree with this list, this list wins.

## Goal

Feedback from the headset:

- The card looked angled down. It must be upright and straight ahead, like the Quest's own start-up card.
- Controls should be shown on the controllers themselves, not as a list on the card.
- First run needs a tutorial that teaches each control, with a way to skip it.

What changes:

- The card is vertical, 1.2 m ahead, its centre 10 cm below eye height. It shows a title, the current tutorial step when one is running, the status, and one hint line. The control list is gone from the card.
- Labels attach to the real buttons on the controller models. The button for the current tutorial step pulses. Outside the tutorial, a controller's labels show while the person looks at it.
- A tutorial of eight steps runs on first launch. Each step ends when the person does the action. Clicking the left stick skips it. Holding the left stick for 1.5 seconds in the void starts it again.
- The home frame waits until the headset is on a head before it lines up.

Sources checked on 2026-11-14: Meta's design pages say ray-operated panels belong 0.8 to 3 m away, about 1 m, slightly below the line of sight, level and facing the user. Unity OpenXR 1.11 and later report presence through `CommonUsages.userPresence`. Nothing documents a tilt.

## Reuse, do not rewrite

- The controller models already have named parts: `Trigger`, `Bumper` (the grip), `Button_A`, `Button_B`, `ThumbStick`. On the left controller `Button_A` is X and `Button_B` is Y. Find them by name under `XR Origin (XR Rig)/Camera Offset/Left Controller` and `.../Right Controller`. Use no measured offsets.
- `ControllerActions` for buttons. `HomeFrame` for where straight ahead is. `ZapTheme` for look values. `Zap/Solid` and `Zap/Glass` shaders. `TextMeshPro` 3D text.
- The VR template's `StepManager` and `Callout` are wired through prefabs. Do not use them, and do not edit any prefab.
- No new packages, shaders, scenes, prefabs or materials on disk.

## Five parts, built at the same time

- Part A: `Assets/_Project/Scripts/Constellation/Onboarding.cs` (new), `Assets/_Project/Scripts/Constellation/ConstellationController.cs`, `Assets/_Project/Tests/World/ConstellationTests.cs`.
- Part B: `Assets/_Project/Scripts/Constellation/ControlsCard.cs`, `Assets/_Project/Scripts/Core/ZapTheme.cs`.
- Part C: `Assets/_Project/Scripts/Constellation/ControllerHints.cs` (new).
- Part D: `Assets/_Project/Scripts/Core/ControllerActions.cs`.
- Part E: `Assets/_Project/Scripts/Core/HomeFrame.cs`.
- Edit only your part's files. Code against the contract below; the other parts are being written at the same time.
- Do not write `.meta` files. Do not start Unity. Do not run git commands that change state. Skip the handoff file.
- No allocations per frame once running. Never call `ZapPlatform.IsDesktop` from a field initializer or constructor.

## Contract shared by all parts

`Zap.Core.ControllerActions` (Part D) gains:

```csharp
public bool TutorialHeld;   // left thumbstick pressed in; Backspace in the Editor and debug builds
```

`Zap.Core.ZapTheme` (Part B) gains:

```csharp
public float cardStepSize = .34f;
public float hintTextSize = .12f;
public float hintLineWidth = .0015f;
public Color hintHighlightColor = Color.white;
```

`Zap.Constellation.ControlsCard` (Part B), replacing today's `Show`:

```csharp
public static ControlsCard Create(Transform parent);
public bool Visible { get; set; }
public bool IsCompact { get; }   // true while no step text is showing
// step: the tutorial instruction, or null or empty for none. hint: the bottom line, or null or empty for none.
public void Show(string title, string step, string status, string hint, bool listening, float level, bool busy);
```

`Zap.Constellation.ControllerHints` (Part C):

```csharp
public enum ControlId { RightA, RightB, RightGrip, RightTrigger, RightStick, LeftX, LeftY, LeftGrip, LeftTrigger, LeftStick }

public readonly struct ControlHint
{
    public readonly ControlId Id;
    public readonly string Label;
    public readonly bool Highlight;
    public ControlHint(ControlId id, string label, bool highlight = false);
}

public sealed class ControllerHints : MonoBehaviour
{
    public static ControllerHints Create(Transform parent);
    // Pass the same array instance while nothing changes. Null or empty clears every label.
    // always true: labels stay visible. false: a controller's labels show only while the person looks at that controller.
    public void Show(ControlHint[] hints, bool always);
}
```

`Zap.Constellation.Onboarding` (Part A):

```csharp
public struct TutorialFacts { public int Topics; public int Links; public bool Holding; public bool Pulling; public bool InWorld; }

public sealed class Onboarding : MonoBehaviour
{
    public const int StepCount = 8;
    public static Onboarding Create(ConstellationController controller, ControllerHints hints);
    public bool Running { get; }
    public int Step { get; }          // 0-based
    public string Title { get; }      // "2 of 8 · Grab it", or null when not running
    public string Text { get; }       // the instruction, or null when not running
    public void Skip();
    public void Restart();
    // Pure rules, for tests. `start` is the facts when the step began. `sawMiddle` is true once Middle() has held during the step.
    public static bool Middle(int step, TutorialFacts start, TutorialFacts now);
    public static bool Done(int step, TutorialFacts start, TutorialFacts now, bool sawMiddle);
}
```

`ConstellationController` gains (Part A): `public int LinkCount => _edges.Count;`, `public bool IsPulling => _pulling;`, `public bool IsHolding => AnyBallHeld();`.

## Part A: tutorial, controller, tests

### `Onboarding.cs`

Steps, 0-based. `Middle` is false where the column is empty.

| Step | Title | Instruction | Controls labelled, all highlighted | Middle | Done |
|---|---|---|---|---|---|
| 0 | Make a topic | Hold A on the right controller, say a topic, then let go. | RightA `Hold and speak` | | `now.Topics > start.Topics` |
| 1 | Grab it | Reach the topic, squeeze a grip to grab it, then throw it. | RightGrip `Grab`, LeftGrip `Grab` | `now.Holding` | `sawMiddle && !now.Holding` |
| 2 | Add another | Hold A and say a second topic. | RightA `Hold and speak` | | `now.Topics > start.Topics` |
| 3 | Link them | Point at one topic, hold the trigger, drag to the other, then let go. | RightTrigger `Hold and drag` | | `now.Links > start.Links` |
| 4 | Cut the link | Hold the trigger in empty space and sweep across the line. | RightTrigger `Hold and sweep` | | `now.Links < start.Links` |
| 5 | Move everything | Squeeze both grips in empty space and pull. Move your hands apart to stretch. | LeftGrip `Squeeze`, RightGrip `Squeeze` | `now.Pulling` | `sawMiddle && !now.Pulling` |
| 6 | Step inside | Point at a topic, then press X on the left controller. | LeftX `Enter the topic` | | `now.InWorld` |
| 7 | Look around | Move and look with the sticks. Press Y to come back. | LeftStick `Move`, RightStick `Look`, LeftY `Return` | `now.InWorld` | `!now.InWorld` |

- `Title` is `<n> of 8 · <step title>` with `n` 1-based (a longer title is cut off on the card). Build the eight strings once.
- When the tutorial starts or restarts, set the controller's status to empty: the idle status repeats step 1.
- Remembered with `PlayerPrefs` key `zap.tutorialDone` (int). `Create` starts the tutorial when it is 0.
- Each `Update`: read the facts from the controller (`Balls.Count`, `LinkCount`, `IsHolding`, `IsPulling`, `InWorld`); when a step begins store them as `start` and clear `sawMiddle`; set `sawMiddle` when `Middle` holds; move on when `Done` holds. After step 7: stop, set the pref to 1, and `controller.SetStatus("You're set. Hold the left stick to see the tutorial again.")`.
- Own one `ControllerActions` and `Poll()` it each `Update`.
  - While running: a fresh press of `TutorialHeld` calls `Skip()`.
  - While not running, not in a world: `TutorialHeld` held for 1.5 seconds calls `Restart()`. It must be released before it can trigger again.
- `Skip()`: stop, set the pref to 1, `controller.SetStatus("Tutorial skipped. Hold the left stick to see it again.")`.
- `Restart()`: step 0, running, pref to 0.
- Hints, set every `Update` with cached arrays (build each array once):
  - Running: `hints.Show(<the step's array>, true)`.
  - Not running, in the void: `hints.Show(VoidHints, false)` with RightA `Hold: new topic`, RightB `Hold: change topic`, RightGrip `Grab`, RightTrigger `Link or cut`, LeftGrip `Grab`, LeftX `Enter topic`, LeftStick `Hold: tutorial`.
  - Not running, in a world: `hints.Show(WorldHints, false)` with LeftStick `Move`, RightStick `Look`, RightA `Hold: speak`, RightB `Hold: change topic`, RightTrigger `Event`, LeftY `Return`.

### `ConstellationController.cs`

- Add the three public members from the contract.
- Add `private ControllerHints _hints; private Onboarding _onboarding;`. In `BuildHud`, after the card, when `!ZapPlatform.IsDesktop`: `_hints = ControllerHints.Create(transform); _onboarding = Onboarding.Create(this, _hints);`.
- `UpdateHud` body after the `ShowControlsCard` check becomes:

```csharp
bool tutorial = _onboarding != null && _onboarding.Running;
string title = tutorial ? _onboarding.Title : (_worldVisible ? _worldNode?.Data.topic : (Current != null ? Current.Data.topic : "ZAP!"));
string hint = tutorial ? TutorialHint : (_worldVisible ? WorldHint : VoidHint);
_card.Show(title, tutorial ? _onboarding.Text : null, Status, hint, _voice.IsRecording, _voice.Level, _busy);
```

with constants `TutorialHint = "Click the left stick to skip the tutorial"`, `WorldHint = "Sticks: move and look · Trigger: event\nA: speak · B: edit topic · Y: return"`, `VoidHint = "Look at a controller to see its buttons\nHold the left stick for the tutorial"`.

- Change nothing else.

### `ConstellationTests.cs`

- Update `ControlsCardBuildsAndSwitchesLayout` to the new `Show`: with a step text `IsCompact` is false; with a null step it is true.
- Add `[Test] OnboardingRulesAdvanceOnTheRightActions`: for each of the eight steps, `Done` is false with `now == start` and `sawMiddle` false, and true after the action in the table (for steps 1, 5 and 7 only once `Middle` has held and then stopped holding).

## Part B: `ControlsCard.cs` and `ZapTheme.cs`

### `ZapTheme.cs`

- Add the four fields from the contract, each with a one-line tooltip.
- Change defaults: `controlsDistance` to `1.2f`, `controlsDropBelowEyes` to `.1f`, `cardSize` to `(.56f, .41f)` (tooltip: card size while a tutorial step shows), `cardCompactSize` to `(.56f, .31f)` (tooltip: card size otherwise), `cardTitleSize` to `.4f`.
- Delete `cardBodySize`. Nothing reads it after this brief.

### `ControlsCard.cs`

- Remove the keys column, the labels column, the divider, their constants, and the built-in compact hint text.
- Texts, top to bottom inside the padding: title (one line, `cardTitleSize`, no wrap), step (up to three lines, `cardStepSize`, `cardTextColor`), status (up to three lines without a step, two lines with one; `cardStatusSize`; `cardTextColor` without a step, `cardDimTextColor` with one), level bar, hint (up to two lines, `cardHintSize`, `cardDimTextColor`).
- Two layouts. With a step: size `cardSize`, every piece shown. Without: size `cardCompactSize`, the step text hidden. `IsCompact` is true without a step. Switch when the step goes between empty and not empty.
- The hint object is hidden while the hint is null or empty.
- Box height stays `lines × LineHeight(size)`.
- Placement: position `(0, headHeight - theme.controlsDropBelowEyes, theme.controlsDistance)`, rotation `Quaternion.identity`. Delete the tilt. The card is vertical.
- Set each text only when its string changed. Keep the depth layers, the shader guard, the level bar behaviour and `OnDestroy` as they are.

## Part C: `ControllerHints.cs`

One label per `ControlId`, built once and pooled. All objects live under the component's own GameObject, in world space.

- Parts: `RightA`/`LeftX` → `Button_A`, `RightB`/`LeftY` → `Button_B`, grips → `Bumper`, triggers → `Trigger`, sticks → `ThumbStick`, searched by name among the descendants of that hand's controller object. While a controller or a part is missing, look again once per second. A hint whose part is missing shows nothing.
- Each label: a `TextMeshPro` at `hintTextSize` in `cardTextColor`, no wrapping, `richText = false`, and a `LineRenderer` with two points from the part to the label's near edge, width `hintLineWidth`, `Zap/Solid` in `cardOutlineColor`, `useWorldSpace = true`. One shared line material, destroyed in `OnDestroy`. If the shader is missing, log one error and show nothing.
- Layout, in `LateUpdate`, per controller: the labels form a column beside the controller on its outer side as the person sees it. The left controller's column is at the controller's position minus the head's right vector × 0.13 m, the right controller's at plus. Rows are 0.03 m apart along the head's up vector, centred on the controller, ordered by the height of their parts so the lines do not cross. Left-column text is right-aligned, right-column text left-aligned. Every label faces the head: `rotation = Quaternion.LookRotation(label position - head position, head up)`.
- Highlight: for a hint with `Highlight` true, pulse the part's `Renderer` with a `MaterialPropertyBlock`, setting `_BaseColor` and `_Color` to a blend between the material's own colour and `hintHighlightColor`, driven by `Mathf.PingPong(Time.unscaledTime * 1.5f, 1f)`. When the hint goes away call `SetPropertyBlock(null)` on that renderer. Reuse one `MaterialPropertyBlock`.
- Visibility. `always` true: every hint with a found part shows. `always` false: a controller's labels show while the angle between the head's forward and the direction to that controller is under 25 degrees and the controller is within 0.8 m; turn on after 0.25 s of that holding, off after 0.5 s of it not holding.
- `Show` stores the array and the flag. Rebuild label texts and highlights only when the array instance changes.
- The head is `Camera.main`. Do nothing while it is null.

## Part D: `ControllerActions.cs`

- Add `public bool TutorialHeld;`. In `Poll`: `TutorialHeld = Read(left, CommonUsages.primary2DAxisClick);`, and inside the existing keyboard block `TutorialHeld |= keyboard.backspaceKey.isPressed;`.
- Change nothing else.

## Part E: `HomeFrame.cs`

Wait for the headset to be on a head.

- Each `Update`, read presence: `var head = InputDevices.GetDeviceAtXRNode(XRNode.Head); bool known = head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out bool present);`.
- When presence is known and goes from false to true: `Request()`.
- While presence is known and false, and less than 8 seconds have passed since the request: do not align in any way (normal, fallback or the provisional redo), and do not let the 0.3 and 1.5 second timers run (restart the request time).
- After those 8 seconds, or when presence is not known: behave exactly as today.
- Keep `Version`, `HeadHeight`, `Hold`, `Align`, `Request` and everything else as they are.

## Done when

- Every file in your part exists and you have re-read it for compile errors.
- Reply with a short summary listing anything you could not check. The coordinator compiles, runs the tests, and looks at the running app.

## Not in this brief

- A card that follows the person when they turn.
- Teaching `B` (change a topic) as its own tutorial step.
- Images, icons or animation on the labels.
- The desktop panel and the developer overlay.
