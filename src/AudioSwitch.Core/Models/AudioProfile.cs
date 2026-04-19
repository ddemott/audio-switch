namespace AudioSwitch.Core.Models;

public sealed class AudioProfile
{
    public string Name { get; set; } = string.Empty;

    public string? Hotkey { get; set; }

    public DeviceRef? InputDevice { get; set; }

    public DeviceRef? OutputDevice { get; set; }

    public SpatialAudioMode SpatialMode { get; set; } = SpatialAudioMode.Stereo;

    public int InputVolume { get; set; } = 80;

    public int OutputVolume { get; set; } = 80;
}

public sealed class DeviceRef
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
