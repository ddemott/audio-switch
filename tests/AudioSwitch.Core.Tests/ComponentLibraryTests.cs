using AudioSwitch.Core.Models;

namespace AudioSwitch.Core.Tests;

public sealed class ComponentLibraryTests
{
    private static OutputDeviceComponent NewOutput(string name = "Headphones") =>
        new() { Name = name, DeviceId = "dev-out-1", Volume = 70 };

    private static EqualizerComponent NewEqualizer(string name = "Heavy Bass") =>
        new() { Name = name, ApoConfigPath = @"C:\eq\bass.txt" };

    // === Happy path ===

    [Fact]
    public void Add_DistinctComponents_AllStoredInTypedLists()
    {
        var lib = new ComponentLibrary();
        var output = NewOutput();
        var eq = NewEqualizer();
        var spatial = new SpatialAudioComponent { Name = "Atmos", Mode = SpatialAudioMode.DolbyAtmos };
        var input = new InputDeviceComponent { Name = "Mic", DeviceId = "mic-1", Volume = 60 };

        Assert.True(lib.Add(output));
        Assert.True(lib.Add(eq));
        Assert.True(lib.Add(spatial));
        Assert.True(lib.Add(input));

        Assert.Single(lib.Outputs);
        Assert.Single(lib.Inputs);
        Assert.Single(lib.SpatialModes);
        Assert.Single(lib.Equalizers);
    }

    [Fact]
    public void FindById_ReturnsCorrectComponentRegardlessOfType()
    {
        var lib = new ComponentLibrary();
        var output = NewOutput();
        var eq = NewEqualizer();
        lib.Add(output);
        lib.Add(eq);

        Assert.Same(output, lib.FindById(output.Id));
        Assert.Same(eq, lib.FindById(eq.Id));
    }

    [Fact]
    public void All_EnumeratesEveryComponentAcrossAllTypes()
    {
        var lib = new ComponentLibrary();
        lib.Add(NewOutput());
        lib.Add(NewEqualizer());
        lib.Add(new InputDeviceComponent { Name = "Mic" });

        Assert.Equal(3, lib.All.Count());
    }

    [Fact]
    public void Remove_ExistingComponent_DeletesFromCorrectList()
    {
        var lib = new ComponentLibrary();
        var output = NewOutput();
        lib.Add(output);

        Assert.True(lib.Remove(output.Id));
        Assert.Empty(lib.Outputs);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: Add called twice with the same component instance (or same id).
    /// Who: A retry of "save component" after a network/UI hiccup, or a duplicate "import" pass.
    /// What: Second Add returns false; the typed list still contains exactly one entry.
    /// Why: Component IDs are the join key for profile.ComponentIds — duplicating an id breaks the lookup contract (FindById would only ever return the first match).
    /// Where: ComponentLibrary.Add `FindById is not null` guard.
    /// How: Add the same component twice and assert false + count of 1.
    /// </summary>
    [Fact]
    public void Add_DuplicateId_ReturnsFalseAndDoesNotInsertTwice()
    {
        var lib = new ComponentLibrary();
        var output = NewOutput();

        Assert.True(lib.Add(output));
        Assert.False(lib.Add(output));
        Assert.Single(lib.Outputs);
    }

    /// <summary>
    /// Sad path: FindById called with an id that was never added (or was removed).
    /// Who: ProfileApplier resolving a profile.ComponentIds entry whose component was deleted.
    /// What: Returns null (not throw) so caller can skip + record a "missing component" error.
    /// Why: Per the graceful-failure standard, missing references are surfaced as data, not exceptions.
    /// Where: ComponentLibrary.FindById uses FirstOrDefault, which is null-safe.
    /// </summary>
    [Fact]
    public void FindById_UnknownId_ReturnsNull()
    {
        var lib = new ComponentLibrary();
        lib.Add(NewOutput());

        Assert.Null(lib.FindById("ghost-id"));
    }

    /// <summary>
    /// Sad path: Remove called with an id that does not exist.
    /// Who: Stale UI deleting a component that was already removed in another pane.
    /// What: Returns false; library is unchanged.
    /// Why: Idempotent removal is more useful than throwing — caller doesn't need a try/catch.
    /// Where: ComponentLibrary.Remove RemoveAll-returns-zero short-circuit at end.
    /// </summary>
    [Fact]
    public void Remove_UnknownId_ReturnsFalse()
    {
        var lib = new ComponentLibrary();
        lib.Add(NewOutput());

        Assert.False(lib.Remove("ghost-id"));
        Assert.Single(lib.Outputs);
    }
}
