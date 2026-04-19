using System.Runtime.InteropServices;
using AudioSwitch.Audio.Interop;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Audio;

// Sets the per-endpoint spatial-audio format by writing the
// PKEY_AudioEndpoint_Spatial_Audio_Format DWORD on the device's IPropertyStore
// (same property Windows Sound Settings flips when you change the spatial dropdown).
//
// Per-endpoint property writes succeed without admin on most Windows 11 setups.
// If the IPropertyStore is opened as read-only by Windows for a particular endpoint
// (rare; happens on locked-down or enterprise-policy machines), SetValue / Commit
// will return E_ACCESSDENIED — callers should let that propagate so ProfileApplier's
// TryStep records it and the user sees a status-bar message.
//
// For Atmos/DTS to actually take audible effect, the corresponding companion app
// (Dolby Access / DTS Sound Unbound) must be installed and the endpoint must
// support the format. We write the DWORD regardless; the audio engine silently
// falls back to stereo if the format isn't actually available.
public sealed class SpatialAudioController : ISpatialAudioController
{
    public SpatialAudioMode GetMode(string deviceId)
    {
        var device = GetDevice(deviceId);
        if (device is null) return SpatialAudioMode.Stereo;
        try
        {
            if (device.OpenPropertyStore(SpatialAudioInterop.STGM_READ, out var store) != 0 || store is null)
            {
                return SpatialAudioMode.Stereo;
            }
            try
            {
                var key = SpatialAudioInterop.PKEY_AudioEndpoint_Spatial_Audio_Format;
                if (store.GetValue(ref key, out var pv) != 0)
                {
                    return SpatialAudioMode.Stereo;
                }
                try
                {
                    if (pv.vt != SpatialAudioInterop.VT_UI4)
                    {
                        return SpatialAudioMode.Stereo;
                    }
                    return SpatialAudioFormatRegistry.FromFormatId((int)pv.uintValue);
                }
                finally
                {
                    SpatialAudioInterop.PropVariantClear(ref pv);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    public void SetMode(string deviceId, SpatialAudioMode mode)
    {
        var formatId = SpatialAudioFormatRegistry.GetFormatId(mode);
        var device = GetDevice(deviceId)
            ?? throw new InvalidOperationException($"Audio endpoint '{deviceId}' not found.");
        try
        {
            Marshal.ThrowExceptionForHR(
                device.OpenPropertyStore(SpatialAudioInterop.STGM_READWRITE, out var store));
            try
            {
                var key = SpatialAudioInterop.PKEY_AudioEndpoint_Spatial_Audio_Format;
                var pv = new PROPVARIANT
                {
                    vt = SpatialAudioInterop.VT_UI4,
                    uintValue = (uint)formatId,
                };
                Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref pv));
                Marshal.ThrowExceptionForHR(store.Commit());
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private static IMMDevice? GetDevice(string deviceId)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
        try
        {
            return enumerator.GetDevice(deviceId, out var device) == 0 ? device : null;
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }
}
