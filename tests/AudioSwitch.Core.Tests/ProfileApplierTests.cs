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

    private static (AudioProfile Profile, ComponentLibrary Library, OutputDeviceComponent Out, InputDeviceComponent In, SpatialAudioComponent Sp) BuildFullProfile()
    {
        var output = new OutputDeviceComponent { Name = "Headset", DeviceId = "out", Volume = 40 };
        var input = new InputDeviceComponent { Name = "Mic", DeviceId = "in", Volume = 60 };
        var spatial = new SpatialAudioComponent { Name = "Atmos", Mode = SpatialAudioMode.DolbyAtmos };
        var library = new ComponentLibrary();
        library.Add(output);
        library.Add(input);
        library.Add(spatial);
        var profile = new AudioProfile
        {
            Name = "Gaming",
            ComponentIds = { output.Id, spatial.Id, input.Id },
        };
        return (profile, library, output, input, spatial);
    }

    // === Happy path ===

    [Fact]
    public void Apply_FullProfile_RunsOutputThenInputAndSetsSpatialOnOutput()
    {
        var (applier, devices, volume, spatial) = Create();
        var (profile, library, out_, in_, sp) = BuildFullProfile();

        var result = applier.Apply(profile, library);

        Assert.True(result.IsFullSuccess);
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

    [Fact]
    public void Apply_OutputOnly_SkipsInputAndSpatial()
    {
        var (applier, devices, volume, spatial) = Create();
        var output = new OutputDeviceComponent { Name = "Speakers", DeviceId = "spk", Volume = 75 };
        var library = new ComponentLibrary();
        library.Add(output);
        var profile = new AudioProfile { Name = "Music", ComponentIds = { output.Id } };

        var result = applier.Apply(profile, library);

        Assert.True(result.IsFullSuccess);
        Assert.Equal(("spk", AudioDeviceDirection.Render), Assert.Single(devices.SetDefaultCalls));
        Assert.Equal(("spk", AudioDeviceDirection.Render, 75), Assert.Single(volume.SetVolumeCalls));
        Assert.Empty(spatial.SetModeCalls);
    }

    [Fact]
    public void Apply_TwoOutputs_AppliesBothInOrder()
    {
        var (applier, devices, volume, _) = Create();
        var a = new OutputDeviceComponent { Name = "A", DeviceId = "a", Volume = 50 };
        var b = new OutputDeviceComponent { Name = "B", DeviceId = "b", Volume = 60 };
        var library = new ComponentLibrary();
        library.Add(a);
        library.Add(b);
        var profile = new AudioProfile { Name = "Both", ComponentIds = { a.Id, b.Id } };

        applier.Apply(profile, library);

        Assert.Equal(new[]
        {
            ("a", AudioDeviceDirection.Render),
            ("b", AudioDeviceDirection.Render),
        }, devices.SetDefaultCalls);
    }

    // === Sad path ===

    /// <summary>
    /// Sad path: a profile.ComponentIds entry points at an id no longer in the library (component was deleted).
    /// Who: User deleted "Heavy Bass EQ" but a profile still references its id.
    /// What: One ResolveComponent error per missing id; remaining (resolvable) components still apply.
    /// Why: Per graceful-failure standard, missing references are surfaced as data so the UI can prompt cleanup.
    /// Where: ProfileApplier.ResolveAll FindById null-coalesce-add-error branch.
    /// How: Add an output to the library, then add a profile referencing that output AND a fake "ghost" id.
    /// </summary>
    [Fact]
    public void Apply_UnknownComponentId_RecordsErrorAndAppliesRest()
    {
        var (applier, devices, _, _) = Create();
        var output = new OutputDeviceComponent { Name = "Headset", DeviceId = "out", Volume = 50 };
        var library = new ComponentLibrary();
        library.Add(output);
        var profile = new AudioProfile { Name = "Gaming", ComponentIds = { "ghost-id", output.Id } };

        var result = applier.Apply(profile, library);

        var error = Assert.Single(result.Errors);
        Assert.Equal("ResolveComponent", error.Step);
        Assert.Equal("ghost-id", error.DeviceId);
        Assert.Equal(("out", AudioDeviceDirection.Render), Assert.Single(devices.SetDefaultCalls));
    }

    /// <summary>
    /// Sad path: profile has a SpatialAudioComponent but no OutputDeviceComponent.
    /// Who: User built a profile with only an EQ + spatial setting, intending it as a "preset overlay" — but applying without an output makes no sense.
    /// What: Spatial setter is never called (no output to apply to); no error recorded — silent skip.
    /// Why: Spatial format is per-output-device in the Windows API. With no output in the profile, applying spatial would require choosing a target arbitrarily, which would surprise the user.
    /// Where: ProfileApplier nests the spatial loop inside the output loop, so empty outputs short-circuit.
    /// How: Profile contains only a SpatialAudioComponent; assert no spatial calls.
    /// </summary>
    [Fact]
    public void Apply_SpatialWithoutOutput_SilentlySkipsSpatial()
    {
        var (applier, _, _, spatial) = Create();
        var sp = new SpatialAudioComponent { Name = "Atmos", Mode = SpatialAudioMode.DolbyAtmos };
        var library = new ComponentLibrary();
        library.Add(sp);
        var profile = new AudioProfile { Name = "Stranded spatial", ComponentIds = { sp.Id } };

        var result = applier.Apply(profile, library);

        Assert.True(result.IsFullSuccess);
        Assert.Empty(spatial.SetModeCalls);
    }

    /// <summary>
    /// Sad path: SetDefault for the output throws (device unplugged mid-apply).
    /// Who: User triggers a hotkey for a profile whose output device just disconnected.
    /// What: SetDefaultOutput is recorded as an error; later steps (volume, spatial, input) still execute.
    /// Why: Partial application is more useful than total bailout; ProfileManager keeps the profile active and the UI surfaces the error.
    /// Where: ProfileApplier.TryStep wraps each controller call individually.
    /// </summary>
    [Fact]
    public void Apply_SetDefaultOutputThrows_RecordsErrorAndContinues()
    {
        var (applier, devices, volume, spatial) = Create();
        var (profile, library, _, _, _) = BuildFullProfile();
        devices.OnSetDefault = (_, dir) =>
        {
            if (dir == AudioDeviceDirection.Render) throw new InvalidOperationException("device unplugged");
        };

        var result = applier.Apply(profile, library);

        Assert.False(result.IsFullSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("SetDefaultOutput", error.Step);
        Assert.Equal("out", error.DeviceId);
        // Later steps still ran
        Assert.Contains(("out", AudioDeviceDirection.Render, 40), volume.SetVolumeCalls);
        Assert.Equal(("out", SpatialAudioMode.DolbyAtmos), Assert.Single(spatial.SetModeCalls));
        Assert.Contains(("in", AudioDeviceDirection.Capture), devices.SetDefaultCalls);
    }

    /// <summary>
    /// Sad path: profile.ComponentIds is empty.
    /// Who: A freshly-created profile that the user hasn't linked anything to yet.
    /// What: Applier returns a no-error result; no calls made.
    /// Why: An empty profile is a valid intermediate state — the user is mid-construction.
    /// Where: ProfileApplier loops never iterate when component lists are empty.
    /// </summary>
    [Fact]
    public void Apply_NoComponents_NoCallsNoErrors()
    {
        var (applier, devices, volume, spatial) = Create();
        var library = new ComponentLibrary();
        var profile = new AudioProfile { Name = "Empty" };

        var result = applier.Apply(profile, library);

        Assert.True(result.IsFullSuccess);
        Assert.Empty(devices.SetDefaultCalls);
        Assert.Empty(volume.SetVolumeCalls);
        Assert.Empty(spatial.SetModeCalls);
    }

    // === Per-profile volume override ===

    [Fact]
    public void Apply_OutputVolumeOverride_UsesOverrideNotComponentDefault()
    {
        var (applier, _, volume, _) = Create();
        var output = new OutputDeviceComponent { Name = "Speakers", DeviceId = "spk", Volume = 80 };
        var library = new ComponentLibrary();
        library.Add(output);
        var profile = new AudioProfile
        {
            Name = "Late night",
            ComponentIds = { output.Id },
            ComponentVolumes = { [output.Id] = 30 },
        };

        applier.Apply(profile, library);

        Assert.Equal(("spk", AudioDeviceDirection.Render, 30), Assert.Single(volume.SetVolumeCalls));
    }

    [Fact]
    public void Apply_InputVolumeOverride_UsesOverrideNotComponentDefault()
    {
        var (applier, _, volume, _) = Create();
        var input = new InputDeviceComponent { Name = "Mic", DeviceId = "mic", Volume = 60 };
        var library = new ComponentLibrary();
        library.Add(input);
        var profile = new AudioProfile
        {
            Name = "Loud calls",
            ComponentIds = { input.Id },
            ComponentVolumes = { [input.Id] = 95 },
        };

        applier.Apply(profile, library);

        Assert.Equal(("mic", AudioDeviceDirection.Capture, 95), Assert.Single(volume.SetVolumeCalls));
    }

    [Fact]
    public void Apply_NoOverride_FallsBackToComponentDefaultVolume()
    {
        var (applier, _, volume, _) = Create();
        var output = new OutputDeviceComponent { Name = "Speakers", DeviceId = "spk", Volume = 80 };
        var library = new ComponentLibrary();
        library.Add(output);
        var profile = new AudioProfile { Name = "Default", ComponentIds = { output.Id } };

        applier.Apply(profile, library);

        Assert.Equal(("spk", AudioDeviceDirection.Render, 80), Assert.Single(volume.SetVolumeCalls));
    }

    [Fact]
    public void Apply_TwoProfilesSameDeviceDifferentOverrides_SendDifferentVolumes()
    {
        var (applier, _, volume, _) = Create();
        var output = new OutputDeviceComponent { Name = "Speakers", DeviceId = "spk", Volume = 80 };
        var library = new ComponentLibrary();
        library.Add(output);
        var quiet = new AudioProfile
        {
            Name = "Quiet",
            ComponentIds = { output.Id },
            ComponentVolumes = { [output.Id] = 20 },
        };
        var loud = new AudioProfile
        {
            Name = "Loud",
            ComponentIds = { output.Id },
            ComponentVolumes = { [output.Id] = 100 },
        };

        applier.Apply(quiet, library);
        applier.Apply(loud, library);

        Assert.Equal(2, volume.SetVolumeCalls.Count);
        Assert.Equal(20, volume.SetVolumeCalls[0].Volume);
        Assert.Equal(100, volume.SetVolumeCalls[1].Volume);
    }

    /// <summary>
    /// Sad path: ComponentVolumes contains an entry whose id is no longer in profile.ComponentIds (orphaned override).
    /// Who: User edited the profile to drop a component but the volume override entry was left behind (cleanup not strict).
    /// What: Orphan is silently ignored — no SetVolume call for the orphaned id, no error recorded.
    /// Why: An override without the matching ComponentId is meaningless — there's no device step to attach it to. Surfacing it as an error would just be noise; the next profile-edit can prune it.
    /// Where: ProfileApplier iterates components from ComponentIds only; ComponentVolumes is consulted as a lookup, never enumerated.
    /// How: Override map contains an extra ghost id alongside the active one; only the active id results in a SetVolume call.
    /// </summary>
    [Fact]
    public void Apply_OrphanedOverride_SilentlyIgnored()
    {
        var (applier, _, volume, _) = Create();
        var output = new OutputDeviceComponent { Name = "Speakers", DeviceId = "spk", Volume = 80 };
        var library = new ComponentLibrary();
        library.Add(output);
        var profile = new AudioProfile
        {
            Name = "Has orphan",
            ComponentIds = { output.Id },
            ComponentVolumes =
            {
                [output.Id] = 55,
                ["ghost-deleted-id"] = 12,
            },
        };

        var result = applier.Apply(profile, library);

        Assert.True(result.IsFullSuccess);
        Assert.Equal(("spk", AudioDeviceDirection.Render, 55), Assert.Single(volume.SetVolumeCalls));
    }
}
