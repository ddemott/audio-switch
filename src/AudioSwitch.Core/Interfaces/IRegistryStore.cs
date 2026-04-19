namespace AudioSwitch.Core.Interfaces;

public interface IRegistryStore
{
    bool HasValue(string name);

    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name);
}
