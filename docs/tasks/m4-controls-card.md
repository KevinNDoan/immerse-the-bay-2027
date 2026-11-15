# Brief: controls card rework (lane: core)

Updated 2026-11-14. Read `AGENTS.md` and `docs/tasks/m4-start-in-front.md` first. Core-lane work on the branch `core/app`.

## Changed after review (2026-11-14)

- Compact layout has no title: status (up to three lines), level bar, hint. With a title the status was cut off.
- Status shows up to three lines in both layouts. Every text box height is `lines × font size × 0.125`.
- Defaults changed: `cardSize` (.58, .5), `cardCompactSize` (.58, .27), `cardStatusSize` .32, `cardColor` alpha .9.
- The panel sits at local z = +0.002; outline, divider and level bar at +0.001; text at 0. Sharing one depth made them flicker.
- `Zap/Glass` writes depth (`ZWrite On`), so a topic's note behind the card is hidden instead of showing through.
- Title, keys and labels do not wrap. The trigger label is `Link topics, sweep to cut`; the key column is 0.15 wide.
- If `Zap/Solid` is missing the card logs one error and stays hidden.
- `ConstellationController.OnDisable` clears `HomeFrame.Hold`.
- Where the sections below disagree with this list, this list wins.

## Goal

The controls card in the headset becomes a real card that is easy to read and easy to build on. Skeleton quality: plain shapes, theme colours, no images. The designer restyles it later.

Problems being fixed:

- Bare text with no panel: it collides with topics and would be unreadable over a bright world.
- No hierarchy: the live status is the most important line and looks like everything else.
- One auto-sized text block: the size jumps when the status changes, and long messages are cut off.
- Inside a world the full card covers the middle of the picture.
- The text is rebuilt every frame.
- The code lives inside `ConstellationController`.

## Reuse, do not rewrite

- `HomeFrame` decides where straight ahead is. The card stays fixed in the home frame.
- `Shader.Find("Zap/Solid")` for the opaque shapes. The panel uses the new see-through shader `Zap/Glass` (Part C), so a world stays visible behind the card.
- `ZapTheme` for every colour, size and distance. `TextMeshPro` (3D, not UGUI) for text, `theme.font` when it is set.
- No new packages, scenes, prefabs or materials on disk. One new shader, in Part C.

## Three parts, built at the same time

- Part A: `Assets/_Project/Scripts/Constellation/ControlsCard.cs` (new), `Assets/_Project/Scripts/Core/ZapTheme.cs`, `Assets/_Project/Tests/World/ConstellationTests.cs`.
- Part B: `Assets/_Project/Scripts/Constellation/ConstellationController.cs`.
- Part C: `Assets/_Project/Scripts/Constellation/Resources/ZapGlass.shader` (new).
- Edit only your part's files. Code against the contract below; the other parts are being written at the same time.
- Do not write `.meta` files. Do not start Unity. Do not run git commands that change state. Skip the handoff file.

## Contract shared by all parts

`Zap.Constellation.ControlsCard` (Part A):

```csharp
public sealed class ControlsCard : MonoBehaviour
{
    // Creates a GameObject "ZAP controls card" under `parent` with this component and builds the card.
    public static ControlsCard Create(Transform parent);
    // Shows or hides the whole card.
    public bool Visible { get; set; }
    // True while the compact layout (inside a world) is showing.
    public bool IsCompact { get; }
    // Call every frame. Cheap when nothing changed.
    // level is the microphone level, 0 to 1. busy is true while the app is working on a request.
    public void Show(string title, string status, bool inWorld, bool listening, float level, bool busy);
}
```

## Part A: card, theme, test

### `ZapTheme.cs`

Under `Controls card`, delete `controlsCardSize`, `controlsTextSizeMin` and `controlsTextSizeMax`. Change the default of `controlsDistance` to `1.3f` and of `controlsDropBelowEyes` to `.35f`. Add, each with a one-line tooltip:

```csharp
public Vector2 cardSize = new Vector2(.58f, .44f);          // full card, metres
public Vector2 cardCompactSize = new Vector2(.58f, .2f);    // inside a world
public float cardPadding = .03f;
public float cardTitleSize = .45f;
public float cardStatusSize = .36f;
public float cardBodySize = .28f;
public float cardHintSize = .24f;
public Color cardColor = new Color(.078f, .051f, .149f, .85f);   // lifted void, 85 percent opaque
public Color cardOutlineColor = new Color(.25f, .24f, .29f, 1f);
public float cardOutlineWidth = .004f;
public Color cardTextColor = new Color(.83f, .82f, .86f, 1f);    // off-white: pure white glares on a dark panel
public Color cardDimTextColor = new Color(.83f, .82f, .86f, .6f);
```

### `ControlsCard.cs`

Built once in code, in `Create`. All children are under the component's own GameObject, in its local space. The card faces the person: with identity rotation it is read from the negative Z side.

Shapes, each a `GameObject.CreatePrimitive(PrimitiveType.Quad)` with its collider destroyed at once:

- Panel: the card's size, at local z = +0.001 (behind the text). Material from `Shader.Find("Zap/Glass")` with `_BaseColor` = `cardColor` and `renderQueue = 2999`, so it always draws before the text. If that shader is missing, fall back to `Zap/Solid`.
- Outline: four thin strips along the card's edges, `cardOutlineWidth` wide, `Zap/Solid` in `cardOutlineColor`, at local z = +0.001. Not one big quad behind the panel: the panel is see-through.
- Divider: `Zap/Solid` in `cardOutlineColor`, 0.002 high, the card's width minus twice the padding. Full layout only.
- Level bar: `Zap/Solid` in `cardTextColor`, 0.006 high, left-aligned under the status text. Its width is `level` times the inner width while listening. While `busy` and not listening it pulses between 10 and 40 percent of the inner width (`Mathf.PingPong` on `Time.unscaledTime`). Hidden otherwise.
- Create three materials (panel, outline and divider, bar) and destroy them in `OnDestroy`.

