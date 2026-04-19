# CLAUDE.md

Orientation for AI collaborators working in this repo. Read `CODING_STANDARDS.md` first — its rules are binding.

## Project shape
Windows-only WPF audio-switcher. Three C# projects + two xUnit test projects. Target framework: `net8.0-windows`.

```
src/
  AudioSwitch.Core/   # domain: models, services, interfaces. NO WPF, NO COM.
  AudioSwitch.Audio/  # COM adapters (CoreAudioController, VolumeController, ...).
  AudioSwitch.App/    # WPF shell, tray, views, composition root.
tests/
  AudioSwitch.Core.Tests/   # 99 tests via xUnit + fakes in tests/.../Fakes/.
  AudioSwitch.Audio.Tests/  # empty scaffold — COM is not unit-tested.
```

## What lives where (things you'll touch often)
- **Add a new testable rule / pure logic →** `AudioSwitch.Core/Services/`, TDD in `AudioSwitch.Core.Tests/`.
- **Add a new domain type →** `AudioSwitch.Core/Models/`. If it's a component, inherit `Component`.
- **Add a COM-touching capability →** implement a new `IFoo` interface in `Core/Interfaces/`, implement against COM in `AudioSwitch.Audio/`. Wire in `AppHost`.
- **Add a WPF view →** `AudioSwitch.App/Views/`. Keep logic thin; push decisions into Core and test them.
- **Add persistent settings →** extend `ProfileStoreData` (fields default-initialize — forward-compatible without bumping `CurrentSchemaVersion`). Access via `ProfileManager.PersistSetting(Action<ProfileStoreData>)`.

## Composition root
`App.xaml.cs.OnStartup` → instantiate `ThemeService`, `MainWindow`, `HotkeyService`, `AppHost`, `TrayIconHost` in that order (`HotkeyService` needs `HwndSource` from a shown `MainWindow`). `AppHost` wires everything else.

## Non-obvious invariants

- **`ShutdownMode = OnExplicitShutdown`.** Closing the main window does NOT quit. Only `Application.Shutdown()` from `TrayIconHost.ExitApplication()` does. `MainWindow.Closing` is intercepted; it delegates to `TrayIconHost.RequestClose`, which consults `ProfileManager.CloseBehavior` (Prompt / Minimize / Exit).
- **`--startup` CLI arg.** Single-sourced in `StartupRegistrationService.StartupArg`. When the user enables "Start with Windows", `Register(processPath)` writes the command line `"{ProcessPath}" --startup` to HKCU Run. `App.OnStartup` calls `StartupRegistrationService.IsStartupLaunch(e.Args)` to detect this and hides `MainWindow` so auto-start is silent.
- **Portable mode.** `PortableMode.IsActive(exeDir)` = `File.Exists(<exeDir>/portable.flag)`. Checked once in `AppHost` and cached as `IsPortable`. When active: `ProfileStore.DefaultFilePath(exeDir)` returns `<exeDir>/profiles.json` (not `%APPDATA%`), `SetStartWithWindows` is a no-op, the tray "Start with Windows" menu item is disabled with a "(disabled — portable)" suffix, and `MainWindow.Title` becomes "AudioSwitch (Portable)". `scripts/build-portable.ps1` publishes self-contained + drops the `portable.flag` marker.
- **Per-profile volume override.** `AudioProfile.ComponentVolumes: Dictionary<componentId, int>` is consulted by `ProfileApplier` via `profile.ResolveVolume(component, fallback)` — override wins, falls back to `component.Volume`. The dict only stores entries that *differ* from the component default (the editor strips equal-to-default values on save), so an empty dict means "use device defaults everywhere." Orphaned entries (id no longer in `ComponentIds`) are silently ignored, never errored.
- **Settings are not a separate file.** `ThemePreference`, `CloseBehavior`, and future prefs all live on `ProfileStoreData` alongside profiles. Route all writes through `ProfileManager.PersistSetting` — do NOT call `ProfileStore.Load/Save` directly from `AppHost`, or you will clobber `ProfileManager`'s in-memory copy (this bug existed before; it has been fixed — don't re-introduce it).
- **Hotkey registration races.** When a profile is deleted, its `WM_HOTKEY` callback may still fire once. The callback swallows missing-profile errors intentionally; keep that pattern.
- **Tooltip 63-char limit.** `TrayIconHost.BuildTooltip` clips to 63 chars because Windows' `NOTIFYICONDATA.szTip` is a fixed 64-char buffer. Don't remove the clip.
- **Schema version bumping.** `ProfileStore.CurrentSchemaVersion = 2`. If you add a breaking model change, bump it AND delete/migrate existing user data deliberately — the current `data.SchemaVersion < CurrentSchemaVersion` guard quarantines older files, which is good for breakage but BAD for additive changes. Default-value additions should NOT bump the version (they're forward-compat).
- **Bezier redraw coalescing.** `MainWindow.QueueRedraw` uses a `_redrawQueued` flag + `Dispatcher.InvokeAsync` with `DispatcherPriority.Loaded` to avoid a `LayoutUpdated` → `RedrawLinks` → `LayoutUpdated` infinite loop. Don't switch to synchronous redraws.

## Testing posture
- Write a failing test first. Use fakes from `tests/AudioSwitch.Core.Tests/Fakes/`.
- Sad-path tests carry the 5W XML doc (Who / What / Why / Where / How) — see `CODING_STANDARDS.md` §6.
- COM classes (`CoreAudioController`, `VolumeController`, `SpatialAudioController`) are not unit-tested. If you add logic around them, extract it into Core.

## Commands
```
dotnet build
dotnet test
dotnet run --project src/AudioSwitch.App
```

## Common gotchas
- **Smart App Control** on Windows 11 has blocked `AudioSwitch.Core.dll` in the past, collapsing all tests at once with `System.IO.FileLoadException 0x800711C7`. Reboot usually clears it. `Unblock-File` does NOT clear SAC — it's a separate policy layer.
- **`Icon.ToBitmap()` on the generated ICO** throws because the file uses Vista+ PNG-embedded entries; use `Services/TrayIconHost.cs`'s pack-URI `BitmapImage` approach or re-run `scripts/preview-icon.ps1` for a visible-size PNG.
- The `AudioSwitch.Audio.Tests` project has no test files — don't treat "No test is available" as a regression.

## Docs
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — full layout + flows.
- [`CODING_STANDARDS.md`](CODING_STANDARDS.md) — binding rules.
- [`CORE_PRINCIPLE.md`](CORE_PRINCIPLE.md) — why the zero-latency and tray-first decisions exist.
- [`README.md`](README.md) — user-facing.
