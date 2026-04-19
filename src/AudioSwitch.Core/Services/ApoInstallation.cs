using System.IO;

namespace AudioSwitch.Core.Services;

// Probes whether Equalizer APO is installed on this machine.
//
// APO installs to C:\Program Files\EqualizerAPO\ by default; the per-device
// config files live in <install>\config\. Presence of config\config.txt is the
// canonical "APO is here and initialized" signal — the installer creates it on
// first run, and APO won't load filters without it.
//
// Detection is intentionally a file probe (not a registry read): APO does
// register an entry in HKLM, but the registry path varies by APO version and
// 32/64-bit install variant. The config dir is stable across versions and is
// also exactly the directory we need to write to anyway, so we may as well
// use it as the single source of truth.
public static class ApoInstallation
{
    public static string DefaultConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "EqualizerAPO",
            "config");

    public const string ConfigFileName = "config.txt";

    public static bool IsInstalled() => IsInstalled(DefaultConfigDirectory);

    public static bool IsInstalled(string? configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory)) return false;
        if (!Directory.Exists(configDirectory)) return false;
        return File.Exists(Path.Combine(configDirectory, ConfigFileName));
    }
}
