namespace AudioSwitch.Core.Models;

public sealed class ProfileStoreData
{
    public int SchemaVersion { get; set; }

    public ComponentLibrary Library { get; set; } = new();

    public List<AudioProfile> Profiles { get; set; } = new();

    public string? ActiveProfile { get; set; }
}
