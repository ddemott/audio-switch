using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Audio;

// TODO(phase 3): Spatial audio format is stored in undocumented endpoint properties.
// Plan: write to IPropertyStore on the MMDevice using PKEY_AudioEndpoint_Spatial,
// or fall back to HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{id}\Properties.
// Stubbed for now so ProfileManager.ApplyProfile has a working dependency to inject.
public sealed class SpatialAudioController : ISpatialAudioController
{
    public SpatialAudioMode GetMode(string deviceId) => SpatialAudioMode.Stereo;

    public void SetMode(string deviceId, SpatialAudioMode mode)
    {
    }
}
