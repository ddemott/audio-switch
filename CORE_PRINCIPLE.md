# Core Principles

## Zero Latency
Gameplay is more important than perfect sound. Never introduce latency in the audio pipeline. All audio processing (EQ, spatial) must happen at the driver layer (e.g., Equalizer APO config swapping), not through real-time application-level DSP.

## UX Model
- **System tray icon** — primary interface; right-click menu with "Apply profile" submenu, "Show AudioSwitch", "Start with Windows", and "Exit". Tooltip shows the active profile.
- **Main window** — four-column node-link layout (Outputs / Inputs / Equalizers / Link configs) with Bezier connectors. Opened from tray left-click / double-click or the hotkey-triggered apply path.
- **Profile switching** — instant via global hotkeys (`Ctrl+Shift+1..9` auto-assigned), tray submenu, or double-click in main window.

## Profile Composition (current — component-based)
A profile is a named bundle of component ids. Components are reusable first-class entities stored in a shared library:
- **Output device** — name + deviceId + target volume
- **Input device** — name + deviceId + target volume
- **Spatial audio mode** — stereo, Windows Sonic, THX, Dolby Atmos
- **Equalizer** — 10 bands of gain (catalog + custom)

Mix and match. An equalizer can belong to many profiles without being duplicated.

## What's wired vs. what isn't (current)
- ✅ Output/input default-device switching + volume
- ✅ Profile CRUD, hotkeys, tray, theming, startup registration, minimize-to-tray
- ⏳ Equalizer — bands persist; APO wiring still to do
- ⏳ Spatial audio — controller is a stub returning Stereo

## Target Users
People who frequently switch audio contexts — Teams calls, competitive gaming (CoD, Arc Raiders), casual listening — and are tired of manually reconfiguring devices and settings each time.
