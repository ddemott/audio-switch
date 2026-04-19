using AudioSwitch.App.Services;
using AudioSwitch.Audio;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.App.Composition;

public sealed class AppHost : IDisposable
{
    public AppHost(IHotkeyService hotkeyService)
    {
        var deviceService = new CoreAudioController();
        var volumeController = new VolumeController();
        var spatialController = new SpatialAudioController();
        ProfileStore = new ProfileStore();
        ProfileManager = new ProfileManager(ProfileStore, deviceService, volumeController, spatialController);
        DeviceService = deviceService;
        VolumeController = volumeController;
        SpatialController = spatialController;
        HotkeyService = hotkeyService;

        SyncHardwareToLibrary();
        RegisterAllHotkeys();
        ProfileManager.ProfilesChanged += (_, _) => RegisterAllHotkeys();
    }

    private void SyncHardwareToLibrary()
    {
        var knownOutputs = ProfileManager.Library.Outputs.Select(o => o.DeviceId).ToHashSet();
        foreach (var device in DeviceService.GetDevices(AudioDeviceDirection.Render))
        {
            if (knownOutputs.Contains(device.Id)) continue;
            ProfileManager.AddComponent(new OutputDeviceComponent
            {
                Name = device.Name,
                DeviceId = device.Id,
                Volume = 80,
            });
        }

        var knownInputs = ProfileManager.Library.Inputs.Select(i => i.DeviceId).ToHashSet();
        foreach (var device in DeviceService.GetDevices(AudioDeviceDirection.Capture))
        {
            if (knownInputs.Contains(device.Id)) continue;
            ProfileManager.AddComponent(new InputDeviceComponent
            {
                Name = device.Name,
                DeviceId = device.Id,
                Volume = 80,
            });
        }
    }

    public IProfileStore ProfileStore { get; }

    public IProfileManager ProfileManager { get; }

    public IAudioDeviceService DeviceService { get; }

    public IVolumeController VolumeController { get; }

    public ISpatialAudioController SpatialController { get; }

    public IHotkeyService HotkeyService { get; }

    public void Dispose()
    {
        HotkeyService.Dispose();
    }

    private void RegisterAllHotkeys()
    {
        HotkeyService.UnregisterAll();
        foreach (var profile in ProfileManager.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Hotkey))
            {
                continue;
            }
            var name = profile.Name;
            HotkeyService.Register(name, profile.Hotkey, () =>
            {
                try { ProfileManager.ApplyProfile(name); }
                catch { /* graceful: missing profile race after delete is benign */ }
            });
        }
    }
}
