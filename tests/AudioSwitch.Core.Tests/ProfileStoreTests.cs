using System.IO;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _tempFile;

    public ProfileStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"audioswitch-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    // === Happy path ===

    [Fact]
    public void SaveThenLoad_RoundTripsProfile()
    {
        var store = new ProfileStore(_tempFile);
        var original = new ProfileStoreData
        {
            ActiveProfile = "Teams Call",
            Profiles =
            {
                new AudioProfile
                {
                    Name = "Teams Call",
                    Hotkey = "Ctrl+Shift+1",
                    InputDevice = new DeviceRef { Id = "mic-id", Name = "Headset Mic" },
                    OutputDevice = new DeviceRef { Id = "out-id", Name = "Headset" },
                    SpatialMode = SpatialAudioMode.Stereo,
                    InputVolume = 80,
                    OutputVolume = 65,
                },
            },
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal("Teams Call", loaded.ActiveProfile);
        var profile = Assert.Single(loaded.Profiles);
        Assert.Equal("Teams Call", profile.Name);
        Assert.Equal("Ctrl+Shift+1", profile.Hotkey);
        Assert.Equal("mic-id", profile.InputDevice?.Id);
        Assert.Equal("out-id", profile.OutputDevice?.Id);
        Assert.Equal(SpatialAudioMode.Stereo, profile.SpatialMode);
        Assert.Equal(80, profile.InputVolume);
        Assert.Equal(65, profile.OutputVolume);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var nestedPath = Path.Combine(
            Path.GetTempPath(),
            $"audioswitch-test-{Guid.NewGuid():N}",
            "profiles.json");
        try
        {
            var store = new ProfileStore(nestedPath);

            store.Save(new ProfileStoreData());

            Assert.True(File.Exists(nestedPath));
        }
        finally
        {
            var dir = Path.GetDirectoryName(nestedPath);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: profiles.json file does not exist on first run.
    /// Who: ProfileManager constructor calling store.Load() before any save has occurred.
    /// What: Load returns an empty ProfileStoreData (no profiles, null active) instead of throwing.
    /// Why: First-run users have no settings yet — throwing would crash app launch.
    /// Where: ProfileStore.Load file-existence guard before File.ReadAllText.
    /// How: Point the store at a path that has never been written and call Load.
    /// </summary>
    [Fact]
    public void Load_MissingFile_ReturnsEmptyData()
    {
        var store = new ProfileStore(_tempFile);

        var data = store.Load();

        Assert.Empty(data.Profiles);
        Assert.Null(data.ActiveProfile);
    }

    /// <summary>
    /// Sad path: profiles.json exists but is empty/whitespace (e.g., disk full mid-write before atomic replace).
    /// Who: ProfileManager constructor on second-launch after a corrupted prior write.
    /// What: Load returns empty ProfileStoreData rather than throwing JsonException.
    /// Why: An empty file is more recoverable than a crash loop — user can recreate profiles.
    /// Where: ProfileStore.Load string.IsNullOrWhiteSpace guard after ReadAllText.
    /// How: Write whitespace-only content to the path, then Load.
    /// </summary>
    [Fact]
    public void Load_EmptyFile_ReturnsEmptyData()
    {
        File.WriteAllText(_tempFile, "   \n  ");
        var store = new ProfileStore(_tempFile);

        var data = store.Load();

        Assert.Empty(data.Profiles);
        Assert.Null(data.ActiveProfile);
    }

    /// <summary>
    /// Sad path: profiles.json contains malformed JSON (manual edit gone wrong, partial write).
    /// Who: ProfileManager constructor reading store written by an external editor or interrupted process.
    /// What: Load returns empty data and renames the bad file to "{path}.corrupt-{utc-timestamp}" — does not throw.
    /// Why: Crashing app launch on parse error is unacceptable; user data is preserved as a backup for manual recovery.
    /// Where: ProfileStore.Load JsonException catch + BackupCorruptFile.
    /// How: Write invalid JSON, call Load, assert empty result and that a sibling .corrupt-* backup exists.
    /// </summary>
    [Fact]
    public void Load_CorruptJson_BacksUpAndReturnsEmpty()
    {
        File.WriteAllText(_tempFile, "{ this is not json");
        var store = new ProfileStore(_tempFile);

        var data = store.Load();

        Assert.Empty(data.Profiles);
        Assert.Null(data.ActiveProfile);
        Assert.False(File.Exists(_tempFile), "corrupt file should have been moved aside");

        var dir = Path.GetDirectoryName(_tempFile)!;
        var fileName = Path.GetFileName(_tempFile);
        var backups = Directory.GetFiles(dir, $"{fileName}.corrupt-*");
        try
        {
            Assert.Single(backups);
        }
        finally
        {
            foreach (var b in backups) File.Delete(b);
        }
    }
}
