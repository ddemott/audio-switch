namespace AudioSwitch.Core.Models;

public sealed class AudioProfile
{
    public string Name { get; set; } = string.Empty;

    public string? Hotkey { get; set; }

    public List<string> ComponentIds { get; set; } = new();
}
