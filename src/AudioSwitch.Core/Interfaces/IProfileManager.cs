using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Interfaces;

public interface IProfileManager
{
    ComponentLibrary Library { get; }

    IReadOnlyList<AudioProfile> Profiles { get; }

    AudioProfile? ActiveProfile { get; }

    event EventHandler? LibraryChanged;

    event EventHandler? ProfilesChanged;

    event EventHandler<ProfileApplyResult>? ProfileApplied;

    bool AddComponent(Component component);

    bool UpdateComponent(Component component);

    bool RemoveComponent(string componentId);

    void AddProfile(AudioProfile profile);

    void UpdateProfile(AudioProfile profile);

    void RenameProfile(string oldName, string newName);

    void RemoveProfile(string name);

    void ApplyProfile(string name);
}
