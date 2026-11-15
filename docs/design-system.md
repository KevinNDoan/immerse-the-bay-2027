# ZAP! design system brief

For the designer. Updated 2026-11-14.

This is everything you need to design the interface pieces for ZAP! in Figma, and how to hand them to the developers building it in Unity. The numbers are starting values. Overrule any of them; the section called "Fixed rules" is the only part that isn't up for change.

## 1. What ZAP! is

ZAP! is a study tool for the Meta Quest 3 headset. You stand in a dark, empty space. You say a topic out loud, and it appears in your hand as a star with a small sticky note. You throw it into the space, where it hangs. You say another topic, throw it, and draw a line between the two. Over a study session you build a constellation of everything you're learning.

Any star can be entered. Pull it up to your face and the dark space is replaced by a moving, AI-generated world of that topic, wrapped all around you. You steer the world with the controller sticks or by asking for what you want to see. Press a button and you're back in your constellation.

It is for one person studying alone. The name sets the tone: quick, playful, electric.

### The two spaces

| | The void | A world |
|---|---|---|
| What it is | The dark space where you build your constellation | The inside of one topic |
| What the user sees | Stars, lines, sticky notes, nothing else | Generated video all around them |
| Who makes the visuals | You and the developers | An AI video model (Orbis). You don't design this |
| Your work here | Almost everything in this brief | Three small pieces that sit on top of the video |

### A session, step by step

1. The user is in the void. It is empty.
2. They hold a button and say "volcanoes". A small indicator on the controller shows it is listening, then thinking.
3. A star with a sticky note reading "Volcanoes" appears in their hand.
4. They throw it. It drifts to a stop and hangs in the air.
5. Three faint stars appear near it: suggested next topics, such as "Magma", "Plate tectonics", "Pompeii". They grab one to keep it, and a line zaps into place between the two. The other two fade away.
6. They make another topic themselves. As it appears, a faint line reaches toward the existing star it relates to most. Thrown near that star, the line becomes real.
7. They hold a trigger on one star and drag to another to draw a line by hand. A small lightning bolt snaps between them and settles into a thin glowing line.
8. They bring a note close to read it. It grows and shows a short summary of the topic.
9. They pull the star to their face. The void gives way to a world of volcanoes.
10. They push a stick to move. A small marker shows the instruction is on its way; about two seconds later the world responds.
11. They press a button and return to the void. The star now carries a picture of the world they visited.

## 2. Fixed rules

These come from the product decisions and from what is comfortable in a headset.

**Look**
- White on black. No colour yet. Colour will be added later, so don't rely on it to carry meaning.
- The void is a very dark purple-black, not pure black.
- Tone is playful and electric: snappy, bright, a little cheeky. Not calm, not corporate, not sci-fi-serious.

**Headset limits**
- **No real glow effects.** The headset can't afford the effect that makes bright things bloom. Every glow, halo and spark has to be a soft image you supply, which the developers layer on top.
- **No hairlines.** Lines thinner than 2 px at the Figma scale below shimmer and vanish. Use 2 px or more.
- **Fine detail is lost.** The headset shows roughly a third of the sharpness of a phone screen held at reading distance. Favour bold shapes over delicate ones.
- **Text has a minimum size.** Never go below the minimum listed for each piece in section 5.
- **Text contrast.** White at 70% opacity or more on dark; near-black on the white note. No mid-grey text.

**Comfort**
- Nothing is ever fixed to the user's view. Every piece lives at a place in the space or on a controller.
- Nothing sits closer than 30 cm to the face.
- No large flashes. The lightning effect is small and local. Nothing flashes across the whole view, and nothing blinks faster than three times a second.

**Text is live**
- Topics, summaries and messages are typed by Unity at run time, so they can't be part of your images. You design the background and specify the text style; the developers place live text on top.
- Text that never changes, such as labels on the controls card, can be part of the image.

## 3. Scale: how Figma pixels map to the real world

Everything in the headset has a real size in centimetres. Use one rule for every piece:

**1 px in Figma = 0.5 mm in the headset.**

So a 200 × 200 px frame is a 10 × 10 cm sticky note. At this scale, a piece held at arm's length (50 cm) looks about the size your Figma frame looks at 100% zoom, so your instincts about text size carry over for anything the user holds.

Pieces that sit further away need physically larger text to look the same size, which is why minimum text sizes differ per piece in section 5.

