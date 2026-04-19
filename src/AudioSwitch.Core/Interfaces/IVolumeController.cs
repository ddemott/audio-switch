using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Interfaces;

public interface IVolumeController
{
    int GetVolume(string deviceId, AudioDeviceDirection direction);

    void SetVolume(string deviceId, AudioDeviceDirection direction, int volume);
}
