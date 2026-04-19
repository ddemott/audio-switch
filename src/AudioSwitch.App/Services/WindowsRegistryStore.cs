using AudioSwitch.Core.Interfaces;
using Microsoft.Win32;

namespace AudioSwitch.App.Services;

public sealed class WindowsRegistryStore : IRegistryStore
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _subKey;

    public WindowsRegistryStore(string subKey = RunKeyPath)
    {
        _subKey = subKey;
    }

    public bool HasValue(string name) => GetValue(name) is not null;

    public string? GetValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(_subKey, writable: false);
        return key?.GetValue(name) as string;
    }

    public void SetValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_subKey, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(_subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
