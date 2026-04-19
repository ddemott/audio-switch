# Core Principles

## Zero Latency
Gameplay is more important than perfect sound. Never introduce latency in the audio pipeline. All audio processing (EQ, spatial) must happen at the driver layer (e.g., Equalizer APO config swapping), not through real-time application-level DSP.

## UX Model
- **System tray icon** — primary interface; right-click context menu with profile list and "Dashboard" menu item
- **Dashboard window** — full profile management UI; opened from tray context menu or global hotkey
- **Profile switching** — instant switching via global hotkeys (e.g., Ctrl+Shift+1, 2, 3) or tray context menu selection

## What a Profile Contains (V1)
- Input device (microphone)
- Output device (headset/speakers)
- Spatial audio mode (stereo, Windows Sonic, THX, Dolby Atmos)
- Volume levels

## What a Profile Contains (V2)
- Everything in V1
- Equalizer preset (via Equalizer APO config file swapping — zero latency)

## Target Users
People who frequently switch audio contexts — Teams calls, competitive gaming (CoD, Arc Raiders), casual listening — and are tired of manually reconfiguring devices and settings each time.
