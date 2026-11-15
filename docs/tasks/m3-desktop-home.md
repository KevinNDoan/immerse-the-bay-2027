# Brief: home screen, several constellations, delete (lane: core)

Updated 2026-11-14. Read `AGENTS.md` and `docs/tasks/m3-desktop-mode.md` first. Core-lane work on the branch `core/app`.

## Changed after review (2026-11-14)

- The home screen was removed at the owner's request. The app opens straight on the constellation.
- `DesktopHome.cs` became `DesktopSettings.cs`: the settings overlay only, closed at launch, opened by `DesktopSettings.Instance.Show()`.
- `Settings` and `Quit` buttons sit at the top right of the working panel in `DesktopHud.cs`. `ZAP!` is plain text again and the constellation name is not shown.
- `ConstellationLibrary`, `Open`, `CreateAndOpen` and `DeleteBall` stay in the code with no panel for switching or creating constellations.
- The sections below describe the brief as first built. Where they disagree with this list, this list wins.

## Goal

A skeleton, not a finished design. Plain panels, plain text, everything working.

- ZAP! on desktop opens on a home screen: continue the last constellation, start a new one, pick another, open settings, quit.
- A person can keep several constellations. Each saves by itself as they work. There is no "save" or "load" wording anywhere: this is a productivity app, not a game.
- A topic can be deleted.

## Reuse, do not rewrite

- `NodeStore.Load` and `NodeStore.Save` already read and write one constellation file. A constellation is still one such file.
- `ConstellationController` stays the only owner of balls, edges and saving.
- The panel helpers that exist as private methods in `DesktopHud.cs` (`CreatePanel`, `CreateText`, `CreateButton`, `CreateInputField`, `SetRect`, `SetText`, `SetActive`) move to a shared static class and are used by every desktop panel.
- `ZapTheme` for colours and font. uGUI and TextMeshPro. No sprites, no new packages, no scene or prefab files.
- `TMP_Text.enableWordWrapping` is removed in this Unity version; use `textWrappingMode = TextWrappingModes.Normal`.

## Four parts, built at the same time

- Part A: `Assets/_Project/Scripts/Constellation/ConstellationLibrary.cs` (new), `Assets/_Project/Scripts/Constellation/NodeData.cs`, `Assets/_Project/Scripts/Constellation/ConstellationController.cs`, `Assets/_Project/Scripts/Core/ZapPlatform.cs`, `Assets/_Project/Scripts/Core/ControllerActions.cs`, `Assets/_Project/Tests/World/ConstellationTests.cs`.
- Part B: `Assets/_Project/Scripts/Desktop/DesktopUi.cs` (new), `Assets/_Project/Scripts/Desktop/DesktopHome.cs` (new).
- Part C: `Assets/_Project/Scripts/Desktop/DesktopHud.cs`.
- Part D: `Assets/_Project/Scripts/Desktop/DesktopMode.cs`.
- Edit only your part's files. Code against the contract below.
- Do not write `.meta` files. Do not start Unity. Do not run git commands that change state. Skip the handoff file.

## Contract shared by all parts

`Zap.Core.ZapPlatform` gains (Part A):

```csharp
// True while a full-window menu (the home screen or settings) is open on desktop.
public static bool IsMenuOpen { get; set; }
```

`Zap.Constellation.ConstellationLibrary` (Part A), a static class:

```csharp
public sealed class Entry
{
    public string id;          // file name without extension
    public string name;
    public string path;        // full path to the .json file
    public DateTime updatedUtc;
    public int topicCount;
}
public static string Folder { get; }                       // persistentDataPath + "/constellations"
public static List<Entry> List();                          // newest first
public static Entry Find(string id);                       // null when missing
public static Entry Create(string name);                   // writes an empty constellation file and returns its entry
public static string LastOpenedId { get; set; }            // PlayerPrefs key "zap.lastConstellation"
public static Entry EnsureDefault();                       // see Part A
```

`ConstellationController` gains (Part A):

