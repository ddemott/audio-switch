using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Tests.Fakes;

internal sealed class FakeSpatialAudioController : ISpatialAudioController
{
    public List<(string DeviceId, SpatialAudioMode Mode)> SetModeCalls { get; } = new();

    public Action<string, SpatialAudioMode>? OnSetMode { get; set; }

    public SpatialAudioMode GetMode(string deviceId) => SpatialAudioMode.Stereo;

    public void SetMode(string deviceId, SpatialAudioMode mode)
    {
        SetModeCalls.Add((deviceId, mode));
        OnSetMode?.Invoke(deviceId, mode);
    }
}
