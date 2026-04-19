using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Tests.Fakes;

internal sealed class FakeVolumeController : IVolumeController
{
    public List<(string DeviceId, AudioDeviceDirection Direction, int Volume)> SetVolumeCalls { get; } = new();

    public Action<string, AudioDeviceDirection, int>? OnSetVolume { get; set; }

    public int GetVolume(string deviceId, AudioDeviceDirection direction) => 0;

    public void SetVolume(string deviceId, AudioDeviceDirection direction, int volume)
    {
        SetVolumeCalls.Add((deviceId, direction, volume));
        OnSetVolume?.Invoke(deviceId, direction, volume);
    }
}
