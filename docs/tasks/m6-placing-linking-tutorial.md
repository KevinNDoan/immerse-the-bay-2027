# Brief: topics stay put, toss to delete, easier linking, shorter tutorial (lane: core)

Updated 2026-11-14. Read `AGENTS.md` and `docs/tasks/m5-onboarding.md` first. Core-lane work on the branch `core/app`.

## Goal

Feedback from the headset:

1. The tutorial is far too long, and its wording is not friendly.
2. Linking two topics is hard.
3. A topic keeps drifting after it is put down, which makes arranging things annoying.
4. Tossing a topic into the void should delete it.

What changes:

- A topic that is let go slowly stays exactly where it was let go and cannot be nudged afterwards.
- A topic that is thrown (2 m/s or faster at release) flies off, shrinks away and is deleted.
- Linking gets two easier ways: tap two held topics together, or aim roughly at one, hold the trigger and drag roughly to another. A preview line and a larger target show what will be linked.
- The tutorial is four short steps.

This replaces the earlier rule that a thrown topic slows down and hangs where it stops.

## Reuse, do not rewrite

- `ConstellationController.ToggleLink`, `DeleteBall`, `CutAcross` and the line material stay the owners of links, deletion and cutting.
- `XRGrabInteractable` keeps doing the grabbing. Do not replace it.
- `ZapTheme` for look values. No new packages, shaders, scenes, prefabs or materials on disk.

## Three parts, built at the same time

- Part A: `Assets/_Project/Scripts/Constellation/TopicBall.cs`, `Assets/_Project/Scripts/Constellation/ConstellationController.cs`, `Assets/_Project/Tests/World/ConstellationTests.cs`.
- Part B: `Assets/_Project/Scripts/Constellation/Onboarding.cs`.
- Part C: `Assets/_Project/Scripts/Constellation/LinkAim.cs` (new).
- Edit only your part's files. Code against the contract below; the other parts are being written at the same time.
- Do not write `.meta` files. Do not start Unity. Do not run git commands that change state. Skip the handoff file.
- No allocations per frame once running.

## Contract shared by all parts

`Zap.Constellation.LinkAim` (Part C):

```csharp
public static class LinkAim
{
    // Index of the position closest in angle to the ray, among those within maxAngleDegrees of it and within maxDistance of origin. -1 when none.
    // skip is an index to ignore, or -1. Positions closer than 0.05 m to origin are ignored.
    public static int Nearest(IReadOnlyList<Vector3> positions, Vector3 origin, Vector3 direction, float maxAngleDegrees, float maxDistance, int skip);
}
```

`Zap.Constellation.TopicBall` gains (Part A):

```csharp
public const float TossSpeed = 2f;                    // metres per second at release
public bool IsTossed { get; }                         // flying away, about to be deleted
public bool Highlighted { get; set; }                 // link target preview: drawn larger
public event Action<TopicBall> Tossed;                // raised once, when the fade-out has finished
public static bool IsToss(Vector3 releaseVelocity);   // magnitude >= TossSpeed
public void Release(Vector3 velocity);                // place it or toss it. Public for tests.
```

`Zap.Constellation.Onboarding` (Part B): `StepCount` becomes 4. `Middle` and `Done` keep their signatures with these rules, 0-based:

| Step | Middle | Done |
|---|---|---|
| 0 | | `now.Topics > start.Topics` |
| 1 | `now.Holding` | `sawMiddle && !now.Holding` |
| 2 | | `now.Topics > start.Topics` |
| 3 | | `now.InWorld` |

## Part A: placing, tossing, linking, tests

### `TopicBall.cs`