| Zone | Distance from the user | What lives there |
|---|---|---|
| Reach | 30 to 70 cm | Anything held or grabbed: stars, notes, the controls card |
| Read | 50 cm to 2 m | Notes hanging in the constellation, messages, the world title |
| Far | Beyond 2 m | Stars and lines only. Notes fade out, so no text is ever read here |

## 4. Starting values

### Colour

| Name | Value | Used for |
|---|---|---|
| Void | `#07050D` | The background of the void |
| Void lift | `#140D26` | A soft, barely visible gradient so the void has depth |
| White | `#FFFFFF` | Star cores, lines, text on dark, icons |
| White 70% | | Secondary text on dark |
| White 40% | | Suggested stars and lines, anything not yet real |
| White 15% | | Card outlines, dividers |
| Dark glass | Void at 80% opacity | Card backgrounds that sit over a bright world |
| Paper | `#F2F0F7` | The sticky note |
| Ink | `#0B0814` | Text on the sticky note |
| Ink 60% | | Secondary text on the sticky note |

### Type

The project has **Inter Regular** ready to use. If you want other weights, or a second typeface for topics or the wordmark, choose one with a free licence (SIL Open Font License) and send the font files with your handoff.

Sizes are in Figma px for pieces at arm's length.

| Style | Size | Real size at arm's length | Used for |
|---|---|---|---|
| Caption | 16 px | 8 mm | Smallest allowed. Hints, labels |
| Body | 20 px | 10 mm | Summary on the note |
| Topic | 28 px | 14 mm | Topic on the note |
| Title | 40 px | 20 mm | Headings on cards |

Line height 1.3 for body, 1.15 for topic and title.

### Spacing, corners, strokes

- Spacing steps: 4, 8, 12, 16, 24, 32, 48 px.
- Corners: 6 px on the sticky note, 16 px on cards, fully round on pills.
- Strokes: 2 px standard, 3 px for emphasis.

## 5. The pieces to design

Each piece lists its real size, where it sits, its Figma frame, the smallest text allowed in that frame, and its states. "Live text" marks text Unity fills in.

### 5.1 Star

The star is the node: the thing the user grabs, and where lines attach. The body is a simple shape built in Unity. You supply the images layered on it.

- **Real size:** 3 cm across, with a glow reaching to about 10 cm.
- **Zone:** all three.
- **Figma:** 64 × 64 px for the core, 192 × 192 px for the glow.
- **You supply:** a soft glow, a ring, a sparkle, and the core shape if it should be anything other than a round dot (a four-point star, for example).

| State | Meaning | Starting idea |
|---|---|---|
| Idle | Hanging in the void | Bright core, soft glow, slow gentle bob |
| Pointed at | The controller is aimed at it | Ring appears, slightly larger |
| Grabbed | In the user's hand | Brighter, larger, a few sparks |
| Suggested | An AI suggestion, not yet kept | 40% opacity, dashed ring, twinkles |
| Visited | Its world has been entered | Some lasting mark, such as an orbit ring |

### 5.2 Sticky note, compact

Hangs just below its star and always turns to face the user.

- **Real size:** 10 × 10 cm.
- **Zone:** reach and read. Fades out in the far zone.
- **Figma:** 200 × 200 px.
- **Minimum text:** 20 px.
- **Content:** the topic only (live text, Topic style, up to three lines, shrinks to 20 px for long topics).
- **States:** default, pointed at, held.

### 5.3 Sticky note, expanded

The compact note grows into this when held close.

- **Real size:** 16 cm wide. About 14 cm tall without a picture, about 23 cm with one.
- **Zone:** reach.
- **Figma:** 320 px wide; 280 px tall without a picture, 460 px with.
- **Minimum text:** 16 px.
- **Content:**
  - Topic (live text).
  - Summary, up to 30 words (live text, Body style).
  - A picture slot, 280 × 158 px (16:9), showing the last view of the topic's world. Empty until the world has been visited.
  - A one-line hint that pulling the star to your face enters the world.
- **States:** without picture, with picture, summary still loading.
- **Stretching:** the note's height changes with the text, so keep the corners and edges even and tell us the corner size.

### 5.4 Suggested topic label

A suggested star has no sticky note, only a small label.

- **Real size:** about 8 × 1.5 cm.
- **Zone:** reach and read.
- **Figma:** 160 × 28 px, width follows the text.
- **Minimum text:** 16 px.
- **Content:** the suggested topic (live text, white 70%).
- **States:** shown, fading out.

### 5.5 Line

Connects two stars. Drawn by Unity as a strip between them; you supply the images it is drawn with.

