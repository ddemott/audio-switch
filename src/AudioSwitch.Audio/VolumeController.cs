using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;
using NAudio.CoreAudioApi;

namespace AudioSwitch.Audio;

public sealed class VolumeController : IVolumeController
{
    public int GetVolume(string deviceId, AudioDeviceDirection direction)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDevice(deviceId);
        var scalar = device.AudioEndpointVolume.MasterVolumeLevelScalar;
        return (int)Math.Round(scalar * 100f);
    }

    public void SetVolume(string deviceId, AudioDeviceDirection direction, int volume)
    {
        var clamped = Math.Clamp(volume, 0, 100);
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDevice(deviceId);
        device.AudioEndpointVolume.MasterVolumeLevelScalar = clamped / 100f;
    }
}