- At rest (not held, not tossed) the body is frozen: `Body.constraints = RigidbodyConstraints.FreezeAll`, linear and angular velocity zero. Set this in `Initialize` too.
- `OnGrab`: `Body.constraints = RigidbodyConstraints.None`.
- `OnRelease`: remember `Time.frameCount`. XR Interaction Toolkit applies the throw velocity later in that frame, so decide on a later frame: in `Update`, when a release is pending, the frame count is higher, and the ball is not held again, call `Release(Body.linearVelocity)` and clear the pending flag.
- `Release(velocity)`:
  - Not a toss: zero both velocities, `FreezeAll`.
  - A toss: `IsTossed = true`; constraints `None`; `Body.linearVelocity = velocity`; disable the `XRGrabInteractable` and the `SphereCollider`; hide the note; shrink the orb to nothing over 0.5 seconds (unscaled time); then raise `Tossed` once. Do nothing if already tossed.
- `Highlighted`: while true the orb is drawn at `nodeSelectedDiameter × 1.25`.
- `LateUpdate`: while tossed, only the shrink runs; skip the note and the pull-to-face check.
- Keep the 8 m/s clamp in `FixedUpdate`. Remove the "stop when very slow" line: a resting ball is frozen now.

### `ConstellationController.cs`

- Tossed topics: in `CreateBall` subscribe `ball.Tossed`. The handler adds the ball to a `List<TopicBall>` of pending deletions. In `Update`, when `!_busy && !IsListening && !InWorld`, call `DeleteBall` for each pending ball and clear the list. A tossed ball is ignored by `AnyBallHeld`, the two-grip pull, and every link search.
- Tap to link: when exactly two balls are held and their centres are closer than 0.15 m, call `ToggleLink` once for that pair. Do not fire again until fewer than two are held or the two are further apart than 0.22 m.
- Aim and drag to link, replacing the exact-hover rule in `UpdateConstellationControls`:
  - Keep a reusable `List<Vector3>` of ball positions, filled only on frames where the right trigger is held (tossed balls get a position far away, such as `Vector3.one * 9999f`, so indices still match `_balls`).
  - Trigger pressed: the start is `Current` when it is hovered, else `LinkAim.Nearest(positions, _right.position, _right.forward, 10f, 6f, -1)`. With a start, begin a link drag. With none, begin a cut sweep exactly as today.
  - While dragging: the candidate is `LinkAim.Nearest(positions, _right.position, _right.forward, 14f, 6f, <start index>)`. Only the candidate has `Highlighted = true`. A preview `LineRenderer` (created once in `Start`, same material as the connection lines, twice their width, disabled when idle) runs from the start ball to the candidate, or with no candidate to the point on the controller's ray at the start ball's distance.
  - Trigger released: with a candidate, `ToggleLink(start, candidate)`. Clear the highlight and hide the preview line either way.
- Wording (headset strings only; leave the desktop branches):
  - After creating: `<topic> is ready. Grab it to move it. Throw it away to delete it.`
  - `ToggleLink`: `Linked <from> and <to>.`, `Link removed.`, and for a loop `Those two are already connected another way.`
- Change nothing else. `HomeFrame.Hold`, the card, onboarding and saving stay as they are.

### `ConstellationTests.cs`

- Replace `GrabReleaseTransfersThrowVelocityAndBallFloats` with `ReleasedTopicStaysPutAndTossedTopicIsRemoved`: create a `TopicBall` as that test does (no interactor needed). `Release(new Vector3(0, 0, .5f))`: `IsTossed` is false, the body's constraints are `FreezeAll`, its velocity is zero, and after 0.3 seconds its position has not changed. On a second ball `Release(new Vector3(0, 0, 3f))`: `IsTossed` is true and the `Tossed` event fires within 1.5 seconds.
- Add `[Test] TopicBallTossThreshold`: `IsToss` is false at 1.9 m/s and true at 2.0 m/s.
- Add `[Test] LinkAimPicksTheNearestWithinTheCone`: with positions straight ahead at 1 m, 5 degrees off at 2 m and 30 degrees off at 1 m, a 14 degree cone returns the first; skipping the first returns the second; a 2 degree cone that skips the first returns -1; a position beyond `maxDistance` is ignored.
- Update `OnboardingRulesAdvanceOnTheRightActions` to the four-step table in the contract.