- **Real size:** a 2 mm bright core with a soft halo about 8 mm wide.
- **Zone:** all three.
- **You supply:**
  - A soft line image, 64 × 16 px, that repeats along its length.
  - Three lightning-bolt strips, 512 × 64 px each, that can repeat end to end.
  - A dash pattern for suggested lines.
  - A spark, 64 × 64 px.

| State | Meaning | Starting idea |
|---|---|---|
| Being drawn | The user is dragging from a star | A jittery bolt from the star to the controller |
| Settled | A real connection | Thin, steady glowing line |
| Suggested | The app's proposed connection | Dashed, 40% opacity |
| Cut | The user sliced it | Snaps apart with sparks, gone in a fifth of a second |

### 5.6 Voice indicator

Sits on the right controller and shows what the app is doing with the user's voice.

- **Real size:** 6 cm across.
- **Zone:** reach.
- **Figma:** 120 × 120 px.
- **Minimum text:** 16 px.

| State | Meaning |
|---|---|
| Listening | The button is held and the app hears the user |
| Thinking | The app is working out the topic and its summary |
| Heard | Shows the words it understood for a moment (live text) |
| Didn't catch that | Asks the user to say it again |

### 5.7 World title card

Appears in front of the user for a few seconds when they enter a world, then fades.

- **Real size:** 60 × 15 cm, 1.5 m away.
- **Zone:** read.
- **Figma:** 1200 × 300 px.
- **Minimum text:** 48 px. The topic itself should be around 120 px.
- **Content:** the topic (live text). It sits over bright moving video, so use the dark glass background or another way to keep it readable.
- **States:** appearing, shown, fading.

### 5.8 Pending-instruction marker

The world takes about two seconds to respond to any instruction. This marker tells the user their instruction was received and is on its way, so the wait feels intended.

- **Real size:** 12 cm across, 1.5 m away, low in the view.
- **Zone:** read.
- **Figma:** 240 × 240 px, plus a pill beside it for spoken requests.
- **Minimum text:** 48 px.
- **Content:** an icon for the instruction (move forward, back, left, right; look left, right, up, down; event; spoken request). For spoken requests, the words appear in the pill (live text).

| State | Meaning |
|---|---|
| Sent | A ring fills over about two seconds |
| Landed | The world has responded; a quick pop, then gone |
| Failed | The instruction didn't go through |

### 5.9 Controls card

A reminder of the controls, attached to the left controller. The user glances down at it. Two versions are needed.

- **Real size:** 12 × 8 cm.
- **Zone:** reach.
- **Figma:** 240 × 160 px.
- **Minimum text:** 16 px.
- **Content:** a simple drawing of the two Quest controllers with labels. Labels can be part of the image.

| Control | In the void | In a world |
|---|---|---|
| Grip (side button) | Grab and throw a star | |
| Both grips on empty space | Pull the constellation; move hands apart to stretch it | |
| Trigger, tap on a star | Bring that star and its neighbours to you | |
| Trigger, hold and drag | Draw a line between stars, or cut one | |
| A button, hold | Speak a new topic | Speak a request |
| Left stick | | Move |
| Right stick | | Look |
| X button | | Trigger the suggested event |
| B button | | Leave the world |

The button assignments are the developers' current plan and may change after testing. Design the card so labels are easy to swap.

### 5.10 Messages

Short notices that appear in front of the user, in either space.

- **Real size:** 30 × 5 cm, 1 m away.
- **Zone:** read.
- **Figma:** 600 × 100 px, width follows the text.
- **Minimum text:** 32 px.
- **Content:** an icon and one line (live text).
- **States:** working (for example, "Opening world"), problem (for example, "Can't reach the world right now").

### 5.11 Icons

- **Figma:** 48 × 48 px frames, 2 px stroke, rounded ends, pure white.
- **Needed:** microphone, lightning bolt, enter, leave, cut, link, grab, four movement arrows, four look arrows, retry, warning, tick.

### 5.12 Wordmark

- The ZAP! wordmark, for the moment the app starts and for the pitch.
- A square app icon, 512 × 512 px.

## 6. Motion direction

The developers build the motion. These notes describe the feeling to aim for, so the images you make suit it. If you have a clear picture of any of these, a short sketch or reference video helps a lot.