```csharp
public string ConstellationId { get; }
public string ConstellationName { get; }
public void Open(string id);                 // saves the current one, clears the scene, loads the other
public void CreateAndOpen(string name);
public void DeleteBall(TopicBall ball);
```

`Zap.Desktop.DesktopUi` (Part B), a static class with the helper methods listed under Reuse, made `public static` with the same signatures they have in `DesktopHud.cs` today.

`Zap.Desktop.DesktopHome` (Part B): `public sealed class DesktopHome : MonoBehaviour` with `public static DesktopHome Instance { get; }`, `public bool IsOpen { get; }`, `public void Show()`, `public void Hide()`.

## Part A: library, controller, platform

### `NodeData.cs`

- `ConstellationData` gains `public string name = "";` and `public string updated = "";` (UTC, ISO 8601 round-trip format). Old files without them still load.

### `ConstellationLibrary.cs`

- Files live in `Folder`, one `<id>.json` per constellation, where `id` is a new GUID in `N` format. Create the folder when needed.
- `List()` loads each file with `NodeStore.Load`, skips files that fail to load, and sorts by `updatedUtc` descending. When `updated` is empty, use the file's last write time. When `name` is empty, use `Untitled`.
- `Create(name)`: trims the name; empty becomes `Untitled`; writes an empty `ConstellationData` with that name and the current time.
- `EnsureDefault()`: returns the entry to open at startup.
  - If `LastOpenedId` names an existing file, return it.
  - Otherwise, if any constellation exists, return the newest.
  - Otherwise, if the old single file `persistentDataPath/zap-constellation.json` exists, copy it into the folder under a new id with the name `My first constellation` (leave the old file in place), and return that.
  - Otherwise create one named `My first constellation`.
- Ignore the `.bak` and `.tmp` files `NodeStore` writes next to each constellation.

### `ConstellationController.cs`

- At startup, open `ConstellationLibrary.EnsureDefault()` in place of the fixed `zap-constellation.json` path. This applies on the headset too, so existing work carries over.
- `Open(id)`: do nothing while busy, listening or in a world, or when `id` is unknown or already open. Otherwise: `Save()`; destroy every ball and connection line and clear the lists; `Select(null)`; point the save path at the new file; load it exactly as startup does; rebuild lines; set `ConstellationLibrary.LastOpenedId`; set status to `Opened <name>`.
- `CreateAndOpen(name)`: `ConstellationLibrary.Create(name)`, then `Open` it.
- `Save()` writes the current `name` and sets `updated` to now whenever it actually writes.
- `DeleteBall(ball)`: do nothing while busy, listening or in a world, or when `ball` is null. Otherwise remove every edge that touches it, remove its id from every other ball's `parentIds`, deselect it if it is current, remove it from the list, destroy its GameObject, rebuild lines, mark dirty, `Save()`, and set status to `Deleted <topic>`.
- Change nothing else.

### `ZapPlatform.cs` and `ControllerActions.cs`

- Add `IsMenuOpen`.
- In `ControllerActions`, the keyboard block is also skipped while `ZapPlatform.IsMenuOpen`.

### `ConstellationTests.cs`

Add one `[Test]` for `ConstellationLibrary`, working in a temporary folder so it never touches real data. If `Folder` cannot be redirected cleanly, add an internal overload or an `internal static string FolderOverride` used only by the test, and say so in your summary.

- `Create` then `List` returns the new entry with its name and zero topics.
- Two entries come back newest first.
- A file whose `name` is empty lists as `Untitled`.

Add one `[UnityTest]` that guards against a bug that already happened once:

- Create a GameObject, add `ConstellationController` to it, wait two frames, and destroy it. The test passes when nothing throws and no error is logged. (Unity forbids calling `ZapPlatform.IsDesktop` from a field initializer; that broke startup on every platform and no test noticed.)

## Part B: `DesktopUi.cs` and `DesktopHome.cs`

### `DesktopUi.cs`

