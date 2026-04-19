using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests.Fakes;

internal sealed class FakeApoConfigWriter : IApoConfigWriter
{
    public bool IsAvailable { get; set; }

    public List<IReadOnlyList<ApoDeviceEntry>> WriteCalls { get; } = new();

    public Action<IReadOnlyList<ApoDeviceEntry>>? OnWrite { get; set; }

    public void Write(IReadOnlyList<ApoDeviceEntry> entries)
    {
        WriteCalls.Add(entries);
        OnWrite?.Invoke(entries);
    }
}
