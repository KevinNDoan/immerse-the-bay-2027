# Designer: how the constellation looks

Updated 2026-11-14. Read [README.md](README.md) first. Your branch is `design/nodes`.

## Your job

Decide and deliver how everything in the constellation looks: the nodes, the lines between them, the notes, the spacing, and the on-screen text. The full brief, with sizes and every piece to design, is [docs/design-system.md](../design-system.md).

## What you own

- `Assets/_Project/Art/` and everything under it
- `Assets/_Project/Materials/` and `Assets/_Project/Prefabs/`
- `docs/design-system.md`
- This page

## What you do not touch

- Any `.cs` code file. If the look needs a code change, ask the core developer.
- Scenes (`.unity` files), `ProjectSettings/`, `Packages/`.
- Anything under `Assets/_Project/Scripts/` or `relay/`.

## What exists today

The app runs on the headset, with a placeholder look written directly in code. These are the values you will be replacing:

| Piece | Today |
|---|---|
| Node | Plain white ball, 11 cm across; 12.5 cm when selected |
| Topic text | White text 9 cm below the ball, always facing the user |
| Summary | Appears under the topic when the node is held, or selected and closer than 85 cm |
| Text visibility | Hidden when the node is more than 4 m away |
| Line between nodes | Plain white line, 2.5 mm wide |
| Controls card | White text block, 50 × 32 cm, floating near the left controller, never closer than 60 cm to the face |
| Background | Flat dark purple-black, `#07050D` |

After Step 0, all of these live in one theme asset, `Assets/_Project/Art/Theme/Resources/ZapTheme.asset`. You change the look by editing that asset in Unity's Inspector and by adding images to your folders.

## Two ways to work

**Figma only.** Design in Figma, export images, and put them in the folders below. You never open Unity.
- Tell the core developer when you add files. They open Unity once, which creates the matching `.meta` files, and commit those for you.
- Send the numbers you want (sizes, spacing, text sizes) as a short list. The core developer enters them in the theme.

**Figma and Unity.** As above, and you also open the project in Unity 6000.6.9f1.
- Select `ZapTheme` in `Assets/_Project/Art/Theme/Resources/` and change values in the Inspector.
- Press Play in the `Void` scene to see the result. Do not save the scene.
- Commit each new image together with the `.meta` file Unity creates next to it.

## Where files go

```
Assets/_Project/Art/UI/      notes, cards, icons, voice indicator
Assets/_Project/Art/FX/      glows, sparks, line images
Assets/_Project/Art/Fonts/   any font files you add
Assets/_Project/Art/Models/  any 3D models from free asset packs
Assets/_Project/Art/Theme/   the theme asset
```

File names are lowercase with underscores, piece first, then state: `note_compact.png`, `star_glow.png`, `line_bolt_01.png`. Export rules (format, sizes, what stays live text) are in section 7 of the design brief.

The core developer creates these folders in Step 0. If you need another folder, ask; do not create it yourself, because two people creating the same folder causes a conflict.

## Order of work

1. Node images (glow, ring, sparkle), compact note, line images, voice indicator.
2. Expanded note, suggested-topic label, icons, the controls card for the constellation.
3. World title card, pending-instruction marker, the controls card for worlds, messages, wordmark.

Push after each group so it can be wired in while you continue.

## Asking for something the theme cannot do

Send the core developer a short message with:
- the piece and the state it is for (for example "node, visited"),
- the file names you added,
- the sizes or spacing you want.

## Before you open a pull request

- Only files under `Assets/_Project/Art/`, `Materials/`, `Prefabs/` and `docs/design-system.md` are changed.
- Every image you added in Unity has its `.meta` file next to it. If you worked in Figma only, say so in the pull request.
- No scene or settings files are in the list. If Unity changed some by itself, undo them with the command in the README.
