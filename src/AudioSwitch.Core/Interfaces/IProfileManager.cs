using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Interfaces;

public interface IProfileManager
{
    IReadOnlyList<AudioProfile> Profiles { get; }

    AudioProfile? ActiveProfile { get; }

    event EventHandler? ProfilesChanged;

    event EventHandler<ProfileApplyResult>? ProfileApplied;

    void AddProfile(AudioProfile profile);

    void UpdateProfile(AudioProfile profile);

    void RemoveProfile(string name);

    void ApplyProfile(string name);
}
