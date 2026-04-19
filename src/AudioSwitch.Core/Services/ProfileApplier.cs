using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

internal interface IProfileApplier
{
    ProfileApplyResult Apply(AudioProfile profile);
}

internal sealed class ProfileApplier : IProfileApplier
{
    private readonly IAudioDeviceService _deviceService;
    private readonly IVolumeController _volumeController;
    private readonly ISpatialAudioController _spatialAudioController;

    public ProfileApplier(
        IAudioDeviceService deviceService,
        IVolumeController volumeController,
        ISpatialAudioController spatialAudioController)
    {
        _deviceService = deviceService;
        _volumeController = volumeController;
        _spatialAudioController = spatialAudioController;
    }

    public ProfileApplyResult Apply(AudioProfile profile)
    {
        var errors = new List<ProfileApplyStepError>();
        if (profile.OutputDevice is { } output)
        {
            ApplyOutput(output, profile.SpatialMode, profile.OutputVolume, errors);
        }
        if (profile.InputDevice is { } input)
        {
            ApplyInput(input, profile.InputVolume, errors);
        }
        return new ProfileApplyResult(profile, errors);
    }

    private void ApplyOutput(DeviceRef device, SpatialAudioMode mode, int volume, List<ProfileApplyStepError> errors)
    {
        TryStep(errors, "SetDefaultOutput", device.Id, () =>
            _deviceService.SetDefault(device.Id, AudioDeviceDirection.Render));
        TryStep(errors, "SetSpatialMode", device.Id, () =>
            _spatialAudioController.SetMode(device.Id, mode));
        TryStep(errors, "SetOutputVolume", device.Id, () =>
            _volumeController.SetVolume(device.Id, AudioDeviceDirection.Render, volume));
    }

    private void ApplyInput(DeviceRef device, int volume, List<ProfileApplyStepError> errors)
    {
        TryStep(errors, "SetDefaultInput", device.Id, () =>
            _deviceService.SetDefault(device.Id, AudioDeviceDirection.Capture));
        TryStep(errors, "SetInputVolume", device.Id, () =>
            _volumeController.SetVolume(device.Id, AudioDeviceDirection.Capture, volume));
    }

    private static void TryStep(List<ProfileApplyStepError> errors, string step, string deviceId, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            errors.Add(new ProfileApplyStepError(step, deviceId, ex.Message));
        }
    }
}
