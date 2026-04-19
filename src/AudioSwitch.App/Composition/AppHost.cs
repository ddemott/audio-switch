using System.IO;
using System.Windows;
using AudioSwitch.App.Services;
using AudioSwitch.Audio;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.App.Composition;

public sealed class AppHost : IDisposable
{
    private readonly HotkeyRegistrar _hotkeyRegistrar;

    public AppHost(IHotkeyService hotkeyService, ThemeService themeService, Application application)
    {
        var baseDir = Path.GetDirectoryName(Environment.ProcessPath);
        IsPortable = PortableMode.IsActive(baseDir);

        var deviceService = new CoreAudioController();
        var volumeController = new VolumeController();
        var spatialController = new SpatialAudioController();
        ApoConfigWriter = new ApoConfigWriter();
        var profilePath = Core.Services.ProfileStore.DefaultFilePath(baseDir);
        ProfileStore = new ProfileStore(profilePath);
        ProfileManager = new ProfileManager(ProfileStore, deviceService, volumeController, spatialController, ApoConfigWriter);
        DeviceService = deviceService;
        VolumeController = volumeController;
        SpatialController = spatialController;
        HotkeyService = hotkeyService;
        ThemeService = themeService;
        StartupRegistration = new StartupRegistrationService(new WindowsRegistryStore());
        _hotkeyRegistrar = new HotkeyRegistrar(hotkeyService);

        ThemeService.Apply(ProfileManager.ThemePreference);

        SyncHardwareToLibrary();
        ReRegisterHotkeys();
        ProfileManager.ProfilesChanged += (_, _) => ReRegisterHotkeys();
    }

    public IProfileStore ProfileStore { get; }

    public IProfileManager ProfileManager { get; }

    public IAudioDeviceService DeviceService { get; }

    public IVolumeController VolumeController { get; }

    public ISpatialAudioController SpatialController { get; }

    public IHotkeyService HotkeyService { get; }

    public ThemeService ThemeService { get; }

    public StartupRegistrationService StartupRegistration { get; }

    public IApoConfigWriter ApoConfigWriter { get; }

    public bool IsPortable { get; }

    public void SetThemePreference(ThemePreference preference)
    {
        ThemeService.Apply(preference);
        ProfileManager.PersistSetting(d => d.ThemePreference = preference);
    }

    public void SetCloseBehavior(WindowCloseBehavior behavior) =>
        ProfileManager.PersistSetting(d => d.CloseBehavior = behavior);

    public bool IsStartWithWindowsEnabled => !IsPortable && StartupRegistration.IsRegistered();

    public void SetStartWithWindows(bool enabled)
    {
        if (IsPortable) return;

        if (enabled)
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path)) return;
            StartupRegistration.Register(path);
        }
        else
        {
            StartupRegistration.Unregister();
        }
    }

    public void Dispose()
    {
        HotkeyService.Dispose();
        ThemeService.Dispose();
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

    private void ReRegisterHotkeys() =>
        _hotkeyRegistrar.RegisterAll(ProfileManager.Profiles, ProfileManager.ApplyProfile);
}
