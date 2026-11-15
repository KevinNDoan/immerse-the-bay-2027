# Brief: suggested stars (lane: core)

Updated 2026-11-14. Read `AGENTS.md` and `docs/split/README.md` first. This is core-lane work on the branch `core/app`.

## Goal

When a node is selected, up to three faint stars float beside it, each labelled with a suggested related topic. Gripping one turns it into a real node connected to the selected node. The suggestions already exist in `NodeData.suggestions`; nothing shows them today.

## Files

- Part A, one file: `Assets/_Project/Scripts/Constellation/SuggestionPicker.cs` (new).
- Part B: `Assets/_Project/Scripts/Constellation/SuggestedStars.cs` (new), `Assets/_Project/Scripts/Constellation/ConstellationController.cs`, `Assets/_Project/Scripts/Core/ZapTheme.cs`, `Assets/_Project/Tests/World/ConstellationTests.cs`.
- Parts A and B are built by different tools at the same time. Never edit the other part's files.
- Do not write `.meta` files. Do not start Unity. Do not run git commands that change state.

## Part A: `SuggestionPicker`

Namespace `Zap.Constellation`. `public static class SuggestionPicker`. Pure functions, no Unity objects created, no logging.

- `public const int MaxSuggestions = 3;`
- `public static string[] Pick(NodeData node, IEnumerable<NodeData> allNodes, int max = MaxSuggestions)`
  - Returns the suggestions to show for `node`, in their original order, at most `max`.
  - Trim each suggestion. Drop empty ones.
  - Drop duplicates, comparing trimmed text ignoring case.
  - Drop any suggestion equal (trimmed, ignoring case) to `node.topic` or to the topic of any node in `allNodes`.
  - `node` null, `node.suggestions` null, or `max` <= 0: return an empty array.
  - `allNodes` null: treat as empty. Skip null entries and null topics in `allNodes`.
- `public static string[] Without(string[] suggestions, string topic)`
  - Returns `suggestions` with every entry equal to `topic` (trimmed, ignoring case) removed. Order kept.
  - `suggestions` null: return an empty array. `topic` null or empty: return a copy unchanged.
- Never return null.

## Part B: showing and keeping

### `ZapTheme.cs`

Add under a new header "Suggested topics", with these defaults:

- `public float suggestedNodeDiameter = .06f;`
- `public float suggestedDistance = .24f;` (centre of the selected node to centre of each suggested star)
- `public Color suggestedColor = new Color(.4f, .4f, .4f, 1f);` (the node shader is opaque, so "faint" is a dimmer colour)
- `public float suggestedTextSize = .2f;`
- `public Color suggestedTextColor = new Color(1f, 1f, 1f, .7f);`

Do not edit `ZapTheme.asset`. Missing fields take these defaults.

### `ConstellationController.cs`: hooks only

- `public bool InWorld => _worldVisible;`
- `public bool IsListening`: true while the voice recorder is recording or starting.
- In `Start`, after `_content` exists: add a `SuggestedStars` component to this GameObject and call `Initialize(this, _content)`.
- `ApplyText` gains an optional last parameter `Vector3? position = null`. When creating a node and `position` has a value, the new ball is created there instead of in front of the right controller. All existing callers keep working unchanged.
- `public void KeepSuggestion(TopicBall parent, string topic, Vector3 position)`:
  - Do nothing if busy, listening, in a world, `parent` is null, or `topic` is empty.
  - Remove the topic from `parent.Data.suggestions` with `SuggestionPicker.Without`, and mark the save dirty.
  - Start `ApplyText(topic, "create", parent, position)`. That path already asks the relay for the full note and adds the connection from `parent`.
- Change nothing else in this file.

### `SuggestedStars.cs`

Namespace `Zap.Constellation`. `public sealed class SuggestedStars : MonoBehaviour`. `public void Initialize(ConstellationController controller, Transform content)`.

- Source node is `controller.Current`.
- Show stars only when all hold: source is not null, source is not being grabbed, `!controller.InWorld`, `!controller.Busy`, `!controller.IsListening`, `Camera.main` is not null.
- Topics come from `SuggestionPicker.Pick(source.Data, controller.Balls.Select(b => b.Data))`. Recompute only when the source, its `suggestions` array reference, or the number of balls changes; not every frame.
- Keep a pool of `SuggestionPicker.MaxSuggestions` star objects, created once under `content`, switched on and off. Each star:
  - Root GameObject named "Suggested star" with a `SphereCollider` (radius: the larger of 0.045 and 0.75 times the star diameter) and an `XRSimpleInteractable`.
  - A child sphere primitive with its collider removed, drawn with one shared `Material` made from `Shader.Find("Zap/Solid")` and coloured `theme.suggestedColor`. Destroy that material in `OnDestroy`.
  - A child `TextMeshPro` label: the topic, `theme.suggestedTextSize`, `theme.suggestedTextColor`, `theme.font` if set, box 0.3 by 0.08, top-centre aligned, pivot top-centre, ellipsis overflow, rich text off. It sits just below the star and faces the camera.
- Layout, recomputed every frame from the source's position so the stars follow it:
  - `toCamera` = normalised direction from the source to the camera. `right` = `Cross(Vector3.up, toCamera)` normalised (use `Vector3.right` if it is near zero). `up` = `Cross(toCamera, right)`.
  - Angles: three stars at 150, 90 and 30 degrees; two at 135 and 45; one at 90.
  - Position = source position + (`right` * cos + `up` * sin) * `theme.suggestedDistance`.
- Motion: each star scales in over 0.35 seconds, the second and third starting 0.12 and 0.24 seconds later. After that its size pulses by 8 percent, about once every two seconds, out of step with the others. A hovered star is 20 percent larger.
- Read theme values every frame so Inspector changes show live.
- Keeping: when a star's `selectEntered` fires, call `controller.KeepSuggestion(source, topic, starPosition)` and hide all stars until the source or its suggestions change.
- Selecting a suggested star must not make it the controller's current node.
- No per-frame allocations once the stars are showing.

### `ConstellationTests.cs`

Add one `[Test]` covering `SuggestionPicker`:

- `Pick` keeps order, trims, caps at three, drops duplicates ignoring case, drops the node's own topic and topics that already exist as nodes.
- `Pick` returns empty for a null node and for null suggestions.
- `Without` removes a topic ignoring case and returns empty for null input.

## Done when

- Every file above exists and you have re-read each for compile errors.
- You list what you could not check. You cannot compile or run Unity; the coordinator will.
- Skip the handoff file. Reply with a short summary instead.

## Not in this brief

- Images for the stars. They use the plain sphere until the designer's images arrive.
- A suggested connection line for new nodes.
- Any change to the relay.
