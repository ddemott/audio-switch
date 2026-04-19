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

    public List<EqualizerBand> Bands { get; set; } = DefaultBands();

    public static List<EqualizerBand> DefaultBands() => new()
    {
        new EqualizerBand { Frequency = 31 },
        new EqualizerBand { Frequency = 62 },
        new EqualizerBand { Frequency = 125 },
        new EqualizerBand { Frequency = 250 },
        new EqualizerBand { Frequency = 500 },
        new EqualizerBand { Frequency = 1000 },
        new EqualizerBand { Frequency = 2000 },
        new EqualizerBand { Frequency = 4000 },
        new EqualizerBand { Frequency = 8000 },
        new EqualizerBand { Frequency = 16000 },
    };
}

public sealed class EqualizerBand
{
    public int Frequency { get; set; }

    public double Gain { get; set; }
}
