using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Interfaces;

public interface ISpatialAudioController
{
    SpatialAudioMode GetMode(string deviceId);

    void SetMode(string deviceId, SpatialAudioMode mode);
}
