# TODO

Deferred work, in rough priority order. Covers what's known but not yet implemented.

---

## Near-term

### Per-profile volume editing — **blocked on data-model decision**
Currently every auto-populated Output/Input component defaults to `Volume = 80`. Two profiles that share the same output device produce no audible change between them.

Three possible shapes, not yet picked:
1. **Volume on the profile itself** (`Profile.Overrides: componentId → volume`) — cleanest model, biggest refactor. Components stay reusable atoms; each profile overrides values as-needed.
2. **New "Levels" column** — keeps the orthogonal-component model. User creates `VolumeComponent` entries and links them to profiles.
3. **Lump onto `EqualizerComponent`** — smallest diff, ugly semantics (forces a flat-EQ entry every time you only want a volume tweak).

Recommendation: option 1. Revisit when user is ready to commit to a shape.

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
- ~~**Rename Output/Input components** — right-click row → "Rename '...'..." reuses `NameEditorWindow`; persists via `UpdateComponent`.~~
- ~~**Portable mode** — `Core/Services/PortableMode.cs` + `ProfileStore.DefaultFilePath(baseDir)` branch; `AppHost.IsPortable` short-circuits `SetStartWithWindows`; tray menu disables + main-window title suffix; `scripts/build-portable.ps1` publishes self-contained with `portable.flag`.~~

Still open:
- **`AudioSwitch.Audio.Tests` is empty.** Nothing concrete to test yet; re-evaluate when non-COM logic lands in that project.
- **Settings extraction.** `ProfileStoreData` mixes profiles + library + `ThemePreference` + `CloseBehavior`. Deferred as YAGNI until a third/fourth setting lands — at that point split into `AppSettings` vs `ProfileData`.

---

## Known quirks (document, don't fix)

- `AudioSwitch.Audio.Tests` reports "No test is available" — it's an empty scaffold, not a regression.
- `Icon.ToBitmap()` on the generated `.ico` throws because entries are Vista+ PNG-embedded; use `scripts/preview-icon.ps1` or a pack-URI `BitmapImage` when you need a preview.
- Smart App Control on Windows 11 occasionally blocks `AudioSwitch.Core.dll` with `0x800711C7` — reboot clears it. `Unblock-File` does not.
