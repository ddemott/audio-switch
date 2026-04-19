namespace AudioSwitch.Core.Models;

public sealed class ProfileStoreData
{
    public List<AudioProfile> Profiles { get; set; } = new();

    public string? ActiveProfile { get; set; }
}
