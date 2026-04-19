using System.IO;
using AudioSwitch.Core.Interfaces;
using AudioSwitch.Core.Services;

namespace AudioSwitch.App.Services;

// Writes the AudioSwitch-managed APO config segment to disk.
//
// Two files involved:
//   1. <APO config dir>/audioswitch.txt — owned entirely by us. Contains the
//      generated header + Device blocks for whatever the current profile asks
//      for. Rewritten on every Write call (no merge with prior content).
//   2. <APO config dir>/config.txt — owned by APO and the user. We touch it
//      ONLY to ensure a single line `Include: audioswitch.txt` exists, and we
//      add it idempotently if missing. We never rewrite or remove existing
//      content from config.txt.
//
// This split means:
// - User-edited filters in config.txt survive AudioSwitch updates.
// - Removing AudioSwitch is a single-file delete (audioswitch.txt) plus
//   stripping our one Include line — no risk of clobbering user state.
// - APO loads our segment on next stream init (no audio service restart needed
//   since APO 1.2.x; the audio engine reparses on each new audio session).
public sealed class ApoConfigWriter : IApoConfigWriter
{
    public const string AudioSwitchConfigFileName = "audioswitch.txt";
    public const string IncludeDirective = $"Include: {AudioSwitchConfigFileName}";

    private readonly string _configDirectory;

    public ApoConfigWriter() : this(ApoInstallation.DefaultConfigDirectory) { }

    public ApoConfigWriter(string configDirectory)
    {
        _configDirectory = configDirectory;
    }

    public bool IsAvailable => ApoInstallation.IsInstalled(_configDirectory);

    public void Write(IReadOnlyList<ApoDeviceEntry> entries)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                "Equalizer APO is not installed; cannot write config.");
        }

        var ourFile = Path.Combine(_configDirectory, AudioSwitchConfigFileName);
        var configFile = Path.Combine(_configDirectory, ApoInstallation.ConfigFileName);

        File.WriteAllText(ourFile, ApoConfigBuilder.BuildConfig(entries));
        EnsureIncludeDirective(configFile);
    }

    public string? Snapshot()
    {
        if (!IsAvailable) return null;
        var ourFile = Path.Combine(_configDirectory, AudioSwitchConfigFileName);
        return File.Exists(ourFile) ? File.ReadAllText(ourFile) : null;
    }

    public void Restore(string? snapshot)
    {
        if (!IsAvailable) return;
        var ourFile = Path.Combine(_configDirectory, AudioSwitchConfigFileName);
        if (snapshot is null)
        {
            if (File.Exists(ourFile)) File.Delete(ourFile);
            return;
        }
        File.WriteAllText(ourFile, snapshot);
    }

    private static void EnsureIncludeDirective(string configFilePath)
    {
        var existing = File.ReadAllText(configFilePath);
        if (existing.Contains(IncludeDirective, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var separator = existing.Length > 0 && !existing.EndsWith('\n') ? Environment.NewLine : string.Empty;
        File.AppendAllText(configFilePath, separator + IncludeDirective + Environment.NewLine);
    }
}
