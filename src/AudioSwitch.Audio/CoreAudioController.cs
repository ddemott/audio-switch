using System.Runtime.InteropServices;
using AudioSwitch.Audio.Interop;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;
using NAudio.CoreAudioApi;

namespace AudioSwitch.Audio;

public sealed class CoreAudioController : IAudioDeviceService
{
    public IReadOnlyList<AudioDevice> GetDevices(AudioDeviceDirection direction)
    {
        using var enumerator = new MMDeviceEnumerator();
        var flow = ToDataFlow(direction);
        var defaultId = TryGetDefaultId(enumerator, flow);

        var result = new List<AudioDevice>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            try
            {
                result.Add(new AudioDevice(
                    device.ID,
                    device.FriendlyName,
                    direction,
                    IsDefault: device.ID == defaultId));
            }
            finally
            {
                device.Dispose();
            }
        }
        return result;
    }

    public AudioDevice? GetDefault(AudioDeviceDirection direction)
    {
        using var enumerator = new MMDeviceEnumerator();
        var flow = ToDataFlow(direction);
        if (!enumerator.HasDefaultAudioEndpoint(flow, Role.Multimedia))
        {
            return null;
        }

        using var device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
        return new AudioDevice(device.ID, device.FriendlyName, direction, IsDefault: true);
    }

    public void SetDefault(string deviceId, AudioDeviceDirection direction)
    {
        var policy = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, ERole.Console));
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, ERole.Multimedia));
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(deviceId, ERole.Communications));
        }
        finally
        {
            Marshal.ReleaseComObject(policy);
        }
    }

    private static DataFlow ToDataFlow(AudioDeviceDirection direction) =>
        direction == AudioDeviceDirection.Render ? DataFlow.Render : DataFlow.Capture;

    private static string? TryGetDefaultId(MMDeviceEnumerator enumerator, DataFlow flow)
    {
        if (!enumerator.HasDefaultAudioEndpoint(flow, Role.Multimedia))
        {
            return null;
        }
        using var device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
        return device.ID;
    }
}
