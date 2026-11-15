# Handoff: coordinator, current Quest installation verified

Written by: Codex
Reason: brief finished

## Where things stand

- main at d11b977 with existing uncommitted work preserved.
- Brief: direct request to ensure the current version is installed on Quest.

## Finished and checked

- Authorized Quest 340YC10G840Q5N connected.
- Installed package SHA256 exactly matches latest Builds/Quest/ImmerseTheBay.apk: 068cc9ba6a2dd1b666fb440eddffd358061163f243523880f3c52b00b1824fea.
- No runtime code/art inputs newer than APK. Only newer build inputs are the relay URL resource and ProjectSettings file restored by the build; both match Git with no diff.
- App process 9884 running; collection relay health healthy and USB reverse8794 restored.
- No rebuild/reinstall needed. No save modifications, provider requests, or credits spent.

## Written but not checked

- Physical headset interactions not retested in this turn.

## Half-done

- None for this request.

## Next steps, in order

1. User can use the currently installed build.

## How to run and check

- Compare local APK SHA256 to adb shell sha256sum of the path returned by pm path com.immersethebay.vr.

## Known problems

- World Labs replacement credential was rejected in the preceding turn; this install verification does not resolve that separate issue.

## Requests for the coordinator

- None for installation.
