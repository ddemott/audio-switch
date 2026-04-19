using AudioSwitch.Core.Models;
using AudioSwitch.Core.Services;
using AudioSwitch.Core.Tests.Fakes;

namespace AudioSwitch.Core.Tests;

public sealed class ProfileApplierTests
{
    private static (ProfileApplier Applier, FakeAudioDeviceService Devices, FakeVolumeController Volume, FakeSpatialAudioController Spatial) Create()
    {
        var devices = new FakeAudioDeviceService();
        var volume = new FakeVolumeController();
        var spatial = new FakeSpatialAudioController();
        return (new ProfileApplier(devices, volume, spatial), devices, volume, spatial);
    }

    private static AudioProfile FullProfile() => new()
    {
        Name = "x",
        OutputDevice = new DeviceRef { Id = "out", Name = "Headset" },
        InputDevice = new DeviceRef { Id = "in", Name = "Mic" },
        SpatialMode = SpatialAudioMode.DolbyAtmos,
        OutputVolume = 40,
        InputVolume = 60,
    };

    // === Happy path ===

    [Fact]
    public void Apply_OutputBeforeInput_AndSetsSpatialBeforeVolume()
    {
        var (applier, devices, volume, spatial) = Create();

        var result = applier.Apply(FullProfile());

        Assert.True(result.IsFullSuccess);
        Assert.Empty(result.Errors);

        Assert.Equal(new[]
        {
            ("out", AudioDeviceDirection.Render),
            ("in", AudioDeviceDirection.Capture),
        }, devices.SetDefaultCalls);

        Assert.Equal(new[]
        {
            ("out", AudioDeviceDirection.Render, 40),
            ("in", AudioDeviceDirection.Capture, 60),
        }, volume.SetVolumeCalls);

        Assert.Equal(("out", SpatialAudioMode.DolbyAtmos), Assert.Single(spatial.SetModeCalls));
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: profile has no OutputDevice (input-only profile, e.g., podcaster mic switch).
    /// Who: ProfileManager.ApplyProfile passing a partially-configured profile to the applier.
    /// What: Applier skips render-side calls (SetDefault Render and SpatialMode) entirely; only capture-side runs; result has no errors.
    /// Why: Spatial mode is only meaningful for an output device — applying it to a non-existent device would crash COM.
    /// Where: ProfileApplier null-pattern guard on profile.OutputDevice.
    /// </summary>
    [Fact]
    public void Apply_NullOutput_SkipsRenderAndSpatial()
    {
        var (applier, devices, volume, spatial) = Create();
        var profile = new AudioProfile
        {
            Name = "x",
            InputDevice = new DeviceRef { Id = "in", Name = "Mic" },
            InputVolume = 60,
        };

        var result = applier.Apply(profile);

        Assert.True(result.IsFullSuccess);
        Assert.Equal(("in", AudioDeviceDirection.Capture), Assert.Single(devices.SetDefaultCalls));
        Assert.Equal(("in", AudioDeviceDirection.Capture, 60), Assert.Single(volume.SetVolumeCalls));
        Assert.Empty(spatial.SetModeCalls);
    }

    /// <summary>
    /// Sad path: profile has no InputDevice (output-only profile, e.g., music listening).
    /// Who: ProfileManager.ApplyProfile passing an output-only profile to the applier.
    /// What: Applier skips capture-side calls; render and spatial run normally; result has no errors.
    /// Why: Forcing an input device on a music profile would steal the user's mic from another app.
    /// Where: ProfileApplier null-pattern guard on profile.InputDevice.
    /// </summary>
    [Fact]
    public void Apply_NullInput_SkipsCapture()
    {
        var (applier, devices, volume, spatial) = Create();
        var profile = new AudioProfile
        {
            Name = "x",
            OutputDevice = new DeviceRef { Id = "out", Name = "Headset" },
            SpatialMode = SpatialAudioMode.WindowsSonic,
            OutputVolume = 80,
        };

        var result = applier.Apply(profile);

        Assert.True(result.IsFullSuccess);
        Assert.Equal(("out", AudioDeviceDirection.Render), Assert.Single(devices.SetDefaultCalls));
        Assert.Equal(("out", AudioDeviceDirection.Render, 80), Assert.Single(volume.SetVolumeCalls));
        Assert.Equal(("out", SpatialAudioMode.WindowsSonic), Assert.Single(spatial.SetModeCalls));
    }

    /// <summary>
    /// Sad path: profile has neither InputDevice nor OutputDevice.
    /// Who: ProfileManager.ApplyProfile passing an empty profile (placeholder created via "New" in dashboard).
    /// What: Applier makes zero calls and returns a no-error result.
    /// Why: An empty profile is a legal V1 state — applying it should be a silent no-op.
    /// Where: Both null-pattern guards in ProfileApplier short-circuit.
    /// </summary>
    [Fact]
    public void Apply_BothNull_NoCalls()
    {
        var (applier, devices, volume, spatial) = Create();

        var result = applier.Apply(new AudioProfile { Name = "empty" });

        Assert.True(result.IsFullSuccess);
        Assert.Empty(devices.SetDefaultCalls);
        Assert.Empty(volume.SetVolumeCalls);
        Assert.Empty(spatial.SetModeCalls);
    }

    /// <summary>
    /// Sad path: a single step throws (output device unplugged mid-apply, COM HRESULT failure).
    /// Who: User triggers a profile apply for a saved device that no longer exists.
    /// What: Failing step is recorded in ProfileApplyResult.Errors with Step name + DeviceId + message; remaining steps still execute.
    /// Why: Partial application is better than total bailout — user may have unplugged only the output, input is still useful.
    /// Where: ProfileApplier.TryStep wraps each device/volume/spatial call individually.
    /// How: Inject a throwing OnSetDefault for Render only; assert the input chain still ran and Errors lists the output failure.
    /// </summary>
    [Fact]
    public void Apply_SetDefaultOutputThrows_RecordsErrorAndContinuesWithInput()
    {
        var (applier, devices, volume, spatial) = Create();
        devices.OnSetDefault = (_, dir) =>
        {
            if (dir == AudioDeviceDirection.Render)
            {
                throw new InvalidOperationException("device unplugged");
            }
        };

        var result = applier.Apply(FullProfile());

        Assert.False(result.IsFullSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("SetDefaultOutput", error.Step);
        Assert.Equal("out", error.DeviceId);
        Assert.Contains("device unplugged", error.Message);

        // Render-side later steps still ran (they may also fail in real life, but applier doesn't short-circuit).
        Assert.Equal(("out", SpatialAudioMode.DolbyAtmos), Assert.Single(spatial.SetModeCalls));
        Assert.Contains(("out", AudioDeviceDirection.Render, 40), volume.SetVolumeCalls);
        // Input-side fully ran.
        Assert.Contains(("in", AudioDeviceDirection.Capture), devices.SetDefaultCalls);
        Assert.Contains(("in", AudioDeviceDirection.Capture, 60), volume.SetVolumeCalls);
    }

    /// <summary>
    /// Sad path: every step throws (catastrophic — audio subsystem not available).
    /// Who: Profile applied right after a Windows audio service crash.
    /// What: All five steps are recorded in Errors; applier still returns a result rather than throwing.
    /// Why: ProfileManager needs the result to decide whether to mark the profile active and notify the user.
    /// Where: ProfileApplier.TryStep individual catches around every controller call.
    /// </summary>
    [Fact]
    public void Apply_AllStepsThrow_CollectsAllErrors()
    {
        var (applier, devices, volume, spatial) = Create();
        devices.OnSetDefault = (_, _) => throw new Exception("dev fail");
        volume.OnSetVolume = (_, _, _) => throw new Exception("vol fail");
        spatial.OnSetMode = (_, _) => throw new Exception("sp fail");

        var result = applier.Apply(FullProfile());

        Assert.Equal(5, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Step == "SetDefaultOutput");
        Assert.Contains(result.Errors, e => e.Step == "SetSpatialMode");
        Assert.Contains(result.Errors, e => e.Step == "SetOutputVolume");
        Assert.Contains(result.Errors, e => e.Step == "SetDefaultInput");
        Assert.Contains(result.Errors, e => e.Step == "SetInputVolume");
    }
}
