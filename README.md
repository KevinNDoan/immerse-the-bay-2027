# ZAP! — Immerse the Bay

A Unity **6000.6.9f1** study tool for **Meta Quest 3**: speak a topic into a throwable ball, edit it by voice, connect related topics, and enter an Orbis-generated world. GPT-6 Luna creates and edits notes using their parent topics; local speech recognition runs on the laptop relay.

See [voice, controls, setup and verification](docs/voice-worlds.md) for the current implementation. Development is on `zap/build`. The void visuals are a temporary placeholder for the designer's skybox.

## Open and run

1. In Unity Hub, choose **Add → Add project from disk** and select this folder.
2. Open it with **6000.6.9f1**.
3. Start the relay: `cd relay && .venv/bin/python -m zap_relay` (first-time setup is in the guide above).
4. Open `Assets/_Project/Scenes/Void.unity`.
5. Press **Play**, focus the **Game** view, and use the XR Interaction Simulator's on-screen controls to simulate the headset and controllers. The development panel supports typed topics; hold **V** to speak a new topic, or **B** to edit the selected ball.

Press **Tab** to switch between FPS and device control modes. Use **[** for the left controller and **]** for the right controller.

The app scene contains the controller rig; voice, balls, notes, saving and world playback install themselves at runtime. The original starter remains at `Assets/_Project/Scenes/Main.unity`, and Unity's larger reference/demo is at `Assets/Scenes/SampleScene.unity`.

The simulator is enabled **only in the Editor**. Configure it under **Edit → Project Settings → XR Plug-in Management → XR Interaction Toolkit**. It helps test interactions; performance and real tracking still need testing on a headset. On macOS, use a device build for actual headset testing; [Meta Horizon Link requires Windows](https://developers.meta.com/vr/documentation/unity/unity-deploy-and-test/).

## Build for Quest

Install these Unity Hub modules for this exact Editor version if setting up another machine:

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

Use **Immerse the Bay → Build Quest Development APK** to create `Builds/Quest/ImmerseTheBay.apk`. This builds `Void.unity`. Supply the laptop's LAN address through `ZAP_RELAY_URL` when building from the command line; see the setup guide. The build generates microphone and Internet permissions and permits local HTTP in this development APK.

For deployment:

1. Enable your headset's **Developer Mode**, connect it with a USB data cable, and accept **Allow USB debugging** inside the headset. See [Meta's device setup instructions](https://developers.meta.com/vr/documentation/unity/unity-software-setup/).
2. Open **File → Build Profiles** and use the **Android** platform, which is already configured for Quest through OpenXR.
3. Select your headset under **Run Device** and choose **Build And Run**.

The development APK uses local debug signing. Before publishing, set your final app identifier, version, release signing, and the store's current SDK requirements.

## Project layout

| Location | Purpose |
| --- | --- |
| `Assets/_Project/Scenes` | App scenes; start with `Void.unity` |
| `Assets/_Project/Scripts` | Your runtime C# code |
| `Assets/_Project/Prefabs` | Your reusable objects |
| `Assets/_Project/Materials`, `Art`, `Audio` | Your content |
| `Assets/_Project/Editor` | Editor-only build tools |
| `Assets/VRTemplateAssets`, `Assets/Samples` | Unity's reference assets and interaction prefabs |
| `Assets/Settings`, `Assets/XR`, `Assets/XRI` | Rendering, OpenXR, and interaction settings |
| `Packages` | Package manifest and locked dependency versions |
| `ProjectSettings` | Shared Unity project configuration |

Move or rename Unity assets **inside the Editor** so their `.meta` files and references stay intact. Keep `.meta` files in source control. `Library`, `Temp`, `Logs`, `UserSettings`, and build outputs are ignored.

## Git and large assets

Install [Git LFS](https://git-lfs.com/) on every development machine before cloning or adding media. On macOS with Homebrew:

```sh
brew install git-lfs
git lfs install
```

After cloning, run `git lfs pull` in the project folder to ensure the full asset files are downloaded before opening Unity. Commit and push normally; Git LFS uploads the asset contents through its pre-push hook.

`.gitattributes` routes models, textures, audio, video, fonts, and binary baked lighting data through LFS. Scripts, scenes, prefabs, materials, `.meta` files, and other serialized Unity settings stay in ordinary Git. Commit `.gitattributes`, `.gitignore`, `Assets`, `Packages` (including the lockfile), and `ProjectSettings` together.

Use `git lfs ls-files` to inspect managed assets and `git lfs status` before committing. New media with a listed extension is handled automatically. If you add an LFS rule for an already tracked file type, run `git add --renormalize` on those files to stage their conversion. This does not rewrite earlier commits, which retain their original asset copies.

## Foundation

- Universal Render Pipeline (URP), Linear color, 4× MSAA, mobile performance quality settings.
- OpenXR with Meta Quest support and Touch / Touch Pro / Touch Plus controller profiles.
- XR Interaction Toolkit and the Input System.
- Android, Vulkan, IL2CPP, ARM64, ASTC textures, single-pass instanced/multiview rendering.
- Android minimum API 29; target API 34, inherited from the VR template.
- App identifier: `com.immersethebay.vr`; version: `0.1.0`.
- Force Text asset serialization and Visible Meta Files.

The template also includes XR Hands and supporting XR packages for the reference demos. `Main.unity` starts with controllers. Package versions are pinned in `Packages/manifest.json` and `Packages/packages-lock.json`; keep both files together when upgrading. Sample folders retain the version labels shipped in Unity's template.

## Command-line build on this Mac

Close the Editor for this project before running:

```sh
/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -buildTarget Android \
  -executeMethod ImmerseTheBay.Editor.QuestBuild.BuildDevelopment \
  -logFile /tmp/immerse-quest-build.log
```

For XR configuration changes, check **Edit → Project Settings → XR Plug-in Management → Project Validation → Android** before building.

## Original starter verification

Verified with Unity 6000.6.9f1: successful project import and script compilation, no missing scripts in `Main.unity`, no Android OpenXR validation errors, and a successful development APK build. The generated APK contains ARM64 native code and the Android VR head-tracking feature. No headset was connected, so on-device tracking, interactions, and performance are not yet verified.

[Unity's Meta Quest packages and templates](https://docs.unity.com/en-us/engine/6000.6/manual/xr/configuring-project-for/meta-quest-develop/packages)
# immerse-the-bay-2027
