namespace AudioSwitch.Core.Models;

public sealed class AudioProfile
{
    public string Name { get; set; } = string.Empty;

    public string? Hotkey { get; set; }

    public List<string> ComponentIds { get; set; } = new();

    public Dictionary<string, int> ComponentVolumes { get; set; } = new();

    public int ResolveVolume(Component component, int fallback) =>
        ComponentVolumes.TryGetValue(component.Id, out var v) ? v : fallback;
}