Texts, each a `TextMeshPro` at local z = 0 with `richText = false`, `enableAutoSizing = false`, wrapping on (`textWrappingMode = TextWrappingModes.Normal`; `enableWordWrapping` does not exist in this Unity), overflow `Ellipsis`:

- Title: `cardTitleSize`, `cardTextColor`, one line, top left.
- Status: `cardStatusSize`, `cardTextColor`, up to two lines, under the title. It sits high on the card on purpose: the top is the easiest part to glance at.
- Keys column and labels column: `cardBodySize`. Keys in `cardDimTextColor`, in a column 0.14 wide; labels in `cardTextColor` in the rest. Full layout only. One text object per column, lines separated by `\n`, same line count in both.
- Hint: `cardHintSize`, `cardDimTextColor`, up to two lines at the bottom. Compact layout only.

Full layout, top to bottom inside the padding: title, status, level bar, divider, then the two columns:

| Key | Label |
|---|---|
| A | Hold and say a new topic |
| B | Hold and say a change |
| Grip | Grab and throw |
| Both grips | Pull and stretch |
| Trigger | Link two topics, sweep to cut |
| X | Enter the selected topic |

Compact layout, top to bottom: title at `cardBodySize`, status, level bar, hint. Hint text:

```
Sticks: move and look · Trigger: event
A: speak · B: edit topic · Y: return
```

Lay the pieces out from the theme values with simple arithmetic (line height = font size × 0.125 metres per line is a good start: a font size of 1 is about 0.1 metres tall). Leave nothing overlapping at the default theme values.

Placement, in world space, set whenever `HomeFrame.Version` changes or the layout switches:

- Position `(0, HomeFrame.HeadHeight - theme.controlsDropBelowEyes, theme.controlsDistance)`. Both layouts use the same centre: a lower card inside a world would strain the neck.
- Rotation `Quaternion.Euler(tilt, 0, 0)` with `tilt = Mathf.Atan2(theme.controlsDropBelowEyes, theme.controlsDistance) * Mathf.Rad2Deg`, so the face points at the person's eyes.
- Before the first alignment (`HomeFrame.Version == 0`) use a head height of 1.5.

`Show`:

- Switch layout when `inWorld` differs from `IsCompact`: resize the panel and outline strips, show and hide the pieces.
- Set a text only when the incoming string differs from the last one set (`string.Equals`, ordinal). Treat a null title as `ZAP!` and a null status as empty.
- Update the level bar every call. No allocations per call when nothing changed.

### `ConstellationTests.cs`

Add one `[UnityTest]`, `ControlsCardBuildsAndSwitchesLayout`:

- Create a parent GameObject, `ControlsCard.Create(parent.transform)`, wait one frame.
- `Show("Volcanoes", "Listening…", false, true, .5f, false)`: `IsCompact` is false.
- `Show("Volcanoes", "Opening…", true, false, 0f, true)`: `IsCompact` is true.
- `Visible = false` then `true` toggles the card's `gameObject.activeSelf`.
- Destroy the parent. The test fails on any logged error (the Unity test runner does this by default).

## Part B: `ConstellationController.cs`

- Delete the `_hud` and `_hudFrame` fields, and the bodies of `BuildHud` and `UpdateHud`.
- Add `private ControlsCard _card;`. `BuildHud` becomes `_card = ControlsCard.Create(transform);`.
- `UpdateHud` becomes:

```csharp
if (_card == null) return;
_card.Visible = ShowControlsCard;
if (!ShowControlsCard) return;
string title = _worldVisible ? _worldNode?.Data.topic : (Current != null ? Current.Data.topic : "ZAP!");
_card.Show(title, Status, _worldVisible, _voice.IsRecording, _voice.Level, _busy);
```

- Wording, in the status strings only (do not rename code or GameObjects):
  - `Hold A and say a topic. Release to write it on a ball.` becomes `Hold A and say a topic. Let go to create it.`
  - `Point at or grab a ball before holding B to edit.` becomes `Point at or grab a topic, then hold B to edit it.`
  - `Select a ball first.` becomes `Select a topic first.`
  - `That ball is no longer available.` becomes `That topic is no longer available.`
- Remove `using` lines that are no longer needed only if you are sure nothing else in the file uses them.
- Change nothing else.

## Part C: `ZapGlass.shader`

- Copy `Assets/_Project/Scripts/Constellation/Resources/ZapSolid.shader` to `ZapGlass.shader` in the same folder and name the shader `Zap/Glass`.
- Make it alpha blended: tags `"Queue"="Transparent"` and `"RenderType"="Transparent"`, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, `Cull Off`. Keep `ZTest LEqual`.
- Keep everything else, including the `_BaseColor` property and every single-pass instanced stereo macro, exactly as in `ZapSolid.shader`.
- Do not edit `ZapSolid.shader`.

## Done when

- Every file in your part exists and you have re-read it for compile errors.
- Reply with a short summary listing anything you could not check. The coordinator compiles, runs the tests, and looks at the running app.

## Not in this brief

- Rounded corners, images or icons on the card.
- Hiding or collapsing the control list after first use, and a card that follows the person when they turn. Both were suggested in review and are the owner's call.
- The developer overlay drawn with `OnGUI`.
- The desktop panel.
