# TODO

Deferred work, in rough priority order. Covers what's known but not yet implemented.

---

## Near-term

### Portable / "mobile" installation (no registry writes)
A drop-in folder that runs without touching HKCU/HKLM and stores data beside the exe.
- **`Core/Services/PortableMode.cs`** — static helper; `IsActive` = `File.Exists(<exe-dir>/portable.flag)`.
- **`ProfileStore.DefaultFilePath`** — return `<exe-dir>/profiles.json` when portable mode is active, otherwise keep `%APPDATA%\AudioSwitch\profiles.json`.
- **`AppHost.SetStartWithWindows`** — become a no-op when portable; `IsStartWithWindowsEnabled` returns false.
- **`TrayIconHost`** — disable the "Start with Windows" menu item with tooltip "disabled in portable mode"; surface portable status in the main-window title or footer.
- **`scripts/build-portable.ps1`** — runs `dotnet publish -c Release -r win-x64 --self-contained`, copies output to `build/portable/`, drops `portable.flag` in it, zips the folder.
- **Tests** — `ProfileStoreTests`: verify `DefaultFilePath(baseDir)` returns the portable path when the marker is present in `baseDir`, and the `APPDATA` path when it is absent.

### Update docs once portable mode lands
- **README.md** — add a "Portable install" section describing the `portable.flag` convention and the `build-portable.ps1` script.
- **ARCHITECTURE.md** — document `PortableMode` in the services table and note the branch in `ProfileStore.DefaultFilePath`.
- **CLAUDE.md** — add to "non-obvious invariants": portable mode disables registry writes; `Environment.ProcessPath` is the source of truth for the base directory.
- **CORE_PRINCIPLE.md** — add portability alongside zero-latency as a design goal.

### Per-profile volume editing
Currently every auto-populated Output/Input component defaults to `Volume = 80` and there is no UI to change it. Switching between profiles that share a device produces no audible change.
- Context menu on an Output/Input row → "Edit…" opens a simple numeric slider dialog (0–100) that updates `Volume` on the component.
- Persist via `ProfileManager.UpdateComponent`.
- This is the fastest path to an audibly-different profile when two profiles point to the same output device (which is the current state of `profiles.json` for Gaming Profile / Gaming Boost / Voice clarity).

### Rename + edit Output and Input components
Task #30 from the prior session state. Currently only Equalizers and Profiles can be renamed/edited; Output/Input components are read-only after auto-sync populates them.
- Add "Rename…" and "Edit…" items to `Component_RightClick` for Output / Input rows (reusing `NameEditorWindow` for rename, a numeric-field dialog for volume).

---

## V2 roadmap (affects audio directly)

### Equalizer APO wiring
EQ bands are stored in `profiles.json` but do not affect audio. The zero-latency principle (see `CORE_PRINCIPLE.md`) mandates driver-level config-file swap, not real-time DSP.
- Detect Equalizer APO installation (registry or file probe).
- On `ApplyProfile`, write the selected `EqualizerComponent.Bands` to the device's APO config file (path varies per device — typically `C:\Program Files\EqualizerAPO\config\config.txt` or per-device configs).
- Handle "APO not installed" gracefully — status-bar message, don't fail the whole apply.

### Spatial audio controller
`SpatialAudioController.SetMode` is currently a stub returning `Stereo`.
- Wire against Windows' spatial audio API (`AudioExtensionSettings` / `PropVariant` on `ENDPOINT_PROPKEY_SpatialAudioFormat` or the newer CLSID registration path).
- Test against Windows Sonic, Dolby Atmos, and DTS Headphone:X.

---

## Refactors / polish

Done (shipped):
- ~~**HotkeyRegistrar extraction** — `Core/Services/HotkeyRegistrar.cs` with 6 tests + `FakeHotkeyService`.~~
- ~~**Attached property for row-select borders** — `App/Controls/ListBoxAssist.SelectedBorderBrush`; `MainWindow.xaml` no longer overloads `Tag`.~~
- ~~**Single-source `--startup`** — `StartupRegistrationService.StartupArg` + `IsStartupLaunch(args)`; `Register` takes a raw path and formats the command line internally.~~

Still open:
- **`AudioSwitch.Audio.Tests` is empty.** Nothing concrete to test yet; re-evaluate when non-COM logic lands in that project.
- **Settings extraction.** `ProfileStoreData` mixes profiles + library + `ThemePreference` + `CloseBehavior`. Deferred as YAGNI until a third/fourth setting lands — at that point split into `AppSettings` vs `ProfileData`.

---

## Known quirks (document, don't fix)

- `AudioSwitch.Audio.Tests` reports "No test is available" — it's an empty scaffold, not a regression.
- `Icon.ToBitmap()` on the generated `.ico` throws because entries are Vista+ PNG-embedded; use `scripts/preview-icon.ps1` or a pack-URI `BitmapImage` when you need a preview.
- Smart App Control on Windows 11 occasionally blocks `AudioSwitch.Core.dll` with `0x800711C7` — reboot clears it. `Unblock-File` does not.
