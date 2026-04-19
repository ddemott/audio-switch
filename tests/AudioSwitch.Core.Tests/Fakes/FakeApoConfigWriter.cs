using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests.Fakes;

internal sealed class FakeApoConfigWriter : IApoConfigWriter
{
    public bool IsAvailable { get; set; }

    public List<IReadOnlyList<ApoDeviceEntry>> WriteCalls { get; } = new();

    public Action<IReadOnlyList<ApoDeviceEntry>>? OnWrite { get; set; }

    public string? CurrentSnapshot { get; set; }

    public List<string?> RestoreCalls { get; } = new();

    public void Write(IReadOnlyList<ApoDeviceEntry> entries)
    {
        WriteCalls.Add(entries);
        OnWrite?.Invoke(entries);
    }

    public string? Snapshot() => CurrentSnapshot;

    public void Restore(string? snapshot) => RestoreCalls.Add(snapshot);
}
