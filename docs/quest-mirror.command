#!/bin/bash
# Double-click in Finder, or run from Terminal. Close the video window to stop.
set -euo pipefail

export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"
export ADB="${ADB:-/Applications/Unity/Hub/Editor/6000.6.9f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb}"

fail() {
    printf '%s\n' "$1" >&2
    if [[ -t 0 ]]; then
        read -r -p 'Press Return to close.' || true
    fi
    exit 1
}

[[ -x "$ADB" ]] || fail 'ADB was not found. Set ADB to your Android SDK adb executable.'
command -v scrcpy >/dev/null || fail 'Install scrcpy first: brew install scrcpy'
model=$("$ADB" -d shell getprop ro.product.model 2>/dev/null | tr -d '\r') ||
    fail 'Connect one Quest by USB and accept Allow USB debugging inside the headset.'
[[ "$model" == *Quest* ]] || fail "The USB device is $model, not a Quest. Connect the Quest instead."

crop_args=()
display_size=$("$ADB" -d shell wm size | tr -d '\r')
if [[ "$model" == 'Quest 3S' && "$display_size" == 'Physical size: 3664x1920' ]]; then
    # The verified Quest 3S display contains two eyes side by side.
    crop_args=(--crop=1832:1920:0:0)
fi

printf 'Mirroring %s. Wear/wake the headset and open your build.\n' "$model"
printf 'Close the Quest Live View window to stop. Recording is off unless requested.\n'
exec scrcpy --select-usb --no-control --no-audio \
    --max-size=1600 --max-fps=30 --video-bit-rate=8M \
    --window-title='Quest Live View' --window-width=800 "${crop_args[@]}" "$@"
