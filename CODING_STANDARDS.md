# Coding Standards

Standards for the audio-switch project. These are binding for any contributor (human or AI) working in this repo.

---

## 1. Architecture

- **Clear layer separation.** `AudioSwitch.Core` is platform-agnostic business logic and interfaces. `AudioSwitch.Audio` implements those interfaces against Windows Core Audio COM. `AudioSwitch.App` is WPF presentation only. Never let COM or WPF types leak up into Core.
- Implementations live behind interfaces defined in Core. The Core project never references Audio or App.
- Components (`OutputDeviceComponent`, `InputDeviceComponent`, `EqualizerComponent`, `SpatialAudioComponent`) are first-class reusable entities identified by stable GUIDs. Profiles reference them by id; they do not inline device data.

## 2. DRY — don't repeat yourself

- Extract repeated patterns into named helpers. Example: `ProfileManager.PersistProfiles()` replaces three occurrences of `_store.Save(_data) + ProfilesChanged?.Invoke(...)`.
- If you write the same 3+ lines twice, pause. Name them once.

## 3. Small modules, small methods

- Prefer many small focused classes over one large class. A class should fit one sentence of responsibility.
- If a method needs more than ~15 lines **and** has multiple responsibilities, consider splitting. (Length alone isn't a code smell; mixed responsibility is.)
- Concrete example from this repo: `ProfileApplier` was extracted out of `ProfileManager` so manager handles CRUD and applier handles orchestration.

## 4. Test-Driven Development (TDD)

- Write a failing test first. Then write the simplest code that makes it pass. Then refactor.
- When fixing a bug, reproduce it in a test before changing any code. That test becomes a regression guard.
- TDD has already paid off in this repo — `HotkeyParser` tests caught a real `RemoveEmptyEntries` bug before it shipped; `ProfileStore` tests caught the `SchemaVersion` default-value bug during V1→V2 migration.

## 5. Thorough test coverage

- Cover the happy path, edge cases, error cases, and event emission. Use **fakes** (in `tests/AudioSwitch.Core.Tests/Fakes/`) over mocking libraries.
- Platform code that touches real COM (e.g. `CoreAudioController`, `VolumeController`) can't be unit-tested. When such a class gains new logic, extract the testable part into Core and unit-test that.

## 6. Test file structure — Happy / Sad with 5Ws

Every test file splits into two clearly marked sections:

```csharp
// === Happy path ===
// ...

// === Sad path ===
// ...
```

A **sad-path test** is anything where the system rejects, throws, no-ops, or refuses — not just thrown exceptions. Example: `RemoveProfile_UnknownName_NoOps` is sad.

Every sad-path test MUST carry an XML `///` doc comment with exactly five lines:

- **Who:** which caller / actor triggers the failure
- **What:** what fails (exception type, error message, side-effect avoided)
- **Why:** the underlying invariant or rule the production code is protecting
- **Where:** the file / method / guard that enforces the rule
- **How:** how the test reproduces the failure

Example:

```csharp
/// <summary>
/// Sad path: AddProfile called with a name that already exists.
/// Who: ProfileEditor save handler when the user types an existing name.
/// What: throws InvalidOperationException naming the duplicate; no profile added, no persist, no event.
/// Why: Profile name is the unique key for lookup, apply, active-profile tracking — a collision corrupts every lookup.
/// Where: ProfileManager.AddProfile FindProfile guard before _data.Profiles.Add.
/// How: Add a profile, then call AddProfile again with the same name.
/// </summary>
[Fact]
public void AddProfile_DuplicateName_Throws() { ... }
```

Happy-path tests use plain descriptive names and do not need the 5W doc.

## 7. Graceful failure

Audio is mission-critical for the user. Prefer degrading gracefully over propagating exceptions at the app boundary.

Concrete rules:

- **`ProfileStore.Load`** — corrupt JSON or old schema version → rename file to `.corrupt-{utc-timestamp}` and return empty `ProfileStoreData`. Never crash app launch.
- **`ProfileApplier.Apply`** — each device / volume / spatial step is wrapped individually in `TryStep`. Failures are collected into a `ProfileApplyResult.Errors` list and returned — *not* thrown. The caller decides whether to notify the user, log, or retry.
- **`ProfileManager.ApplyProfile`** — even if some steps fail, mark the profile active and persist. The user explicitly chose this profile; partial apply is better than no apply.
- **COM boundary classes** (`CoreAudioController`, `VolumeController`, `SpatialAudioController`) may throw COM HRESULT exceptions. That's fine — the surrounding `TryStep` in the applier converts them into `ProfileApplyStepError` records.

Anti-patterns:

- **Don't** wrap business logic in a blanket `try/catch`. Only catch where the failure is *recoverable* (per-step in apply, parse in load).
- **Don't** swallow exceptions silently. Always surface them as data (error list) or via a backup file.
- **Don't** catch in test fakes — let production code's `TryStep` handle it.

Exception-throwing is only appropriate for **programmer errors** (null arg, invalid state, corrupted invariants) that indicate a bug — not for runtime conditions like "device unplugged" or "profiles.json is stale".

## 8. Comments and documentation

- Default to writing **no comments**. Well-named identifiers already explain the *what*.
- Only add a comment when the *why* is non-obvious: a hidden constraint, a subtle invariant, a workaround for a specific bug, behavior that would surprise a reader.
- Don't reference the current task or ticket number in comments — those belong in the PR description and rot as the codebase evolves.
- Do document non-obvious undocumented-API usage (e.g. the `IPolicyConfig` COM interface in `AudioSwitch.Audio/Interop/PolicyConfig.cs`).

## 9. Security and risky actions

- Never skip git hooks (`--no-verify`) or bypass signing unless the user explicitly requests it.
- Never `git push --force` to `main` without explicit confirmation.
- Never create commits unless requested. Commit messages focus on the *why*.
- Destructive operations (`rm -rf`, `git reset --hard`, dropping a file) require user confirmation in the same message, not tacit past consent.
