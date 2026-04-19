using AudioSwitch.Core.Interfaces;

namespace AudioSwitch.Core.Services;

public sealed class StartupRegistrationService
{
    public const string DefaultValueName = "AudioSwitch";

    public const string StartupArg = "--startup";

    private readonly IRegistryStore _store;
    private readonly string _valueName;

    public StartupRegistrationService(IRegistryStore store, string valueName = DefaultValueName)
    {
        _store = store;
        _valueName = valueName;
    }

    public bool IsRegistered() => _store.HasValue(_valueName);

    public void Register(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }
        var commandLine = $"\"{executablePath}\" {StartupArg}";
        _store.SetValue(_valueName, commandLine);
    }

    public void Unregister()
    {
        if (_store.HasValue(_valueName))
        {
            _store.DeleteValue(_valueName);
        }
    }

    public static bool IsStartupLaunch(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], StartupArg, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
