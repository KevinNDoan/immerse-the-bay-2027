# Immerse the Bay

A Unity **6000.6.9f1** VR starter for **standalone Meta Quest**, based on Unity's official VR template 10.0.0.

## Open and run

1. In Unity Hub, choose **Add → Add project from disk** and select this folder.
2. Open it with **6000.6.9f1**.
3. Open `Assets/_Project/Scenes/Main.unity`.
4. Press **Play**, focus the **Game** view, and use the XR Interaction Simulator's on-screen controls to simulate the headset and controllers.

Press **Tab** to switch between FPS and device control modes. Use **[** for the left controller and **]** for the right controller.

The starter scene contains an XR Origin, controller interactions, locomotion, a teleport floor, and a grabbable cube on a pedestal. Unity's larger reference/demo scene is available at `Assets/Scenes/SampleScene.unity`.

The simulator is enabled **only in the Editor**. Configure it under **Edit → Project Settings → XR Plug-in Management → XR Interaction Toolkit**. It helps test interactions; performance and real tracking still need testing on a headset. On macOS, use a device build for actual headset testing; [Meta Horizon Link requires Windows](https://developers.meta.com/vr/documentation/unity/unity-deploy-and-test/).

## Build for Quest

Install these Unity Hub modules for this exact Editor version if setting up another machine:

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

Use **Immerse the Bay → Build Quest Development APK** to create `Builds/Quest/ImmerseTheBay.apk`. This builds the enabled scenes in the shared Build Profiles scene list, initially just `Main.unity`.

For deployment:

1. Enable your headset's **Developer Mode**, connect it with a USB data cable, and accept **Allow USB debugging** inside the headset. See [Meta's device setup instructions](https://developers.meta.com/vr/documentation/unity/unity-software-setup/).
2. Open **File → Build Profiles** and use the **Android** platform, which is already configured for Quest through OpenXR.
3. Select your headset under **Run Device** and choose **Build And Run**.

The development APK uses local debug signing. Before publishing, set your final app identifier, version, release signing, and the store's current SDK requirements.

## Project layout

| Location | Purpose |
| --- | --- |
| `Assets/_Project/Scenes` | Your game scenes; start with `Main.unity` |
| `Assets/_Project/Scripts` | Your runtime C# code |
| `Assets/_Project/Prefabs` | Your reusable objects |
| `Assets/_Project/Materials`, `Art`, `Audio` | Your content |
| `Assets/_Project/Editor` | Editor-only build tools |
| `Assets/VRTemplateAssets`, `Assets/Samples` | Unity's reference assets and interaction prefabs |
| `Assets/Settings`, `Assets/XR`, `Assets/XRI` | Rendering, OpenXR, and interaction settings |
| `Packages` | Package manifest and locked dependency versions |
| `ProjectSettings` | Shared Unity project configuration |

Move or rename Unity assets **inside the Editor** so their `.meta` files and references stay intact. Keep `.meta` files in source control. `Library`, `Temp`, `Logs`, `UserSettings`, and build outputs are ignored.

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

## Setup verification

Verified with Unity 6000.6.9f1: successful project import and script compilation, no missing scripts in `Main.unity`, no Android OpenXR validation errors, and a successful development APK build. The generated APK contains ARM64 native code and the Android VR head-tracking feature. No headset was connected, so on-device tracking, interactions, and performance are not yet verified.

[Unity's Meta Quest packages and templates](https://docs.unity.com/en-us/engine/6000.6/manual/xr/configuring-project-for/meta-quest-develop/packages)
# immerse-the-bay-2027
