using System.Globalization;
using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class ApoConfigBuilderTests
{
    // === Happy path ===

    [Fact]
    public void BuildGraphicEqLine_TenBandFlatPreset_ProducesExpectedString()
    {
        var bands = EqualizerComponent.DefaultBands();

        var line = ApoConfigBuilder.BuildGraphicEqLine(bands);

        Assert.Equal(
            "GraphicEQ: 31 0.0; 62 0.0; 125 0.0; 250 0.0; 500 0.0; 1000 0.0; 2000 0.0; 4000 0.0; 8000 0.0; 16000 0.0",
            line);
    }

    [Fact]
    public void BuildGraphicEqLine_MixedPositiveNegativeGains_FormatsBothSigns()
    {
        var bands = new List<EqualizerBand>
        {
            new() { Frequency = 60, Gain = -3.5 },
            new() { Frequency = 1000, Gain = 0 },
            new() { Frequency = 8000, Gain = 4.5 },
        };

        var line = ApoConfigBuilder.BuildGraphicEqLine(bands);

        Assert.Equal("GraphicEQ: 60 -3.5; 1000 0.0; 8000 4.5", line);
    }

    [Fact]
    public void BuildDeviceBlock_WrapsGraphicEqWithDeviceDirective()
    {
        var bands = new List<EqualizerBand>
        {
            new() { Frequency = 1000, Gain = 2.0 },
        };

        var block = ApoConfigBuilder.BuildDeviceBlock("Speakers (Realtek Audio)", bands);

        Assert.Contains("Device: Speakers (Realtek Audio)", block);
        Assert.Contains("GraphicEQ: 1000 2.0", block);
    }

    [Fact]
    public void BuildConfig_TwoDevices_ProducesTwoDeviceBlocks()
    {
        var entries = new List<ApoDeviceEntry>
        {
            new("Speakers", new List<EqualizerBand> { new() { Frequency = 1000, Gain = 1.0 } }),
            new("Headphones", new List<EqualizerBand> { new() { Frequency = 2000, Gain = -2.0 } }),
        };

        var config = ApoConfigBuilder.BuildConfig(entries);

        Assert.StartsWith(ApoConfigBuilder.GeneratedHeader, config);
        Assert.Contains("Device: Speakers", config);
        Assert.Contains("Device: Headphones", config);
        Assert.Contains("GraphicEQ: 1000 1.0", config);
        Assert.Contains("GraphicEQ: 2000 -2.0", config);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: BuildGraphicEqLine called with an empty band list (corrupt EqualizerComponent or pre-init state).
    /// Who: A profile-apply path that lazily encounters an EqualizerComponent whose Bands list was cleared by a future feature, or by a hand-edit of profiles.json.
    /// What: Returns "GraphicEQ: " (the directive name with empty payload) — a syntactically valid no-op line that APO parses without crashing.
    /// Why: Throwing here would break ApplyProfile's TryStep wrapper for any profile that ever encountered an empty EQ. APO ignores empty filter lines, so emitting a no-op is safer than aborting.
    /// Where: ApoConfigBuilder.BuildGraphicEqLine `bands.Count == 0` early return.
    /// </summary>
    [Fact]
    public void BuildGraphicEqLine_EmptyBands_ReturnsDirectiveWithoutPayload()
    {
        var line = ApoConfigBuilder.BuildGraphicEqLine(new List<EqualizerBand>());

        Assert.Equal("GraphicEQ: ", line);
    }

    /// <summary>
    /// Sad path: gain values use comma decimal separator under non-invariant CurrentCulture (e.g. de-DE, fr-FR).
    /// Who: A user in any locale that uses comma decimals running AudioSwitch — without explicit invariant formatting, ToString("0.0") would emit "1,5" instead of "1.5".
    /// What: BuildGraphicEqLine always uses period decimals regardless of CurrentCulture.
    /// Why: APO config parsing is locale-independent and only accepts period decimals. A "1,5" gain would be silently dropped by APO, leaving the user wondering why their EQ doesn't apply. This sad-path test pins the InvariantCulture usage so a future "we don't need invariant here" refactor breaks loudly.
    /// Where: ApoConfigBuilder.BuildGraphicEqLine ToString("0.0", CultureInfo.InvariantCulture).
    /// How: Force CurrentCulture to de-DE for the duration of the call and assert the output still uses periods.
    /// </summary>
    [Fact]
    public void BuildGraphicEqLine_UnderCommaDecimalCulture_StillUsesPeriodDecimals()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var bands = new List<EqualizerBand> { new() { Frequency = 1000, Gain = 1.5 } };

            var line = ApoConfigBuilder.BuildGraphicEqLine(bands);

            Assert.Equal("GraphicEQ: 1000 1.5", line);
            Assert.DoesNotContain(",", line);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
