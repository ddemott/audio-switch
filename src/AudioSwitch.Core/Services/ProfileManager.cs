using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Services;

public sealed class ProfileManager : IProfileManager
{
    private readonly IProfileStore _store;
    private readonly IProfileApplier _applier;
    private readonly ProfileStoreData _data;

    public ProfileManager(
        IProfileStore store,
        IAudioDeviceService deviceService,
        IVolumeController volumeController,
        ISpatialAudioController spatialAudioController)
        : this(store, new ProfileApplier(deviceService, volumeController, spatialAudioController))
    {
    }

    internal ProfileManager(IProfileStore store, IProfileApplier applier)
    {
        _store = store;
        _applier = applier;
        _data = store.Load();
    }

    public ComponentLibrary Library => _data.Library;

    public IReadOnlyList<AudioProfile> Profiles => _data.Profiles;

    public AudioProfile? ActiveProfile =>
        _data.ActiveProfile is null ? null : FindProfile(_data.ActiveProfile);

    public event EventHandler? LibraryChanged;

    public event EventHandler? ProfilesChanged;

    public event EventHandler<ProfileApplyResult>? ProfileApplied;

    public bool AddComponent(Component component)
    {
        if (!_data.Library.Add(component))
        {
            return false;
        }
        _store.Save(_data);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool RemoveComponent(string componentId)
    {
        if (!_data.Library.Remove(componentId))
        {
            return false;
        }
        var changedProfiles = false;
        foreach (var profile in _data.Profiles)
        {
            if (profile.ComponentIds.RemoveAll(id => id == componentId) > 0)
            {
                changedProfiles = true;
            }
        }
        _store.Save(_data);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
        if (changedProfiles)
        {
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        return true;
    }

    public void AddProfile(AudioProfile profile)
    {
        if (FindProfile(profile.Name) is not null)
        {
            throw new InvalidOperationException($"Profile '{profile.Name}' already exists.");
        }
        _data.Profiles.Add(profile);
        PersistProfiles();
    }

    public void UpdateProfile(AudioProfile profile)
    {
        var index = _data.Profiles.FindIndex(p => p.Name == profile.Name);
        if (index < 0)
        {
            throw new InvalidOperationException($"Profile '{profile.Name}' not found.");
        }
        _data.Profiles[index] = profile;
        PersistProfiles();
    }

    public void RemoveProfile(string name)
    {
        if (_data.Profiles.RemoveAll(p => p.Name == name) == 0)
        {
            return;
        }
        if (_data.ActiveProfile == name)
        {
            _data.ActiveProfile = null;
        }
        PersistProfiles();
    }

    public void ApplyProfile(string name)
    {
        var profile = FindProfile(name)
            ?? throw new InvalidOperationException($"Profile '{name}' not found.");

        var result = _applier.Apply(profile, _data.Library);
        _data.ActiveProfile = name;
        _store.Save(_data);
        ProfileApplied?.Invoke(this, result);
    }

    private AudioProfile? FindProfile(string name) =>
        _data.Profiles.FirstOrDefault(p => p.Name == name);

    private void PersistProfiles()
    {
        _store.Save(_data);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }
}
