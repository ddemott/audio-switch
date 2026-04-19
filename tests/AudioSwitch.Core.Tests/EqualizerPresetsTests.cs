using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class EqualizerPresetsTests
{
    // === Happy path ===

    [Fact]
    public void All_ContainsAtLeastOnePresetPerCategory()
    {
        var categories = EqualizerPresets.All.Select(p => p.Category).Distinct().ToHashSet();

        Assert.Contains(EqualizerPresets.CategoryMusic, categories);
        Assert.Contains(EqualizerPresets.CategoryVideoConferencing, categories);
        Assert.Contains(EqualizerPresets.CategoryGaming, categories);
    }

    [Fact]
    public void All_EveryPresetHasTenGainsMatchingDefaultBandCount()
    {
        var bandCount = EqualizerComponent.DefaultBands().Count;

        foreach (var preset in EqualizerPresets.All)
        {
            Assert.Equal(bandCount, preset.Gains.Length);
        }
    }

    [Fact]
    public void All_EveryGainStaysWithinClippingBudget()
    {
        foreach (var preset in EqualizerPresets.All)
        {
            foreach (var g in preset.Gains)
            {
                Assert.InRange(g, -12.0, 12.0);
            }
        }
    }

    [Fact]
    public void All_PresetNamesUniqueWithinEachCategory()
    {
        foreach (var group in EqualizerPresets.All.GroupBy(p => p.Category))
        {
            var names = group.Select(p => p.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
        }
    }

    [Fact]
    public void BuildBands_ReturnsDefaultFrequenciesWithPresetGains()
    {
        var preset = EqualizerPresets.All.First(p => p.Name == "Bass boost");

        var bands = EqualizerPresets.BuildBands(preset);

        var defaultFreqs = EqualizerComponent.DefaultBands().Select(b => b.Frequency);
        Assert.Equal(defaultFreqs, bands.Select(b => b.Frequency));
        Assert.Equal(preset.Gains, bands.Select(b => b.Gain));
    }

    [Fact]
    public void BuildBands_FlatPreset_ProducesAllZeroGains()
    {
        var flat = EqualizerPresets.All.First(p => p.Name == "Flat");

        var bands = EqualizerPresets.BuildBands(flat);

        Assert.All(bands, b => Assert.Equal(0.0, b.Gain));
    }
}
