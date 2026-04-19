using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Tests.Fakes;

internal sealed class FakeAudioDeviceService : IAudioDeviceService
{
    public List<(string DeviceId, AudioDeviceDirection Direction)> SetDefaultCalls { get; } = new();

    public Action<string, AudioDeviceDirection>? OnSetDefault { get; set; }

    public IReadOnlyList<AudioDevice> GetDevices(AudioDeviceDirection direction) => Array.Empty<AudioDevice>();

    public AudioDevice? GetDefault(AudioDeviceDirection direction) => null;

    public void SetDefault(string deviceId, AudioDeviceDirection direction)
    {
        SetDefaultCalls.Add((deviceId, direction));
        OnSetDefault?.Invoke(deviceId, direction);
    }
}
