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
}
