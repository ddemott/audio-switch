using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Interfaces;

public interface IAudioDeviceService
{
    IReadOnlyList<AudioDevice> GetDevices(AudioDeviceDirection direction);

    AudioDevice? GetDefault(AudioDeviceDirection direction);

    void SetDefault(string deviceId, AudioDeviceDirection direction);
}