| Moment | Feeling | Rough timing |
|---|---|---|
| A new star appears in the hand | Pops in with a small overshoot | A quarter of a second |
| A thrown star comes to rest | Glides to a stop, then bobs gently | Bob repeats every four seconds |
| Drawing a line | A bolt snaps across, flashes, and calms into a thin line | Snap in a tenth of a second, calm in a third |
| Cutting a line | Breaks at the cut with sparks | A fifth of a second |
| Suggested stars arriving | Twinkle in one after another | Half a second each |
| Note expanding | Unfolds from compact to expanded | A third of a second |
| Entering a world | The star rushes at the face and the world opens out from it | Under a second |
| Leaving a world | The reverse | Under a second |

Everything should feel springy, never slow or floaty.

## 7. Handoff

### Figma file

- One page per piece, named as in section 5.
- Each piece is a component, with one variant per state.
- Share the file with edit or view access; developers will read measurements from it directly.

### Exports

- **Format:** PNG with a transparent background.
- **Size:** export at 4×. If that makes the longest side larger than 2048 px, use 2× or 1×, whichever is the largest that fits.
- **Glow, spark and line images:** square or strip sizes that are powers of two after export (256, 512, 1024).
- **Colour:** draw icons, glows, sparks and lines in pure white on transparent. Developers set their brightness in Unity. The sticky note keeps its paper colour.
- **Stretching pieces** (expanded note, cards, pills): keep corners and edges uniform, and note the corner size in px beside the component.
- **No live text in exports.** Hide topic, summary and message text before exporting, and leave the text style visible in the Figma file.

### Names and folders

Lowercase, underscores, piece then state:

```
note_compact.png
note_compact_held.png
note_expanded.png
star_glow.png
star_ring.png
line_bolt_01.png
voice_listening.png
icon_mic.png
```

Send a zip, or add the files to the project yourself:

```
Assets/_Project/Art/UI/      notes, cards, icons, voice indicator
Assets/_Project/Art/FX/      glows, sparks, line images
Assets/_Project/Art/Fonts/   any font files you add
```

### Order of work

1. **First:** star images, compact note, line images, voice indicator. These unlock the constellation.
2. **Next:** expanded note, suggested topic label, icons, void controls card.
3. **Then:** world title card, pending-instruction marker, world controls card, messages, wordmark.

## 8. What you don't need to design

- **Worlds.** They are generated live.
- **3D models.** Shapes are built in Unity, or taken from free asset-store packs. If you think a specific model would help, say which.
- **Sound.** Coming later.
- **Colour.** Coming later.
- **Hand tracking.** Controllers only for now.

## 9. Questions for you

1. **The note:** white paper with dark text (the current default, because it reads instantly as a sticky note), or a dark note with a white outline and white text (calmer on the eyes in a dark space)?
2. **The star:** a round glowing dot, or a drawn star shape?
3. **Visited stars:** what mark says "you've been inside this one"?
4. **Typeface:** is Inter right for a playful, electric product, or should topics and the wordmark use something with more character?
5. **The bolt:** how cartoonish should the lightning be?
6. **Anything missing?** If a moment in the session in section 1 has no piece listed, tell us.

## 10. Delivered from Figma

Updated 2026-11-14. Source: the "ZAP! UI" Figma file, page "Export (Unity files)". Every piece in section 5 has a page there, with one component variant per state.

All 59 images are exported with the export settings saved in Figma (4×, or smaller where 4× would pass 2048 px). They are PNGs with transparent backgrounds, and no live text is baked in. The only baked-in text is on the two controls cards, which section 2 allows.

### `Assets/_Project/Art/FX/` (white on transparent; set brightness in Unity)

| File | Pixels | Figma px | Use |
|---|---|---|---|
| `star_core.png` | 256 × 256 | 64 | Star core, round dot |
| `star_glow.png` | 1024 × 1024 | 256 | Soft glow behind the core, all states |
| `star_ring.png` | 512 × 512 | 128 | Pointed-at ring |
| `star_ring_dashed.png` | 512 × 512 | 128 | Suggested ring |
| `star_sparkle.png` | 256 × 256 | 64 | Single twinkle |
| `star_sparks.png` | 512 × 512 | 128 | Grabbed spark burst |
| `star_orbit.png` | 1024 × 1024 | 256 | Visited mark: tilted orbit ring with a moon |
| `line_soft.png` | 256 × 64 | 64 × 16 | Settled line; repeats along its length |
| `line_dash.png` | 256 × 64 | 64 × 16 | Suggested line; repeats |
| `line_spark.png` | 256 × 256 | 64 | Spark when a line is drawn or cut |
| `line_bolt_01..03.png` | 2048 × 256 | 512 × 64 | Bolt strips for a line being drawn; tile end to end |

### How the star layers combine (Figma page "Star", frame "Star (state preview)")

