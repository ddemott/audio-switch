# Architecture

## Tech Stack
- **Language:** C# 12 / .NET 8 (`net8.0-windows`)
- **UI:** WPF
- **Audio API:** Windows Core Audio (MMDevice / PolicyConfig COM) via `AudioSwitch.Audio`
- **Storage:** JSON (`System.Text.Json`) at `%APPDATA%/AudioSwitch/profiles.json`, or next to the exe when `portable.flag` is present
- **Tray:** `Hardcodet.NotifyIcon.Wpf`
- **Hotkeys:** `RegisterHotKey` / `WM_HOTKEY` via P/Invoke
- **Startup:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

## Projects

```
src/
  AudioSwitch.Core/     # Platform-agnostic domain + interfaces (no WPF, no COM)
  AudioSwitch.Audio/    # COM / PolicyConfig implementations of Core interfaces
  AudioSwitch.App/      # WPF shell, tray host, views, composition root
tests/
  AudioSwitch.Core.Tests/   # xUnit + in-memory fakes (99 tests)
  AudioSwitch.Audio.Tests/  # empty scaffold; COM is not unit-tested
```

### AudioSwitch.Core — domain
- **Models:** `Component` (abstract) + `OutputDeviceComponent`, `InputDeviceComponent`, `EqualizerComponent`, `SpatialAudioComponent`; `AudioProfile` (references components by id); `ComponentLibrary`; `ProfileStoreData`; enums `ThemePreference`, `WindowCloseBehavior`, `CloseAction`, `SpatialAudioMode`.
- **Services:** `ProfileStore` (JSON load/save + quarantine on corruption; `DefaultFilePath(baseDir)` branches between APPDATA and the exe directory), `ProfileManager` (CRUD + `ApplyProfile`), `ProfileApplier` (per-step `TryStep` orchestration), `HotkeyParser`, `HotkeyRegistrar`, `EqualizerPresets`, `CloseBehaviorResolver`, `StartupRegistrationService` (owns `--startup` arg + `IsStartupLaunch`), `PortableMode` (probes for `portable.flag` beside the exe).
- **Interfaces:** `IProfileStore`, `IProfileManager`, `IAudioDeviceService`, `IVolumeController`, `ISpatialAudioController`, `IHotkeyService`, `IRegistryStore`.

### AudioSwitch.Audio — platform
Implementations that touch COM: `CoreAudioController` (MMDevice + `IPolicyConfig` for default-device switching), `VolumeController`, `SpatialAudioController` (stub). These may throw HRESULT exceptions; the applier's `TryStep` converts them to data.

### AudioSwitch.App — shell
- **`App.xaml.cs`** — composition root. Reads command-line args (`--startup` suppresses window), builds `ThemeService`, `MainWindow`, `HotkeyService`, `AppHost`, `TrayIconHost`.
- **`Composition/AppHost.cs`** — holds all services, exposes settings helpers (`SetThemePreference`, `SetCloseBehavior`, `SetStartWithWindows`), reconciles hardware on launch. Exposes `IsPortable` (read once from `PortableMode.IsActive(exeDir)`); when portable, `SetStartWithWindows` is a no-op and `IsStartWithWindowsEnabled` returns false.
- **`Services/ThemeService.cs`** — System / Light / Dark resource-dictionary swap.
- **`Services/HotkeyService.cs`** — `WM_HOTKEY` hook via `HwndSource`.
- **`Services/TrayIconHost.cs`** — `TaskbarIcon` owner. Tooltip bound to active profile. Context menu: Show, Apply profile submenu, Start with Windows (checkable), Exit. Also handles the close-prompt flow when `MainWindow.Closing` fires.
- **`Services/WindowsRegistryStore.cs`** — `Microsoft.Win32` impl of `IRegistryStore`.
- **`Controls/ListBoxAssist.cs`** — attached `SelectedBorderBrush` property used by `MainWindow.xaml` row styles (avoids overloading `ListBox.Tag` for accent colors).
- **`MainWindow.xaml`** — 4-column node-link UI (Outputs / Inputs / Equalizers / Link configs). Bezier curves overlay `LinkCanvas` connecting selections. Intercepts `Closing` and delegates to `TrayIconHost`.
- **`Views/EqualizerEditorWindow.xaml`** — 10-band modal.
- **`Views/NameEditorWindow.xaml`** — generic name prompt (rename / save-as).
- **`Views/HelpWindow.xaml`** — in-app documentation modal (opened via Help button or F1).
- **`Views/CloseChoiceWindow.xaml`** — Minimize / Close / Cancel + "Don't ask again".
- **`Themes/Dark.xaml` + `Light.xaml`** — resource dictionaries.
- **`Assets/audio-switch.ico`** — multi-resolution tray / taskbar icon.

