namespace AudioSwitch.Core.Models;

public abstract class Component
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;
}

public sealed class OutputDeviceComponent : Component
{
    public string DeviceId { get; set; } = string.Empty;

    public int Volume { get; set; } = 80;
}

public sealed class InputDeviceComponent : Component
{
    public string DeviceId { get; set; } = string.Empty;

    public int Volume { get; set; } = 80;
}

public sealed class SpatialAudioComponent : Component
{
    public SpatialAudioMode Mode { get; set; } = SpatialAudioMode.Stereo;
}

public sealed class EqualizerComponent : Component
{
    public string ApoConfigPath { get; set; } = string.Empty;
}
