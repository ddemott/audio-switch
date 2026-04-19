# AudioSwitch

A Windows 11 tool for switching audio device profiles instantly. Built for people who swap contexts all day — Teams call, gaming, music — and hate redoing default device / volume / EQ every time.

## What it does
- Define profiles as bundles of components — an output device, an input device, an equalizer preset, a spatial-audio mode. Pick and mix.
- Activate a profile by global hotkey (`Ctrl+Shift+1`, `2`, ...), tray menu, or double-click in the main window. Windows default devices and volumes flip instantly.
- Lives in the system tray with a live tooltip showing the active profile.

## Install / run
Requires .NET 8 SDK on Windows 11.

```
git clone https://github.com/ddemott/audio-switch
cd audio-switch
dotnet build
dotnet run --project src/AudioSwitch.App
```

Profiles are stored at `%APPDATA%\AudioSwitch\profiles.json`.

## Using it

**Main window** — four columns: Outputs, Inputs, Equalizers, Link configs (profiles). Select one item per column and hit **+ Save current links as config** to make a profile. Bezier curves show the links between selections.

**Tray icon** — right-click for the menu:
- **Show AudioSwitch** — opens the main window.
- **Apply profile** — submenu of saved profiles; click to switch instantly.
- **Start with Windows** — toggle HKCU auto-start. When Windows launches it, the main window stays hidden and you drive everything from the tray.
- **Exit** — fully quits.

Left-click the tray icon to open the main window.

**Closing the window** — the first time you click the window's X button, you'll be asked: minimize to tray, close the app, or cancel. Tick **Don't ask again** to remember the choice. The default is "ask each time."

**Theme** — click the Theme button (top right of the main window). System / Light / Dark.

**Equalizer** — click **+ Add equalizer** to add a flat preset or pick from the curated catalog (Music / Video conferencing / Gaming). Double-click an equalizer to edit its 10 bands. *(Note: EQ values are stored but not yet applied to audio — see roadmap.)*

**Hotkeys** — auto-assigned `Ctrl+Shift+{1..9}` when you save a link config. Global; work even when the app is minimized or another window is focused.

## Design principles
- **Zero latency.** EQ / spatial adjustments use driver-level config swaps (Equalizer APO planned), never real-time DSP in-process.
- **Graceful failure.** Corrupt `profiles.json` gets renamed to `.corrupt-{timestamp}` and the app starts clean. A partial profile apply is still marked active; failures are reported as data, not exceptions.
- **TDD for the domain.** `AudioSwitch.Core` has 99 unit tests. COM and WPF are smoke-tested.

## Roadmap
- Equalizer APO wiring (currently EQ bands persist but don't affect audio).
- Working spatial-audio controller (currently a stub).

## Documentation
- [`CORE_PRINCIPLE.md`](CORE_PRINCIPLE.md) — the why.
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — the how.
- [`CODING_STANDARDS.md`](CODING_STANDARDS.md) — binding standards for contributors.
- [`CLAUDE.md`](CLAUDE.md) — codebase orientation for AI collaborators.

## License
Not yet chosen.