## Profile model

Profile = named bundle of `ComponentIds[]`. Components are stored once in `ComponentLibrary` and referenced by id.

```json
{
  "schemaVersion": 2,
  "library": {
    "outputs":      [{ "id": "guid", "name": "...", "deviceId": "...", "volume": 80 }],
    "inputs":       [{ "id": "guid", "name": "...", "deviceId": "...", "volume": 80 }],
    "equalizers":   [{ "id": "guid", "name": "...", "bands": [...] }],
    "spatialModes": [{ "id": "guid", "name": "...", "mode": "DolbyAtmos" }]
  },
  "profiles": [
    { "name": "Gaming", "hotkey": "Ctrl+Shift+1", "componentIds": ["guid", "guid"] }
  ],
  "activeProfile": "Gaming",
  "themePreference": "System",
  "closeBehavior": "Prompt"
}
```

## Apply flow

```
User → hotkey / double-click / tray menu
  → ProfileManager.ApplyProfile(name)
    → ProfileApplier.Apply(profile, library)
      → TryStep(SetDefault render)       ──► CoreAudioController (COM)
      → TryStep(SetDefault capture)      ──► CoreAudioController (COM)
      → TryStep(SetVolume output)        ──► VolumeController (COM)
      → TryStep(SetVolume input)         ──► VolumeController (COM)
      → TryStep(SetMode spatial)         ──► SpatialAudioController
    → returns ProfileApplyResult { Profile, Errors[] }
  → _data.ActiveProfile = name; store.Save(_data)
  → ProfileApplied event → MainWindow status bar + TrayIconHost tooltip
```

Errors are data, not exceptions. The profile is still marked active and persisted even on partial failure.

## Graceful failure invariants
- Corrupt / old-schema `profiles.json` → renamed to `.corrupt-{utc-timestamp}`; empty data returned.
- Forward-compat load: new top-level fields default-initialize when absent (no quarantine).
- `Unregister` is idempotent; `SetStartWithWindows(false)` safe when nothing is registered.
- Hotkey registration for a missing profile is wrapped in `try { ... } catch { }` — delete-races are benign.

## Tray + window lifecycle
- `Application.ShutdownMode = OnExplicitShutdown`; closing `MainWindow` never shuts the app down unless `TrayIconHost` escalates.
- `MainWindow.Closing` → `TrayIconHost.RequestClose()`:
  - `Prompt` (default) → `CloseChoiceWindow`; user picks Minimize / Close / Cancel. If "Don't ask again" is ticked, the chosen action becomes the persisted `CloseBehavior` via `CloseBehaviorResolver`.
  - `MinimizeToTray` → `Hide()`, window stays in tray.
  - `Exit` → `Application.Shutdown()` (tray host sets `_exiting` so the Closing handler lets the close proceed).
- Tray left-click / double-click → `Show + Activate`.
- Tray "Start with Windows" → registers HKCU Run key value `AudioSwitch` with `"<path>" --startup`; auto-launched instance hides `MainWindow`.

## Test strategy
- **Unit-test Core.** Everything testable without WPF / COM lives here: `ProfileStore`, `ProfileManager`, `ProfileApplier`, `HotkeyParser`, `EqualizerPresets`, `CloseBehaviorResolver`, `StartupRegistrationService`, `ComponentLibrary`, `EqualizerComponent`.
- **Fakes over mocks.** `tests/AudioSwitch.Core.Tests/Fakes/` contains `FakeAudioDeviceService`, `FakeVolumeController`, `FakeSpatialAudioController`, `FakeProfileStore`, `InMemoryRegistryStore`.
- **Happy / Sad sections.** Sad-path tests carry a 5W XML doc (Who / What / Why / Where / How) — see `CODING_STANDARDS.md` §6.
- **WPF and COM are not unit-tested.** The `AudioSwitch.Audio.Tests` project is an empty scaffold. UI changes are smoke-tested manually (launch → exercise → verify).

## Key dependencies
| Package | Purpose |
|---|---|
| `Hardcodet.NotifyIcon.Wpf` | System tray icon |
| `CommunityToolkit.Mvvm` | MVVM source generators (reserved; not yet used broadly) |
| `Microsoft.Win32.Registry` (via `WindowsBase`) | HKCU Run-key access |

## Untouched V2 surface
- **Equalizer APO wiring.** `EqualizerComponent.Bands` values persist through the library but aren't yet swapped into a live APO config. Zero-latency principle still applies — config-file swap, not DSP.
- **Spatial audio controller.** Currently a stub returning `Stereo`.