Copy the helper methods out of `DesktopHud.cs` unchanged in behaviour, as `public static`. Do not edit `DesktopHud.cs`; Part C switches it over.

### `DesktopHome.cs`

A full-window overlay on its own `ScreenSpaceOverlay` canvas with sorting order 10, so it draws above the working panel. Background: `theme.voidColor` at 96 percent opacity. Built once in code. Open at launch.

- `Show()` activates the canvas, refreshes the lists, and sets `ZapPlatform.IsMenuOpen = true`. `Hide()` does the reverse. `OnDisable` sets `IsMenuOpen` false.
- Find the controller with `FindAnyObjectByType<ConstellationController>()`; it may appear a few frames late. Buttons do nothing until it exists.

Home page, a centred column 560 wide:

- `ZAP!` at 72 px. Under it at 20 px, 70 percent white: `Your ideas, in space.`
- Primary button, 64 tall: `Continue: <name>`, with a second line at 16 px: `<n> topics · edited <relative time>`. It hides the home screen. The constellation is already open, so it opens nothing.
- `New constellation`: a text field (placeholder `Name it`) with a `Create` button beside it. On submit or click: `controller.CreateAndOpen(text)`, clear the field, hide the home screen. An empty name is allowed.
- A heading `Your constellations` at 18 px, then up to six buttons for the other constellations, newest first, each showing name, topic count and relative time. Clicking one calls `controller.Open(id)` and hides the home screen. When there are none, show `No others yet` at 70 percent white.
- A row of two small buttons at the bottom: `Settings` and `Quit`. `Quit` calls `Application.Quit()`.
- Relative time: `just now` under a minute, `<n> min ago` under an hour, `<n> h ago` under a day, otherwise the date as `d MMM`.

Settings page, same column, replaces the home page while open:

- Heading `Settings`.
- `Relay address` and the value of `Zap.Net.RelayConfig.BaseUrl`.
- A button `Check connection`. It sends `GET <BaseUrl>/capabilities` with `UnityWebRequest` (timeout 3 seconds) and shows the result in plain words on three lines: worlds are `live` or `stand-in picture`; voice is `ready` or `not installed`; note writing is `on` or `off (no key)`. On failure: `Cannot reach the relay at <address>`. The JSON fields are `orbisMode` (`"live"` or `"standin"`), `speechInstalled` (bool) and `nodeAiConfigured` (bool); parse with `JsonUtility`.
- A `Back` button returning to the home page.

`ZapPlatform.IsTyping` must also be true while the name field has focus: set it each frame while the home screen is open, as `DesktopHud` does for its fields, and never set it to false while `DesktopHud`'s fields might have focus (only write `true`, and let `DesktopHud` own the false case).

## Part C: `DesktopHud.cs`

- Replace the private helper methods with calls to `DesktopUi`. Delete the private copies.
- Top left: the `ZAP!` word becomes a button. Clicking it calls `DesktopHome.Instance.Show()`. Beside it, at 20 px and 70 percent white, show `controller.ConstellationName`.
- While `ZapPlatform.IsMenuOpen`, the whole working panel canvas is hidden.
- Side panel: add a `Delete` button under `Enter world` and `Edit`. First click changes its label to `Click again to delete` for 3 seconds; a second click within that time calls `controller.DeleteBall(controller.Current)`. The label returns to `Delete` when the time runs out or the selection changes.
- Keep everything else as it is.

## Part D: `DesktopMode.cs`

- In `Install`, also add `DesktopHome` to the `[ZAP Desktop]` GameObject.
- While `ZapPlatform.IsMenuOpen`, skip all orbit, pick, drag, link and cut handling, and clear any press in progress. The camera keeps easing to its target.
- Change nothing else.

## Done when

- Every file in your part exists and you have re-read it for compile errors.
- Reply with a short summary listing anything you could not check. The coordinator compiles, runs the tests, and looks at the running app.

## Not in this brief

- Renaming or deleting a whole constellation.
- Undo.
- A home screen in the headset.
- Any visual design beyond plain panels and text.
