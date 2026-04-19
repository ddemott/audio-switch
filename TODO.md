# TODO

Deferred work, in rough priority order. Covers what's known but not yet implemented.

---

## Refactors / polish

Done (shipped):
- ~~**HotkeyRegistrar extraction** — `Core/Services/HotkeyRegistrar.cs` with 6 tests + `FakeHotkeyService`.~~
- ~~**Attached property for row-select borders** — `App/Controls/ListBoxAssist.SelectedBorderBrush`; `MainWindow.xaml` no longer overloads `Tag`.~~
- ~~**Single-source `--startup`** — `StartupRegistrationService.StartupArg` + `IsStartupLaunch(args)`; `Register` takes a raw path and formats the command line internally.~~
- ~~**Rename Output/Input components** — right-click row → "Rename '...'..." reuses `NameEditorWindow`; persists via `UpdateComponent`.~~
- ~~**Portable mode** — `Core/Services/PortableMode.cs` + `ProfileStore.DefaultFilePath(baseDir)` branch; `AppHost.IsPortable` short-circuits `SetStartWithWindows`; tray menu disables + main-window title suffix; `scripts/build-portable.ps1` publishes self-contained with `portable.flag`.~~
- ~~**Per-profile volume editing** — `AudioProfile.ComponentVolumes` dict (override → component default); `ProfileApplier.ResolveVolume`; `ProfileVolumesWindow` modal with sliders per Output/Input; right-click profile → "Edit volumes...". 5 new applier tests.~~
- ~~**In-app Help window** — `HelpWindow.xaml` opened from Help button (top-right) or F1 via `ApplicationCommands.Help`. Covers main-window layout, profile CRUD, hotkeys, tray, theme, portable mode, troubleshooting.~~
- ~~**Spatial audio controller** — `SpatialAudioController` writes `PKEY_AudioEndpoint_Spatial_Audio_Format` DWORD via raw `IPropertyStore` interop; `Core/Services/SpatialAudioFormatRegistry` maps `SpatialAudioMode` ↔ DWORD. 14 new tests for the mapping + 5W sad-path coverage. Atmos/DTS apps not auto-installed — they silently fall back to stereo if missing.~~
- ~~**Equalizer APO wiring (full)** — `Core/Services/{ApoConfigBuilder,ApoInstallation}` for testable config text + detection (Piece 1). `IApoConfigWriter` + `App/Services/ApoConfigWriter` writes `audioswitch.txt` + idempotent `Include:` line in `config.txt`; `ProfileApplier.ApplyEqualizer` step writes per-output device blocks (Piece 2a). `EqualizerEditorWindow` live preview on slider drag with snapshot/restore on cancel (Piece 2c). `App/Services/ApoInstallHelper` + Tools menu launches bundled `EqualizerAPO64.exe` with UAC + reboot prompt (Piece 2b). 13 new APO tests + 5 new applier tests covering the apply-with-APO path.~~
- ~~**In-app Tools menu** — title-bar Tools button hosts the "Install Equalizer APO..." entry; first home for future install/maintenance helpers.~~
- ~~**Live volume preview in profile editor** — slider drag pushes to `IVolumeController` immediately; Cancel restores the snapshot taken on dialog open.~~

Still open:
- **`AudioSwitch.Audio.Tests` is empty.** Nothing concrete to test yet; re-evaluate when non-COM logic lands in that project.
- **Settings extraction.** `ProfileStoreData` mixes profiles + library + `ThemePreference` + `CloseBehavior`. Deferred as YAGNI until a third/fourth setting lands — at that point split into `AppSettings` vs `ProfileData`.

---

## Known quirks (document, don't fix)

- `AudioSwitch.Audio.Tests` reports "No test is available" — it's an empty scaffold, not a regression.
- `Icon.ToBitmap()` on the generated `.ico` throws because entries are Vista+ PNG-embedded; use `scripts/preview-icon.ps1` or a pack-URI `BitmapImage` when you need a preview.
- Smart App Control on Windows 11 occasionally blocks `AudioSwitch.Core.dll` with `0x800711C7` — reboot clears it. `Unblock-File` does not.
