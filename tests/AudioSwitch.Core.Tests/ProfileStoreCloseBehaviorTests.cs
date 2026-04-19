using System.IO;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class ProfileStoreCloseBehaviorTests : IDisposable
{
    private readonly string _tempFile;

    public ProfileStoreCloseBehaviorTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"audioswitch-closebehavior-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    // === Happy path ===

    [Fact]
    public void CloseBehavior_RoundTripsThroughStore()
    {
        var store = new ProfileStore(_tempFile);
        store.Save(new ProfileStoreData { CloseBehavior = WindowCloseBehavior.MinimizeToTray });

        var loaded = store.Load();

        Assert.Equal(WindowCloseBehavior.MinimizeToTray, loaded.CloseBehavior);
    }

    [Fact]
    public void CloseBehavior_DefaultsToPromptForNewStore()
    {
        var store = new ProfileStore(_tempFile);

        var loaded = store.Load();

        Assert.Equal(WindowCloseBehavior.Prompt, loaded.CloseBehavior);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: store file was written before CloseBehavior property existed (forward-compat load).
    /// Who: Existing v2 user upgrading to the build that adds CloseBehavior; their profiles.json has no `closeBehavior` key.
    /// What: Load returns data with CloseBehavior == Prompt (the C# default) and does not quarantine.
    /// Why: Adding a field without a schema bump must be backward-compatible. Quarantining would wipe user data on upgrade.
    /// Where: ProfileStoreData.CloseBehavior default initializer + System.Text.Json's tolerant handling of missing keys at schemaVersion == CurrentSchemaVersion.
    /// How: Write a minimal schema-current JSON that omits closeBehavior; load; assert Prompt default.
    /// </summary>
    [Fact]
    public void Load_SchemaCurrentButMissingCloseBehavior_DefaultsToPrompt()
    {
        File.WriteAllText(_tempFile, $$"""
        {
          "schemaVersion": {{ProfileStore.CurrentSchemaVersion}},
          "library": { "outputs": [], "inputs": [], "spatialModes": [], "equalizers": [] },
          "profiles": []
        }
        """);

        var store = new ProfileStore(_tempFile);
        var loaded = store.Load();

        Assert.Equal(WindowCloseBehavior.Prompt, loaded.CloseBehavior);
        Assert.True(File.Exists(_tempFile), "valid schema-current file must not be quarantined");
    }
}
