using System.IO;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class EqualizerComponentTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"audioswitch-eq-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    // === Happy path ===

    [Fact]
    public void New_ProvidesStandardTenBandLayoutAtZeroDb()
    {
        var eq = new EqualizerComponent { Name = "Flat" };

        Assert.Equal(
            new[] { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 },
            eq.Bands.Select(b => b.Frequency));
        Assert.All(eq.Bands, b => Assert.Equal(0.0, b.Gain));
    }

    [Fact]
    public void Roundtrip_PreservesBandValues()
    {
        var store = new ProfileStore(_tempFile);
        var eq = new EqualizerComponent { Name = "Heavy Bass" };
        eq.Bands[0].Gain = 8.5;
        eq.Bands[1].Gain = 6.0;
        eq.Bands[9].Gain = -3.5;
        var data = new ProfileStoreData();
        data.Library.Add(eq);

        store.Save(data);
        var loaded = store.Load();

        var loadedEq = Assert.Single(loaded.Library.Equalizers);
        Assert.Equal("Heavy Bass", loadedEq.Name);
        Assert.Equal(8.5, loadedEq.Bands[0].Gain);
        Assert.Equal(6.0, loadedEq.Bands[1].Gain);
        Assert.Equal(-3.5, loadedEq.Bands[9].Gain);
    }
}