Sizes are relative to a 256 px state frame (the glow at 100%).

| State | Layers, bottom to top | Scale and opacity |
|---|---|---|
| Idle | glow, core | glow 256, core 64 |
| Pointed at | glow, ring, core | glow 269 (×1.05), ring 128, core 70 (×1.1) |
| Grabbed | glow, sparks, core | glow 307 (×1.2), sparks 166 (×1.3), core 83 (×1.3) |
| Suggested | glow, dashed ring, core | glow 256, ring 128, core 64; the whole star at 40% |
| Visited | glow, orbit, core | glow 256, orbit 256, core 64 |

### `Assets/_Project/Art/UI/`

| File | Pixels | Figma px | Use |
|---|---|---|---|
| `note_compact.png`, `_pointed`, `_held` | 928 × 928 | 232 | Compact note, three states. The 200 px note sits inside 16 px of padding for its shadow and outline |
| `note_expanded.png` | 1280 × 1120 | 320 × 280 | Expanded note, no picture |
| `note_expanded_picture.png` | 1280 × 1840 | 320 × 460 | Expanded note with the 280 × 158 picture slot |
| `note_summary_loading.png` | 1120 × 224 | 280 × 56 | Three placeholder bars while the summary loads |
| `note_hint.png` | 872 × 96 | 218 × 24 | "Pull to your face to enter" hint (fixed text) |
| `label_suggested.png` | 640 × 112 | 160 × 28 | Suggested-topic label background (pill) |
| `voice_listening.png`, `_thinking`, `_heard`, `_didnt_catch` | 480 × 480 | 120 | Voice indicator states |
| `voice_heard_pill.png` | 800 × 160 | 200 × 40 | Pill behind the heard words (live text) |
| `voice_pulse_ring.png`, `voice_spinner.png` | 480 × 480 | 120 | Animated layers: scale the pulse, spin the spinner |
| `icon_*.png` (18 files) | 192 × 192 | 48 | Icons from section 5.11 |
| `controls_void.png`, `controls_world.png` | 1504 × 800 | 376 × 200 | Controls cards, labels baked in |
| `title_card.png` | 1200 × 300 | 1200 × 300 | World title card background (dark glass), exported at 1× |
| `marker_sent.png`, `_landed`, `_failed` | 960 × 960 | 240 | Pending-instruction marker states |
| `marker_track.png`, `marker_fill.png` | 960 × 960 | 240 | Ring track, and the ring that fills over two seconds (radial fill in Unity) |
| `marker_pill.png` | 1920 × 320 | 480 × 80 | Pill for spoken requests (live text) |
| `message_working.png`, `message_problem.png` | 1200 × 200 | 600 × 100 | Message backgrounds with icon; live text goes to the right of the icon |
| `wordmark.png` | 1600 × 600 | 800 × 300 | ZAP! wordmark, exported at 2× |
| `app_icon.png` | 2048 × 2048 | 512 | App icon |

### Stretching pieces (use as 9-slice sprites)

Corner sizes follow section 4 of this brief. They were not measured again in Figma. Multiply by the export scale to get the pixel border.

| Piece | Corner | Border at export scale |
|---|---|---|
| Notes (compact and expanded) | 6 px | 24 px, plus the 16 px (64 px) shadow padding on the compact note |
| Cards and messages (`controls_*`, `title_card`, `message_*`) | 16 px | 64 px (`title_card`: 16 px at 1×) |
| Pills (`label_suggested`, `voice_heard_pill`, `marker_pill`, `message_*`) | fully round | half the height |

### What the app uses so far

Wired in on 2026-11-14 on `design/nodes`, with the core developer's agreement:

- **Star:** `star_core`, `star_glow`, `star_ring`, `star_sparks` and `star_orbit` replace the plain white ball.
  - The ring shows on the selected star.
  - The sparks show while the star is held.
  - The orbit shows once the star's world has been visited.
- **Note:** `note_compact`, `note_compact_pointed`, `note_compact_held` and `note_expanded` sit behind the topic text. On paper, the text turns Ink.
- **Line:** `line_soft` repeats along every connection.

The theme gets these images when Unity imports them, and **ZAP > Fill Theme Images** refills them. Sizes are in the theme under "Star images", "Note paper" and "Line image". If a slot is empty, that piece falls back to the old look.

Not used yet: everything else in `Art/UI/`, plus `star_ring_dashed`, `star_sparkle`, `line_dash`, `line_spark` and the bolts. The app has no suggested stars, drawn-line animation, voice indicator, markers or messages for them to go on yet.
