using System.IO;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class ApoInstallationTests : IDisposable
{
    private readonly string _tempDir;

    public ApoInstallationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"audioswitch-apo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // === Happy path ===

    [Fact]
    public void IsInstalled_ConfigDirAndConfigFilePresent_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, ApoInstallation.ConfigFileName), "Filter 1: ON None\n");

        Assert.True(ApoInstallation.IsInstalled(_tempDir));
    }

    [Fact]
    public void DefaultConfigDirectory_PointsAtProgramFilesEqualizerApoConfig()
    {
        var path = ApoInstallation.DefaultConfigDirectory;

        Assert.EndsWith(Path.Combine("EqualizerAPO", "config"), path);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: APO directory exists but config.txt is missing (interrupted install or partial removal).
    /// Who: User uninstalled APO via Add/Remove Programs but the empty install dir was left behind, OR APO was installed but the user hasn't launched the Configurator yet (which creates config.txt on first run).
    /// What: IsInstalled returns false — same as if the directory didn't exist at all.
    /// Why: Without config.txt, APO doesn't actually load filters, so writing to <dir>/audioswitch.txt would do nothing audible. Treating the install as absent prompts the install-helper UI to (re)run the installer, which is the right recovery.
    /// Where: ApoInstallation.IsInstalled File.Exists(Path.Combine(dir, ConfigFileName)) check.
    /// </summary>
    [Fact]
    public void IsInstalled_DirExistsButConfigFileMissing_ReturnsFalse()
    {
        // _tempDir is created but empty.
        Assert.False(ApoInstallation.IsInstalled(_tempDir));
    }

    /// <summary>
    /// Sad path: APO config directory does not exist (APO never installed).
    /// Who: First-run user on a fresh Windows install.
    /// What: IsInstalled returns false rather than throwing DirectoryNotFoundException from the File.Exists path-combine.
    /// Why: This is the default state for the vast majority of users; throwing would crash any code path that probes for APO. False is the load-bearing answer that drives the "do you want to install APO?" prompt.
    /// Where: ApoInstallation.IsInstalled Directory.Exists short-circuit before File.Exists.
    /// </summary>
    [Fact]
    public void IsInstalled_DirDoesNotExist_ReturnsFalse()
    {
        var ghostDir = Path.Combine(Path.GetTempPath(), $"audioswitch-ghost-{Guid.NewGuid():N}");

        Assert.False(ApoInstallation.IsInstalled(ghostDir));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsInstalled_NullOrWhitespacePath_ReturnsFalse(string? path)
    {
        Assert.False(ApoInstallation.IsInstalled(path));
    }
}
