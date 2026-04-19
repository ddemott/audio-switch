using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Tests.Fakes;

internal sealed class FakeProfileStore : IProfileStore
{
    public ProfileStoreData Data { get; set; } = new();

    public int SaveCount { get; private set; }

    public ProfileStoreData Load() => Data;

    public void Save(ProfileStoreData data)
    {
        Data = data;
        SaveCount++;
    }
}
