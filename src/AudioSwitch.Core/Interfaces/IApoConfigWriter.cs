using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Interfaces;

// Writes Equalizer APO configuration to disk so the audio engine picks up our
// EQ settings on the next stream. Implementations live outside Core because
// they touch the file system.
//
// IsAvailable should return false when APO isn't installed (or the config
// directory isn't writable). Callers MUST check it before calling Write — when
// false, Write is a no-op or may throw, depending on the implementation.
//
// The single Write(entries) call replaces the entire AudioSwitch-owned config
// segment. Implementations must not preserve previously-written device blocks
// across calls — the caller decides what should be active.
public interface IApoConfigWriter
{
    bool IsAvailable { get; }

    void Write(IReadOnlyList<ApoDeviceEntry> entries);

    // Captures the current AudioSwitch-owned APO config so a live-preview
    // session can restore it on cancel. Returns null if there's nothing to
    // snapshot (APO not installed, or the file doesn't exist yet — the
    // restore path treats null as "delete the file").
    string? Snapshot();

    // Restores the AudioSwitch-owned config to a prior snapshot. Passing null
    // deletes the file — used when no snapshot was captured because the file
    // didn't exist before the preview session began.
    void Restore(string? snapshot);
}
