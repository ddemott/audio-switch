using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

// Maps SpatialAudioMode to the DWORD value Windows expects in the
// PKEY_AudioEndpoint_Spatial_Audio_Format property on the audio endpoint.
//
// Values mirror what Windows Sound Settings writes when you flip the
// spatial-audio dropdown:
//   0 = Off (stereo passthrough)
//   1 = Windows Sonic for Headphones (built into Windows; always available)
//   2 = Dolby Atmos for Headphones (requires Dolby Access app from Microsoft Store)
//   3 = DTS Headphone:X / DTS Sound Unbound (requires DTS app)
//
// Modes whose backing app isn't installed will still be set in the property
// store, but won't take audible effect — the audio engine silently falls back
// to stereo. Detection of "is this format registered" lives in the COM layer.
public static class SpatialAudioFormatRegistry
{
    public const int OffFormatId = 0;
    public const int WindowsSonicFormatId = 1;
    public const int DolbyAtmosFormatId = 2;
    public const int DtsHeadphoneXFormatId = 3;

    public static int GetFormatId(SpatialAudioMode mode) => mode switch
    {
        SpatialAudioMode.Stereo => OffFormatId,
        SpatialAudioMode.WindowsSonic => WindowsSonicFormatId,
        SpatialAudioMode.DolbyAtmos => DolbyAtmosFormatId,
        SpatialAudioMode.ThxSpatial => DtsHeadphoneXFormatId,
        _ => OffFormatId,
    };

    public static SpatialAudioMode FromFormatId(int formatId) => formatId switch
    {
        WindowsSonicFormatId => SpatialAudioMode.WindowsSonic,
        DolbyAtmosFormatId => SpatialAudioMode.DolbyAtmos,
        DtsHeadphoneXFormatId => SpatialAudioMode.ThxSpatial,
        _ => SpatialAudioMode.Stereo,
    };
}
