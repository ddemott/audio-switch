using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Interfaces;

public interface IProfileStore
{
    ProfileStoreData Load();

    void Save(ProfileStoreData data);
}
