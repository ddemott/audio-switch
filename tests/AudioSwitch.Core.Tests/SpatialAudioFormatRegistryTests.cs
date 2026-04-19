using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;

namespace AudioSwitch.Core.Tests;

public sealed class SpatialAudioFormatRegistryTests
{
    [Theory]
    [InlineData(SpatialAudioMode.Stereo, 0)]
    [InlineData(SpatialAudioMode.WindowsSonic, 1)]
    [InlineData(SpatialAudioMode.DolbyAtmos, 2)]
    [InlineData(SpatialAudioMode.ThxSpatial, 3)]
    public void GetFormatId_KnownModes_ReturnExpectedDwordValue(SpatialAudioMode mode, int expected)
    {
        Assert.Equal(expected, SpatialAudioFormatRegistry.GetFormatId(mode));
    }

    [Theory]
    [InlineData(0, SpatialAudioMode.Stereo)]
    [InlineData(1, SpatialAudioMode.WindowsSonic)]
    [InlineData(2, SpatialAudioMode.DolbyAtmos)]
    [InlineData(3, SpatialAudioMode.ThxSpatial)]
    public void FromFormatId_KnownIds_RoundTripToMode(int id, SpatialAudioMode expected)
    {
        Assert.Equal(expected, SpatialAudioFormatRegistry.FromFormatId(id));
    }

    [Theory]
    [InlineData(SpatialAudioMode.Stereo)]
    [InlineData(SpatialAudioMode.WindowsSonic)]
    [InlineData(SpatialAudioMode.DolbyAtmos)]
    [InlineData(SpatialAudioMode.ThxSpatial)]
    public void RoundTrip_ModeToIdAndBack_PreservesMode(SpatialAudioMode mode)
    {
        var id = SpatialAudioFormatRegistry.GetFormatId(mode);
        Assert.Equal(mode, SpatialAudioFormatRegistry.FromFormatId(id));
    }

    /// <summary>
    /// Sad path: GetFormatId is called with an enum value outside the defined SpatialAudioMode set.
    /// Who: A future enum addition that someone forgot to map, or a corrupted profiles.json that deserialized to an undefined int cast.
    /// What: Returns the Off/stereo format id (0) instead of throwing.
    /// Why: Throwing here would break ApplyProfile via the TryStep wrapper — better to silently fall back to stereo (the safest no-op spatial setting) and let the visible "I picked Atmos but I'm getting stereo" surface the bug naturally.
    /// Where: SpatialAudioFormatRegistry.GetFormatId switch default arm.
    /// How: Cast an out-of-range int to the enum and assert the off-format-id comes back.
    /// </summary>
    [Fact]
    public void GetFormatId_UnknownModeValue_FallsBackToOff()
    {
        var unknown = (SpatialAudioMode)999;

        Assert.Equal(SpatialAudioFormatRegistry.OffFormatId, SpatialAudioFormatRegistry.GetFormatId(unknown));
    }

    /// <summary>
    /// Sad path: FromFormatId reads an unknown DWORD value from the property store (third-party app registered a new spatial format AudioSwitch hasn't been taught about, or the value was clobbered).
    /// Who: ProfileManager surfacing the device's current spatial mode in the UI on a machine with an unfamiliar codec installed.
    /// What: Returns Stereo as the safe display value rather than throwing or invented enum values.
    /// Why: The UI just needs *something* renderable. Surfacing it as Stereo is honest ("we don't know what's set") and doesn't crash the device-state read path.
    /// Where: SpatialAudioFormatRegistry.FromFormatId switch default arm.
    /// How: Pass an unfamiliar id and assert Stereo.
    /// </summary>
    [Fact]
    public void FromFormatId_UnknownDwordValue_FallsBackToStereo()
    {
        Assert.Equal(SpatialAudioMode.Stereo, SpatialAudioFormatRegistry.FromFormatId(99));
    }
}
