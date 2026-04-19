using AudioSwitch.Core.Interfaces;

namespace AudioSwitch.Core.Tests.Fakes;

public sealed class InMemoryRegistryStore : IRegistryStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Values => _values;

    public bool HasValue(string name) => _values.ContainsKey(name);

    public string? GetValue(string name) => _values.TryGetValue(name, out var v) ? v : null;

    public void SetValue(string name, string value) => _values[name] = value;

    public void DeleteValue(string name) => _values.Remove(name);
}
