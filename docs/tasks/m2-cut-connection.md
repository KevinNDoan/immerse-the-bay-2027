# Brief: cut a connection (lane: core)

Updated 2026-11-14. Read `AGENTS.md` first. Core-lane work on the branch `core/app`.

## Goal

Hold the right trigger while pointing at empty space and sweep the controller's pointer across a connection line: that line is cut. One cut per trigger press. Pressing the trigger while pointing at a ball still starts a link, as today.

## Three parts, three tools, one file each

- Part A: `Assets/_Project/Scripts/Constellation/LineCutter.cs` (new).
- Part B: `Assets/_Project/Scripts/Constellation/ConstellationController.cs`.
- Part C: `Assets/_Project/Tests/World/ConstellationTests.cs`.
- Edit only your part's file. The parts are written at the same time; code against the signature below.
- Do not write `.meta` files. Do not start Unity. Do not run git commands. Skip the handoff file.

## Part A: `LineCutter`

Namespace `Zap.Constellation`. `public static class LineCutter`. Pure geometry: `using UnityEngine;` only, no objects created, no logging, no allocations.

```csharp
public static bool SweepCrosses(
    Vector3 previousOrigin, Vector3 previousDirection,
    Vector3 currentOrigin, Vector3 currentDirection, float range,
    Vector3 lineStart, Vector3 lineEnd, out float distanceFromOrigin)
```

- Last frame the pointer was the ray from `previousOrigin` along `previousDirection`; this frame it is the ray from `currentOrigin` along `currentDirection`. Both the hand moving and the hand turning sweep the pointer.
- The swept area is the four-sided shape with corners `P0 = previousOrigin`, `P1 = previousOrigin + previousDirection.normalized * range`, `C1 = currentOrigin + currentDirection.normalized * range`, `C0 = currentOrigin`. Test it as two triangles: `(P0, P1, C1)` and `(P0, C1, C0)`.
- Return true when the segment `lineStart` to `lineEnd` passes through either triangle. Set `distanceFromOrigin` to the distance from `currentOrigin` to the crossing point. If both triangles are crossed, use the smaller distance.
- Otherwise return false and set `distanceFromOrigin` to 0.
- Return false when `range` <= 0 or either direction is near zero length.
- A triangle with near-zero area, or one whose plane is parallel to the segment, is simply not crossed. It must not stop the other triangle being tested.
- Use a segment-triangle intersection (Moller-Trumbore, with the segment parameter limited to 0..1). Points exactly on a triangle's edges count as inside.
- Keep one private helper for the single-triangle test.

## Part B: the gesture in `ConstellationController`

All changes are in `UpdateConstellationControls` and its fields, plus one new method and one text change.

- New fields: `private bool _cutting;` `private Vector3 _cutOrigin;` `private Vector3 _cutDirection;` `private const float CutRange = 4f;`
- When the right trigger goes down (`_input.RightTrigger && !_trigger`):
  - `bool onBall = Current != null && Current.Grab.isHovered;`
  - `_lineStart = onBall ? Current : null;`
  - `_cutting = !onBall && _right != null;` and when cutting, `_cutOrigin = _right.position;` and `_cutDirection = _right.forward;`
- While `_cutting`, the trigger is held and `_right` is not null:
  - `direction = _right.forward`.
  - Over all edges `i` whose `_lines[i].enabled` is true, call `LineCutter.SweepCrosses(_cutOrigin, _cutDirection, _right.position, direction, CutRange, _lines[i].GetPosition(0), _lines[i].GetPosition(1), out float distance)` and remember the index with the smallest `distance`.
  - Then set `_cutOrigin = _right.position` and `_cutDirection = direction`.
  - If an edge was crossed: call `CutEdge(_edges[index])` and set `_cutting = false`. Do this after the loop, never inside it.
- When the trigger is released: `_cutting = false`. The existing link behaviour stays: on release, if `_lineStart != null && Current != null && Current != _lineStart`, call `ToggleLink(_lineStart, Current)`. Then set `_lineStart = null`.
- `_trigger = _input.RightTrigger;` stays last.
- New `private void CutEdge(NodeEdge edge)`: find the two balls by `edge.from` and `edge.to`. If both exist, call `ToggleLink(from, to)` (the edge exists, so this removes it and updates parent links). If either is missing, remove the edge from `_edges`, set `_dirty = true`, call `RebuildLines()` and `Save()`. Then `SetStatus("Connection cut.")`.
- In `UpdateHud`, the constellation controls text gains a third line: `Trigger: link two balls · sweep a line to cut`.
- Change nothing else in the file.

## Part C: test

Add one `[Test]` named `LineCutterDetectsSweepAcrossLine` to the existing `ConstellationTests` class.

Turning the pointer, with previous and current origin both `(0,0,0)`, previous direction `(-0.2, 0, 1)`, current direction `(0.2, 0, 1)`, range 4:

- A vertical line from `(0, -0.5, 1)` to `(0, 0.5, 1)`: true, distance within 0.01 of 1.
- The same line moved to z = 10: false (beyond range).
- A vertical line from `(2, -0.5, 1)` to `(2, 0.5, 1)`: false (outside the sweep).
- A vertical line from `(0, 0.2, 1)` to `(0, 0.5, 1)`: false (the segment ends above the sweep plane).

Moving the hand without turning it, with previous origin `(-0.2, 0, 0)`, current origin `(0.2, 0, 0)`, both directions `(0, 0, 1)`, range 4:

- A vertical line from `(0, -0.5, 1)` to `(0, 0.5, 1)`: true, distance within 0.01 of 1.02 (the crossing point is `(0, 0, 1)`, measured from the current origin).

No movement at all:

- Identical origins and identical directions: false.
- Range 0: false.

In every false case, `distanceFromOrigin` is 0.

## Not in this brief

- Any visual for the cut (sparks, a blade). That waits for the designer's images.
- Changes to linking.