## Part B: `Onboarding.cs`

Four steps. Keep everything else in the file as it is: the hold-to-skip and hold-to-restart rules, the stored preference, the cached hint arrays, the public API.

| Step | Title | Instruction | Controls labelled, all highlighted |
|---|---|---|---|
| 0 | Say a topic | Hold A and say a topic, like 'volcanoes'. Let go when you're done. | RightA `Hold and speak` |
| 1 | Move it | Squeeze the grip to pick it up. Let go to place it. Throw it to delete it. | RightGrip `Grab`, LeftGrip `Grab` |
| 2 | Connect ideas | Say another topic. To link or unlink two, tap them together. | RightA `Hold and speak` |
| 3 | Step inside | Aim at a topic and press X to step inside. Press Y to come back. | LeftX `Step inside`, LeftY `Come back` |

- `StepCount = 4`. Titles are `<n> of 4 · <title>`.
- Remove the rules and texts of the old steps (cut, link, move everything, look around) and the "skip the cut step when there is no link" special case.
- On finishing: `controller.SetStatus("That's it! Look at a controller to see its buttons.")`.
- Void hints shown outside the tutorial become: RightA `Hold: new topic`, RightB `Hold: change topic`, RightGrip `Grab · throw to delete`, LeftGrip `Grab · throw to delete`, RightTrigger `Aim and drag: link`, LeftX `Step inside`, LeftStick `Hold: tutorial`. World hints stay as they are.
- Use plain apostrophes, not curly quotes: the font may not have them.

## Part C: `LinkAim.cs`

- Implement the contract. Normalise `direction`; return -1 when it is zero.
- For each position: skip the `skip` index, anything nearer than 0.05 m or further than `maxDistance`; compute the angle between `direction` and the vector to it; keep the smallest angle that is at most `maxAngleDegrees`.
- No allocations.

## Done when

- Every file in your part exists and you have re-read it for compile errors.
- Reply with a short summary listing anything you could not check. The coordinator compiles, runs the tests, and looks at the running app.

## Not in this brief

- Undo for a tossed topic.
- Changing how a link is cut.
- Any change to the card, the controller labels' look, or the home frame.

## Changed after review (2026-11-14)

Where this section disagrees with the sections above, this section wins.

- `TopicBall.TossSpeed` is `2.5f`.
- The release speed is the topic's own movement, not the rigidbody's velocity: while held, `TopicBall` samples its position every `Update`; on release it takes the displacement over about the last 0.1 seconds divided by that time. A physics contact between release and the next frame can no longer turn a placement into a toss.
- `Tossed` is raised at once, at the start of the fly-away, not after the shrink. The controller removes the topic from the constellation (links, parents, selection, save) in that handler, with no wait for `_busy`. The `TopicBall` shrinks for 0.5 seconds and then destroys its own GameObject. Nothing tossed is ever saved.
- A voice request that was in flight for a topic that has since been removed does nothing to it: an edit reports `That topic is no longer available.`; a new topic simply gets no link to the removed parent.
- A link drag starts only from the right controller's ray, within 6 degrees of a topic. The target cone stays 14 degrees. A cut sweep starts anywhere else.
- Tap to link arms only after the two held topics have been more than 0.22 m apart while both are held, and is off while a trigger drag is active.
- Whenever the constellation controls are skipped for a frame (busy, recording, in a world) and on entering a world, any link drag is cancelled and the trigger state is reset.
- `LinkAim.Nearest` returns -1 for a direction that is zero or not a number.
- Fixed after a headset try: steps 0 and 2 no longer compare topic counts. A topic thrown away just after step 2 began lowered the count, so one new topic never finished the step. `TutorialFacts` gained `Created` and `LinkChanges`, fed by `ConstellationController.TopicsCreated` and `LinkChanges`, which only go up. Step 2 finishes on a new topic or on a link gesture.
