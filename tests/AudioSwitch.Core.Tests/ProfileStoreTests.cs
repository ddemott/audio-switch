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
        var dir = Path.GetDirectoryName(_tempFile)!;
        var fileName = Path.GetFileName(_tempFile);
        foreach (var leftover in Directory.GetFiles(dir, $"{fileName}.corrupt-*"))
        {
            File.Delete(leftover);
        }
    }

    // === Happy path ===

    [Fact]
    public void SaveThenLoad_RoundTripsLibraryAndProfiles()
    {
        var store = new ProfileStore(_tempFile);
        var output = new OutputDeviceComponent { Name = "Headset", DeviceId = "out-id", Volume = 65 };
        var input = new InputDeviceComponent { Name = "Mic", DeviceId = "mic-id", Volume = 80 };
        var spatial = new SpatialAudioComponent { Name = "Atmos", Mode = SpatialAudioMode.DolbyAtmos };
        var library = new ComponentLibrary();
        library.Add(output);
        library.Add(input);
        library.Add(spatial);

        var original = new ProfileStoreData
        {
            ActiveProfile = "Gaming",
            Library = library,
            Profiles =
            {
                new AudioProfile
                {
                    Name = "Gaming",
                    Hotkey = "Ctrl+Shift+1",
                    ComponentIds = { output.Id, input.Id, spatial.Id },
                },
            },
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(ProfileStore.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal("Gaming", loaded.ActiveProfile);

        var loadedOutput = Assert.Single(loaded.Library.Outputs);
        Assert.Equal("Headset", loadedOutput.Name);
        Assert.Equal("out-id", loadedOutput.DeviceId);
        Assert.Equal(65, loadedOutput.Volume);

        var loadedSpatial = Assert.Single(loaded.Library.SpatialModes);
        Assert.Equal(SpatialAudioMode.DolbyAtmos, loadedSpatial.Mode);

        var loadedProfile = Assert.Single(loaded.Profiles);
        Assert.Equal(new[] { output.Id, input.Id, spatial.Id }, loadedProfile.ComponentIds);
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

    [Fact]
    public void Save_AlwaysStampsCurrentSchemaVersion()
    {
        var store = new ProfileStore(_tempFile);
        var data = new ProfileStoreData { SchemaVersion = 0 };

        store.Save(data);
        var loaded = store.Load();

        Assert.Equal(ProfileStore.CurrentSchemaVersion, loaded.SchemaVersion);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: profiles.json file does not exist on first run.
    /// Who: ProfileManager constructor calling store.Load() before any save has occurred.
    /// What: Load returns an empty ProfileStoreData (no profiles, empty library) instead of throwing.
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
        Assert.Empty(data.Library.All);
        Assert.Null(data.ActiveProfile);
    }

    /// <summary>
    /// Sad path: profiles.json exists but is empty/whitespace (interrupted prior write).
    /// Who: ProfileManager constructor on second-launch after a corrupted prior write.
    /// What: Load returns empty ProfileStoreData rather than throwing JsonException.
    /// Why: An empty file is more recoverable than a crash loop — user can recreate state.
    /// Where: ProfileStore.Load string.IsNullOrWhiteSpace guard after ReadAllText.
    /// </summary>
    [Fact]
    public void Load_EmptyFile_ReturnsEmptyData()
    {
        File.WriteAllText(_tempFile, "   \n  ");
        var store = new ProfileStore(_tempFile);

        var data = store.Load();

        Assert.Empty(data.Profiles);
    }

    /// <summary>
    /// Sad path: profiles.json contains malformed JSON (manual edit gone wrong, partial write).
    /// Who: ProfileManager constructor reading store written by an external editor or interrupted process.
    /// What: Load returns empty data and renames the bad file to "{path}.corrupt-{utc-timestamp}".
    /// Why: Crashing app launch on parse error is unacceptable; user data is preserved as a backup for manual recovery.
    /// Where: ProfileStore.Load JsonException catch + QuarantineFile.
    /// How: Write invalid JSON, call Load, assert empty result and that a sibling .corrupt-* backup exists.
    /// </summary>
    [Fact]
    public void Load_CorruptJson_BacksUpAndReturnsEmpty()
    {
        File.WriteAllText(_tempFile, "{ this is not json");
        var store = new ProfileStore(_tempFile);

        var data = store.Load();

        Assert.Empty(data.Profiles);
        Assert.False(File.Exists(_tempFile), "corrupt file should have been moved aside");
        Assert.NotEmpty(GetCorruptBackups());
    }

    /// <summary>
    /// Sad path: profiles.json is from V1 schema (no Library, profile has inline DeviceRef fields).
    /// Who: User upgrading from phase-3 to phase-4 (component-based) — their existing JSON is valid but the wrong shape.
    /// What: Load detects SchemaVersion &lt; 2, quarantines the file, returns empty. User starts fresh in V2 UI.
    /// Why: Auto-migrating V1 profiles into V2 components is non-trivial (each device would need to be deduped into a component) and gold-plating — better to preserve the V1 file as a backup and let the user rebuild deliberately. Matches existing graceful-failure stance: surface as data, don't crash.
    /// Where: ProfileStore.Load `data.SchemaVersion &lt; CurrentSchemaVersion` guard after deserialize.
    /// How: Write a structurally-valid V1 JSON (no schemaVersion field), assert empty result + quarantine backup exists.
    /// </summary>
    [Fact]
    public void Load_OldV1Schema_QuarantinesAndReturnsEmpty()
    {
        var v1Json = """
        {
          "profiles": [
            { "name": "Old", "hotkey": "Ctrl+Shift+1",
              "outputDevice": { "id": "x", "name": "Old Headset" } }
          ],
          "activeProfile": "Old"
        }
        """;
        File.WriteAllText(_tempFile, v1Json);
        var store = new ProfileStore(_tempFile);

        var data = store.Load();

        Assert.Empty(data.Profiles);
        Assert.False(File.Exists(_tempFile), "V1 file should have been quarantined");
        Assert.NotEmpty(GetCorruptBackups());
    }

    private string[] GetCorruptBackups()
    {
        var dir = Path.GetDirectoryName(_tempFile)!;
        var fileName = Path.GetFileName(_tempFile);
        return Directory.GetFiles(dir, $"{fileName}.corrupt-*");
    }
}
