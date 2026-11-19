# Orb audio

The app bundles two short Kenney clips for immediate offline playback. This implementation does not use Satie or need an API key.

## Assets and licence

Both original archives contain `License.txt` declaring Creative Commons Zero (CC0), permitting personal, educational and commercial use with optional attribution. Creator: Kenney / Kenney Vleugels. Licence: https://creativecommons.org/publicdomain/zero/1.0/.

| Bundled resource | Original file | Official source | Duration |
|---|---|---|---|
| `ZapOrbThrow.wav` | `Audio/knifeSlice.ogg` | https://kenney.nl/assets/rpg-audio | 0.600 s |
| `ZapOrbImpact.wav` | `Audio/impactGlass_medium_000.ogg` | https://kenney.nl/assets/impact-sounds | 0.540 s |

Files live in the existing core-owned `Assets/_Project/Scripts/Constellation/Resources/` folder. Converted with FFmpeg to mono, 48 kHz PCM; peak normalized to -3 dBFS and saved as 16-bit WAV. Timing is retained. Unity generates import metadata; none is hand-authored. Total size is 109,528 bytes. The repository already tracks WAV files with Git LFS.

SHA-256 provenance:

| Asset | Original OGG | Bundled WAV |
|---|---|---|
| Throw | `4cd96dc630bed9840c15f1dd2306da2cc56a4da26a5d3f1a03c5a7265ac5e54f` | `b6c5651f1bf0dfa8b1ada65cbcc423342897adcf81420ed2f257c9c619a02c82` |
| Impact | `9252d50bfb85edb17d6073c4a7806e10cdb9de56d3dbfc93a4b9727146d2df6d` | `f5e7541d693bc1e73828047533bee7d41e4560b52156a35c809b1ef060ca75d6` |

## Playback

- `TopicBall.CaptureRelease` plays the whoosh after a valid XRI detach, above 0.75 m/s. Canceled releases, gentle placement and draft orbs stay silent.
- `TopicBall.OnCollisionEnter` plays a glass impact at the contact point above 0.65 m/s. Each orb has a 150 ms cooldown; an orb-to-orb contact plays once.
- `OrbAudio` preloads clips in the Void scene and uses eight positional audio sources on a separate runtime root. Hiding or destroying an orb does not cut off its one-shot. Speed controls volume and pitch, within fixed limits.
- `WorldJourney.Begin` layers the glass clip over the existing procedural opening sound. The existing journey source owns both sounds, so cancellation stops both. The loaded resource is not destroyed with the generated procedural clip.
- Home throws still place ideas; linked-world throws still travel. Sound does not change either behavior or request any network generation.

## Verification

Downloaded archives, licences, PCM headers, sample peaks, FFmpeg decoding and SHA-256 values were checked. The final revision compiled in the shared Unity run: 28 tests passed, zero failed, and one optional visual test skipped across ConstellationTests, CrystalJourneyTests and WorldTravelTests (`/tmp/zap-world-travel-verified-tests.xml`). These cover interaction regressions, not an audible-quality verdict.

After the user authorized Unity verification and a Quest build, the coordinator's concurrent Android IL2CPP ARM64 build succeeded and exited 0 (`/tmp/zap-world-travel-build.log`). A duplicate launch from this chat was rejected by Unity's project lock before doing work; no second build was run. The resulting `Builds/Quest/ImmerseTheBay.apk` is 97,266,875 bytes, SHA-256 `068cc9ba6a2dd1b666fb440eddffd358061163f243523880f3c52b00b1824fea`. APK ZIP integrity, both named audio resources, ARM64 IL2CPP binary and OrbAudio/PlayThrow/PlayImpact metadata were verified inside the package. No Packages or ProjectSettings diff remained after the build.

Audible balance and Quest playback are still unverified. This chat did not install the APK.

In the existing Void scene, try a gentle release, a fast home throw, a fast linked-world throw, an orb hitting room geometry, two orbs colliding, and a canceled grab. Listen for one whoosh per fast release, one glass hit per collision, and no impact chatter at rest. Enter by X to check the opening accent, then cancel a journey immediately to check both opening layers stop. Check the Unity Console for missing-resource warnings. Rebuild/install the Quest app to hear the changes on the headset.
