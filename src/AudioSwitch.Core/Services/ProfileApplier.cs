using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

internal interface IProfileApplier
{
    ProfileApplyResult Apply(AudioProfile profile, ComponentLibrary library);
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

    public ProfileApplyResult Apply(AudioProfile profile, ComponentLibrary library)
    {
        var errors = new List<ProfileApplyStepError>();
        var components = ResolveAll(profile, library, errors);
        var outputs = components.OfType<OutputDeviceComponent>().ToList();
        var inputs = components.OfType<InputDeviceComponent>().ToList();
        var spatials = components.OfType<SpatialAudioComponent>().ToList();

        foreach (var output in outputs)
        {
            ApplyOutput(profile, output, spatials, errors);
        }
        foreach (var input in inputs)
        {
            ApplyInput(profile, input, errors);
        }

        return new ProfileApplyResult(profile, errors);
    }

    private void ApplyOutput(AudioProfile profile, OutputDeviceComponent output, List<SpatialAudioComponent> spatials, List<ProfileApplyStepError> errors)
    {
        TryStep(errors, "SetDefaultOutput", output.DeviceId, () =>
            _deviceService.SetDefault(output.DeviceId, AudioDeviceDirection.Render));
        var volume = profile.ResolveVolume(output, output.Volume);
        TryStep(errors, "SetOutputVolume", output.DeviceId, () =>
            _volumeController.SetVolume(output.DeviceId, AudioDeviceDirection.Render, volume));
        foreach (var s in spatials)
        {
            TryStep(errors, "SetSpatialMode", output.DeviceId, () =>
                _spatialAudioController.SetMode(output.DeviceId, s.Mode));
        }
    }

    private void ApplyInput(AudioProfile profile, InputDeviceComponent input, List<ProfileApplyStepError> errors)
    {
        TryStep(errors, "SetDefaultInput", input.DeviceId, () =>
            _deviceService.SetDefault(input.DeviceId, AudioDeviceDirection.Capture));
        var volume = profile.ResolveVolume(input, input.Volume);
        TryStep(errors, "SetInputVolume", input.DeviceId, () =>
            _volumeController.SetVolume(input.DeviceId, AudioDeviceDirection.Capture, volume));
    }

    private static List<Component> ResolveAll(AudioProfile profile, ComponentLibrary library, List<ProfileApplyStepError> errors)
    {
        var result = new List<Component>(profile.ComponentIds.Count);
        foreach (var id in profile.ComponentIds)
        {
            var component = library.FindById(id);
            if (component is null)
            {
                errors.Add(new ProfileApplyStepError("ResolveComponent", id, $"Component '{id}' not found in library"));
                continue;
            }
            result.Add(component);
        }
        return result;
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
