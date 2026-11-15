# Live Quest view on the Mac

Connect the Quest by USB, wear/wake it, and open the installed build. Double-click
`quest-mirror.command` in Finder. The **Quest Live View** window shows the headset
display while the build runs. Close that window to stop mirroring.

From Terminal:

```sh
/Users/kevindoan/Code/immerse-the-bay/docs/quest-mirror.command
```

The launcher uses scrcpy 5.0.1 and the existing Unity Android SDK's ADB. It selects
the single USB-connected Quest. On the verified Quest 3S (3664×1920 display), it
shows one eye in its original orientation. It targets 30 fps with a maximum
dimension of 1600 pixels.

Mouse/keyboard device control and audio forwarding are disabled. Recording is off
by default. This does not require a Unity rebuild, app changes, a Meta login, or
the relay's browser page.

If the window stays blank or shows an old frame, make sure the headset is awake
and being worn, then close and reopen the mirror. If USB authorization is requested,
accept **Allow USB debugging** inside the headset. Connect only one USB Android
device when using this launcher.

This is a two-dimensional view of one eye. It does not reproduce binocular depth.
Passthrough capture and wireless casting were not tested. Other Quest models use
the full display until an appropriate crop has been verified.

References: [scrcpy installation on macOS](https://github.com/Genymobile/scrcpy/blob/master/doc/macos.md),
[video options](https://github.com/Genymobile/scrcpy/blob/master/doc/video.md).
